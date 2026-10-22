"""Armure de plates du chevalier de Corvalune (style gothique tardif, sombre) : chaque pièce suit la logique d'une vraie armure —
plaques qui se recouvrent dans le sens du mouvement, bords roulés, rivets, articulations (cubitières, genouillères à ailerons),
casque armet à visière fendue et percée. Côté gauche modélisé, côté droit par symétrie."""
import numpy as np
from forge.geo import (Loft, spline, curve, plate, plate_edge, tube, rivet, rivets_on, disc_cut, concat, Mesh,
                       rigid, chain, blend_bones, smoothstep, mirror, unit)
from forge import humanoid as H
from knight import body as BD

STEEL, BRASS, LEATHER, MAIL, GAMB, VOID = 'KN_Steel', 'KN_Brass', 'KN_Leather', 'KN_Mail', 'KN_Gambeson', 'KN_Void'


def tri_shape(q): return 1 - np.abs(2 * np.clip(q, 0, 1) - 1)


class Builder:
    def __init__(self, sk, asset):
        self.sk, self.asset = sk, asset
        self.swap = H.swap_table(sk)

    def add(self, name, m, mat, both=False):
        self.asset.add(name, m, mat)
        if both: self.asset.add(name + '_R', mirror(m, self.swap), mat)
        return m

    def b(self, n): return self.sk[n]


def weights_by_y(m, sk, pairs):
    """pairs : [(y, os)] décroissant ; interpolation linéaire entre os voisins selon la hauteur."""
    y = m.V[:, 1]; ys = np.array([p[0] for p in pairs]); bones = [sk[p[1]] for p in pairs]
    m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4))
    for i, yy in enumerate(y):
        if yy >= ys[0]: m.J[i, 0] = bones[0]; m.W[i, 0] = 1; continue
        if yy <= ys[-1]: m.J[i, 0] = bones[-1]; m.W[i, 0] = 1; continue
        k = np.searchsorted(-ys, -yy) - 1
        f = smoothstep((ys[k] - yy) / (ys[k] - ys[k + 1]))
        m.J[i, 0] = bones[k]; m.W[i, 0] = 1 - f; m.J[i, 1] = bones[k + 1]; m.W[i, 1] = f
    return m


def build(sk, asset):
    B = Builder(sk, asset)
    torso, neck, head = BD.torso_loft(), BD.neck_loft(), BD.head_loft()
    arm, leg, foot, palm = BD.arm_loft(), BD.leg_loft(), BD.foot_loft(), BD.palm_loft()
    ty = lambda y: (y - 0.86) / 0.70                                      # hauteur → paramètre t du tronc

    # ============================================================== couche souple : gambison, maille, cuir
    m = torso.mesh(0.15, 2, 2); BD.skin_torso(m, sk); B.add('gambison', m, GAMB)
    m = neck.mesh(0.12, 1, 2); BD.skin_torso(m, sk); B.add('col', m, MAIL)
    m = arm.mesh(0.12, 2, 2); BD.skin_arm(m, sk); B.add('manche_maille', m, MAIL, both=True)
    m = leg.mesh(0.12, 2, 2); BD.skin_leg(m, sk); B.add('chausse_maille', m, MAIL, both=True)
    m = foot.mesh(0.2, 2, 2); BD.skin_leg(m, sk); B.add('soulier', m, LEATHER, both=True)
    m = palm.mesh(0.1); BD.skin_arm(m, sk); B.add('paume', m, LEATHER, both=True)
    for fn in H.FINGERS:
        m = BD.finger_loft(fn).mesh(0.06); BD.skin_finger(m, sk, fn); B.add('gant_' + fn, m, LEATHER, both=True)
    # jupe de maille sous les faulds (haubergeon)
    sk_path = spline([(0, 1.02, 0.004), (0, 0.92, 0.0), (0, 0.80, -0.004)], 16)
    skirt_m = Loft(sk_path, curve([(0, .124), (1, .150)], 16), curve([(0, .168), (1, .205)], 16), p=2.4, segs=72)
    m = skirt_m.mesh(0.12, 1, 2)
    wl = smoothstep((m.V[:, 0] + 0.06) / 0.12); s = smoothstep((1.0 - m.V[:, 1]) / 0.2) * 0.75
    m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.zeros((len(m.V), 4))
    m.J[:, 0] = sk['Hips']; m.W[:, 0] = 1 - s; m.J[:, 1] = sk['LeftUpperLeg']; m.W[:, 1] = s * wl; m.J[:, 2] = sk['RightUpperLeg']; m.W[:, 2] = s * (1 - wl)
    B.add('jupe_maille', m, MAIL)

    # ============================================================== cuirasse
    hi_b = lambda a: ty(1.475) - 3.6 * a * a - 0.06 * np.clip(np.abs(a) - 0.17, 0, 1) * 20     # encolure + emmanchures
    lo_b = lambda a: ty(1.075) + 0.9 * a * a
    off_b = lambda da, tr, qa, qt: 0.030 + 0.010 * np.exp(-((qa - 0.5) / 0.22) ** 2) * np.sin(np.pi * np.clip(tr, 0, 1)) ** 0.7
    m = plate(torso, -0.235, 0.235, lo_b, hi_b, off=off_b, thick=0.0045, bevel=0.007, na=70, nt=48, dome=0.006, tile=0.35, edge_roll=0.0035)
    BD.skin_torso(m, sk); B.add('plastron', m, STEEL)
    # arête médiane (« tapul ») : léger relief longitudinal
    m = plate(torso, -0.010, 0.010, ty(1.09), ty(1.45), off=lambda da, tr, qa, qt: 0.0405 + 0.002 * np.sin(np.pi * tr), thick=0.0025, bevel=0.004, na=6, nt=30, tile=0.35)
    BD.skin_torso(m, sk); B.add('arete', m, STEEL)
    # bord roulé laiton de l'encolure
    m = plate_edge(torso, -0.19, 0.19, lambda a: hi_b(a) - 0.004, 0.0345, r=0.0042, n=70, tile=0.06); BD.skin_torso(m, sk); B.add('plastron_bord', m, BRASS)
    # bandes gravées dorées de part et d'autre de l'arête
    for c0 in (0.045, -0.075):
        m = plate(torso, c0, c0 + 0.030, lambda a: ty(1.25) - 1.6 * np.abs(a) + 0.02, lambda a: hi_b(a) - 0.03, off=lambda da, tr, qa, qt: off_b(da, tr, qa * 0.15 + (0.55 if c0 > 0 else 0.30), qt) + 0.0042, thick=0.0012, bevel=0.002, na=8, nt=30, tile=0.06)
        BD.skin_torso(m, sk); B.add(f'plastron_gravure{c0}', m, BRASS)
    # pansière (plackart) en pointe, rivetée
    hi_p = lambda a: ty(1.25) - 1.6 * np.abs(a)
    m = plate(torso, -0.19, 0.19, ty(1.07), hi_p, off=0.0405, thick=0.004, bevel=0.006, na=48, nt=24, dome=0.004, tile=0.35, edge_roll=0.003)
    BD.skin_torso(m, sk); B.add('pansiere', m, STEEL)
    m = plate_edge(torso, -0.17, 0.17, lambda a: hi_p(a) - 0.006, 0.045, r=0.0036, n=60, tile=0.06); BD.skin_torso(m, sk); B.add('pansiere_bord', m, BRASS)
    pts = [(hi_p(np.array(a)) - 0.025, a) for a in (-0.14, -0.07, 0.07, 0.14)] + [(ty(1.11), a) for a in (-0.16, -0.08, 0.0, 0.08, 0.16)]
    m = rivets_on(torso, pts, 0.046, r=0.0055, h=0.0032); BD.skin_torso(m, sk); B.add('pansiere_rivets', m, BRASS)
    # dossière
    hi_d = lambda a: ty(1.50) - 2.4 * (a - 0.5) ** 2
    m = plate(torso, 0.255, 0.745, ty(1.08), hi_d, off=0.026, thick=0.0045, bevel=0.007, na=60, nt=40, dome=0.004, tile=0.35, edge_roll=0.003)
    BD.skin_torso(m, sk); B.add('dossiere', m, STEEL)
    # sangles latérales (cuir) et boucles qui ferment plastron et dossière
    for y in (1.17, 1.29):
        st = plate(torso, 0.20, 0.30, ty(y) - 0.012, ty(y) + 0.012, off=0.0365, thick=0.003, bevel=0.002, na=10, nt=4, tile=0.1)
        BD.skin_torso(st, sk); B.add(f'sangle_{y}', st, LEATHER, both=True)
        P, Nn, _, _ = torso.frame(np.array(ty(y)), np.array(0.235))
        bk = rivet(P + Nn * 0.039, Nn, r=0.008, h=0.003, segs=12); BD.skin_torso(bk, sk); B.add(f'boucle_{y}', bk, BRASS, both=True)
    # rivets de bordure du plastron
    m = rivets_on(torso, [(lo_b(np.array(a)) + 0.03, a) for a in np.linspace(-0.2, 0.2, 9)], 0.034, r=0.0045, h=0.0026); BD.skin_torso(m, sk); B.add('plastron_rivets', m, STEEL)

    # ============================================================== faulds, culet, tassettes
    fp = spline([(0, 1.09, 0.006), (0, 0.98, 0.006), (0, 0.86, 0.0), (0, 0.70, -0.004)], 30)
    fauld = Loft(fp, curve([(0, .128), (.35, .138), (.7, .160), (1, .178)], 30), curve([(0, .158), (.35, .172), (.7, .200), (1, .220)], 30), p=2.5, segs=80)
    for k, (lo, hi, off) in enumerate([(0.13, 0.30, 0.020), (0.26, 0.43, 0.016), (0.39, 0.56, 0.012)]):
        m = plate(fauld, -0.245, 0.245, lo, hi, off=off, thick=0.004, bevel=0.006, na=60, nt=8, dome=0.002, tile=0.35, edge_roll=0.0025)
        weights_by_y(m, sk, [(1.0, 'Hips')]); B.add(f'fauld{k}', m, STEEL)
        m = rivets_on(fauld, [((lo + hi) / 2, a) for a in (-0.21, 0.0, 0.21)], off + 0.0045, r=0.004, h=0.0024); weights_by_y(m, sk, [(1.0, 'Hips')]); B.add(f'fauld_rivets{k}', m, BRASS)
    for k, (lo, hi, off) in enumerate([(0.10, 0.30, 0.020), (0.26, 0.48, 0.015)]):
        m = plate(fauld, 0.27, 0.73, lo, hi, off=off, thick=0.004, bevel=0.006, na=50, nt=10, dome=0.002, tile=0.35, edge_roll=0.0025)
        weights_by_y(m, sk, [(1.0, 'Hips')]); B.add(f'culet{k}', m, STEEL)
    # tassettes : plaques suspendues devant chaque cuisse (gauche ; droite par symétrie), 3 lames
    for k, (lo, hi, off) in enumerate([(0.52, 0.70, 0.016), (0.66, 0.84, 0.012), (0.80, 0.98, 0.008)]):
        shape = lambda a, lo=lo, hi=hi, k=k: hi - (0.05 if k == 2 else 0.0) * (2 * np.abs((a - 0.115) / 0.095)) ** 2
        m = plate(fauld, 0.025, 0.205, lo, shape, off=off, thick=0.004, bevel=0.006, na=26, nt=10, dome=0.004, tile=0.35, edge_roll=0.0025)
        y = m.V[:, 1]; f = smoothstep((0.95 - y) / 0.25) * 0.65
        blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], f); B.add(f'tassette{k}', m, STEEL, both=True)
    m = plate_edge(fauld, 0.03, 0.20, lambda a: 0.98 - 0.05 * (2 * np.abs((a - 0.115) / 0.095)) ** 2 - 0.01, 0.0125, r=0.0034, n=30, tile=0.06)
    blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], 0.65); B.add('tassette_bord', m, BRASS, both=True)
    m = rivets_on(fauld, [(0.61, 0.05), (0.61, 0.18), (0.75, 0.05), (0.75, 0.18)], 0.0205, r=0.0045, h=0.0026)
    blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], 0.3); B.add('tassette_rivets', m, BRASS, both=True)
    # sangles de suspension des tassettes
    m = plate(fauld, 0.10, 0.13, 0.40, 0.56, off=0.0215, thick=0.002, bevel=0.002, na=4, nt=8, tile=0.1)
    blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], 0.2); B.add('tassette_sangle', m, LEATHER, both=True)

    # ============================================================== gorgerin
    gp = spline([(0, 1.66, -0.002), (0, 1.58, -0.008), (0, 1.50, -0.016), (0, 1.43, -0.020)], 24)
    gor = Loft(gp, curve([(0, .068), (.4, .078), (.75, .112), (1, .135)], 24), curve([(0, .072), (.4, .082), (.75, .135), (1, .170)], 24), p=2.3, segs=72)
    for k, (lo, hi, off) in enumerate([(0.0, 0.34, 0.010), (0.28, 0.58, 0.014), (0.52, 0.80, 0.018), (0.74, 1.0, 0.022)]):
        m = plate(gor, -0.5, 0.5, lo, hi, off=off, thick=0.0035, bevel=0.005, na=72, nt=8, tile=0.3, edge_roll=0.002)
        weights_by_y(m, sk, [(1.60, 'Neck'), (1.48, 'UpperChest')]); B.add(f'gorgerin{k}', m, STEEL)
    m = plate_edge(gor, -0.5, 0.5, 0.012, 0.0115, r=0.0042, n=90, tile=0.06); weights_by_y(m, sk, [(1.60, 'Neck')]); B.add('gorgerin_bord', m, BRASS)

    # ============================================================== épaulières (spalières à larges ailes)
    ua = np.array(H.ARM['UpperArm'])
    pp = spline([ua + (-0.085, 0.032, 0.0), ua + (-0.02, 0.026, 0.0), ua + (0.06, 0.012, 0.0), ua + (0.15, -0.002, 0.0), ua + (0.22, -0.006, 0.0)], 36)
    pal = Loft(pp, curve([(0, .088), (.25, .104), (.55, .094), (1, .072)], 36), curve([(0, .080), (.25, .096), (.55, .086), (1, .066)], 36), p=2.2, segs=72)
    # cop principal : enveloppe l'épaule de l'avant (aile pectorale) à l'arrière (aile dorsale)
    wing = lambda a: 0.52 + 0.35 * np.exp(-((a - 1.0) / 0.12) ** 2) + 0.30 * np.exp(-((a - 0.5) / 0.12) ** 2)
    m = plate(pal, 0.40, 1.10, 0.0, wing, off=0.020, thick=0.005, bevel=0.008, na=80, nt=30, dome=0.010, tile=0.35, edge_roll=0.004)
    f = smoothstep((m.V[:, 0] - 0.13) / 0.14); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); B.add('epaule_cop', m, STEEL, both=True)
    m = plate(pal, 0.43, 1.07, lambda a: wing(a) - 0.075, lambda a: wing(a) - 0.035, off=0.0262, thick=0.0012, bevel=0.002, na=70, nt=5, tile=0.06)
    f = smoothstep((m.V[:, 0] - 0.13) / 0.14); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); B.add('epaule_gravure', m, BRASS, both=True)
    m = plate_edge(pal, 0.43, 1.07, lambda a: wing(a) - 0.012, 0.0265, r=0.0045, n=90, tile=0.06)
    f = smoothstep((m.V[:, 0] - 0.13) / 0.14); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); B.add('epaule_bord', m, BRASS, both=True)
    for k, (lo, hi, off) in enumerate([(0.50, 0.66, 0.016), (0.62, 0.79, 0.012), (0.75, 0.93, 0.008)]):
        m = plate(pal, 0.45, 1.05, lo, hi, off=off, thick=0.0045, bevel=0.006, na=56, nt=8, dome=0.003, tile=0.35, edge_roll=0.003)
        rigid(m, sk['LeftUpperArm']); B.add(f'epaule_lame{k}', m, STEEL, both=True)
        m = plate_edge(pal, 0.47, 1.03, hi - 0.006, off + 0.006, r=0.0028, n=50, tile=0.06); rigid(m, sk['LeftUpperArm']); B.add(f'epaule_lame_bord{k}', m, BRASS, both=True)
        m = rivets_on(pal, [((lo + hi) / 2, a) for a in (0.55, 0.75, 0.95)], off + 0.0048, r=0.0045, h=0.0026); rigid(m, sk['LeftUpperArm']); B.add(f'epaule_rivets{k}', m, BRASS, both=True)
    # garde-cou (haute-pièce) : lame dressée sur le bord intérieur du cop
    hp = spline([(0.125, 1.53, 0.075), (0.150, 1.56, 0.0), (0.125, 1.53, -0.075)], 20)
    haute = Loft(hp, 0.004, curve([(0, .012), (.5, .036), (1, .012)], 20), p=2.0, segs=8, ref=(1, 0, 0), cap0=2, cap1=2)
    m = haute.mesh(0.2); m.V[:, 1] += np.clip(m.V[:, 1] - 1.53, 0, None) * 0.0; rigid(m, sk['LeftShoulder']); B.add('epaule_garde', m, STEEL, both=True)
    m = rivets_on(pal, [(0.20, 0.75), (0.30, 0.62), (0.30, 0.88)], 0.0255, r=0.0065, h=0.0036)
    f = smoothstep((m.V[:, 0] - 0.13) / 0.14); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); B.add('epaule_rosettes', m, BRASS, both=True)

    # ============================================================== bras : canon d'arrière-bras, cubitière, canon d'avant-bras
    for k, (lo, hi, off) in enumerate([(0.15, 0.30, 0.012), (0.27, 0.43, 0.009)]):
        m = plate(arm, 0.0, 1.0, lo, hi, off=off, thick=0.004, bevel=0.005, na=48, nt=10, tile=0.3, edge_roll=0.002)
        rigid(m, sk['LeftUpperArm']); B.add(f'canon_haut{k}', m, STEEL, both=True)
    # cubitière : coquille bombée sur la pointe du coude (arrière, a=0.5) + aileron en éventail côté extérieur
    m = plate(arm, 0.30, 0.70, 0.40, 0.58, off=0.012, thick=0.005, bevel=0.007, na=30, nt=18, dome=0.022, tile=0.25, edge_roll=0.003)
    blend_bones(m, sk['LeftUpperArm'], sk['LeftLowerArm'], 0.5); B.add('cubitiere', m, STEEL, both=True)
    fan_hi = lambda a: 0.49 + 0.10 * np.sin(np.pi * np.clip((a - 0.58) / 0.30, 0, 1))
    fan_lo = lambda a: 0.49 - 0.10 * np.sin(np.pi * np.clip((a - 0.58) / 0.30, 0, 1))
    m = plate(arm, 0.58, 0.88, fan_lo, fan_hi, off=0.022, thick=0.004, bevel=0.006, na=26, nt=18, dome=0.010, tile=0.25, edge_roll=0.003)
    blend_bones(m, sk['LeftUpperArm'], sk['LeftLowerArm'], 0.5); B.add('cubitiere_aile', m, STEEL, both=True)
    m = rivets_on(arm, [(0.49, 0.73)], 0.034, r=0.008, h=0.004); blend_bones(m, sk['LeftUpperArm'], sk['LeftLowerArm'], 0.5); B.add('cubitiere_rosette', m, BRASS, both=True)
    for k, (lo, hi, off) in enumerate([(0.36, 0.42, 0.010), (0.56, 0.62, 0.010)]):
        m = plate(arm, 0.28, 0.72, lo, hi, off=off, thick=0.0035, bevel=0.004, na=20, nt=5, tile=0.25)
        rigid(m, sk['LeftUpperArm'] if k == 0 else sk['LeftLowerArm']); B.add(f'cubitiere_lame{k}', m, STEEL, both=True)
    # canon d'avant-bras : deux valves (dessus/dessous) avec charnière rivetée
    m = plate(arm, 0.47, 1.03, 0.60, 0.90, off=0.012, thick=0.0045, bevel=0.006, na=40, nt=24, dome=0.003, tile=0.3, edge_roll=0.0025)
    rigid(m, sk['LeftLowerArm']); B.add('canon_bas_dessus', m, STEEL, both=True)
    m = plate(arm, 0.0, 0.50, 0.61, 0.89, off=0.009, thick=0.004, bevel=0.006, na=36, nt=24, tile=0.3, edge_roll=0.002)
    rigid(m, sk['LeftLowerArm']); B.add('canon_bas_dessous', m, STEEL, both=True)
    m = rivets_on(arm, [(t, 0.49) for t in (0.65, 0.75, 0.85)] + [(t, 0.01) for t in (0.65, 0.75, 0.85)], 0.0175, r=0.004, h=0.0024)
    rigid(m, sk['LeftLowerArm']); B.add('canon_bas_rivets', m, BRASS, both=True)

    # ============================================================== gantelets
    hd = np.array(H.ARM['Hand'])
    cp = spline([hd + (-0.075, 0, 0), hd + (-0.035, 0, 0), hd + (0.005, -0.002, 0)], 16)
    cuff = Loft(cp, curve([(0, .036), (.5, .042), (1, .052)], 16), curve([(0, .040), (.5, .048), (1, .060)], 16), p=2.4, segs=48)
    m = plate(cuff, 0.0, 1.0, 0.0, 1.0, off=0.006, thick=0.004, bevel=0.005, na=48, nt=12, tile=0.25, edge_roll=0.0025)
    blend_bones(m, sk['LeftLowerArm'], sk['LeftHand'], smoothstep((m.V[:, 0] - hd[0] + 0.03) / 0.04)); B.add('gantelet_manchette', m, STEEL, both=True)
    m = plate_edge(cuff, 0.0, 1.0, 0.985, 0.0095, r=0.003, n=60, tile=0.06)
    rigid(m, sk['LeftHand']); B.add('gantelet_bord', m, BRASS, both=True)
    # plaque métacarpienne + lames des jointures (palm_loft : a=0 dessus de la main)
    m = plate(palm, -0.22, 0.22, 0.25, 0.70, off=0.004, thick=0.0035, bevel=0.005, na=20, nt=14, dome=0.004, tile=0.2, edge_roll=0.0015)
    rigid(m, sk['LeftHand']); B.add('gantelet_dos', m, STEEL, both=True)
    for k, (lo, hi) in enumerate([(0.66, 0.82), (0.79, 0.97)]):
        m = plate(palm, -0.22, 0.22, lo, hi, off=0.0045 + 0.001 * k, thick=0.0032, bevel=0.004, na=20, nt=6, dome=0.002, tile=0.2)
        rigid(m, sk['LeftHand']); B.add(f'gantelet_jointure{k}', m, STEEL, both=True)
    m = rivets_on(palm, [(0.5, -0.12), (0.5, 0.12)], 0.0085, r=0.004, h=0.0022); rigid(m, sk['LeftHand']); B.add('gantelet_rivets', m, BRASS, both=True)
    # lames de doigts : une par phalange
    for fn in H.FINGERS:
        fl = BD.finger_loft(fn)
        segs = [(0.02, 0.40, 'Proximal'), (0.40, 0.70, 'Intermediate'), (0.68, 0.98, 'Distal')]
        for lo, hi, seg in segs:
            m = plate(fl, -0.27, 0.27, lo, hi, off=0.0015, thick=0.0022, bevel=0.0025, na=12, nt=6, dome=0.0012, tile=0.1)
            rigid(m, sk[f'Left{fn}{seg}']); B.add(f'doigt_{fn}_{seg}', m, STEEL, both=True)

    # ============================================================== jambes : cuissots, genouillères, grèves, solerets
    for k, (lo, hi, off) in enumerate([(0.04, 0.13, 0.016), (0.11, 0.42, 0.012)]):
        m = plate(leg, -0.34, 0.24, lo, hi, off=off, thick=0.0045, bevel=0.007, na=48, nt=8 if k == 0 else 30, dome=0.004 if k else 0.0, tile=0.35, edge_roll=0.003)
        blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], smoothstep((0.98 - m.V[:, 1]) / 0.06)); B.add(f'cuissot{k}', m, STEEL, both=True)
    m = plate_edge(leg, -0.32, 0.22, 0.045, 0.0205, r=0.0042, n=60, tile=0.06)
    blend_bones(m, sk['Hips'], sk['LeftUpperLeg'], 0.6); B.add('cuissot_bord', m, BRASS, both=True)
    m = rivets_on(leg, [(0.20, -0.25), (0.30, -0.25), (0.20, 0.17), (0.30, 0.17)], 0.0165, r=0.0045, h=0.0026); rigid(m, sk['LeftUpperLeg']); B.add('cuissot_rivets', m, BRASS, both=True)
    tk = 0.53
    for k, (lo, hi) in enumerate([(0.42, 0.47), (0.60, 0.645)]):
        m = plate(leg, -0.27, 0.27, lo, hi, off=0.012, thick=0.004, bevel=0.005, na=26, nt=5, tile=0.3, edge_roll=0.0015)
        rigid(m, sk['LeftUpperLeg'] if k == 0 else sk['LeftLowerLeg']); B.add(f'genou_lame{k}', m, STEEL, both=True)
    m = plate(leg, -0.24, 0.24, 0.455, 0.615, off=0.016, thick=0.005, bevel=0.007, na=30, nt=20, dome=0.030, tile=0.25, edge_roll=0.003)
    blend_bones(m, sk['LeftUpperLeg'], sk['LeftLowerLeg'], 0.5); B.add('genouillere', m, STEEL, both=True)
    gw_hi = lambda a: 0.535 + 0.085 * np.sin(np.pi * np.clip((a + 0.42) / 0.26, 0, 1))
    gw_lo = lambda a: 0.535 - 0.085 * np.sin(np.pi * np.clip((a + 0.42) / 0.26, 0, 1))
    m = plate(leg, -0.42, -0.16, gw_lo, gw_hi, off=0.026, thick=0.004, bevel=0.006, na=24, nt=18, dome=0.010, tile=0.25, edge_roll=0.003)
    blend_bones(m, sk['LeftUpperLeg'], sk['LeftLowerLeg'], 0.5); B.add('genou_aile', m, STEEL, both=True)
    m = rivets_on(leg, [(0.535, -0.29)], 0.037, r=0.008, h=0.004); blend_bones(m, sk['LeftUpperLeg'], sk['LeftLowerLeg'], 0.5); B.add('genou_rosette', m, BRASS, both=True)
    m = plate(leg, -0.29, 0.29, 0.63, 0.965, off=0.012, thick=0.0045, bevel=0.007, na=44, nt=30, dome=0.004, tile=0.35, edge_roll=0.003)
    rigid(m, sk['LeftLowerLeg']); B.add('greve_avant', m, STEEL, both=True)
    m = plate(leg, 0.22, 0.78, 0.66, 0.96, off=0.009, thick=0.004, bevel=0.006, na=40, nt=28, tile=0.35, edge_roll=0.002)
    rigid(m, sk['LeftLowerLeg']); B.add('greve_arriere', m, STEEL, both=True)
    m = plate_edge(leg, -0.27, 0.27, 0.955, 0.0165, r=0.0038, n=50, tile=0.06); rigid(m, sk['LeftLowerLeg']); B.add('greve_bord', m, BRASS, both=True)
    m = rivets_on(leg, [(t, -0.27) for t in (0.70, 0.80, 0.90)] + [(t, 0.27) for t in (0.70, 0.80, 0.90)], 0.0175, r=0.0042, h=0.0024)
    rigid(m, sk['LeftLowerLeg']); B.add('greve_rivets', m, BRASS, both=True)
    # solerets : lames articulées sur le cou-de-pied, pointe, talonnière (foot_loft : a=0 dessus)
    for k in range(5):
        lo = 0.30 + 0.105 * k; hi = lo + 0.13
        m = plate(foot, -0.30, 0.30, lo, hi, off=0.006 + 0.0012 * (4 - k), thick=0.0035, bevel=0.005, na=28, nt=6, dome=0.002, tile=0.25, edge_roll=0.0015)
        z = m.V[:, 2]; f = smoothstep((z - 0.06) / 0.05)
        blend_bones(m, sk['LeftFoot'], sk['LeftToes'], f); B.add(f'soleret{k}', m, STEEL, both=True)
    m = plate(foot, -0.5, 0.5, 0.80, 1.0, off=0.007, thick=0.004, bevel=0.005, na=30, nt=10, dome=0.003, tile=0.25)
    rigid(m, sk['LeftToes']); B.add('soleret_pointe', m, STEEL, both=True)
    m = plate(foot, 0.30, 0.70, 0.0, 0.30, off=0.006, thick=0.004, bevel=0.005, na=24, nt=10, dome=0.003, tile=0.25)
    rigid(m, sk['LeftFoot']); B.add('talonniere', m, STEEL, both=True)

    # ============================================================== casque : armet à visière en bec, fente de vue, ventaille percée
    hl = head
    rigid_head = lambda m: rigid(m, sk['Head'])
    m = hl.mesh(0.2, 2, 2); rigid_head(m); B.add('casque_interieur', m, VOID)
    # timbre (bombe) : du front à la nuque
    m = plate(hl, 0.0, 1.0, lambda a: 0.50 + 0.17 * np.exp(-((a - 0.0) / 0.2) ** 2) + 0.17 * np.exp(-((a - 1.0) / 0.2) ** 2), 1.0,
              off=0.018, thick=0.0045, bevel=0.006, na=96, nt=40, tile=0.3, edge_roll=0.0)
    rigid_head(m); B.add('casque_timbre', m, STEEL)
    # crête : relief médian d'avant en arrière
    for a0 in (-0.012, 0.488):
        m = plate(hl, a0, a0 + 0.024, 0.52, 0.995, off=lambda da, tr, qa, qt: 0.0215 + 0.0035 * np.sin(np.pi * qa), thick=0.002, bevel=0.004, na=8, nt=40, tile=0.2)
        rigid_head(m); B.add(f'casque_crete{a0}', m, STEEL)
    # bandeau gravé laiton (côtés et nuque)
    m = plate(hl, 0.23, 0.77, 0.47, 0.53, off=0.023, thick=0.0022, bevel=0.002, na=60, nt=5, tile=0.12)
    rigid_head(m); B.add('casque_bandeau', m, BRASS)
    # couvre-nuque en lames
    for k, (lo, hi, off) in enumerate([(0.05, 0.30, 0.022), (-0.10, 0.12, 0.028)]):
        m = plate(hl, 0.26, 0.74, max(lo, 0.0), hi, off=off, thick=0.004, bevel=0.005, na=44, nt=10, tile=0.3, edge_roll=0.0025)
        if k == 1:
            m.V[:, 1] -= 0.018 * smoothstep((-m.V[:, 2] - 0.04) / 0.06); m.V[:, 2] -= 0.012
        rigid_head(m); B.add(f'casque_nuque{k}', m, STEEL)
    # joues (couvrent les côtés du visage, ferment sous le menton)
    m = plate(hl, 0.14, 0.32, 0.0, 0.52, off=0.022, thick=0.0045, bevel=0.006, na=24, nt=24, dome=0.004, tile=0.3, edge_roll=0.0025)
    rigid_head(m); B.add('casque_joue', m, STEEL, both=True)
    # mentonnière / ventaille : bec avancé, percée de bouches d'aération
    beak = lambda da, tr, qa, qt: 0.024 + 0.050 * (1 - np.abs(2 * qa - 1)) ** 1.7 * (0.25 + 0.75 * np.sin(np.pi * np.clip(tr * 0.85 + 0.15, 0, 1)))
    m = plate(hl, -0.17, 0.17, 0.0, 0.475, off=beak, thick=0.0045, bevel=0.006, na=48, nt=30, tile=0.3, edge_roll=0.0025)
    rigid_head(m); B.add('casque_ventaille', m, STEEL)
    visor = lambda da, tr, qa, qt: 0.0245 + 0.040 * (1 - np.abs(2 * qa - 1)) ** 1.7 * (1 - np.clip(tr, 0, 1)) ** 1.4
    m = plate(hl, -0.19, 0.19, 0.505, 0.72, off=visor, thick=0.0045, bevel=0.005, na=48, nt=18, tile=0.3, edge_roll=0.0025)
    rigid_head(m); B.add('casque_visiere', m, STEEL)
    # fente de vue : sous la visière, l'intérieur sombre est visible (intervalle t 0.475–0.505)
    m = plate(hl, -0.17, 0.17, 0.46, 0.52, off=0.012, thick=0.001, bevel=0.001, na=20, nt=4, tile=0.3)
    rigid_head(m); B.add('casque_fente', m, VOID)
    # bouches d'aération (côté droit de la ventaille, comme sur les armets réels)
    holes = []
    for row in range(3):
        for col in range(5):
            a = -0.035 - 0.022 * col; t = 0.17 + 0.075 * row
            P, Nn, _, _ = hl.frame(np.array(t), np.array(a % 1.0))
            o = float(beak(np.array(0.0), np.array(t / 0.475), np.array((a + 0.17) / 0.34), np.array(0.0))) + 0.0048
            holes.append(disc_cut(P + Nn * o, Nn, 0.0032, depth=0.004))
    m = concat(holes); rigid_head(m); B.add('casque_aeration', m, VOID)
    # pivots de visière : rosettes laiton
    m = rivets_on(hl, [(0.53, 0.205), (0.53, 0.795)], 0.032, r=0.010, h=0.005); rigid_head(m); B.add('casque_pivots', m, BRASS)
    m = rivets_on(hl, [(0.30, a) for a in (0.18, 0.28, 0.72, 0.82)] + [(0.62, a) for a in (0.30, 0.40, 0.5, 0.60, 0.70)], 0.0245, r=0.004, h=0.0024)
    rigid_head(m); B.add('casque_rivets', m, STEEL)
    m = plate_edge(hl, -0.18, 0.18, 0.512, lambda a: 0.0245 + 0.040 * (1 - np.abs(a / 0.19)) ** 1.7 + 0.004, r=0.0028, n=50, tile=0.06) if False else None
    return B
