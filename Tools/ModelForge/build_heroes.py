"""Construit les héros de Corvalune et les exporte dans Unity (FBX + textures + manifestes).

  python3 build_heroes.py kael tharion …          # export
  python3 build_heroes.py kael --preview          # aperçus seulement (textures réduites)
  python3 build_heroes.py kael --preview --anims  # + planche d'animations
"""
import sys, os, time, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from heroes.common import TexBank, turnaround, anim_sheet
from forge import export as EX

HEROES = {'kael': 'heroes.kael_draven', 'tharion': 'heroes.tharion', 'varek': 'heroes.varek', 'elyra': 'heroes.elyra', 'morvanna': 'heroes.morvanna', 'base_m': 'heroes.base_male', 'base_f': 'heroes.base_female'}


def run(key, preview, anims):
    mod = importlib.import_module(HEROES[key])
    t0 = time.time(); print(f'== {mod.NAME}', flush=True)
    bank = TexBank(mod.RECIPES, preview)
    a, gear, clips = mod.build(bank)
    bank.bind(a)
    for g in gear.values(): bank.bind(g)
    att = [(gear[n], s) for s, n in mod.GEAR]
    print('  ', a.stats(), f'{time.time() - t0:.0f}s', flush=True)
    if preview:
        turnaround(a, mod.NAME, att, head=getattr(mod, 'HEAD_VIEW', (0, 1.66, 0.04)))
        if anims: anim_sheet(a, mod.NAME, att)
        return
    scale = getattr(mod, 'SCALE', 1.0)
    if scale != 1.0: EX.scale_asset(a, scale)
    ex = EX.hero_expect((1.60, 1.98), getattr(mod, 'MAX_TRIS', 140000))
    folder = getattr(mod, 'FOLDER', 'Heroes')
    mats = EX.export(a, bank, bank.specs(), f'{folder}/{mod.NAME}', clips=clips,
                     attachments=[(s, f'../../Weapons/{n}/{n}.fbx') for s, n in mod.GEAR],
                     collider=dict(type='capsule', center=[0, 0.93 * scale, 0], height=1.86 * scale, radius=0.33), expect=ex)
    shared = EX.shared_from(mats, f'../../{folder}/{mod.NAME}/')
    for gname, g in gear.items():
        if scale != 1.0: EX.scale_asset(g, scale)
        EX.export_prop(g, bank, bank.specs(), f'Weapons/{gname}', shared=shared)
    print(f'  terminé en {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    keys = [k for k in sys.argv[1:] if not k.startswith('--')] or list(HEROES)
    for k in keys: run(k, '--preview' in sys.argv, '--anims' in sys.argv)
