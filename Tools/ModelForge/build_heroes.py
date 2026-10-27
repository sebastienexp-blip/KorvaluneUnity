"""Construit les héros de Corvalune et les exporte dans Unity (FBX + textures + manifestes).

  python3 build_heroes.py kael tharion …          # export
  python3 build_heroes.py kael --preview          # aperçus seulement (textures réduites)
  python3 build_heroes.py kael --preview --anims  # + planche d'animations
"""
import sys, os, time, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from heroes.common import TexBank, turnaround, anim_sheet
from forge import export as EX
from heroes import modular as MOD

HEROES = {'kael': 'heroes.kael_draven', 'tharion': 'heroes.tharion', 'varek': 'heroes.varek', 'elyra': 'heroes.elyra', 'morvanna': 'heroes.morvanna', 'base_m': 'heroes.base_male', 'base_f': 'heroes.base_female'}


def run(key, preview, anims, sexes=('M', 'F'), morph=True):
    mod = importlib.import_module(HEROES[key])
    single = key.startswith('base_')                 # corps de base : un seul sexe par module
    for sex in (('M',) if single else sexes):
        female = sex == 'F'
        t0 = time.time(); print(f'== {mod.NAME} {"" if single else sex}', flush=True)
        bank = TexBank(mod.RECIPES, preview)
        a, gear, clips = mod.build(bank) if single else mod.build(bank, female=female)
        if morph and not preview and hasattr(a, 'base_body'):
            t1 = time.time(); n = MOD.morph(a, a.base_body, a.base_marks, a.base_female)
            print(f'   {len(n)} curseurs de morphologie ({time.time() - t1:.0f}s)', flush=True)
        bank.bind(a)
        for g in gear.values(): bank.bind(g)
        att = [(gear[n], s) for s, n in mod.GEAR]
        print('  ', a.stats(), f'{time.time() - t0:.0f}s', flush=True)
        if preview:
            turnaround(a, a.name, att, head=getattr(mod, 'HEAD_VIEW', (0, 1.66, 0.04)))
            if anims: anim_sheet(a, a.name, att)
            continue
        scale = getattr(mod, 'SCALE', 1.0)
        if scale != 1.0: EX.scale_asset(a, scale)
        ex = EX.hero_expect((1.60, 1.98), getattr(mod, 'MAX_TRIS', 220000))
        folder = getattr(mod, 'FOLDER', 'Heroes')
        if getattr(a, 'morph_table', None) is not None:
            from forge import morphs as MO
            if scale != 1.0: a.morph_table = (a.morph_table[0] * scale, {k: v * scale for k, v in a.morph_table[1].items()})
            os.makedirs(os.path.join(EX.ART, folder, a.name), exist_ok=True)
            ns = MO.write_table(os.path.join(EX.ART, folder, a.name, a.name + '_Morphs.bytes'), a.morph_table)
            print(f'   table de morphologie : {ns} formes × {len(a.morph_table[0])} sommets du corps', flush=True)
        mats = EX.export(a, bank, bank.specs(), f'{folder}/{a.name}', clips=clips,
                         attachments=[(s, f'../../Weapons/{n}/{n}.fbx') for s, n in mod.GEAR],
                         collider=dict(type='capsule', center=[0, 0.93 * scale, 0], height=1.86 * scale, radius=0.33), expect=ex)
        if sex == 'M' or single or 'M' not in sexes:
            shared = EX.shared_from(mats, f'../../{folder}/{a.name}/')
            for gname, g in gear.items():
                if scale != 1.0: EX.scale_asset(g, scale)
                EX.export_prop(g, bank, bank.specs(), f'Weapons/{gname}', shared=shared)
        print(f'  terminé en {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    keys = [k for k in sys.argv[1:] if not k.startswith('--')] or list(HEROES)
    sexes = tuple(s for s in ('M', 'F') if f'--{s.lower()}' in sys.argv) or ('M', 'F')
    for k in keys: run(k, '--preview' in sys.argv, '--anims' in sys.argv, sexes, '--no-morph' not in sys.argv)
