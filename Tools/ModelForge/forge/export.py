"""Export d'un asset ModelForge vers le projet Unity : FBX, textures URP séparées, manifeste d'intégration (*.forge.json)."""
import os, json
import numpy as np
from .fbx import write_fbx

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'Korvalune', 'Art')


def scale_asset(asset, s):
    """Mise à l'échelle uniforme (maillages, squelette, déplacements du bassin)."""
    for _, m, _ in asset.parts: m.V = m.V * s
    if asset.skeleton is not None:
        asset.skeleton.pos = [p * s for p in asset.skeleton.pos]; asset.skeleton._rg = None
    for c in asset.clips.values():
        for b in c.pos: c.pos[b] = c.pos[b] * s
    return asset


def material_entries(asset, tex, specs, folder, shared=None):
    """Écrit les textures des matériaux de l'asset (sauf celles déjà écrites ailleurs : shared) ; renvoie les entrées du manifeste."""
    shared = shared or {}
    out = []
    for mn in sorted({p[2] for p in asset.parts}):
        sp = specs.get(mn, {})
        if mn in shared: files = dict(shared[mn])
        else:
            files = tex[mn].save(os.path.join(folder, 'Textures'), mn)
            files = {k: 'Textures/' + v for k, v in files.items()}
        e = dict(name=mn, color=list(sp.get('color', (1, 1, 1, 1))), metallic=sp.get('metallic', 1.0), smoothness=sp.get('smoothness', 1.0),
                 normalScale=sp.get('normalScale', 1.0), occlusionStrength=1.0, tiling=[1, 1], doubleSided=sp.get('doubleSided', False),
                 emission=list(sp.get('emission', (0, 0, 0))), maxSize=sp.get('maxSize', 2048), mobileMaxSize=sp.get('mobileMaxSize', 1024),
                 alphaClip=sp.get('cutoff') is not None, cutoff=sp.get('cutoff') or 0.5, **files)
        if sp.get('cutoff') is not None: e['doubleSided'] = True
        out.append(e)
    return out


def shared_from(entries, rel_prefix):
    return {m['name']: {k: rel_prefix + v for k, v in m.items() if k.endswith('Map')} for m in entries}


def export(asset, tex, specs, subdir, kind='character', rig='Human', clips=(), attachments=(), collider=None, expect=None,
           controller='humanoid-combat', prefab_dir='Characters', priority=10, shared=None):
    """subdir : chemin sous Assets/Korvalune/Art (ex. 'Heroes/KaelDraven'). clips : [(nom, boucle, mouvement_racine, événements)]."""
    folder = os.path.join(ART, subdir); os.makedirs(folder, exist_ok=True)
    mats = material_entries(asset, tex, specs, folder, shared)
    info = write_fbx(os.path.join(folder, asset.name + '.fbx'), asset)
    man = dict(model=asset.name + '.fbx', kind=kind, rig=rig, prefab=f'Assets/Korvalune/Prefabs/{prefab_dir}/{asset.name}.prefab',
               controller=controller if rig != 'None' and asset.clips else '', priority=priority, materials=mats,
               clips=[dict(name=n, loop=l, rootMotion=r, events=list(ev)) for n, l, r, ev in clips],
               attachments=[dict(socket=s, model=m) for s, m in attachments])
    if getattr(asset, 'slot', None):
        names = sorted(set(asset.slot.values()))
        hides = getattr(asset, 'hides', {})
        man['slots'] = [dict(name=s, hides=list(hides.get(s, []))) for s in names]
    if getattr(asset, 'morphs', None): man['morphs'] = asset.morphs
    if collider: man['collider'] = collider
    if expect: man['expect'] = expect
    json.dump(man, open(os.path.join(folder, asset.name + '.forge.json'), 'w'), indent=2, ensure_ascii=False)
    print(f'  FBX {subdir}/{asset.name}.fbx : {info["polygons"]} triangles, {len(info["materials"])} matériaux, clips {info["clips"]}', flush=True)
    return mats


def export_prop(asset, tex, specs, subdir, shared=None, priority=1):
    allV = np.vstack([m.V for _, m, _ in asset.parts]); size = allV.max(0) - allV.min(0); ctr = (allV.max(0) + allV.min(0)) / 2
    return export(asset, tex, specs, subdir, kind='prop', rig='None', prefab_dir='Weapons', priority=priority, shared=shared,
                  collider=dict(type='box', center=[round(float(x), 4) for x in ctr], size=[round(float(x), 4) for x in size], trigger=True),
                  expect=dict(heightMin=float(size[1]) * 0.95, heightMax=float(size[1]) * 1.05, maxTriangles=25000))


def hero_expect(height=(1.70, 1.98), tris=140000):
    return dict(heightMin=height[0], heightMax=height[1], maxTriangles=tris, sockets=['Socket_Weapon_R', 'Socket_Shield_L', 'Socket_Back'],
                facingBone='LeftToes', behindBone='LeftFoot', leftBone='LeftHand')
