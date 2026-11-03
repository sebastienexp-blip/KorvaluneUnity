"""Couches de terrain « haute définition » : au lieu d'un bruit coloré, le sol est composé d'éléments dessinés un par un
(brins d'herbe effilés avec pointe éclairée, terre visible entre les touffes, cailloux, trèfles, brindilles), empilés par
hauteur comme dans une photogrammétrie. Chaque couche produit aussi sa carte de masque de terrain URP
(R métal, G occlusion, B hauteur, A lissé) pour le mélange par hauteur entre couches.

Tout est raccordable (les coordonnées bouclent) : aucune couture visible quand la texture se répète."""
import os
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
from forge.texgen import fbm, n01, height_to_normal, cavity_ao, voronoi_edges


class Layer:
    def __init__(self, S, soil, soil_h):
        self.S = S
        self.col = soil.copy(); self.h = soil_h.copy()
        self.smooth = np.full((S, S), 0.12)

    def stamp(self, ys, xs, hs, cols, smooth=None):
        """Dépose des points (y, x) de hauteur hs : chaque pixel garde le point le plus haut (tri croissant puis
        affectation, la dernière écriture l'emporte)."""
        S = self.S
        ys = np.mod(np.round(ys).astype(np.int64), S); xs = np.mod(np.round(xs).astype(np.int64), S)
        keep = hs >= self.h[ys, xs]
        ys, xs, hs, cols = ys[keep], xs[keep], hs[keep], cols[keep]
        o = np.argsort(hs, kind='stable')
        ys, xs, hs, cols = ys[o], xs[o], hs[o], cols[o]
        self.h[ys, xs] = hs; self.col[ys, xs] = cols
        if smooth is not None: self.smooth[ys, xs] = smooth[keep][o] if np.ndim(smooth) else smooth


def _blades(L, r, n, length, width, hue_fn, base_h=0.25, lean=0.5, clump=None):
    """n brins : segments effilés, légèrement courbés, plus clairs vers la pointe. hue_fn(u, v, k) → couleurs (k, 3)."""
    S = L.S
    if clump is not None:   # positions concentrées en touffes
        cx = r.uniform(0, S, clump[0]); cy = r.uniform(0, S, clump[0])
        pick = r.integers(0, clump[0], n)
        x0 = cx[pick] + r.normal(0, clump[1], n); y0 = cy[pick] + r.normal(0, clump[1], n)
    else:
        x0 = r.uniform(0, S, n); y0 = r.uniform(0, S, n)
    ang = r.uniform(0, 2 * np.pi, n)
    ln = length * r.uniform(0.55, 1.25, n)
    bend = r.normal(0, lean, n)
    shade = r.uniform(0, 1, n)
    steps = max(6, int(length * 1.3))
    t = np.linspace(0, 1, steps)
    ys, xs, hs, cs = [], [], [], []
    for w_off in np.linspace(-0.5, 0.5, max(1, int(round(width)))):
        a = ang[:, None] + bend[:, None] * t[None, :]
        px = x0[:, None] + np.cos(a) * ln[:, None] * t[None, :] + np.cos(ang[:, None] + np.pi / 2) * w_off * (1 - t[None, :])
        py = y0[:, None] + np.sin(a) * ln[:, None] * t[None, :] + np.sin(ang[:, None] + np.pi / 2) * w_off * (1 - t[None, :])
        hh = base_h + t[None, :] * (0.55 + 0.25 * shade[:, None]) + 0.02 * r.random((n, 1))
        col = hue_fn(x0 % S, y0 % S, shade)                          # (n, 3) : teinte prise au pied du brin
        col = col[:, None, :] * (0.55 + 0.75 * t[None, :, None])    # pied sombre, pointe éclairée
        ys.append(py.ravel()); xs.append(px.ravel()); hs.append(hh.ravel()); cs.append(col.reshape(-1, 3))
    L.stamp(np.concatenate(ys), np.concatenate(xs), np.concatenate(hs), np.concatenate(cs))


def _pebbles(L, r, n, rmin, rmax, tone, var=0.15):
    S = L.S
    for _ in range(n):
        cx, cy = r.uniform(0, S, 2); rad = r.uniform(rmin, rmax); el = r.uniform(0.6, 1.0); a = r.uniform(0, np.pi)
        R = int(rad) + 2
        yy, xx = np.mgrid[-R:R + 1, -R:R + 1].astype(float)
        u = (xx * np.cos(a) + yy * np.sin(a)) / rad; v = (-xx * np.sin(a) + yy * np.cos(a)) / (rad * el)
        d = u * u + v * v; m = d < 1
        hh = 0.35 + 0.5 * np.sqrt(np.clip(1 - d, 0, 1))
        light = 0.8 + 0.25 * np.clip(-(u + v) * 0.5 + 0.5, 0, 1)
        c = np.array(tone) * (1 + r.uniform(-var, var)) * light[..., None]
        L.stamp(cy + yy[m], cx + xx[m], hh[m], c[m], smooth=np.full(m.sum(), 0.35))


def _clovers(L, r, n, size, col):
    S = L.S
    for _ in range(n):
        cx, cy = r.uniform(0, S, 2); rot = r.uniform(0, 2 * np.pi); s = size * r.uniform(0.7, 1.3)
        for k in range(3):
            a = rot + k * 2 * np.pi / 3
            lx, ly = cx + np.cos(a) * s * 0.7, cy + np.sin(a) * s * 0.7
            R = int(s) + 1
            yy, xx = np.mgrid[-R:R + 1, -R:R + 1].astype(float)
            d = (xx ** 2 + yy ** 2) / (s * 0.62) ** 2; m = d < 1
            c = np.array(col) * (0.8 + 0.35 * (1 - d[m]))[:, None] * r.uniform(0.85, 1.15)
            L.stamp(ly + yy[m], lx + xx[m], 0.5 + 0.2 * (1 - d[m]), c)


def grass(S=1024, seed=2101):
    """Prairie de la vallée : terre sombre, mousse rase, trois générations de brins (anciens jaunis, verts, jeunes
    clairs) groupés en touffes, trèfles, cailloux épars, zones sèches."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 6, octaves=5))
    dry = np.clip((n01(fbm(S, S, r, base=S / 3, octaves=4)) - 0.55) * 3.0, 0, 1)
    soil = np.array([0.20, 0.15, 0.09])[None, None] * (0.7 + 0.5 * n01(fbm(S, S, r, base=S / 30, octaves=4)))[..., None]
    moss = np.array([0.17, 0.22, 0.08])[None, None] * (0.75 + 0.4 * g[..., None])
    mm = np.clip(n01(fbm(S, S, r, base=S / 12, octaves=4)) * 1.6 - 0.3, 0, 1)[..., None]
    L = Layer(S, soil * (1 - mm) + moss * mm, 0.1 * g + 0.05)
    _pebbles(L, r, 160, 2.0, 6, (0.30, 0.28, 0.24))

    def hue(px, py, shade, base, alt):
        gi = g[py.astype(int) % S, px.astype(int) % S]; di = dry[py.astype(int) % S, px.astype(int) % S]
        c = np.array(base)[None] * (1 - gi[:, None] * 0.5) + np.array(alt)[None] * gi[:, None] * 0.5
        c = c * (1 - 0.7 * di[:, None]) + np.array([0.50, 0.44, 0.24])[None] * 0.7 * di[:, None]
        return c * (0.8 + 0.35 * shade[:, None])

    # brins anciens, jaunis et couchés
    _blades(L, r, 9000, 26, 2, lambda x, y, s: hue(x, y, s, (0.40, 0.37, 0.19), (0.33, 0.30, 0.15)), base_h=0.15, lean=0.9)
    # masse verte en touffes
    _blades(L, r, 30000, 22, 2, lambda x, y, s: hue(x, y, s, (0.20, 0.33, 0.09), (0.29, 0.40, 0.12)), base_h=0.25, lean=0.6, clump=(900, 7))
    _clovers(L, r, 260, 5.5, (0.15, 0.27, 0.08))
    # jeunes pousses claires
    _blades(L, r, 14000, 16, 1, lambda x, y, s: hue(x, y, s, (0.34, 0.47, 0.15), (0.40, 0.50, 0.18)), base_h=0.35, lean=0.4, clump=(1400, 5))
    col = L.col * (0.92 + 0.16 * g[..., None])
    h = gaussian_filter(L.h, 0.6, mode='wrap')
    smooth = np.clip(0.10 + 0.25 * (h - h.min()) / (np.ptp(h) + 1e-6) * 0.4, 0, 1)
    return col, h, smooth


def forest_floor(S=1024, seed=2102):
    """Sous-bois : humus, feuilles mortes ovales empilées (brunes, rousses, quelques jaunes), aiguilles de pin,
    brindilles, mousse."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 5, octaves=5))
    soil = np.array([0.17, 0.12, 0.08])[None, None] * (0.7 + 0.5 * n01(fbm(S, S, r, base=S / 40, octaves=3)))[..., None]
    L = Layer(S, soil, 0.05 * g)
    tones = np.array([[0.34, 0.20, 0.09], [0.42, 0.26, 0.10], [0.27, 0.17, 0.09], [0.50, 0.38, 0.14], [0.22, 0.15, 0.08]])
    NL = 6500
    for k in range(NL):
        cx, cy = r.uniform(0, S, 2); ln = r.uniform(6, 19); wd = ln * r.uniform(0.35, 0.5); a = r.uniform(0, np.pi * 2)
        R = int(ln) + 2
        yy, xx = np.mgrid[-R:R + 1, -R:R + 1].astype(float)
        u = (xx * np.cos(a) + yy * np.sin(a)) / ln; v = (-xx * np.sin(a) + yy * np.cos(a)) / wd
        v = v / np.clip(1.15 - np.abs(u) ** 1.5 * (0.8 + 0.6 * (u > 0)), 0.15, 2)   # pointe et pétiole
        d = u * u + v * v; m = d < 1
        vein = np.exp(-(v * 6) ** 2) * 0.25
        c = tones[r.integers(0, len(tones))] * r.uniform(0.8, 1.15) * (0.85 + 0.25 * (1 - d) - vein)[..., None]
        L.stamp(cy + yy[m], cx + xx[m], (k / NL) * 0.5 + 0.2 + 0.08 * (1 - d[m]), c[m])
    _blades(L, r, 5000, 14, 1, lambda x, y, s: np.array([0.30, 0.22, 0.12])[None] * (0.7 + 0.5 * s[:, None]), base_h=0.72, lean=0.1)  # aiguilles
    _blades(L, r, 300, 40, 2, lambda x, y, s: np.array([0.25, 0.18, 0.11])[None] * (0.8 + 0.3 * s[:, None]), base_h=0.75, lean=0.2)   # brindilles
    mossm = np.clip((n01(fbm(S, S, r, base=S / 6, octaves=5)) - 0.6) * 2.5, 0, 1)
    mt = n01(fbm(S, S, r, base=S / 90, octaves=3))
    col = L.col * (1 - mossm[..., None]) + np.array([0.16, 0.23, 0.07])[None, None] * (0.6 + 0.7 * mt[..., None]) * mossm[..., None]
    h = gaussian_filter(L.h * (1 - 0.5 * mossm) + 0.3 * mossm, 0.6, mode='wrap')
    return col, h, np.clip(0.15 + 0.15 * mossm, 0, 1)


def dirt(S=1024, seed=2103):
    """Chemin de terre battue : terre compacte, gravier de toutes tailles, ornières, quelques brins en bordure."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 8, octaves=6))
    crack = gaussian_filter(voronoi_edges(S, S, r, 120), 0.7, mode='wrap')
    fine = n01(fbm(S, S, r, base=S / 160, octaves=3)); damp = np.clip((n01(fbm(S, S, r, base=S / 5, octaves=4)) - 0.6) * 2.5, 0, 1)
    soil = np.array([0.33, 0.25, 0.16])[None, None] * (0.75 + 0.4 * g[..., None]) * (0.82 + 0.3 * fine[..., None]) * (1 - 0.35 * n01(crack)[..., None] ** 3) * (1 - 0.3 * damp[..., None])
    L = Layer(S, soil, 0.15 * g + 0.06 * fine - 0.1 * n01(crack) ** 3)
    L.smooth = 0.12 + 0.3 * damp
    _pebbles(L, r, 2600, 1.0, 2.6, (0.40, 0.34, 0.26), 0.3)
    _pebbles(L, r, 700, 2.0, 4.5, (0.36, 0.31, 0.25), 0.25)
    _pebbles(L, r, 90, 4, 9, (0.36, 0.33, 0.28), 0.2)
    _blades(L, r, 2500, 14, 1, lambda x, y, s: np.array([0.30, 0.34, 0.14])[None] * (0.7 + 0.5 * s[:, None]), base_h=0.3, lean=0.6, clump=(120, 6))
    h = gaussian_filter(L.h, 0.6, mode='wrap')
    return L.col, h, np.clip(L.smooth, 0, 1)


def save(folder, prefix, col, h, smooth, strength=0.9):
    os.makedirs(folder, exist_ok=True)
    u8 = lambda a: (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    hn = (h - h.min()) / (np.ptp(h) + 1e-6)
    ao = np.clip(cavity_ao(hn, 4, 0.12), 0, 1)
    nrm = height_to_normal(hn * 2.0, strength)
    Image.fromarray(u8(col)).save(os.path.join(folder, prefix + '_BaseMap.png'), optimize=True)
    Image.fromarray(u8(nrm)).save(os.path.join(folder, prefix + '_Normal.png'), optimize=True)
    zero = np.zeros_like(hn)
    Image.fromarray(u8(np.dstack([zero, zero, zero, smooth])), 'RGBA').save(os.path.join(folder, prefix + '_MetallicSmoothness.png'), optimize=True)
    Image.fromarray(u8(ao), 'L').save(os.path.join(folder, prefix + '_Occlusion.png'), optimize=True)
    Image.fromarray(u8(np.dstack([zero, ao, hn, smooth])), 'RGBA').save(os.path.join(folder, prefix + '_Mask.png'), optimize=True)


if __name__ == '__main__':
    import sys
    out = sys.argv[1] if len(sys.argv) > 1 else 'out/terrain_hd'
    S = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
    for name, fn in (('Herbe', grass), ('SousBois', forest_floor), ('Chemin', dirt)):
        col, h, sm = fn(S); save(out, name, col, h, sm); print('couche', name, flush=True)
