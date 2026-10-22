"""Pièces d'armure et de costume réutilisables, posées sur le corps MakeHuman :
- shield_plate : plaque en écu (genouillère, jambière, cuissard, boucle…) bombée, nervurée, enroulée autour du membre, chant fermé + filet ;
- leaf_spike : lame/aileron en feuille (épaulières, brassards, épines) ;
- hip_envelope / lappet : pans de tabard en feuilles pointues qui tombent de la ceinture en épousant l'enveloppe des hanches et des cuisses.
"""
import numpy as np
from .geo import Mesh, Loft, spline, curve, concat, unit, vertex_normals, tube, smoothstep


def _grid_faces(nr, nc):
    idx = np.arange(nr * nc).reshape(nr, nc)
    return idx, np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                                np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])


def _close(front, Vb, idx, borders, uv, out_dir_fn):
    """front : Mesh face avant ; Vb : sommets de la face arrière ; borders : listes d'indices (chaînes) à fermer."""
    nV = len(front.V)
    back = Mesh(Vb, front.F[:, ::-1].copy(), uv.copy())
    rim = []
    for line in borders:
        for i, j in zip(line[:-1], line[1:]): rim += [(i, j, j + nV), (i, j + nV, i + nV)]
    parts = [front, back]
    if rim:
        rm = Mesh(np.vstack([front.V, Vb]), np.array(rim), np.vstack([uv, uv]))
        fn = np.cross(rm.V[rm.F[:, 1]] - rm.V[rm.F[:, 0]], rm.V[rm.F[:, 2]] - rm.V[rm.F[:, 0]])
        cen = rm.V[rm.F].mean(1); flip = (fn * out_dir_fn(cen)).sum(1) < 0
        rm.F[flip] = rm.F[flip][:, ::-1]; parts.append(rm)
    return concat(parts)


def shield_plate(C, N, U, W=0.06, H=0.15, top=0.40, tip=1.0, dome=0.015, ridge=0.006, wrap=0.08, wrap_v=0.6, thick=0.005,
                 trim=0.0035, ns=19, nv=27, tile=0.12, flare=0.0):
    """Écu centré en C (normale N, haut U) : demi-largeur W, hauteur H (top = part au-dessus de C), haut arrondi, bas en pointe (tip=1)
    ou arrondi (tip=0). wrap : rayon d'enroulement autour du membre ; flare : bords relevés. Renvoie (plaque, filet ou None)."""
    N = unit(np.asarray(N, float)); U = np.asarray(U, float); U = unit(U - N * np.dot(U, N)); R = np.cross(U, N)
    s = np.linspace(-1, 1, ns); v = np.linspace(0, 1, nv)
    SS, VV = np.meshgrid(s, v)
    vr = 0.28
    hw_top = W * np.sqrt(np.clip(1 - ((vr - VV) / vr) ** 2, 0, 1))
    tv = np.clip((VV - vr) / (1 - vr), 0, 1)
    hw_pt = W * np.clip(1 - tv ** 1.5, 0, 1) ** 0.85
    hw_fl = W * (1 - 0.18 * tv) * np.sqrt(np.clip(1 - ((tv - 0.82) / 0.18).clip(0, 1) ** 2, 0, 1))
    hw = np.where(VV < vr, hw_top, tip * hw_pt + (1 - tip) * hw_fl)
    x = SS * hw; y = H * top - VV * H
    z = dome * (1 - SS ** 2) * np.sin(np.pi * np.clip(VV, 0.02, 0.98)) ** 0.6 + ridge * np.clip(1 - np.abs(SS) * 3, 0, 1) ** 2 * (VV > 0.05)
    z = z - x ** 2 / (2 * wrap) - (y - 0.0) ** 2 / (2 * wrap / max(wrap_v, 1e-3)) * 0.15 + flare * np.abs(SS) ** 4
    P = C[None, None] + R[None, None] * x[..., None] + U[None, None] * y[..., None] + N[None, None] * z[..., None]
    idx, F = _grid_faces(nv, ns)
    uv = np.stack([x.ravel() / tile + 0.5, y.ravel() / tile], 1)
    front = Mesh(P.reshape(-1, 3), F, uv)
    n = vertex_normals(front)
    if (n * N).sum(1).mean() < 0: front.F = front.F[:, ::-1].copy(); n = vertex_normals(front)
    Vb = front.V - n * thick
    ctr = C - N * 0.05
    plate = _close(front, Vb, idx, [idx[:, 0], idx[:, -1], idx[-1, :]], uv, lambda c: c - ctr - N * ((c - ctr) @ N)[:, None] * 0.9)
    if not trim: return plate, None
    line = np.concatenate([idx[::-1, 0], idx[1:, -1]]) if tip > 0.5 else np.concatenate([idx[::-1, 0], idx[-1, 1:-1], idx[:, -1]])
    pts = front.V[line] + n[line] * (trim * 0.4)
    keep = np.r_[True, np.linalg.norm(np.diff(pts, axis=0), axis=1) > 1e-5]
    return plate, tube(pts[keep], trim, segs=6, tile=0.05, cap=True)


def leaf_spike(base, direction, side, length=0.06, width=0.018, thick=0.004, curl=0.3, n=10):
    """Lame en feuille pointue : de base vers direction (courbée de « curl » vers la normale du plat), largeur max au tiers."""
    d = unit(np.asarray(direction, float)); sd = unit(np.asarray(side, float) - d * np.dot(side, d)); nrm = np.cross(d, sd)
    t = np.linspace(0, 1, n)
    path = base[None] + d[None] * (t * length)[:, None] + nrm[None] * (curl * length * t ** 2)[:, None]
    w = width * np.sin(np.pi * np.clip(t * 0.85 + 0.15, 0, 1)) ** 0.7 * (1 - t ** 3)
    w = np.maximum(w, 0.0008)
    return Loft(path, np.maximum(thick * (1 - t), 0.0006), w, p=2.0, segs=8, ref=tuple(nrm), cap0=1, cap1=1).mesh(0.05)


def hip_envelope(body, mask, y0=0.30, y1=1.06, ny=40, na=72):
    """Enveloppe convexe (fonction support) du bassin et des jambes, tranche par tranche, rendue non décroissante vers le bas
    (le tissu tombe et ne rentre pas entre les jambes). Renvoie r(theta, y) (theta 0 = devant +Z)."""
    V = body.V[mask]
    ys = np.linspace(y0, y1, ny); th = np.linspace(0, 2 * np.pi, na, endpoint=False)
    D = np.stack([np.sin(th), np.cos(th)], 1)
    env = np.zeros((ny, na))
    for i, y in enumerate(ys):
        sel = np.abs(V[:, 1] - y) < (y1 - y0) / ny * 1.5
        Q = V[sel][:, [0, 2]] if sel.sum() > 5 else V[np.argsort(np.abs(V[:, 1] - y))[:40]][:, [0, 2]]
        env[i] = (Q @ D.T).max(0)
    for i in range(ny - 2, -1, -1): env[i] = np.maximum(env[i], env[i + 1] * 0.985)       # du haut vers le bas : ne rentre pas
    def r(theta, y):
        fa = (np.asarray(theta) % (2 * np.pi)) / (2 * np.pi) * na; fy = np.clip((np.asarray(y) - y0) / (y1 - y0) * (ny - 1), 0, ny - 1.001)
        a0 = np.floor(fa).astype(int); i0 = np.floor(fy).astype(int); ta = fa - a0; ty = fy - i0
        g = lambda i, j: env[i, j % na]
        return g(i0, a0) * (1 - ta) * (1 - ty) + g(i0, a0 + 1) * ta * (1 - ty) + g(i0 + 1, a0) * (1 - ta) * ty + g(i0 + 1, a0 + 1) * ta * ty
    return r


def lappet(env, theta, ytop, length, width, off=0.020, flare=0.045, thick=0.004, tip=1.0, seed=0, nu=11, nv=22, tile=0.15, notch=0.0):
    """Pan en feuille pointue suspendu à la ceinture, angle theta autour du bassin. Renvoie (pan, liseré)."""
    rng = np.random.default_rng(seed)
    u = np.linspace(-1, 1, nu); v = np.linspace(0, 1, nv)
    UU, VV = np.meshgrid(u, v)
    w = width * (1 + 0.15 * np.sin(np.pi * np.clip(VV / 0.6, 0, 1))) * np.where(VV < 0.55, 1.0, np.clip(1 - ((VV - 0.55) / 0.45) ** 1.4, 0, 1) ** (0.8 * tip) if tip else 1.0)
    w = np.maximum(w, 0.0015)
    y = ytop - VV * length
    if notch:   # entailles irrégulières sur les bords
        k = np.interp(v, np.linspace(0, 1, 7), rng.uniform(0, 1, 7)); w = w * (1 - notch * (k[:, None] > 0.7) * (VV > 0.3))
    r0 = env(theta, y)
    rr = r0 + off + flare * VV ** 1.3
    th = theta + UU * w / np.maximum(rr, 0.05)
    rr = env(th, y) * 0 + rr + 0.004 * np.cos(np.pi * UU * 0.5) - 0.006 * UU ** 2
    P = np.stack([np.sin(th) * rr, y, np.cos(th) * rr], -1)
    idx, F = _grid_faces(nv, nu)
    uv = np.stack([(UU * width).ravel() / tile + 0.5 + seed * 0.37, y.ravel() / tile], 1)
    front = Mesh(P.reshape(-1, 3), F, uv)
    n = vertex_normals(front)
    out = (front.V * np.array([1, 0, 1]))
    if (n * out).sum(1).mean() < 0: front.F = front.F[:, ::-1].copy(); n = vertex_normals(front)
    m = _close(front, front.V - n * thick, idx, [idx[:, 0], idx[:, -1], idx[-1, :]], uv, lambda c: c * np.array([1, 0, 1]))
    line = np.concatenate([idx[::-1, 0], idx[1:, -1]])
    pts = front.V[line] + n[line] * 0.0015
    return m, tube(pts, 0.0035, segs=5, tile=0.05)
