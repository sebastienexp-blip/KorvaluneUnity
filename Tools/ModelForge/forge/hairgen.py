"""Pilosité au standard des jeux : textures de mèches RGBA (découpe alpha) et géométries en couches.

- strand_atlas : atlas 4 bandes de densité décroissante (bande 0 dense → bande 3 clairsemée), mèches orientées selon v,
  racine en bas de bande (v local 0) et pointe en haut (v local 1).
- shells : couches superposées qui épousent une région de la peau (cheveux courts, barbe, sourcils) ; chaque couche
  lit une bande de l'atlas → densité qui diminue vers l'extérieur, volume crédible.
- cards : cartes (rubans plats double face) pour les cheveux longs.
"""
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter
from .texgen import TexSet, fbm, noise, n01
from .geo import Mesh, Loft, spline, curve, concat, unit, vertex_normals, smoothstep
from .humanparts import submesh, region


def strand_atlas(S=2048, color=(0.06, 0.04, 0.03), tip=None, seed=7, density=(1.0, 0.65, 0.40, 0.22), n=2600, width=(1.2, 2.4),
                 curl=0.6, length=(0.55, 1.0), slant=0.0, gray=0.0):
    rng = np.random.default_rng(seed)
    tip = tip or tuple(min(1, c * 1.8 + 0.03) for c in color)
    SS = S * 2                                                        # sur-échantillonnage pour des bords doux
    alpha = Image.new('L', (SS, SS), 0); da = ImageDraw.Draw(alpha)
    shade = Image.new('L', (SS, SS), 0); ds = ImageDraw.Draw(shade)
    tipm = Image.new('L', (SS, SS), 0); dt = ImageDraw.Draw(tipm)
    bh = SS / 4
    for b, dens in enumerate(density):
        for k in range(int(n * dens)):
            x0 = rng.uniform(0, SS); L = rng.uniform(*length) * bh * 0.98
            y_root = (b + 1) * bh - 2                                 # racine en bas de la bande (v local 0)
            w = rng.uniform(*width) * 2
            ph = rng.uniform(0, 2 * np.pi); amp = curl * rng.uniform(2, 10)
            sl = slant * L + rng.normal(0, 0.03) * L
            pts = []
            for t in np.linspace(0, 1, 16):
                pts.append((x0 + sl * t + amp * np.sin(ph + t * 3.5), y_root - t * L))
            v = int(rng.uniform(140, 255))
            for ox in (-SS, 0, SS):
                seg = [(px + ox, py) for px, py in pts]
                for i in range(len(seg) - 1):
                    t = i / (len(seg) - 1)
                    ww = max(1, int(round(w * (1 - 0.85 * t ** 1.5))))
                    da.line([seg[i], seg[i + 1]], fill=255, width=ww)
                    ds.line([seg[i], seg[i + 1]], fill=v, width=ww)
                    dt.line([seg[i], seg[i + 1]], fill=int(255 * t ** 1.3), width=ww)
    A = np.asarray(alpha.resize((S, S), Image.LANCZOS), float) / 255
    sh = np.asarray(shade.resize((S, S), Image.LANCZOS), float) / 255
    tp = np.asarray(tipm.resize((S, S), Image.LANCZOS), float) / 255
    sh = np.where(A > 0.02, sh / np.maximum(A, 1e-3), 0.6)
    tp = np.where(A > 0.02, tp / np.maximum(A, 1e-3), 0)
    base = np.array(color)[None, None] * (0.55 + 0.6 * sh[..., None]) * (1 - tp[..., None]) + np.array(tip)[None, None] * tp[..., None] * (0.6 + 0.5 * sh[..., None])
    if gray: base = base * (1 - gray) + np.array([0.55, 0.55, 0.56]) * gray * (0.6 + 0.5 * sh[..., None])
    # fond (sous les mèches) : couleur moyenne pour un filtrage propre
    base = np.where(A[..., None] > 0.01, base, np.array(color) * 0.8)
    # normales : profil cylindrique des mèches (dérivée transversale de l'alpha)
    hgt = gaussian_filter(A, 0.8)
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 1.5
    nrm = np.dstack([-gx, np.zeros_like(gx), np.ones_like(gx)]); nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    smooth = np.clip(0.40 + 0.15 * sh, 0, 1)
    return TexSet(np.clip(base, 0, 1), nrm * 0.5 + 0.5, np.zeros((S, S)), smooth, np.clip(0.55 + 0.45 * sh, 0, 1), alpha=np.clip(A * 1.15, 0, 1))


def shells(body, vmask, uv_fn, offsets, bands, sk_weights=True, droop=None, rim=False, erode=None):
    """Couches qui épousent la région vmask de body. uv_fn(V) → (u, vloc ∈ [0,1]) ; offsets : décalages (m) ;
    bands : bande d'atlas pour chaque couche. droop(V, k) : déplacement additionnel (ex. gravité) par couche."""
    s = submesh(body, region(body, vmask))
    N = vertex_normals(s)
    u, vl = uv_fn(s.V)
    # distance (en anneaux de sommets) au bord de la région : les couches extérieures se rétractent → bord progressif
    E = np.concatenate([s.F[:, [0, 1]], s.F[:, [1, 2]], s.F[:, [2, 0]]]); Es = np.sort(E, 1)
    uq, cnt = np.unique(Es, axis=0, return_counts=True)
    ring = np.full(len(s.V), 99); ring[np.unique(uq[cnt == 1])] = 0
    for it in range(1, 8):
        nb = ring[E[:, 0]] == it - 1
        cand = E[nb, 1]; ring[cand] = np.minimum(ring[cand], it)
        nb = ring[E[:, 1]] == it - 1
        cand = E[nb, 0]; ring[cand] = np.minimum(ring[cand], it)
    out = []
    for k, (off, b) in enumerate(zip(offsets, bands)):
        V = s.V + N * off
        if droop is not None: V = V + droop(s.V, k)
        UV = np.c_[u, 1 - (b + 1 - np.clip(vl, 0.002, 0.998)) / 4.0]   # racine (vl=0) en bas de la bande, pointe en haut
        Fk = s.F
        if erode is not None and erode[k] > 0: Fk = s.F[(ring[s.F] >= erode[k]).all(1)]
        m = Mesh(V, Fk.copy(), UV, s.J.copy(), s.W.copy())
        out.append(m)
    return concat(out)


def cards(paths, widths, normals, bands=None, seed=1):
    """Cartes de cheveux longs : un ruban plat (double face via matériau) par chemin, UV v le long de la mèche."""
    rng = np.random.default_rng(seed)
    out = []
    for i, (P, w, nrm) in enumerate(zip(paths, widths, normals)):
        P = np.asarray(P); n = len(P)
        T = unit(np.gradient(P, axis=0)); side = unit(np.cross(T, nrm))
        t = np.linspace(0, 1, n)
        ww = w * (0.65 + 0.35 * np.sin(np.pi * np.clip(t * 1.1, 0, 1)) ** 0.5) * (1 - 0.6 * t ** 3)
        L = P[:, None, :] + side[:, None, :] * (np.array([-0.5, 0, 0.5])[None, :, None] * ww[:, None, None])
        # légère courbure transversale (mèche bombée)
        L[:, 1, :] += np.cross(side, T) * (ww * 0.12)[:, None]
        V = L.reshape(-1, 3)
        F = []
        for r in range(n - 1):
            for c in range(2):
                a = r * 3 + c; b = a + 1; cc = a + 3; d = cc + 1
                F += [(a, cc, b), (b, cc, d)]
        b = (bands[i] if bands is not None else rng.integers(0, 2))
        u0 = rng.uniform(0, 0.8)
        UV = np.c_[np.tile([u0, u0 + 0.06, u0 + 0.12], n), np.repeat(1 - (b + 1 - (0.002 + 0.996 * t)) / 4.0, 3)]
        out.append(Mesh(V, np.array(F), UV))
    return concat(out)


def _subdivide(V, F, N, J, W, times=2):
    for _ in range(times):
        nV = len(V); edges = {}
        newV, newN, newJ, newW = [V], [N], [J], [W]
        def mid(a, b):
            k = (a, b) if a < b else (b, a)
            if k not in edges:
                edges[k] = nV + len(edges)
                newV.append(((V[a] + V[b]) / 2)[None]); newN.append(unit((N[a] + N[b]) / 2)[None]); newJ.append(J[a][None]); newW.append(W[a][None])
            return edges[k]
        F2 = []
        for a, b, c in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            F2 += [(a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)]
        V = np.vstack(newV); N = np.vstack(newN); J = np.vstack(newJ); W = np.vstack(newW); F = np.array(F2)
    return V, F, N, J, W


def smooth_shells(body, coarse_mask, inside, uv_fn, offsets, bands, shrink, subdiv=2):
    """Couches de poils à contour lisse : la région grossière est subdivisée, puis découpée par une fonction continue
    inside(P) (> 0 dedans, en mètres) ; la couche k ne garde que inside > shrink[k] → bord fondu et naturel."""
    s = submesh(body, region(body, coarse_mask, all_in=False))
    N0 = vertex_normals(s)
    V, F, N, J, W = _subdivide(s.V, s.F, N0, s.J, s.W, subdiv)
    val = inside(V[F].mean(1))
    u, vl = uv_fn(V)
    out = []
    for k, (off, b) in enumerate(zip(offsets, bands)):
        keep = val > shrink[k]
        if not keep.any(): continue
        UV = np.c_[u, 1 - (b + 1 - np.clip(vl, 0.002, 0.998)) / 4.0]
        out.append(Mesh(V + N * off, F[keep].copy(), UV, J.copy(), W.copy()))
    return concat(out)
