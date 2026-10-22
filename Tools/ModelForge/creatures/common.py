"""Outils des créatures : texture et cartes de pelage (mèches à transparence), peau sous le poil, yeux, crocs."""
import numpy as np
from scipy.ndimage import gaussian_filter
from scipy.spatial import cKDTree
from forge.geo import Mesh, concat, unit, vertex_normals
from forge.texgen import TexSet, fbm, n01, height_to_normal


def fur_texture(S=1024, seed=1, root=(0.04, 0.035, 0.035), tip=(0.30, 0.29, 0.28), accent=None, accent_amt=0.0, cols=4, density=1.0):
    """Atlas de mèches (cols variantes côte à côte) : centaines de poils effilés, racine sombre, pointe claire ;
    alpha = poils (découpe), normale = cylindre de chaque poil. accent : teinte de pointe alternative (crinière rouge)."""
    r = np.random.default_rng(seed)
    H, W = S, S
    col = np.zeros((H, W, 3)); alpha = np.zeros((H, W)); hgt = np.zeros((H, W))
    yy = np.arange(H)[:, None] / (H - 1)                     # 0 racine (bas de l'image) → 1 pointe
    cw = W // cols
    for c in range(cols):
        n = int(260 * density)
        for k in range(n):
            x0 = r.uniform(0.08, 0.92) * cw + c * cw
            L = r.uniform(0.55, 1.0)
            wpx = r.uniform(1.2, 3.2) * S / 1024
            bend = r.uniform(-0.12, 0.12) * cw
            t = np.linspace(0, L, int(H * L))
            xs = x0 + bend * t ** 2 + r.normal(0, 0.6, len(t)).cumsum() * 0.05
            ys = (H - 1) - t * (H - 1)
            wv = wpx * (1 - t / L) ** 0.6 + 0.4
            tone = r.uniform(0.7, 1.15)
            tipc = np.array(accent if (accent is not None and r.uniform() < accent_amt) else tip)
            for xi, yi, wi, ti in zip(xs[::2], ys[::2], wv[::2], t[::2]):
                xa = int(np.clip(xi - wi, c * cw, (c + 1) * cw - 1)); xb = int(np.clip(xi + wi, c * cw, (c + 1) * cw - 1)) + 1
                yi = int(yi)
                if yi < 0 or xa >= xb: continue
                f = ti / L
                cc = (np.array(root) * (1 - f ** 1.5) + tipc * f ** 1.5) * tone
                xx = np.arange(xa, xb); prof = np.clip(1 - np.abs(xx - xi) / (wi + 1e-6), 0, 1)
                for dy in (0, 1):
                    if yi + dy >= H: continue
                    upd = prof > alpha[yi + dy, xa:xb] * 0.98
                    col[yi + dy, xa:xb][upd] = cc
                    alpha[yi + dy, xa:xb] = np.maximum(alpha[yi + dy, xa:xb], prof)
                    hgt[yi + dy, xa:xb] = np.maximum(hgt[yi + dy, xa:xb], prof)
    alpha = np.clip(alpha * 1.6, 0, 1)
    nrm = height_to_normal(gaussian_filter(hgt, 0.7), 1.2)
    ao = np.clip(0.45 + 0.55 * yy.repeat(W, 1) ** 0.5, 0, 1)
    return TexSet(np.clip(col, 0, 1), nrm, np.zeros((H, W)), np.full((H, W), 0.25), ao, None, alpha)


def under_fur(S=1024, seed=2, color=(0.05, 0.045, 0.045), streak=(0.25, 0.24, 0.23)):
    """Peau / sous-poil visible entre les mèches : traînées de poils couchés."""
    r = np.random.default_rng(seed)
    st = fbm(S, S, r, base=S / 40, octaves=3, aniso=(0.12, 1.0))
    mott = n01(fbm(S, S, r, base=S / 6, octaves=4))
    s = np.clip(n01(st) * 1.2 - 0.2, 0, 1)
    base = np.array(color)[None, None] * (0.7 + 0.6 * mott[..., None]) + np.array(streak)[None, None] * s[..., None] * 0.35
    return TexSet(np.clip(base, 0, 1), height_to_normal(st, 0.5), np.zeros((S, S)), np.full((S, S), 0.25), np.clip(0.7 + 0.3 * s, 0, 1))


def sample_surface(m, n, rng, weight=None):
    V, F = m.V, m.F
    A = 0.5 * np.linalg.norm(np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]), axis=1)
    if weight is not None:
        C = V[F].mean(1); A = A * np.clip(weight(C), 0, None)
    p = A / A.sum()
    fi = rng.choice(len(F), n, p=p)
    u, v = rng.uniform(size=(2, n)); s = u + v > 1; u[s], v[s] = 1 - u[s], 1 - v[s]
    P = V[F[fi, 0]] * (1 - u - v)[:, None] + V[F[fi, 1]] * u[:, None] + V[F[fi, 2]] * v[:, None]
    N = vertex_normals(m)
    Nn = unit(N[F[fi, 0]] * (1 - u - v)[:, None] + N[F[fi, 1]] * u[:, None] + N[F[fi, 2]] * v[:, None])
    return P, Nn


def fur_cards(m, n, rng, flow, length, width=0.035, lift=0.35, cols=4, weight=None, jitter=0.25, curl=0.25, double=False):
    """Cartes de pelage posées sur le maillage m (skinné) : chaque carte part de la peau, se couche dans le sens du poil
    flow(P, N) (unitaire), se relève de lift, et s'incurve. length(P, N) en mètres. Renvoie un maillage skinné."""
    P, N = sample_surface(m, n, rng, weight)
    D = flow(P, N)
    D = unit(D - (D * N).sum(1, keepdims=True) * N * 0.0 + rng.normal(0, jitter, D.shape) * 0.6)
    L = length(P, N) * rng.uniform(0.7, 1.25, n)
    keep = L > 0.004
    P, N, D, L = P[keep], N[keep], D[keep], L[keep]
    n = len(P)
    T = unit(np.cross(D, N) + 1e-9)                      # largeur, posée à plat sur la peau
    Dl = unit(D * (1 - lift) + N * lift)
    w = width * rng.uniform(0.7, 1.3, n) * np.clip(L / 0.08, 0.5, 1.6)
    rows = 4
    tt = np.linspace(0, 1, rows)
    Vs, UVs = [], []
    col = rng.integers(0, cols, n)
    for i, t in enumerate(tt):
        bend = Dl * t + (D - N * 0.6) * curl * t * t
        c = P - N * 0.003 + bend * L[:, None]
        ww = w * (1 - 0.35 * t)
        for s in (-1, 1):
            Vs.append(c + T * (ww * 0.5 * s)[:, None])
            UVs.append(np.c_[(col + (0.02 if s < 0 else 0.98)) / cols, np.full(n, t * 0.98 + 0.01)])
    V = np.stack(Vs, 1).reshape(-1, 3); UV = np.stack(UVs, 1).reshape(-1, 2)
    base = np.arange(n)[:, None] * (rows * 2)
    F = []
    for i in range(rows - 1):
        a, b, c_, d = 2 * i, 2 * i + 1, 2 * i + 2, 2 * i + 3
        F += [np.c_[base + a, base + b, base + d], np.c_[base + a, base + d, base + c_]]
    F = np.vstack(F)
    out = Mesh(V, F, UV)
    # orientation : faces vers l'extérieur (normale de la peau)
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    Nf = np.repeat(N, (rows - 1) * 2, 0)[np.argsort(np.r_[[np.arange(n)] * ((rows - 1) * 2)].T.ravel(), kind='stable')] if False else None
    owner = (F[:, 0] // (rows * 2))
    flip = (fn * N[owner]).sum(1) < 0
    out.F[flip] = out.F[flip][:, ::-1]
    if double:
        back = Mesh(V.copy(), out.F[:, ::-1].copy(), UV.copy()); out = concat([out, back])
    # poids : ceux de la peau sous la racine
    i = cKDTree(m.V).query(np.repeat(P, rows * 2, 0) if not double else np.tile(np.repeat(P, rows * 2, 0), (2, 1)))[1]
    out.J, out.W = m.J[i].copy(), m.W[i].copy()
    return out


def ember(ts, k=0.9):
    """Pointes de poils incandescentes : émission = couleur rouge des pointes (braises)."""
    r = ts.base[..., 0]; g = ts.base[..., 1]
    m = np.clip((r - g * 1.8) * 3.0, 0, 1)
    ts.emit = np.clip(ts.base * m[..., None] * k, 0, 1)
    return ts
