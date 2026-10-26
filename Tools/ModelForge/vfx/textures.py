"""Textures d'effets spéciaux (RGBA, fond transparent) pour les particules des compétences :
planches animées (flipbooks 4×4) de flammes et de fumée, étincelles étirées, éclair ramifié, éclats de glace,
cercle de runes, onde de choc, entaille d'arme, lueur douce, débris de roche, crâne spectral, rayons de lumière."""
import os
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates
from forge.texgen import fbm, n01

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))), 'Assets', 'Korvalune', 'Art', 'VFX')


def save(name, rgba):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '.png'), optimize=True)


def grid(S):
    y, x = np.mgrid[0:S, 0:S].astype(float) / (S - 1) * 2 - 1
    return x, -y


def flipbook(frames, cols=4):
    S = frames[0].shape[0]; rows = (len(frames) + cols - 1) // cols
    out = np.zeros((S * rows, S * cols, 4))
    for i, f in enumerate(frames):
        r, c = divmod(i, cols); out[r * S:(r + 1) * S, c * S:(c + 1) * S] = f
    return out


def fire(S=256, n=16, seed=1):
    """Langue de flamme qui monte et se tord : masque en goutte déformé par un bruit qui défile vers le haut ;
    blanc-jaune au cœur, orange puis rouge sombre vers les bords (couleur finale réglée par la particule)."""
    r = np.random.default_rng(seed)
    base = [fbm(S, S, r, base=S / 4, octaves=4) for _ in range(2)]
    x, y = grid(S)
    frames = []
    for k in range(n):
        t = k / n
        sh = int(t * S) % S
        nz = np.roll(base[0], -sh, axis=0) * 0.6 + np.roll(base[1], -sh // 2, axis=0) * 0.4
        life = np.sin(np.pi * (t * 0.9 + 0.05))
        w = (0.85 - 0.55 * (y + 1) / 2) * (0.75 + 0.25 * life)
        d = np.abs(x + 0.25 * nz * (y + 1)) / np.maximum(w, 1e-3)
        h = np.clip(1 - (y + 0.95) / (1.9 * (0.65 + 0.35 * life)), 0, 1)
        m = np.clip(1 - d, 0, 1) * h * np.clip((y + 1) * 3, 0, 1)
        m = np.clip(m + 0.35 * nz * m, 0, 1) ** 0.8
        heat = m ** 0.6
        col = np.stack([np.ones_like(m), 0.25 + 0.7 * heat, 0.05 + 0.75 * heat ** 2.5], -1)
        frames.append(np.dstack([col, gaussian_filter(m, 1.0)]))
    return flipbook(frames)


def smoke(S=256, n=16, seed=2):
    r = np.random.default_rng(seed)
    base = fbm(S * 2, S * 2, r, base=S / 3, octaves=5)
    x, y = grid(S)
    frames = []
    for k in range(n):
        t = k / n
        rad = np.sqrt(x ** 2 + y ** 2) / (0.55 + 0.4 * t)
        crop = base[int(t * S * 0.5):int(t * S * 0.5) + S, int(t * S * 0.3):int(t * S * 0.3) + S]
        m = np.clip(1 - rad, 0, 1) ** 1.2 * np.clip(0.6 + 0.8 * n01(crop), 0, 1.2)
        m *= (1 - t) ** 0.7 * np.clip(t * 6, 0, 1)
        g = 0.75 + 0.25 * n01(crop)
        frames.append(np.dstack([g, g, g, np.clip(m, 0, 1)]))
    return flipbook(frames)


def glow(S=128):
    x, y = grid(S); r = np.sqrt(x ** 2 + y ** 2)
    a = np.clip(1 - r, 0, 1) ** 2.2
    return np.dstack([np.ones((S, S)), np.ones((S, S)), np.ones((S, S)), a])


def spark(S=128):
    """Étincelle étirée (à utiliser en « stretched billboard »)."""
    x, y = grid(S)
    a = np.exp(-(x / 0.12) ** 2) * np.clip(1 - np.abs(y), 0, 1) ** 1.5
    core = np.exp(-(x / 0.04) ** 2) * np.clip(1 - np.abs(y) * 1.4, 0, 1)
    return np.dstack([np.ones((S, S)), 0.85 + 0.15 * core, 0.6 + 0.4 * core, np.clip(a + core, 0, 1)])


def lightning(W=512, H=128, seed=3, strands=3):
    """Éclair ramifié horizontal (gauche → droite), à étirer entre deux points (LineRenderer)."""
    r = np.random.default_rng(seed)
    img = np.zeros((H, W))
    for s in range(strands):
        pts = [H / 2]
        for i in range(1, W):
            pts.append(pts[-1] + r.normal(0, 2.2))
            pts[-1] += (H / 2 - pts[-1]) * 0.03
        ys = np.array(pts)
        for i in range(W):
            yy = np.arange(H); w = 1.6 if s == 0 else 0.9
            img[:, i] = np.maximum(img[:, i], np.exp(-((yy - ys[i]) / w) ** 2) * (1.0 if s == 0 else 0.6))
        # branches
        for b in range(6):
            x0 = r.integers(20, W - 80); y = ys[x0]; L = r.integers(25, 50); d = r.choice([-1, 1])
            for i in range(L):
                y += d * abs(r.normal(0.5, 0.4)); xi = x0 + i
                if xi >= W or y < 2 or y > H - 3: break
                yy = np.arange(H); img[:, xi] = np.maximum(img[:, xi], np.exp(-((yy - y) / 0.7) ** 2) * 0.5 * (1 - i / L))
    halo = gaussian_filter(img, 4) * 1.5
    a = np.clip(img + halo, 0, 1)
    return np.dstack([np.clip(0.75 + img, 0, 1), np.clip(0.8 + img, 0, 1), np.ones_like(img), a])


def ice_shard(S=256, seed=4):
    r = np.random.default_rng(seed)
    x, y = grid(S)
    a = np.zeros((S, S)); c = np.zeros((S, S))
    for k in range(5):
        ang = r.uniform(-0.5, 0.5) + (k - 2) * 0.35; L = r.uniform(0.6, 0.95); w = r.uniform(0.06, 0.12)
        u = x * np.sin(ang) + (y + 0.8) * np.cos(ang); v = x * np.cos(ang) - (y + 0.8) * np.sin(ang)
        m = (u > 0) & (u < L * 1.8) & (np.abs(v) < w * (1 - u / (L * 1.8)))
        a = np.maximum(a, m.astype(float)); c = np.maximum(c, m * (0.6 + 0.4 * np.abs(v) / w))
    a = gaussian_filter(a, 0.8)
    return np.dstack([0.75 + 0.25 * c, 0.9 + 0.1 * c, np.ones((S, S)), a])


def rune_circle(S=512, seed=5):
    """Cercle magique : anneaux concentriques, runes angulaires, étoile inscrite."""
    r = np.random.default_rng(seed)
    x, y = grid(S); rr = np.sqrt(x ** 2 + y ** 2); th = np.arctan2(y, x)
    a = np.zeros((S, S))
    for R, w in ((0.95, 0.012), (0.88, 0.006), (0.62, 0.008), (0.56, 0.004), (0.25, 0.006)):
        a = np.maximum(a, np.exp(-((rr - R) / w) ** 2))
    band = (rr > 0.66) & (rr < 0.84)
    seg = (th + np.pi) / (2 * np.pi) * 24; cell = np.floor(seg); f = seg - cell
    glyph = np.zeros((S, S))
    for k in range(24):
        sel = band & (cell == k)
        pattern = r.integers(0, 2, (3, 3))
        gx = np.clip((f * 3).astype(int), 0, 2); gy = np.clip(((rr - 0.66) / 0.18 * 3).astype(int), 0, 2)
        glyph[sel] = pattern[gy[sel], gx[sel]] * (np.abs(f[sel] * 3 - gx[sel] - 0.5) < 0.32)
    a = np.maximum(a, gaussian_filter(glyph, 0.8) * 0.9)
    for k in range(5):   # pentagramme
        a0 = np.pi / 2 + k * 4 * np.pi / 5; a1 = a0 + 4 * np.pi / 5
        p0 = np.array([np.cos(a0), np.sin(a0)]) * 0.6; p1 = np.array([np.cos(a1), np.sin(a1)]) * 0.6
        d = p1 - p0; t = np.clip(((x - p0[0]) * d[0] + (y - p0[1]) * d[1]) / (d @ d), 0, 1)
        dist = np.sqrt((x - p0[0] - t * d[0]) ** 2 + (y - p0[1] - t * d[1]) ** 2)
        a = np.maximum(a, np.exp(-(dist / 0.007) ** 2))
    halo = gaussian_filter(a, 6) * 0.7
    a = np.clip(a + halo, 0, 1)
    return np.dstack([np.ones((S, S)), np.ones((S, S)), np.ones((S, S)), a])


def shockwave(S=512):
    x, y = grid(S); rr = np.sqrt(x ** 2 + y ** 2)
    a = np.exp(-((rr - 0.85) / 0.06) ** 2) + 0.4 * np.exp(-((rr - 0.8) / 0.15) ** 2)
    return np.dstack([np.ones((S, S)), np.ones((S, S)), np.ones((S, S)), np.clip(a, 0, 1)])


def slash(S=512, seed=6):
    """Arc d'entaille (trace de lame) : croissant effilé, cœur blanc, traînée."""
    r = np.random.default_rng(seed)
    x, y = grid(S); rr = np.sqrt(x ** 2 + y ** 2); th = np.arctan2(y, x)
    span = np.clip((th + 0.2) / 2.6, 0, 1) * (th > -0.2) * (th < 2.4)
    w = 0.10 * np.sin(np.pi * span) ** 0.8
    a = np.exp(-((rr - 0.78) / np.maximum(w, 1e-3)) ** 2) * (span > 0)
    streaks = n01(fbm(S, S, r, base=S / 40, octaves=2, aniso=(0.05, 1.0)))
    a = np.clip(a * (0.6 + 0.6 * streaks), 0, 1)
    core = np.exp(-((rr - 0.80) / np.maximum(w * 0.25, 1e-3)) ** 2) * (span > 0)
    return np.dstack([np.ones((S, S)), np.ones((S, S)), np.clip(0.8 + core, 0, 1), np.clip(a + core, 0, 1)])


def debris(S=256, seed=7):
    """Atlas 2×2 d'éclats de roche (silhouettes polygonales ombrées)."""
    r = np.random.default_rng(seed)
    out = np.zeros((S, S, 4)); h = S // 2
    for k in range(4):
        x, y = grid(h)
        ang = np.arctan2(y, x); n = r.integers(5, 8); R = r.uniform(0.5, 0.9, n)
        rad = np.interp((ang + np.pi) / (2 * np.pi) * n, np.arange(n + 1), np.r_[R, R[0]])
        m = (np.sqrt(x ** 2 + y ** 2) < rad).astype(float)
        sh = 0.45 + 0.4 * np.clip(x * 0.5 + y * 0.6, -1, 1)
        rr_, cc = divmod(k, 2)
        out[rr_ * h:(rr_ + 1) * h, cc * h:(cc + 1) * h] = np.dstack([sh * 0.55, sh * 0.50, sh * 0.45, gaussian_filter(m, 0.7)])
    return out


def beam(W=128, H=512, seed=8):
    """Rayon vertical (colonne de lumière, faisceau sacré) : cœur vif, bords doux, stries qui défilent."""
    r = np.random.default_rng(seed)
    x = np.linspace(-1, 1, W)[None, :]
    st = n01(fbm(H, W, r, base=W / 6, octaves=3, aniso=(1.0, 0.1)))
    a = np.exp(-(x / 0.35) ** 2) * (0.6 + 0.5 * st) + np.exp(-(x / 0.08) ** 2)
    yfade = np.clip(np.linspace(0, 1, H)[:, None] * 4, 0, 1) * np.clip(np.linspace(1, 0, H)[:, None] * 1.5, 0, 1)
    a = np.clip(a * yfade, 0, 1)
    return np.dstack([np.ones((H, W)), np.ones((H, W)), np.ones((H, W)), a])


def soul(S=256, seed=9):
    """Feu follet / âme : flamme ronde à queue ondulante, pour les projectiles nécromantiques et du Néant."""
    r = np.random.default_rng(seed)
    x, y = grid(S)
    nz = fbm(S, S, r, base=S / 6, octaves=4)
    head = np.exp(-((x) ** 2 + (y - 0.35) ** 2) / 0.08)
    tail = np.exp(-((x + 0.15 * np.sin(y * 6) + 0.1 * nz) / (0.25 * np.clip(y + 1.1, 0, 2))) ** 2) * np.clip(0.4 - y, 0, 1.2) * (y < 0.4)
    a = np.clip(head + tail * 0.8 + 0.2 * nz * (head + tail), 0, 1)
    return np.dstack([np.ones((S, S)), np.ones((S, S)), np.ones((S, S)), a])


def main():
    for name, fn in (('VFX_Feu', fire), ('VFX_Fumee', smoke), ('VFX_Lueur', glow), ('VFX_Etincelle', spark), ('VFX_Eclair', lightning),
                     ('VFX_Glace', ice_shard), ('VFX_Runes', rune_circle), ('VFX_Onde', shockwave), ('VFX_Entaille', slash), ('VFX_Debris', debris),
                     ('VFX_Rayon', beam), ('VFX_Ame', soul)):
        save(name, fn()); print('  ', name, flush=True)


if __name__ == '__main__':
    main()
