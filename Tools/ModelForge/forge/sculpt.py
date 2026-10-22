"""Sculpture par champs de distance : formes organiques (muscles, os, chairs) décrites par des primitives (cônes arrondis,
ellipsoïdes) fondues par union lisse, détaillées par un bruit de surface, puis maillées (marching cubes), lissées
(Taubin) et skinnées automatiquement : chaque primitive appartient à un os, et le poids d'un sommet pour un os est la
contribution lisse des primitives de cet os à la surface en ce point (même noyau que l'union lisse).
"""
import numpy as np
from skimage.measure import marching_cubes
from scipy.ndimage import gaussian_filter
from .geo import Mesh, vertex_normals, unit


class Prim:
    def __init__(self, kind, bone, k=None, sub=False, **p):
        self.kind, self.bone, self.k, self.sub, self.p = kind, bone, k, sub, p

    def dist(self, X):
        p = self.p
        if self.kind == 'cone':        # cône arrondi (capsule à rayons différents)
            a, b, ra, rb = p['a'], p['b'], p['ra'], p['rb']
            ba = b - a; L2 = ba @ ba
            pa = X - a
            h = np.clip((pa @ ba) / L2, 0, 1)
            r = ra + (rb - ra) * h
            q = pa - h[:, None] * ba[None]
            sq = p.get('squash')                              # aplatissement (rapport, axe)
            if sq is not None:
                ax = sq[1]; comp = q @ ax
                q = q + (comp / sq[0] - comp)[:, None] * ax[None]
            return np.linalg.norm(q, axis=1) - r
        if self.kind == 'ell':         # ellipsoïde (distance approchée)
            c, R, rad = p['c'], p['R'], p['r']
            q = (X - c) @ R / rad
            k0 = np.linalg.norm(q, axis=1)
            k1 = np.linalg.norm(q / rad, axis=1)
            return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
        raise ValueError(self.kind)

    def bbox(self, pad):
        p = self.p
        if self.kind == 'cone':
            r = max(p['ra'], p['rb'])
            lo = np.minimum(p['a'], p['b']) - r - pad; hi = np.maximum(p['a'], p['b']) + r + pad
        else:
            r = np.max(p['r']); lo = p['c'] - r - pad; hi = p['c'] + r + pad
        return lo, hi


def rot_from(fwd, up=(0, 1, 0)):
    """Matrice dont les colonnes sont (x, y, z) locaux : z = fwd, y ≈ up."""
    z = unit(np.asarray(fwd, float)); x = np.cross(np.asarray(up, float), z)
    if np.linalg.norm(x) < 1e-6: x = np.cross((1, 0, 0), z)
    x = unit(x); y = np.cross(z, x)
    return np.stack([x, y, z], 1)


class Sculpt:
    def __init__(self, k=0.020):
        self.prims, self.k = [], k

    def cone(self, bone, a, b, ra, rb, k=None, squash=None, sub=False):
        sq = None if squash is None else (squash[0], unit(np.asarray(squash[1], float)))
        self.prims.append(Prim('cone', bone, k, sub, a=np.asarray(a, float), b=np.asarray(b, float), ra=ra, rb=rb, squash=sq)); return self

    def ell(self, bone, c, r, fwd=(0, 0, 1), up=(0, 1, 0), k=None, sub=False):
        self.prims.append(Prim('ell', bone, k, sub, c=np.asarray(c, float), r=np.asarray(r, float), R=rot_from(fwd, up))); return self

    def mirror(self, sw=lambda b: b):
        """Ajoute le symétrique (X → −X) des primitives dont un point est à x > 0.002."""
        add = []
        for q in self.prims:
            p = dict(q.p)
            if q.kind == 'cone':
                if max(abs(p['a'][0]), abs(p['b'][0])) < 0.002: continue
                p['a'] = p['a'] * (-1, 1, 1); p['b'] = p['b'] * (-1, 1, 1)
                if p.get('squash') is not None: p['squash'] = (p['squash'][0], p['squash'][1] * (-1, 1, 1))
            else:
                if abs(p['c'][0]) < 0.002: continue
                p['c'] = p['c'] * (-1, 1, 1); R = p['R'].copy(); R[0, :] *= -1; R[:, 0] *= -1; p['R'] = R
            add.append(Prim(q.kind, sw(q.bone), q.k, q.sub, **p))
        self.prims += add; return self

    # --- champ
    def field(self, X, detail=None):
        """Union lisse exponentielle ; les primitives « sub » creusent (différence lisse)."""
        acc = np.zeros(len(X)); sub = np.full(len(X), np.inf)
        for q in self.prims:
            d = q.dist(X)
            if q.sub: sub = np.minimum(sub, d); continue
            k = q.k or self.k
            acc += np.exp(-np.clip(d, -0.5, 1.0) / k)
        D = -self.k * np.log(np.maximum(acc, 1e-30))
        if np.isfinite(sub).any(): D = np.maximum(D, -sub + 0.0)
        if detail is not None: D = D + detail(X)
        return D

    def mesh(self, res=0.005, detail=None, pad=0.03, smooth=8, chunk=400000):
        P = [q.bbox(pad) for q in self.prims if not q.sub]
        lo = np.min([p[0] for p in P], 0); hi = np.max([p[1] for p in P], 0)
        n = np.ceil((hi - lo) / res).astype(int) + 1
        gx, gy, gz = (lo[i] + np.arange(n[i]) * res for i in range(3))
        vol = np.empty(tuple(n), np.float32)
        # évaluation par tranches en x, chaque primitive seulement dans sa boîte
        acc = np.zeros(tuple(n), np.float64); sub = np.full(tuple(n), np.inf)
        for q in self.prims:
            blo, bhi = q.bbox(pad + 3 * (q.k or self.k))
            ix = slice(max(0, int((blo[0] - lo[0]) / res)), min(n[0], int((bhi[0] - lo[0]) / res) + 2))
            iy = slice(max(0, int((blo[1] - lo[1]) / res)), min(n[1], int((bhi[1] - lo[1]) / res) + 2))
            iz = slice(max(0, int((blo[2] - lo[2]) / res)), min(n[2], int((bhi[2] - lo[2]) / res) + 2))
            X = np.stack(np.meshgrid(gx[ix], gy[iy], gz[iz], indexing='ij'), -1)
            sh = X.shape[:3]; X = X.reshape(-1, 3)
            d = q.dist(X).reshape(sh)
            if q.sub: sub[ix, iy, iz] = np.minimum(sub[ix, iy, iz], d)
            else: acc[ix, iy, iz] += np.exp(-np.clip(d, -0.5, 1.0) / (q.k or self.k))
        D = -self.k * np.log(np.maximum(acc, 1e-30))
        D = np.where(np.isfinite(sub), np.maximum(D, -sub), D)
        if detail is not None:
            for i0 in range(0, n[0], 16):
                X = np.stack(np.meshgrid(gx[i0:i0 + 16], gy, gz, indexing='ij'), -1)
                near = np.abs(D[i0:i0 + 16]) < 0.02
                if near.any():
                    blk = D[i0:i0 + 16]; blk[near] += detail(X[near]); D[i0:i0 + 16] = blk
        vol[:] = D
        V, F, Nn, _ = marching_cubes(vol, 0.0, spacing=(res, res, res))
        V = V + lo
        m = Mesh(V, F[:, ::-1])
        if (vertex_normals(m) * (V - V.mean(0))).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
        if smooth: taubin(m, smooth)
        return m

    # --- skinning
    def skin(self, m, sk, kw=0.030, top=4):
        names = sorted({q.bone for q in self.prims if not q.sub})
        idx = {b: sk[b] for b in names}
        S = np.zeros((len(m.V), len(names)))
        for q in self.prims:
            if q.sub: continue
            d = q.dist(m.V)
            S[:, names.index(q.bone)] += np.exp(-np.clip(d, -0.5, 1.0) / kw)
        S = S / np.maximum(S.sum(1, keepdims=True), 1e-30)
        o = np.argsort(-S, 1)[:, :top]
        W = np.take_along_axis(S, o, 1); W = W / W.sum(1, keepdims=True)
        m.J = np.vectorize(lambda j: idx[names[j]])(o).astype(np.int64); m.W = W
        return m


def taubin(m, it=8, lam=0.5, mu=-0.53):
    from scipy.sparse import coo_matrix, diags
    n = len(m.V); F = m.F
    i = np.r_[F[:, 0], F[:, 1], F[:, 2], F[:, 1], F[:, 2], F[:, 0]]; j = np.r_[F[:, 1], F[:, 2], F[:, 0], F[:, 0], F[:, 1], F[:, 2]]
    A = coo_matrix((np.ones(len(i)), (i, j)), shape=(n, n)).tocsr(); A.data[:] = 1.0
    deg = np.asarray(A.sum(1)).ravel(); Dinv = diags(1.0 / np.maximum(deg, 1))
    L = Dinv @ A
    V = m.V.copy()
    for _ in range(it):
        V = V + lam * (L @ V - V)
        V = V + mu * (L @ V - V)
    m.V = V
    return m


def noise3(X, scale, seed=0, octaves=3):
    """Bruit de valeur 3D lisse (somme d'octaves), rapide et déterministe."""
    rng = np.random.default_rng(seed)
    out = np.zeros(len(X)); amp = 1.0; tot = 0
    for o in range(octaves):
        s = scale * 2 ** o
        G = rng.standard_normal((32, 32, 32))
        G = gaussian_filter(G, 1.0, mode='wrap')
        Q = X / s
        i0 = np.floor(Q).astype(int); f = Q - i0; f = f * f * (3 - 2 * f)
        v = 0
        for dx in (0, 1):
            for dy in (0, 1):
                for dz in (0, 1):
                    w = (f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1]) * (f[:, 2] if dz else 1 - f[:, 2])
                    v = v + w * G[(i0[:, 0] + dx) % 32, (i0[:, 1] + dy) % 32, (i0[:, 2] + dz) % 32]
        out += amp * v; tot += amp; amp *= 0.5
    return out / tot


def box_uv(m, scale=0.25):
    """UV par projection selon l'axe dominant de la normale (textures de pelage, de peau, de chitine)."""
    N = vertex_normals(m); a = np.abs(N).argmax(1); V = m.V
    U = np.where(a == 0, V[:, 2], V[:, 0]); W = np.where(a == 1, V[:, 2], V[:, 1])
    m.UV = np.c_[U, W] / scale + a[:, None] * 0.33
    return m
