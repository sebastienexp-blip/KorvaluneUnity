"""Exporte le kit de la Crypte de Korvalune vers Unity (Art/World/Crypte/*). La crypte elle-même est assemblée dans Unity
par Korvalune.EditorTools.DungeonBuilder.

  python3 build_dungeon.py [--preview]
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from heroes.common import TexBank
from forge import export as EX
from world import dungeon as DG, recipes as R


def main(preview=False):
    rec = dict(R.RECIPES); rec.update(DG.RECIPES)
    bank = TexBank(rec, preview)
    owners = {}                                   # matériau → textures déjà écrites par une pièce précédente
    for a in DG.pieces():
        allV = np.vstack([m.V for _, m, _ in a.parts]); size = allV.max(0) - allV.min(0)
        bank.bind(a)
        tris = sum(len(m.F) for _, m, _ in a.parts)
        if preview:
            print(a.name, tris, 'triangles', np.round(size, 2)); continue
        mats = EX.export(a, bank, bank.specs(), f'World/Crypte/{a.name}', kind='prop', rig='None', prefab_dir='World/Crypte', priority=1,
                         collider=dict(type='mesh'), shared=owners,
                         expect=dict(heightMin=float(size[1]) * 0.95, heightMax=float(size[1]) * 1.05, maxTriangles=40000))
        for m in mats:
            if m['name'] not in owners:
                owners[m['name']] = {k: f'../{a.name}/' + v for k, v in m.items() if k.endswith('Map')}

if __name__ == '__main__':
    main('--preview' in sys.argv)
