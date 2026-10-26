"""Matériaux du monde de Korvalune : couches de terrain (herbe de la vallée, sous-bois, chemin de terre, roche des
Pics Sélénites, sable du Désert des Étoiles), pierre de taille du château, ardoises des toits, écorce, feuillages."""
import numpy as np
from scipy.ndimage import gaussian_filter
from forge.texgen import TexSet, fbm, noise, n01, height_to_normal, cavity_ao, voronoi_edges, scratches


def _tex(base, hgt, smooth, strength=0.6, ao_r=4, ao_s=0.1, alpha=None, emit=None):
    S = base.shape[0]
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, strength), np.zeros((S, S)), np.clip(np.broadcast_to(smooth, (S, S)), 0, 1).copy(), np.clip(cavity_ao(hgt, ao_r, ao_s), 0, 1), emit, alpha)


def grass(S=1024, seed=1001, c1=(0.20, 0.36, 0.09), c2=(0.36, 0.48, 0.14), dry=(0.48, 0.44, 0.20)):
    """Herbe grasse de la vallée : touffes de brins (bruit anisotrope multi-orientation), trèfles plus sombres, zones sèches."""
    r = np.random.default_rng(seed)
    blades = np.zeros((S, S))
    for ang in (0.0, 0.6, -0.5, 1.2):
        b = fbm(S, S, r, base=S / 90, octaves=3, aniso=(0.15, 1.0))
        blades += np.abs(np.rot90(b, k=int(ang * 2) % 4)) if ang else np.abs(b)
    blades = n01(blades)
    patch = n01(fbm(S, S, r, base=S / 4, octaves=5))
    dryz = np.clip((n01(fbm(S, S, r, base=S / 3, octaves=4)) - 0.62) * 3, 0, 1)
    col = np.array(c1)[None, None] * (1 - patch[..., None]) + np.array(c2)[None, None] * patch[..., None]
    col = col * (1 - 0.6 * dryz[..., None]) + np.array(dry)[None, None] * 0.6 * dryz[..., None]
    col = col * (0.55 + 0.6 * blades[..., None])
    return _tex(col, 2.0 * blades + 0.5 * patch, 0.18 + 0.1 * blades, 0.7)


def forest_floor(S=1024, seed=1002):
    """Sous-bois de la Forêt de l'Astral : feuilles mortes, mousse, brindilles."""
    r = np.random.default_rng(seed)
    cells = n01(gaussian_filter(voronoi_edges(S, S, r, 900), 0.8, mode='wrap'))
    moss = np.clip((n01(fbm(S, S, r, base=S / 5, octaves=5)) - 0.45) * 2.5, 0, 1)
    tw = gaussian_filter(scratches(S, S, r, 500, (0.02, 0.06), (1, 2)), 0.6, mode='wrap')
    leaf = np.array([0.30, 0.20, 0.10])[None, None] * (0.6 + 0.6 * n01(fbm(S, S, r, base=S / 40, octaves=3)))[..., None]
    col = leaf * (1 - moss[..., None]) + np.array([0.16, 0.24, 0.07])[None, None] * moss[..., None]
    col = col * (0.7 + 0.3 * (1 - cells)[..., None]) * (1 - 0.4 * tw[..., None])
    return _tex(col, 1.2 * (1 - cells) + 0.8 * moss + tw, 0.2 + 0.1 * moss, 0.6)


def dirt(S=1024, seed=1003):
    """Chemin de terre battue : ornières, cailloux, flaques séchées."""
    r = np.random.default_rng(seed)
    pebbles = np.clip(1 - n01(gaussian_filter(voronoi_edges(S, S, r, 700), 1.0, mode='wrap')) * 1.6, 0, 1) ** 2
    pebbles *= np.clip(n01(fbm(S, S, r, base=S / 16, octaves=3)) * 2 - 0.6, 0, 1)
    base = n01(fbm(S, S, r, base=S / 8, octaves=5))
    col = np.array([0.36, 0.27, 0.17])[None, None] * (0.7 + 0.5 * base[..., None])
    col = col * (1 - pebbles[..., None]) + np.array([0.48, 0.45, 0.40])[None, None] * pebbles[..., None] * (0.8 + 0.3 * base[..., None])
    return _tex(col, 0.8 * base + 2.0 * pebbles, 0.15 + 0.2 * pebbles, 0.8)


def rock(S=1024, seed=1004):
    """Roche des Pics Sélénites : strates, fissures, lichens clairs."""
    r = np.random.default_rng(seed)
    yy = np.arange(S)[:, None] / S
    strata = np.sin((yy * 18 + 0.25 * fbm(S, S, r, base=S / 4, octaves=4)) * np.pi * 2) * 0.5 + 0.5
    cr = gaussian_filter(scratches(S, S, r, 160, (0.03, 0.2), (1, 3), angle=1.4, spread=0.5), 0.8, mode='wrap')
    g = n01(fbm(S, S, r, base=S / 10, octaves=6))
    lich = np.clip((n01(fbm(S, S, r, base=S / 20, octaves=3)) - 0.7) * 4, 0, 1)
    col = np.array([0.42, 0.40, 0.38])[None, None] * (0.6 + 0.4 * g[..., None] + 0.15 * strata[..., None]) * (1 - 0.6 * cr[..., None])
    col = col * (1 - lich[..., None] * 0.6) + np.array([0.55, 0.58, 0.42])[None, None] * lich[..., None] * 0.6
    return _tex(col, 1.5 * g + 0.8 * strata - 2.0 * cr, 0.25 + 0.1 * g, 0.9)


def sand(S=1024, seed=1005):
    """Sable du Désert des Étoiles : rides éoliennes, grains, paillettes."""
    r = np.random.default_rng(seed)
    xx = np.arange(S)[None, :] / S
    rip = np.sin((xx * 40 + 0.6 * fbm(S, S, r, base=S / 5, octaves=3)) * np.pi * 2) * 0.5 + 0.5
    grain = n01(noise(S, S, 1.5, r))
    big = n01(fbm(S, S, r, base=S / 3, octaves=4))
    col = np.array([0.78, 0.64, 0.42])[None, None] * (0.8 + 0.15 * rip[..., None] + 0.12 * grain[..., None] + 0.1 * big[..., None])
    return _tex(col, 0.8 * rip + 0.3 * grain, 0.25 + 0.1 * grain, 0.5)


def stone_blocks(S=1024, seed=1006, rows=8, tone=(0.62, 0.58, 0.52)):
    """Pierre de taille du château : assises de blocs irréguliers (joints décalés), arêtes épaufrées, salissures
    verticales, mousse au pied. UV : 1 répétition = 2 m."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    row = np.floor(yy * rows); fy = yy * rows - row
    off = (row % 2) * 0.5 + r.uniform(0, 0.2, rows)[row.astype(int)]
    cols = 4
    u = xx * cols + off; fx = u - np.floor(u)
    jit = n01(fbm(S, S, r, base=S / 30, octaves=3)) * 0.06
    edge = np.minimum(np.minimum(fx, 1 - fx) * (S / cols), np.minimum(fy, 1 - fy) * (S / rows))
    mortar = np.clip(1 - (edge - 2.5 - jit * 40) / 3.0, 0, 1)
    bid = (np.floor(u) * 7.3 + row * 3.1) % 1.0
    tint = 0.85 + 0.25 * np.sin(bid * 40)[..., None] * 0.5
    g = n01(fbm(S, S, r, base=S / 12, octaves=5))
    chips = np.clip((n01(fbm(S, S, r, base=S / 60, octaves=2)) - 0.75) * 4, 0, 1) * np.clip(1 - edge / 12, 0, 1)
    streak = np.clip(n01(fbm(S, S, r, base=S / 6, octaves=4, aniso=(1.0, 0.08))) - 0.5, 0, 1) * 1.5
    col = np.array(tone)[None, None] * tint * (0.75 + 0.35 * g[..., None]) * (1 - 0.35 * streak[..., None])
    col = col * (1 - 0.55 * mortar[..., None]) * (1 - 0.3 * chips[..., None])
    hgt = 2.5 * (1 - mortar) + 0.6 * g - 1.5 * chips
    return _tex(col, hgt, 0.18 + 0.1 * g, 1.0, 6, 0.15)


def slate_roof(S=1024, seed=1007, tone=(0.22, 0.25, 0.32)):
    """Ardoises bleu-gris en écailles (rangées décalées), lichens et usure. UV : 1 répétition = 2 m."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    rows = 14; row = np.floor(yy * rows); fy = yy * rows - row
    u = xx * 10 + (row % 2) * 0.5; fx = u - np.floor(u)
    scale = np.clip(1 - ((fx - 0.5) ** 2 * 3 + (1 - fy) ** 2 * 0.8), 0, 1)
    g = n01(fbm(S, S, r, base=S / 20, octaves=4))
    bid = (np.floor(u) * 5.7 + row * 2.3) % 1.0
    col = np.array(tone)[None, None] * (0.7 + 0.4 * scale[..., None]) * (0.85 + 0.3 * np.sin(bid * 30)[..., None] * 0.5) * (0.85 + 0.25 * g[..., None])
    lich = np.clip((n01(fbm(S, S, r, base=S / 10, octaves=3)) - 0.72) * 4, 0, 1)
    col = col * (1 - 0.5 * lich[..., None]) + np.array([0.45, 0.48, 0.30])[None, None] * 0.5 * lich[..., None]
    return _tex(col, 2.0 * scale + 0.4 * g, 0.35 + 0.2 * scale, 1.0)


def bark(S=512, seed=1008, tone=(0.30, 0.22, 0.15)):
    r = np.random.default_rng(seed)
    f = fbm(S, S, r, base=S / 16, octaves=5, aniso=(1.0, 0.08))
    cr = np.clip(1 - np.abs(f) * 6, 0, 1) ** 2
    g = n01(fbm(S, S, r, base=S / 8, octaves=4))
    col = np.array(tone)[None, None] * (0.6 + 0.5 * g[..., None]) * (1 - 0.6 * cr[..., None])
    return _tex(col, 1.0 * g - 2.5 * cr, 0.15, 1.0)


def leaves(S=1024, seed=1009, c1=(0.16, 0.30, 0.08), c2=(0.34, 0.46, 0.14), kind='broad', cols=2):
    """Atlas de branches feuillues à transparence : rameaux avec dizaines de feuilles (ovales pointues) ou
    aiguilles (conifères), teintes variées, nervure claire."""
    r = np.random.default_rng(seed)
    col = np.zeros((S, S, 3)); alpha = np.zeros((S, S)); hgt = np.zeros((S, S))
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    cw = S // cols
    for c in range(cols):
        for k in range(110 if kind == 'broad' else 320):
            cx = c * cw + r.uniform(0.12, 0.88) * cw; cy = r.uniform(0.08, 0.92) * S
            L = r.uniform(0.06, 0.11) * S if kind == 'broad' else r.uniform(0.05, 0.09) * S
            W = L * (0.42 if kind == 'broad' else 0.06)
            a = r.uniform(0, np.pi) if kind == 'broad' else r.uniform(-0.7, 0.7) + np.pi / 2 * (k % 2)
            ca, sa = np.cos(a), np.sin(a)
            x0, x1 = int(max(cx - L, c * cw)), int(min(cx + L, (c + 1) * cw - 1)); y0, y1 = int(max(cy - L, 0)), int(min(cy + L, S - 1))
            if x1 <= x0 or y1 <= y0: continue
            X = xx[y0:y1, x0:x1] - cx; Y = yy[y0:y1, x0:x1] - cy
            u = (X * ca + Y * sa) / L; v = (-X * sa + Y * ca) / W
            prof = 1 - u ** 2
            inside = (np.abs(v) < np.sqrt(np.clip(prof, 0, 1)) * (1 - 0.3 * u)) & (np.abs(u) < 1)
            t = r.uniform()
            cc = np.array(c1) * (1 - t) + np.array(c2) * t
            shade = (0.75 + 0.3 * (1 - np.abs(v))) * (1 - 0.35 * (np.abs(v) < 0.08)) if kind == 'broad' else np.ones_like(v)
            hv = np.clip(1 - np.abs(v), 0, 1)
            blk = col[y0:y1, x0:x1]; blk[inside] = cc[None] * (shade[inside] * (0.8 + 0.3 * hv[inside]))[..., None]
            alpha[y0:y1, x0:x1][inside] = 1.0; hgt[y0:y1, x0:x1][inside] = hv[inside]
        # rameau
        pts = np.linspace(0.05, 0.95, 200)
        for p in pts:
            x = int(c * cw + cw * (0.5 + 0.05 * np.sin(p * 6))); y = int((1 - p) * (S - 1))
            col[y, max(x - 2, 0):x + 2] = (0.25, 0.18, 0.10); alpha[y, max(x - 2, 0):x + 2] = 1
    return TexSet(np.clip(col, 0, 1), height_to_normal(gaussian_filter(hgt, 1), 0.8), np.zeros((S, S)), np.full((S, S), 0.3), np.clip(0.6 + 0.4 * hgt, 0, 1), None, alpha)


def water(S=512, seed=1010):
    r = np.random.default_rng(seed)
    w = fbm(S, S, r, base=S / 6, octaves=5)
    col = np.tile(np.array([0.10, 0.22, 0.28]), (S, S, 1)) * (0.9 + 0.2 * n01(w)[..., None])
    return _tex(col, 0.6 * w, np.full((S, S), 0.92), 0.25)
