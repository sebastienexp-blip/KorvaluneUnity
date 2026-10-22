"""Morvanna — la Nécromancienne : corset de cuir noir à filets d'argent, crânes à la ceinture et aux épaules, épaulières et
brassards hérissés de pointes, pans de jupe déchirés, cuissardes, cape en lambeaux ; bâton surmonté d'un crâne et d'un
cristal vert d'âme. Corps de base féminin : chaque pièce est détachable."""
from forge.rig import Asset
from forge import humanoid as H
from knight import anims, materials as KM
from heroes import modular as MOD, female_kit as FK

NAME = 'Morvanna'
HEAD_VIEW = (0, 1.66, 0.05)
RECIPES = {
    'MO_Corset':  (lambda S: KM.tooled_leather(S, seed=301, tone=(0.035, 0.035, 0.038), line=(0.45, 0.50, 0.46), n=34), 1024, 512, dict(metallic=0.5, smoothness=1.0)),
    'MO_Sleeve':  (lambda S: KM.gambeson(S, seed=302, color=(0.03, 0.04, 0.035)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'MO_Cloth':   (lambda W, H_: KM.cloak(W, H_, seed=303, color=(0.030, 0.045, 0.038), thread_color=(0.25, 0.65, 0.40), mud_amt=1.2), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'MO_Cloak':   (lambda W, H_: KM.cloak(W, H_, seed=304, color=(0.02, 0.025, 0.022), thread_color=(0.20, 0.45, 0.30), mud_amt=1.4), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'MO_Silver':  (lambda S: KM.brass(S, seed=305, gold=(0.62, 0.64, 0.62), dark=(0.08, 0.09, 0.08)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'MO_Metal':   (lambda S: KM.steel(S, seed=306, tone=(0.09, 0.10, 0.10), polish=0.55, rust_amt=0.3), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'MO_Leather': (lambda S: KM.leather(S, seed=307, tone=(0.03, 0.03, 0.032)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'MO_Bone':    (lambda S: KM.bone(S, seed=308), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'MO_Crystal': (lambda S: KM.crystal(S, seed=309, color=(0.20, 1.0, 0.62)), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.3, 1.4, 0.8))),
    'MO_Dark':    (lambda S: KM.leather(S, seed=310, tone=(0.015, 0.015, 0.015)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'SoulStaff')]
M = dict(corset='MO_Corset', sleeve='MO_Sleeve', cloth='MO_Cloth', cloak='MO_Cloak', gold='MO_Silver', metal='MO_Metal', leather='MO_Leather',
         crystal='MO_Crystal', dark='MO_Dark', bone='MO_Bone', wood='MO_Dark')


def build(bank, female=True):
    sk = H.build(); a = Asset(NAME + ('_F' if female else '_M'), sk)
    body, marks = MOD.base(sk, a, bank, 'MO' + ('F' if female else 'M'), female=female, look=None if not female else dict(skin='young_caucasian_female2', hair='o4saken_long01', hair_tint=(0.035, 0.03, 0.035),
                                                                     brows='eyebrow003', skin_kw=dict(pale=0.25, pubic=0.45, pubic_col=(0.03, 0.03, 0.03))))
    FK.build_outfit(a, body, marks, sk, 'MO', M, dict(male=not female, neck=2.8, spikes=True, skulls=True, tatter=0.45, collar=True, cloak=True, cloak_len=1.10, cloak_tatter=1.4))
    MOD.assign(a, [])
    MOD.covers(a, FK.COVERS)
    anims.build(sk, a, FK.StaffStyle())
    return a, {'SoulStaff': FK.staff('SoulStaff', dict(wood='MO_Dark', gold='MO_Silver', crystal='MO_Crystal', bone='MO_Bone', dark='MO_Dark'), 'skull')}, anims.CLIPS
