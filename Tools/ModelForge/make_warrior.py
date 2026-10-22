import sys, os; sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, humanoid as h, tex, gltf, preview

def materials(g, pal):
    M = {}
    for name, kind, tint in pal: M[name] = tex.add_material(g, name, kind, tint)
    return M

def build_char(name, cfg, pal, out, anims=True):
    g = gltf.Glb(); M = materials(g, pal); parts = h.build(cfg, M)
    gltf.add_skinned(g, name, parts, h.BONES, M, h.all_anims() if anims else None); g.save(out); return g, parts, M

PAL_WARRIOR = [("skin", "skin", (1, 1, 1)), ("cloth", "cloth", (0.8, 0.3, 0.25)), ("leather", "leather", (1, 1, 1)), ("steel", "steel", (1, 1, 1)), ("dark", "darksteel", (1, 1, 1)), ("gold", "gold", (1, 1, 1)),
               ("bone", "bone", (1, 1, 1)), ("hide", "hide", (1, 1, 1)), ("crystal", "crystal", (1, 1, 1)), ("wood", "leather", (1.3, 1.1, 0.9))]
if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "/tmp/claude-0/fr/warrior.glb"
    g, parts, M = build_char("Warrior", dict(helm="great", shield=True, weapon="sword", cape=True, plate="steel"), PAL_WARRIOR, out)
    print(os.path.getsize(out) // 1024, "Ko", sum(len(p.pos) for p in parts), "sommets", sum(len(p.idx) for p in parts), "triangles")
