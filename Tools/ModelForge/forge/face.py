"""Tête humaine paramétrique : crâne et visage sculptés par déformations (arcades, orbites, nez, pommettes, lèvres,
menton, mâchoire), yeux en globes séparés, oreilles, cheveux en mèches, barbe. Texture de peau peinte dans l'espace UV
(teint, rougeurs, lèvres, ombres des orbites, maquillage). Repère : centre de la tête en C, regard +Z."""
import numpy as np
from scipy.ndimage import gaussian_filter
from .geo import Mesh, Loft, spline, curve, concat, unit, rivet, vertex_normals, grid_faces, smoothstep
from .texgen import TexSet, fbm, noise, n01, height_to_normal

G = lambda x, s: np.exp(-(x / s) ** 2)


class FaceParams(dict):
    """Paramètres de morphologie (valeurs par défaut : homme adulte)."""
    def __init__(self, **kw):
        super().__init__(width=0.074, height=0.112, depth=0.098, jaw=1.0, chin=1.0, brow=1.0, nose=1.0, nose_len=1.0,
                         lips=1.0, cheek=1.0, female=False, eye_y=0.010, eye_x=0.032)
        self.update(kw)


def head_surface(fp, nu=96, nv=80):
    """Grille (nv, nu) de points de la tête. u : longitude (0 = arrière, 0.5 = avant), v : 0 = sous le cou → 1 = sommet."""
    u = np.linspace(0, 1, nu); v = np.linspace(0.08, 1, nv)
    U, V = np.meshgrid(u, v)
    th = 2 * np.pi * U - np.pi            # 0 → avant (+Z)
    ph = np.pi * (V - 0.5)                # -π/2 bas … +π/2 haut
    x = np.sin(th) * np.cos(ph); z = np.cos(th) * np.cos(ph); y = np.sin(ph)
    W, H, D = fp['width'], fp['height'], fp['depth']
    X = x * W; Y = y * H; Z = z * D
    fem = fp['female']
    # crâne plus large en haut-arrière, mâchoire plus étroite
    jawn = smoothstep((-Y - 0.010) / 0.085)
    X *= 1 - (0.30 if not fem else 0.38) * jawn * (0.7 + 0.3 * (Z > 0)) / fp['jaw']
    Z += np.where(Z < 0, -0.010 * smoothstep((Y + 0.02) / 0.08), 0)                       # occiput
    front = np.clip(Z / D, 0, 1)
    # aplatissement de la face, menton qui avance
    Z = np.where(Z > 0, Z * (0.93 + 0.05 * smoothstep((Y - 0.03) / 0.05)), Z)
    Z += 0.012 * fp['chin'] * G(Y + 0.095, 0.022) * G(X, 0.03) * front
    Y -= 0.010 * jawn * front                                                                  # menton plus bas que la nuque
    # orbites, arcades
    ex, ey = fp['eye_x'], fp['eye_y']
    for s in (1, -1):
        Z -= 0.010 * G(X - s * ex, 0.020) * G(Y - ey - 0.002, 0.014) * front                               # cavité orbitaire
        Z -= 0.009 * G(X - s * ex, 0.0125) * G(Y - ey, 0.0058) * front                                     # fente palpébrale (amande)
        Z += 0.0035 * G(X - s * ex, 0.013) * G(Y - ey - 0.0085, 0.0028) * front                             # pli de la paupière supérieure
        Z += 0.0020 * G(X - s * ex, 0.012) * G(Y - ey + 0.0075, 0.0025) * front                             # paupière inférieure
        Z += 0.006 * fp['brow'] * G(X - s * ex * 0.95, 0.022) * G(Y - ey - 0.022, 0.008) * front * (0.6 if fem else 1)
        Z += 0.006 * fp['cheek'] * G(X - s * 0.046, 0.016) * G(Y + 0.012, 0.016) * front                 # pommettes
        X += s * 0.004 * fp['cheek'] * G(Y + 0.015, 0.02) * front * (np.sign(X) == s)
        Z -= 0.004 * G(X - s * 0.040, 0.012) * G(Y + 0.045, 0.02) * front                                  # creux des joues
    # nez : arête, pointe, ailes
    nl = fp['nose_len']; nz = fp['nose'] * (0.85 if fem else 1)
    ridge = np.clip((ey + 0.008 - Y) / (0.050 * nl), 0, 1)
    Z += nz * (0.006 + 0.019 * ridge ** 1.2) * G(X, 0.0048 + 0.0055 * ridge) * (Y > ey - 0.058 * nl) * (Y < ey + 0.01) * front
    tipy = ey - 0.052 * nl
    Z += nz * 0.008 * G(X, 0.0085) * G(Y - tipy, 0.0075) * front
    for s in (1, -1):
        Z += nz * 0.006 * G(X - s * 0.011, 0.0055) * G(Y - tipy - 0.002, 0.0055) * front
    Z -= 0.004 * G(X, 0.006) * G(Y - tipy + 0.010, 0.004) * front * 0                                      # philtrum
    # bouche
    my = ey - 0.084 * nl
    lip = fp['lips'] * (1.25 if fem else 1.0)
    bow = 0.0018 * (G(X - 0.006, 0.004) + G(X + 0.006, 0.004))
    Z += 0.0055 * lip * G(X, 0.019) * G(Y - my - 0.0055 - bow, 0.0040) * front
    Z += 0.0065 * lip * G(X, 0.016) * G(Y - my + 0.0065, 0.0048) * front
    Z -= 0.0045 * G(X, 0.023) * G(Y - my, 0.0016) * front
    for s in (1, -1): Z -= 0.003 * G(X - s * 0.024, 0.004) * G(Y - my, 0.004) * front
    Z += 0.003 * G(X, 0.02) * G(Y - my - 0.016, 0.006) * front                                             # lèvre sup / philtrum
    P = np.stack([X, Y, Z], -1)
    return P, U, V, dict(eye=(ex, ey), mouth=my, nose_tip=tipy)


def head_mesh(fp, center):
    P, U, V, marks = head_surface(fp)
    nv, nu = P.shape[:2]
    m = Mesh((P + center).reshape(-1, 3), grid_faces(nv, nu), np.stack([U.ravel(), (V.ravel() - 0.08) / 0.92], 1))
    # couture arrière : souder les normales (même position en u=0 et u=1) — vertex_normals soude les doublons
    n = vertex_normals(m)
    if (n * (m.V - center)).sum() < 0: m.flip()
    return m, marks


def _surface_z(fp, x, y):
    P, U, V, _ = head_surface(fp)
    front = P[..., 2] > 0
    d = (P[..., 0] - x) ** 2 + (P[..., 1] - y) ** 2 + (~front) * 10
    i = np.unravel_index(np.argmin(d), d.shape)
    return P[i][2]


def eye_meshes(fp, center, marks, r=0.0118):
    ex, ey = marks['eye']
    out = []
    for s in (1, -1):
        c = np.array(center) + (s * ex, ey, _surface_z(fp, s * ex, ey) - r * 0.62)
        segs, rings = 20, 14
        th = np.linspace(0, np.pi, rings); ph = np.linspace(0, 2 * np.pi, segs + 1)
        TH, PH = np.meshgrid(th, ph, indexing='ij')
        d = np.stack([np.sin(TH) * np.cos(PH), np.sin(TH) * np.sin(PH), np.cos(TH)], -1)    # +Z = pupille
        Vv = c + d * r
        UV = np.stack([0.5 + 0.5 * d[..., 0], 0.5 + 0.5 * d[..., 1]], -1)
        m = Mesh(Vv.reshape(-1, 3), grid_faces(rings, segs + 1), UV.reshape(-1, 2))
        if (vertex_normals(m) * (m.V - c)).sum() < 0: m.flip()
        out.append(m)
    return concat(out)


def ears(fp, center, scale=1.0):
    ms = []
    for s in (1, -1):
        base = np.array(center) + (s * fp['width'] * 0.98, 0.0, -0.005)
        path = spline([base + (0, -0.026, 0.004), base + (s * 0.010, 0.0, -0.004), base + (s * 0.008, 0.026, -0.010)], 14)
        lo = Loft(path, curve([(0, .006), (.5, .007), (1, .004)], 14), curve([(0, .010), (.5, .016), (1, .010)], 14), p=2.2, segs=14, ref=(s, 0, 0), cap0=2, cap1=2)
        ms.append(lo.mesh(0.05))
    return concat(ms)


def hair(fp, center, style='long', rng=None, n=520, length=0.45):
    """Cheveux : calotte + mèches (rubans épais) qui partent du cuir chevelu et retombent avec la gravité.
    style : 'long' (longs, raie au milieu), 'short' (courts, coiffés vers l'arrière)."""
    rng = rng or np.random.default_rng(3)
    P, U, V, _ = head_surface(fp)
    c = np.array(center)
    ms = []
    # calotte : surface de la tête au-dessus de la ligne de front, légèrement gonflée
    # ligne des cheveux : arrondie sur le front, plus basse aux tempes et dans la nuque
    hairline = lambda u: 0.62 + 0.13 * np.clip(np.cos(2 * np.pi * (u - 0.5)), 0, 1) ** 1.5
    lift = smoothstep((V - hairline(U)) / 0.05)                       # 0 sous la ligne, 1 au-dessus
    rows = np.where((lift > 0.001).any(1))[0]
    capP = P * (1.0 + 0.012 * lift[..., None]) - P * (1 - lift[..., None]) * 0.004
    cap = capP[rows[0]:]
    capm = Mesh((cap + c).reshape(-1, 3), grid_faces(cap.shape[0], cap.shape[1]), np.stack([U[rows[0]:].ravel() * 4, V[rows[0]:].ravel() * 2], 1))
    # supprime les facettes sous la ligne des cheveux (la peau reste visible)
    keep = (lift[rows[0]:].ravel()[capm.F] > 0.02).all(1); capm.F = capm.F[keep]
    if (vertex_normals(capm) * (capm.V - c)).sum() < 0: capm.flip()
    ms.append(capm)
    for k in range(n):
        u = rng.uniform(0, 1)
        if style == 'long' and abs(u - 0.5) < 0.12: u = 0.5 + np.sign(u - 0.5 + 1e-6) * rng.uniform(0.12, 0.2)
        if style == 'short' and abs(u - 0.5) < 0.06: continue
        vv = rng.uniform(hairline(u) + 0.01, 0.98)
        i = int(np.clip(vv, 0.08, 1) * 0 + (vv - 0.08) / 0.92 * (P.shape[0] - 1)); j = int(u * (P.shape[1] - 1))
        root = P[i, j] * 1.015; nrm = unit(root)
        side = 1 if root[0] >= 0 else -1
        if style == 'long':
            L = length * rng.uniform(0.75, 1.1)
            back = -1 if root[2] < 0.03 else 0.2
            pts = [root, root + nrm * 0.03 + np.array([0, -0.01, 0]),
                   root + nrm * 0.045 + np.array([side * 0.02, -0.08, back * 0.03]),
                   np.array([root[0] * 1.5 + side * 0.03, -0.12 - 0.3 * L * 0.5, root[2] * 0.5 - 0.03 + 0.02 * back]),
                   np.array([root[0] * 1.7 + side * 0.04 + rng.normal(0, 0.02), -0.10 - L, root[2] * 0.4 - 0.05 + rng.normal(0, 0.02)])]
            w0, w1 = 0.020, 0.008
        else:
            L = 0.06 * rng.uniform(0.7, 1.2)
            pts = [root, root + nrm * 0.007 + np.array([0, 0.003, -0.02]), root + nrm * 0.008 + np.array([0, -0.004, -0.045]) * (L / 0.06)]
            w0, w1 = 0.016, 0.006
        pth = spline(pts, 12 if style == 'long' else 5)
        lo = Loft(pth + c, curve([(0, .0035), (1, .0015)], len(pth)), curve([(0, w0), (.5, w0 * 0.9), (1, w1)], len(pth)), p=2.0, segs=6, ref=tuple(nrm))
        mm = lo.mesh(1.0); mm.UV = np.c_[np.tile(np.linspace(0, 1, 7), len(pth))[:len(mm.V)] * 0.2 + rng.uniform(0, 0.8), np.repeat(np.linspace(0, 1, len(pth)), 7)[:len(mm.V)] * 2]
        ms.append(mm)
    return concat(ms)


def beard(fp, center, rng=None, n=520):
    rng = rng or np.random.default_rng(9)
    P, U, V, marks = head_surface(fp)
    c = np.array(center); ms = []
    my = marks['mouth']
    sel = []
    for i in range(P.shape[0]):
        for j in range(P.shape[1]):
            x, y, z = P[i, j]
            if z > 0.01 and y < my + 0.012 and y > -0.13 and abs(x) < 0.07 and not (abs(x) < 0.022 and abs(y - my) < 0.008):
                sel.append((i, j))
    for k in range(n):
        i, j = sel[rng.integers(len(sel))]
        root = P[i, j] * 1.008; nrm = unit(root)
        L = 0.012 + 0.018 * rng.random() * (1 + (root[1] < -0.09))
        tip = root + unit(nrm * 0.35 + np.array([0, -1, 0.1])) * L
        lo = Loft(spline([root, (root + tip) / 2 + nrm * 0.003, tip], 5) + c, curve([(0, .0025), (1, .001)], 5), curve([(0, .007), (1, .002)], 5), p=2.0, segs=5, ref=tuple(nrm))
        ms.append(lo.mesh(1.0))
    # moustache
    for s in (1, -1):
        root = np.array([s * 0.004, my + 0.011, _surface_z(fp, s * 0.004, my + 0.011) + 0.002])
        tip = root + np.array([s * 0.026, -0.018, -0.010])
        lo = Loft(spline([root, root + (s * 0.016, -0.002, 0.002), tip], 8) + c, curve([(0, .004), (1, .0015)], 8), curve([(0, .007), (1, .002)], 8), p=2.0, segs=6, ref=(0, 1, 0))
        ms.append(lo.mesh(1.0))
    return concat(ms)


# ------------------------------------------------------------------------------------------- textures
def skin_texture(fp, tone=(0.80, 0.62, 0.52), S=1024, seed=1, makeup=None, lip=(0.62, 0.30, 0.30), pale=0.0, stubble=0.0, scars=False):
    """Peau peinte dans l'espace UV de head_surface (u, v)."""
    r = np.random.default_rng(seed)
    nu, nv = S, S
    u = np.linspace(0, 1, nu); v = np.linspace(1, 0.08, nv)         # image : ligne 0 = haut = v = 1
    U, V = np.meshgrid(u, v)
    th = 2 * np.pi * U - np.pi; ph = np.pi * (V - 0.5)
    X = np.sin(th) * np.cos(ph) * fp['width']; Y = np.sin(ph) * fp['height']; Z = np.cos(th) * np.cos(ph)
    front = np.clip(Z, 0, 1)
    ex, ey = fp['eye_x'], fp['eye_y']; my = ey - 0.084 * fp['nose_len']
    base = np.array(tone)[None, None] * (1 + 0.04 * fbm(nv, nu, r, base=S / 6, octaves=4)[..., None])
    base = base * (1 - pale) + np.array([0.86, 0.84, 0.86]) * pale
    blush = (G(X - 0.045, 0.02) * G(Y + 0.01, 0.02) + G(X + 0.045, 0.02) * G(Y + 0.01, 0.02)) * front
    base = base * (1 - 0.10 * blush[..., None]) + np.array([0.80, 0.45, 0.42]) * 0.10 * blush[..., None]
    lipm = np.clip(G(X, 0.020) * (G(Y - my - 0.006, 0.0045) + G(Y - my + 0.007, 0.0050)) * front * 1.6, 0, 1)
    base = base * (1 - lipm[..., None]) + np.array(lip) * lipm[..., None]
    sock = sum(G(X - s * ex, 0.017) * G(Y - ey, 0.010) for s in (1, -1)) * front
    base = base * (1 - 0.25 * sock[..., None])
    if makeup is not None:   # fard sombre autour des yeux
        mk = sum(G(X - s * ex * 1.05, 0.020) * G(Y - ey - 0.004, 0.010) for s in (1, -1)) * front
        base = base * (1 - 0.6 * mk[..., None]) + np.array(makeup) * 0.6 * mk[..., None]
    brows = sum(G(X - s * ex * 1.0, 0.016) * G(Y - ey - 0.021 + 0.004 * ((X * s - ex) / 0.016) ** 2, 0.0028) for s in (1, -1)) * front
    base = base * (1 - 0.75 * np.clip(brows, 0, 1)[..., None]) + np.array([0.06, 0.04, 0.03]) * 0.75 * np.clip(brows, 0, 1)[..., None]
    if stubble:
        st = np.clip((Y < my + 0.012) * (Y > -0.13) * front * 2, 0, 1) * (r.random((nv, nu)) > 0.5)
        base = base * (1 - stubble * 0.5 * gaussian_filter(st.astype(float), 0.6)[..., None])
    pores = noise(nv, nu, 1.2, r) * 0.6 + fbm(nv, nu, r, base=24, octaves=3) * 0.4
    hgt = pores * 0.5 - 2.0 * np.clip(brows, 0, 1)
    if scars:
        sc = G(X - 0.03 - (Y - 0.0) * 0.4, 0.0015) * (Y < 0.04) * (Y > -0.05) * front
        base = base * (1 - 0.3 * sc[..., None]) + np.array([0.7, 0.4, 0.38]) * 0.3 * sc[..., None]; hgt += 3 * sc
    smooth = np.clip(0.38 + 0.15 * lipm - 0.1 * np.clip(brows, 0, 1) + 0.05 * front, 0, 1)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.4), np.zeros((nv, nu)), smooth, np.clip(1 - 0.3 * sock, 0, 1))


def eye_texture(iris=(0.35, 0.25, 0.6), glow=None, S=256):
    yy, xx = np.mgrid[0:S, 0:S] / (S - 1)
    rr = np.hypot(xx - 0.5, yy - 0.5) * 2
    base = np.ones((S, S, 3)) * np.array([0.86, 0.84, 0.82])
    ir = rr < 0.42
    ang = np.arctan2(yy - 0.5, xx - 0.5)
    streak = 0.75 + 0.25 * np.sin(ang * 40) * np.sin(ang * 13 + rr * 20)
    base[ir] = (np.array(iris) * streak[..., None] * (0.6 + 0.6 * rr[..., None] / 0.42))[ir]
    base[rr < 0.16] = 0.01
    base = base * (1 - 0.6 * np.clip((rr - 0.40) * 30, 0, 1) * (rr < 0.46))[..., None]
    emit = None
    if glow is not None:
        emit = np.zeros((S, S, 3)); emit[ir] = np.array(glow); emit = gaussian_filter(emit, (2, 2, 0))
    z = np.zeros((S, S))
    return TexSet(base, np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.88, z + 1, emit)


def hair_texture(color=(0.05, 0.035, 0.03), S=512, seed=4):
    r = np.random.default_rng(seed)
    st = fbm(S, S, r, base=S / 8, octaves=5, aniso=(4.0, 0.08))
    fine = noise(S, S, 1.0, r)
    hgt = st * 2 + fine * 0.5
    base = np.array(color)[None, None] * (0.75 + 0.35 * n01(st)[..., None])
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), np.clip(0.45 + 0.2 * n01(st), 0, 1), np.clip(0.7 + 0.3 * n01(st), 0, 1))
