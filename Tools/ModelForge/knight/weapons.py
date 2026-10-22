"""Épée longue et écu du chevalier (FBX statiques, origine au point de prise).
Épée : lame le long de +Y, tranchants en ±X, garde en X, prise centrée sur l'origine.
Écu : face vers +Z, haut vers +Y, origine au centre des énarmes (dos du bouclier)."""
import numpy as np
from forge.geo import Loft, Mesh, spline, curve, tube, rivet, concat, vertex_normals, unit, grid_faces
from forge.rig import Asset

BLADE, STEEL, BRASS, LEATHER, WOOD = 'WP_Blade', 'KN_Steel', 'KN_Brass', 'KN_Leather', 'WP_Wood'


def lens_section(fuller_w=0.32, fuller_d=0.18):
    """Section de lame : losange aplati (lenticulaire) creusé d'une gouttière centrale."""
    def sec(i, a, rf, rs):
        x = np.cos(a); z = np.sin(a)                     # x : épaisseur (avant/arrière), z : largeur (tranchants)
        thick = rf * np.sign(x) * np.abs(x) ** 0.8 * (1 - np.abs(z)) ** 0.45
        width = rs * z
        groove = fuller_d * rf * np.exp(-(z / fuller_w) ** 2 * 3.0) if fuller_w > 0 else 0
        return thick - np.sign(x) * groove, width
    return sec


def sword():
    a = Asset('Longsword')
    # lame : de la garde (y=0.115) à la pointe (y=1.05)
    n = 60
    path = spline([(0, 0.115, 0), (0, 0.6, 0), (0, 1.05, 0)], n)
    t = np.linspace(0, 1, n)
    rs = 0.026 * (1 - t ** 1.6) ** 0.55 * (1 - 0.18 * t) + 0.0006
    rf = 0.0042 * (1 - 0.65 * t) + 0.0005
    fuller = np.where(t < 0.72, 1.0, np.clip((0.82 - t) / 0.1, 0, 1))
    secs = []
    blade = Loft(path, rf, rs, segs=48, ref=(0, 0, 1),
                 section=lambda i, ang, rf_, rs_: lens_section(0.30 * fuller[i] + 1e-3, 0.22 * fuller[i])(i, ang, rf_, rs_))
    m = blade.mesh(0.25)
    m.UV = np.c_[m.V[:, 0] * 4 + 0.5, m.V[:, 1] * 1.2]
    a.add('lame', m, BLADE)
    # ricasso (talon de lame) et garde : quillons recourbés vers la lame, extrémités en bouton
    q = spline([(-0.115, 0.135, 0), (-0.10, 0.112, 0), (-0.05, 0.097, 0), (0, 0.095, 0), (0.05, 0.097, 0), (0.10, 0.112, 0), (0.115, 0.135, 0)], 50)
    rr = curve([(0, .007), (.15, .0075), (.4, .0095), (.5, .012), (.6, .0095), (.85, .0075), (1, .007)], 50)
    g = Loft(q, rr * 1.1, rr, p=2.6, segs=16, ref=(0, 0, 1), cap0=3, cap1=3); a.add('garde', g.mesh(0.08), STEEL)
    for sx in (-1, 1):
        a.add(f'garde_bout{sx}', rivet(np.array([0.116 * sx, 0.138, 0]), np.array([sx * 0.5, 0.86, 0]), r=0.011, h=0.008, segs=14, rings=5), BRASS)
    blk = Loft(spline([(0, 0.086, 0), (0, 0.118, 0)], 6), 0.012, 0.028, p=3.5, segs=24, cap0=1, cap1=0); a.add('garde_centre', blk.mesh(0.05), BRASS)
    # fusée : cuir tressé sur âme de bois, bagues aux extrémités
    gp = spline([(0, 0.085, 0), (0, -0.045, 0), (0, -0.14, 0)], 30)
    grip = Loft(gp, curve([(0, .0145), (.5, .0165), (1, .0145)], 30), curve([(0, .0125), (.5, .014), (1, .0125)], 30), p=2.2, segs=20)
    gm = grip.mesh(0.04); gm.UV = np.c_[gm.UV[:, 0] + gm.UV[:, 1] * 0.6, gm.UV[:, 1]]   # tressage en spirale
    a.add('fusee', gm, LEATHER)
    for y in (0.082, -0.03, -0.138):
        a.add(f'bague{y}', tube([(0, y, 0)] * 0 + [(np.cos(t_) * 0.0175, y, np.sin(t_) * 0.0155) for t_ in np.linspace(0, 2 * np.pi, 24)], 0.0028, segs=8, cap=False), BRASS)
    # pommeau en roue, facettes
    pp = spline([(0, -0.14, 0), (0, -0.165, 0), (0, -0.19, 0)], 14)
    pom = Loft(pp, curve([(0, .012), (.2, .019), (.5, .021), (.8, .019), (1, .010)], 14), curve([(0, .016), (.2, .030), (.5, .033), (.8, .030), (1, .012)], 14), p=2.2, segs=24, cap1=3)
    a.add('pommeau', pom.mesh(0.06), BRASS)
    a.add('pommeau_rivet', rivet(np.array([0, -0.194, 0]), np.array([0, -1.0, 0]), r=0.008, h=0.004), STEEL)
    return a


def heater_outline(v, u):
    """Écu « en fer à repasser » : demi-largeur selon v (0 haut → 1 pointe)."""
    hw = np.where(v < 0.42, 1.0, np.sqrt(np.clip(1 - np.clip((v - 0.42) / 0.58, 0, 1) ** 1.6, 0, 1)))
    return hw


def shield(W=0.27, Hh=0.66, bend=0.55, th=0.022):
    a = Asset('HeaterShield')
    nu, nv = 40, 56
    u = np.linspace(-1, 1, nu); v = np.linspace(0, 1, nv)
    U, Vv = np.meshgrid(u, v)
    top = 0.30
    def surf(U, Vv, dz):
        hw = heater_outline(Vv, U) * W
        x = U * hw; y = top - Vv * Hh + 0.015 * (1 - U ** 2) * (Vv < 0.02) * 0 + 0.012 * U ** 2 * (Vv < 1e-9)
        z = -bend * (x ** 2) + dz
        return np.stack([x, y, z], -1)
    def sheet(dz, flip, mat, name, uvs=1.0):
        P = surf(U, Vv, dz)
        m = Mesh(P.reshape(-1, 3), grid_faces(nv, nu), np.c_[(U.ravel() * W) / 0.35 * uvs + 0.5, (top - Vv.ravel() * Hh) / 0.35 * uvs])
        n = vertex_normals(m, weld=False)
        if (n[:, 2].mean() < 0) != flip: m.flip()
        a.add(name, m, mat); return P
    Pf = sheet(th / 2, False, STEEL, 'face')
    sheet(-th / 2, True, WOOD, 'dos')
    # chant : bourrelet laiton tout autour (bord roulé)
    rim = []
    edge = np.vstack([Pf[0, :], Pf[1:, -1], Pf[-1, ::-1], Pf[-2::-1, 0]])
    edge[:, 2] -= th / 2
    rim = tube(np.vstack([edge, edge[:1]]), th * 0.62, segs=10, tile=0.05, cap=False)
    a.add('bord', rim, BRASS)
    # ornement : croix fleuronnée dorée en relief + ombilic central
    def raised_strip(pts, w, h, name):
        pts = np.asarray(pts, float); n = len(pts)
        Pz = np.c_[pts[:, 0], pts[:, 1], -bend * pts[:, 0] ** 2 + th / 2 + 0.0015]
        lo = Loft(Pz, h, w, p=4.0, segs=16, ref=(0, 0, 1), cap0=2, cap1=2)
        a.add(name, lo.mesh(0.05), BRASS)
    # ornement doré rayonnant (griffe / étoile) autour de l'ombilic, comme sur l'écu de Kael Draven
    cx, cy = 0.0, 0.10
    for k in range(8):
        ang = np.pi / 2 + k * np.pi / 4
        L = (0.30 if k in (0, 4) else 0.21 if k in (2, 6) else 0.19) * (1.15 if k == 4 else 1.0)
        bend_r = 0.05 * (1 if k % 2 else -1)
        pts = [(cx + np.cos(ang) * r_ - np.sin(ang) * bend_r * np.sin(np.pi * r_ / L), cy + np.sin(ang) * r_ + np.cos(ang) * bend_r * np.sin(np.pi * r_ / L), 0)
               for r_ in np.linspace(0.06, L, 20)]
        pts = np.array(pts)
        hw = float(heater_outline(np.array(np.clip((0.30 - pts[-1, 1]) / 0.66, 0, 1)), 0)) * 0.27 - 0.035
        pts[:, 0] = np.clip(pts[:, 0], -hw, hw)
        Pz = np.c_[pts[:, 0], pts[:, 1], -bend * pts[:, 0] ** 2 + th / 2 + 0.0015]
        w_ = curve([(0, .016), (.6, .010), (1, .002)], 20)
        lo = Loft(Pz, curve([(0, .0045), (1, .0015)], 20), w_, p=3.0, segs=12, ref=(0, 0, 1), cap0=1, cap1=1)
        a.add(f'rayon{k}', lo.mesh(0.05), BRASS)
    # bordure intérieure gravée
    inner = []
    for vv in np.linspace(0.03, 0.97, 60):
        hw = float(heater_outline(np.array(vv), 0)) * W - 0.045
        inner.append((hw, top - vv * Hh))
    inner = np.array(inner)
    for side in (1, -1):
        P3 = np.c_[side * inner[:, 0], inner[:, 1], -bend * inner[:, 0] ** 2 + th / 2 + 0.001]
        a.add(f'liseré{side}', tube(P3, 0.0035, segs=6, tile=0.04, cap=True), BRASS)
    top_line = np.array([(x, top - 0.03 * Hh, -bend * x * x + th / 2 + 0.001) for x in np.linspace(-W + 0.045, W - 0.045, 30)])
    a.add('liseré_haut', tube(top_line, 0.0035, segs=6, tile=0.04, cap=True), BRASS)
    c = np.array([0, 0.10, th / 2 + 0.004])
    a.add('ombilic', rivet(c, np.array([0, 0, 1.0]), r=0.05, h=0.025, segs=24, rings=7), STEEL)
    a.add('ombilic_pointe', rivet(c + (0, 0, 0.023), np.array([0, 0, 1.0]), r=0.014, h=0.012), BRASS)
    # rivets de bordure
    for k in range(18):
        s = k / 18
        vv = 0.03 + 0.9 * s if s < 0.5 else 0.03 + 0.9 * (1 - s)
        side = -1 if s < 0.5 else 1
        if s >= 0.5: vv = 0.93 - (s - 0.5) * 2 * 0.9
        hw = float(heater_outline(np.array(vv), 0)) * W - 0.03
        x = side * hw; y = top - vv * Hh
        p = np.array([x, y, -bend * x * x + th / 2]); nrm = unit(np.array([2 * bend * x, 0, 1.0]))
        a.add(f'rivet{k}', rivet(p, nrm, r=0.006, h=0.0035), BRASS)
    # énarmes : deux sangles de cuir au dos
    for y, w in ((0.06, 0.12), (-0.08, 0.10)):
        pts = spline([(-w, y, -th / 2 - 0.002), (0, y, -th / 2 - 0.035), (w, y, -th / 2 - 0.002)], 16)
        lo = Loft(pts, 0.002, 0.014, p=4, segs=8, ref=(0, 0, 1))
        a.add(f'enarme{y}', lo.mesh(0.1), LEATHER)
    for _, mm, _ in a.parts: mm.V[:, 2] += 0.058          # origine au niveau de l'avant-bras, derrière l'écu
    return a
