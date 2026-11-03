"""Grands félins montés (Tigre des cendres, Panthère d'ombre) et Griffon royal (corps de lion, tête et ailes d'aigle).

Anatomie dédiée de grand félin (1 m à l'épaule, 1,9 m du poitrail à la croupe) : poitrine profonde, omoplates saillantes,
pattes antérieures massives, coussinets larges, dos souple, ventre à repli de peau, longue queue. Tête ronde, joues,
museau court, coussinets des moustaches, truffe, oreilles arrondies. Robe en projection cylindrique autour du corps :
les rayures du tigre font le tour du tronc comme chez le vrai tigre, ventre et bas de la face blancs. Mêmes noms d'os
que les autres quadrupèdes (animations communes du loup, adaptées au félin). Socket_Saddle pour le cavalier."""
import numpy as np
from forge.rig import Skeleton, Asset, qeuler
from forge.geo import Mesh, Loft, spline, curve, concat, rigid, unit, mirror
from forge import sculpt as SC
from forge.texgen import TexSet, fbm, n01, height_to_normal
from creatures import common as CC, quadruped as QA
from creatures import cheval as CH
from knight import materials as KM

J = {
    'Hips': (0, 0.92, -0.62), 'Spine1': (0, 0.95, -0.30), 'Spine2': (0, 0.96, 0.0), 'Chest': (0, 0.96, 0.32),
    'Neck1': (0, 0.98, 0.55), 'Neck2': (0, 1.03, 0.70), 'Head': (0, 1.06, 0.82), 'Jaw': (0, 0.97, 0.88),
    'Tail1': (0, 0.92, -0.84), 'Tail2': (0, 0.78, -1.10), 'Tail3': (0, 0.62, -1.40), 'Tail4': (0, 0.56, -1.70),
    'Ear': (0.10, 1.15, 0.80),
    'Scapula': (0.12, 1.00, 0.36), 'UpperArm': (0.17, 0.74, 0.42), 'LowerArm': (0.17, 0.48, 0.29), 'Wrist': (0.16, 0.14, 0.36), 'FrontPaw': (0.16, 0.055, 0.41),
    'Thigh': (0.15, 0.86, -0.62), 'Shin': (0.18, 0.58, -0.42), 'Hock': (0.15, 0.30, -0.75), 'BackPaw': (0.15, 0.05, -0.66),
}
TIP = {'Tail': (0, 0.66, -1.96)}


def skeleton(wings=False):
    mx = lambda p: (-p[0], p[1], p[2])
    sk = Skeleton()
    sk.add('Hips', None, J['Hips']); sk.add('Spine1', 'Hips', J['Spine1']); sk.add('Spine2', 'Spine1', J['Spine2']); sk.add('Chest', 'Spine2', J['Chest'])
    sk.add('Neck1', 'Chest', J['Neck1']); sk.add('Neck2', 'Neck1', J['Neck2']); sk.add('Head', 'Neck2', J['Head']); sk.add('Jaw', 'Head', J['Jaw'])
    par = 'Hips'
    for k in range(1, 5): sk.add(f'Tail{k}', par, J[f'Tail{k}']); par = f'Tail{k}'
    for s, f in (('L', lambda p: p), ('R', mx)):
        sk.add(f'Ear_{s}', 'Head', f(J['Ear']))
        sk.add(f'Scapula_{s}', 'Chest', f(J['Scapula'])); sk.add(f'UpperArm_{s}', f'Scapula_{s}', f(J['UpperArm']))
        sk.add(f'LowerArm_{s}', f'UpperArm_{s}', f(J['LowerArm'])); sk.add(f'Wrist_{s}', f'LowerArm_{s}', f(J['Wrist'])); sk.add(f'FrontPaw_{s}', f'Wrist_{s}', f(J['FrontPaw']))
        sk.add(f'Thigh_{s}', 'Hips', f(J['Thigh'])); sk.add(f'Shin_{s}', f'Thigh_{s}', f(J['Shin'])); sk.add(f'Hock_{s}', f'Shin_{s}', f(J['Hock'])); sk.add(f'BackPaw_{s}', f'Hock_{s}', f(J['BackPaw']))
        if wings:
            sk.add(f'Wing1_{s}', 'Chest', f((0.18, 0.98, 0.30))); sk.add(f'Wing2_{s}', f'Wing1_{s}', f((0.27, 0.93, 0.12)))
            sk.add(f'Wing3_{s}', f'Wing2_{s}', f((0.29, 0.84, -0.40)))
    sk.add('Socket_Saddle', 'Spine2', (0, 1.16, 0.0), null=True)
    sk.add('Socket_Chest', 'Chest', (0, 0.75, 0.52), null=True)
    return sk


def P(n, side=1): p = np.array(J[n], float); p[0] *= side; return p


def body(sk, slim=1.0, eagle=False):
    s = SC.Sculpt(k=0.03)
    w = slim
    # tronc
    s.ell('Chest', (0, 0.78, 0.32), (0.23 * w, 0.29, 0.30))
    s.ell('Spine2', (0, 0.79, 0.02), (0.22 * w, 0.24, 0.40))
    s.ell('Spine1', (0, 0.84, -0.30), (0.19 * w, 0.17, 0.28))
    s.ell('Spine1', (0, 0.63, -0.24), (0.14 * w, 0.08, 0.30), k=0.04)                 # repli de peau du ventre
    s.ell('Hips', (0, 0.83, -0.62), (0.20 * w, 0.21, 0.24))
    s.ell('Hips', (0, 0.80, -0.82), (0.15 * w, 0.17, 0.10))
    for sx in (1, -1): s.ell(f'Scapula_{"L" if sx > 0 else "R"}', (0.12 * sx * w, 0.97, 0.36), (0.075, 0.15, 0.12), fwd=(0, -0.8, 0.6))   # omoplates saillantes
    # cou épais
    s.cone('Neck1', (0, 0.86, 0.45), (0, 1.00, 0.76), 0.22 * w, 0.16 * w)
    if eagle:
        # tête d'aigle : crâne, arcades marquées, cou de plumes plus fin ; le bec est ajouté à part
        s.ell('Head', (0, 1.08, 0.86), (0.11, 0.11, 0.13))
        for sx in (1, -1): s.ell('Head', (0.065 * sx, 1.13, 0.93), (0.045, 0.03, 0.06), k=0.015)
        s.cone('Jaw', (0, 1.00, 0.86), (0, 0.98, 0.98), 0.06, 0.035, k=0.015)
    else:
        s.ell('Head', (0, 1.07, 0.84), (0.165 * w, 0.125, 0.14))                        # crâne large et rond
        for sx in (1, -1):
            s.ell('Head', (0.115 * sx * w, 1.00, 0.89), (0.085, 0.085, 0.09))           # joues
            s.ell('Head', (0.06 * sx, 1.12, 0.95), (0.05, 0.03, 0.04), k=0.015)        # arcades
            s.ell('Head', (0.042 * sx, 0.975, 1.005), (0.045, 0.040, 0.038), k=0.015) # coussinets des moustaches
        s.ell('Head', (0, 0.995, 0.965), (0.095, 0.068, 0.07))                         # museau court et large
        s.ell('Head', (0, 1.022, 1.025), (0.040, 0.026, 0.026), k=0.010)               # truffe
        s.cone('Jaw', (0, 0.94, 0.88), (0, 0.925, 0.97), 0.08, 0.048, k=0.015)          # menton
        s.cone('Ear_L', (0.10 * w, 1.13, 0.81), (0.13 * w, 1.235, 0.79), 0.052, 0.028, squash=(0.40, (0.2, 0, 1)), k=0.012)
    # antérieurs massifs
    s.cone('UpperArm_L', (0.17 * w, 0.80, 0.40), (0.17 * w, 0.50, 0.30), 0.12, 0.09)
    s.ell('UpperArm_L', (0.17 * w, 0.62, 0.29), (0.09, 0.13, 0.10))
    s.cone('LowerArm_L', (0.17 * w, 0.50, 0.30), (0.16 * w, 0.16, 0.36), 0.095, 0.065)
    s.ell('LowerArm_L', (0.165 * w, 0.40, 0.31), (0.08, 0.11, 0.08))
    s.ell('Wrist_L', (0.16 * w, 0.13, 0.37), (0.065, 0.06, 0.065), k=0.015)
    s.ell('FrontPaw_L', (0.16 * w, 0.05, 0.44), (0.085, 0.05, 0.10), k=0.015)
    # postérieurs
    s.ell('Thigh_L', (0.15 * w, 0.76, -0.58), (0.12, 0.24, 0.20), fwd=(0, -0.5, 0.45))
    s.cone('Shin_L', (0.17 * w, 0.60, -0.44), (0.15 * w, 0.31, -0.74), 0.10, 0.055)
    s.ell('Shin_L', (0.16 * w, 0.48, -0.58), (0.07, 0.12, 0.09), fwd=(0, -0.7, -0.6))
    s.cone('Hock_L', (0.15 * w, 0.31, -0.75), (0.15 * w, 0.08, -0.67), 0.052, 0.050, k=0.015)
    s.ell('BackPaw_L', (0.15 * w, 0.045, -0.60), (0.075, 0.045, 0.095), k=0.015)
    # queue
    for a_, b_, ra, rb in (('Tail1', 'Tail2', 0.065, 0.055), ('Tail2', 'Tail3', 0.055, 0.048), ('Tail3', 'Tail4', 0.048, 0.042)):
        s.cone(a_, P(a_), P(b_), ra, rb, k=0.02)
    s.cone('Tail4', P('Tail4'), np.array(TIP['Tail']), 0.042, 0.036 if not eagle else 0.05, k=0.02)
    sw = lambda b: b[:-2] + '_R' if b.endswith('_L') else b
    s.mirror(sw)
    detail = lambda X: 0.0015 * SC.noise3(X, 0.04, seed=21, octaves=2)
    m = s.mesh(res=0.016, detail=detail, smooth=6)
    s.skin(m, sk, kw=0.035)
    # projection cylindrique autour de l'axe du corps : u le long du corps, v de la ligne du dos (0) au ventre (1)
    x, y, z = m.V.T
    ang = np.arctan2(np.abs(x), y - 0.78) / np.pi
    v = ang.copy()
    leg = (y < 0.60) & (np.abs(x) > 0.07); v[leg] = np.minimum(v[leg], 0.30 + 0.25 * (np.abs(x[leg]) < 0.12))   # pattes : rayées dehors, plus claires dedans
    tail = z < -0.80; v[tail] = np.minimum(v[tail], 0.35)
    m.UV = np.c_[(z + 2.0) / 4.0 * 3.0, 1.0 - v]
    return m


def coat(S, seed, base, belly, stripes=None, rosette=None, seam=False):
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S] / S                                     # x : le long du corps (u), y : dos → ventre (v)
    hair = fbm(S, S, r, base=S / 140, octaves=3, aniso=(1.0, 0.10))
    mott = n01(fbm(S, S, r, base=S / 8, octaves=4))
    col = np.array(base)[None, None] * (0.88 + 0.22 * mott[..., None])
    wb = np.clip((yy - 0.62) / 0.18, 0, 1)[..., None]                    # ventre clair
    col = col * (1 - wb) + np.array(belly)[None, None] * wb
    if stripes is not None:
        wob = n01(fbm(S, S, r, base=S / 10, octaves=3))
        ph = xx * 26 + 1.8 * np.sin(yy * 7 + xx * 3) + wob * 1.6
        band = np.abs(np.sin(ph * np.pi / 2)) ** 6                            # bandes fines, irrégulières
        split = (n01(fbm(S, S, r, base=S / 18, octaves=2)) > 0.38)
        taper = np.clip(1 - (yy - 0.05) / 0.70, 0, 1) ** 0.6                  # les rayures s'affinent vers le ventre
        mk = np.clip(band * 2.4 - (1 - taper) * 1.8, 0, 1) * split
        col = col * (1 - mk[..., None]) + np.array(stripes)[None, None] * mk[..., None]
    if rosette is not None:
        n = n01(fbm(S, S, r, base=S / 22, octaves=2)); ring = np.exp(-((n - 0.6) / 0.025) ** 2)
        col = col * (1 - 0.45 * ring[..., None]) + np.array(rosette)[None, None] * 0.45 * ring[..., None]
    col = col * (0.92 + 0.12 * n01(hair)[..., None])
    return TexSet(np.clip(col, 0, 1), height_to_normal(hair, 0.35), np.zeros((S, S)), np.full((S, S), 0.40 if rosette is not None else 0.30), np.clip(0.8 + 0.2 * n01(hair), 0, 1))


def feathers(S, seed, root, tip, barred=False):
    r = np.random.default_rng(seed); cols = 4; cw = S // cols
    col = np.zeros((S, S, 3)); alpha = np.zeros((S, S)); hgt = np.zeros((S, S))
    yy = (np.arange(S)[:, None] / (S - 1)).repeat(S, 1)
    for c in range(cols):
        xx = (np.arange(S)[None, :] - (c + 0.5) * cw) / (cw * 0.5)
        width = 0.92 * np.sqrt(np.clip(1 - (2 * yy - 1) ** 6, 0, 1)) * (0.35 + 0.65 * np.clip(yy * 1.6, 0, 1))
        inside = (np.abs(xx) < width) & ((np.arange(S)[None, :] // cw) == c)
        barbs = 0.5 + 0.5 * np.sin(yy * 120 + np.abs(xx) * 25 + c)
        t = np.clip(yy, 0, 1)[..., None]
        cc = np.array(root)[None, None] * (1 - t) + np.array(tip)[None, None] * t
        if barred: cc = cc * (0.75 + 0.25 * (np.sin(yy * 28 + c) > 0))[..., None]
        cc = cc * (0.80 + 0.20 * barbs[..., None])
        rach = np.exp(-(xx / 0.04) ** 2); cc = cc * (1 - 0.25 * rach[..., None]) + 0.25 * rach[..., None] * 0.85
        col = np.where(inside[..., None], cc, col); alpha = np.where(inside, 1.0, alpha); hgt = np.where(inside, 0.5 + 0.3 * barbs + 0.5 * rach, hgt)
    return TexSet(np.clip(col, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), np.full((S, S), 0.3), np.clip(0.55 + 0.45 * yy, 0, 1), None, alpha)


RECIPES = {
    'FE_Tigre':    (lambda S: coat(S, 951, (0.82, 0.40, 0.08), (0.93, 0.90, 0.84), stripes=(0.03, 0.025, 0.02)), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'FE_Panthere': (lambda S: coat(S, 952, (0.035, 0.032, 0.040), (0.05, 0.045, 0.055), rosette=(0.0, 0.0, 0.0)), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'FE_Lion':     (lambda S: coat(S, 953, (0.66, 0.48, 0.25), (0.86, 0.76, 0.58)), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'FE_Blanc':    (lambda S: coat(S, 959, (0.92, 0.90, 0.85), (0.95, 0.93, 0.89)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'FE_PoilT':    (lambda S: CC.fur_texture(S, seed=954, root=(0.70, 0.36, 0.08), tip=(0.96, 0.92, 0.86), density=1.2), 512, 256, dict(metallic=0.0, smoothness=1.0, cutoff=0.45)),
    'FE_PoilP':    (lambda S: CC.fur_texture(S, seed=955, root=(0.02, 0.02, 0.025), tip=(0.08, 0.08, 0.10), density=1.2), 512, 256, dict(metallic=0.0, smoothness=1.0, cutoff=0.45)),
    'FE_Truffe':   (lambda S: KM.leather(S, seed=956, tone=(0.30, 0.12, 0.10)), 128, 64, dict(metallic=0.0, smoothness=1.0)),
    'FE_TruffeN':  (lambda S: KM.leather(S, seed=957, tone=(0.03, 0.025, 0.03)), 128, 64, dict(metallic=0.0, smoothness=1.0)),
    'FE_Oeil':     (lambda S: _eye(S, (0.85, 0.62, 0.12)), 128, 64, dict(metallic=0.0, smoothness=1.0, emission=(0.4, 0.4, 0.4), emission_preview=(0.3, 0.3, 0.3))),
    'FE_OeilV':    (lambda S: _eye(S, (0.55, 0.85, 0.25)), 128, 64, dict(metallic=0.0, smoothness=1.0, emission=(0.6, 0.6, 0.6), emission_preview=(0.4, 0.4, 0.4))),
    'FE_Griffe':   (lambda S: KM.bone(S, seed=958, tone=(0.85, 0.80, 0.70)), 128, 64, dict(metallic=0.0, smoothness=1.0)),
    'FE_Plume':    (lambda S: feathers(S, 961, (0.85, 0.83, 0.78), (0.99, 0.98, 0.96)), 1024, 512, dict(metallic=0.0, smoothness=1.0, cutoff=0.4)),
    'FE_Remige':   (lambda S: feathers(S, 962, (0.30, 0.19, 0.10), (0.55, 0.40, 0.24), barred=True), 1024, 512, dict(metallic=0.0, smoothness=1.0, cutoff=0.4)),
    'FE_Bec':      (lambda S: KM.bone(S, seed=963, tone=(0.92, 0.70, 0.18)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
}
RECIPES.update({k: v for k, v in CH.RECIPES.items() if k in ('CH_Cuir', 'CH_CuirN', 'CH_Tapis', 'CH_Metal', 'CH_Laiton')})


def _eye(S, iris):
    yy, xx = np.mgrid[0:S, 0:S] / S; r = np.hypot(xx - 0.5, (yy - 0.5) * 0.9)
    pupil = (np.abs(xx - 0.5) < 0.07) & (np.abs(yy - 0.5) < 0.30)
    col = np.where(r < 0.42, 1.0, 0.0)[..., None] * np.array(iris)[None, None] * (0.7 + 0.3 * (1 - r / 0.42))[..., None]
    col = np.where(pupil[..., None], 0.02, col)
    return TexSet(np.clip(col, 0, 1), np.dstack([np.full((S, S), .5), np.full((S, S), .5), np.ones((S, S))]), np.zeros((S, S)), np.full((S, S), 0.95), np.ones((S, S)), np.clip(col * 0.6, 0, 1))


NAMES = {'tigre': 'TigreDesCendres', 'panthere': 'PanthereDOmbre', 'griffon': 'GriffonRoyal'}


def build(kind, bank=None):
    eagle = kind == 'griffon'
    sk = skeleton(wings=eagle); a = Asset(NAMES[kind], sk); rng = np.random.default_rng(77)
    sw = {}
    for i, n in enumerate(sk.names):
        if n.endswith('_L'): j = sk.names.index(n[:-2] + '_R'); sw[i] = j; sw[j] = i
    m = body(sk, slim=0.90 if kind == 'panthere' else 1.0, eagle=eagle)
    skin = {'tigre': 'FE_Tigre', 'panthere': 'FE_Panthere', 'griffon': 'FE_Lion'}[kind]
    if kind == 'griffon':
        C = m.V[m.F].mean(1)
        head = (C[:, 2] > 0.62) & (C[:, 1] > 0.80)
        a.add('corps', CH.subset(m, ~head), skin); a.add('tete', CH.subset(m, head), 'FE_Blanc')
    elif kind == 'tigre':
        C = m.V[m.F].mean(1)
        face = (C[:, 2] > 0.86) & ((C[:, 1] < 1.00) | ((np.abs(C[:, 0]) > 0.04) & (np.abs(C[:, 0]) < 0.09) & (C[:, 1] > 1.10) & (C[:, 2] > 0.93)))
        a.add('corps', CH.subset(m, ~face), skin); a.add('face', CH.subset(m, face), 'FE_Blanc')
    else:
        a.add('corps', m, skin)
    on, tree = CH._on_skin(m)
    CH.SKIN = on
    from forge.geo import vertex_normals
    Nm = vertex_normals(m)
    # ---- yeux
    eye_mat = 'FE_OeilV' if kind == 'panthere' else 'FE_Oeil'
    for sx in (1, -1):
        q, n_ = on(np.array([0.085 * sx, 1.10, 0.97]) if not eagle else np.array([0.085 * sx, 1.11, 0.93]), 0.010)
        fw = unit(n_ + (0, 0, 0.8))
        e = SC.Sculpt(k=0.004).ell('Head', q, (0.030, 0.024, 0.026), fwd=fw).mesh(res=0.003, smooth=2)
        ax = unit(np.cross((0, 1, 0), fw)); ay = np.cross(fw, ax)
        e.UV = np.c_[0.5 + (e.V - q) @ ax / 0.06, 0.5 + (e.V - q) @ ay / 0.05]
        rigid(e, sk['Head']); a.add(f'oeil{sx}', e, eye_mat)
    if not eagle:
        # truffe, moustaches, collerette de joues (poils longs), griffes rétractiles à peine visibles
        q, n_ = on(np.array((0, 1.03, 1.05)), 0.004)
        nose = SC.Sculpt(k=0.005).ell('Head', q, (0.032, 0.020, 0.018)).mesh(res=0.003, smooth=2); rigid(nose, sk['Head'])
        a.add('truffe', nose, 'FE_Truffe' if kind == 'tigre' else 'FE_TruffeN')
        wh = []
        for sx in (1, -1):
            for k in range(7):
                base = np.array((0.055 * sx, 0.985 - k * 0.006, 1.01 - k * 0.004)); d = np.array((sx * 0.9, -0.05 - 0.02 * k, -0.25))
                wh.append(Loft(spline([base, base + d * 0.08 + (0, 0.004, 0), base + d * 0.17 + (0, -0.006, 0)], 6), curve([(0, .0016), (1, .0004)], 6), curve([(0, .0016), (1, .0004)], 6), p=2.0, segs=4).mesh(0.05))
        wm = concat(wh); rigid(wm, sk['Head']); a.add('moustaches', wm, 'FE_Griffe')
    else:
        # ---- bec crochu doré, plumes blanches de la tête et du cou, ailes repliées
        b0 = np.array((0, 1.08, 0.95))
        beak = Loft(spline([b0, b0 + (0, 0.005, 0.07), b0 + (0, -0.02, 0.13), b0 + (0, -0.075, 0.155)], 16),
                    curve([(0, .055), (.5, .040), (.85, .020), (1, .004)], 16), curve([(0, .042), (.5, .032), (.85, .016), (1, .004)], 16), p=2.2, segs=16, cap1=1).mesh(0.05)
        rigid(beak, sk['Head']); a.add('bec', beak, 'FE_Bec')
        lower = Loft(spline([b0 + (0, -0.06, 0.0), b0 + (0, -0.065, 0.08)], 8), curve([(0, .03), (1, .008)], 8), curve([(0, .03), (1, .01)], 8), p=2.2, segs=12, cap1=1).mesh(0.05)
        rigid(lower, sk['Jaw']); a.add('bec_inf', lower, 'FE_Bec')
        def w_head(C):
            return (C[:, 2] > 0.52) * (C[:, 1] > 0.74) * 1.0
        hflow = lambda P_, N_: unit(np.c_[np.sign(P_[:, 0]) * 0.15, np.full(len(P_), -0.35), np.full(len(P_), -1.0)])
        hf = CC.fur_cards(m, 3200, rng, hflow, lambda P_, N_: np.where(P_[:, 2] > 0.80, 0.045, 0.09), width=0.03, lift=0.15, weight=w_head, curl=0.15, jitter=0.15)
        a.add('plumes', hf, 'FE_Plume')
        # griffes d'aigle aux antérieurs
        for sx in (1, -1):
            c = np.array((0.16 * sx, 0.05, 0.44)); tl = []
            for dx in (-0.04, -0.013, 0.013, 0.04):
                bb = c + (dx, 0.0, 0.08)
                tl.append(Loft(spline([bb, bb + (0, -0.01, 0.03), bb + (0, -0.045, 0.04)], 8), curve([(0, .011), (1, .002)], 8), curve([(0, .010), (1, .002)], 8), p=2.0, segs=8, cap1=1).mesh(0.05))
            tm = concat(tl); rigid(tm, sk['FrontPaw_L' if sx > 0 else 'FrontPaw_R']); a.add(f'serres{sx}', tm, 'FE_Griffe')
        # ailes : bras d'aile sculpté, couvertures et rémiges en cartes de plumes, repliées le long des flancs
        for sx in (1, -1):
            sfx = 'L' if sx > 0 else 'R'
            w1, w2, w3 = (np.array(sk.pos[sk[f'Wing{i}_{sfx}']]) for i in (1, 2, 3))
            arm = SC.Sculpt(k=0.012)
            arm.cone(f'Wing1_{sfx}', w1, w2, 0.055, 0.042).cone(f'Wing2_{sfx}', w2, w3, 0.042, 0.022)
            arm.ell(f'Wing2_{sfx}', (w2 + w3) / 2 + (0, -0.12, 0), (0.03, 0.17, 0.28), fwd=unit(w3 - w2))
            am = arm.mesh(res=0.012, smooth=3); arm.skin(am, sk, kw=0.03); SC.box_uv(am, 0.3)
            if am.J.shape[1] < 4:                                    # deux os seulement : on complète à quatre influences
                k_ = 4 - am.J.shape[1]; am.J = np.c_[am.J, np.repeat(am.J[:, :1], k_, 1)]; am.W = np.c_[am.W, np.zeros((len(am.W), k_))]
            a.add(f'aile_{sfx}', am, 'FE_Remige')
            fl = lambda P_, N_: unit(np.c_[np.zeros(len(P_)), np.full(len(P_), -0.45), np.full(len(P_), -1.0)])
            cov = CC.fur_cards(am, 700, rng, fl, lambda P_, N_: np.full(len(P_), 0.16), width=0.07, lift=0.06, curl=0.05, jitter=0.10, double=True)
            a.add(f'couvertures_{sfx}', cov, 'FE_Remige')
            prim = CC.fur_cards(am, 260, rng, fl, lambda P_, N_: np.full(len(P_), 0.42), width=0.09, lift=0.04, curl=0.03, jitter=0.06, double=True,
                                weight=lambda C: (C[:, 1] < 1.02) * 1.0)
            a.add(f'remiges_{sfx}', prim, 'FE_Remige')
        # houppe au bout de la queue (lion)
        tf = CC.fur_cards(m, 220, rng, lambda P_, N_: unit(np.c_[np.zeros(len(P_)), np.full(len(P_), -0.6), np.full(len(P_), -0.8)]), lambda P_, N_: np.full(len(P_), 0.12),
                          width=0.04, lift=0.3, weight=lambda C: (C[:, 2] < -1.75) * 1.0, curl=0.3, jitter=0.3)
        a.add('houppe', tf, 'FE_Remige')
    # ---- tapis et selle (coques taillées dans la peau du dos)
    def shell(sel, off, uvfn):
        keepv = np.unique(m.F[sel]); remap = -np.ones(len(m.V), np.int64); remap[keepv] = np.arange(len(keepv))
        out = Mesh(m.V[keepv] + Nm[keepv] * off, remap[m.F[sel]], uvfn(m.V[keepv])); out.J, out.W = m.J[keepv].copy(), m.W[keepv].copy(); return out
    Cf = m.V[m.F].mean(1)
    tap = (np.abs(Cf[:, 2]) < 0.30) & (Cf[:, 1] > 0.72)
    uvb = lambda V: np.c_[(V[:, 2] + 0.30) / 0.60, 0.5 + 0.5 * np.sign(V[:, 0]) * np.clip((1.04 - V[:, 1]) / 0.32, 0, 1)]
    t1 = shell(tap, 0.010, uvb); t2 = shell(tap, 0.004, uvb); t2.F = t2.F[:, ::-1]
    a.add('tapis', concat([t1, t2]), 'CH_Tapis')
    st_ = (np.abs(Cf[:, 2]) < 0.20) & (Cf[:, 1] > 0.93) & (np.abs(Cf[:, 0]) < 0.15)
    a.add('selle', shell(st_, 0.038, lambda V: np.c_[V[:, 2] * 3, V[:, 0] * 3]), 'CH_Cuir')
    for zc, rise, r_ in ((0.18, 0.012, 0.030), (-0.18, 0.025, 0.034)):
        arc = []
        for xx in np.linspace(-0.17, 0.17, 13):
            q, nn = on(np.array((xx, 1.2, zc))); arc.append(q + nn * 0.038 + np.array((0, rise * (1 - (xx / 0.17) ** 2), 0)))
        roll = Loft(spline(arc, 26), curve([(0, r_ * 0.6), (.5, r_), (1, r_ * 0.6)], 26), curve([(0, r_ * 0.6), (.5, r_ * 0.9), (1, r_ * 0.6)], 26), p=2.2, segs=12, cap0=1, cap1=1).mesh(0.1)
        _, ii = tree.query(roll.V); roll.J, roll.W = m.J[ii].copy(), m.W[ii].copy(); a.add(f'arcon{zc}', roll, 'CH_Cuir')
    girth = CH.strap([(0.22, 1.00, 0.10), (0.24, 0.80, 0.10), (0.15, 0.56, 0.10), (0, 0.50, 0.10), (-0.15, 0.56, 0.10), (-0.24, 0.80, 0.10), (-0.22, 1.00, 0.10)], 0.04, 0.006, 'Spine2', sk, hug=0.008)
    _, ii = tree.query(girth.V); girth.J, girth.W = m.J[ii].copy(), m.W[ii].copy(); a.add('sangle', girth, 'CH_CuirN')
    for sx in (1, -1):
        p0 = np.array((sx * 0.21, 1.08, 0.05)); p1 = np.array((sx * 0.27, 0.70, 0.05))
        a.add(f'etriviere{sx}', CH.strap([p0, (p0 + p1) / 2 + (sx * 0.02, 0, 0), p1], 0.016, 0.004, 'Spine2', sk), 'CH_CuirN')
        st = Loft(spline([p1 + (0, 0, -0.045), p1 + (0, -0.08, -0.045), p1 + (0, -0.10, 0), p1 + (0, -0.08, 0.045), p1 + (0, 0, 0.045)], 14), curve([(0, .007), (1, .007)], 14), curve([(0, .007), (1, .007)], 14), p=2.0, segs=8).mesh(0.05)
        rigid(st, sk['Spine2']); a.add(f'etrier{sx}', st, 'CH_Metal')
    # ---- animations : celles du loup (dos souple, galop rotatoire), queue de félin plus ample ; ailes du griffon
    QA.build(sk, a, kind='wolf')
    for c in a.clips.values():
        if 'Tail1' in c.rot:
            n = c.n; t = np.arange(n) / c.fps
            for i in range(1, 5): c.rot[f'Tail{i}'] = np.array([qeuler(-6 + 4 * np.sin(2 * np.pi * x / 3 + i), 10 * np.sin(2 * np.pi * x / 2.5 + i * 0.7), 0) for x in t])
    if eagle:
        for c in a.clips.values():
            if c.name not in ('Idle', 'Walk', 'Run', 'Attack', 'Howl'): continue
            amp = {'Idle': 3, 'Walk': 6, 'Run': 30, 'Attack': 60, 'Howl': 75}[c.name]
            T = max(c.duration, 1e-3); beats = 2 if c.name == 'Run' else 1; t = np.arange(c.n) / c.fps
            for side, sg in (('L', 1), ('R', -1)):
                for i, mul in ((1, 1.0), (2, 0.5), (3, 0.3)):
                    ang = amp * mul * (0.5 + 0.5 * np.sin(2 * np.pi * t / T * beats))
                    c.rot[f'Wing{i}_{side}'] = np.array([qeuler(0, 0, -sg * x) for x in ang])
    return a
