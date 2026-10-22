"""Squelette, quaternions, cinématique directe et description d'un asset (maillages + matériaux + animations)."""
import numpy as np


def qmul(a, b):
    x1, y1, z1, w1 = np.moveaxis(np.asarray(a, float), -1, 0); x2, y2, z2, w2 = np.moveaxis(np.asarray(b, float), -1, 0)
    return np.stack([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
                     w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2], -1)


def qaxis(axis, ang):
    axis = np.asarray(axis, float); axis = axis / np.linalg.norm(axis); s = np.sin(ang / 2)
    return np.array([axis[0] * s, axis[1] * s, axis[2] * s, np.cos(ang / 2)])


def qeuler(rx=0.0, ry=0.0, rz=0.0):
    """Angles en DEGRÉS, appliqués X puis Y puis Z (R = Rz·Ry·Rx) : même convention que l'ordre FBX « XYZ »."""
    r = np.radians
    return qmul(qaxis((0, 0, 1), r(rz)), qmul(qaxis((0, 1, 0), r(ry)), qaxis((1, 0, 0), r(rx))))


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def mat_euler_xyz(R):
    """Matrice → angles (degrés) pour R = Rz·Ry·Rx."""
    y = np.arcsin(np.clip(-R[2, 0], -1, 1))
    if abs(R[2, 0]) < 0.99999:
        x = np.arctan2(R[2, 1], R[2, 2]); z = np.arctan2(R[1, 0], R[0, 0])
    else:
        x = np.arctan2(-R[1, 2], R[1, 1]); z = 0.0
    return np.degrees([x, y, z])


def slerp(a, b, t):
    a = np.asarray(a, float); b = np.asarray(b, float)
    if np.dot(a, b) < 0: b = -b
    d = np.clip(np.dot(a, b), -1, 1)
    if d > 0.9995: q = a + (b - a) * t; return q / np.linalg.norm(q)
    th = np.arccos(d)
    return (np.sin((1 - t) * th) * a + np.sin(t * th) * b) / np.sin(th)


class Skeleton:
    def __init__(self):
        self.names, self.parents, self.pos, self.rest = [], [], [], []   # pos : position GLOBALE de repos ; rest : rotation locale de repos
        self.null = []        # points d'attache (aucune influence de skinning)

    def add(self, name, parent, pos, rest=(0, 0, 0, 1), null=False):
        self.names.append(name); self.parents.append(-1 if parent is None else self.names.index(parent))
        self.pos.append(np.asarray(pos, float)); self.rest.append(np.asarray(rest, float)); self.null.append(null)
        return len(self.names) - 1

    def __getitem__(self, n): return self.names.index(n)

    def local_t(self, i):
        p = self.parents[i]
        if p < 0: return self.pos[i].copy()
        # repos des os articulés : rotations nulles → décalage en repère monde = décalage local, sauf sous un nœud tourné
        Gp = self.rest_global()[p]
        return np.linalg.inv(Gp)[:3, :3] @ (self.pos[i] - self.pos[p])

    def rest_global(self):
        if getattr(self, '_rg', None) is not None and len(self._rg) == len(self.names): return self._rg
        G = []
        for i in range(len(self.names)):
            M = np.eye(4); M[:3, :3] = qmat(self.rest[i]); M[:3, 3] = self.pos[i]
            G.append(M)
        self._rg = G
        return G

    def fk(self, local_rot=None, root_off=None):
        """local_rot : {nom: quat} rotation locale ANIMÉE (remplace le repos) ; root_off : décalage de la racine (m).
        Renvoie la liste des matrices globales 4x4."""
        local_rot = local_rot or {}
        G = [None] * len(self.names)
        for i, n in enumerate(self.names):
            q = local_rot.get(n, self.rest[i])
            M = np.eye(4); M[:3, :3] = qmat(q); M[:3, 3] = self.local_t(i)
            if self.parents[i] < 0:
                if root_off is not None: M[:3, 3] = M[:3, 3] + root_off
                G[i] = M
            else:
                G[i] = G[self.parents[i]] @ M
        return G


class Clip:
    """Animation échantillonnée : rot[nom] (n,4) rotations locales ; pos[nom] (n,3) translations locales (racine)."""
    def __init__(self, name, fps, n, loop=False):
        self.name, self.fps, self.n, self.loop = name, fps, n, loop
        self.rot, self.pos = {}, {}

    @property
    def duration(self): return (self.n - 1) / self.fps


class MaterialDef:
    def __init__(self, name, textures=None, color=(1, 1, 1, 1), metallic=0.0, smoothness=0.5, emission=None,
                 double_sided=False, normal_scale=1.0, tiling=(1, 1), images=None, cutoff=None):
        self.name = name; self.textures = textures or {}; self.color = color; self.metallic = metallic
        self.smoothness = smoothness; self.emission = emission; self.double_sided = double_sided
        self.normal_scale = normal_scale; self.tiling = tiling; self.cutoff = cutoff
        if cutoff is not None: self.double_sided = True
        self.images = images or {}   # aperçu : {'base': rgb float, 'normal':…, 'ms': (métal, lissé)} en mémoire


class Asset:
    def __init__(self, name, skeleton=None):
        self.name = name; self.skeleton = skeleton
        self.parts = []          # (nom, Mesh, matériau)
        self.materials = {}      # nom → MaterialDef
        self.clips = {}          # nom → Clip

    def add(self, name, mesh, mat):
        self.parts.append((name, mesh, mat)); return mesh

    def by_material(self):
        from .geo import concat
        out = {}
        for n, m, mat in self.parts: out.setdefault(mat, []).append(m)
        return {k: concat(v) for k, v in out.items()}

    def stats(self):
        tris = sum(len(m.F) for _, m, _ in self.parts); verts = sum(len(m.V) for _, m, _ in self.parts)
        return dict(triangles=int(tris), vertices=int(verts), materials=len({p[2] for p in self.parts}),
                    bones=0 if self.skeleton is None else int(sum(1 for x in self.skeleton.null if not x)), clips=len(self.clips))
