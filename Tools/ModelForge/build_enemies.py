"""Construit les ennemis de Corvalune et les exporte dans Unity (FBX + textures + manifestes).

  python3 build_enemies.py squelette sorcier loup demon araignee chevalier
  python3 build_enemies.py loup --preview       # aperçus seulement
"""
import sys, os, time, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from heroes.common import TexBank, turnaround
from forge import export as EX
from knight import anims as KA

# clé : (module, rig, hauteur attendue (m) après mise à l'échelle, collider (centre y, hauteur, rayon), attentes d'orientation)
ENEMIES = {
    'squelette': ('enemies.squelette_guerrier', 'Human', (1.70, 1.98), (0.93, 1.86, 0.33), None),
    'sorcier':   ('enemies.sorcier_corrompu', 'Human', (1.70, 2.05), (0.93, 1.86, 0.33), None),
    'chevalier': ('enemies.chevalier_maudit', 'Human', (1.80, 2.15), (0.95, 1.95, 0.36), None),
    'demon':     ('enemies.demon_brute', 'Human', (2.60, 3.10), (1.40, 2.80, 0.60), None),
    'loup':      ('creatures.loup', 'Generic', (1.30, 1.65), (0.70, 1.40, 0.45), dict(facingBone='Head', behindBone='Hips', leftBone='Ear_L')),
    'araignee':  ('creatures.araignee', 'Generic', (0.95, 1.40), (0.55, 1.10, 0.90), dict(facingBone='Thorax', behindBone='Abdomen', leftBone='Leg0_Coxa_L')),
}
CREATURE_CLIPS = [('Idle', True, False, ()), ('Walk', True, False, (0.0, 0.5)), ('Run', True, False, (0.0, 0.5)), ('Attack', False, False, (0.55,)),
                  ('Howl', False, False, ()), ('Hit', False, False, ()), ('Death', False, False, ())]


def run(key, preview):
    mn, rig, hgt, col, orient = ENEMIES[key]
    mod = importlib.import_module(mn)
    t0 = time.time(); print(f'== {mod.NAME}', flush=True)
    bank = TexBank(mod.RECIPES, preview)
    a, gear, clips = mod.build(bank)
    bank.bind(a)
    for g in gear.values(): bank.bind(g)
    att = [(gear[n], s) for s, n in mod.GEAR]
    print('  ', a.stats(), f'{time.time() - t0:.0f}s', flush=True)
    if preview:
        turnaround(a, a.name, att, head=getattr(mod, 'HEAD_VIEW', (0, 1.66, 0.04))); return
    scale = getattr(mod, 'SCALE', 1.0)
    if scale != 1.0: EX.scale_asset(a, scale)
    if rig == 'Human':
        ex = EX.hero_expect(hgt, getattr(mod, 'MAX_TRIS', 200000))
        ex['sockets'] = [s for s, _ in mod.GEAR]
        clist = KA.CLIPS
    else:
        ex = dict(heightMin=hgt[0], heightMax=hgt[1], maxTriangles=getattr(mod, 'MAX_TRIS', 200000), sockets=[], **orient)
        clist = [c for c in CREATURE_CLIPS if c[0] in a.clips]
    EX.export(a, bank, bank.specs(), f'Enemies/{a.name}', rig=rig, clips=clist, prefab_dir='Enemies',
              attachments=[(s, f'../../Weapons/{n}/{n}.fbx') for s, n in mod.GEAR],
              collider=dict(type='capsule', center=[0, col[0], 0], height=col[1], radius=col[2]), expect=ex)
    shared = None
    for gname, g in gear.items():
        if scale != 1.0: EX.scale_asset(g, scale)
        EX.export_prop(g, bank, bank.specs(), f'Weapons/{gname}')
    print(f'  terminé en {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    keys = [k for k in sys.argv[1:] if not k.startswith('--')] or list(ENEMIES)
    for k in keys: run(k, '--preview' in sys.argv)
