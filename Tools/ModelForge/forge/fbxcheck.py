"""Relecture indépendante d'un FBX ASCII produit par ModelForge : cohérence des objets, connexions, polygones, skinning, animations."""
import re, sys
import numpy as np

def check(path):
    txt = open(path, encoding='utf-8').read()
    ok = True
    def chk(c, msg):
        nonlocal ok; print(('  OK  ' if c else '  ÉCHEC ') + msg); ok &= bool(c)
    ids = set(int(x) for x in re.findall(r'^\t(?:Model|Geometry|Material|Deformer|NodeAttribute|Pose|AnimationStack|AnimationLayer|AnimationCurveNode|AnimationCurve): (\d+),', txt, re.M))
    conns = re.findall(r'C: "(O[OP])",(\d+),(\d+)', txt)
    bad = [c for c in conns if int(c[1]) not in ids or (int(c[2]) not in ids and c[2] != '0')]
    chk(len(bad) == 0, f'{len(conns)} connexions, toutes vers des objets existants ({len(bad)} invalides)')
    def arr(name, s=txt):
        m = re.search(name + r': \*(\d+) \{\s*a: ([^}]*)\}', s)
        return int(m.group(1)), np.array([float(x) for x in m.group(2).split(',')])
    nv, V = arr('Vertices'); npv, PVI = arr('PolygonVertexIndex')
    chk(nv == len(V) and nv % 3 == 0, f'{nv // 3} sommets')
    idx = np.where(PVI < 0, -PVI - 1, PVI).astype(int)
    chk(idx.max() < nv // 3 and (PVI < 0).sum() == npv // 3, f'{(PVI < 0).sum()} triangles, indices valides')
    nn, Nn = arr('Normals'); chk(nn == npv * 3 and np.allclose(np.linalg.norm(Nn.reshape(-1, 3), axis=1), 1, atol=2e-3), 'normales par sommet de polygone, unitaires')
    _, UVI = arr('UVIndex'); chk(len(UVI) == npv, 'indices UV')
    _, MI = arr('Materials'); nmat = len(re.findall(r'^\tMaterial: ', txt, re.M)); chk(MI.max() < nmat and len(MI) == npv // 3, f'{nmat} matériaux, indices par polygone')
    clusters = re.findall(r'"Cluster" \{(.*?)\n\t\}', txt, re.S)
    if clusters:
        W = np.zeros(nv // 3)
        for c in clusters:
            _, ii = arr('Indexes', c); _, ww = arr('Weights', c); np.add.at(W, ii.astype(int), ww)
        chk(np.abs(W - 1).max() < 2e-3, f'{len(clusters)} clusters, somme des poids = 1 (écart max {np.abs(W - 1).max():.4f})')
    stacks = re.findall(r'AnimStack::([^"]+)"', txt); chk(True, f'{len(stacks)} animations : {", ".join(stacks)}')
    curves = re.findall(r'KeyTime: \*(\d+).*?KeyValueFloat: \*(\d+) \{\s*a: ([^}]*)\}', txt, re.S)
    finite = all(np.isfinite(np.array([float(x) for x in v.split(',')])).all() for _, _, v in curves[:4000])
    chk(all(a == b for a, b, _ in curves) and finite, f'{len(curves)} courbes, clés cohérentes et finies')
    print('RÉSULTAT :', 'valide' if ok else 'ERREURS'); return ok

if __name__ == '__main__':
    for p in sys.argv[1:]: print(p); check(p)
