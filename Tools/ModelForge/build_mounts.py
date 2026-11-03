"""Construit les montures (cheval, tigre, panthère, griffon) et les exporte dans Unity.

  python3 build_mounts.py cheval tigre panthere griffon
  python3 build_mounts.py cheval --preview        # aperçus seulement (Tools/ModelForge/out/preview)
"""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from heroes.common import TexBank, turnaround, anim_sheet
from forge import export as EX
from build_enemies import CREATURE_CLIPS


def run(kind, preview):
    t0 = time.time()
    if kind == 'cheval':
        from creatures import cheval as CH
        bank = TexBank(CH.RECIPES, preview); a = CH.build(bank); bank.bind(a)
    else:
        from creatures import felin as FE
        bank = TexBank(FE.RECIPES, preview); a = FE.build(kind, bank); bank.bind(a)
    print(f'== {a.name}', a.stats(), f'{time.time() - t0:.0f}s', flush=True)
    if preview:
        turnaround(a, a.name, head=(0, 1.6, 0.9), headdist=2.6); anim_sheet(a, a.name, frames=4, clips=['Idle', 'Run']); return
    top = max(p[1] for p in a.skeleton.pos)
    clist = [c for c in CREATURE_CLIPS if c[0] in a.clips]
    EX.export(a, bank, bank.specs(), f'Mounts/{a.name}', rig='Generic', clips=clist, prefab_dir='Mounts',
              collider=dict(type='capsule', center=[0, 1.0, 0], height=2.0, radius=0.45),
              expect=dict(heightMin=0.8, heightMax=3.6, maxTriangles=200000, sockets=['Socket_Saddle'], facingBone='Head', behindBone='Hips', leftBone='UpperArm_L'))
    print(f'  terminé en {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    kinds = [k for k in sys.argv[1:] if not k.startswith('--')] or ['cheval', 'tigre', 'panthere', 'griffon']
    for k in kinds: run(k, '--preview' in sys.argv)
