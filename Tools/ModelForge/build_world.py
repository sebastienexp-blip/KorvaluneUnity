"""Exporte le décor de la vallée de Korvalune vers Unity : château, arbres, rochers (FBX + manifestes) et textures des
couches de terrain (Art/World/Terrain). La scène elle-même (relief, rivière, routes, placement) est construite dans
Unity par Korvalune.EditorTools.WorldBuilder.

  python3 build_world.py [--preview]
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from heroes.common import TexBank
from forge import export as EX
from world import props as P, recipes as R, materials as WM, town as T


def export_static(a, bank, collider):
    allV = np.vstack([m.V for _, m, _ in a.parts]); size = allV.max(0) - allV.min(0); ctr = (allV.max(0) + allV.min(0)) / 2
    for _, m, _ in a.parts: m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
    bank.bind(a)
    EX.export(a, bank, bank.specs(), f'World/{a.name}', kind='prop', rig='None', prefab_dir='World', priority=1, collider=collider,
              expect=dict(heightMin=float(size[1]) * 0.95, heightMax=float(size[1]) * 1.05, maxTriangles=60000))


def main(preview=False, only_trees=False, castle_only=False):
    bank = TexBank({**R.RECIPES, **T.RECIPES}, preview)
    if not only_trees: export_static(P.castle(), bank, dict(type='mesh'))
    if castle_only:
        folder = os.path.join(EX.ART, 'World', 'Terrain'); T.cobbles(512 if preview else 1024).save(folder, 'Pave'); return
    for k in range(3):
        for lod in (0, 1):   # LOD0 détaillé, LOD1 allégé (assemblés en LODGroup dans Unity)
            t = P.oak(k, H=8.0 + 1.5 * k, lod=lod); export_static(t, bank, dict(type='capsule', center=[0, 2.0, 0], height=4.0, radius=0.45))
            t = P.pine(k, H=11.0 + 2.0 * k, lod=lod); export_static(t, bank, dict(type='capsule', center=[0, 2.0, 0], height=4.0, radius=0.35))
        if not only_trees: r = P.rock(k, size=1.5 + 1.2 * k); export_static(r, bank, dict(type='mesh'))
    if only_trees: return
    folder = os.path.join(EX.ART, 'World', 'Terrain')
    S = 512 if preview else 1024
    for name, fn in (('Herbe', WM.grass), ('SousBois', WM.forest_floor), ('Chemin', WM.dirt), ('Roche', WM.rock), ('Sable', WM.sand), ('Eau', WM.water), ('Pave', T.cobbles)):
        fn(S).save(folder, name); print('  couche', name, flush=True)


if __name__ == '__main__':
    main('--preview' in sys.argv, '--trees' in sys.argv, '--castle' in sys.argv)
