"""Exporte le kit de la ville de Korvalune vers Unity (Art/World/Ville/*, prefabs Resources/Prefabs/World/Ville). La ville
elle-même est assemblée autour du château dans Unity par Korvalune.EditorTools.TownBuilder.

  python3 build_town.py [--preview]
"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from heroes.common import TexBank
from forge import export as EX
from world import town as TW, shops as SH, recipes as R


def main(preview=False, which='all'):
    rec = dict(R.RECIPES); rec.update(TW.RECIPES); rec.update(SH.RECIPES)
    bank = TexBank(rec, preview)
    ville = os.path.join(EX.ART, 'World', 'Ville'); os.makedirs(ville, exist_ok=True)
    own_path = os.path.join(ville, 'owners.json')
    owners = json.load(open(own_path)) if os.path.exists(own_path) else {}; meta = {}
    only = [x for x in sys.argv[1:] if not x.startswith('--')]
    for a in (TW.pieces() if which in ('all', 'town') else []) + (SH.pieces() if which in ('all', 'shops') else []):
        if only and a.name not in only: continue
        allV = np.vstack([m.V for _, m, _ in a.parts]); size = allV.max(0) - allV.min(0)
        bank.bind(a)
        tris = sum(len(m.F) for _, m, _ in a.parts)
        print(a.name, tris, 'triangles', np.round(size, 2), flush=True)
        meta[a.name] = {k: (list(map(float, v)) if isinstance(v, tuple) else v) for k, v in getattr(a, 'meta', {}).items()}
        if preview: continue
        mats = EX.export(a, bank, bank.specs(), f'World/Ville/{a.name}', kind='prop', rig='None', prefab_dir='World/Ville', priority=1,
                         collider=dict(type='mesh'), shared=owners,
                         expect=dict(heightMin=float(size[1]) * 0.95, heightMax=float(size[1]) * 1.05, maxTriangles=40000))
        for m in mats:
            if m['name'] not in owners:
                owners[m['name']] = {k: f'../{a.name}/' + v for k, v in m.items() if k.endswith('Map')}
        json.dump(owners, open(own_path, 'w'), indent=1)
    out = os.path.join(EX.ART, 'World', 'Ville', 'meta.json')
    if not preview:
        old = json.load(open(out)) if os.path.exists(out) else {}
        old.update(meta); json.dump(old, open(out, 'w'), ensure_ascii=False, indent=1)

if __name__ == '__main__':
    main('--preview' in sys.argv, 'shops' if '--shops' in sys.argv else 'town' if '--town' in sys.argv else 'all')
