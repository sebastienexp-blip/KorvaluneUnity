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
from knight import anims as AN
from heroes import extra_anims as XA
import json

key, sex = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else 'M')
mod = importlib.import_module(HEROES[key])
bank = TexBank(mod.RECIPES, True)
a, gear, clips = mod.build(bank) if key.startswith('base_') else mod.build(bank, female=sex == 'F')
scale = getattr(mod, 'SCALE', 1.0)
if scale != 1.0: EX.scale_asset(a, scale)
folder = os.path.join(EX.ART, getattr(mod, 'FOLDER', 'Heroes'), a.name)
for c in XA.build(AN._SK, AN._STYLE): a.clips[c.name] = c          # poings, tir à l'arc
an = Asset(a.name + '_Anims', a.skeleton); an.clips = a.clips
# manifeste : déclarer les nouvelles prises pour que Unity les importe
mp = os.path.join(folder, a.name + '.forge.json')
if os.path.exists(mp):
    man = json.load(open(mp)); have = {c['name'] for c in man.get('clips', [])}
    for n, l, r, ev in XA.CLIPS:
        if n not in have: man['clips'].append(dict(name=n, loop=l, rootMotion=r, events=list(ev)))
    json.dump(man, open(mp, 'w'), indent=2, ensure_ascii=False)
info = write_fbx(os.path.join(folder, an.name + '.fbx'), an, binary=False)
print(a.name, 'clips', info['clips'], flush=True)
