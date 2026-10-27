"""Exporte les marchands de Korvalune (PNJ) vers Unity (Art/Characters/PNJ_*, prefabs Resources/Prefabs/Characters).

  python3 build_npcs.py [PNJ_Forgeron …] [--preview]
"""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from heroes.common import TexBank, turnaround
from forge import export as EX
from heroes import npcs as NP


def main(names, preview):
    for name in names or list(NP.CATALOG):
        build, rec = NP.CATALOG[name]; t0 = time.time()
        bank = TexBank(dict(rec), preview)
        a, gear, clips = build(bank)
        bank.bind(a)
        print(name, a.stats(), f'{time.time() - t0:.0f}s', flush=True)
        if preview: turnaround(a, name, (), head=NP.HEAD_VIEW); continue
        EX.export(a, bank, bank.specs(), f'Characters/{name}', clips=clips, attachments=[],
                  collider=dict(type='capsule', center=[0, 0.93, 0], height=1.86, radius=0.33), expect=EX.hero_expect((1.55, 1.95), 120000))
        print(f'  terminé en {time.time() - t0:.0f}s', flush=True)

if __name__ == '__main__':
    main([x for x in sys.argv[1:] if not x.startswith('--')], '--preview' in sys.argv)
