"""Aperçu d'un GLB importé : python3 glb_preview.py chemin.glb sortie.png"""
import sys, numpy as np
from forge.glb import GLB
from forge.rig import MaterialDef
from forge.render import render, sheet
from forge.geo import vertex_normals


def load_prims(path, height=None):
    g = GLB(path); prims = []
    for V, N, UV, F, mi in g.meshes():
        base, mr, nrm, fac = g.material(mi)
        im = {}
        if base is not None: im['base'] = (np.asarray(base, np.float32)[..., :3] / 255.0) ** 2.2
        if mr is not None:
            a = np.asarray(mr, np.float32) / 255.0; mk = np.zeros_like(a)
            mk[..., 0] = a[..., 2] * fac['metallic']; mk[..., 3] = 1 - a[..., 1] * fac['roughness']; im['mask'] = mk
        if nrm is not None: im['normal'] = np.asarray(nrm, np.float32)[..., :3] / 255.0
        md = MaterialDef('m', color=tuple(fac.get('base', [1, 1, 1, 1])), metallic=0.0,
                         smoothness=0.35 if mr is None else 1.0, images=im, double_sided=True)
        prims.append([V, N, UV, F, md])
    allV = np.vstack([p[0] for p in prims]); lo, hi = allV.min(0), allV.max(0)
    s = 1.0 if height is None else height / (hi[1] - lo[1]); c = np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])
    out = []
    for V, N, UV, F, md in prims:
        V = (V - c) * s
        class M: pass
        mm = M(); mm.V, mm.F = V, F
        N = vertex_normals(mm) if N is None else N
        out.append((V, N / (np.linalg.norm(N, axis=1, keepdims=True) + 1e-12), UV, F, md))
    return out


if __name__ == '__main__':
    p = load_prims(sys.argv[1], 2.1)
    H = max(v[0][:, 1].max() for v in p)
    ims = [render(p, size=(480, 640), yaw=y, pitch=6, target=(0, H * 0.5, 0), dist=H * 2.3, ss=1) for y in (0, 45, 135, 270)]
    sheet(ims, 4).save(sys.argv[2])
