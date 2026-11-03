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


def boulder(S=1024, seed=1014):
    """Rochers isolés : granit altéré. Grain cristallin (mouchetures claires et sombres), grandes plages de teinte,
    fractures nettes avec ombre et bord éclairé, croûtes de lichen gris et jaune, salissure dans les creux."""
    r = np.random.default_rng(seed)
    big = n01(fbm(S, S, r, base=S / 3, octaves=5)); mid = n01(fbm(S, S, r, base=S / 12, octaves=5))
    fine = n01(fbm(S, S, r, base=S / 200, octaves=2))
    spk = r.random((S, S)); dark = gaussian_filter((spk > 0.93).astype(float), 0.7, mode='wrap'); light = gaussian_filter((spk < 0.05).astype(float), 0.6, mode='wrap')
    # fractures : lignes de niveau d'un bruit, nettes et rares
    from world.terrain_hd import _voronoi
    gy, gx = np.mgrid[0:5, 0:5]; pts = ((np.stack([gy.ravel(), gx.ravel()], 1) + 0.5 + r.uniform(-0.4, 0.4, (25, 2))) * S / 5) % S
    d1, d2, cid = _voronoi(S, r, 25, pts); e = (d2 - d1) / 2.0 + 1.5 * (n01(fbm(S, S, r, base=S / 40, octaves=3)) - 0.5)
    keep = np.clip((n01(fbm(S, S, r, base=S / 4, octaves=3)) - 0.5) * 5, 0, 1)        # seules certaines arêtes sont fendues
    crack = np.clip(1 - (e - 0.5) / 1.4, 0, 1) * keep
    ridge = np.clip(1 - np.abs(e - 2.6) / 1.2, 0, 1) * keep
    lichG = np.clip((n01(fbm(S, S, r, base=S / 14, octaves=5)) - 0.68) * 5, 0, 1)
    lichY = np.clip((n01(fbm(S, S, r, base=S / 22, octaves=4)) - 0.78) * 6, 0, 1)
    base = np.array([0.47, 0.44, 0.40])[None, None] * (0.75 + 0.28 * big[..., None]) * (0.86 + 0.18 * mid[..., None]) * (0.94 + 0.08 * fine[..., None])
    base = base * (1 - 0.35 * dark[..., None]) + 0.25 * light[..., None]
    grime = np.clip(1 - mid * 1.6, 0, 1) * 0.3
    col = base * (1 - grime[..., None]) * (1 - 0.65 * crack[..., None]) * (1 + 0.15 * ridge[..., None])
    col = col * (1 - 0.55 * lichG[..., None]) + np.array([0.66, 0.67, 0.60])[None, None] * 0.55 * lichG[..., None]
    col = col * (1 - 0.6 * lichY[..., None]) + np.array([0.62, 0.56, 0.24])[None, None] * 0.6 * lichY[..., None]
    h = 1.2 * mid + 0.4 * big + 0.15 * fine + 0.2 * light - 1.4 * crack + 0.25 * (lichG + lichY)
    return _tex(col, h, 0.16 + 0.1 * mid * (1 - lichG), 0.9)


def sand(S=1024, seed=1005):
    """Sable du Désert des Étoiles : rides éoliennes, grains, paillettes."""
    r = np.random.default_rng(seed)
    xx = np.arange(S)[None, :] / S
    rip = np.sin((xx * 40 + 0.6 * fbm(S, S, r, base=S / 5, octaves=3)) * np.pi * 2) * 0.5 + 0.5
    grain = n01(noise(S, S, 1.5, r))
    big = n01(fbm(S, S, r, base=S / 3, octaves=4))
    col = np.array([0.78, 0.64, 0.42])[None, None] * (0.8 + 0.15 * rip[..., None] + 0.12 * grain[..., None] + 0.1 * big[..., None])
    return _tex(col, 0.8 * rip + 0.3 * grain, 0.25 + 0.1 * grain, 0.5)


def stone_blocks(S=1024, seed=1006, rows=5, tone=(0.62, 0.58, 0.52)):
    """Pierre de taille : assises de hauteurs inégales, blocs de longueurs variées (joints décalés), faces bombées
    avec arêtes biseautées et épaufrures, joints de mortier creux où pousse la mousse, coulures de salissure, teinte
    propre à chaque bloc. Raccordable. UV : 1 répétition = 2 m (assises d'environ 40 cm)."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    hts = r.uniform(0.75, 1.25, rows); hts /= hts.sum(); ys = np.concatenate([[0], np.cumsum(hts)])
    edge = np.zeros((S, S)); bid = np.zeros((S, S)); fyv = np.zeros((S, S)); side = np.zeros((S, S))
    for k in range(rows):
        m = (yy >= ys[k]) & (yy < ys[k + 1]) if k < rows - 1 else (yy >= ys[k])
        w = r.uniform(0.6, 1.4, 64); w = w / w.sum() * 0.26 * 64      # largeur moyenne ≈ 0,5 m
        b = np.concatenate([[0], np.cumsum(w)]); n = np.searchsorted(b, 1.0) ; b = b[:n + 1]; b = b / b[-1]   # bouclage exact
        off = r.uniform(0, 1)
        pos = (xx[m] + off) % 1.0
        i = np.clip(np.searchsorted(b, pos, side='right'), 1, len(b) - 1)
        dx = np.minimum(pos - b[i - 1], b[i] - pos) * S
        fy = (yy[m] - ys[k]) / hts[k]
        dy = np.minimum(fy, 1 - fy) * hts[k] * S
        kk = 6.0; edge[m] = -kk * np.log(np.exp(-dx / kk) + np.exp(-dy / kk))   # coins arrondis
        sx = np.where(pos - b[i - 1] < b[i] - pos, -1.0, 1.0); sy = np.where(fy < 0.5, -1.0, 1.0)
        side[m] = np.where(dx < dy, sx * 0.0 + sx, sy * 0.0 + 2 * sy)   # ±1 : bord gauche/droit, ±2 : bas/haut
        bid[m] = ((i * 7.31 + k * 3.17) * 0.61803) % 1.0; fyv[m] = fy
    warp = n01(fbm(S, S, r, base=S / 40, octaves=3)); wob = (n01(fbm(S, S, r, base=S / 18, octaves=4)) - 0.5) * 2
    edge = edge + 9.0 * wob                                            # contours de blocs irréguliers (taille à la main)
    mw = 3.0 + 3.0 * warp                                              # joint irrégulier
    mortar = np.clip(1 - (edge - mw) / 2.0, 0, 1)
    bevel = np.clip((edge - mw) / (S * 0.018), 0, 1); bevel = bevel * bevel * (3 - 2 * bevel)
    g = n01(fbm(S, S, r, base=S / 14, octaves=6)); fine = n01(fbm(S, S, r, base=S / 120, octaves=3))
    spots = np.clip((n01(fbm(S, S, r, base=S / 70, octaves=3)) - 0.6) * 3, 0, 1)            # grain de la roche (taches)
    chips = np.clip((n01(fbm(S, S, r, base=S / 50, octaves=3)) - 0.68) * 4, 0, 1) * np.clip(1 - (edge - mw) / (S * 0.03), 0, 1)
    face = 0.55 * bevel + 0.25 * g + 0.12 * fine - 0.5 * chips
    tint = (0.78 + 0.32 * bid)[..., None] * np.array([1.0, 0.98 + 0.04 * 0, 0.96])[None, None]
    warm = (np.sin(bid * 37.0) * 0.5 + 0.5)[..., None]
    stone = np.array(tone)[None, None] * tint * (1 + 0.06 * (warm - 0.5) * np.array([1, 0, -1])[None, None])
    stone = stone * (0.72 + 0.32 * g[..., None]) * (0.9 + 0.15 * fine[..., None]) * (1 - 0.25 * chips[..., None])
    streak = np.clip(n01(fbm(S, S, r, base=S / 5, octaves=4, aniso=(1.0, 0.25))) - 0.55, 0, 1) * 1.4
    stone = stone * (1 - 0.18 * streak[..., None]) * (1 - 0.18 * spots[..., None])
    # relief cuit dans la couleur : arêtes hautes et gauches éclairées, basses et droites dans l'ombre
    rim = (1 - bevel) * (1 - mortar)
    light = np.where(side == 2, 0.10, 0) + np.where(side == -1, 0.05, 0) - np.where(side == -2, 0.16, 0) - np.where(side == 1, 0.08, 0)
    stone = stone * (1 + light * rim)[..., None]
    mortar_col = np.array([0.30, 0.28, 0.25])[None, None] * (0.8 + 0.3 * fine[..., None])
    moss = np.clip((n01(fbm(S, S, r, base=S / 8, octaves=4)) - 0.5) * 3, 0, 1)
    mortar_col = mortar_col * (1 - 0.7 * moss[..., None]) + np.array([0.18, 0.24, 0.09])[None, None] * 0.7 * moss[..., None]
    col = stone * (1 - mortar[..., None]) + mortar_col * mortar[..., None]
    col = col * (0.82 + 0.18 * bevel[..., None])                        # arêtes un peu plus sombres (occlusion)
    hgt = 2.6 * (1 - mortar) * (0.45 + 0.55 * bevel) + 0.5 * face
    return _tex(col, hgt, 0.16 + 0.12 * g * (1 - mortar), 1.1, 6, 0.18)


def slate_roof(S=1024, seed=1007, tone=(0.25, 0.26, 0.30)):
    """Ardoises posées à la main : rangées qui se recouvrent, ardoises de largeurs inégales au bas ébréché, légèrement
    de travers, ombre portée sous chaque rang, teintes bleu-gris à violacées, lichens et coulures. 1 répétition = 2 m."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    rows = 12; row = np.floor(yy * rows).astype(int); fy = yy * rows - row          # fy : 0 en haut du rang, 1 au bas
    col = np.zeros((S, S, 3)); hgt = np.zeros((S, S)); sid = np.zeros((S, S))
    g = n01(fbm(S, S, r, base=S / 20, octaves=5)); fine = n01(fbm(S, S, r, base=S / 150, octaves=3))
    chip = n01(fbm(S, S, r, base=S / 60, octaves=3))
    for k in range(rows):
        m = row == k
        w = r.uniform(0.6, 1.4, 40); w = w / w.sum() * 0.1 * 40; b = np.concatenate([[0], np.cumsum(w)]); n = np.searchsorted(b, 1.0); b = b[:n + 1] / b[n]
        off = r.uniform(0, 1); pos = (xx[m] + off) % 1.0
        i = np.clip(np.searchsorted(b, pos, side='right'), 1, len(b) - 1)
        fx = (pos - b[i - 1]) / (b[i] - b[i - 1])
        ids = (i * 13 + k * 7) % 997
        tilt = ((ids * 0.618) % 1.0 - 0.5) * 0.12
        bottom = 0.93 + tilt * (fx - 0.5) - 0.06 * (chip[m] > 0.72) * (np.abs(fx - 0.5) > 0.3)   # bas ébréché
        gapx = np.minimum(fx, 1 - fx) * (b[i] - b[i - 1]) * S
        inside = (fy[m] < bottom) & (gapx > 1.2)
        sid[m] = (ids * 0.618) % 1.0
        hgt[m] = np.where(inside, 0.4 + 0.6 * fy[m], 0.0)                # chaque ardoise remonte sous la suivante
        shade = np.where(inside, 1.0, 0.35) * (0.62 + 0.38 * np.clip(fy[m] * 1.4, 0, 1))   # ombre du rang du dessus
        t = np.array(tone)[None] * (0.8 + 0.35 * sid[m][:, None]) * (1 + 0.1 * (np.sin(sid[m] * 40)[:, None]) * np.array([[-0.5, 0, 1]]))
        col[m] = t * shade[:, None]
    col = col * (0.82 + 0.25 * g[..., None]) * (0.9 + 0.15 * fine[..., None])
    lich = np.clip((n01(fbm(S, S, r, base=S / 10, octaves=4)) - 0.68) * 4, 0, 1) * (hgt > 0)
    col = col * (1 - 0.6 * lich[..., None]) + np.array([0.50, 0.52, 0.32])[None, None] * 0.6 * lich[..., None]
    streak = np.clip(n01(fbm(S, S, r, base=S / 5, octaves=4, aniso=(1.0, 0.2))) - 0.55, 0, 1) * 1.2
    col = col * (1 - 0.25 * streak[..., None])
    return _tex(col, 2.0 * hgt + 0.3 * g, 0.30 + 0.25 * (hgt > 0) * g, 1.2, 4, 0.2)


def planks(S=512, seed=1011, tone=(0.30, 0.20, 0.12)):
    """Bois de charpente : planches ou poutres de largeurs variées, veinage étiré avec nœuds, fentes, bords usés,
    teinte propre à chaque planche, crasse dans les jointures. 1 répétition = 1 m."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    w = r.uniform(0.7, 1.3, 20); w = w / w.sum() * 0.2 * 20; b = np.concatenate([[0], np.cumsum(w)]); n = np.searchsorted(b, 1.0); b = b[:n + 1] / b[n]
    i = np.clip(np.searchsorted(b, xx, side='right'), 1, len(b) - 1)
    fx = (xx - b[i - 1]) / (b[i] - b[i - 1]); pid = (i * 0.618) % 1.0
    seam = np.clip(1 - np.minimum(fx, 1 - fx) * (b[i] - b[i - 1]) * S / 1.8, 0, 1)
    # veinage : anneaux étirés le long de la planche (y), déformés par des nœuds
    warp = fbm(S, S, r, base=S / 6, octaves=4, aniso=(1.0, 0.15))
    knots = np.zeros((S, S))
    for _ in range(9):
        cy, cx = r.uniform(0, 1, 2); rad = r.uniform(0.012, 0.03)
        d = np.sqrt(((yy - cy + 0.5) % 1 - 0.5) ** 2 + (((xx - cx + 0.5) % 1 - 0.5) * 2.2) ** 2)
        knots += np.exp(-(d / rad) ** 2)
        warp += 0.6 * np.exp(-(d / (rad * 4)) ** 2) * np.sin(np.arctan2(yy - cy, xx - cx) * 2)
    rings = np.sin((fx * 6 + pid * 20 + 0.35 * warp) * np.pi * 2) * 0.5 + 0.5
    fibers = n01(fbm(S, S, r, base=S / 200, octaves=3, aniso=(1.0, 0.03)))
    crack = np.clip((n01(fbm(S, S, r, base=S / 30, octaves=3, aniso=(1.0, 0.05))) - 0.88) * 8, 0, 1)
    t = np.array(tone)[None, None] * (0.75 + 0.45 * pid[..., None])
    col = t * (0.78 + 0.22 * rings[..., None]) * (0.85 + 0.25 * fibers[..., None])
    col = col * (1 - 0.55 * np.clip(knots, 0, 1)[..., None]) * (1 - 0.6 * crack[..., None]) * (1 - 0.7 * seam[..., None])
    hgt = 1.0 - seam - 0.8 * crack + 0.15 * rings + 0.2 * fibers - 0.3 * np.clip(knots, 0, 1)
    return _tex(col, hgt, 0.22 + 0.1 * rings, 0.9, 3, 0.15)


def bark(S=512, seed=1008, tone=(0.30, 0.22, 0.15)):
    """Écorce de chêne : plaques verticales épaisses séparées de fissures profondes qui se rejoignent (réseau en
    losanges étirés), arêtes claires usées, mousse et lichens dans les creux."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    f = n01(fbm(S, S, r, base=S / 6, octaves=4, aniso=(1.0, 0.1)))
    f2 = n01(fbm(S, S, r, base=S / 20, octaves=3, aniso=(1.0, 0.15)))
    fiss = np.abs(np.sin((xx * 10 + f * 1.6 + f2 * 0.5) * np.pi))          # bandes verticales ondulées qui se rejoignent
    fiss = gaussian_filter(fiss, 1.0, mode='wrap')
    plate = np.clip((fiss - 0.18) / 0.5, 0, 1) ** 0.7
    cross = np.clip((n01(fbm(S, S, r, base=S / 18, octaves=3, aniso=(0.3, 1.0))) - 0.66) * 5, 0, 1)   # fissures transversales
    plate = plate * (1 - cross * 0.8)
    g = n01(fbm(S, S, r, base=S / 30, octaves=4)); fine = n01(fbm(S, S, r, base=S / 120, octaves=3, aniso=(1.0, 0.08)))
    moss = np.clip((n01(fbm(S, S, r, base=S / 6, octaves=4)) - 0.55) * 3, 0, 1) * (1 - plate)
    col = np.array(tone)[None, None] * (0.4 + 0.7 * plate[..., None]) * (0.85 + 0.25 * g[..., None]) * (0.75 + 0.35 * fine[..., None])
    col = col * (1 + 0.18 * np.clip(plate - 0.85, 0, 1)[..., None] * 6)  # arêtes usées plus claires
    col = col * (1 - 0.45 * moss[..., None]) + np.array([0.17, 0.24, 0.08])[None, None] * 0.45 * moss[..., None]
    return _tex(col, 2.2 * plate + 0.3 * g + 0.2 * fine, 0.12 + 0.08 * moss, 1.2, 3, 0.2)


def leaves(S=1024, seed=1009, c1=(0.16, 0.30, 0.08), c2=(0.34, 0.46, 0.14), kind='broad', cols=2):
    """Atlas de branches feuillues à transparence : rameaux avec dizaines de feuilles (ovales pointues) ou
    aiguilles (conifères), teintes variées, nervure claire."""
    r = np.random.default_rng(seed)
    col = np.zeros((S, S, 3)); alpha = np.zeros((S, S)); hgt = np.zeros((S, S))
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    cw = S // cols
    if kind == 'broad': return _leaf_clumps(S, r, c1, c2)
    if kind == 'needle': return _pine_twigs(S, r, c1, c2, cols)
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


def _pine_twigs(S, r, c1, c2, cols=2):
    """Atlas de rameaux de pin : un axe brun par colonne, rameaux secondaires alternés, aiguilles fines groupées par
    deux qui partent vers l'avant de chaque rameau, plus claires au bout (pousses de l'année)."""
    SS = 2; W = S * SS
    col = np.zeros((W, W, 3)); alpha = np.zeros((W, W)); hgt = np.zeros((W, W))
    cw = W // cols

    def line(x0, y0, x1, y1, wid, c, hval):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) + 2)
        xs = np.linspace(x0, x1, n); ys = np.linspace(y0, y1, n)
        for o in range(-int(wid), int(wid) + 1):
            xi = np.clip(np.round(xs + o * 0.5).astype(int), 0, W - 1); yi = np.clip(np.round(ys).astype(int), 0, W - 1)
            col[yi, xi] = c; alpha[yi, xi] = 1; hgt[yi, xi] = np.maximum(hgt[yi, xi], hval)

    for c in range(cols):
        cx = c * cw + cw / 2
        line(cx, W * 0.98, cx + r.uniform(-0.05, 0.05) * cw, W * 0.02, 3 * SS, (0.22, 0.15, 0.09), 0.6)
        for k in range(28):
            t = 0.05 + 0.9 * k / 27; y = W * (1 - t); side = 1 if k % 2 else -1
            L = cw * r.uniform(0.32, 0.47) * (1.1 - 0.35 * t)
            ang = np.radians(r.uniform(30, 55)); ex = cx + side * L * np.sin(ang); ey = y - L * np.cos(ang)
            line(cx, y, ex, ey, 1.5 * SS, (0.24, 0.17, 0.10), 0.5)
            m = int(L / (1.3 * SS))
            for j in range(m):
                u = (j + 0.5) / m; px = cx + (ex - cx) * u; py = y + (ey - y) * u
                tip = u > 0.8
                for sgn in (-1, 1):
                    na = ang * side + sgn * np.radians(r.uniform(28, 48)) + np.radians(r.uniform(-6, 6))
                    nl = cw * r.uniform(0.09, 0.15)
                    nx = px + np.sin(na) * nl * (1 if side > 0 else 1); ny = py - np.cos(na) * nl
                    tt = r.uniform()
                    cc = (np.array(c1) * (1 - tt) + np.array(c2) * tt) * (1.35 if tip else 1.0) * r.uniform(0.85, 1.15)
                    line(px, py, nx, ny, 0.8 * SS, tuple(np.clip(cc, 0, 1)), 0.8 + 0.2 * r.uniform())
    # réduction (anti-crénelage) et composition couleur sur alpha
    def down(a): return a.reshape(S, SS, S, SS, *a.shape[2:]).mean((1, 3))
    A = down(alpha); C = down(col * alpha[..., None]) / np.maximum(A[..., None], 1e-4); Hh = down(hgt)
    # couleur étendue sous les zones transparentes (pas de liseré noir au mipmap)
    w = gaussian_filter(A, 4) + 1e-4; ext = np.dstack([gaussian_filter(C[..., i] * A, 4) / w for i in range(3)])
    C = np.where(A[..., None] > 0.02, C, ext)
    return TexSet(np.clip(C, 0, 1), height_to_normal(gaussian_filter(Hh, 1), 0.8), np.zeros((S, S)), np.full((S, S), 0.25), np.clip(0.55 + 0.45 * Hh, 0, 1), None, np.clip(A * 1.3, 0, 1))


def _leaf_clumps(S, r, c1, c2, cells=2):
    """Atlas 2 × 2 de touffes rondes : feuilles ovales pointues rayonnant autour d'un rameau, denses au cœur et clairsemées
    au bord (silhouette découpée, pas de carré visible), plus claires en haut (lumière du ciel), nervure centrale."""
    col = np.zeros((S, S, 3)); alpha = np.zeros((S, S)); hgt = np.zeros((S, S))
    yy, xx = np.mgrid[0:S, 0:S].astype(float); C = S // cells
    for ci in range(cells):
        for cj in range(cells):
            ox, oy = ci * C, cj * C; cxc, cyc = ox + C / 2, oy + C / 2
            for k in range(150):
                rr = 0.44 * C * np.sqrt(r.uniform()) ; th = r.uniform(0, 2 * np.pi)
                cx, cy = cxc + rr * np.cos(th), cyc + rr * np.sin(th)
                L = r.uniform(0.07, 0.12) * C; W = L * 0.42
                a = th + r.uniform(-0.6, 0.6)                                  # feuilles tournées vers l'extérieur
                ca, sa = np.cos(a), np.sin(a)
                x0, x1 = int(max(cx - L, ox)), int(min(cx + L, ox + C - 1)); y0, y1 = int(max(cy - L, oy)), int(min(cy + L, oy + C - 1))
                if x1 <= x0 or y1 <= y0: continue
                X = xx[y0:y1, x0:x1] - cx; Y = yy[y0:y1, x0:x1] - cy
                u = (X * ca + Y * sa) / L; v = (-X * sa + Y * ca) / W
                inside = (np.abs(v) < np.sqrt(np.clip(1 - u ** 2, 0, 1)) * (1 - 0.35 * u)) & (np.abs(u) < 1)
                t = r.uniform(); light = 0.8 + 0.35 * (1 - (cy - oy) / C) + 0.15 * (rr / (0.44 * C))
                cc = (np.array(c1) * (1 - t) + np.array(c2) * t) * light
                hv = np.clip(1 - np.abs(v), 0, 1)
                shade = (0.78 + 0.28 * hv) * (1 - 0.3 * (np.abs(v) < 0.07))
                blk = col[y0:y1, x0:x1]; blk[inside] = cc[None] * shade[inside][..., None]
                alpha[y0:y1, x0:x1][inside] = 1.0; hgt[y0:y1, x0:x1][inside] = hv[inside]
    return TexSet(np.clip(col, 0, 1), height_to_normal(gaussian_filter(hgt, 1), 0.8), np.zeros((S, S)), np.full((S, S), 0.3), np.clip(0.55 + 0.45 * hgt, 0, 1), None, alpha)


def water(S=512, seed=1010):
    r = np.random.default_rng(seed)
    w = fbm(S, S, r, base=S / 6, octaves=5)
    col = np.tile(np.array([0.10, 0.22, 0.28]), (S, S, 1)) * (0.9 + 0.2 * n01(w)[..., None])
    return _tex(col, 0.6 * w, np.full((S, S), 0.92), 0.25)
