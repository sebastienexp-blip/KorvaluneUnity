"""Chevalier maudit — garde d'élite : armure de plates d'acier noirci veinée de fissures de braise, maille et drap
rouge sang, tabard et cape en lambeaux, armet à cornes recourbées d'où luisent deux yeux rouges, grand écu et épée.
Pas de corps : l'armure est vide, animée par la malédiction."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, fitloft as FL
from forge.geo import Loft, spline, curve, unit, rigid, mirror
from knight import armor, cape, anims, weapons, materials as KM
from heroes.female_kit import _blob
from enemies import materials as EM
from enemies.common import remap, template_body

NAME = 'ChevalierMaudit'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.70, 0.05)
RECIPES = {
    'CK_Steel':    (lambda S: EM.cursed_steel(S, seed=401, tone=(0.085, 0.080, 0.085), amount=0.45), 2048, 512, dict(metallic=1.0, smoothness=1.0, emission=(2.2, 2.2, 2.2), emission_preview=(1.0, 1.0, 1.0))),
    'CK_Trim':     (lambda S: KM.brass(S, seed=402, gold=(0.42, 0.16, 0.08), dark=(0.06, 0.02, 0.015)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'CK_Leather':  (lambda S: KM.leather(S, seed=403, tone=(0.05, 0.025, 0.02)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'CK_Mail':     (lambda S: EM.tinted(KM.chainmail(S, seed=404), (0.75, 0.16, 0.12)), 1024, 512, dict(metallic=1.0, smoothness=1.0)),
    'CK_Gambeson': (lambda S: KM.gambeson(S, seed=405, color=(0.26, 0.02, 0.02)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'CK_Cloth':    (lambda W, H_: KM.cloak(W, H_, seed=406, color=(0.34, 0.02, 0.02), thread_color=(0.55, 0.12, 0.05), mud_amt=1.0), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'CK_Void':     (lambda S: KM.void(S), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    'CK_Eye':      (lambda S: EM.glow(S, (1.0, 0.10, 0.03)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(3.0, 3.0, 3.0))),
    'CK_Blade':    (lambda S: EM.cursed_steel(S, seed=407, tone=(0.12, 0.115, 0.12), amount=0.25), 1024, 256, dict(metallic=1.0, smoothness=1.0, emission=(2.0, 2.0, 2.0), emission_preview=(1.0, 1.0, 1.0))),
    'CK_ShieldFace': (lambda S: EM.cursed_steel(S, seed=408, tone=(0.30, 0.28, 0.27), amount=0.25), 2048, 512, dict(metallic=1.0, smoothness=1.0, emission=(1.5, 1.5, 1.5), emission_preview=(0.8, 0.8, 0.8))),
    'CK_Gold':     (lambda S: KM.brass(S, seed=409, gold=(0.78, 0.50, 0.22)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':     (KM.wood, 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'CursedBlade'), ('Socket_Shield_L', 'CursedShield')]
REMAP = {'KN_Steel': 'CK_Steel', 'KN_Brass': 'CK_Trim', 'KN_Leather': 'CK_Leather', 'KN_Mail': 'CK_Mail', 'KN_Gambeson': 'CK_Gambeson',
         'KN_Void': 'CK_Void', 'KN_Cloak': 'CK_Cloth', 'WP_Blade': 'CK_Blade'}


def horns(a, sk, top):
    """Deux cornes fines qui jaillissent des tempes du heaume et se recourbent vers l'arrière, plus une lame de crête."""
    sw = H.swap_table(sk)
    r0 = 0.018
    p0 = np.array([0.080, top - 0.080, 0.000])
    pts = [p0, p0 + (0.045, 0.040, -0.020), p0 + (0.080, 0.110, -0.075), p0 + (0.070, 0.180, -0.165), p0 + (0.035, 0.205, -0.255)]
    lo = Loft(spline(pts, 36), curve([(0, r0), (.45, r0 * 0.62), (1, .0012)], 36), curve([(0, r0 * 1.2), (.45, r0 * 0.7), (1, .0012)], 36), p=2.2, segs=12, cap0=1, cap1=1)
    m = lo.mesh(0.08)
    q = np.linspace(0, 1, len(m.V)) * 0   # stries annulaires de la corne
    rigid(m, sk['Head']); a.add('casque_corne', m, 'CK_Steel'); a.add('casque_corne_R', mirror(m, sw), 'CK_Steel')
    # lame de crête effilée sur le sommet, légèrement recourbée vers l'arrière
    p0 = np.array([0, top + 0.005, 0.02])
    lo = Loft(spline([p0, p0 + (0, 0.08, -0.02), p0 + (0, 0.15, -0.07)], 20), curve([(0, .006), (1, .001)], 20), curve([(0, .030), (.3, .022), (1, .002)], 20),
              p=2.0, segs=10, ref=(1, 0, 0), cap0=1, cap1=1)
    m = lo.mesh(0.08); rigid(m, sk['Head']); a.add('casque_lame', m, 'CK_Steel')


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk)
    body, marks = template_body(sk, a, bank)
    with FL.fitted(body, sk):
        armor.build(sk, a, remap=REMAP)
    cape.build(sk, a, mat='CK_Cloth', clasp='CK_Trim', length=1.05, width=1.05, tatter=1.3, seed=9)
    cape.build_tabard(sk, a, mat='CK_Cloth', length=1.40, width=1.25, tatter=0.9, seed=13)
    top = body.V[:, 1].max()
    horns(a, sk, top + 0.035)
    # yeux : deux braises derrière la fente de vue
    fv = np.vstack([m.V for n, m, mat in a.parts if n.startswith('casque_fente') and not n.startswith('casque_fente_bord')])
    fy = 0.5 * (fv[:, 1].min() + fv[:, 1].max()); fz = np.vstack([m.V for n, m, mat in a.parts if n.startswith('casque_fente_bord')])[:, 2].max() - 0.008
    for sx in (1, -1):
        m = _blob(np.array([0.036 * sx, fy, fz]), (0.022, 0.0065, 0.004), n=14); rigid(m, sk['Head']); a.add(f'yeux_braise{sx}', m, 'CK_Eye')
    # fente de vue et grille rougeoyantes (lueur des yeux de la malédiction)
    a.parts = [(n, m, 'CK_Eye' if n.startswith('casque_fente') and mat == 'CK_Void' else mat) for n, m, mat in a.parts]
    anims.build(sk, a)
    gear = {'CursedBlade': remap(weapons.sword(), REMAP, 'CursedBlade'),
            'CursedShield': remap(weapons.shield(W=0.33, Hh=0.95), dict(REMAP, KN_Steel='CK_ShieldFace', KN_Brass='CK_Gold'), 'CursedShield')}
    return a, gear, anims.CLIPS
