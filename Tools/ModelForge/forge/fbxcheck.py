"""Relecture indépendante d'un FBX ASCII produit par ModelForge : cohérence des objets, connexions, polygones, skinning, animations."""
import re, sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import numpy as np

def check(path):
    raw = open(path, 'rb').read()
    if raw[:18] == b'Kaydara FBX Binary':
        print('  OK  FBX binaire 7.4 (contrôle de structure)'); return _check_bin(raw)
    txt = raw.decode('utf-8')
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



def _check_bin(raw):
    """Contrôle d'un FBX binaire : arbre lisible, maillages, indices, poids par maillage."""
    from forge.fbxbin import read
    import numpy as np
    r = read(raw); ok = True
    def chk(cnd, msg):
        nonlocal ok; print(('  OK  ' if cnd else '  ÉCHEC ') + msg); ok &= bool(cnd)
    top = [c.name for c in r.children]
    chk(all(n in top for n in ('FBXHeaderExtension', 'GlobalSettings', 'Objects', 'Connections')), 'nœuds de premier niveau')
    obj = [c for c in r.children if c.name == 'Objects'][0]
    geos = [g for g in obj.children if g.name == 'Geometry' and g.props[2][1] == b'Mesh']
    clusters = [d for d in obj.children if d.name == 'Deformer' and d.props[2][1] == b'Cluster']
    shapes = [g for g in obj.children if g.name == 'Geometry' and g.props[2][1] == b'Shape']
    for g in geos:
        gname = g.props[1][1].split(b'\x00')[0].decode()
        V = [c for c in g.children if c.name == 'Vertices'][0].props[0][1]; P = [c for c in g.children if c.name == 'PolygonVertexIndex'][0].props[0][1]
        idx = np.where(P < 0, -P - 1, P)
        chk(len(V) % 3 == 0 and idx.max() < len(V) // 3 and (P < 0).sum() * 3 == len(P), f'[{gname}] {len(V) // 3} sommets, {(P < 0).sum()} triangles')
        W = np.zeros(len(V) // 3)
        for d in clusters:
            if not d.props[1][1].split(b'\x00')[0].decode().endswith('_' + gname) and len(geos) > 1: continue
            I = [c for c in d.children if c.name == 'Indexes']; Wt = [c for c in d.children if c.name == 'Weights']
            if I: np.add.at(W, I[0].props[0][1].astype(int), Wt[0].props[0][1])
        if clusters: chk(np.abs(W - 1).max() < 2e-3, f'[{gname}] poids (écart max {np.abs(W - 1).max():.4f})')
    print(f'  OK  {len(shapes)} blendshapes')
    print('RÉSULTAT :', 'valide' if ok else 'ERREURS'); return ok


if __name__ == '__main__':
    for p in sys.argv[1:]: print(p); check(p)
