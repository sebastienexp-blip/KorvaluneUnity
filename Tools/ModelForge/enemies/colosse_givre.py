"""Colosse de givre — géant de la Toundra de Givrecœur : même anatomie massive que le démon brute (musculature
sculptée), peau épaisse bleu-gris couverte de croûtes de givre, fines fissures d'un bleu glacier ; cornes de bélier
gainées de glace ; amas de cristaux de glace qui percent les épaules, le haut du dos et les avant-bras ; manteau de
fourrure grossièrement cousu sur les épaules, pagne de peau et ceinture de fer ; masse de pierre prise dans un bloc
de glace, cerclée de fer, manche de bois enroulé de cuir."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, sculpt as SC
from forge.geo import Loft, spline, curve, unit, concat, rigid, mirror
from knight import anims, materials as KM
from enemies import materials as EM, demon_brute as DB

NAME = 'ColosseGivre'
FOLDER = 'Enemies'
SCALE = 1.5
MAX_TRIS = 180000
HEAD_VIEW = (0, 1.62, 0.10)
RECIPES = {
    'CG_Skin':    (lambda S: EM.frost_hide(S, seed=1301), 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(1.2, 1.2, 1.2), emission_preview=(0.6, 0.6, 0.6))),
    'CG_Horn':    (lambda S: EM.tinted(KM.bone(S, seed=1302, tone=(0.55, 0.60, 0.66)), (1, 1, 1)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'CG_Claw':    (lambda S: KM.bone(S, seed=1303, tone=(0.20, 0.22, 0.26)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'CG_Leather': (lambda S: KM.leather(S, seed=1304, tone=(0.16, 0.12, 0.09)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'CG_Iron':    (lambda S: KM.steel(S, seed=1305, tone=(0.22, 0.24, 0.27), polish=0.4, rust_amt=1.2, grime_amt=1.2), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'CG_Eye':     (lambda S: EM.glow(S, (0.45, 0.85, 1.0)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.5, 2.5, 2.5))),
    'CG_Tooth':   (lambda S: KM.bone(S, seed=1306, tone=(0.70, 0.72, 0.70)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'CG_Glace':   (lambda S: KM.crystal(S, seed=1307, color=(0.55, 0.85, 1.0), glow=0.45), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(0.9, 0.9, 0.9), emission_preview=(0.5, 0.5, 0.5))),
    'CG_Fur':     (lambda W, H_: KM.cloak(W, H_, seed=1308, color=(0.42, 0.38, 0.32), thread_color=(0.55, 0.50, 0.42), mud_amt=0.6), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'CG_Stone':   (lambda S: EM.tinted(EM.lava_rock(S, seed=1309, tone=(0.30, 0.31, 0.33), glow_amt=0.0), (1, 1, 1), emit=None), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'CG_Wood':    (lambda S: EM.tinted(KM.wood(S, seed=1310), (0.55, 0.50, 0.45)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'MasseGivre')]
REMAP = {'DB_Leather': 'CG_Leather', 'DB_Iron': 'CG_Iron'}


def pad4(m):
    """Complète les poids à quatre influences (les pièces sculptées sur un seul os n'en ont qu'une)."""
    if m.J.shape[1] < 4:
        k = 4 - m.J.shape[1]; m.J = np.c_[m.J, np.repeat(m.J[:, :1], k, 1)]; m.W = np.c_[m.W, np.zeros((len(m.W), k))]
    return m


def crystal(c, d, h, r):
    """Prisme de glace à six pans, pointu, légèrement de travers."""
    d = unit(np.asarray(d, float))
    return Loft(spline([c - d * 0.01, c + d * h * 0.75, c + d * h], 6), curve([(0, r), (.75, r * 0.85), (1, r * 0.05)], 6), curve([(0, r), (.75, r * 0.85), (1, r * 0.05)], 6),
                p=1.0, segs=6, cap0=1, cap1=1, ref=unit(np.cross(d, (0.3, 0.1, 1.0)))).mesh(0.05)


def ice_growths(sk, rng):
    """Amas de cristaux qui percent la peau : épaules, haut du dos, avant-bras (gauche ; la droite par symétrie)."""
    sites = [('LeftShoulder', (0.22, 1.56, -0.03), (0.6, 1.0, -0.2), 7, 0.16), ('UpperChest', (0.08, 1.52, -0.13), (0.25, 0.8, -0.8), 6, 0.14),
             ('LeftLowerArm', (0.55, 1.50, -0.04), (0.1, 1.0, -0.5), 4, 0.09)]
    out = {}
    for bone, c, d, n, h in sites:
        parts = []
        for k in range(n):
            jit = rng.normal(0, 0.03, 3); dd = np.asarray(d) + rng.normal(0, 0.35, 3)
            parts.append(crystal(np.asarray(c) + jit, dd, h * rng.uniform(0.5, 1.2), h * rng.uniform(0.12, 0.2)))
        m = concat(parts); rigid(m, sk[bone]); out[bone] = m
    return out


def fur_mantle(sk):
    """Manteau de fourrure jeté sur les épaules : coque épaisse en mèches (bord découpé en pointes), noué devant."""
    s = SC.Sculpt(k=0.03)
    s.ell('UpperChest', (0, 1.50, -0.04), (0.30, 0.10, 0.20), k=0.04)
    for sx in (1, -1): s.ell('UpperChest', (0.22 * sx, 1.52, -0.02), (0.13, 0.08, 0.14))
    s.ell('UpperChest', (0, 1.40, -0.15), (0.24, 0.16, 0.08))
    m = s.mesh(res=0.014, smooth=3)
    # mèches : le bord inférieur est découpé en pointes, la surface ondule
    y = m.V[:, 1]; ang = np.arctan2(m.V[:, 0], m.V[:, 2])
    m.V[:, 1] -= 0.035 * np.clip((1.47 - y) / 0.1, 0, 1) * (np.sin(ang * 14) * 0.5 + 0.5)
    m.V += 0.006 * np.c_[np.sin(m.V[:, 1] * 120), np.zeros(len(y)), np.cos(m.V[:, 0] * 110)]
    s.skin(m, sk, kw=0.04); SC.box_uv(m, 0.5)
    return pad4(m)


def maul(name='MasseGivre'):
    """Masse de givre : bloc de pierre brute pris dans une gangue de glace hérissée de cristaux, deux cercles de fer,
    long manche de bois noueux enroulé de cuir. Prise à l'origine, tête vers +Y."""
    a = Asset(name)
    t = np.linspace(0, 1, 30)
    pts = np.c_[0.006 * np.sin(t * 6), -0.35 + t * 1.25, np.zeros_like(t)]
    r = 0.030 + 0.005 * np.sin(t * 23) ** 2 + 0.008 * t
    a.add('manche', Loft(pts, r, r * 0.92, p=2.2, segs=12, cap0=1).mesh(0.2), 'CG_Wood')
    a.add('cuir', Loft(spline([(0, -0.30, 0), (0, 0.15, 0)], 10), 0.036, 0.036, p=2.2, segs=12).mesh(0.1), 'CG_Leather')
    head = SC.Sculpt(k=0.02).ell('H', (0, 0.98, 0), (0.17, 0.14, 0.15)).ell('H', (0.05, 1.03, 0.04), (0.10, 0.09, 0.10)).mesh(res=0.012, smooth=2)
    head.V += 0.012 * np.c_[np.sin(head.V[:, 1] * 60), np.cos(head.V[:, 2] * 55), np.sin(head.V[:, 0] * 50)]
    SC.box_uv(head, 0.3); a.add('pierre', head, 'CG_Stone')
    rng = np.random.default_rng(1311)
    ice = [crystal(np.array([0, 0.98, 0]) + unit(rng.normal(0, 1, 3)) * 0.12, unit(rng.normal(0, 1, 3)) + (0, 0.3, 0), rng.uniform(0.10, 0.22), rng.uniform(0.025, 0.04)) for _ in range(9)]
    a.add('glace', concat(ice), 'CG_Glace')
    for y in (0.88, 1.08):
        a.add(f'cercle{y}', Loft(np.array([[0.165 * np.cos(q), y, 0.155 * np.sin(q)] for q in np.linspace(0, 2 * np.pi, 40)]), 0.018, 0.008, p=4, segs=6, ref=(0, 1, 0)).mesh(0.1), 'CG_Iron')
    return a


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk); rng = np.random.default_rng(1300)
    m = DB.body(sk)
    a.add('corps', m, 'CG_Skin')
    a.add('cornes', DB.horns(sk), 'CG_Horn')
    e, tt, claws, toes = DB.face_details(sk, m)
    a.add('yeux', e, 'CG_Eye'); a.add('crocs', tt, 'CG_Tooth')
    for k, c in enumerate(claws): a.add(f'griffe{k}', c, 'CG_Claw'); a.add(f'griffe{k}_R', mirror(c, sw), 'CG_Claw')
    a.add('griffes_pied', toes, 'CG_Claw'); a.add('griffes_pied_R', mirror(toes, sw), 'CG_Claw')
    for bone, gm in ice_growths(sk, rng).items():
        a.add(f'glace_{bone}', gm, 'CG_Glace')
        if bone.startswith('Left'): a.add(f'glace_{bone}_R', mirror(gm, sw), 'CG_Glace')
    a.add('manteau', fur_mantle(sk), 'CG_Fur')
    for n, mm, mat in DB.loincloth(sk): a.add(n, mm, REMAP.get(mat, mat))
    anims.build(sk, a)
    return a, {'MasseGivre': maul()}, anims.CLIPS
