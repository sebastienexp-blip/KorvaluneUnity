"""Outils communs aux ennemis : remappage de matériaux, corps de gabarit invisible, blobs."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H
from heroes import modular as MOD


def remap(asset, table, name=None):
    asset.parts = [(n, m, table.get(mat, mat)) for n, m, mat in asset.parts]
    if name: asset.name = name
    return asset


def template_body(sk, a, bank, female=False, keep=False, look=None):
    """Corps MakeHuman ajusté au squelette, servant de gabarit aux pièces (armure, robe) ; retiré de l'asset sauf keep=True."""
    body, marks = MOD.base(sk, a, bank, 'TMP', female=female, look=look)
    if not keep:
        a.parts = []; a.slot = {}; a.hides = {}
    return body, marks
