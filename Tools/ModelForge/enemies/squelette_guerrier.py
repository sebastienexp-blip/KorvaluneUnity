"""Squelette guerrier — mort-vivant : squelette humain complet (os patinés, crâne aux orbites vides et aux dents
découvertes) sous les restes d'une armure rouillée — grande épaulière à lames, cubitières et canons d'avant-bras,
genouillères et grèves, ceinture de faulds — et des lambeaux de cuir et de drap pourri autour des hanches ;
épée longue ébréchée et écu rouillé."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, fitloft as FL, humanparts as HP, armorparts as AP
from forge.geo import smoothstep, mirror
from knight import armor, anims, weapons, materials as KM
from enemies import materials as EM, bones as BN
from enemies.common import remap, template_body

NAME = 'SqueletteGuerrier'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.66, 0.06)
RECIPES = {
    'SK_Bone':      (lambda S: KM.bone(S, seed=601, tone=(0.66, 0.60, 0.48)), 1024, 512, dict(metallic=0.0, smoothness=0.6)),
    'SK_Cartilage': (lambda S: KM.bone(S, seed=602, tone=(0.52, 0.44, 0.33)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'SK_Dark':      (lambda S: KM.void(S), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    'SK_Rust':      (lambda S: KM.steel(S, seed=603, tone=(0.26, 0.24, 0.22), polish=0.45, rust_amt=3.0, grime_amt=1.6, scratch_amt=1.4), 2048, 512, dict(metallic=1.0, smoothness=1.0)),
    'SK_Bronze':    (lambda S: KM.brass(S, seed=604, gold=(0.48, 0.34, 0.16), dark=(0.08, 0.05, 0.02)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'SK_Leather':   (lambda S: KM.leather(S, seed=605, tone=(0.12, 0.07, 0.04)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'SK_Cloth':     (lambda W, H_: KM.cloak(W, H_, seed=606, color=(0.10, 0.065, 0.040), thread_color=(0.30, 0.22, 0.10), mud_amt=2.0), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'SK_Blade':     (lambda S: KM.steel(S, seed=607, tone=(0.40, 0.38, 0.35), polish=0.5, rust_amt=2.2, grime_amt=1.2), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':      (lambda S: EM.tinted(KM.wood(S), (0.6, 0.55, 0.5)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'RustedSword'), ('Socket_Shield_L', 'RustedShield')]
REMAP = {'KN_Steel': 'SK_Rust', 'KN_Brass': 'SK_Bronze', 'KN_Leather': 'SK_Leather', 'KN_Mail': 'SK_Rust', 'KN_Gambeson': 'SK_Cloth',
         'KN_Void': 'SK_Dark', 'KN_Cloak': 'SK_Cloth', 'WP_Blade': 'SK_Blade'}
SKIP = ('arete', 'boucle_', 'casque', 'chausse', 'col', 'cuissot', 'culet', 'doigt_', 'dossiere', 'gambison', 'gant', 'gorgerin', 'jupe_maille',
        'manche_maille', 'pansiere', 'paume', 'plastron', 'sangle_', 'soleret', 'soulier', 'talonniere', 'greve', 'genou', 'tassette')


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk)
    body, marks = template_body(sk, a, bank)
    # dents MakeHuman conservées (le crâne découvre la mâchoire)
    tmp = Asset('t', sk); _b, _m = template_body(sk, tmp, bank, keep=True)
    for n, m, mat in tmp.parts:
        if n in ('dents',): a.add('dents', m, mat if mat in bank.recipes else 'SK_Bone')
    a.parts = [(n, m, 'SK_Bone') if n == 'dents' else (n, m, mat) for n, m, mat in a.parts]
    T = BN.Torso(body, sk)
    s, hm = BN.skull(body, sk, marks)
    a.add('crane', s, 'SK_Bone'); a.add('crane_creux', hm, 'SK_Dark')
    sp, ca = BN.spine_and_ribs(body, sk, T)
    a.add('colonne_cotes', sp, 'SK_Bone'); a.add('cartilages', ca, 'SK_Cartilage')
    for k, (m, b) in enumerate(BN.girdles(body, sk, T)):
        a.add(f'os{k}_{b}', m, 'SK_Bone')
    # restes d'armure rouillée
    with FL.fitted(body, sk):
        armor.build(sk, a, remap=REMAP, helmet=False, skip=SKIP)
    # une seule grande épaulière (bras de l'épée), l'autre épaule nue
    a.parts = [p for p in a.parts if not (p[0].startswith('epaule') and not p[0].endswith('_R'))]
    # ceinture de cuir et lambeaux autour des hanches
    V = body.V; ax = np.abs(V[:, 0])
    legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
    env = AP.hip_envelope(body, (legs | HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)) & (ax < 0.30))
    pans = [(0.0, 0.46, 0.085, 1), (0.55, 0.40, 0.070, 2), (-0.55, 0.42, 0.075, 3), (1.20, 0.34, 0.065, 4), (-1.25, 0.36, 0.065, 5),
            (np.pi, 0.44, 0.090, 6), (np.pi - 0.7, 0.38, 0.070, 7), (np.pi + 0.7, 0.40, 0.075, 8)]
    for th, L, wd, sd in pans:
        m, edge = AP.lappet(env, th, 0.97, L, wd, off=0.030, flare=0.06, seed=60 + sd, notch=0.7)
        for mm, mat in ((m, 'SK_Cloth' if sd % 2 else 'SK_Leather'), (edge, 'SK_Leather')):
            yv = mm.V[:, 1]; f = smoothstep((0.90 - yv) / 0.35) * 0.75
            wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
            mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
            mm.J[:, 0] = sk['Hips']; mm.W[:, 0] = 1 - f
            mm.J[:, 1] = sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
            a.add(f'lambeau{sd}' + ('' if mat != 'SK_Leather' or mm is m else '_bord'), mm, mat)
    # baudrier de cuir en travers des côtes
    a.add('baudrier', HP.weights_from(body, HP.strap(body, [(0.15, 1.46, 0.07), (0.05, 1.30, 0.16), (-0.08, 1.12, 0.15), (-0.16, 0.99, 0.05)], 0.010, 0.050, thick=0.004)), 'SK_Leather')
    anims.build(sk, a)
    gear = {'RustedSword': remap(weapons.sword(), REMAP, 'RustedSword'), 'RustedShield': remap(weapons.shield(W=0.28, Hh=0.62), REMAP, 'RustedShield')}
    return a, gear, anims.CLIPS
