"""Anatomie intime des corps de base (le maillage MakeHuman en est dépourvu) : pièces séparées, masquables dans Unity,
posées sur la surface réelle du bassin (lancer de rayons dans le plan médian), texturées avec la peau du corps
(UV du sommet de peau le plus proche) et pondérées sur le bassin. Rendu anatomique au repos, sans exagération."""
import numpy as np
from .geo import Mesh, Loft, concat, unit, smoothstep, vertex_normals
from scipy.spatial import cKDTree

C0 = np.array([0.0, 0.93, 0.0])          # centre des rayons (dans le bassin)


def _ray(body, o, d):
    """Première intersection du rayon o + t·d avec le maillage (surface extérieure). Renvoie (point, normale)."""
    T = body.V[body.F]
    e1 = T[:, 1] - T[:, 0]; e2 = T[:, 2] - T[:, 0]
    p = np.cross(d, e2); det = (e1 * p).sum(1)
    ok = np.abs(det) > 1e-12
    inv = np.where(ok, 1 / np.where(ok, det, 1), 0)
    s = o - T[:, 0]; u = (s * p).sum(1) * inv
    q = np.cross(s, e1); v = (q @ d) * inv; t = (e2 * q).sum(1) * inv
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 0) & (t < 0.25)
    if not hit.any(): return None, None
    i = np.where(hit)[0][np.argmin(t[hit])]
    n = unit(np.cross(e1[i], e2[i]))
    if np.dot(n, d) < 0: n = -n
    return o + d * t[i], n


def profile(body, x=0.0, angles=np.arange(-10, 150, 4)):
    """Coupe du bassin dans le plan x = cte : angle 0 = devant (+Z), 90 = vers le bas, 180 = derrière."""
    P, N, A = [], [], []
    for a in angles:
        r = np.radians(a); d = np.array([0.0, -np.sin(r), np.cos(r)])
        p, n = _ray(body, C0 + np.array([x, 0, 0]), d)
        if p is not None: P.append(p); N.append(n); A.append(a)
    return np.array(P), np.array(N), np.array(A, float)


def at(Pr, Nr, Ar, a):
    return np.array([np.interp(a, Ar, Pr[:, k]) for k in range(3)]), unit(np.array([np.interp(a, Ar, Nr[:, k]) for k in range(3)]))


def skin_uv(body, m, anchor=(0.045, 0.995, 0.11), shrink=0.5):
    """UV continues sur une zone de peau unie (haut de la cuisse, hors pilosité) : projection plane de la pièce, ramenée
    dans l'îlot UV autour du point d'ancrage par l'application linéaire (x, y) → (u, v) estimée sur la peau voisine.
    Évite les coutures et les taches qu'on obtient en copiant l'UV du sommet le plus proche."""
    tree = cKDTree(body.V)
    a = body.V[tree.query(np.asarray(anchor))[1]]
    near = tree.query_ball_point(a, 0.035)
    X = np.c_[body.V[near][:, :2] - a[:2], np.ones(len(near))]
    J, *_ = np.linalg.lstsq(X, body.UV[near], rcond=None)
    c = m.V.mean(0)
    m.UV = (np.c_[(m.V[:, :2] - c[:2]) * shrink, np.ones(len(m.V))] @ J)
    return m


def _outward(m, axis_pts):
    """Oriente les faces vers l'extérieur d'un tube (axe = points du chemin)."""
    i = cKDTree(axis_pts).query(m.V)[1]
    if (vertex_normals(m) * (m.V - axis_pts[i])).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def _rigid(m, bone):
    m.J = np.zeros((len(m.V), 4), np.int64); m.J[:, 0] = bone; m.W = np.zeros((len(m.V), 4)); m.W[:, 0] = 1.0
    return m


def _blob(c, rx, ry, rz, n=24, sag=0.0):
    """Ellipsoïde (sphère UV) avec affaissement vers le bas."""
    th = np.linspace(0, np.pi, n // 2 + 1); ph = np.linspace(0, 2 * np.pi, n + 1)
    TH, PH = np.meshgrid(th, ph, indexing='ij')
    x = np.sin(TH) * np.cos(PH) * rx; y = np.cos(TH) * ry; z = np.sin(TH) * np.sin(PH) * rz
    low = np.clip(-y / ry, 0, 1)
    x = x * (1 + sag * low); z = z * (1 + 0.5 * sag * low); y = y - sag * ry * 0.4 * low ** 2
    P = np.stack([x, y, z], -1).reshape(-1, 3) + c
    R, Cc = TH.shape
    idx = np.arange(R * Cc).reshape(R, Cc)
    F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                        np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
    m = Mesh(P, F, np.zeros((len(P), 2)))
    if (vertex_normals(m) * (P - c)).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def male(body, sk, scale=1.0):
    """Organes masculins au repos : verge (racine, fût, gland avec couronne) devant un scrotum à deux lobes."""
    Pr, Nr, Ar = profile(body)
    root, nroot = at(Pr, Nr, Ar, 22)
    under, _ = at(Pr, Nr, Ar, 60)
    s = scale
    down = np.array([0, -1.0, 0]); fwd = np.array([0, 0, 1.0])
    # scrotum : une seule poche en poire, raphé médian discret, posée devant l'écart des cuisses
    sc = under + down * 0.026 * s + fwd * 0.058 * s
    scrot = _blob(sc, 0.029 * s, 0.033 * s, 0.025 * s, n=36, sag=0.22)
    q = (scrot.V - sc) / np.array([0.029, 0.033, 0.025]) / s
    top = np.clip(q[:, 1], 0, 1)
    scrot.V[:, 0] = sc[0] + (scrot.V[:, 0] - sc[0]) * (1 - 0.35 * top ** 1.5)          # col plus étroit en haut
    scrot.V[:, 2] = scrot.V[:, 2] - 0.0035 * s * np.exp(-((scrot.V[:, 0] - sc[0]) / (0.0045 * s)) ** 2) * (q[:, 2] > -0.2) * (1 - top)
    scrot.V[:, 1] = scrot.V[:, 1] + 0.003 * s * np.cos(np.clip((scrot.V[:, 0] - sc[0]) / (0.029 * s), -1, 1) * np.pi) * (q[:, 1] < -0.5)   # deux lobes en bas
    # verge : sort du pubis vers l'avant et retombe en douceur sur le devant du scrotum
    fz = sc[2] + 0.025 * s
    pts = np.array([root - nroot * 0.028 * s, root - nroot * 0.004 * s + fwd * 0.002 * s,
                    np.array([0, root[1] - 0.010 * s, root[2] + 0.026 * s]),
                    np.array([0, root[1] - 0.030 * s, fz + 0.012 * s]),
                    np.array([0, root[1] - 0.058 * s, fz + 0.014 * s]),
                    np.array([0, root[1] - 0.086 * s, fz + 0.008 * s])])
    from .geo import spline
    path = spline(pts, 40)
    t = np.linspace(0, 1, 40)
    r = 0.0135 * s * (1 + 0.05 * np.sin(np.pi * t)) * (1 + 0.30 * (1 - smoothstep(t / 0.14)))      # racine évasée qui se fond dans le pubis
    gl = smoothstep((t - 0.76) / 0.05)                                   # couronne du gland, discrète
    r = r * (1 + 0.04 * gl - 0.02 * smoothstep((t - 0.82) / 0.04)) * (1 - 0.12 * smoothstep((t - 0.90) / 0.10))
    shaft = _outward(Loft(path, r * 1.05, r, p=2.0, segs=28, ref=(0, 0, 1), cap0=2, cap1=8).mesh(0.1), path)
    m = concat([shaft, scrot])
    skin_uv(body, m)
    return _rigid(m, sk['Hips'])


def female(body, sk, scale=1.0):
    """Vulve : grandes lèvres (deux bourrelets doux de part et d'autre de la fente médiane) du mont de Vénus au périnée."""
    out = []
    for sx in (-1, 1):
        x = sx * 0.0058 * scale
        Pr, Nr, Ar = profile(body, x=x, angles=np.arange(14, 112, 3))
        sel = (Ar >= 20) & (Ar <= 104)
        P, N = Pr[sel], Nr[sel]
        n = len(P)
        t = np.linspace(0, 1, n)
        w = 0.0088 * scale * (0.40 + 0.60 * np.sin(np.pi * np.clip(t, 0, 1)) ** 0.7)
        h = 0.0050 * scale * (0.30 + 0.70 * np.sin(np.pi * np.clip(t, 0, 1)) ** 0.8)
        path = P - N * 0.0028                                            # crête qui dépasse de ~2 mm, bords fondus dans la peau
        side = np.array([1.0, 0, 0])
        lo = Loft(path, h, w, p=2.2, segs=20, ref=tuple(N.mean(0)), cap0=5, cap1=5)
        mm = _outward(lo.mesh(0.05), path)
        # la moitié intérieure (vers la fente) est un peu plus raide
        inner = (mm.V[:, 0] - x) * sx < 0
        mm.V[inner, 0] = x + (mm.V[inner, 0] - x) * 0.75
        out.append(mm)
    m = concat(out)
    skin_uv(body, m)
    return _rigid(m, sk['Hips'])
