"""Primitives de modélisation : superellipsoïdes, tubes à section variable, révolutions. Tout est skinnable."""
import numpy as np


def nrm(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True); return v / np.maximum(n, 1e-9)


def rot_x(a): c, s = np.cos(a), np.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
def rot_y(a): c, s = np.cos(a), np.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
def rot_z(a): c, s = np.cos(a), np.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
def smooth(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


class Part:
    def __init__(self, pos, normals, idx, mat=0, tile=1.0):
        self.pos = np.asarray(pos, np.float64).reshape(-1, 3); self.nrm = np.asarray(normals, np.float64).reshape(-1, 3)
        self.idx = np.asarray(idx, np.int64).reshape(-1, 3); self.mat = mat; self.tile = tile
        n = len(self.pos); self.joints = np.full((n, 4), "", dtype=object); self.weights = np.zeros((n, 4)); self.uv = None
        self.color = np.ones((n, 3))

    def copy(self):
        p = Part(self.pos.copy(), self.nrm.copy(), self.idx.copy(), self.mat, self.tile); p.joints = self.joints.copy(); p.weights = self.weights.copy(); p.color = self.color.copy(); return p

    def xf(self, R=None, t=(0, 0, 0), s=(1, 1, 1)):
        s = np.broadcast_to(np.asarray(s, np.float64), (3,)); P = self.pos * s
        N = self.nrm / s
        if R is not None: P = P @ np.asarray(R).T; N = N @ np.asarray(R).T
        self.pos = P + np.asarray(t, np.float64); self.nrm = nrm(N); return self

    def set_mat(self, m, tile=None):
        self.mat = m
        if tile: self.tile = tile
        return self

    def tint(self, rgb): self.color = np.broadcast_to(np.asarray(rgb, np.float64), self.color.shape).copy(); return self

    def skin(self, bone):
        self.joints[:] = ""; self.joints[:, 0] = bone; self.weights[:] = 0; self.weights[:, 0] = 1
        for k in range(1, 4): self.joints[:, k] = bone
        return self

    def skin_chain(self, bones, origin, direction, transitions):
        """bones: [b0,b1,..]; transitions: [(s0,s1),..] le long de l'axe (origin,direction)."""
        d = np.asarray(direction, np.float64); d = d / np.linalg.norm(d); s = (self.pos - np.asarray(origin)) @ d
        a = [smooth((s - t0) / (t1 - t0)) for t0, t1 in transitions]
        w = [1 - a[0]] + [a[i - 1] - a[i] for i in range(1, len(a))] + [a[-1]]
        self.joints[:] = ""; self.weights[:] = 0
        for i, b in enumerate(bones): self.joints[:, i] = b; self.weights[:, i] = w[i]
        for i in range(len(bones), 4): self.joints[:, i] = bones[0]
        return self

    def finalize_uv(self):
        n = np.abs(self.nrm); ax = n.argmax(1); P = self.pos * self.tile
        self.uv = np.where((ax == 0)[:, None], P[:, [2, 1]], np.where((ax == 2)[:, None], P[:, [0, 1]], P[:, [0, 2]])); return self


def _grid(P, closed_u=True, fallback=None):
    V, U, _ = P.shape
    du = (np.roll(P, -1, 1) - np.roll(P, 1, 1)) if closed_u else np.gradient(P, axis=1)
    dv = np.gradient(P, axis=0)
    n = np.cross(du, dv); l = np.linalg.norm(n, axis=-1, keepdims=True)
    if fallback is not None: n = np.where(l < 1e-10, fallback, n)
    n = nrm(n)
    idx = []
    Ue = U if closed_u else U - 1
    for j in range(V - 1):
        for i in range(Ue):
            i2 = (i + 1) % U; a = j * U + i; b = j * U + i2; c = (j + 1) * U + i; d = (j + 1) * U + i2
            idx += [[a, c, b], [b, c, d]]
    return P.reshape(-1, 3), n.reshape(-1, 3), np.array(idx)


def _spow(x, e): return np.sign(x) * np.abs(x) ** e


def superellipsoid(radii=(0.5, 0.5, 0.5), n1=1.0, n2=1.0, res=(14, 24), center=(0, 0, 0), mat=0, tile=1.0, flip=False):
    V, U = res
    v = np.linspace(-np.pi / 2, np.pi / 2, V)[:, None]; u = np.linspace(0, 2 * np.pi, U, endpoint=False)[None, :]
    cv, sv = np.cos(v), np.sin(v); cu, su = np.cos(u), np.sin(u)
    x = _spow(cv, n1) * _spow(cu, n2); z = _spow(cv, n1) * _spow(su, n2); y = _spow(sv, n1) * np.ones_like(u)
    P = np.stack([x * radii[0], y * radii[1], z * radii[2]], -1) + np.asarray(center)
    fb = nrm(np.stack([x / radii[0], y / radii[1], z / radii[2]], -1))
    Pf, N, I = _grid(P, True, fb)
    if flip: N = -N; I = I[:, ::-1]
    return Part(Pf, N, I, mat, tile)


def box(size, bevel=0.5, res=(10, 16), center=(0, 0, 0), mat=0, tile=1.0):
    n = max(0.12, bevel)
    return superellipsoid((size[0] / 2, size[1] / 2, size[2] / 2), n1=n, n2=n, res=res, center=center, mat=mat, tile=tile)


def tube(points, radii, sides=14, up=(0, 0, 1), cap_start=True, cap_end=True, mat=0, tile=1.0, twist=0.0):
    """points (K,3), radii (K,2) = (rayon latéral B, rayon selon up N). Extrémités arrondies."""
    P0 = np.asarray(points, np.float64); R0 = np.asarray(radii, np.float64)
    if R0.ndim == 1: R0 = np.stack([R0, R0], -1)
    T = nrm(np.gradient(P0, axis=0)); upv = np.asarray(up, np.float64)
    ring_c, ring_r, ring_t = [], [], []
    def caps(c, r, t, sign):
        return [(c + sign * t * r.mean() * np.sin(ph) * 0.9, r * np.cos(ph), t) for ph in (1.2, 0.8, 0.4, 0.12)]
    if cap_start:
        for c, r, t in reversed(caps(P0[0], R0[0], T[0], -1)): ring_c.append(c); ring_r.append(r); ring_t.append(t)
    for i in range(len(P0)): ring_c.append(P0[i]); ring_r.append(R0[i]); ring_t.append(T[i])
    if cap_end:
        for c, r, t in caps(P0[-1], R0[-1], T[-1], 1): ring_c.append(c); ring_r.append(r); ring_t.append(t)
    th = np.linspace(0, 2 * np.pi, sides, endpoint=False) + twist
    rows = []
    for c, r, t in zip(ring_c, ring_r, ring_t):
        N = upv - np.dot(upv, t) * t
        if np.linalg.norm(N) < 1e-6: N = np.array([1.0, 0, 0]) - np.dot([1.0, 0, 0], t) * t
        N = N / np.linalg.norm(N); B = np.cross(t, N)
        rows.append(c + np.outer(np.cos(th), N) * r[1] + np.outer(np.sin(th), B) * r[0])
    P = np.array(rows)
    fb = nrm(P - np.array(ring_c)[:, None, :])
    Pf, N, I = _grid(P, True, fb)
    return Part(Pf, N, I, mat, tile)


def lathe(profile, sides=24, mat=0, tile=1.0, scale=(1, 1, 1), center=(0, 0, 0), arc=None):
    """profile [(rayon, y)] de bas en haut. arc=(a0,a1) pour une portion (non fermée)."""
    pr = np.asarray(profile, np.float64)
    if arc is None: th = np.linspace(0, 2 * np.pi, sides, endpoint=False); closed = True
    else: th = np.linspace(arc[0], arc[1], sides); closed = False
    P = np.stack([np.outer(pr[:, 0], np.cos(th)) * scale[0], np.repeat(pr[:, 1:2], len(th), 1) * scale[1], np.outer(pr[:, 0], np.sin(th)) * scale[2]], -1) + np.asarray(center)
    fb = nrm(np.stack([np.outer(np.ones(len(pr)), np.cos(th)), np.zeros((len(pr), len(th))), np.outer(np.ones(len(pr)), np.sin(th))], -1))
    Pf, N, I = _grid(P, closed, fb)
    return Part(Pf, N, I, mat, tile)


def merge(parts):
    out = parts[0].copy()
    for p in parts[1:]:
        off = len(out.pos)
        out.pos = np.concatenate([out.pos, p.pos]); out.nrm = np.concatenate([out.nrm, p.nrm]); out.idx = np.concatenate([out.idx, p.idx + off])
        out.joints = np.concatenate([out.joints, p.joints]); out.weights = np.concatenate([out.weights, p.weights]); out.color = np.concatenate([out.color, p.color])
    return out


def mirror(p):
    q = p.copy(); q.pos[:, 0] *= -1; q.nrm[:, 0] *= -1; q.idx = q.idx[:, ::-1]
    for i in range(q.joints.shape[0]):
        for k in range(4):
            b = q.joints[i, k]
            if b.endswith("_L"): q.joints[i, k] = b[:-2] + "_R"
            elif b.endswith("_R"): q.joints[i, k] = b[:-2] + "_L"
    return q
