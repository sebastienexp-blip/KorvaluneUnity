"""Goule des Marais — charognard du Marais de Brumenoire : corps décharné aux côtes saillantes, peau grise et
verdâtre maculée de vase, veines noires, yeux jaunes luisants, crâne chauve ; longues griffes noires recourbées au
bout de chaque doigt ; lambeaux de toile pourrie et de cuir autour des hanches, ceinture de corde ; touffes de mousse
et de lichen accrochées aux épaules, au dos et aux bras, lanières de vase qui dégoulinent."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, humanparts as HP, armorparts as AP
from forge.geo import Loft, spline, curve, unit, concat, rigid, mirror, smoothstep, vertex_normals
from knight import anims, materials as KM
from heroes import modular as MOD
from enemies import materials as EM
from enemies import noye as NO

NAME = 'GouleMarais'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.64, 0.05)
RECIPES = {
    'GM_Claw':    (lambda S: KM.bone(S, seed=1201, tone=(0.06, 0.055, 0.045)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'GM_Cloth':   (lambda W, H_: KM.cloak(W, H_, seed=1202, color=(0.12, 0.11, 0.07), thread_color=(0.20, 0.18, 0.10), mud_amt=3.2), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'GM_Leather': (lambda S: KM.leather(S, seed=1203, tone=(0.07, 0.05, 0.03)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'GM_Corde':   (lambda S: KM.gambeson(S, seed=1204, color=(0.24, 0.21, 0.14)), 512, 128, dict(metallic=0.0, smoothness=1.0)),
    'GM_Mousse':  (lambda W, H_: EM.seaweed(W, H_, seed=1205, color=(0.10, 0.15, 0.05)), (512, 1024), (128, 256), dict(metallic=0.0, smoothness=1.0)),
    'GM_Lichen':  (lambda S: EM.tinted(EM.barnacle(S, seed=1206), (0.45, 0.62, 0.32)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = []


def claws(sk):
    """Griffes longues et recourbées au bout des doigts (main gauche ; la droite par symétrie)."""
    out = []
    for fn, pts in H.FINGERS.items():
        p = [np.array(q) for q in pts]
        tip = p[3] + (p[3] - p[2]) * 0.6; d = unit(p[3] - p[2])
        c = Loft(spline([tip - d * 0.012, tip + d * 0.035 + (0, -0.012, 0), tip + d * 0.060 + (0, -0.045, 0)], 10),
                 curve([(0, .0075), (1, .0008)], 10), curve([(0, .0060), (1, .0007)], 10), p=2, segs=8, cap1=1).mesh(0.03)
        rigid(c, sk[f'Left{fn}Distal']); out.append(c)
    return out


def rags(body, sk, a):
    """Pans de toile pourrie et de cuir effilochés autour des hanches (comme le squelette, en plus long et plus déchiré)."""
    V = body.V; ax = np.abs(V[:, 0])
    legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
    env = AP.hip_envelope(body, (legs | HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)) & (ax < 0.30))
    pans = [(0.0, 0.52, 0.10, 1), (0.6, 0.44, 0.08, 2), (-0.6, 0.47, 0.085, 3), (1.25, 0.36, 0.07, 4), (-1.3, 0.38, 0.07, 5),
            (np.pi, 0.50, 0.10, 6), (np.pi - 0.75, 0.42, 0.08, 7), (np.pi + 0.75, 0.45, 0.08, 8)]
    for th, L, wd, sd in pans:
        m, edge = AP.lappet(env, th, 0.98, L, wd, off=0.022, flare=0.07, seed=120 + sd, notch=1.0)
        for mm, mat in ((m, 'GM_Cloth' if sd % 3 else 'GM_Leather'), (edge, 'GM_Leather')):
            yv = mm.V[:, 1]; f = smoothstep((0.90 - yv) / 0.35) * 0.75
            wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
            mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
            mm.J[:, 0] = sk['Hips']; mm.W[:, 0] = 1 - f
            mm.J[:, 1] = sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
            a.add(f'haillon{sd}' + ('' if mm is m else '_bord'), mm, mat)
    # ceinture de corde nouée
    pts = np.array([[0.175 * np.sin(t), 1.00, 0.125 * np.cos(t) + 0.005] for t in np.linspace(0, 2 * np.pi, 48)])
    a.add('ceinture_corde', HP.weights_from(body, Loft(pts, 0.008, 0.008, p=2, segs=8).mesh(0.1)), 'GM_Corde')


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk); rng = np.random.default_rng(1200)
    body, marks = MOD.base(sk, a, bank, 'GM', female=False, look=dict(
        extra=[('universal-male-young-minmuscle-minweight', 1.0), ('stomach-pregnant-decr', 1.0), ('stomach-tone-incr', 0.4)],
        skin='middleage_caucasian_male', skin_tint=(0.42, 0.47, 0.33), hair=None,
        brows='eyebrow001', eye_color=(1.0, 0.85, 0.15), glow=1.4,
        skin_kw=dict(pale=0.0, makeup=(0.06, 0.07, 0.03), veins=(0.06, 0.08, 0.05), dirt=2.6, pubic=0.0)))
    a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'SousVetementHaut', 'HaillonsHaut', 'HaillonsBas')]
    for k, c in enumerate(claws(sk)):
        a.add(f'griffe{k}', c, 'GM_Claw'); a.add(f'griffe{k}_R', mirror(c, sw), 'GM_Claw')
    rags(body, sk, a)
    # mousse et lichen : touffes plates sur les épaules et le dos, lanières de vase qui pendent
    zones = [((0.18, 1.46, -0.03), 0.08, 10), ((-0.18, 1.46, -0.03), 0.08, 10), ((0.0, 1.30, -0.12), 0.10, 12)]
    a.add('lichen', NO.barnacles(body, sk, rng, zones), 'GM_Lichen')
    anchors = [(0.16 * sx, 1.47, -0.06 + 0.04 * k) for sx in (1, -1) for k in range(3)] + [(0.05 * k, 1.35, -0.13) for k in (-2, -1, 0, 1, 2)]
    a.add('mousse', NO.strands(body, sk, rng, anchors, 0.22, 0.020), 'GM_Mousse')
    MOD.assign(a, [])
    anims.build(sk, a)
    return a, {}, anims.CLIPS
