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


def frost_hide(S=2048, seed=811, tone=(0.42, 0.50, 0.58), frost=(0.80, 0.92, 1.0)):
    """Peau du colosse de givre : cuir épais bleu-gris, plis et pores profonds, croûtes de givre blanc cristallin sur les
    reliefs (plus brillantes), fines fissures d'un bleu glacier à peine lumineux."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 6, octaves=6)); pores = n01(fbm(S, S, r, base=S / 90, octaves=3))
    folds = np.clip(1 - np.abs(fbm(S, S, r, base=S / 10, octaves=4)) * 8.0, 0, 1) ** 2
    rime = np.clip((n01(fbm(S, S, r, base=S / 6, octaves=5)) - 0.66) * 2.2, 0, 1) * 0.8
    crack = np.clip(1 - np.abs(fbm(S, S, r, base=S / 7, octaves=4)) * 14.0, 0, 1) ** 3
    base = np.array(tone)[None, None] * (0.75 + 0.4 * g[..., None]) * (0.9 + 0.15 * pores[..., None]) * (1 - 0.35 * folds[..., None])
    base = base * (1 - rime[..., None]) + np.array(frost)[None, None] * rime[..., None] * (0.85 + 0.15 * pores[..., None])
    base = base * (1 - 0.4 * crack[..., None]) + np.array([0.25, 0.55, 0.80])[None, None] * 0.4 * crack[..., None]
    hgt = 0.8 * g - 1.2 * folds + 0.6 * rime - 0.4 * pores - 1.0 * crack
    emit = np.clip(np.array([0.30, 0.65, 1.0])[None, None] * crack[..., None] * 0.6, 0, 1)
    smooth = np.clip(0.28 + 0.45 * rime - 0.1 * pores, 0, 1)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), smooth, np.clip(cavity_ao(hgt, 6, 0.1), 0, 1), emit)


def drowned_skin_tint():
    """Teinte de peau d'un noyé : blafarde, verdâtre, marbrures bleutées."""
    return (0.50, 0.64, 0.56)


def barnacle(S=512, seed=821):
    """Balanes et coquillages : calcaire blanc-gris strié, bouches sombres, algues vertes dans les creux."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    cells = gaussian_filter(voronoi_edges(S, S, r, 60), 1.0, mode='wrap')
    g = n01(fbm(S, S, r, base=S / 10, octaves=5))
    alg = np.clip((n01(fbm(S, S, r, base=S / 8, octaves=4)) - 0.55) * 3, 0, 1)
    base = np.array([0.66, 0.64, 0.58])[None, None] * (0.7 + 0.4 * g[..., None]) * (1 - 0.55 * n01(cells)[..., None])
    base = base * (1 - alg[..., None]) + np.array([0.14, 0.22, 0.09])[None, None] * alg[..., None]
    hgt = 1.2 * g - 1.4 * n01(cells)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.9), np.zeros((S, S)), np.clip(0.25 + 0.4 * alg, 0, 1), np.clip(cavity_ao(hgt, 4, 0.1), 0, 1))


def seaweed(W=512, H=1024, seed=831, color=(0.12, 0.20, 0.07)):
    """Algues : lanières translucides vert-brun, nervure centrale, bords déchiquetés, aspect mouillé."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    g = n01(fbm(H, W, r, base=W / 6, octaves=5))
    vein = np.exp(-((xx / W - 0.5) * 18) ** 2)
    base = np.array(color)[None, None] * (0.7 + 0.5 * g[..., None]) + np.array([0.10, 0.10, 0.03])[None, None] * vein[..., None]
    hgt = 0.6 * g + 0.4 * vein
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.5), np.zeros((H, W)), np.clip(0.6 + 0.2 * g, 0, 1), np.ones((H, W)))


def stone_golem(S=2048, seed=841):
    """Grès écarlate du golem : strates rouges et ocre, plaques fendues, fissures d'ambre en fusion (émission douce)."""
    t = lava_rock(S, seed=seed, tone=(0.40, 0.17, 0.10), lava=(1.0, 0.62, 0.18), cells=28, glow_amt=0.55)
    r = np.random.default_rng(seed + 3)
    yy = np.mgrid[0:S, 0:S][0] / S
    band = (np.sin((yy * 7 + 0.3 * fbm(S, S, r, base=S / 4, octaves=3)) * np.pi * 2) * 0.5 + 0.5)
    t.base = np.clip(t.base * (0.85 + 0.25 * band[..., None]) * np.array([1.0, 0.92, 0.85])[None, None] + np.array([0.08, 0.04, 0.02])[None, None] * band[..., None], 0, 1)
    t.smooth = np.clip(t.smooth * 0.7, 0, 1)
    return t


def void_cloth(W=1024, H=2048, seed=851):
    """Étoffe du Néant : noir violacé profond, broderies de runes violettes lumineuses (émission), lambeaux fumants."""
    t = KM.cloak(W, H, seed=seed, color=(0.035, 0.015, 0.06), thread_color=(0.70, 0.30, 1.0), mud_amt=0.3)
    lum = t.base.mean(-1)
    thread = np.clip((lum - 0.12) * 6, 0, 1)
    t.emit = np.clip(np.array([0.55, 0.20, 1.0])[None, None] * thread[..., None] * 0.9, 0, 1)
    return t


def tentacle_flesh(S=1024, seed=861, tone=(0.36, 0.16, 0.20), belly=(0.62, 0.44, 0.40)):
    """Chair de pieuvre : dessus pourpre marbré de taches sombres, dessous rosé, peau luisante plissée, ventouses
    (anneaux clairs à creux sombre) en deux rangées le long de V (bas de la texture = dessous)."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    g = n01(fbm(S, S, r, base=S / 10, octaves=5)); spots = np.clip((n01(fbm(S, S, r, base=S / 30, octaves=3)) - 0.62) * 4, 0, 1)
    under = np.clip(1 - np.abs(xx - 0.5) * 3.2, 0, 1)                              # bande du dessous (u ≈ 0,5)
    base = np.array(tone)[None, None] * (0.75 + 0.45 * g[..., None]) * (1 - 0.45 * spots[..., None])
    base = base * (1 - under[..., None]) + np.array(belly)[None, None] * under[..., None] * (0.85 + 0.25 * g[..., None])
    # ventouses : deux rangées dans la bande du dessous
    cup = np.zeros((S, S))
    for cx in (0.44, 0.56):
        for k in range(28):
            cy = (k + 0.5 + 0.5 * (cx > 0.5)) / 28
            d = np.sqrt(((xx - cx) * 1.0) ** 2 + ((yy - cy) * 1.0) ** 2)
            cup = np.maximum(cup, np.clip(1 - np.abs(d - 0.011) / 0.004, 0, 1) - np.clip(1 - d / 0.007, 0, 1) * 0.0)
            cup = np.where(d < 0.007, -1.0, cup)
    base = base + np.array([0.20, 0.14, 0.12])[None, None] * np.clip(cup, 0, 1)[..., None] - np.array([0.25, 0.18, 0.16])[None, None] * np.clip(-cup, 0, 1)[..., None]
    wr = np.clip(1 - np.abs(fbm(S, S, r, base=S / 40, octaves=3)) * 10, 0, 1) ** 2
    hgt = 0.5 * g + 1.2 * cup - 0.4 * wr
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), np.clip(0.55 + 0.3 * g, 0, 1), np.clip(cavity_ao(hgt, 4, 0.1), 0, 1))


def crab_shell(S=1024, seed=871, tone=(0.55, 0.16, 0.08), dark=(0.16, 0.05, 0.04)):
    """Carapace de crabe : rouge-orangé passant au brun sombre, granulations, petits tubercules clairs, taches de
    vase, liserés foncés, vernis humide."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 6, octaves=5)); fine = n01(fbm(S, S, r, base=S / 80, octaves=3))
    tub = np.clip(n01(gaussian_filter(r.random((S, S)), 2.0, mode='wrap')) * 3 - 2.3, 0, 1)
    mud = np.clip((n01(fbm(S, S, r, base=S / 9, octaves=4)) - 0.62) * 3, 0, 1)
    base = np.array(dark)[None, None] * (1 - g[..., None]) + np.array(tone)[None, None] * g[..., None]
    base = base * (0.85 + 0.25 * fine[..., None]) + np.array([0.45, 0.35, 0.25])[None, None] * tub[..., None] * 0.6
    base = base * (1 - mud[..., None] * 0.6) + np.array([0.12, 0.11, 0.07])[None, None] * mud[..., None] * 0.6
    hgt = 0.6 * g + 0.4 * fine + 1.4 * tub
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.9), np.zeros((S, S)), np.clip(0.55 + 0.2 * fine - 0.3 * mud, 0, 1), np.clip(cavity_ao(hgt, 4, 0.1), 0, 1))


def frost_fur_skin(S=2048, seed=881):
    """Peau tannée du wendigo : gris cendré, tendue sur les os, crevasses noires, givre dans les creux, taches de sang séché."""
    from forge.texgen import cavity_ao
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 8, octaves=6)); cr = np.clip(1 - np.abs(fbm(S, S, r, base=S / 12, octaves=4)) * 12, 0, 1) ** 2
    blood = np.clip((n01(fbm(S, S, r, base=S / 10, octaves=4)) - 0.72) * 3, 0, 1)
    base = np.array([0.55, 0.55, 0.56])[None, None] * (0.7 + 0.4 * g[..., None]) * (1 - 0.6 * cr[..., None])
    base = base * (1 - blood[..., None] * 0.7) + np.array([0.22, 0.05, 0.04])[None, None] * blood[..., None] * 0.7
    frost = cr * 0.6
    base = base + np.array([0.25, 0.3, 0.35])[None, None] * frost[..., None]
    hgt = 0.7 * g - 1.2 * cr
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), np.clip(0.25 + 0.3 * frost, 0, 1), np.clip(cavity_ao(hgt, 5, 0.1), 0, 1))
