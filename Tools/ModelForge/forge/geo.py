"""Géométrie : maillages, surfaces balayées (lofts), plaques d'armure qui épousent une surface, tubes, rivets, skinning.

Repère : Y vers le haut, le personnage regarde +Z, sa gauche est +X. Unités : mètres.
Tout maillage porte : V (positions), F (triangles), UV, J/W (4 os et 4 poids par sommet), mat (nom de matériau par maillage).
"""
import numpy as np
import os as _os
# densité des plaques (1 = maillage fin pour vues rapprochées ; < 1 pour les niveaux de détail)
DENSITY = float(_os.environ.get('FORGE_DENSITY', '0.45'))


# ----------------------------------------------------------------------------------------------- maillage
class Mesh:
    def __init__(self, V, F, UV=None, J=None, W=None):
        self.V = np.asarray(V, np.float64).reshape(-1, 3)
        self.F = np.asarray(F, np.int64).reshape(-1, 3)
        n = len(self.V)
        self.UV = np.zeros((n, 2)) if UV is None else np.asarray(UV, np.float64).reshape(-1, 2)
        self.J = np.zeros((n, 4), np.int64) if J is None else np.asarray(J, np.int64)
        self.W = np.tile([1.0, 0, 0, 0], (n, 1)) if W is None else np.asarray(W, np.float64)
        self.N = None

    def copy(self):
        m = Mesh(self.V.copy(), self.F.copy(), self.UV.copy(), self.J.copy(), self.W.copy())
        m.N = None if self.N is None else self.N.copy()
        return m

    def flip(self):
        self.F = self.F[:, ::-1].copy(); return self

    def apply(self, M):
        M = np.asarray(M, float)
        self.V = self.V @ M[:3, :3].T + M[:3, 3]
        if np.linalg.det(M[:3, :3]) < 0: self.F = self.F[:, ::-1].copy()
        return self


def concat(meshes):
    ms = [m for m in meshes if m is not None and len(m.V)]
    off = np.cumsum([0] + [len(m.V) for m in ms])[:-1]
    out = Mesh(np.vstack([m.V for m in ms]), np.vstack([m.F + o for m, o in zip(ms, off)]), np.vstack([m.UV for m in ms]),
               np.vstack([m.J for m in ms]), np.vstack([m.W for m in ms]))
    if any(m.N is not None for m in ms):
        out.N = np.vstack([m.N if m.N is not None else vertex_normals(m) for m in ms])
    names = sorted(set(k for m in ms for k in getattr(m, 'shapes', {})))
    if names:   # blendshapes : zéros pour les pièces qui n'en ont pas
        out.shapes = {k: np.vstack([getattr(m, 'shapes', {}).get(k, np.zeros((len(m.V), 3))) for m in ms]) for k in names}
    return out


def vertex_normals(m, weld=True, crease_deg=None):
    """Normales lissées pondérées par l'aire. weld : les sommets superposés (coutures d'UV) partagent leur normale."""
    V, F = m.V, m.F
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    if weld:
        key = np.round(V * 2e4).astype(np.int64)
        _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.reshape(-1)
        acc = np.zeros((inv.max() + 1, 3))
        for k in range(3): np.add.at(acc, inv[F[:, k]], fn)
        acc = acc[inv]
    else:
        acc = np.zeros_like(V)
        for k in range(3): np.add.at(acc, F[:, k], fn)
    n = np.linalg.norm(acc, axis=1, keepdims=True); n[n < 1e-15] = 1
    return acc / n


def unit(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True); n = np.where(n < 1e-12, 1, n)
    return v / n


def smoothstep(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


# ----------------------------------------------------------------------------------------------- courbes
def spline(pts, n):
    """Catmull-Rom centripète passant par pts, n points répartis à abscisse curviligne quasi uniforme."""
    P = np.asarray(pts, float)
    if len(P) == 2:
        t = np.linspace(0, 1, n)[:, None]; return P[0] * (1 - t) + P[1] * t
    Q = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    dense = []
    for k in range(len(P) - 1):
        p0, p1, p2, p3 = Q[k], Q[k + 1], Q[k + 2], Q[k + 3]
        for t in np.linspace(0, 1, 40, endpoint=False):
            dense.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    dense.append(P[-1]); D = np.array(dense)
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(D, axis=0), axis=1))])
    u = np.linspace(0, s[-1], n)
    return np.stack([np.interp(u, s, D[:, i]) for i in range(3)], 1)


def curve(keys, n):
    """Profil 1D : keys = [(t, valeur)], interpolation monotone douce (PCHIP) → n valeurs pour t ∈ [0,1]."""
    from scipy.interpolate import PchipInterpolator
    k = sorted(keys); t = np.array([a for a, _ in k]); v = np.array([b for _, b in k])
    if len(t) == 1: return np.full(n, v[0])
    return PchipInterpolator(t, v)(np.linspace(0, 1, n))


def frames(path, ref=(0, 0, 1)):
    """Repère par transport parallèle le long d'un chemin : tangente T, « avant » A, « côté » S = T × A."""
    T = unit(np.gradient(path, axis=0))
    A = np.zeros_like(path)
    r = np.asarray(ref, float) - T[0] * np.dot(ref, T[0])
    if np.linalg.norm(r) < 1e-6: r = np.array([1.0, 0, 0]) - T[0] * T[0][0]
    A[0] = unit(r)
    for i in range(1, len(path)):
        r = A[i - 1] - T[i] * np.dot(A[i - 1], T[i])
        A[i] = unit(r) if np.linalg.norm(r) > 1e-9 else A[i - 1]
    return T, A, np.cross(T, A)


# ----------------------------------------------------------------------------------------------- loft
class Loft:
    """Surface balayée : sections superelliptiques le long d'un chemin.
    rf : demi-épaisseur vers « l'avant » (repère ref), rs : demi-largeur latérale, p : exposant (2 ellipse, 4 carré arrondi).
    of / os_ : décalage de la section. Angle a ∈ [0,1) : 0 = avant, 0.25 = côté (S), 0.5 = arrière, 0.75 = côté opposé."""

    def __init__(self, path, rf, rs, p=2.4, of=0.0, os_=0.0, segs=32, ref=(0, 0, 1), cap0=0, cap1=0, bulge=None, section=None):
        self.section = section; self._cap0, self._cap1, self._ref = cap0, cap1, ref
        path = np.asarray(path, float); n = len(path)
        B = lambda x: np.broadcast_to(np.asarray(x, float), (n,)).copy()
        self.rf, self.rs, self.p, self.of, self.os = B(rf), B(rs), B(p), B(of), B(os_)
        self.path = path; self.n = n; self.segs = segs
        self.T, self.A, self.S = frames(path, ref)
        a = np.linspace(0, 1, segs + 1)
        self.grid = self._ring_grid(a, bulge)
        # extrémités arrondies
        rows = [self.grid[i] for i in range(n)]
        pre, post = [], []
        for cap, end in ((cap0, 0), (cap1, n - 1)):
            if not cap: continue
            base = self.grid[end]; c = base[:-1].mean(0)
            d = -self.T[0] if end == 0 else self.T[-1]
            rad = 0.5 * (self.rf[end] + self.rs[end])
            for k in range(1, cap + 1):
                ang = k / cap * np.pi / 2
                ring = c + (base - c) * np.cos(ang) + d * np.sin(ang) * rad * 0.85
                (pre if end == 0 else post).append(ring)
        self.rows_all = np.array(pre[::-1] + rows + post)
        self.core0 = len(pre)

    def _ring_grid(self, a, bulge=None):
        ang = 2 * np.pi * a; ca, sa = np.cos(ang), np.sin(ang)
        G = np.zeros((self.n, len(a), 3))
        for i in range(self.n):
            if self.section is not None:
                x, z = self.section(i, ang, self.rf[i], self.rs[i])
            else:
                e = 2.0 / self.p[i]
                x = self.rf[i] * np.sign(ca) * np.abs(ca) ** e
                z = self.rs[i] * np.sign(sa) * np.abs(sa) ** e
            if bulge is not None:
                k = bulge(i / max(self.n - 1, 1), a); x = x * k; z = z * k
            c = self.path[i] + self.A[i] * self.of[i] + self.S[i] * self.os[i]
            G[i] = c + np.outer(x, self.A[i]) + np.outer(z, self.S[i])
        return G

    def surf(self, t, a):
        """Point de la surface (rangées « corps ») pour t ∈ [0,1] le long, a ∈ [0,1] autour. Vectorisé."""
        t = np.clip(np.asarray(t, float), 0, 1) * (self.n - 1); a = (np.asarray(a, float) % 1.0) * self.segs
        i0 = np.clip(np.floor(t).astype(int), 0, self.n - 2); j0 = np.clip(np.floor(a).astype(int), 0, self.segs - 1)
        ft = (t - i0)[..., None]; fa = (a - j0)[..., None]
        G = self.grid
        return (G[i0, j0] * (1 - ft) * (1 - fa) + G[i0 + 1, j0] * ft * (1 - fa) + G[i0, j0 + 1] * (1 - ft) * fa + G[i0 + 1, j0 + 1] * ft * fa)

    def frame(self, t, a, e=1e-3):
        P = self.surf(t, a)
        dt = self.surf(t + e, a) - self.surf(t - e, a)
        da = self.surf(t, a + e) - self.surf(t, a - e)
        N = unit(np.cross(da, dt))
        return P, N, unit(dt), unit(da)

    def mesh(self, tile=0.4, rstep=1, cstep=1):
        G = self.rows_all
        if rstep > 1: G = G[sorted(set(list(range(0, len(G), rstep)) + [len(G) - 1]))]
        if cstep > 1: G = G[:, sorted(set(list(range(0, G.shape[1], cstep)) + [G.shape[1] - 1]))]
        R, C = G.shape[:2]
        V = G.reshape(-1, 3)
        vlen = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(G, axis=0), axis=2).mean(1))])
        circ = np.linalg.norm(np.diff(G[R // 2], axis=0), axis=1).sum()
        UV = np.stack(np.meshgrid(np.linspace(0, circ, C), vlen), -1).reshape(-1, 2) / tile
        m = Mesh(V, grid_faces(R, C), UV)
        orient_out(m, None, self)
        return m


def grid_faces(R, C, wrap=False):
    idx = np.arange(R * C).reshape(R, C)
    a = idx[:-1, :-1].ravel(); b = idx[:-1, 1:].ravel(); c = idx[1:, :-1].ravel(); d = idx[1:, 1:].ravel()
    return np.concatenate([np.stack([a, c, b], 1), np.stack([b, c, d], 1)])


def orient_out(m, ctr, loft=None):
    """Oriente les facettes vers l'extérieur (test local sur l'axe du loft)."""
    V, F = m.V, m.F
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    fc = V[F].mean(1)
    if loft is not None:
        # projeté sur le chemin le plus proche
        d = np.linalg.norm(fc[:, None, :] - loft.path[None, ::max(1, loft.n // 24), :], axis=2)
        near = loft.path[::max(1, loft.n // 24)][d.argmin(1)]
        out = fc - near
    else:
        out = fc - ctr
    if np.sum(np.einsum('ij,ij->i', fn, out)) < 0: m.F = m.F[:, ::-1].copy()


# ----------------------------------------------------------------------------------------------- plaques
def plate(loft, a0, a1, t_lo, t_hi, off=0.006, thick=0.006, bevel=0.006, na=24, nt=14, dome=0.0, tile=0.3,
          edge_roll=0.0, bulge_t=None):
    """Plaque qui épouse un loft : angles [a0,a1] (a1 > a0, peut dépasser 1), hauteurs t_lo(a) → t_hi(a) (fonctions ou constantes).
    Coque fermée : face extérieure, chant arrondi, face intérieure. dome : bombé central (m). edge_roll : bourrelet sur le bord."""
    flo = t_lo if callable(t_lo) else (lambda a, v=t_lo: np.full_like(a, v))
    fhi = t_hi if callable(t_hi) else (lambda a, v=t_hi: np.full_like(a, v))
    na = max(4, int(round(na * DENSITY))); nt = max(3, int(round(nt * DENSITY)))
    qa = np.linspace(0, 1, na); qt = np.linspace(0, 1, nt)
    # rangées serrées près des bords pour un chant net
    qa = np.unique(np.concatenate([qa, [0.012, 0.03, 0.97, 0.988]])); qt = np.unique(np.concatenate([qt, [0.02, 0.05, 0.95, 0.98]]))
    A = a0 + qa * (a1 - a0)
    lo, hi = flo(A), fhi(A)
    T = lo[None, :] + qt[:, None] * (hi - lo)[None, :]
    AA = np.broadcast_to(A, T.shape)
    P, N, _, _ = loft.frame(T, AA)
    # distance métrique au bord
    La = np.linalg.norm(np.diff(P, axis=1), axis=2); Lt = np.linalg.norm(np.diff(P, axis=0), axis=2)
    ca = np.concatenate([np.zeros((P.shape[0], 1)), np.cumsum(La, 1)], 1); ct = np.concatenate([np.zeros((1, P.shape[1])), np.cumsum(Lt, 0)], 0)
    ea = np.minimum(ca, ca[:, -1:] - ca); et = np.minimum(ct, ct[-1:, :] - ct)
    e = np.minimum(ea, et)
    x = np.clip(e / max(bevel, 1e-6), 0, 1)
    pe = np.sqrt(1 - (1 - x) ** 2)
    u = (qa - 0.5) * 2; v = (qt - 0.5) * 2
    dm = dome * np.clip(1 - 0.5 * (u[None, :] ** 2 + v[:, None] ** 2), 0, 1)
    roll = edge_roll * np.exp(-((e - bevel * 1.6) / (bevel * 0.9 + 1e-6)) ** 2) if edge_roll else 0
    if callable(off): off = off(AA - a0, (T - lo[None, :]) / np.maximum(hi - lo, 1e-9)[None, :], qa[None, :] + 0 * T, qt[:, None] + 0 * T)
    H = off + thick * pe + dm * pe + roll
    outer = P + N * H[..., None]
    inner = P + N * (np.asarray(off)[..., None] if np.ndim(off) else off) + N * 0.0004
    R, C = P.shape[:2]
    UV = np.stack([ca, ct], -1).reshape(-1, 2) / tile
    mo = Mesh(outer.reshape(-1, 3), grid_faces(R, C), UV)
    fn = np.cross(mo.V[mo.F[:, 1]] - mo.V[mo.F[:, 0]], mo.V[mo.F[:, 2]] - mo.V[mo.F[:, 0]])
    if np.sum(np.einsum('ij,ij->i', fn, N.reshape(-1, 3)[mo.F[:, 0]])) < 0: mo.flip()
    # face intérieure à demi-résolution (rarement vue de près)
    ri = sorted(set(list(range(0, R, 2)) + [R - 1])); ci = sorted(set(list(range(0, C, 2)) + [C - 1]))
    inn = inner[ri][:, ci]; uvi = UV.reshape(R, C, 2)[ri][:, ci].reshape(-1, 2)
    mi = Mesh(inn.reshape(-1, 3), grid_faces(len(ri), len(ci)), uvi)
    fi = np.cross(mi.V[mi.F[:, 1]] - mi.V[mi.F[:, 0]], mi.V[mi.F[:, 2]] - mi.V[mi.F[:, 0]])
    if np.sum(np.einsum('ij,ij->i', fi, N[ri][:, ci].reshape(-1, 3)[mi.F[:, 0]])) > 0: mi.flip()
    return concat([mo, mi])


def plate_edge(loft, a0, a1, t_fn, off, r=0.004, n=60, tile=0.1, side=+1):
    """Bourrelet (bord roulé) le long d'une limite de plaque : t = t_fn(a) pour a ∈ [a0,a1]."""
    tf = t_fn if callable(t_fn) else (lambda a, v=t_fn: np.full_like(a, v))
    n = max(8, int(n * 0.6))
    A = np.linspace(a0, a1, n); T = tf(A)
    P, N, _, _ = loft.frame(T, A)
    o = off(A) if callable(off) else off
    pts = P + N * (np.asarray(o)[..., None] if np.ndim(o) else o) + N * r * 0.8
    return tube(pts, r, segs=8, tile=tile)


def tube(pts, r, segs=12, tile=0.1, cap=True, ref=None):
    pts = np.asarray(pts, float)
    if ref is None:
        d = pts[-1] - pts[0]; ref = np.cross(unit(d), [0, 1, 0]) if abs(unit(d)[1]) < 0.9 else np.array([1.0, 0, 0])
    lo = Loft(pts, r, r, p=2, segs=segs, ref=ref, cap0=2 if cap else 0, cap1=2 if cap else 0)
    return lo.mesh(tile)


def rivet(P, N, r=0.0045, h=0.0028, segs=10, rings=4):
    """Rivet à tête bombée posé en P (normale N)."""
    N = unit(np.asarray(N, float)); P = np.asarray(P, float)
    t1 = unit(np.cross(N, [0, 1, 0] if abs(N[1]) < 0.9 else [1, 0, 0])); t2 = np.cross(N, t1)
    V = [P + N * h]; UV = [(0.5, 0.5)]
    for k in range(1, rings + 1):
        th = k / rings * np.pi / 2
        rr = r * np.sin(th); hh = h * np.cos(th)
        for s in range(segs):
            ph = 2 * np.pi * s / segs
            V.append(P + N * hh + (t1 * np.cos(ph) + t2 * np.sin(ph)) * rr)
            UV.append((0.5 + 0.5 * np.sin(th) * np.cos(ph), 0.5 + 0.5 * np.sin(th) * np.sin(ph)))
    # jupe sous la tête (s'enfonce dans la plaque)
    for s in range(segs):
        ph = 2 * np.pi * s / segs
        V.append(P - N * 0.0015 + (t1 * np.cos(ph) + t2 * np.sin(ph)) * r * 0.98); UV.append((0.5, 0.5))
    F = []
    for s in range(segs): F.append((0, 1 + s, 1 + (s + 1) % segs))
    for k in range(rings):
        b0 = 1 + (k - 1) * segs if k > 0 else None
        if k == 0: continue
        for s in range(segs):
            a = 1 + (k - 1) * segs + s; b = 1 + (k - 1) * segs + (s + 1) % segs
            c = 1 + k * segs + s; d = 1 + k * segs + (s + 1) % segs
            F += [(a, c, b), (b, c, d)]
    last = 1 + (rings - 1) * segs; sk = 1 + rings * segs
    for s in range(segs):
        a = last + s; b = last + (s + 1) % segs; c = sk + s; d = sk + (s + 1) % segs
        F += [(a, c, b), (b, c, d)]
    m = Mesh(np.array(V), np.array(F), np.array(UV) * 0.05)
    fn = np.cross(m.V[m.F[:, 1]] - m.V[m.F[:, 0]], m.V[m.F[:, 2]] - m.V[m.F[:, 0]])
    if (fn @ N).sum() < 0: m.flip()
    return m


def rivets_on(loft, pts_ta, off, r=0.0045, h=0.0028):
    """Rivets aux coordonnées (t, a) du loft, posés à la hauteur off au-dessus de la surface."""
    ms = []
    for t, a in pts_ta:
        P, N, _, _ = loft.frame(np.array(t), np.array(a))
        ms.append(rivet(P + N * off, N, r, h))
    return concat(ms)


def disc_cut(P, N, r, depth=0.003, segs=12):
    """Petit trou sombre (bouche d'aération du casque) : cylindre creux orienté vers l'intérieur."""
    N = unit(np.asarray(N, float))
    t1 = unit(np.cross(N, [0, 1, 0] if abs(N[1]) < 0.9 else [1, 0, 0])); t2 = np.cross(N, t1)
    ring = np.array([t1 * np.cos(2 * np.pi * s / segs) + t2 * np.sin(2 * np.pi * s / segs) for s in range(segs)])
    V = np.vstack([P + ring * r, P - N * depth + ring * r * 0.8, [P - N * depth]])
    F = []
    for s in range(segs):
        a, b = s, (s + 1) % segs; c, d = segs + s, segs + (s + 1) % segs
        F += [(a, b, c), (b, d, c), (c, d, 2 * segs)]
    m = Mesh(V, np.array(F)); return m


# ----------------------------------------------------------------------------------------------- skinning
def rigid(m, bone):
    m.J = np.zeros((len(m.V), 4), np.int64); m.J[:, 0] = bone
    m.W = np.zeros((len(m.V), 4)); m.W[:, 0] = 1.0
    return m


def chain(m, joints, bones, blend=0.06):
    """Poids le long d'une chaîne d'os (joints[i] = position du joint de bones[i], dernier = bout de chaîne)."""
    P = np.asarray(joints, float); V = m.V; nseg = len(P) - 1
    best = np.full(len(V), np.inf); seg = np.zeros(len(V), int); sv = np.zeros(len(V))
    for i in range(nseg):
        a, b = P[i], P[i + 1]; d = b - a; L2 = d @ d
        s = np.clip((V - a) @ d / L2, 0, 1); dist = np.linalg.norm(V - (a + s[:, None] * d), axis=1)
        bt = dist < best - 1e-12
        best[bt] = dist[bt]; seg[bt] = i; sv[bt] = s[bt]
    L = np.linalg.norm(np.diff(P, axis=0), axis=1)
    pos = sv * L[seg]                                           # distance depuis le début du segment
    J = np.zeros((len(V), 4), np.int64); W = np.zeros((len(V), 4))
    J[:, 0] = np.array(bones)[np.minimum(seg, len(bones) - 1)]; W[:, 0] = 1
    for i in range(nseg):
        sel = seg == i
        # mélange autour du joint de début de segment (avec le segment précédent)
        if i > 0:
            q = sel & (pos < blend)
            f = smoothstep(0.5 + 0.5 * pos[q] / blend)
            J[q, 0] = bones[i]; W[q, 0] = f; J[q, 1] = bones[i - 1]; W[q, 1] = 1 - f
        if i < nseg - 1 and i + 1 < len(bones):
            q = sel & (L[i] - pos < blend)
            f = smoothstep(0.5 + 0.5 * (L[i] - pos[q]) / blend)
            J[q, 0] = bones[i]; W[q, 0] = f; J[q, 1] = bones[i + 1]; W[q, 1] = 1 - f
    m.J, m.W = J, W
    return m


def blend_bones(m, bone_a, bone_b, f):
    """Poids mixtes entre deux os, f ∈ [0,1] par sommet (part de bone_b)."""
    f = np.clip(np.broadcast_to(f, (len(m.V),)), 0, 1)
    m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.zeros((len(m.V), 4))
    m.J[:, 0] = bone_a; m.W[:, 0] = 1 - f; m.J[:, 1] = bone_b; m.W[:, 1] = f
    return m


def mirror(m, swap):
    """Copie symétrique X → -X (os gauche ↔ droit via swap : dict indice → indice)."""
    c = m.copy(); c.V[:, 0] *= -1; c.F = c.F[:, ::-1].copy()
    if c.N is not None: c.N[:, 0] *= -1
    lut = np.arange(max(512, c.J.max() + 1))
    for a, b in swap.items(): lut[a] = b
    c.J = lut[c.J]
    return c
