"""Les Terriers de l'Astral : donjon-parcours naturel à la Diablo (grottes et terriers creusés sous la forêt), et non une
suite de salles carrées. Un tracé sinueux relie l'entrée au repaire du gardien en passant par des cavernes, des goulets
étroits, des corniches et des descentes ; des terriers secondaires en cul-de-sac cachent des trésors.

Génération (graine fixe) :
  1. tracé principal (spline de Catmull-Rom sur des points déviés) + 3 terriers secondaires ;
  2. cavernes elliptiques bruitées le long du tracé ; largeur du passage variable (goulets de 3 m à galeries de 8 m) ;
  3. grille d'occupation 0,5 m → contour (marching squares) → parois rocheuses extrudées et bosselées, au pied posé sur
     le sol ; sol en relief (altitude qui descend le long du tracé, ressauts) ; masse rocheuse sombre au-dessus des
     parois (vue plongeante) ;
  4. décor fusionné : stalagmites, blocs éboulés, racines pendantes, champignons luminescents, cristaux, ossements,
     toiles ; points de lumière, camps d'ennemis et coffres décrits dans layout.json pour Unity (CaveBuilder).
Le niveau est découpé en tronçons de 48 m (une pièce par tronçon) pour l'élimination hors champ."""
import json, os
import numpy as np
from scipy import ndimage as ND
from scipy.spatial import cKDTree
from skimage import measure
from forge.geo import Mesh, concat, grid_faces, vertex_normals, unit
from forge.rig import Asset
from world.props import cylinder, cone, box
from world import materials as WM
from forge.texgen import TexSet, fbm, n01, height_to_normal, cavity_ao, voronoi_edges

ROCK, FLOOR, TOPROCK, MUSH, CRYST, ROOT, BONE, WEB = 'C_Rock', 'C_Floor', 'C_Top', 'C_Mushroom', 'C_Crystal', 'C_Root', 'D_Bone', 'C_Web'
RES = 0.5          # m par case de la grille d'occupation
CHUNK = 48.0


def catmull(pts, n=24):
    P = np.asarray(pts, float); out = []
    P = np.vstack([P[0] * 2 - P[1], P, P[-1] * 2 - P[-2]])
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for t in np.linspace(0, 1, n, endpoint=False):
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-2]); return np.array(out)


class Level:
    def __init__(self, seed=7):
        self.r = np.random.default_rng(seed)
        r = self.r
        # ---- tracé principal : progression vers le nord-est en serpentant
        way = [np.array([0.0, 0.0])]; ang = 0.3
        for k in range(11):
            ang += r.uniform(-1.4, 1.4); ang = np.clip(ang, -1.3, 1.9)
            step = r.uniform(18, 26)
            way.append(way[-1] + step * np.array([np.sin(ang), np.cos(ang)]))
        self.main = catmull(way, 30)
        self.way = np.array(way)
        # ---- largeur et altitude le long du tracé
        n = len(self.main); s = np.linspace(0, 1, n)
        self.width = 3.2 + 2.4 * n01(np.convolve(r.normal(0, 1, n + 40), np.ones(40) / 40, 'same')[20:20 + n]) + 0.0 * s
        self.width[: n // 30] = 4.0
        drops = np.zeros(n)
        for k in r.choice(np.arange(n // 8, n - n // 8), 4, replace=False): drops[k:] -= r.uniform(1.0, 2.2)     # ressauts
        self.elev = -8.0 * s + ND.gaussian_filter1d(drops, 6)
        # ---- cavernes : à l'entrée, à intervalles, salle du gardien au bout
        self.caves = [(self.main[8], 7.0, 6.0, 'entree')]
        idx = np.linspace(n * 0.14, n * 0.86, 6).astype(int)
        kinds = ['camp', 'champignons', 'camp', 'cristaux', 'camp', 'ossuaire']
        for i, kd in zip(idx, kinds):
            off = r.normal(0, 3.0, 2)
            self.caves.append((self.main[i] + off, r.uniform(8, 12), r.uniform(6, 10), kd))
        self.caves.append((self.main[-1] + unit(np.r_[self.main[-1] - self.main[-12], 0])[:2] * 6, 15.0, 12.0, 'gardien'))
        # ---- terriers secondaires (culs-de-sac à trésor)
        self.side = []
        for i in r.choice(np.arange(n // 5, n - n // 5), 3, replace=False):
            d = self.main[min(i + 3, n - 1)] - self.main[max(i - 3, 0)]; nrm = unit(np.r_[-d[1], d[0], 0])[:2] * r.choice([-1, 1])
            pts = [self.main[i]]
            for k in range(3): pts.append(pts[-1] + nrm * r.uniform(7, 10) + r.normal(0, 3, 2))
            self.side.append((catmull(pts, 16), self.elev[i]))
        self.build_grid()

    # ------------------------------------------------------------------------------------------ occupation
    def build_grid(self):
        allp = np.vstack([self.main] + [p for p, _ in self.side] + [np.array([c for c, *_ in self.caves])])
        lo = allp.min(0) - 22; hi = allp.max(0) + 22
        self.org = lo; W, H = ((hi - lo) / RES).astype(int) + 1
        xs = lo[0] + np.arange(W) * RES; zs = lo[1] + np.arange(H) * RES
        X, Z = np.meshgrid(xs, zs, indexing='xy'); Pg = np.stack([X.ravel(), Z.ravel()], 1)
        open_ = np.zeros(len(Pg), bool)
        # passage principal
        tree = cKDTree(self.main); d, i = tree.query(Pg)
        wob = 0.9 * self.noise2(Pg, 0.08, 1) + 0.5 * self.noise2(Pg, 0.25, 2)
        open_ |= d < self.width[i] + wob
        self.near_i = i
        # terriers secondaires (plus étroits)
        for p, e in self.side:
            dd, _ = cKDTree(p).query(Pg); open_ |= dd < 2.6 + 0.6 * wob
        # cavernes
        for c, ra, rb, kd in self.caves:
            q = Pg - c; ang = np.arctan2(q[:, 1], q[:, 0])
            rr = np.hypot(q[:, 0] / ra, q[:, 1] / rb)
            bump = 0.12 * np.sin(ang * 5 + ra) + 0.08 * np.sin(ang * 9 + rb) + 0.06 * self.noise2(Pg, 0.15, 3)
            open_ |= rr < 1 + bump
        G = open_.reshape(H, W)
        G = ND.binary_opening(G, iterations=1); G = ND.binary_closing(G, iterations=2)
        lab, nl = ND.label(G); keep = lab == lab[self.cell(self.main[0])]
        self.G = keep; self.xs, self.zs = xs, zs

    def cell(self, p): return (int((p[1] - self.org[1]) / RES), int((p[0] - self.org[0]) / RES))

    def noise2(self, P, f, seed):
        r = np.random.default_rng(100 + seed); k = r.normal(0, 1, (6, 2)); ph = r.uniform(0, 6.28, 6)
        return sum(np.sin(P @ (k[j] * f * (1 + j * 0.7)) + ph[j]) / (1 + j * 0.6) for j in range(6)) / 2.2

    def floor_h(self, P):
        """Altitude du sol : celle du tracé le plus proche (ou du terrier), petites bosses, cuvettes dans les cavernes."""
        d, i = cKDTree(self.main).query(P)
        h = self.elev[i] + 0.25 * self.noise2(P, 0.3, 5) + 0.15 * self.noise2(P, 1.1, 6)
        for c, ra, rb, kd in self.caves:
            q = np.hypot((P[:, 0] - c[0]) / ra, (P[:, 1] - c[1]) / rb)
            h -= 0.6 * np.clip(1 - q, 0, 1) ** 2
        return h

    # ------------------------------------------------------------------------------------------ maillages
    def meshes(self):
        G = self.G; H, W = G.shape
        out = {'floor': [], 'wall': [], 'top': []}
        # ---- sol : grille de 1 m sur les cases ouvertes (dilatées d'une case pour passer sous les parois)
        step = 2
        Gd = ND.binary_dilation(G, iterations=2)
        cells = np.argwhere(Gd[::step, ::step])
        if len(cells):
            vid = -np.ones((H // step + 2, W // step + 2), int); V = []
            for (j, i) in cells:
                for dj in (0, 1):
                    for di in (0, 1):
                        jj, ii = j + dj, i + di
                        if vid[jj, ii] < 0: vid[jj, ii] = len(V); V.append((self.xs[0] + ii * step * RES, self.zs[0] + jj * step * RES))
            V = np.array(V); F = []
            for (j, i) in cells:
                a, b, c, d = vid[j, i], vid[j, i + 1], vid[j + 1, i + 1], vid[j + 1, i]
                F += [(a, c, b), (a, d, c)]
            Y = self.floor_h(V)
            m = Mesh(np.c_[V[:, 0], Y, V[:, 1]], np.array(F), V * 0.25)
            if vertex_normals(m)[:, 1].mean() < 0: m.F = m.F[:, ::-1].copy()
            out['floor'].append(m)
        # ---- parois : contours de la zone ouverte
        cont = measure.find_contours(np.pad(G.astype(float), 1), 0.5)
        for c in cont:
            P = np.c_[self.xs[0] + (c[:, 1] - 1) * RES, self.zs[0] + (c[:, 0] - 1) * RES]
            if len(P) < 8: continue
            # rééchantillonne à ~0,6 m et lisse légèrement
            L = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
            t = np.arange(0, L[-1], 0.6); P = np.c_[np.interp(t, L, P[:, 0]), np.interp(t, L, P[:, 1])]
            P = ND.gaussian_filter1d(P, 1.2, axis=0, mode='wrap')
            n = len(P)
            T = np.roll(P, -1, 0) - np.roll(P, 1, 0); N2 = np.c_[T[:, 1], -T[:, 0]]; N2 /= np.linalg.norm(N2, axis=1, keepdims=True) + 1e-9
            # oriente la normale vers l'ouvert
            probe = P + N2 * 0.8; inside = np.array([self.open_at(q) for q in probe])
            if inside.mean() < 0.5: N2 = -N2
            base = self.floor_h(P) - 0.4
            rows = 10; hgt = 4.2 + 1.6 * n01(self.noise2(P, 0.12, 8)) + 0.8 * self.noise2(P, 0.5, 9)
            V = []; UV = []
            for k in range(rows):
                f = k / (rows - 1)
                lean = -0.6 * f ** 1.5 + 0.35 * np.sin(f * 3.1)                       # paroi bombée, sommet en retrait
                ridge = 1 - np.abs(self.noise2(P * 0.9 + f * 5, 1.1, 13))                       # arêtes rocheuses
                bulge = (0.45 * self.noise2(P + f * 3, 0.6, 10) + 0.22 * self.noise2(P * 1.7 + f, 1.5, 11) + 0.28 * ridge ** 3
                         + 0.25 * np.sign(np.sin(f * 9 + self.noise2(P, 0.4, 14) * 3)) * 0.4) * (0.3 + 0.7 * np.sin(np.pi * f))
                Q = P + N2 * (lean + bulge)[:, None] * 1.0
                y = base + hgt * f
                V.append(np.c_[Q[:, 0], y, Q[:, 1]]); UV.append(np.c_[t[:n] if len(t) >= n else np.arange(n) * 0.6, y] * 0.25)
            V = np.vstack(V); UV = np.vstack(UV)
            F = []
            for k in range(rows - 1):
                for i in range(n):
                    a = k * n + i; b = k * n + (i + 1) % n; c_ = (k + 1) * n + (i + 1) % n; d = (k + 1) * n + i
                    F += [(a, b, c_), (a, c_, d)]
            m = Mesh(V, np.array(F), UV)
            # normales vers l'ouvert
            vn = vertex_normals(m); want = np.repeat(np.c_[N2[:, 0], np.zeros(n), N2[:, 1]][None], rows, 0).reshape(-1, 3)
            if (vn * want).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
            out['wall'].append(m)
            # masse rocheuse au sommet : bandeau qui s'éloigne vers le plein
            Vt = []; top = V[(rows - 1) * n:]
            for k, dist in enumerate((0.0, 2.5, 6.0, 11.0)):
                Q = top[:, [0, 2]] - N2 * dist + N2 * 0.0
                y = top[:, 1] + 0.6 * k + 0.5 * self.noise2(Q, 0.3, 12 + k)
                Vt.append(np.c_[Q[:, 0], y, Q[:, 1]])
            Vt = np.vstack(Vt); Ft = []
            for k in range(3):
                for i in range(n):
                    a = k * n + i; b = k * n + (i + 1) % n; c_ = (k + 1) * n + (i + 1) % n; d = (k + 1) * n + i
                    Ft += [(a, b, c_), (a, c_, d)]
            mt = Mesh(Vt, np.array(Ft), Vt[:, [0, 2]] * 0.2)
            if vertex_normals(mt)[:, 1].mean() < 0: mt.F = mt.F[:, ::-1].copy()
            out['top'].append(mt)
        return out

    def open_at(self, q):
        j, i = self.cell(q)
        return 0 <= j < self.G.shape[0] and 0 <= i < self.G.shape[1] and self.G[j, i]

    def wall_dist(self):
        if not hasattr(self, '_wd'): self._wd = ND.distance_transform_edt(self.G) * RES
        return self._wd

    def free_spot(self, p, min_wall=1.5):
        j, i = self.cell(p); wd = self.wall_dist()
        return 0 <= j < wd.shape[0] and 0 <= i < wd.shape[1] and wd[j, i] >= min_wall


# ------------------------------------------------------------------------------------------- décor
def stalagmite(c, h, r):
    m = cone(c, r, h, segs=7, flare=0.25)
    m.V[:, 0] += 0.06 * h * np.sin(m.V[:, 1] * 3); return m


def mushroom(c, s):
    stem = cylinder(c, 0.05 * s, 0.35 * s, segs=6)
    cap = cone(np.asarray(c) + (0, 0.3 * s, 0), 0.22 * s, 0.14 * s, segs=8, flare=0.4)
    return stem, cap


def crystal_cluster(c, r_, s):
    out = []
    for k in range(5):
        d = unit(np.r_[r_.normal(0, 0.5), 1.0, r_.normal(0, 0.5)]); h = s * r_.uniform(0.5, 1.2)
        m = cone((0, 0, 0), 0.12 * s, h, segs=5, flare=0)
        ax = unit(np.cross([0, 1, 0], d) + 1e-6); ang = np.arccos(np.clip(d[1], -1, 1))
        K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]]); R = np.eye(3) + np.sin(ang) * K + (1 - np.cos(ang)) * K @ K
        m.V = m.V @ R.T + np.asarray(c); out.append(m)
    return concat(out)


def roots(c, rng, top):
    out = []
    for k in range(rng.integers(3, 7)):
        L = rng.uniform(1.0, 2.6); p = np.asarray(c) + np.r_[rng.normal(0, 0.6), 0, rng.normal(0, 0.6)]
        m = cylinder(p + (0, top - L, 0), 0.02, L, segs=5, r_top=0.06)
        m.V[:, 0] += 0.15 * np.sin(m.V[:, 1] * 2.5 + k); out.append(m)
    return concat(out)


def decorate(L, chunks_parts, layout):
    r = L.r; G = L.G
    def add(p2, mesh, mat):
        key = (int(np.floor(p2[0] / CHUNK)), int(np.floor(p2[1] / CHUNK)))
        chunks_parts.setdefault(key, []).append((mesh, mat))
    wd = L.wall_dist()
    opens = np.argwhere(G)
    pts = np.c_[L.xs[opens[:, 1]], L.zs[opens[:, 0]]]
    wdv = wd[opens[:, 0], opens[:, 1]]
    # au pied des parois : stalagmites, blocs, champignons, racines
    edge = pts[(wdv > 0.6) & (wdv < 1.6)]
    sel = edge[r.choice(len(edge), min(len(edge), 420), replace=False)]
    for k, p in enumerate(sel):
        y = float(L.floor_h(p[None])[0])
        kind = k % 7
        if kind in (0, 1): add(p, stalagmite((p[0], y - 0.1, p[1]), r.uniform(0.6, 2.4), r.uniform(0.2, 0.55)), ROCK)
        elif kind == 2:
            m = box((p[0], y + 0.25, p[1]), r.uniform(0.5, 1.3, 3), rot=r.uniform(0, 3)); m.V[:, 1] += 0.1 * np.sin(m.V[:, 0] * 4); add(p, m, ROCK)
        elif kind in (3, 4):
            for q in range(r.integers(2, 6)):
                st, cap = mushroom((p[0] + r.normal(0, 0.3), y - 0.02, p[1] + r.normal(0, 0.3)), r.uniform(0.6, 1.6)); add(p, st, ROOT); add(p, cap, MUSH)
            if k % 3 == 0: layout['lights'].append(dict(p=V3(p[0], y + 0.6, p[1]), color=C3(0.35, 0.9, 0.75), intensity=1.2, range=5))
        elif kind == 5: add(p, roots((p[0], 0, p[1]), r, y + 4.5), ROOT)
        else:
            for q in range(4): add(p, box((p[0] + r.normal(0, 0.4), y + 0.03, p[1] + r.normal(0, 0.4)), (0.05, 0.05, r.uniform(0.25, 0.45)), rot=r.uniform(0, 3)), BONE)
    # cavernes : décor thématique
    for c, ra, rb, kd in L.caves:
        y = float(L.floor_h(np.array([c]))[0])
        if kd in ('cristaux', 'gardien'):
            for q in range(10 if kd == 'cristaux' else 6):
                a = r.uniform(0, 6.28); p = c + np.array([np.cos(a) * ra * 0.8, np.sin(a) * rb * 0.8])
                if not L.open_at(p): continue
                yy = float(L.floor_h(p[None])[0]); add(p, crystal_cluster((p[0], yy - 0.1, p[1]), r, r.uniform(0.8, 2.0)), CRYST)
                layout['lights'].append(dict(p=V3(p[0], yy + 1.0, p[1]), color=C3(0.55, 0.7, 1.0), intensity=1.6, range=7))
        if kd == 'ossuaire':
            for q in range(40):
                p = c + r.normal(0, 1, 2) * [ra * 0.4, rb * 0.4]
                if L.open_at(p): add(p, box((p[0], y + 0.04, p[1]), (0.06, 0.06, r.uniform(0.25, 0.5)), rot=r.uniform(0, 3)), BONE)
        if kd == 'champignons':
            for q in range(14):
                p = c + r.normal(0, 1, 2) * [ra * 0.45, rb * 0.45]
                if not L.free_spot(p, 1.0): continue
                yy = float(L.floor_h(p[None])[0]); st, cap = mushroom((p[0], yy - 0.05, p[1]), r.uniform(2.0, 4.5)); add(p, st, ROOT); add(p, cap, MUSH)
                layout['lights'].append(dict(p=V3(p[0], yy + 1.5, p[1]), color=C3(0.3, 1.0, 0.7), intensity=1.8, range=8))
        layout['caves'].append(dict(kind=kd, p=V3(c[0], y, c[1]), ra=float(ra), rb=float(rb)))
    # toiles d'araignée entre parois dans les goulets
    narrow = pts[(wdv > 1.2) & (wdv < 1.8)]
    for p in narrow[r.choice(len(narrow), min(30, len(narrow)), replace=False)]:
        y = float(L.floor_h(p[None])[0])
        m = Mesh(np.array([[p[0] - 1.2, y + 1.2, p[1]], [p[0] + 1.2, y + 1.4, p[1]], [p[0] + 0.9, y + 3.2, p[1] + 0.1], [p[0] - 1.0, y + 3.0, p[1] - 0.1]]), np.array([[0, 1, 2], [0, 2, 3], [0, 2, 1], [0, 3, 2]]), np.array([[0, 0], [1, 0], [1, 1], [0, 1]]))
        add(p, m, WEB)


def V3(x, y, z): return dict(x=float(x), y=float(y), z=float(z))
def C3(r_, g, b): return dict(r=float(r_), g=float(g), b=float(b), a=1.0)


def build(seed=7):
    L = Level(seed)
    ms = L.meshes()
    chunks = {}
    def put(m, mat):
        # découpe en tronçons par centre de triangle
        cen = m.V[m.F].mean(1)
        key = np.floor(cen[:, [0, 2]] / CHUNK).astype(int)
        for k in {tuple(x) for x in key}:
            sel = np.all(key == k, axis=1)
            F = m.F[sel]; u, inv = np.unique(F, return_inverse=True)
            sub = Mesh(m.V[u], inv.reshape(-1, 3), m.UV[u])
            chunks.setdefault(k, []).append((sub, mat))
    for m in ms['floor']: put(m, FLOOR)
    for m in ms['wall']: put(m, ROCK)
    for m in ms['top']: put(m, TOPROCK)
    layout = dict(lights=[], caves=[], chunks=[])
    decorate(L, chunks, layout)
    assets = []
    for (cx, cz), parts in sorted(chunks.items()):
        a = Asset(f'T_Troncon_{cx}_{cz}'.replace('-', 'm'))
        by = {}
        for m, mat in parts: by.setdefault(mat, []).append(m)
        for mat, l in by.items():
            m = concat(l); m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
            a.add(mat.lower(), m, mat)
        assets.append(a); layout['chunks'].append(a.name)
    p0 = L.main[0]; y0 = float(L.floor_h(p0[None])[0])
    d0 = unit(np.r_[L.main[6] - L.main[0], 0])[:2]
    layout['entrance'] = V3(p0[0], y0, p0[1]); layout['entranceDir'] = V3(d0[0], 0, d0[1])
    layout['path'] = [V3(x, float(L.floor_h(np.array([[x, z]]))[0]), z) for x, z in L.main[::6]]
    bc = L.caves[-1][0]; layout['boss'] = V3(bc[0], float(L.floor_h(bc[None])[0]), bc[1])
    layout['treasure'] = []
    for p, e in L.side:
        q = p[-2]; d = p[-1] - p[-3]
        layout['treasure'].append(dict(p=V3(q[0], float(L.floor_h(q[None])[0]), q[1]), dir=V3(d[0], 0, d[1])))
    return L, assets, layout


# ------------------------------------------------------------------------------------------- matériaux
def cave_rock(S=1024, seed=4001):
    """Roche de caverne : strates obliques, fines fissures, coulées minérales humides, mousse et salpêtre dans les creux."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 6, octaves=6)); fine = n01(fbm(S, S, r, base=S / 60, octaves=3))
    yy = np.mgrid[0:S, 0:S][0] / S
    strata = n01(np.sin((yy * 22 + 2.5 * g) * np.pi) * 0.6 + fbm(S, S, r, base=S / 40, octaves=3) * 0.4)
    ridged = 1 - np.abs(fbm(S, S, r, base=S / 14, octaves=4)) * 3
    cr = np.clip((ridged - 0.86) * 9, 0, 1)
    col = np.array([0.30, 0.28, 0.26])[None, None] * (0.55 + 0.6 * g[..., None]) * (0.85 + 0.25 * strata[..., None]) * (0.9 + 0.15 * fine[..., None])
    col = col * (1 - 0.55 * cr[..., None])
    moss = np.clip((n01(fbm(S, S, r, base=S / 8, octaves=4)) - 0.68) * 3, 0, 1) * (1 - g)
    col = col * (1 - moss[..., None] * 0.55) + np.array([0.14, 0.22, 0.11]) * moss[..., None] * 0.55
    nitre = np.clip((n01(fbm(S, S, r, base=S / 12, octaves=3, aniso=(1.0, 0.15))) - 0.75) * 4, 0, 1)
    col = col * (1 - nitre[..., None] * 0.3) + np.array([0.55, 0.55, 0.5]) * nitre[..., None] * 0.3
    wet = np.clip(n01(fbm(S, S, r, base=S / 5, octaves=4, aniso=(1.0, 0.1))) - 0.5, 0, 1)
    return WM._tex(col, 1.6 * g + 0.6 * strata + 0.4 * fine - 1.2 * cr, 0.12 + 0.55 * wet, 1.0, 6, 0.2)


def cave_floor(S=1024, seed=4002):
    """Sol de terrier : terre tassée et humide, gravillons, petites pierres, fines racines, flaques sombres."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 10, octaves=5)); fine = n01(fbm(S, S, r, base=S / 120, octaves=2))
    col = np.array([0.25, 0.20, 0.15])[None, None] * (0.7 + 0.45 * g[..., None]) * (0.85 + 0.25 * fine[..., None])
    hgt = 0.8 * g + 0.4 * fine
    yy, xx = np.mgrid[0:S, 0:S]
    for k in range(260):                                                     # cailloux
        cx, cy, rad = r.uniform(0, S), r.uniform(0, S), r.uniform(2, 9) * S / 1024
        x0, x1, y0, y1 = int(max(cx - rad - 1, 0)), int(min(cx + rad + 2, S)), int(max(cy - rad - 1, 0)), int(min(cy + rad + 2, S))
        dd = np.hypot(xx[y0:y1, x0:x1] - cx, (yy[y0:y1, x0:x1] - cy) * 1.3) / rad
        m = np.clip(1 - dd, 0, 1) ** 0.5
        tone = r.uniform(0.32, 0.48)
        col[y0:y1, x0:x1] = col[y0:y1, x0:x1] * (1 - m[..., None]) + np.array([tone, tone * 0.97, tone * 0.92]) * m[..., None] * (0.8 + 0.2 * fine[y0:y1, x0:x1, None])
        hgt[y0:y1, x0:x1] += m * 1.2
    pud = np.clip((n01(fbm(S, S, r, base=S / 4, octaves=4)) - 0.74) * 6, 0, 1)
    col = col * (1 - 0.45 * pud[..., None])
    return WM._tex(col, hgt, 0.12 + 0.75 * pud, 0.9)


def glow_tex(color, S=128, seed=4003, spots=True):
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 6, octaves=3))
    col = np.array(color)[None, None] * (0.6 + 0.6 * g[..., None])
    emit = np.array(color)[None, None] * (0.5 + 0.8 * g[..., None])
    return TexSet(np.clip(col, 0, 1), height_to_normal(g, 0.3), np.zeros((S, S)), np.full((S, S), 0.6), np.ones((S, S)), np.clip(emit, 0, 1))


def web_tex(S=256, seed=4004):
    from PIL import Image, ImageDraw
    r = np.random.default_rng(seed)
    im = Image.new('L', (S, S), 0); d = ImageDraw.Draw(im); c = (S / 2, S / 2)
    for k in range(12):
        a = k / 12 * 2 * np.pi; d.line([c, (c[0] + np.cos(a) * S, c[1] + np.sin(a) * S)], fill=200, width=1)
    for rr in np.linspace(10, S * 0.7, 14):
        pts = [(c[0] + np.cos(a) * rr * (0.9 + 0.1 * np.sin(a * 3)), c[1] + np.sin(a) * rr) for a in np.linspace(0, 2 * np.pi, 13)]
        d.line(pts, fill=170, width=1)
    al = np.asarray(im, np.float32) / 255
    col = np.ones((S, S, 3)) * 0.85
    return TexSet(col, height_to_normal(np.zeros((S, S)), 0.1), np.zeros((S, S)), np.full((S, S), 0.3), np.ones((S, S)), None, al)


RECIPES = {
    ROCK: (lambda S: cave_rock(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    TOPROCK: (lambda S: cave_rock(S, seed=4011), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    FLOOR: (lambda S: cave_floor(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    MUSH: (lambda S: glow_tex((0.25, 0.95, 0.75)), 128, 64, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.25, 0.9, 0.7))),
    CRYST: (lambda S: glow_tex((0.5, 0.7, 1.0), seed=4005), 128, 64, dict(metallic=0.0, smoothness=1.0, emission=(1.8, 1.8, 1.8), emission_preview=(0.45, 0.65, 1.0))),
    ROOT: (lambda S: WM.bark(S, tone=(0.24, 0.17, 0.11)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    WEB: (lambda S: web_tex(S), 256, 128, dict(metallic=0.0, smoothness=1.0, cutoff=0.3)),
}
