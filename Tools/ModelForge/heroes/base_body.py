"""Modèles de base nus (homme / femme) : tout le corps MakeHuman CC0 avec peau photo, yeux recolorés (rouges / violets),
sourcils, cils, cheveux, dents et langue en proxys. Servent de référence anatomique et de base aux héros."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, mhbase as MB
from knight import anims
from heroes.common import mh_character

RECIPES = {}
GEAR = []
HEAD_VIEW = (0, 1.66, 0.05)
CFG = {
    'BaseMale': dict(targets=MB.MALE, extra=[('universal-male-young-maxmuscle-averageweight', 0.45)], skin='middleage_caucasian_male',
                     hair='short02', hair_tint=(0.10, 0.07, 0.05), brows='eyebrow001', lashes='eyelashes01', eye_color=(0.62, 0.05, 0.04)),
    'BaseFemale': dict(targets=MB.FEMALE, extra=[('female-young-averagemuscle-averageweight-maxcup-maxfirmness', 0.75)], skin='young_caucasian_female',
                       hair='culturalibre_hair_01', brows='eyebrow010', lashes='eyelashes02', eye_color=(0.45, 0.14, 0.72)),
}


def make(name):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        mh_character(sk, a, bank, 'BM' if name == 'BaseMale' else 'BF', body_mask=lambda V: V[:, 1] > -1, **CFG[name])
        anims.build(sk, a)
        return a, {}, anims.CLIPS
    return build
