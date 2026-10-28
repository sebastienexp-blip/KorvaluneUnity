"""Réexporte uniquement les animations des héros (<Nom>_Anims.fbx), sans retoucher maillages, textures ni morphologie.
  python3 anims_only.py kael M
"""
import sys, os, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_heroes import HEROES
from heroes.common import TexBank
from forge import export as EX
from forge.fbx import write_fbx
from forge.rig import Asset

key, sex = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else 'M')
mod = importlib.import_module(HEROES[key])
bank = TexBank(mod.RECIPES, True)
a, gear, clips = mod.build(bank) if key.startswith('base_') else mod.build(bank, female=sex == 'F')
scale = getattr(mod, 'SCALE', 1.0)
if scale != 1.0: EX.scale_asset(a, scale)
folder = os.path.join(EX.ART, getattr(mod, 'FOLDER', 'Heroes'), a.name)
an = Asset(a.name + '_Anims', a.skeleton); an.clips = a.clips
info = write_fbx(os.path.join(folder, an.name + '.fbx'), an, binary=False)
print(a.name, 'clips', info['clips'], flush=True)
