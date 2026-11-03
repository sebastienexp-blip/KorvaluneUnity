"""Spectre du Néant — mage déchu de l'Abîme : chair grise et creusée, veines violettes lumineuses, yeux de néant ;
robe et cape d'étoffe noire violacée brodées de runes qui luisent, pans réduits en lambeaux fumants jusqu'au sol,
capuche profonde ; épaulières et brassards d'acier noirci à pointes ; bâton de bois mort dont la tête en croissant
enserre un éclat de néant incandescent."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H
from knight import anims, materials as KM
from heroes import female_kit as FK, varek as VR, modular as MOD
from enemies import materials as EM, sorcier_corrompu as SCO
from enemies.common import remap

NAME = 'SpectreNeant'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.66, 0.05)
RECIPES = {
    'SN_Robe':    (lambda S: KM.tooled_leather(S, seed=1501, tone=(0.03, 0.015, 0.05), line=(0.55, 0.25, 0.85), n=40), 1024, 512, dict(metallic=0.3, smoothness=1.0)),
    'SN_Sleeve':  (lambda S: KM.gambeson(S, seed=1502, color=(0.04, 0.02, 0.06)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'SN_Cloth':   (lambda W, H_: EM.void_cloth(W, H_, seed=1503), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0, emission=(1.5, 1.5, 1.5), emission_preview=(0.8, 0.8, 0.8))),
    'SN_Cloak':   (lambda W, H_: EM.void_cloth(W, H_, seed=1504), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0, emission=(1.2, 1.2, 1.2), emission_preview=(0.6, 0.6, 0.6))),
    'SN_Gold':    (lambda S: KM.brass(S, seed=1505, gold=(0.40, 0.30, 0.55), dark=(0.04, 0.02, 0.06)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'SN_Metal':   (lambda S: EM.cursed_steel(S, seed=1506, tone=(0.06, 0.05, 0.08), ember=(0.6, 0.25, 1.0), amount=0.35), 1024, 256, dict(metallic=1.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.8, 0.8, 0.8))),
    'SN_Leather': (lambda S: KM.leather(S, seed=1507, tone=(0.03, 0.02, 0.04)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'SN_Crystal': (lambda S: KM.crystal(S, seed=1508, color=(0.72, 0.30, 1.0), glow=1.8), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(3.0, 3.0, 3.0), emission_preview=(1.6, 0.8, 2.2))),
    'SN_Dark':    (lambda S: KM.gambeson(S, seed=1509, color=(0.006, 0.003, 0.01)), 256, 128, dict(metallic=0.0, smoothness=0.2)),
    'SN_Wood':    (lambda S: EM.tinted(KM.wood(S, seed=1510), (0.22, 0.20, 0.24)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'BatonNeant')]
M = dict(corset='SN_Robe', sleeve='SN_Sleeve', cloth='SN_Cloth', cloak='SN_Cloak', gold='SN_Gold', metal='SN_Metal', leather='SN_Leather',
         crystal='SN_Crystal', dark='SN_Dark', bone='SN_Gold', wood='SN_Wood')
STAFF = {'SC_Wood': 'SN_Wood', 'SC_Gold': 'SN_Gold', 'SC_Crystal': 'SN_Crystal', 'SC_Dark': 'SN_Dark'}


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk)
    body, marks = MOD.base(sk, a, bank, 'SN', female=False, look=dict(
        extra=[('universal-male-young-averagemuscle-minweight', 1.0), ('stomach-pregnant-decr', 1.0)],
        skin='middleage_caucasian_male', skin_tint=(0.58, 0.55, 0.66), hair=None,
        brows='eyebrow001', eye_color=(0.75, 0.30, 1.0), glow=2.0, skin_kw=dict(pale=0.65, makeup=(0.07, 0.02, 0.10), veins=(0.65, 0.25, 1.0), dirt=0.3, pubic=0.0)))
    a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'SousVetementBas', 'SousVetementHaut', 'HaillonsHaut', 'HaillonsBas')]
    FK.build_outfit(a, body, marks, sk, 'SN', M, dict(male=True, front_panel=True, spikes=True, skulls=True, tatter=1.4, collar=True, cloak=True, cloak_len=1.18, cloak_tatter=2.2))
    def add(n, m, mat): a.add(n, m, mat, slot='Capuche')
    VR.hood(a, add, body, marks, sk, cloth='SN_Cloak', lining='SN_Dark', depth=1.9, loose=0.022)
    MOD.assign(a, [])
    MOD.covers(a, FK.COVERS)
    anims.build(sk, a, FK.StaffStyle())
    return a, {'BatonNeant': remap(SCO.corrupt_staff('BatonNeant'), STAFF, 'BatonNeant')}, anims.CLIPS
