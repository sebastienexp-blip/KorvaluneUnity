"""Modèles de base nus (homme / femme) : tout le corps MakeHuman CC0 avec peau photo, yeux recolorés (rouges / violets),
sourcils, cils, cheveux, dents et langue en proxys. Servent de référence anatomique et de base aux héros."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, mhbase as MB
from knight import anims
from heroes.common import mh_character
from forge import anatomy as ANA, humanparts as HP
from knight import materials as KM

RECIPES = {}
GEAR = []
HEAD_VIEW = (0, 1.66, 0.05)
CFG = {
    'BaseMale': dict(targets=MB.MALE, extra=[('universal-male-young-maxmuscle-averageweight', 0.45), ('stomach-pregnant-decr', 0.5), ('stomach-tone-incr', 0.6)], skin='middleage_caucasian_male',
                     hair='short02', hair_tint=(0.10, 0.07, 0.05), brows='eyebrow001', lashes='eyelashes01', eye_color=(0.62, 0.05, 0.04),
                     skin_kw=dict(pubic=0.75, pubic_col=(0.07, 0.05, 0.035))),
    'BaseFemale': dict(targets=MB.FEMALE, extra=[('female-young-averagemuscle-averageweight-maxcup-maxfirmness', 0.75), ('stomach-pregnant-decr', 1.0), ('stomach-tone-incr', 0.7)], skin='young_caucasian_female',
                       hair='culturalibre_hair_01', brows='eyebrow010', lashes='eyelashes02', eye_color=(0.45, 0.14, 0.72),
                       skin_kw=dict(pubic=0.45, pubic_col=(0.04, 0.035, 0.035))),
}


def make(name):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        P = 'BM' if name == 'BaseMale' else 'BF'
        body, marks = mh_character(sk, a, bank, P, body_mask=lambda V: V[:, 1] > -1, **CFG[name])
        male = name == 'BaseMale'
        # anatomie intime : pièce séparée (masquable dans Unity), même matériau que la peau
        a.add('sexe', ANA.male(body, sk) if male else ANA.female(body, sk), P + '_Skin', slot='Anatomie')
        # sous-vêtements de lin : coques à bord net (subdivision + découpe continue), double face, détachables
        V = body.V; ax = np.abs(V[:, 0])
        waist = 0.985 if male else 0.962
        def brief_in(Q):
            hole = 0.884 + 0.050 * np.clip((np.abs(Q[:, 0]) - 0.028) / 0.11, 0, 1) ** 0.9
            return np.minimum(waist - Q[:, 1], Q[:, 1] - hole)
        coarse = HP.bone_mask(body, sk, ['Hips', 'Spine', 'UpperLeg'], 0.2) & (V[:, 1] > 0.84) & (V[:, 1] < waist + 0.03) & (ax < 0.25)
        bump = (lambda Q: 0.030 * np.exp(-((Q[:, 0] / 0.034) ** 2 + ((Q[:, 1] - 0.872) / 0.038) ** 2)) * np.clip(Q[:, 2] / 0.03, 0, 1)) if male else None
        a.add('sous_vetement_bas', HP.cloth_cut(body, coarse, brief_in, 0.0024, bump=bump), P + '_Under', slot='SousVetementBas')
        if not male:
            ch = HP.bone_mask(body, sk, ['Chest', 'UpperChest', 'Spine'], 0.25)
            cen = []
            for sx in (1, -1):
                s_ = ch & (V[:, 2] > 0.05) & (V[:, 0] * sx > 0) & (V[:, 1] > 1.18) & (V[:, 1] < 1.40)
                cen.append(V[s_][np.argmax(V[s_][:, 2])])
            def bra_in(Q):
                cups = np.max([0.072 - np.sqrt((Q[:, 0] - c[0]) ** 2 + ((Q[:, 1] - c[1] - 0.006) / 1.05) ** 2) for c in cen], 0)
                cups = np.where(Q[:, 2] > cen[0][2] - 0.10, cups, -1)
                band = 0.018 - np.abs(Q[:, 1] - (cen[0][1] - 0.062))
                straps = np.max([0.008 - np.abs(Q[:, 0] - c[0] * 0.95) for c in cen], 0)
                straps = np.where((Q[:, 1] > cen[0][1]) & (Q[:, 1] < 1.47), straps, -1)
                return np.maximum(np.maximum(cups, band), straps)
            coarse = (ch | HP.bone_mask(body, sk, ['Shoulder'], 0.3)) & (V[:, 1] > 1.12) & (V[:, 1] < 1.50) & (ax < 0.20)
            a.add('sous_vetement_haut', HP.cloth_cut(body, coarse, bra_in, 0.0024), P + '_Under', slot='SousVetementHaut')
        bank.recipes[P + '_Under'] = (lambda S: KM.linen(S, color=(0.60, 0.55, 0.46)), 512, 256, dict(metallic=0.0, smoothness=1.0))
        anims.build(sk, a)
        return a, {}, anims.CLIPS
    return build
