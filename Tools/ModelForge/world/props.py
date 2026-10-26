"""Décor de la vallée de Korvalune : chêne, pin, rochers, et le Château de Korvalune (« Cœur Céleste ») tel que sur la
carte — enceinte crénelée à tours rondes coiffées de flèches d'ardoise, châtelet d'entrée, grand donjon hérissé de
flèches et de clochetons. Toutes les dimensions en mètres ; UV en mètres / 2 (textures « 1 répétition = 2 m »)."""
import numpy as np
from forge.rig import Asset
from forge.geo import Mesh, Loft, spline, curve, concat, unit, vertex_normals, grid_faces
from forge import sculpt as SC

STONE, ROOF, WOOD, DARK, GOLD, BARK, LEAF, NEEDLE, ROCK = 'W_Stone', 'W_Roof', 'W_Wood', 'W_Dark', 'W_Gold', 'W_Bark', 'W_Leaves', 'W_Needles', 'W_Rock'


# ------------------------------------------------------------------------------------------- géométrie de base
def box(c, size, rot=0.0, uv=0.5):
    """Pavé centré en c (x, y, z), size (sx, sy, sz), tourné de rot (rad) autour de Y ; UV en mètres × uv."""
    c = np.asarray(c, float); hx, hy, hz = np.asarray(size, float) / 2
    ca, sa = np.cos(rot), np.sin(rot)
    R = np.array([[ca, 0, sa], [0, 1, 0], [-sa, 0, ca]])
    faces = []
    for ax, sgn in ((0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1)):
        n = np.zeros(3); n[ax] = sgn
        u_ax, v_ax = [(2, 1), (2, 1), (0, 2), (0, 2), (0, 1), (0, 1)][ax * 2 + (0 if sgn > 0 else 1)]
        h = np.array([hx, hy, hz])
        P = []; UV = []
        for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            p = n * h
            p[u_ax] = i * h[u_ax]; p[v_ax] = j * h[v_ax]
            P.append(p); UV.append((p[u_ax] if ax != 2 else p[0], p[v_ax]))
        P = np.array(P) @ R.T + c
        UV = np.array(UV) * uv + np.array([c[0] + c[2], c[1]]) * uv * 0
        F = np.array([[0, 1, 2], [0, 2, 3]])
        m = Mesh(P, F, UV)
        fn = np.cross(P[1] - P[0], P[2] - P[0])
        if fn @ (R @ n) < 0: m.F = m.F[:, ::-1].copy()
        faces.append(m)
    return concat(faces)


def cylinder(c, r, h, segs=24, uv=0.5, cap_top=True, r_top=None):
    c = np.asarray(c, float); r_top = r if r_top is None else r_top
    a = np.linspace(0, 2 * np.pi, segs + 1)
    rows = []
    for y, rr in ((0.0, r), (h, r_top)):
        rows.append(np.stack([c[0] + rr * np.sin(a), np.full_like(a, c[1] + y), c[2] + rr * np.cos(a)], 1))
    G = np.array(rows)
    UV = np.stack(np.meshgrid(a * r, [0, h]), -1).reshape(-1, 2) * uv
    m = Mesh(G.reshape(-1, 3), grid_faces(2, segs + 1), UV)
    if (vertex_normals(m) * (m.V - np.r_[c[0], c[1] + h / 2, c[2]])).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    parts = [m]
    if cap_top:
        ctr = np.r_[c[0], c[1] + h, c[2]]
        P = np.vstack([ctr, G[1, :-1]]); F = np.array([[0, i + 1, (i + 1) % segs + 1] for i in range(segs)])
        cm = Mesh(P, F, (P[:, [0, 2]] - ctr[[0, 2]]) * uv)
        if vertex_normals(cm)[:, 1].mean() < 0: cm.F = cm.F[:, ::-1].copy()
        parts.append(cm)
    return concat(parts)


def cone(c, r, h, segs=24, uv=0.5, flare=0.12):
    """Flèche d'ardoise : cône légèrement évasé à la base (coyau)."""
    c = np.asarray(c, float)
    t = np.linspace(0, 1, 8); a = np.linspace(0, 2 * np.pi, segs + 1)
    rr = r * (1 - t) ** 1.08 * (1 + flare * np.exp(-t / 0.08))
    G = np.stack([c[0] + rr[:, None] * np.sin(a)[None], c[1] + (t * h)[:, None] * np.ones_like(a)[None], c[2] + rr[:, None] * np.cos(a)[None]], -1)
    UV = np.stack(np.meshgrid(a * r, t * h * 1.05), -1).reshape(-1, 2) * uv
    m = Mesh(G.reshape(-1, 3), grid_faces(len(t), segs + 1), UV)
    if (vertex_normals(m) * (m.V - np.r_[c[0], c[1] + h * 0.3, c[2]])).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def merlons_ring(c, r, y, n, w=0.9, h=1.1, d=0.7, uv=0.5):
    out = []
    for k in range(n):
        a = 2 * np.pi * k / n
        p = np.array([c[0] + r * np.sin(a), y + h / 2, c[2] + r * np.cos(a)])
        out.append(box(p, (w, h, d), rot=a, uv=uv))
    return concat(out)


def merlons_line(p0, p1, y, step=1.8, w=0.9, h=1.1, d=0.6, off=0.0, uv=0.5):
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d_ = p1 - p0; L = np.linalg.norm(d_[[0, 2]]); rot = np.arctan2(d_[0], d_[2])
    nrm = np.array([np.cos(rot), 0, -np.sin(rot)])
    out = []
    for k in range(int(L / step)):
        t = (k + 0.5) / int(L / step)
        p = p0 + d_ * t; p[1] = y + h / 2
        out.append(box(p + nrm * off, (d, h, w), rot=rot, uv=uv))
    return concat(out)


def windows(c, r, y, n, w=0.35, h=1.1, start=0.0):
    """Meurtrières : petites plaques sombres posées sur la paroi d'une tour."""
    out = []
    for k in range(n):
        a = start + 2 * np.pi * k / n
        p = np.array([c[0] + (r + 0.03) * np.sin(a), y, c[2] + (r + 0.03) * np.cos(a)])
        out.append(box(p, (w, h, 0.06), rot=a))
    return concat(out)


# ------------------------------------------------------------------------------------------- château
def tower(a, c, r, h, roof=True, roof_h=None, wins=3):
    c = np.asarray(c, float)
    a.add('tour', cylinder(c, r * 1.06, 1.2, cap_top=False), STONE)              # plinthe
    a.add('tour', cylinder(c, r, h, cap_top=not roof), STONE)
    a.add('tour', cylinder(c + (0, h - 0.8, 0), r * 1.12, 0.8, cap_top=True), STONE)     # mâchicoulis / corniche
    for k in range(wins):
        a.add('fenetre', windows(c, r, h * (0.35 + 0.22 * k), 4, start=0.4 * k), DARK)
    if roof:
        rh = roof_h or r * 3.0
        a.add('toit', cone(c + (0, h, 0), r * 1.22, rh), ROOF)
        a.add('epi', cylinder(c + (0, h + rh - 0.1, 0), 0.08, 1.4, segs=8), GOLD)
    else:
        a.add('creneau', merlons_ring(c, r * 1.05, h, max(8, int(2 * np.pi * r / 1.8))), STONE)


def wall(a, p0, p1, h=11.0, t=3.0):
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0; L = np.linalg.norm(d); rot = np.arctan2(d[0], d[2])
    mid = (p0 + p1) / 2; mid[1] = h / 2
    a.add('muraille', box(mid, (t, h, L), rot=rot), STONE)
    nrm = np.array([np.cos(rot), 0, -np.sin(rot)])
    for s in (1, -1):
        a.add('creneau', merlons_line(p0 + nrm * s * (t / 2 - 0.3), p1 + nrm * s * (t / 2 - 0.3), h, d=0.6), STONE)
    # contreforts côté extérieur
    for k in range(1, int(L / 9)):
        q = p0 + d * k / int(L / 9); q[1] = h * 0.35
        a.add('contrefort', box(q - nrm * (t / 2 + 0.5) * np.sign(np.dot(nrm, q)) * -1, (1.2, h * 0.7, 1.4), rot=rot), STONE)


def castle():
    """Château de Korvalune, porte au sud (−Z) : enceinte octogonale (rayon 34 m), 8 tours à flèches, châtelet,
    donjon central à flèches multiples. Origine au centre, sol à y = 0."""
    a = Asset('ChateauKorvalune')
    R = 34.0; n = 8
    corners = [np.array([R * np.sin(2 * np.pi * (k + 0.5) / n), 0, R * np.cos(2 * np.pi * (k + 0.5) / n)]) for k in range(n)]
    gate_dir = np.array([0, 0, -1.0])
    for k in range(n):
        p0, p1 = corners[k], corners[(k + 1) % n]
        mid = (p0 + p1) / 2
        if np.dot(unit(mid), gate_dir) > 0.95:                 # côté sud : châtelet entre deux tronçons
            g = unit(p1 - p0)
            wall(a, p0, mid - g * 7.5); wall(a, mid + g * 7.5, p1)
            for s in (-1, 1): tower(a, mid + g * s * 6.0, 4.2, 17.0, roof=True, roof_h=11.0)
            a.add('chatelet', box(mid + (0, 12.0, 0), (9.0, 6.0, 4.0), rot=np.arctan2(g[0], g[2]) + np.pi / 2), STONE)
            a.add('creneau', merlons_line(mid - g * 4.5 + (0, 0, -1.6), mid + g * 4.5 + (0, 0, -1.6), 15.0, d=0.6), STONE)
            # arche de la porte (ouverture sombre) et herse de bois
            a.add('porte', box(mid + (0, 3.6, -2.05), (5.0, 7.2, 0.2)), DARK)
            a.add('herse', box(mid + (0, 3.6, -2.15), (4.6, 6.8, 0.12)), WOOD)
        else:
            wall(a, p0, p1)
    for k, p in enumerate(corners):
        tower(a, p, 5.0, 16.0 + 2.0 * (k % 2), roof=True, roof_h=13.0 + 2 * (k % 2))
    # ---- donjon : corps principal, aile, tours d'angle, grande flèche centrale, clochetons
    a.add('donjon', box((0, 11.0, 4.0), (22.0, 22.0, 16.0)), STONE)
    a.add('donjon', box((0, 24.0, 4.0), (14.0, 6.0, 10.0)), STONE)
    a.add('creneau', merlons_line((-11, 0, -4.2), (11, 0, -4.2), 22.0, d=0.6), STONE)
    a.add('toit', box((0, 30.0, 4.0), (12.0, 6.0, 8.0)), ROOF)
    tower(a, (0, 0, 4.0), 4.0, 34.0, roof=True, roof_h=18.0, wins=5)
    for sx in (-1, 1):
        for sz, hh in ((-1, 27.0), (1, 25.0)):
            tower(a, (sx * 11.0, 0, 4.0 + sz * 8.0), 3.0, hh, roof=True, roof_h=10.0, wins=4)
        tower(a, (sx * 6.0, 0, -6.5), 2.2, 24.0, roof=True, roof_h=8.0, wins=3)
    # grande baie sombre et vitrail du donjon
    a.add('fenetre', box((0, 14.0, -4.05), (3.0, 7.0, 0.15)), DARK)
    for sx in (-1, 1):
        for y in (8.0, 16.0):
            a.add('fenetre', box((sx * 6.5, y, -4.05), (1.2, 3.0, 0.15)), DARK)
    # cour : bâtiments bas (écuries, chapelle)
    a.add('batiment', box((-20.0, 3.5, 6.0), (8.0, 7.0, 18.0), rot=0.2), STONE)
    a.add('toit', box((-20.0, 8.0, 6.0), (9.0, 2.0, 19.0), rot=0.2), ROOF)
    a.add('batiment', box((19.0, 4.0, 8.0), (9.0, 8.0, 14.0), rot=-0.25), STONE)
    a.add('toit', box((19.0, 9.0, 8.0), (10.0, 2.0, 15.0), rot=-0.25), ROOF)
    # sol de la cour (pavés)
    a.add('cour', cylinder((0, -0.05, 0), R - 1.0, 0.1, segs=n, cap_top=True), STONE)
    # fusionne par matériau
    merged = {}
    for nm, m, mat in a.parts: merged.setdefault(mat, []).append(m)
    a.parts = [(mat.replace('W_', '').lower(), concat(ms), mat) for mat, ms in merged.items()]
    return a


# ------------------------------------------------------------------------------------------- arbres
def _cards(centers, size, rng, up_bias=0.3):
    """Cartes de feuillage croisées (deux quads perpendiculaires) orientées au hasard autour de chaque centre."""
    ms = []
    for c in centers:
        n = unit(rng.normal(0, 1, 3) + np.array([0, up_bias, 0]))
        t = unit(np.cross(n, (0, 1, 0)) + 1e-6); b = np.cross(n, t)
        s = size * rng.uniform(0.75, 1.25)
        col = rng.integers(0, 2)
        for ax1, ax2 in ((t, b), (unit(t + n), unit(b - n * 0.3))):
            P = np.array([c - ax1 * s / 2 - ax2 * s / 2, c + ax1 * s / 2 - ax2 * s / 2, c + ax1 * s / 2 + ax2 * s / 2, c - ax1 * s / 2 + ax2 * s / 2])
            UV = np.array([[col * 0.5, 0], [col * 0.5 + 0.5, 0], [col * 0.5 + 0.5, 1], [col * 0.5, 1]])
            ms.append(Mesh(P, np.array([[0, 1, 2], [0, 2, 3]]), UV))
    return concat(ms)


def oak(seed=0, H=9.0, lod=0):
    """lod=1 : version allégée (moitié moins de cartes, plus grandes ; tronc à 6 côtés)."""
    rng = np.random.default_rng(seed)
    a = Asset(f'Chene{seed}' + ('_LOD1' if lod else ''))
    lean = rng.normal(0, 0.2, 2)
    trunk = [(0, 0, 0), (lean[0] * 0.3, H * 0.25, lean[1] * 0.3), (lean[0] * 0.6, H * 0.45, lean[1] * 0.6)]
    a.add('tronc', Loft(spline(trunk, 16 if not lod else 6), curve([(0, .55), (.1, .38), (1, .26)], 16 if not lod else 6), curve([(0, .55), (.1, .38), (1, .26)], 16 if not lod else 6), p=2.2, segs=12 if not lod else 6).mesh(0.6), BARK)
    top = np.array(trunk[-1], float)
    centers = []
    for k in range(6):
        ang = k * 2 * np.pi / 6 + rng.uniform(-0.3, 0.3)
        d = np.array([np.cos(ang), 0.9 + rng.uniform(-0.2, 0.3), np.sin(ang)])
        L = H * rng.uniform(0.28, 0.38)
        end = top + unit(d) * L
        mid = top + unit(d) * L * 0.5 + np.array([0, 0.4, 0])
        a.add('branche', Loft(spline([top, mid, end], 10), curve([(0, .20), (1, .05)], 10), curve([(0, .20), (1, .05)], 10), p=2.2, segs=8, cap1=1).mesh(0.6), BARK)
        for j in range(16 if not lod else 7):
            centers.append(end + rng.normal(0, 1.0, 3) * np.array([1.0, 0.7, 1.0]))
    crown = top + np.array([0, H * 0.25, 0])
    for j in range(40 if not lod else 16):
        centers.append(crown + rng.normal(0, 1, 3) * np.array([2.3, 1.4, 2.3]))
    a.add('feuillage', _cards(centers, 2.0 if not lod else 2.9, rng), LEAF)
    return a


def pine(seed=0, H=13.0, lod=0):
    rng = np.random.default_rng(seed)
    a = Asset(f'Pin{seed}' + ('_LOD1' if lod else ''))
    a.add('tronc', Loft(spline([(0, 0, 0), (0, H * 0.5, 0), (0, H, 0)], 16), curve([(0, .40), (.08, .28), (1, .04)], 16), curve([(0, .40), (.08, .28), (1, .04)], 16), p=2.2, segs=10, cap1=1).mesh(0.6), BARK)
    ms = []
    for k, y in enumerate(np.linspace(H * 0.22, H * 0.97, 15 if not lod else 7)):
        f = 1 - (y - H * 0.22) / (H * 0.80)
        L = 0.4 + 3.0 * f
        nb = 6 if not lod else 4
        for j in range(nb):
            ang = j * 2 * np.pi / nb + k * 0.5 + rng.uniform(-0.2, 0.2)
            d = np.array([np.cos(ang), -0.25 - 0.1 * f, np.sin(ang)])
            p0 = np.array([0, y, 0]); p1 = p0 + unit(d) * L
            side = unit(np.cross(d, (0, 1, 0))) * (0.6 + 0.6 * f) * (1.0 if not lod else 1.4)
            up = np.array([0, 0.35 + 0.2 * f, 0])
            P = np.array([p0 - side * 0.3, p1 - side, p1 + side, p0 + side * 0.3])
            col = rng.integers(0, 2)
            for lift in (up, -up * 0.6):
                Q = P + np.array([np.zeros(3), lift, lift, np.zeros(3)])
                ms.append(Mesh(Q, np.array([[0, 1, 2], [0, 2, 3]]), np.array([[col * 0.5, 0], [col * 0.5, 1], [col * 0.5 + 0.5, 1], [col * 0.5 + 0.5, 0]])))
    a.add('aiguilles', concat(ms), NEEDLE)
    return a


def rock(seed=0, size=2.5):
    rng = np.random.default_rng(seed)
    s = SC.Sculpt(k=0.12 * size)
    for k in range(5):
        c = rng.normal(0, 0.35, 3) * size; c[1] = abs(c[1]) * 0.6
        s.ell('root', c, rng.uniform(0.35, 0.7, 3) * size * np.array([1.0, 0.7, 1.0]), fwd=unit(rng.normal(0, 1, 3)))
    det = lambda X: 0.06 * size * SC.noise3(X, 0.35 * size, seed=seed, octaves=3)
    m = s.mesh(res=0.06 * size, detail=det, smooth=2)
    m.V[:, 1] -= 0.15 * size
    SC.box_uv(m, 2.0)
    m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
    a = Asset(f'Rocher{seed}'); a.add('roche', m, ROCK)
    return a
