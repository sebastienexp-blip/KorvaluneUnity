import sys, numpy as np
from heroes.common import TexBank
from forge.render import posed_meshes, render, sheet
import importlib
mod = importlib.import_module(sys.argv[1] if len(sys.argv) > 1 else 'enemies.abbe_trellis')
bank = TexBank(mod.RECIPES, True); a, gear, _ = mod.build(bank); bank.bind(a)
for g in gear.values(): bank.bind(g)
c = a.clips.get('Idle'); att = [(gear[n], s) for s, n in mod.GEAR]
ims = []
for clip in (None, c):
    pm = posed_meshes(a, clip, 0, att)
    for yaw in (0, 90):
        ims.append(render(pm, size=(420, 520), yaw=yaw, pitch=4, target=(0, 1.22, 0.02), dist=1.6, ss=1))
sheet(ims, 4, ['lié face', 'lié profil', 'idle face', 'idle profil']).save('out/head_check.png')
