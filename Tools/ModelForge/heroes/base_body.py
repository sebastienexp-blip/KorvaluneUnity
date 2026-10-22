"""Modèles de base nus (homme / femme) : corps MakeHuman CC0 complet découpé en zones de peau, peau photo, yeux recolorés
(rouges / violets), sourcils, cils, cheveux, dents, langue ; anatomie et sous-vêtements en pièces détachables."""
from forge.rig import Asset
from forge import humanoid as H
from knight import anims
from heroes import modular as MOD

RECIPES = {}
GEAR = []
HEAD_VIEW = (0, 1.66, 0.05)


def make(name):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        MOD.base(sk, a, bank, 'BM' if name == 'BaseMale' else 'BF', female=(name == 'BaseFemale'))
        anims.build(sk, a)
        return a, {}, anims.CLIPS
    return build
