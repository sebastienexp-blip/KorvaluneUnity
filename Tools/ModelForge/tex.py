"""Textures PBR procédurales (albedo, normales, métal/rugosité), tuilables."""
import numpy as np
from PIL import Image

SZ = 256
_rng = np.random.default_rng(1234)


def _up(a, n):
    p = np.pad(a, 2, mode="wrap"); im = Image.fromarray(p.astype(np.float32), mode="F").resize((int((a.shape[1] + 4) * n / a.shape[1]), int((a.shape[0] + 4) * n / a.shape[0])), Image.BICUBIC)
    o = np.asarray(im); k = int(2 * n / a.shape[0]); return o[k:k + n, k:k + n]


def noise(scale=8, oct=4, size=SZ, aniso=(1, 1), seed=None):
    r = np.random.default_rng(seed) if seed is not None else _rng; out = np.zeros((size, size)); amp = 1.0; tot = 0
    for o in range(oct):
        sx = max(2, int(scale * aniso[0] * 2 ** o)); sy = max(2, int(scale * aniso[1] * 2 ** o))
        g = r.random((sy, sx)); p = np.pad(g, 2, mode="wrap")
        im = Image.fromarray(p.astype(np.float32), mode="F").resize(((sx + 4) * size // sx, (sy + 4) * size // sy), Image.BICUBIC); a = np.asarray(im)
        ox, oy = 2 * size // sx, 2 * size // sy; out += a[oy:oy + size, ox:ox + size] * amp; tot += amp; amp *= 0.5
    out /= tot; return (out - out.min()) / max(out.max() - out.min(), 1e-6)


def cells(n=10, size=SZ):
    pts = _rng.random((n * n, 2)); gx, gy = np.meshgrid(np.arange(size) / size, np.arange(size) / size); best = np.full((size, size), 9.0); second = best.copy()
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for p in pts:
                d = np.hypot(gx - p[0] - dx, gy - p[1] - dy); m = d < best; second = np.where(m, best, np.minimum(second, d)); best = np.where(m, d, best)
    return best, second


def normal_map(h, k=3.0):
    gx = np.roll(h, -1, 1) - np.roll(h, 1, 1); gy = np.roll(h, -1, 0) - np.roll(h, 1, 0)
    n = np.stack([-gx * k * SZ / 64, gy * k * SZ / 64, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def _pack(alb, h, rough, metal, k=3.0):
    a = Image.fromarray((np.clip(alb, 0, 1) * 255).astype(np.uint8)); mr = np.zeros(h.shape + (3,)); mr[..., 1] = np.clip(rough, 0, 1); mr[..., 2] = np.clip(metal, 0, 1)
    return a, normal_map(h, k), Image.fromarray((mr * 255).astype(np.uint8))


def make(kind, tint=(1, 1, 1)):
    t = np.array(tint)
    if kind == "steel":
        h = noise(3, 3, aniso=(6, 1)) * 0.5 + noise(40, 2) * 0.1; sc = (noise(30, 2, aniso=(10, 1)) > 0.86) * 0.25
        base = np.array([0.62, 0.65, 0.70]) * t; alb = base[None, None] * (0.7 + 0.3 * noise(5, 3)[..., None]) * (1 - sc[..., None] * 0.5)
        return _pack(alb, h + sc * 0.4, 0.28 + 0.3 * noise(6, 3) + sc * 0.2, 0.95 + 0 * h, 2.0)
    if kind == "darksteel":
        n = noise(4, 4); rust = noise(7, 4, seed=5) > 0.7; h = noise(3, 3, aniso=(5, 1)) * 0.4 + n * 0.2
        base = np.array([0.22, 0.23, 0.27]) * t; alb = base[None, None] * (0.7 + 0.5 * n[..., None]); alb = np.where(rust[..., None], np.array([0.35, 0.17, 0.08]) * (0.6 + n[..., None] * 0.5), alb)
        return _pack(alb, h + rust * 0.1, 0.45 + 0.35 * n + rust * 0.2, np.where(rust, 0.25, 0.9), 2.5)
    if kind == "gold":
        n = noise(5, 3); h = n * 0.2; alb = (np.array([0.85, 0.62, 0.22]) * t)[None, None] * (0.8 + 0.3 * n[..., None]); return _pack(alb, h, 0.3 + 0.2 * n, 0.97 + 0 * h, 1.5)
    if kind == "leather":
        b, s = cells(14); grain = (s - b); h = grain * 0.8 + noise(20, 3) * 0.15; n = noise(4, 4)
        alb = (np.array([0.30, 0.18, 0.10]) * t)[None, None] * (0.7 + 0.5 * n[..., None]) * (0.8 + 0.4 * grain[..., None] * 4); return _pack(alb, h, 0.7 + 0.2 * n, 0.0 * h, 3.0)
    if kind == "cloth":
        x = np.arange(SZ)[None, :]; y = np.arange(SZ)[:, None]; wv = (np.sin(x * np.pi / 2) * np.sin(y * np.pi / 2)) * 0.5 + 0.5; n = noise(6, 4); h = wv * 0.4 + n * 0.3
        alb = (np.array([0.35, 0.10, 0.12]) * t)[None, None] * (0.6 + 0.6 * n[..., None]) * (0.85 + 0.2 * wv[..., None]); return _pack(alb, h, 0.9 + 0 * h, 0.0 * h, 1.5)
    if kind == "skin":
        n = noise(10, 4); pores = noise(60, 1); h = pores * 0.1 + n * 0.05
        alb = (np.array([0.78, 0.58, 0.46]) * t)[None, None] * (0.9 + 0.2 * n[..., None]); return _pack(alb, h, 0.55 + 0.2 * n, 0.0 * h, 1.0)
    if kind == "bone":
        n = noise(5, 5); b, s = cells(6); crack = np.clip(1 - (s - b) * 18, 0, 1) * (noise(12, 2, seed=9) > 0.55)
        alb = (np.array([0.80, 0.76, 0.64]) * t)[None, None] * (0.7 + 0.4 * n[..., None]) * (1 - crack[..., None] * 0.6); return _pack(alb, n * 0.3 - crack * 0.5, 0.6 + 0.3 * n, 0.0 * n, 3.0)
    if kind == "stone":
        b, s = cells(9); edge = np.clip((s - b) * 14, 0, 1); n = noise(6, 5); h = edge * 0.6 + n * 0.4
        alb = (np.array([0.35, 0.34, 0.36]) * t)[None, None] * (0.6 + 0.6 * n[..., None]) * (0.55 + 0.45 * edge[..., None]); return _pack(alb, h, 0.85 + 0.1 * n, 0.0 * n, 4.0)
    if kind == "hide":
        n = noise(8, 5); fur = noise(60, 2, aniso=(1, 8)); h = fur * 0.3 + n * 0.1
        alb = (np.array([0.28, 0.22, 0.18]) * t)[None, None] * (0.6 + 0.7 * n[..., None]) * (0.8 + 0.4 * fur[..., None]); return _pack(alb, h, 0.85 + 0 * n, 0.0 * n, 2.0)
    if kind == "scale":
        b, s = cells(16); sc = np.clip(b * 4, 0, 1); n = noise(5, 4)
        alb = (np.array([0.20, 0.30, 0.18]) * t)[None, None] * (0.5 + 0.8 * (1 - sc[..., None])) * (0.7 + 0.5 * n[..., None]); return _pack(alb, (1 - sc) * 0.6, 0.45 + 0.3 * sc, 0.05 + 0 * n, 3.5)
    if kind == "flesh":
        n = noise(7, 5); v = noise(14, 3, aniso=(2, 1)); alb = (np.array([0.45, 0.20, 0.20]) * t)[None, None] * (0.6 + 0.6 * n[..., None]) * (0.85 + 0.3 * v[..., None]); return _pack(alb, n * 0.4 + v * 0.2, 0.4 + 0.3 * n, 0.0 * n, 3.0)
    if kind == "crystal":
        n = noise(4, 4); facets = np.floor(noise(6, 2, seed=3) * 6) / 6.0; alb = (np.array([0.45, 0.12, 0.75]) * t)[None, None] * (0.7 + 0.5 * facets[..., None]); return _pack(alb, facets * 0.5, 0.12 + 0.1 * n, 0.1 + 0 * n, 3.0)
    raise ValueError(kind)


def emissive_crystal(tint=(0.7, 0.25, 1.0)):
    n = noise(5, 4); v = noise(10, 3, seed=11); e = np.clip((v - 0.35) * 2.2, 0, 1) * (0.5 + 0.5 * n)
    return Image.fromarray((np.clip(e[..., None] * np.array(tint)[None, None], 0, 1) * 255).astype(np.uint8))


def add_material(g, name, kind, tint=(1, 1, 1), emissive=None):
    a, n, mr = make(kind, tint)
    if emissive:
        et = emissive_crystal(emissive); return g.material(name, a, n, mr, emissive=(1, 1, 1), emissive_tex=et)
    return g.material(name, a, n, mr, metallic=1.0 if kind in ("steel", "darksteel", "gold") else 0.0, rough=1.0)
