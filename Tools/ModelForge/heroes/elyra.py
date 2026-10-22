"""Elyra Noctis — l'Archimage du Néant : corset bleu nuit à filigrane d'or au décolleté plongeant, col montant, épaulières
à lames, manches sombres, brassards et cuissardes à genouillères d'or, longs pans de jupe ouverts devant, cape ;
bâton à couronne d'or et cristal violet luminescent. Corps de base féminin : chaque pièce est détachable."""
from forge.rig import Asset
from forge import humanoid as H
from knight import anims, materials as KM
from heroes import modular as MOD, female_kit as FK

NAME = 'ElyraNoctis'
HEAD_VIEW = (0, 1.66, 0.05)
RECIPES = {
    'EL_Corset':  (lambda S: KM.tooled_leather(S, seed=201, tone=(0.035, 0.040, 0.075), line=(0.70, 0.52, 0.22), n=40), 1024, 512, dict(metallic=0.6, smoothness=1.0)),
    'EL_Sleeve':  (lambda S: KM.gambeson(S, seed=202, color=(0.035, 0.035, 0.06)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'EL_Cloth':   (lambda W, H_: KM.cloak(W, H_, seed=203, color=(0.040, 0.045, 0.09), thread_color=(0.72, 0.55, 0.22), mud_amt=0.3), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'EL_Cloak':   (lambda W, H_: KM.cloak(W, H_, seed=204, color=(0.025, 0.025, 0.045), thread_color=(0.55, 0.30, 0.75), mud_amt=0.4), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'EL_Gold':    (lambda S: KM.brass(S, seed=205, gold=(0.78, 0.58, 0.28)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'EL_Metal':   (lambda S: KM.steel(S, seed=206, tone=(0.10, 0.10, 0.14), polish=0.7, rust_amt=0.1), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'EL_Leather': (lambda S: KM.leather(S, seed=207, tone=(0.035, 0.032, 0.040)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'EL_Crystal': (lambda S: KM.crystal(S, seed=208, color=(0.62, 0.22, 1.0)), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.9, 0.4, 1.4))),
    'EL_Dark':    (lambda S: KM.leather(S, seed=209, tone=(0.02, 0.02, 0.025)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'WP_Wood':    (lambda S: KM.wood(S), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'VoidStaff')]
M = dict(corset='EL_Corset', sleeve='EL_Sleeve', cloth='EL_Cloth', cloak='EL_Cloak', gold='EL_Gold', metal='EL_Metal', leather='EL_Leather',
         crystal='EL_Crystal', dark='EL_Dark', bone='EL_Gold', wood='WP_Wood')


def build(bank, female=True):
    sk = H.build(); a = Asset(NAME + ('_F' if female else '_M'), sk)
    body, marks = MOD.base(sk, a, bank, 'EL' + ('F' if female else 'M'), female=female, look=None if not female else dict(skin='young_caucasian_female', hair='culturalibre_hair_01', brows='eyebrow010'))
    if not female: marks['breasts'] = [__import__('numpy').array([sx * 0.085, 1.33, 0.16]) for sx in (1, -1)]
    FK.build_outfit(a, body, marks, sk, 'EL', M, dict(neck=2.4 if female else 7.0, spikes=False, skulls=False, tatter=0.10, collar=True, cloak=True, cloak_len=1.12, cloak_tatter=0.4))
    MOD.assign(a, [])
    MOD.covers(a, FK.COVERS)
    anims.build(sk, a, FK.StaffStyle())
    return a, {'VoidStaff': FK.staff('VoidStaff', dict(wood='EL_Dark', gold='EL_Gold', crystal='EL_Crystal'), 'void')}, anims.CLIPS
