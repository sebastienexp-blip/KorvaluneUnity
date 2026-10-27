"""Exporte les Terriers de l'Astral (donjon-parcours naturel) vers Unity : un FBX par tronçon de 48 m
(Art/World/Terriers, prefabs Resources/Prefabs/World/Terriers) et layout.json (entrée, tracé, cavernes, lumières,
trésors, gardien) lu par Korvalune.EditorTools.CaveBuilder.

  python3 build_caves.py [--preview]
"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from heroes.common import TexBank
from forge import export as EX
from world import caves as CV, recipes as R, dungeon as DG


def main(preview=False):
    rec = dict(R.RECIPES); rec.update(DG.RECIPES); rec.update(CV.RECIPES)
    bank = TexBank(rec, preview)
    L, assets, layout = CV.build(7)
    folder = os.path.join(EX.ART, 'World', 'Terriers'); os.makedirs(folder, exist_ok=True)
    owners = {}
    for a in assets:
        bank.bind(a)
        allV = np.vstack([m.V for _, m, _ in a.parts]); size = allV.max(0) - allV.min(0)
        tris = sum(len(m.F) for _, m, _ in a.parts); print(a.name, tris, 'triangles', flush=True)
        if preview: continue
        mats = EX.export(a, bank, bank.specs(), f'World/Terriers/{a.name}', kind='prop', rig='None', prefab_dir='World/Terriers', priority=1,
                         collider=dict(type='mesh'), shared=owners,
                         expect=dict(heightMin=float(size[1]) * 0.9, heightMax=float(size[1]) * 1.1, maxTriangles=60000))
        for m in mats:
            if m['name'] not in owners: owners[m['name']] = {k: f'../{a.name}/' + v for k, v in m.items() if k.endswith('Map')}
    if not preview:
        json.dump(layout, open(os.path.join(folder, 'layout.json'), 'w'), ensure_ascii=False, indent=1)
    print(len(assets), 'tronçons,', len(layout['lights']), 'lumières')

if __name__ == '__main__':
    main('--preview' in sys.argv)
