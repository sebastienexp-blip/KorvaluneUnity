"""Chevalier de Corvalune — construit le modèle complet (géométrie, squelette, skinning, textures PBR, animations),
l'exporte en FBX pour Unity avec son manifeste d'intégration, et produit des rendus de contrôle.

  python3 build_knight.py                 # export complet dans Assets/Korvalune/Art/Characters/Knight
  python3 build_knight.py --preview       # aperçus rapides (textures réduites), rien n'est exporté
"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from forge.rig import Asset, MaterialDef
from forge import humanoid as H
from forge.fbx import write_fbx
from forge.render import posed_meshes, render, sheet
from knight import armor, materials as KM

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Korvalune', 'Art', 'Characters', 'Knight')
PREV = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')

# matériau → (fonction de texture, tailles (export, aperçu), tuilage, réglages URP)
MATS = {
    'KN_Steel':    (KM.steel,     (2048, 512), 1.0, dict(metallic=1.0, smoothness=1.0)),
    'KN_Brass':    (KM.brass,     (1024, 256), 1.0, dict(metallic=1.0, smoothness=1.0)),
    'KN_Leather':  (KM.leather,   (1024, 256), 1.0, dict(metallic=0.0, smoothness=1.0)),
    'KN_Mail':     (KM.chainmail, (1024, 512), 1.0, dict(metallic=1.0, smoothness=1.0)),
    'KN_Gambeson': (KM.gambeson,  (1024, 256), 1.0, dict(metallic=0.0, smoothness=1.0)),
    'KN_Cloak':    (KM.cloak,     ((1024, 2048), (256, 512)), 1.0, dict(metallic=1.0, smoothness=1.0)),
    'WP_Blade':    (KM.blade,     (1024, 256), 1.0, dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':     (KM.wood,      (1024, 256), 1.0, dict(metallic=0.0, smoothness=1.0)),
    'KN_Void':     (KM.void,      (64, 64), 1.0, dict(metallic=0.0, smoothness=1.0)),
}


def make_textures(preview):
    sets = {}
    for name, (fn, sizes, tile, kw) in MATS.items():
        if name in ('KN_Cloak',) and False: pass
        sz = sizes[1] if preview else sizes[0]
        t0 = time.time()
        if name == 'KN_Cloak': ts = fn(W=sz[0], H=sz[1])
        else: ts = fn(S=sz)
        sets[name] = ts
        print(f'  texture {name} {sz} {time.time() - t0:.1f}s', flush=True)
    return sets


def build(preview=False, with_anims=True):
    sk = H.build()
    asset = Asset('Knight', sk)
    armor.build(sk, asset)
    from knight import cape
    cape.build(sk, asset)
    cape.build_tabard(sk, asset)
    tex = make_textures(preview)
    for name, (fn, sizes, tile, kw) in MATS.items():
        asset.materials[name] = MaterialDef(name, images=tex[name].images(), **kw)
    if with_anims:
        from knight import anims
        anims.build(sk, asset)
    from knight import weapons
    gear = {'Longsword': weapons.sword(), 'HeaterShield': weapons.shield()}
    for g in gear.values():
        for mn in {p[2] for p in g.parts}:
            fn, sizes, tile, kw = MATS[mn]
            g.materials[mn] = MaterialDef(mn, images=tex[mn].images(), **kw)
    asset.gear = gear
    return asset, tex


def previews(asset, tag, attachments=()):
    os.makedirs(PREV, exist_ok=True)
    views = []
    for yaw in (20, 160, 90):
        views.append(render(posed_meshes(asset, attachments=attachments), size=(560, 700), yaw=yaw, pitch=6, target=(0, 0.98, 0), dist=4.9))
    sheet(views, 3).save(os.path.join(PREV, f'{tag}_turn.png'))
    render(posed_meshes(asset), size=(560, 560), yaw=25, pitch=4, target=(0, 1.66, 0.02), dist=1.05).save(os.path.join(PREV, f'{tag}_head.png'))


if __name__ == '__main__' and '--preview' in sys.argv:
    preview = True
    t0 = time.time()
    asset, tex = build(preview, with_anims='--noanim' not in sys.argv)
    print('géométrie', asset.stats(), f'{time.time() - t0:.1f}s', flush=True)
    if preview:
        previews(asset, 'knight'); print('aperçus écrits dans', PREV)
        sys.exit(0)


def anim_sheet(asset, tag='knight', frames=5, yaw=60, attachments=(), clips=None):
    ims, labels = [], []
    for name, c in asset.clips.items():
        if clips and name not in clips: continue
        for k in range(frames):
            f = int(round(k * (c.n - 1) / (frames - 1)))
            off = c.pos['Hips'][f] if 'Hips' in c.pos else np.zeros(3)
            tgt = (off[0], 0.95, off[2]) if name == 'Dodge' else (0, 0.9, 0)
            ims.append(render(posed_meshes(asset, c, f, attachments), size=(300, 380), yaw=yaw, pitch=8, target=tgt, dist=5.2, ss=1))
            labels.append(f'{name} {f}/{c.n - 1}')
    sheet(ims, frames, labels).save(os.path.join(PREV, f'{tag}_anims_{yaw}.png'))


def export(asset, tex):
    """Écrit FBX + textures + manifestes d'intégration Unity (chevalier, épée, écu)."""
    art = os.path.join(ROOT, 'Assets', 'Korvalune', 'Art')
    def mat_entries(a, folder, texdir_rel, shared):
        out = []
        for mn in sorted({p[2] for p in a.parts}):
            fn, sizes, tile, kw = MATS[mn]
            if mn in shared: files = {k: v for k, v in shared[mn].items()}
            else:
                files = tex[mn].save(os.path.join(folder, texdir_rel), mn)
                files = {k: texdir_rel + '/' + v for k, v in files.items()}
            e = dict(name=mn, color=[1, 1, 1, 1], metallic=kw['metallic'], smoothness=kw['smoothness'], normalScale=1.0,
                     occlusionStrength=1.0, tiling=[1, 1], doubleSided=False, maxSize=2048, mobileMaxSize=1024, **files)
            out.append(e)
        return out
    # chevalier
    kdir = os.path.join(art, 'Characters', 'Knight'); os.makedirs(kdir, exist_ok=True)
    kmats = mat_entries(asset, kdir, 'Textures', {})
    info = write_fbx(os.path.join(kdir, 'Knight.fbx'), asset)
    from knight.anims import CLIPS
    shared = {m['name']: {k: '../../Characters/Knight/' + v for k, v in m.items() if k.endswith('Map')} for m in kmats}
    man = dict(model='Knight.fbx', kind='character', rig='Human', prefab='Assets/Korvalune/Resources/Prefabs/Characters/Knight.prefab',
               controller='humanoid-combat', priority=10, materials=kmats,
               clips=[dict(name=n, loop=l, rootMotion=r, events=list(ev)) for n, l, r, ev in CLIPS],
               attachments=[dict(socket='Socket_Weapon_R', model='../../Weapons/Longsword/Longsword.fbx'),
                            dict(socket='Socket_Shield_L', model='../../Weapons/HeaterShield/HeaterShield.fbx')],
               collider=dict(type='capsule', center=[0, 0.93, 0], height=1.86, radius=0.33),
               expect=dict(heightMin=1.75, heightMax=1.98, maxTriangles=140000, sockets=['Socket_Weapon_R', 'Socket_Shield_L', 'Socket_Back'],
                           facingBone='LeftToes', behindBone='LeftFoot', leftBone='LeftHand'))
    json.dump(man, open(os.path.join(kdir, 'Knight.forge.json'), 'w'), indent=2, ensure_ascii=False)
    print('FBX', info['path'], info['polygons'], 'triangles', info['clips'])
    # armes
    for gname, g in asset.gear.items():
        gdir = os.path.join(art, 'Weapons', gname); os.makedirs(gdir, exist_ok=True)
        mats = mat_entries(g, gdir, 'Textures', shared)
        gi = write_fbx(os.path.join(gdir, gname + '.fbx'), g)
        allV = np.vstack([m.V for _, m, _ in g.parts]); size = allV.max(0) - allV.min(0); ctr = (allV.max(0) + allV.min(0)) / 2
        gm = dict(model=gname + '.fbx', kind='prop', rig='None', prefab=f'Assets/Korvalune/Resources/Prefabs/Weapons/{gname}.prefab', priority=1, materials=mats,
                  collider=dict(type='box', center=[round(float(x), 4) for x in ctr], size=[round(float(x), 4) for x in size], trigger=True),
                  expect=dict(heightMin=float(size[1]) * 0.95, heightMax=float(size[1]) * 1.05, maxTriangles=20000))
        json.dump(gm, open(os.path.join(gdir, gname + '.forge.json'), 'w'), indent=2, ensure_ascii=False)
        print('FBX', gi['path'], gi['polygons'], 'triangles')


if __name__ == '__main__' and '--preview' not in sys.argv:
    t0 = time.time()
    asset, tex = build(False)
    print('construit', asset.stats(), f'{time.time() - t0:.0f}s', flush=True)
    export(asset, tex)
    print(f'terminé en {time.time() - t0:.0f}s')
