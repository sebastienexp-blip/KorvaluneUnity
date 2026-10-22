import sys, os; sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, humanoid as h, preview, make_warrior as mw
from PIL import Image
COL = {"skin": (0.78, 0.58, 0.46), "cloth": (0.5, 0.12, 0.12), "leather": (0.30, 0.18, 0.10), "steel": (0.62, 0.65, 0.72), "dark": (0.25, 0.26, 0.30), "gold": (0.85, 0.65, 0.22), "bone": (0.8, 0.76, 0.64), "hide": (0.3, 0.24, 0.2), "crystal": (0.55, 0.2, 0.9), "wood": (0.4, 0.28, 0.16)}

def shots(parts, M, anims, picks, out, size=(330, 480), yaw=0.5, dist=3.6):
    names = {v: k for k, v in M.items()}; ims = []
    for (an, t) in picks:
        rots, trans = {}, {}
        if an:
            a = [x for x in anims if x["name"] == an][0]
            for b, (ts, qs) in a["rot"].items(): i = min(range(len(ts)), key=lambda k: abs(ts[k] - t)); rots[b] = qs[i]
            for b, (ts, vs) in a.get("pos", {}).items():
                i = min(range(len(ts)), key=lambda k: abs(ts[k] - t)); trans[b] = np.array(vs[i]) - (np.array(h.BONES[b][1]) - np.array(h.BONES[h.BONES[b][0]][1]))
        W = preview.world_mats(h.BONES, rots, trans); items = []
        for p in parts:
            pos, n = preview.skin_part(p, h.BONES, W); items.append((pos, n, p.idx, COL[names[p.mat]]))
        ims.append(preview.render(items, size=size, yaw=yaw[0] if isinstance(yaw, tuple) else yaw, dist=dist, center=(0, 0.95, 0)))
    W_ = sum(i.width for i in ims); sheet = Image.new("RGB", (W_, ims[0].height))
    x = 0
    for i in ims: sheet.paste(i, (x, 0)); x += i.width
    sheet.save(out)

if __name__ == "__main__":
    g, parts, M = mw.build_char("Warrior", dict(helm="great", shield=True, weapon="sword", cape=True), mw.PAL_WARRIOR, "/tmp/claude-0/fr/w.glb", anims=False)
    A = h.all_anims()
    shots(parts, M, A, [(None, 0), ("Walk", 0.25), ("Attack1", 0.25)], "/tmp/claude-0/fr/warrior.png", size=(520, 760), dist=3.0, yaw=3.14159+0.5)
