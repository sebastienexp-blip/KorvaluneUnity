"""Pièces de costume des héroïnes, construites sur le corps de base féminin (toutes détachables, une par emplacement) :
corset à décolleté plongeant, col montant, manches, brassards, gants, épaulières à lames (pointes en option), ceinture,
pans de jupe longs ouverts devant, cuissardes à genouillère, cape. Plus : crânes procéduraux, bâton de mage.
"""
import numpy as np
from forge.rig import Asset
from forge.geo import Loft, Mesh, spline, curve, tube, rivet, plate, plate_edge, concat, rigid, unit, mirror, smoothstep, blend_bones, vertex_normals
from forge import humanoid as H, humanparts as HP, armorparts as AP
from knight import cape


def _blob(c, r, n=18):
    th = np.linspace(0, np.pi, n // 2 + 1); ph = np.linspace(0, 2 * np.pi, n + 1)
    TH, PH = np.meshgrid(th, ph, indexing='ij')
    P = np.stack([np.sin(TH) * np.cos(PH) * r[0], np.cos(TH) * r[1], np.sin(TH) * np.sin(PH) * r[2]], -1).reshape(-1, 3) + c
    R, C = TH.shape; idx = np.arange(R * C).reshape(R, C)
    F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                        np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
    m = Mesh(P, F, np.stack([PH.ravel() / (2 * np.pi), TH.ravel() / np.pi], 1))
    if (vertex_normals(m) * (P - c)).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def skull(c=(0, 0, 0), s=1.0, face=(0, 0, 1)):
    """Crâne stylisé (os + orbites sombres) : renvoie (os, creux). face : direction du visage."""
    c = np.asarray(c, float)
    parts = [_blob((0, 0.012, -0.004), (0.050, 0.052, 0.060)),            # calotte
             _blob((0, -0.020, 0.030), (0.040, 0.032, 0.030)),            # face / maxillaire
             _blob((0, -0.050, 0.030), (0.030, 0.014, 0.026))]            # mâchoire
    for sx in (-1, 1): parts.append(_blob((sx * 0.033, -0.012, 0.018), (0.016, 0.016, 0.022)))   # pommettes
    bone_m = concat(parts)
    holes = concat([_blob((sx * 0.018, -0.006, 0.052), (0.013, 0.012, 0.010)) for sx in (-1, 1)] + [_blob((0, -0.030, 0.058), (0.006, 0.009, 0.006))]
                   + [_blob((x, -0.043, 0.054), (0.0035, 0.006, 0.003)) for x in np.linspace(-0.016, 0.016, 6)])
    f = unit(np.asarray(face, float)); up = np.array([0, 1.0, 0]) - f * f[1]; up = unit(up) if np.linalg.norm(up) > 1e-6 else np.array([0, 0, 1.0])
    R = np.stack([np.cross(up, f), up, f], 1)
    for m in (bone_m, holes): m.V = (m.V * s) @ R.T + c
    return bone_m, holes


def staff(name, mats, kind='void'):
    """Bâton de mage, prise à l'origine, tête vers +Y. kind 'void' : couronne d'or et cristal violet ; 'skull' : crâne et cristal."""
    W, G, C, B, D = mats['wood'], mats['gold'], mats['crystal'], mats.get('bone', mats['gold']), mats.get('dark', mats['wood'])
    a = Asset(name)
    shaft = Loft(spline([(0, -0.78, 0), (0, 0.0, 0), (0, 0.86, 0)], 40), curve([(0, .012), (.5, .015), (1, .017)], 40), curve([(0, .012), (.5, .015), (1, .017)], 40), p=2.2, segs=14, cap0=2)
    a.add('fut', shaft.mesh(0.3), W)
    for y in (-0.76, -0.10, 0.12, 0.62, 0.80):
        a.add(f'bague{y}', Loft(spline([(0, y - 0.012, 0), (0, y + 0.012, 0)], 3), 0.021, 0.021, p=2.2, segs=16, cap0=1, cap1=1).mesh(0.05), G)
    a.add('pointe', Loft(spline([(0, -0.78, 0), (0, -0.86, 0)], 5), curve([(0, .013), (1, .002)], 5), curve([(0, .013), (1, .002)], 5), p=2, segs=10, cap1=1).mesh(0.05), G)
    top = np.array([0, 0.98, 0])
    if kind == 'void':
        # cristal allongé à facettes, tenu par quatre griffes d'or recourbées et un anneau
        cr = Loft(spline([top - (0, 0.10, 0), top, top + (0, 0.12, 0)], 12), curve([(0, .004), (.45, .034), (1, .002)], 12), curve([(0, .004), (.45, .030), (1, .002)], 12),
                  p=1.3, segs=6, cap0=1, cap1=1)
        a.add('cristal', cr.mesh(0.1), C)
        for k in range(4):
            an = k * np.pi / 2 + np.pi / 4; d = np.array([np.cos(an), 0, np.sin(an)])
            pts = [np.array([0, 0.86, 0]) + d * 0.015, top + d * 0.050 - (0, 0.06, 0), top + d * 0.058 + (0, 0.02, 0), top + d * 0.030 + (0, 0.085, 0)]
            cl = Loft(spline(pts, 14), curve([(0, .006), (1, .002)], 14), curve([(0, .008), (1, .003)], 14), p=2, segs=8, cap1=1)
            a.add(f'griffe{k}', cl.mesh(0.05), G)
        ring = tube([top + np.array([np.cos(t) * 0.062, -0.02 + 0.006 * np.sin(3 * t), np.sin(t) * 0.062]) for t in np.linspace(0, 2 * np.pi, 40)], 0.004, segs=8, cap=False)
        a.add('anneau', ring, G)
        for k in range(3):   # petits éclats en orbite
            an = k * 2.1; p = top + np.array([np.cos(an) * 0.085, 0.03 * (k - 1), np.sin(an) * 0.085])
            a.add(f'eclat{k}', Loft(spline([p - (0, 0.02, 0), p + (0, 0.02, 0)], 5), curve([(0, .002), (.5, .009), (1, .001)], 5), curve([(0, .002), (.5, .008), (1, .001)], 5), p=1.3, segs=5, cap0=1, cap1=1).mesh(0.05), C)
    else:
        bm, hm = skull(top + (0, -0.01, 0), 0.95, face=(0, 0, 1))
        a.add('crane', bm, B); a.add('crane_orbites', hm, D)
        cr = Loft(spline([top + (0, 0.04, -0.01), top + (0, 0.11, -0.01), top + (0, 0.20, -0.01)], 12), curve([(0, .006), (.4, .028), (1, .002)], 12),
                  curve([(0, .006), (.4, .024), (1, .002)], 12), p=1.3, segs=6, cap0=1, cap1=1)
        a.add('cristal', cr.mesh(0.1), C)
        for k, an in enumerate(np.linspace(0, 2 * np.pi, 6, endpoint=False)):   # couronne de pointes d'os autour du crâne
            d = np.array([np.cos(an), 0, np.sin(an)])
            a.add(f'epine{k}', AP.leaf_spike(top + d * 0.045 - (0, 0.05, 0), unit(d * 0.6 + np.array([0, 1.0, 0])), np.cross(d, (0, 1, 0)), length=0.11, width=0.010, thick=0.004, curl=0.2), D)
    return a


def build_outfit(a, body, marks, sk, P, M, style):
    """P : préfixe d'emplacement inutile (les emplacements sont nommés), M : matériaux {corset, cloth, gold, leather, metal, bone, dark}.
    style : dict(neck=…, spikes=bool, skulls=bool, tatter=float, collar=bool, sleeves=bool, cloak=bool)."""
    sw = H.swap_table(sk)
    V = body.V; ax = np.abs(V[:, 0])
    def add(n, m, mat, slot, both=False):
        a.add(n, m, mat, slot=slot)
        if both: a.add(n + '_R', mirror(m, sw), mat, slot=slot)
    W = lambda m: HP.weights_from(body, m)
    cen = marks['breasts']; by = cen[0][1]
    arms = HP.bone_mask(body, sk, ['UpperArm', 'LowerArm', 'Hand'], 0.25)
    # ---- corset : sous la poitrine jusqu'aux hanches, bonnets, décolleté en V plongeant, dos montant
    def corset_in(Q):
        x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
        ftop = np.minimum(by + 0.030 + 0.25 * np.clip(np.abs(x) - 0.09, 0, None), by - 0.075 + style.get('neck', 2.6) * np.abs(x))
        top = np.where(z > 0.0, ftop, 1.40)
        return np.minimum(y - 0.905, top - y)
    coarse = HP.bone_mask(body, sk, ['Hips', 'Spine', 'Chest', 'UpperChest'], 0.2) & ~arms & (V[:, 1] > 0.86) & (V[:, 1] < 1.46) & (ax < 0.22)
    add('corset', HP.cloth_cut(body, coarse, corset_in, 0.0045, thick=0.0025, uv_scale=7.0), M['corset'], 'Corset')
    # baleines dorées verticales et laçage central
    for x in (-0.11, -0.05, 0.05, 0.11):
        pts = [(x, 0.93, 0.25), (x, 1.02, 0.25), (x * 0.95, by - 0.05, 0.25)]
        add(f'corset_baleine{x}', HP.strap(body, pts, 0.0075, 0.007, thick=0.0018), M['gold'], 'Corset')
    for k, y in enumerate(np.linspace(0.95, by - 0.07, 6)):
        q, n_, _ = HP.surface_point(body, (0, y, 0.25), 0.010)
        add(f'corset_lacet{k}', W(tube([q + (-0.014, 0.006, 0), q + (0.014, -0.006, 0)], 0.0016, segs=5)), M['dark'], 'Corset')
        add(f'corset_oeillet{k}', W(concat([rivet(q + (sx * 0.016, 0, 0), n_, r=0.0035, h=0.002, segs=8, rings=2) for sx in (-1, 1)])), M['gold'], 'Corset')
    # ---- manches (épaule → poignet) et gants
    if style.get('sleeves', True):
        def sleeve_in(Q): return np.minimum(np.abs(Q[:, 0]) - 0.17, 0.665 - np.abs(Q[:, 0]))
        coarse = arms & (ax > 0.14) & (ax < 0.70)
        add('manches', HP.cloth_cut(body, coarse, sleeve_in, 0.0035, thick=0.002, uv_scale=8.0), M['sleeve'], 'Manches')
    add('gants', HP.shell(body, HP.region(body, HP.bone_mask(body, sk, ['Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little'], 0.3) | (arms & (ax > 0.69))),
                           offset=0.0015, thick=0.0012, uv_scale=16), M['leather'], 'Gants')
    # ---- brassards (avant-bras) bordés d'or
    larm = HP.bone_mask(body, sk, ['LowerArm'], 0.5)
    add('brassards', HP.shell(body, HP.region(body, larm & (ax > 0.53) & (ax < 0.69)), offset=0.008, thick=0.004, uv_scale=8, smooth=6, inflate=0.003), M['metal'], 'Brassards')
    for x0 in (0.525, 0.665):
        add(f'brassard_bord{x0}', HP.cloth_cut(body, larm & (ax > x0 - 0.03) & (ax < x0 + 0.06), lambda Q, x0=x0: np.minimum(np.abs(Q[:, 0]) - x0, x0 + 0.022 - np.abs(Q[:, 0])),
                                                0.0125, thick=0.003, uv_scale=6.0), M['gold'], 'Brassards')
    if style.get('spikes'):
        for k, (x, ln) in enumerate([(0.56, 0.06), (0.61, 0.05)]):
            q, n_, _ = HP.surface_point(body, (x, 1.51, -0.05), 0.012)
            fin = AP.leaf_spike(q, unit(np.array([-0.7, 0.6, -0.3])), (0, 0, 1), length=ln, width=0.012, thick=0.003, curl=0.25)
            rigid(fin, sk['LeftLowerArm']); add(f'brassard_pointe{k}', fin, M['metal'], 'Brassards', both=True)
    # ---- col montant évasé derrière la nuque, bordé d'or
    if style.get('collar', True):
        path = spline([(0, 1.47, -0.010), (0, 1.58, -0.022), (0, 1.70, -0.050)], 16)
        lo = Loft(path, curve([(0, .080), (1, .115)], 16), curve([(0, .092), (1, .135)], 16), p=2.2, segs=64)
        m = plate(lo, 0.24, 0.76, 0.0, lambda q: 1.0 - 0.18 * np.abs(np.cos(np.pi * (q - 0.24) / 0.52)) ** 2, off=0.0, thick=0.004, bevel=0.005, na=60, nt=12, tile=0.25)
        blend_bones(m, sk['UpperChest'], sk['Neck'], smoothstep((m.V[:, 1] - 1.52) / 0.06)); add('col', m, M['corset'], 'Col')
        e = plate_edge(lo, 0.25, 0.75, lambda q: 0.985 - 0.18 * np.abs(np.cos(np.pi * (q - 0.25) / 0.50)) ** 2, 0.002, r=0.004, n=60)
        blend_bones(e, sk['UpperChest'], sk['Neck'], smoothstep((e.V[:, 1] - 1.52) / 0.06)); add('col_bord', e, M['gold'], 'Col')
    # ---- épaulières à lames, bord d'or (pointes en option)
    ua = np.array(H.ARM['UpperArm'])
    pal = Loft(spline([ua + (-0.06, 0.03, 0), ua + (0.02, 0.02, 0), ua + (0.12, 0.0, 0)], 20), curve([(0, .080), (1, .064)], 20), curve([(0, .074), (1, .060)], 20), p=2.2, segs=60)
    def armL(m):
        f = smoothstep((m.V[:, 0] - 0.13) / 0.12); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); return m
    for k, (lo_, hi_) in enumerate([(0.0, 0.45), (0.35, 0.72), (0.62, 1.0)]):
        m = plate(pal, 0.40, 1.10, lo_, hi_, off=0.014 - 0.002 * k, thick=0.005, bevel=0.006, na=44, nt=10, dome=0.006, tile=0.3)
        add(f'epauliere{k}', armL(m), M['metal'], 'Epaulieres', both=True)
        e = plate_edge(pal, 0.42, 1.08, hi_ - 0.012, 0.014 - 0.002 * k + 0.003, r=0.0038, n=44)
        add(f'epauliere_bord{k}', armL(e), M['gold'], 'Epaulieres', both=True)
    if style.get('spikes'):
        for k, (t, ln, aa) in enumerate([(0.05, 0.12, 0.72), (0.20, 0.10, 0.76), (0.34, 0.075, 0.79)]):
            P_, N_, _, _ = pal.frame(np.array([t]), np.array([aa]))
            base_ = P_[0] + unit(N_[0]) * 0.022
            sp = AP.leaf_spike(base_, unit(unit(N_[0]) * 0.85 - np.array([0.5, 0, 0.1])), (1, 0, 0), length=ln, width=0.016, thick=0.005, curl=-0.3)
            add(f'epauliere_pointe{k}', armL(sp), M['metal'], 'Epaulieres', both=True)
    if style.get('skulls'):
        P_, N_, _, _ = pal.frame(np.array([0.30]), np.array([0.02]))
        bm, hm = skull(P_[0] + unit(N_[0]) * 0.03, 0.42, face=unit(N_[0]))
        add('epauliere_crane', armL(bm), M['bone'], 'Epaulieres', both=True); add('epauliere_crane_orbites', armL(hm), M['dark'], 'Epaulieres', both=True)
    # ---- ceinture et boucle
    hips = HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)
    add('ceinture', HP.cloth_cut(body, (V[:, 1] > 0.88) & (V[:, 1] < 0.98) & hips, lambda Q: np.minimum(Q[:, 1] - 0.908, 0.952 - Q[:, 1]), 0.010, thick=0.004, uv_scale=6.0),
        M['leather'], 'Ceinture')
    q, n_, _ = HP.surface_point(body, (0, 0.93, 0.2), 0.016)
    if style.get('skulls'):
        bm, hm = skull(q + n_ * 0.022, 0.55, face=n_)
        add('boucle_crane', W(bm), M['bone'], 'Ceinture'); add('boucle_crane_orbites', W(hm), M['dark'], 'Ceinture')
        for sx in (-1, 1):
            q2, n2, _ = HP.surface_point(body, (sx * 0.15, 0.92, 0.10), 0.018)
            bm, hm = skull(q2 + n2 * 0.018 - (0, 0.03, 0), 0.40, face=n2)
            add(f'hanche_crane{sx}', W(bm), M['bone'], 'Ceinture'); add(f'hanche_crane_orbites{sx}', W(hm), M['dark'], 'Ceinture')
    else:
        pl, tr = AP.shield_plate(q, n_, (0, 1, 0), W=0.040, H=0.070, top=0.45, tip=1.0, dome=0.010, ridge=0.004, wrap=0.3, trim=0.004)
        add('boucle', W(pl), M['metal'], 'Ceinture'); add('boucle_bord', W(tr), M['gold'], 'Ceinture')
        add('boucle_gemme', W(rivet(q + n_ * 0.013, n_, r=0.012, h=0.008, segs=10, rings=4)), M['crystal'], 'Ceinture')
    # ---- pans de jupe longs (côtés et dos jusqu'aux chevilles, devant ouvert sur les jambes)
    legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
    env = AP.hip_envelope(body, (legs | HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)) & (ax < 0.30), y0=0.05)
    tat = style.get('tatter', 0.15)
    for k, (th, L, wd) in enumerate([(0.80, 0.70, 0.080), (-0.80, 0.70, 0.080), (1.30, 0.80, 0.090), (-1.30, 0.80, 0.090),
                                     (1.80, 0.84, 0.095), (-1.80, 0.84, 0.095), (np.pi, 0.86, 0.110)]):
        m, edge = AP.lappet(env, th, 0.925, L, wd, off=0.020, flare=0.09, seed=11 + k, notch=tat, tip=0.6)
        for mm, mat, nm in ((m, M['cloth'], f'jupe{k}'), (edge, M['gold'], f'jupe_bord{k}')):
            yv = mm.V[:, 1]; f = smoothstep((0.88 - yv) / 0.40) * 0.85
            wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
            mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
            mm.J[:, 0] = sk['Hips']; mm.W[:, 0] = 1 - f
            mm.J[:, 1] = sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
            add(nm, mm, mat, 'Jupe')
    # ---- cuissardes : tige jusqu'à mi-cuisse, pied de botte, genouillère d'or, revers
    add('cuissardes', HP.shell(body, HP.region(body, legs & (V[:, 1] < 0.68) & (V[:, 1] > 0.085)), offset=0.006, thick=0.004, uv_scale=8, smooth=80, restore=0.002, inflate=0.004),
        M['leather'], 'Bottes')
    for sd in ('Left', 'Right'): add('pied_botte_' + sd, HP.boot_last(body, sk, sd, margin=0.010, sole=0.012), M['leather'], 'Bottes')
    add('cuissardes_revers', HP.cloth_cut(body, legs & (V[:, 1] < 0.73) & (V[:, 1] > 0.62), lambda Q: np.minimum(Q[:, 1] - 0.650, 0.690 - Q[:, 1]), 0.012, thick=0.004, uv_scale=6.0),
        M['gold'], 'Bottes')
    for sx in (1, -1):
        q, n_, _ = HP.surface_point(body, (0.095 * sx, 0.53, 0.07), 0.016)
        pl, tr = AP.shield_plate(q, n_, (0, 1, 0), W=0.046, H=0.12, top=0.45, tip=1.0, dome=0.018, ridge=0.006, wrap=0.065, trim=0.0035)
        add(f'genouillere{sx}', W(pl), M['metal'], 'Bottes'); add(f'genouillere_bord{sx}', W(tr), M['gold'], 'Bottes')
    # ---- cape longue
    if style.get('cloak', True):
        cape.build(sk, a, mat=M['cloak'], clasp=M['gold'], length=style.get('cloak_len', 1.12), width=style.get('cloak_w', 1.0), tatter=style.get('cloak_tatter', 0.6))
        for n, m, mat in a.parts:
            if n.startswith('cape'): a.slot[n] = 'Cape'


COVERS = {'Corset': ['SousVetementHaut'], 'Manches': ['Bras', 'AvantBras'], 'Gants': ['Mains'], 'Brassards': [], 'Col': [], 'Epaulieres': [],
          'Ceinture': [], 'Jupe': [], 'Bottes': ['Pieds', 'Jambes'], 'Cape': []}


# ------------------------------------------------------------------------------ animation : bâton de mage
import numpy as _np
from knight import anims as AN
from knight.anims import Style, Pose, key, build_qclip, BONES, aim_wrist


class StaffStyle(Style):
    """Bâton tenu droit dans la main droite, main gauche libre pour lancer les sorts."""
    name = 'staff'

    def guard(self, p):
        p.arm('R', down=70, fwd=16, twist=-12, elbow=52, wrist=(-20, 0, -8))
        p.arm('L', down=58, fwd=28, twist=30, elbow=62, wrist=(10, 0, 10))
        p.fist('R'); p.fist('L', 0.25)
        if AN._SK is not None: aim_wrist(AN._SK, p, 'R', (0.06, 1.0, 0.12))
        return p

    def gait_arms(self, p, w, run, arm_amp):
        p.arm('R', down=70, fwd=16 + 0.3 * arm_amp * _np.sin(w + _np.pi), twist=-12, elbow=52 + (15 if run else 0), wrist=(-20, 0, -8))
        if AN._SK is not None: aim_wrist(AN._SK, p, 'R', (0.06, 1.0, 0.25 if run else 0.12))
        p.arm('L', down=66, fwd=16 + arm_amp * _np.sin(w), twist=25, elbow=35 + (35 if run else 0)); p.fist('L', 0.3)

    def attack(self, sk):
        def P(sp, tw, rd, rf, rel, ld, lf, lel, aim, lth, rth):
            p = Pose().spine(sp, tw, 0).head(-sp * 0.4, -tw * 0.7); self.guard(p)
            p.arm('R', down=rd, fwd=rf, twist=-12, elbow=rel, wrist=(-20, 0, -8)); p.arm('L', down=ld, fwd=lf, twist=10, elbow=lel, wrist=(25, 0, 0))
            p.fist('L', 0.05); aim_wrist(sk, p, 'R', aim)
            p.leg('L', thigh=lth, knee=lth * 1.1, spread=6, turn=-8); p.leg('R', thigh=rth, knee=abs(rth) * 0.8, spread=-6, turn=12)
            return p
        poses = [(0.00, P(2, -4, 70, 16, 52, 58, 28, 62, (0.06, 1, 0.12), 4, -2), 0),
                 (0.30, P(-6, 18, 40, 30, 70, 30, 40, 80, (0.0, 1, -0.25), 6, -10), 0),        # élan : bâton levé, main gauche ramenée
                 (0.52, P(14, -16, 35, 55, 40, 12, 88, 6, (0.0, 0.7, 1.0), 26, -16), 0.10),     # projection : main gauche tendue, bâton pointé
                 (0.66, P(16, -18, 35, 58, 38, 10, 90, 4, (0.0, 0.6, 1.0), 28, -16), 0.11),
                 (1.00, P(2, -4, 70, 16, 52, 58, 28, 62, (0.06, 1, 0.12), 4, -2), 0)]
        ks = [key(t, p, hips=(0, -0.03 - 0.05 * (hz > 0.05), hz), hips_rot=(0, -10 if hz else 0, 0)) for t, p, hz in poses]
        return build_qclip('Attack', ks, BONES(sk))

    def block(self, sk):
        ks = []
        for t, br in ((0, 0), (0.6, 1), (1.2, 0)):
            p = Pose().spine(10 + br, 6, 0).head(-6, -4)
            p.arm('R', down=35, fwd=55, twist=-20, elbow=75, wrist=(-20, 0, -8))
            p.arm('L', down=30, fwd=70, twist=20, elbow=40, wrist=(40, 0, 0)); p.fist('R'); p.fist('L', 0.05)
            aim_wrist(sk, p, 'R', (1.0, 0.75, 0.25))                       # bâton en travers, main gauche levée (bouclier magique)
            p.leg('L', thigh=20, knee=28, spread=8, turn=-8); p.leg('R', thigh=-8, knee=22, spread=-8, turn=14)
            ks.append(key(t, p, hips=(0, -0.09, 0.02), hips_rot=(0, -10, 0)))
        return build_qclip('Block', ks, BONES(sk), loop=True)
