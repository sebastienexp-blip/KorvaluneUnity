"""Vues quasi orthographiques (face, profil gauche, dos) d'un GLB normalisé à 1,83 m, quadrillées tous les 10 cm,
pour poser les repères du squelette. python3 glb_grid.py modele.glb sortie.png [yaw] [module_repères]"""
import sys, numpy as np, matplotlib
matplotlib.use('Agg'); import matplotlib.pyplot as plt
from imported.glb_creature import load
from forge.rig import MaterialDef
from forge.render import render
from forge.geo import vertex_normals, Mesh

V, UV, F, base = load(sys.argv[1], 1.83, float(sys.argv[3]) if len(sys.argv) > 3 else 0.0)
L = None
if len(sys.argv) > 4:
    import importlib; L = importlib.import_module(sys.argv[4]).LANDMARKS
m = Mesh(V, F, UV); N = vertex_normals(m)
img = None if base is None else (np.asarray(base.convert('RGB'), np.float32) / 255.0) ** 2.2 * 2.5
md = MaterialDef('m', color=(0.55, 0.52, 0.48, 1) if img is None else (1, 1, 1, 1), metallic=0.0, smoothness=0.3, images={} if img is None else {'base': img}, double_sided=True)
UVf = UV.copy(); UVf[:, 1] = 1 - UVf[:, 1]
prims = [(V, N, UVf, F, md)]
dist, fov, Wp, Hp = 80.0, 1.6, 700, 1000
fpx = 1 / np.tan(np.radians(fov / 2)) * Hp / 2 / dist       # pixels par mètre au plan de la cible
cy = 0.95
fig, ax = plt.subplots(1, 3, figsize=(21, 10))
for a, (yaw, hi, t) in zip(ax, ((0, 0, 'FACE  (x → gauche du perso)'), (90, 2, 'PROFIL GAUCHE  (z ← avant à gauche)'), (180, 0, 'DOS (x inversé)'))):
    im = np.asarray(render(prims, size=(Wp, Hp), yaw=yaw, pitch=0, target=(0, cy, 0), dist=dist, fov=fov, ss=1, ground=False))
    sgn = -1 if yaw == 180 else 1
    ext = [-Wp / 2 / fpx, Wp / 2 / fpx, cy - Hp / 2 / fpx, cy + Hp / 2 / fpx]
    if yaw in (90, 180): ext = [ext[1], ext[0], ext[2], ext[3]]   # l'image va vers -z (profil) ou -x (dos)
    a.imshow(im, extent=ext)
    if L:
        for k, p in L.items(): a.plot(p[hi], p[1], 'r+', ms=12, mew=1.5); a.annotate(k, (p[hi], p[1]), fontsize=7, color='yellow')
    a.set_xticks(np.round(np.arange(-1.0, 1.01, 0.1), 2)); a.set_yticks(np.round(np.arange(0, 2.01, 0.1), 2))
    a.grid(True, lw=0.4, color='w', alpha=0.5); a.tick_params(labelsize=7); a.set_title(t)
plt.tight_layout(); plt.savefig(sys.argv[2], dpi=85)
