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
    nmat = len(re.findall(r'^\tMaterial: ', txt, re.M))
    geoms = re.findall(r'\tGeometry: \d+, "Geometry::([^"]+)", "Mesh" \{(.*?)\n\t\}', txt, re.S)
    chk(len(geoms) >= 1, f'{len(geoms)} maillage(s) : {", ".join(g[0] for g in geoms)}')
    allc = re.findall(r'"SubDeformer::([^"]+)", "Cluster" \{(.*?)\n\t\}', txt, re.S)
    for gname, g in geoms:
        nv, V = arr('Vertices', g); npv, PVI = arr('PolygonVertexIndex', g)
        idx = np.where(PVI < 0, -PVI - 1, PVI).astype(int)
        chk(nv == len(V) and nv % 3 == 0 and idx.max() < nv // 3 and (PVI < 0).sum() == npv // 3, f'[{gname}] {nv // 3} sommets, {(PVI < 0).sum()} triangles, indices valides')
        nn, Nn = arr('Normals', g); chk(nn == npv * 3 and np.allclose(np.linalg.norm(Nn.reshape(-1, 3), axis=1), 1, atol=2e-3), f'[{gname}] normales unitaires par sommet de polygone')
        _, UVI = arr('UVIndex', g); chk(len(UVI) == npv, f'[{gname}] indices UV')
        _, MI = arr('Materials', g); chk(MI.max() < nmat and len(MI) == npv // 3, f'[{gname}] indices de matériaux par polygone')
        cl = [c for n, c in allc if n.endswith('_' + gname) or len(geoms) == 1]
        if cl:
            W = np.zeros(nv // 3)
            for c in cl:
                _, ii = arr('Indexes', c); _, ww = arr('Weights', c); np.add.at(W, ii.astype(int), ww)
            chk(np.abs(W - 1).max() < 2e-3, f'[{gname}] {len(cl)} clusters, somme des poids = 1 (écart max {np.abs(W - 1).max():.4f})')
    stacks = re.findall(r'AnimStack::([^"]+)"', txt); chk(True, f'{len(stacks)} animations : {", ".join(stacks)}')
    curves = re.findall(r'KeyTime: \*(\d+).*?KeyValueFloat: \*(\d+) \{\s*a: ([^}]*)\}', txt, re.S)
    finite = all(np.isfinite(np.array([float(x) for x in v.split(',')])).all() for _, _, v in curves[:4000])
    chk(all(a == b for a, b, _ in curves) and finite, f'{len(curves)} courbes, clés cohérentes et finies')
    print('RÉSULTAT :', 'valide' if ok else 'ERREURS'); return ok

if __name__ == '__main__':
    for p in sys.argv[1:]: print(p); check(p)
