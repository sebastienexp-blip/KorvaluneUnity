"""Matériaux des ennemis : acier noirci fissuré de braises, maille et drap rougis, lueurs d'yeux, os, étoffes corrompues."""
import numpy as np
from scipy.ndimage import gaussian_filter
from forge.texgen import TexSet, fbm, n01, voronoi_edges, height_to_normal
from knight import materials as KM


def cursed_steel(S=2048, seed=401, tone=(0.055, 0.052, 0.058), ember=(1.0, 0.16, 0.05), amount=1.0):
    """Acier bruni presque noir, veiné de fissures rougeoyantes (carte d'émission) qui suivent un réseau de Voronoï
    perturbé, plus denses par plaques ; usure claire sur les arêtes rayées."""
    t = KM.steel(S, seed=seed, tone=tone, polish=0.55, rust_amt=0.25, grime_amt=1.2, scratch_amt=1.3)
    r = np.random.default_rng(seed + 1)
    # veines fines et ramifiées : crêtes de bruit fractal (lignes |fbm| ≈ 0), deux échelles
    v1 = np.clip(1 - np.abs(fbm(S, S, r, base=S / 5, octaves=5)) * 9.0, 0, 1) ** 3
    v2 = np.clip(1 - np.abs(fbm(S, S, r, base=S / 12, octaves=4)) * 16.0, 0, 1) ** 3 * 0.5
    zone = np.clip((n01(fbm(S, S, r, base=S / 4, octaves=4)) - (1 - amount)) * 3.0, 0, 1)
    crack = np.clip(np.maximum(v1, v2) * zone, 0, 1)
    glow = gaussian_filter(crack, 2.5, mode='wrap') * 0.8 + crack
    emit = np.clip(np.array(ember)[None, None] * glow[..., None] * 1.3, 0, 1)
    base = t.base * (1 - 0.6 * crack[..., None]) + np.array([0.55, 0.06, 0.02])[None, None] * crack[..., None]
    hgt = -1.5 * crack
    nrm = t.normal * 0.5 + height_to_normal(hgt, 0.6) * 0.5
    return TexSet(np.clip(base, 0, 1), nrm, t.metal * (1 - crack), np.clip(t.smooth - 0.3 * crack, 0, 1), t.ao, emit)


def tinted(ts, tint, emit=None):
    ts.base = np.clip(ts.base * np.asarray(tint)[None, None], 0, 1)
    if emit is not None: ts.emit = emit
    return ts


def glow(S=64, color=(1.0, 0.12, 0.04)):
    z = np.zeros((S, S))
    c = np.tile(np.array(color, float), (S, S, 1))
    return TexSet(c, np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.9, z + 1, c)


def lava_rock(S=2048, seed=801, tone=(0.075, 0.062, 0.055), lava=(1.0, 0.42, 0.08), cells=34, glow_amt=1.0):
    """Peau de démon : plaques de roche sombre fissurées (Voronoï), fissures incandescentes de lave (émission),
    grain minéral, arêtes usées plus claires."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    big = np.clip(1 - np.abs(fbm(S, S, r, base=S / 3.5, octaves=4)) * 7.0, 0, 1) ** 2.2          # grandes fissures ramifiées
    mid = np.clip(1 - np.abs(fbm(S, S, r, base=S / 8, octaves=4)) * 10.0, 0, 1) ** 2.6 * 0.8
    fine = np.clip(1 - np.abs(fbm(S, S, r, base=S / 18, octaves=3)) * 14.0, 0, 1) ** 3 * 0.45
    mask = np.clip((n01(fbm(S, S, r, base=S / 3, octaves=3)) - 0.30) * 2.2, 0, 1)
    crack = np.clip(np.maximum.reduce([big, mid * (0.4 + 0.6 * mask), fine * mask]) * glow_amt, 0, 1)
    e = gaussian_filter(crack, 8, mode='wrap')
    grain = fbm(S, S, r, base=S / 30, octaves=5)
    plates = e
    hgt = 1.5 * n01(grain) - 3.0 * crack - 1.2 * n01(plates)
    base = np.array(tone)[None, None] * (0.75 + 0.5 * n01(grain)[..., None]) * (1 - 0.5 * crack[..., None])
    hot = gaussian_filter(crack, 3, mode='wrap')
    base = base + np.array([0.45, 0.12, 0.02])[None, None] * crack[..., None]
    emit = np.clip(np.array(lava)[None, None] * (crack ** 1.2 + 0.35 * hot)[..., None] * 1.4, 0, 1)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.9), np.zeros((S, S)), np.clip(0.30 - 0.2 * crack + 0.15 * n01(grain), 0, 1),
                  np.clip(cavity_ao(hgt, 6, 0.1), 0, 1), emit)
