"""Modèles des pièces interchangeables du créateur de personnage : chaque coiffure, sourcil et cil du catalogue est ajusté
(proxy MakeHuman) sur le corps de base homme ET femme, reçoit les mêmes curseurs de morphologie (blendshapes) que le corps,
et est exporté en FBX skinné sur le squelette commun (rattaché par nom d'os dans Unity).

  python3 catalog/build_parts.py [--only hair|eyebrows|eyelashes] [--sex M|F]
"""
import os, sys, json, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, mhassets as MA, morphs as MO, export as EX
from forge.texgen import TexSet
from heroes.common import TexBank
from heroes import modular as MOD

CAT = os.path.join(EX.ART, 'Catalog')


def flat(color, S=64):
    z = np.zeros((S, S))
    return TexSet(np.tile(np.array(color, float), (S, S, 1)), np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.35, z + 1)


def main(only=None, sexes=('M', 'F')):
    cat = json.load(open(os.path.join(CAT, 'catalog.json')))
    for S in sexes:
        female = S == 'F'
        t0 = time.time()
        sk = H.build(); a = Asset('tmp', sk)
        body, marks = MOD.base(sk, a, TexBank({}, True), 'TMP', female=female)
        sl = MO.sliders(female, extra_dirs=[d for d in (os.path.join(os.path.dirname(MA.A), 'targets', 'hands'),) if os.path.isdir(d)])
        D = MO.shape_deltas(marks, sl)
        base = (marks['base_V'], marks['base_J'], marks['base_W'])
        print(f'== corps {S} prêt ({time.time() - t0:.0f}s), {len(D)} formes', flush=True)
        for kind, cutoff in (('hair', 0.40), ('eyebrows', 0.30), ('eyelashes', 0.30)):
            if only and kind != only: continue
            for e in cat[kind]:
                folder = '/' + e['proxy']
                if not os.path.isdir(folder): print('  absent :', folder); continue
                try:
                    p = MA.Proxy(folder)
                    m = p.fit(base[0], J=base[1], W=base[2])
                except Exception as ex:
                    print('  échec', kind, e['id'], ex); continue
                name = f"{e['id']}_{S}"
                pa = Asset(name, sk); mat = f"CAT_{kind}_{e['id']}"
                pa.add(kind, m, mat)
                MO.apply(pa, body, D)
                rel = '../../../'
                vfile = (e.get('variants') or [{}])[0].get('file') if kind == 'hair' else e.get('file')
                shared, tex = {}, {}
                if vfile:
                    shared[mat] = {'baseMap': rel + vfile}
                    vn = (e.get('variants') or [{}])[0].get('normal') if kind == 'hair' else None
                    if vn: shared[mat]['normalMap'] = rel + vn
                else:
                    tex[mat] = flat((0.05, 0.04, 0.035))
                specs = {mat: dict(metallic=0.0, smoothness=0.35, cutoff=cutoff if vfile else None)}
                EX.export(pa, tex, specs, f'Catalog/Models/{S}/{kind}', kind='part', rig='Generic', controller='', prefab_dir=f'Catalog/{kind}',
                          priority=2, shared=shared)
        print(f'== {S} terminé en {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    only = sys.argv[sys.argv.index('--only') + 1] if '--only' in sys.argv else None
    sexes = (sys.argv[sys.argv.index('--sex') + 1],) if '--sex' in sys.argv else ('M', 'F')
    main(only, sexes)
