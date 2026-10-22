"""Rendu logiciel (z-buffer) pour contrôler les modèles sans moteur 3D. Gère le skinnage pour vérifier les animations."""
import numpy as np
from PIL import Image
from geo import nrm


def quat_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def world_mats(bones, rots=None, trans=None):
    """Matrices 4x4 monde par os (skin = world * invBind). rots: nom -> quaternion local."""
    rots = rots or {}; trans = trans or {}; W = {}
    def get(n):
        if n in W: return W[n]
        p, head = bones[n]; ph = bones[p][1] if p else (0, 0, 0)
        L = np.eye(4); L[:3, :3] = quat_mat(rots.get(n, (0, 0, 0, 1))); L[:3, 3] = np.array(head) - np.array(ph) + np.array(trans.get(n, (0, 0, 0)))
        W[n] = (get(p) if p else np.eye(4)) @ L; return W[n]
    for n in bones: get(n)
    return W


def skin_part(p, bones, W):
    out = np.zeros_like(p.pos); on = np.zeros_like(p.nrm)
    for k in range(4):
        w = p.weights[:, k]
        if not w.any(): continue
        for b in set(p.joints[:, k]):
            sel = (p.joints[:, k] == b) & (w > 0)
            if not sel.any(): continue
            head = np.array(bones[b][1]); M = W[b]
            lp = p.pos[sel] - head; out[sel] += w[sel, None] * (lp @ M[:3, :3].T + M[:3, 3])
            on[sel] += w[sel, None] * (p.nrm[sel] @ M[:3, :3].T)
    return out, nrm(on)


def render(items, size=(640, 800), yaw=0.5, pitch=0.12, center=(0, 0.9, 0), dist=3.6, fov=1.6, bg=(34, 36, 46), light=(0.5, 0.8, 0.6)):
    """items : liste de (pos, nrm, idx, rgb(3), emissive?)"""
    Wd, Hd = size; img = np.zeros((Hd, Wd, 3), np.float64); img[:] = np.array(bg) / 255.0; zb = np.full((Hd, Wd), 1e9)
    cy, sy, cp, sp = np.cos(yaw), np.sin(yaw), np.cos(pitch), np.sin(pitch)
    R = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]) @ np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]])
    L = nrm(np.array(light)); L2 = nrm(np.array([-0.6, 0.2, -0.5]))
    f = Hd * 0.5 * fov
    for pos, nr, idx, col in items:
        P = (pos - np.array(center)) @ R.T; N = nr @ R.T; P[:, 2] += dist
        sx = Wd / 2 - P[:, 0] / P[:, 2] * f; sy_ = -P[:, 1] / P[:, 2] * f + Hd / 2
        lum = np.clip(N @ (R @ L), 0, 1) * 0.75 + np.clip(N @ (R @ L2), 0, 1) * 0.25 + 0.22 + 0.12 * N[:, 1]
        spec = np.clip(N @ nrm(R @ L + np.array([0, 0, -1.0])), 0, 1) ** 24 * 0.25
        for tri in idx:
            a, b, c = tri; xs = sx[[a, b, c]]; ys = sy_[[a, b, c]]; zs = P[[a, b, c], 2]
            if (zs <= 0.05).any(): continue
            x0, x1 = int(max(0, np.floor(xs.min()))), int(min(Wd - 1, np.ceil(xs.max()))); y0, y1 = int(max(0, np.floor(ys.min()))), int(min(Hd - 1, np.ceil(ys.max())))
            if x0 > x1 or y0 > y1: continue
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-9: continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            w0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den; w1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den; w2 = 1 - w0 - w1
            m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not m.any(): continue
            z = w0 * zs[0] + w1 * zs[1] + w2 * zs[2]; sub = zb[y0:y1 + 1, x0:x1 + 1] if False else zb_view(zb, y0, y1, x0, x1)
            upd = m & (z < sub)
            if not upd.any(): continue
            l = (w0 * lum[a] + w1 * lum[b] + w2 * lum[c]); s_ = (w0 * spec[a] + w1 * spec[b] + w2 * spec[c])
            for ch in range(3): img[y0:y1 + 1, x0:x1 + 1, ch][upd] = np.clip(col[ch] * l[upd] + s_[upd], 0, 1)
            sub[upd] = z[upd]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def zb_view(zb, y0, y1, x0, x1): return zb[y0:y1 + 1, x0:x1 + 1]
