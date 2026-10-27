"""Ville de Korvalune, bâtie aux abords du château : maisons à colombages sur rez-de-chaussée de pierre (étages en
encorbellement, pignons, cheminées, volets, jardinières), taverne à enseigne, forge à auvent et foyer rougeoyant, étals du
marché à bâches rayées, fontaine, puits, lanternes, tonneaux, caisses, charrettes, bottes de foin ; enceinte de ville
(courtines crénelées, tours rondes, portes voûtées praticables). Pièces statiques assemblées dans Unity par
Korvalune.EditorTools.TownBuilder. Origine au sol, façade principale vers −Z. Dimensions en mètres."""
import numpy as np
from forge.rig import Asset
from forge.geo import Mesh, concat, unit
from world.props import box, cylinder, cone, merlons_line, merlons_ring
from world import materials as WM
from forge.texgen import TexSet, fbm, n01, height_to_normal, cavity_ao

STONE, ROOF = 'W_Stone', 'W_Roof'
PLASTER, TIMBER, TILE, GLASS, COBBLE = 'T_Plaster', 'T_Timber', 'T_Tile', 'T_Glass', 'T_Cobble'
IRON, EMBER, PLANT, HAY, WATER = 'T_Iron', 'T_Ember', 'T_Plant', 'T_Hay', 'T_Water'
CLOTH = ['T_ClothRed', 'T_ClothCream', 'T_ClothBlue', 'T_ClothGreen']
DARK, WOOD = 'W_Dark', 'W_Wood'


# ------------------------------------------------------------------------------------------- géométrie
def Rx(a): c, s = np.cos(a), np.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
def Ry(a): c, s = np.cos(a), np.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
def Rz(a): c, s = np.cos(a), np.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def obox(c, size, R=None, uv=0.5):
    """Pavé centré en c, de taille size, orienté par la matrice R (UV en mètres locaux)."""
    m = box((0, 0, 0), size, uv=uv)
    if R is not None: m.V = m.V @ np.asarray(R).T
    m.V = m.V + np.asarray(c, float)
    return m


def prism(pts2d, depth, center, axis='x', uv=0.5):
    """Polygone convexe 2D (u, y) extrudé sur depth : pignons. axis : direction d'extrusion ('x' ou 'z')."""
    P = np.asarray(pts2d, float); n = len(P); c = np.asarray(center, float)
    def to3(u, y, w):
        return (c + (np.array([w, y, u]) if axis == 'x' else np.array([u, y, w])))
    ms = []
    for sgn in (-1, 1):
        V = np.array([to3(u, y, sgn * depth / 2) for u, y in P])
        F = np.array([[0, i, i + 1] for i in range(1, n - 1)])
        if sgn < 0: F = F[:, ::-1]
        if axis == 'x': F = F[:, ::-1]
        UV = P * uv
        ms.append(Mesh(V, F.copy(), UV))
    for i in range(n):
        a, b = P[i], P[(i + 1) % n]
        V = np.array([to3(a[0], a[1], -depth / 2), to3(b[0], b[1], -depth / 2), to3(b[0], b[1], depth / 2), to3(a[0], a[1], depth / 2)])
        L = np.linalg.norm(b - a)
        F = np.array([[0, 1, 2], [0, 2, 3]]) if axis == 'x' else np.array([[0, 2, 1], [0, 3, 2]])
        ms.append(Mesh(V, F, np.array([[0, 0], [L, 0], [L, depth], [0, depth]]) * uv))
    m = concat(ms)
    # orientation : normales vers l'extérieur
    from forge.geo import vertex_normals
    if (vertex_normals(m) * (m.V - m.V.mean(0))).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def _static(a):
    for _, m, _ in a.parts:
        m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
    return a


def _merge(a):
    merged = {}
    for nm, m, mat in a.parts: merged.setdefault(mat, []).append(m)
    a.parts = [(mat.lower().replace('w_', '').replace('t_', ''), concat(ms), mat) for mat, ms in merged.items()]
    return _static(a)


class B:
    """Petit constructeur : accumule les pièces par matériau."""
    def __init__(self, name): self.a = Asset(name)
    def add(self, m, mat): self.a.add('p', m, mat); return m
    def done(self): return _merge(self.a)


# ------------------------------------------------------------------------------------------- éléments de façade
def window(b, c, face, w=0.9, h=1.2, shutters=True, flowers=False, rng=None, lit=True):
    """Fenêtre posée sur une façade : face = normale extérieure ('-z', '+z', '-x', '+x'). Encadrement de bois, vitre
    (légèrement lumineuse), meneau, volets ouverts, appui, jardinière fleurie."""
    c = np.asarray(c, float)
    n = {'-z': np.array([0, 0, -1.]), '+z': np.array([0, 0, 1.]), '-x': np.array([-1., 0, 0]), '+x': np.array([1., 0, 0])}[face]
    t = np.cross([0, 1, 0], n); t = t / np.linalg.norm(t)
    R = np.column_stack([t, [0, 1, 0], n])                                    # axes locaux : x = tangente, z = normale
    def L(x, y, z): return c + t * x + np.array([0, y, 0]) + n * z
    b.add(obox(L(0, 0, 0.02), (w, h, 0.06), R), GLASS if lit else DARK)
    for sx in (-1, 1): b.add(obox(L(sx * (w / 2 + 0.06), 0, 0.06), (0.12, h + 0.2, 0.12), R), TIMBER)
    for sy in (-1, 1): b.add(obox(L(0, sy * (h / 2 + 0.06), 0.06), (w + 0.24, 0.12, 0.12), R), TIMBER)
    b.add(obox(L(0, 0, 0.06), (0.06, h, 0.05), R), TIMBER); b.add(obox(L(0, h * 0.1, 0.06), (w, 0.05, 0.05), R), TIMBER)
    b.add(obox(L(0, -h / 2 - 0.14, 0.12), (w + 0.4, 0.08, 0.26), R), STONE)                  # appui
    if shutters:
        ang = 1.25 if rng is None else rng.uniform(1.0, 1.5)
        for sx in (-1, 1):
            hinge = L(sx * (w / 2 + 0.12), 0, 0.12)
            Rs = R @ Ry(-sx * ang)
            ctr = hinge + (Rs @ np.array([sx * w / 4, 0, 0]))
            b.add(obox(ctr, (w / 2, h, 0.05), Rs), WOOD)
            for yy in (-h / 3, h / 3): b.add(obox(ctr + Rs @ np.array([0, yy, 0.03]), (w / 2 - 0.05, 0.06, 0.02), Rs), IRON)
    if flowers:
        b.add(obox(L(0, -h / 2 - 0.32, 0.3), (w + 0.2, 0.26, 0.28), R), WOOD)
        r = rng or np.random.default_rng(0)
        for k in range(7):
            x = -w / 2 + (k + 0.5) * w / 7
            b.add(obox(L(x, -h / 2 - 0.12 + r.uniform(0, 0.08), 0.3 + r.uniform(-0.05, 0.05)), (0.16, 0.2, 0.16), R @ Ry(r.uniform(0, 1))), PLANT)
            if k % 2 == 0: b.add(obox(L(x, -h / 2 - 0.0, 0.32), (0.09, 0.09, 0.09), R), CLOTH[int(r.integers(0, 4))])


def door(b, c, face, w=1.3, h=2.3, arch=True):
    c = np.asarray(c, float)
    n = {'-z': np.array([0, 0, -1.]), '+z': np.array([0, 0, 1.]), '-x': np.array([-1., 0, 0]), '+x': np.array([1., 0, 0])}[face]
    t = np.cross([0, 1, 0], n); t = t / np.linalg.norm(t); R = np.column_stack([t, [0, 1, 0], n])
    def L(x, y, z): return c + t * x + np.array([0, y, 0]) + n * z
    b.add(obox(L(0, h / 2, 0.0), (w, h, 0.1), R), WOOD)
    for k in range(5): b.add(obox(L(-w / 2 + (k + 0.5) * w / 5, h / 2, 0.06), (w / 5 - 0.03, h - 0.05, 0.03), R), WOOD)
    for yy in (0.4, h - 0.5): b.add(obox(L(0, yy, 0.09), (w * 0.9, 0.08, 0.03), R), IRON)
    b.add(obox(L(w * 0.32, h * 0.48, 0.12), (0.08, 0.16, 0.06), R), IRON)                       # poignée
    for sx in (-1, 1): b.add(obox(L(sx * (w / 2 + 0.17), h / 2 + 0.05, 0.08), (0.3, h + 0.1, 0.22), R), STONE)
    b.add(obox(L(0, h + 0.2, 0.08), (w + 0.7, 0.36, 0.24), R), STONE)                          # linteau
    b.add(obox(L(0, -0.06, 0.45), (w + 0.6, 0.16, 0.7), R), STONE)                            # marche
    if arch:
        for k in range(7):
            a0 = np.pi * (k + 0.5) / 7
            b.add(obox(L(np.cos(a0) * (w / 2 + 0.15), h + 0.4 + np.sin(a0) * 0.35, 0.1), (0.24, 0.2, 0.22), R @ Rz(a0 - np.pi / 2)), STONE)


# ------------------------------------------------------------------------------------------- maison à colombages
def _timber_frame(b, x0, x1, y0, y1, z, face_n, rng, braces=True):
    """Pan de bois sur une façade plane (de x0 à x1, hauteur y0..y1, plan z, normale ±z)."""
    s = np.sign(face_n)
    zz = z + s * 0.05
    H = y1 - y0; W = x1 - x0
    for y in (y0 + 0.08, y1 - 0.08): b.add(box(((x0 + x1) / 2, y, zz), (W, 0.18, 0.12)), TIMBER)
    n = max(2, int(round(W / 1.25)))
    xs = np.linspace(x0 + 0.1, x1 - 0.1, n + 1)
    for x in xs: b.add(box((x, (y0 + y1) / 2, zz), (0.16, H, 0.12)), TIMBER)
    b.add(box(((x0 + x1) / 2, y0 + H * 0.38, zz), (W, 0.12, 0.1)), TIMBER)                    # entretoise
    if braces:
        for i in (0, n - 1):
            xa, xb = xs[i], xs[i + 1]; L = np.hypot(xb - xa, H * 0.38); ang = np.arctan2(H * 0.38, (xb - xa)) * (1 if i == 0 else -1)
            b.add(obox(((xa + xb) / 2, y0 + H * 0.19, zz), (L, 0.12, 0.1), Rz(ang)), TIMBER)


def _side_frame(b, z0, z1, y0, y1, x, face_n):
    s = np.sign(face_n); xx = x + s * 0.05; H = y1 - y0; D = z1 - z0
    for y in (y0 + 0.08, y1 - 0.08): b.add(box((xx, y, (z0 + z1) / 2), (0.12, 0.18, D)), TIMBER)
    n = max(2, int(round(D / 1.4)))
    for z in np.linspace(z0 + 0.1, z1 - 0.1, n + 1): b.add(box((xx, (y0 + y1) / 2, z), (0.12, H, 0.16)), TIMBER)
    b.add(box((xx, y0 + H * 0.5, (z0 + z1) / 2), (0.1, 0.12, D)), TIMBER)


def house(seed=0, w=None, d=None, floors=None, roof=None, name=None):
    """Maison de ville : rez-de-chaussée en pierre (porte cintrée, fenêtres à volets), un ou deux étages à pans de bois
    en encorbellement, toit à deux pentes raides (tuiles ou ardoises) débordant, pignons à colombages, cheminée."""
    r = np.random.default_rng(seed)
    w = w or float(r.choice([6.0, 7.0, 8.0])); d = d or float(r.choice([6.0, 7.0]))
    floors = floors or int(r.choice([1, 2, 2]))
    roofmat = roof or (TILE if r.uniform() < 0.6 else ROOF)
    b = B(name or f'M_Maison{seed}')
    h0, h1, j = 3.2, 2.7, 0.35
    # fondation et rez-de-chaussée
    b.add(box((0, -0.15, 0), (w + 0.3, 0.5, d + 0.3)), STONE)
    b.add(box((0, h0 / 2, 0), (w, h0, d)), STONE)
    for sx in (-1, 1):
        for sz in (-1, 1): b.add(box((sx * (w / 2 - 0.15), h0 / 2, sz * (d / 2 - 0.15)), (0.42, h0, 0.42)), STONE)   # chaînes d'angle
    dx = float(r.uniform(-w / 2 + 1.5, w / 2 - 1.5)) * 0.5
    door(b, (dx, 0.0, -d / 2), '-z')
    for x in (-w / 2 + 1.1, w / 2 - 1.1):
        if abs(x - dx) > 1.5: window(b, (x, 1.7, -d / 2), '-z', 0.8, 1.0, True, False, r)
    window(b, (0, 1.7, d / 2), '+z', 0.8, 1.0, True, False, r)
    for sx in (-1, 1): window(b, (sx * w / 2, 1.7, 0), '+x' if sx > 0 else '-x', 0.7, 0.9, False, False, r, lit=r.uniform() < 0.5)
    # étages à pans de bois en encorbellement
    y = h0; W, D = w, d
    for f in range(floors):
        W, D = W + 2 * j * (1 if f == 0 else 0.6), D + 2 * j * (1 if f == 0 else 0.6)
        for sx in (-1, 1): b.add(box((0, y + 0.09, sx * (D / 2 - 0.25)), (W, 0.18, 0.5)), TIMBER)            # sablières
        for k in range(int(W / 0.9)):                                                                          # abouts de solives
            x = -W / 2 + (k + 0.5) * W / int(W / 0.9)
            for sz in (-1, 1): b.add(box((x, y - 0.1, sz * (D / 2 - 0.1)), (0.16, 0.2, 0.5)), TIMBER)
        b.add(box((0, y + h1 / 2, 0), (W, h1, D)), PLASTER)
        _timber_frame(b, -W / 2, W / 2, y, y + h1, -D / 2, -1, r)
        _timber_frame(b, -W / 2, W / 2, y, y + h1, D / 2, 1, r)
        for sx in (-1, 1): _side_frame(b, -D / 2, D / 2, y, y + h1, sx * W / 2, sx)
        nwin = 2 if W < 7.5 else 3
        for k in range(nwin):
            x = -W / 2 + (k + 0.5) * W / nwin
            window(b, (x, y + h1 * 0.55, -D / 2 - 0.08), '-z', 0.75, 1.05, True, r.uniform() < 0.6, r)
            window(b, (x, y + h1 * 0.55, D / 2 + 0.08), '+z', 0.75, 1.05, True, r.uniform() < 0.3, r)
        y += h1
    # toit : faîtage selon X (pignons sur les côtés), pente ~52°
    pitch = np.radians(r.uniform(50, 56)); ov = 0.55
    half = D / 2 + ov; rise = half * np.tan(pitch); L = half / np.cos(pitch)
    for sz in (-1, 1):
        R = Rx(sz * pitch)
        c = np.array([0, y + rise / 2 - 0.05, sz * (half / 2) - sz * 0.0])
        b.add(obox(c + np.array([0, 0.12, 0]), (W + 2 * ov, 0.22, L + 0.05), R), roofmat)
        b.add(obox(c + np.array([0, 0.0, 0]), (W + 2 * ov - 0.2, 0.12, L), R), TIMBER)                     # voligeage (dessous)
        # rives de bois
        for sx in (-1, 1): b.add(obox(c + np.array([sx * (W / 2 + ov - 0.05), 0.15, 0]), (0.14, 0.3, L + 0.1), R), TIMBER)
    b.add(box((0, y + rise + 0.12, 0), (W + 2 * ov + 0.1, 0.24, 0.36)), roofmat)                              # faîtière
    gable = [(-D / 2, 0), (D / 2, 0), (0, D / 2 * np.tan(pitch))]
    for sx in (-1, 1):
        b.add(prism(gable, 0.2, (sx * (W / 2 - 0.1), y, 0), 'x'), PLASTER)
        xx = sx * (W / 2 + 0.02)
        b.add(box((xx, y + 0.1, 0), (0.14, 0.18, D)), TIMBER)
        b.add(box((xx, y + D / 4 * np.tan(pitch), 0), (0.14, D / 2 * np.tan(pitch) * 1.0, 0.16)), TIMBER)    # poinçon
        for sz in (-1, 1):
            ang = np.arctan2(D / 2 * np.tan(pitch), D / 2); Lg = np.hypot(D / 2, D / 2 * np.tan(pitch))
            b.add(obox((xx, y + D / 4 * np.tan(pitch), sz * D / 4), (0.12, 0.16, Lg), Rx(sz * ang)), TIMBER)
        window(b, (xx + sx * 0.08, y + D / 2 * np.tan(pitch) * 0.35, 0), '+x' if sx > 0 else '-x', 0.55, 0.7, False, False, r)
    # cheminée
    cx = float(r.choice([-1, 1])) * (W / 2 - 1.0); cz = float(r.uniform(-0.6, 0.6))
    top = y + rise + 1.4
    b.add(box((cx, top / 2 + 0.5, cz + d * 0.1), (0.9, top - 0.5, 0.9)), STONE)
    b.add(box((cx, top + 0.1, cz + d * 0.1), (1.1, 0.2, 1.1)), STONE)
    for sx in (-1, 1): b.add(box((cx + sx * 0.25, top + 0.4, cz + d * 0.1), (0.2, 0.5, 0.2)), STONE)
    a = b.done(); a.meta = dict(width=W, depth=D, height=top, door=(dx, -d / 2), chimney=(cx, top + 0.5, cz + d * 0.1))
    return a


def tavern():
    """Taverne « Le Sanglier Noir » (pas de texte : enseigne peinte d'une silhouette) : grande maison à deux étages,
    enseigne en fer forgé, lanternes, tonneaux devant."""
    a = house(seed=41, w=11.0, d=8.0, floors=2, roof=TILE, name='M_Taverne')
    b = B('tmp')
    # potence et enseigne
    b.add(box((2.8, 4.6, -4.6), (0.12, 0.12, 1.6)), IRON); b.add(box((2.8, 4.1, -4.0), (0.08, 1.0, 0.08)), IRON)
    b.add(box((2.8, 3.6, -5.2), (0.9, 0.7, 0.08)), WOOD)
    b.add(box((2.8, 3.62, -5.25), (0.6, 0.35, 0.02)), DARK)                     # silhouette peinte
    for x in (-1.6, 1.6):
        b.add(box((x, 2.9, -4.12), (0.06, 0.4, 0.3)), IRON); lantern(b, (x, 2.55, -4.35), small=True)
    for k, x in enumerate((-4.2, -3.3, 4.3)): barrel(b, (x, 0, -4.8 - 0.3 * (k % 2)))
    b.add(box((-2.6, 0.45, -5.3), (2.2, 0.08, 0.6)), WOOD)                        # banc
    for x in (-3.5, -1.7): b.add(box((x, 0.22, -5.3), (0.1, 0.44, 0.5)), WOOD)
    for p in b.a.parts: a.parts.append(p)
    _merge(a); return a


def lantern(b, c, small=False):
    c = np.asarray(c, float); s = 0.7 if small else 1.0
    b.add(box(c, (0.3 * s, 0.4 * s, 0.3 * s)), GLASS)
    for sx in (-1, 1):
        for sz in (-1, 1): b.add(box(c + (sx * 0.15 * s, 0, sz * 0.15 * s), (0.04, 0.44 * s, 0.04)), IRON)
    b.add(box(c + (0, 0.24 * s, 0), (0.38 * s, 0.06, 0.38 * s)), IRON)
    b.add(cone(c + (0, 0.27 * s, 0), 0.2 * s, 0.18 * s, segs=4, flare=0.0), IRON)
    b.add(box(c + (0, -0.22 * s, 0), (0.34 * s, 0.05, 0.34 * s)), IRON)


def barrel(b, c, h=1.0, r=0.42):
    c = np.asarray(c, float)
    for k in range(8):                                          # douelles bombées
        t0 = k / 8
        y0 = h * t0; y1 = h * (t0 + 1 / 8); rr = r * (0.86 + 0.14 * np.sin(np.pi * (t0 + 1 / 16)))
        b.add(cylinder(c + (0, y0, 0), rr, y1 - y0, segs=14, cap_top=(k == 7)), WOOD)
    for y in (0.12, 0.35, 0.65, 0.88):
        rr = r * (0.88 + 0.14 * np.sin(np.pi * y)) + 0.015
        b.add(cylinder(c + (0, h * y - 0.03, 0), rr, 0.06, segs=14, cap_top=False), IRON)


def crate(b, c, s=0.8, rot=0.0):
    c = np.asarray(c, float)
    b.add(box(c + (0, s / 2, 0), (s, s, s), rot=rot), WOOD)
    R = Ry(rot)
    for sx in (-1, 1):
        for sz in (-1, 1): b.add(obox(c + R @ np.array([sx * s / 2, s / 2, sz * s / 2]), (0.08, s + 0.02, 0.08), R), TIMBER)
    for sy in (0.03, s - 0.03):
        for sz in (-1, 1): b.add(obox(c + R @ np.array([0, sy, sz * s / 2]), (s + 0.02, 0.08, 0.08), R), TIMBER)


# ------------------------------------------------------------------------------------------- forge
def forge():
    b = B('M_Forge')
    w, d, h = 7.0, 6.0, 3.6
    b.add(box((0, -0.15, 0.5), (w + 0.3, 0.5, d + 4.5)), STONE)
    b.add(box((0, h / 2, 1.5), (w, h, d)), STONE)
    door(b, (-2.0, 0, -1.5), '-z', 1.2, 2.2)
    # auvent sur poteaux devant (atelier ouvert)
    for x in (-3.2, 3.2): b.add(box((x, 1.6, -3.8), (0.25, 3.2, 0.25)), TIMBER)
    b.add(box((0, 3.25, -3.8), (7.0, 0.25, 0.25)), TIMBER)
    b.add(obox((0, 3.65, -2.7), (7.6, 0.18, 3.0), Rx(-0.32)), TILE)
    b.add(obox((0, 3.55, -2.7), (7.4, 0.1, 2.9), Rx(-0.32)), TIMBER)
    # toit du bâtiment
    for sz in (-1, 1):
        b.add(obox((0, h + 1.1, 1.5 + sz * 1.7), (w + 0.8, 0.22, 4.1), Rx(sz * 0.6)), TILE)
    b.add(prism([(-d / 2, 0), (d / 2, 0), (0, 2.3)], 0.2, (-(w / 2 - 0.1), h, 1.5), 'x'), STONE)
    b.add(prism([(-d / 2, 0), (d / 2, 0), (0, 2.3)], 0.2, ((w / 2 - 0.1), h, 1.5), 'x'), STONE)
    # foyer et grande cheminée
    b.add(box((2.0, 0.55, -1.0), (2.0, 1.1, 1.4)), STONE)
    b.add(box((2.0, 1.12, -1.0), (1.4, 0.06, 0.9)), EMBER)
    b.add(box((2.0, 2.4, -0.4), (1.8, 2.6, 0.6)), STONE)
    b.add(box((2.0, 5.6, 1.2), (1.4, 4.6, 1.4)), STONE); b.add(box((2.0, 8.0, 1.2), (1.7, 0.25, 1.7)), STONE)
    # enclume, billot, seau, râtelier d'outils
    b.add(box((-0.4, 0.35, -2.4), (0.6, 0.7, 0.6)), WOOD)
    b.add(box((-0.4, 0.8, -2.4), (0.9, 0.22, 0.35)), IRON); b.add(cone((-0.88, 0.8, -2.4), 0.12, 0.3, segs=6, flare=0), IRON)
    barrel(b, (0.9, 0, -2.9), 0.8, 0.35); b.add(cylinder((0.9, 0.78, -2.9), 0.3, 0.02, segs=12), WATER)
    b.add(box((-3.0, 1.6, -0.8), (0.1, 1.8, 1.6)), TIMBER)
    for k in range(4): b.add(box((-2.92, 1.4 + 0.1 * (k % 2), -1.4 + k * 0.4), (0.06, 1.0, 0.08)), IRON)
    for k in range(3): crate(b, (-2.5 + k * 0.2, 0, -4.6 - k * 0.15), 0.7, k * 0.3)
    a = b.done(); a.meta = dict(ember=(2.0, 1.3, -1.0), chimney=(2.0, 8.3, 1.2)); return a


# ------------------------------------------------------------------------------------------- marché et mobilier
def stall(seed=0):
    r = np.random.default_rng(seed); b = B(f'M_Etal{seed}')
    w, d = 3.2, 1.8
    c1, c2 = ['T_ClothRed', 'T_ClothBlue', 'T_ClothGreen', 'T_ClothRed'][seed % 4], 'T_ClothCream'
    b.add(box((0, 0.9, 0), (w, 0.1, d)), WOOD)
    b.add(box((0, 0.45, -d / 2 + 0.05), (w, 0.85, 0.08)), WOOD)
    for sx in (-1, 1):
        for sz, hh in ((-1, 2.4), (1, 2.9)): b.add(box((sx * (w / 2 - 0.06), hh / 2, sz * (d / 2 - 0.06)), (0.12, hh, 0.12)), TIMBER)
    # bâche rayée inclinée
    n = 7; ang = np.arctan2(0.5, d + 0.6); L = np.hypot(d + 0.6, 0.5)
    for k in range(n):
        x = -w / 2 - 0.15 + (k + 0.5) * (w + 0.3) / n
        b.add(obox((x, 2.68, 0), ((w + 0.3) / n, 0.04, L), Rx(-ang)), c1 if k % 2 == 0 else c2)
    for k in range(n):                                                 # lambrequin festonné
        x = -w / 2 - 0.15 + (k + 0.5) * (w + 0.3) / n
        b.add(box((x, 2.32, -d / 2 - 0.29), ((w + 0.3) / n - 0.02, 0.3 - 0.08 * (k % 2), 0.02)), c1 if k % 2 == 0 else c2)
    # marchandises
    kind = seed % 4
    for k in range(5):
        x = -w / 2 + 0.4 + k * (w - 0.8) / 4
        if kind == 0:                                                    # fruits
            b.add(box((x, 1.02, 0.1), (0.5, 0.14, 0.4)), WOOD)
            for q in range(4): b.add(box((x + r.uniform(-0.15, 0.15), 1.15, 0.1 + r.uniform(-0.12, 0.12)), (0.12, 0.12, 0.12), rot=r.uniform(0, 1)), CLOTH[int(r.integers(0, 4))] if q % 2 else PLANT)
        elif kind == 1:                                                  # tissus en rouleaux
            b.add(obox((x, 1.06, 0.1), (0.5, 0.18, 0.18), Rz(0)), CLOTH[k % 4])
        elif kind == 2:                                                  # poteries
            b.add(cylinder((x, 0.95, 0.1), 0.16, 0.32, segs=10, r_top=0.1), STONE)
        else:                                                            # armes et outils
            b.add(obox((x, 1.0, 0.15), (0.08, 0.04, 0.9), Ry(0.2)), IRON)
    crate(b, (w / 2 + 0.6, 0, 0.3), 0.7, 0.4); crate(b, (w / 2 + 0.6, 0.7, 0.3), 0.55, 0.9)
    if r.uniform() < 0.6: barrel(b, (-w / 2 - 0.6, 0, 0.4), 0.9, 0.36)
    return b.done()


def fountain():
    b = B('M_Fontaine'); R0 = 3.0; n = 8
    for k in range(n):
        a0 = 2 * np.pi * k / n; a1 = 2 * np.pi * (k + 1) / n; am = (a0 + a1) / 2
        L = 2 * R0 * np.sin(np.pi / n)
        b.add(box((R0 * np.cos(am), 0.4, R0 * np.sin(am)), (0.45, 0.8, L + 0.3), rot=-am), STONE)
        b.add(box((R0 * np.cos(am), 0.85, R0 * np.sin(am)), (0.65, 0.12, L + 0.4), rot=-am), STONE)
    b.add(cylinder((0, 0, 0), R0, 0.15, segs=n), STONE)
    b.add(cylinder((0, 0.55, 0), R0 - 0.2, 0.02, segs=24), WATER)
    b.add(cylinder((0, 0, 0), 0.5, 2.2, segs=8), STONE)
    b.add(cylinder((0, 2.2, 0), 1.2, 0.3, segs=12, r_top=0.6), STONE)
    b.add(cylinder((0, 2.43, 0), 1.0, 0.02, segs=16), WATER)
    b.add(cylinder((0, 2.5, 0), 0.25, 1.0, segs=8), STONE)
    b.add(cone((0, 3.5, 0), 0.4, 0.8, segs=8, flare=0.2), STONE)
    a = b.done(); a.meta = dict(spouts=[(0, 3.3, 0)]); return a


def well():
    b = B('M_Puits')
    for k in range(10):
        am = 2 * np.pi * (k + 0.5) / 10
        b.add(box((1.0 * np.cos(am), 0.45, 1.0 * np.sin(am)), (0.4, 0.9, 0.7), rot=-am), STONE)
    b.add(cylinder((0, 0.2, 0), 0.82, 0.02, segs=16), DARK)
    for sx in (-1, 1): b.add(box((sx * 1.15, 1.4, 0), (0.18, 2.8, 0.18)), TIMBER)
    b.add(obox((0, 2.0, 0), (2.4, 0.12, 0.12), Rz(np.pi / 2) @ Rz(np.pi / 2)), TIMBER)
    b.add(obox((0, 2.0, 0), (0.12, 0.12, 2.4), Ry(np.pi / 2)), TIMBER)
    for sz in (-1, 1): b.add(obox((0, 3.0, sz * 0.55), (2.8, 0.1, 1.35), Rx(sz * 0.7)), TILE)
    b.add(box((0, 1.45, 0), (0.06, 0.6, 0.06)), IRON); b.add(cylinder((0, 0.95, 0), 0.18, 0.3, segs=10), WOOD)
    return b.done()


def lamppost():
    b = B('M_Lanterne')
    b.add(cylinder((0, 0, 0), 0.22, 0.4, segs=8), STONE)
    b.add(cylinder((0, 0.4, 0), 0.07, 3.0, segs=8), IRON)
    b.add(box((0.35, 3.3, 0), (0.75, 0.06, 0.06)), IRON)
    b.add(obox((0.18, 3.1, 0), (0.05, 0.5, 0.05), Rz(0.8)), IRON)
    lantern(b, (0.65, 2.98, 0))
    a = b.done(); a.meta = dict(light=(0.65, 2.98, 0)); return a


def cart():
    b = B('M_Charrette')
    b.add(box((0, 0.95, 0), (1.5, 0.1, 2.6)), WOOD)
    for sx in (-1, 1): b.add(box((sx * 0.72, 1.2, 0), (0.06, 0.45, 2.6)), WOOD)
    b.add(box((0, 1.2, 1.28), (1.5, 0.45, 0.06)), WOOD)
    for sx in (-1, 1):
        w = cylinder((0, -0.06, 0), 0.62, 0.12, segs=16)
        w.V = w.V @ Rz(np.pi / 2).T + np.array([sx * 0.9, 0.62, 0.3]); b.add(w, WOOD)
        hub = cylinder((0, -0.1, 0), 0.12, 0.2, segs=8); hub.V = hub.V @ Rz(np.pi / 2).T + np.array([sx * 0.92, 0.62, 0.3]); b.add(hub, IRON)
        for k in range(6):
            a0 = np.pi * k / 6; b.add(obox((sx * 0.9, 0.62, 0.3), (0.05, 1.15, 0.06), Rx(a0)), WOOD)
    for sx in (-1, 1): b.add(obox((sx * 0.35, 0.55, -2.1), (0.08, 0.08, 2.0), Rx(-0.25)), WOOD)
    for k in range(3): b.add(box((-0.3 + k * 0.3, 1.25, -0.2 + 0.4 * (k % 2)), (0.5, 0.45, 0.8), rot=0.2 * k), HAY)
    return b.done()


def props_small():
    """Petits accessoires regroupés en une pièce chacun."""
    out = []
    b = B('M_Tonneaux'); barrel(b, (0, 0, 0)); barrel(b, (0.9, 0, 0.2)); barrel(b, (0.45, 0, 0.9)); out.append(b.done())
    b = B('M_Caisses'); crate(b, (0, 0, 0), 0.9, 0.1); crate(b, (1.0, 0, 0.1), 0.8, -0.2); crate(b, (0.4, 0.9, 0.05), 0.7, 0.5); out.append(b.done())
    b = B('M_Foin'); b.add(box((0, 0.45, 0), (1.2, 0.9, 0.8), rot=0.1), HAY); b.add(box((0.3, 1.2, 0), (1.2, 0.6, 0.8), rot=-0.3), HAY)
    for y in (0.25, 0.65): b.add(box((0, y, 0), (1.22, 0.04, 0.82), rot=0.1), TIMBER)
    out.append(b.done())
    b = B('M_Banc'); b.add(box((0, 0.46, 0), (1.8, 0.08, 0.45)), WOOD)
    for x in (-0.7, 0.7): b.add(box((x, 0.22, 0), (0.1, 0.44, 0.4)), WOOD)
    out.append(b.done())
    b = B('M_Banniere')
    b.add(cylinder((0, 0, 0), 0.06, 6.0, segs=8), WOOD); b.add(box((0.55, 5.7, 0), (1.2, 0.05, 0.05)), WOOD)
    b.add(box((0.55, 4.55, 0), (1.1, 2.2, 0.03)), 'T_ClothRed'); b.add(box((0.55, 4.7, -0.02), (0.5, 0.5, 0.02)), 'T_ClothCream')
    out.append(b.done())
    return out


# ------------------------------------------------------------------------------------------- enceinte de la ville
RAMP_H, RAMP_T = 6.5, 2.6


def rampart(length=12.0):
    """Courtine de 12 m : maçonnerie à fruit, chemin de ronde, parapet crénelé côté extérieur (−Z), contreforts."""
    b = B('M_Rempart')
    b.add(box((0, RAMP_H / 2, 0), (length, RAMP_H, RAMP_T)), STONE)
    b.add(box((0, 0.6, -RAMP_T / 2 - 0.25), (length, 1.2, 0.5)), STONE)                          # talus
    b.add(box((0, RAMP_H + 0.6, -RAMP_T / 2 + 0.25), (length, 1.2, 0.5)), STONE)                 # parapet
    b.add(merlons_line((-length / 2, 0, -RAMP_T / 2 + 0.25), (length / 2, 0, -RAMP_T / 2 + 0.25), RAMP_H + 1.2, step=1.7, d=0.5), STONE)
    b.add(box((0, RAMP_H + 0.35, RAMP_T / 2 - 0.15), (length, 0.7, 0.3)), STONE)
    for x in (-length / 2 + 3, length / 2 - 3): b.add(box((x, RAMP_H * 0.4, -RAMP_T / 2 - 0.4), (1.0, RAMP_H * 0.8, 0.9)), STONE)
    for k in range(6): b.add(box((-length / 2 + 1 + k * 2, RAMP_H * 0.55, -RAMP_T / 2 - 0.02), (0.18, 0.6, 0.06)), DARK)      # archères
    return b.done()


def town_tower():
    b = B('M_TourVille'); r, h = 3.4, 10.0
    b.add(cylinder((0, 0, 0), r * 1.08, 1.4, segs=20, cap_top=False), STONE)
    b.add(cylinder((0, 0, 0), r, h, segs=20, cap_top=True), STONE)
    b.add(cylinder((0, h - 0.6, 0), r * 1.12, 0.7, segs=20), STONE)
    for k in range(10):                                                  # mâchicoulis : consoles
        am = 2 * np.pi * k / 10
        b.add(box(((r + 0.12) * np.cos(am), h - 0.9, (r + 0.12) * np.sin(am)), (0.3, 0.5, 0.3), rot=-am), STONE)
    b.add(cone((0, h + 0.1, 0), r * 1.25, 7.5, segs=20), ROOF)
    b.add(cylinder((0, h + 7.4, 0), 0.07, 1.4, segs=6), IRON)
    for k in range(3):
        am = 2 * np.pi * k / 3 + 0.4
        for y in (3.5, 7.0): b.add(box(((r + 0.02) * np.cos(am), y, (r + 0.02) * np.sin(am)), (0.08, 1.1, 0.3), rot=-am), DARK)
    return b.done()


def town_gate():
    """Porte de ville praticable : passage voûté (4,6 m × 5,4 m) entre deux tours carrées, herse relevée, bretèche."""
    b = B('M_PorteVille'); W, Dp, H = 4.6, RAMP_T + 2.4, 5.4
    for sx in (-1, 1):
        b.add(box((sx * (W / 2 + 2.0), 5.5, 0), (4.0, 11.0, Dp + 1.0)), STONE)
        b.add(merlons_line((sx * (W / 2 + 0.2), 0, -(Dp + 1.0) / 2 + 0.2), (sx * (W / 2 + 3.8), 0, -(Dp + 1.0) / 2 + 0.2), 11.0, step=1.6, d=0.5), STONE)
        b.add(cone((sx * (W / 2 + 2.0), 11.0, 0), 3.2, 6.0, segs=4, flare=0.05), ROOF)
        for y in (3.0, 7.5): b.add(box((sx * (W / 2 + 2.0), y, -(Dp + 1.0) / 2 - 0.02), (0.18, 1.2, 0.06)), DARK)
    b.add(box((0, H + 2.4, 0), (W + 0.2, 4.8 - 0.0, Dp)), STONE)                                   # au-dessus du passage
    for k in range(13):                                                                              # voûte en berceau
        a0 = np.pi * k / 12
        b.add(obox((np.cos(a0) * (W / 2 - 0.15), H - 1.0 + np.sin(a0) * 1.2, 0), (0.35, 0.35, Dp), Rz(a0)), STONE)
    for sz in (-1, 1):                                                                               # archivoltes
        for k in range(11):
            a0 = np.pi * (k + 0.5) / 11
            b.add(obox((np.cos(a0) * (W / 2 + 0.05), H - 1.0 + np.sin(a0) * 1.4, sz * Dp / 2), (0.5, 0.36, 0.3), Rz(a0 - np.pi / 2)), STONE)
    for k in range(8): b.add(box((-W / 2 + 0.3 + k * (W - 0.6) / 7, H - 0.2, -Dp / 2 + 0.6), (0.1, 1.6, 0.1)), IRON)     # herse relevée
    b.add(box((0, H - 1.0, -Dp / 2 + 0.6), (W, 0.1, 0.1)), IRON)
    b.add(box((0, H + 4.0, -Dp / 2 - 0.6), (3.0, 2.0, 1.2)), STONE)                                 # bretèche
    for x in (-1, 0, 1): b.add(box((x * 1.0, H + 2.9, -Dp / 2 - 0.6), (0.3, 0.5, 1.0)), STONE)
    b.add(merlons_line((-W / 2 - 0.1, 0, -Dp / 2 + 0.2), (W / 2 + 0.1, 0, -Dp / 2 + 0.2), H + 4.8, step=1.5, d=0.45), STONE)
    for sx in (-1, 1):                                                                              # bannières
        b.add(box((sx * 1.5, H + 3.2, -Dp / 2 - 0.05), (0.9, 2.6, 0.03)), 'T_ClothRed')
        b.add(box((sx * 1.5, H + 3.5, -Dp / 2 - 0.08), (0.4, 0.4, 0.02)), 'T_ClothCream')
    return b.done()


# ------------------------------------------------------------------------------------------- matériaux
def _t(base, hgt, smooth, strength=0.8, emit=None):
    return WM._tex(base, hgt, smooth, strength, 4, 0.12, emit=emit)


def plaster(S=1024, seed=3001):
    """Enduit à la chaux ocre clair : taloché, salissures en coulures, éclats laissant voir le torchis."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 8, octaves=5)); fine = n01(fbm(S, S, r, base=S / 90, octaves=3))
    streak = np.clip(n01(fbm(S, S, r, base=S / 5, octaves=4, aniso=(1.0, 0.07))) - 0.55, 0, 1) * 1.6
    chip = np.clip((n01(fbm(S, S, r, base=S / 25, octaves=3)) - 0.8) * 5, 0, 1)
    col = np.array([0.80, 0.74, 0.60])[None, None] * (0.85 + 0.2 * g[..., None]) * (0.95 + 0.08 * fine[..., None])
    col = col * (1 - 0.3 * streak[..., None])
    col = col * (1 - chip[..., None]) + np.array([0.45, 0.36, 0.25]) * chip[..., None]
    return _t(col, 0.6 * g + 0.3 * fine - 1.0 * chip, 0.12, 0.6)


def timber(S=512, seed=3002):
    """Poutres de chêne sombre : fil du bois, fentes de séchage."""
    r = np.random.default_rng(seed)
    f = fbm(S, S, r, base=S / 10, octaves=5, aniso=(1.0, 0.05)); cr = np.clip(1 - np.abs(f) * 9, 0, 1) ** 3
    g = n01(fbm(S, S, r, base=S / 6, octaves=4))
    col = np.array([0.22, 0.14, 0.08])[None, None] * (0.7 + 0.45 * n01(f)[..., None]) * (0.9 + 0.2 * g[..., None]) * (1 - 0.5 * cr[..., None])
    return _t(col, n01(f) - 1.5 * cr, 0.2, 0.9)


def roof_tiles(S=1024, seed=3003):
    """Tuiles plates de terre cuite (rangs décalés), mousses et lichens, teintes brun-rouge variées."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    rows = 12; row = np.floor(yy * rows); fy = yy * rows - row
    u = xx * 9 + (row % 2) * 0.5; fx = u - np.floor(u)
    tile = np.clip(1 - np.abs(fx - 0.5) * 2.2, 0, 1) ** 0.3 * np.clip(fy * 3, 0, 1)
    bid = (np.floor(u) * 5.31 + row * 2.7) % 1.0
    g = n01(fbm(S, S, r, base=S / 20, octaves=4))
    base = np.array([0.50, 0.24, 0.14])[None, None] * (0.75 + 0.35 * np.sin(bid * 25)[..., None] * 0.5 + 0.2) * (0.85 + 0.25 * g[..., None])
    base = base * (0.55 + 0.45 * tile[..., None])
    moss = np.clip((n01(fbm(S, S, r, base=S / 8, octaves=4)) - 0.7) * 3.5, 0, 1)
    base = base * (1 - 0.6 * moss[..., None]) + np.array([0.28, 0.32, 0.14]) * 0.6 * moss[..., None]
    return _t(base, 2.0 * tile + 0.3 * g, 0.25 + 0.15 * tile, 1.0)


def cobbles(S=1024, seed=3004):
    """Pavés de rue arrondis (cellules de Voronoï), joints de terre, usure polie au centre."""
    from forge.texgen import voronoi_edges
    r = np.random.default_rng(seed)
    e = voronoi_edges(S, S, r, 260); e = n01(e)
    stone = np.clip(e * 3.0, 0, 1) ** 0.6
    g = n01(fbm(S, S, r, base=S / 14, octaves=4))
    tint = n01(fbm(S, S, r, base=S / 30, octaves=2))
    col = np.array([0.42, 0.40, 0.37])[None, None] * (0.75 + 0.35 * tint[..., None]) * (0.85 + 0.2 * g[..., None])
    col = col * stone[..., None] + np.array([0.20, 0.16, 0.11]) * (1 - stone[..., None])
    return _t(col, 2.2 * stone + 0.3 * g, 0.18 + 0.25 * stone, 1.0)


def flat(color, smooth=0.3, emit=None, S=64, noise_amt=0.08, seed=0):
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 4, octaves=3))
    col = np.array(color)[None, None] * (1 - noise_amt + 2 * noise_amt * g[..., None])
    em = None if emit is None else np.broadcast_to(np.array(emit)[None, None] * (0.8 + 0.4 * g[..., None]), (S, S, 3)).copy()
    return _t(col, 0.2 * g, smooth, 0.3, em)


def cloth(color, S=256, seed=3010):
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    weave = 0.5 + 0.25 * np.sin(xx * 1.6) + 0.25 * np.sin(yy * 1.6)
    g = n01(fbm(S, S, r, base=S / 6, octaves=4))
    col = np.array(color)[None, None] * (0.8 + 0.15 * weave[..., None]) * (0.8 + 0.3 * g[..., None])
    return _t(col, 0.4 * weave + 0.3 * g, 0.1, 0.4)


def hay(S=256, seed=3011):
    r = np.random.default_rng(seed)
    f = n01(fbm(S, S, r, base=S / 40, octaves=3, aniso=(1.0, 0.06)))
    col = np.array([0.72, 0.60, 0.30])[None, None] * (0.6 + 0.6 * f[..., None])
    return _t(col, f, 0.15, 0.8)


_STEEL = lambda S: __import__('knight.materials', fromlist=['x']).steel(S, tone=(0.20, 0.20, 0.21), polish=0.3)
RECIPES = {
    PLASTER: (lambda S: plaster(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    TIMBER:  (lambda S: timber(S), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    TILE:    (lambda S: roof_tiles(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    COBBLE:  (lambda S: cobbles(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    GLASS:   (lambda S: flat((0.32, 0.25, 0.16), 0.9, emit=(0.75, 0.45, 0.16)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(1.0, 1.0, 1.0), emission_preview=(0.6, 0.38, 0.14))),
    EMBER:   (lambda S: flat((1.0, 0.35, 0.05), 0.2, emit=(1.0, 0.32, 0.04), noise_amt=0.3), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(2.5, 2.5, 2.5), emission_preview=(1.0, 0.4, 0.1))),
    IRON:    (lambda S: _STEEL(S), 512, 256, dict(metallic=1.0, smoothness=1.0)),
    PLANT:   (lambda S: flat((0.18, 0.34, 0.10), 0.2, noise_amt=0.25), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    HAY:     (lambda S: hay(S), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    WATER:   (lambda S: WM.water(S), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'T_ClothRed':   (lambda S: cloth((0.55, 0.08, 0.07), seed=1), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'T_ClothCream': (lambda S: cloth((0.82, 0.76, 0.60), seed=2), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'T_ClothBlue':  (lambda S: cloth((0.10, 0.20, 0.45), seed=3), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'T_ClothGreen': (lambda S: cloth((0.14, 0.32, 0.14), seed=4), 256, 128, dict(metallic=0.0, smoothness=1.0)),
}


def pieces():
    out = [house(k) for k in range(6)]
    out += [house(6, 6.0, 6.0, 1), house(7, 8.0, 7.0, 2, TILE)]
    out += [tavern(), forge(), fountain(), well(), lamppost(), cart(), rampart(), town_tower(), town_gate()]
    out += [stall(k) for k in range(4)]
    out += props_small()
    return out
