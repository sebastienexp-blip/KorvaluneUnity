"""Matériaux PBR du chevalier : acier bruni et usé, laiton gravé, cuir, maille rivetée, gambison matelassé,
drap de laine rouge (cape, texture non répétée avec galon brodé), intérieur du casque."""
import numpy as np
from scipy.ndimage import gaussian_filter, map_coordinates
from PIL import Image, ImageDraw, ImageFilter
from forge.texgen import TexSet, fbm, noise, n01, height_to_normal, cavity_ao, scratches, voronoi_edges


def steel(S=2048, seed=11, tone=(0.36, 0.355, 0.37), polish=0.62, rust_amt=1.0):
    """Acier de plates : poli à l'origine, bruni par l'usage. Coups de marteau du forgeron, rayures de combat,
    zones ternies et piquetées. UV en mètres / 0,35 m par répétition."""
    r = np.random.default_rng(seed)
    hammer = fbm(S, S, r, base=S / 6, octaves=3, gain=0.5)                    # martelage (cupules larges)
    pits = np.clip(fbm(S, S, r, base=6, octaves=2) - 2.2, 0, None)            # piqûres
    brushed = fbm(S, S, r, base=S / 10, octaves=4, aniso=(0.08, 1.0))          # polissage directionnel
    sc_fine = gaussian_filter(scratches(S, S, r, 900, (0.01, 0.06), (1, 1)), 0.5, mode='wrap')
    sc_big = gaussian_filter(scratches(S, S, r, 70, (0.04, 0.22), (1, 3), angle=0.5, spread=0.9), 0.7, mode='wrap')
    tarnish = n01(fbm(S, S, r, base=S / 3, octaves=4))                       # ternissure
    grime = np.clip((tarnish - 0.58) * 3.0, 0, 1)
    rust = rust_amt * np.clip((n01(fbm(S, S, r, base=S / 5, octaves=5)) - 0.80) * 6, 0, 1) * np.clip(n01(fbm(S, S, r, base=24, octaves=3)) * 1.6 - 0.3, 0, 1)
    hgt = 4.0 * hammer + 0.25 * brushed - 1.0 * sc_big - 0.45 * sc_fine - 1.5 * pits
    normal = height_to_normal(hgt, 0.55 * S / 512)
    base = np.array(tone)[None, None, :] * (1 + 0.035 * brushed[..., None] + 0.03 * hammer[..., None])
    base = base * (1 - 0.35 * grime[..., None]) + 0.14 * (0.6 * sc_big + 0.4 * sc_fine)[..., None]
    rustc = np.array([0.30, 0.14, 0.07]) * (0.7 + 0.5 * n01(fbm(S, S, r, base=16, octaves=2)))[..., None]
    base = base * (1 - rust[..., None]) + rustc * rust[..., None]
    metal = np.clip(1 - rust * 0.9 - grime * 0.08, 0, 1)
    smooth = np.clip(polish - 0.25 * grime - 0.12 * sc_fine - 0.10 * sc_big + 0.04 * brushed - 0.6 * rust - 0.3 * np.clip(pits, 0, 1), 0.08, 0.92)
    ao = cavity_ao(hgt, 6, 0.08) * (1 - 0.12 * grime)
    return TexSet(np.clip(base, 0, 1), normal, metal, smooth, ao)


def brass(S=1024, seed=21):
    """Laiton doré gravé : rinceaux de feuillage (creux noircis), reliefs polis. 0,10 m par répétition."""
    r = np.random.default_rng(seed)
    im = Image.new('L', (S, S), 0); d = ImageDraw.Draw(im)
    # rinceau : tige ondulée + volutes, répétée 2 fois par tuile (raccord parfait sur les bords)
    for row in range(2):
        y0 = S * (0.25 + 0.5 * row)
        xs = np.linspace(0, S, 400)
        ys = y0 + np.sin(xs / S * 2 * np.pi * 2 + row * np.pi) * S * 0.09
        pts = list(zip(xs, ys))
        for oy in (-S, 0, S): d.line([(x, y + oy) for x, y in pts], fill=255, width=max(3, S // 140))
        for k in range(4):
            cx = (k + 0.25 + 0.5 * (row % 2)) / 4 * S; ph = np.sin(cx / S * 2 * np.pi * 2 + row * np.pi)
            cy = y0 + ph * S * 0.09; sgn = -1 if ph > 0 else 1
            th = np.linspace(0, 3.6 * np.pi, 120); rad = S * 0.075 * (1 - th / (4.2 * np.pi))
            vx = cx + np.cos(th) * rad; vy = cy + sgn * (S * 0.075) + np.sin(th) * rad * sgn
            for ox in (-S, 0, S):
                for oy in (-S, 0, S):
                    d.line([(x + ox, y + oy) for x, y in zip(vx, vy)], fill=255, width=max(2, S // 200))
            # feuille
            lx, ly = cx + S * 0.05, cy - sgn * S * 0.05
            leaf = [(lx + np.cos(a) * S * 0.045 * np.sin(a * 0.5) ** 0.5 * 2, ly + np.sin(a) * S * 0.018) for a in np.linspace(0, 2 * np.pi, 30)]
            for ox in (-S, 0, S):
                for oy in (-S, 0, S): d.polygon([(x + ox, y + oy) for x, y in leaf], fill=200)
    etch = np.asarray(im.filter(ImageFilter.GaussianBlur(1.2)), float) / 255
    stipple = (r.random((S, S)) < 0.06) * (1 - etch) * 0.35                  # fond pointillé (matoir)
    wear = n01(fbm(S, S, r, base=S / 4, octaves=4))
    hgt = -3.0 * etch - 0.8 * gaussian_filter(stipple, 0.6, mode='wrap') + 0.4 * fbm(S, S, r, base=S / 8, octaves=3)
    normal = height_to_normal(hgt, 0.9 * S / 1024)
    gold = np.array([0.80, 0.60, 0.30]); dark = np.array([0.16, 0.11, 0.06])
    base = gold[None, None] * (1 - 0.15 * wear[..., None]) * (1 - etch[..., None]) + dark * etch[..., None]
    metal = 1 - etch * 0.6
    smooth = np.clip(0.78 - 0.45 * etch - 0.18 * wear - 0.2 * stipple, 0.1, 0.9)
    ao = np.clip(1 - 0.55 * etch, 0, 1)
    return TexSet(np.clip(base, 0, 1), normal, metal, smooth, ao)


def leather(S=1024, seed=31):
    r = np.random.default_rng(seed)
    cells = voronoi_edges(S // 2, S // 2, r, 700)
    cells = np.kron(cells, np.ones((2, 2))); cells = n01(gaussian_filter(cells, 1.0, mode='wrap'))
    crease = gaussian_filter(scratches(S, S, r, 160, (0.02, 0.09), (1, 2), angle=0.0, spread=0.25), 1.2, mode='wrap')
    mott = fbm(S, S, r, base=S / 5, octaves=5)
    hgt = 2.0 * np.sqrt(cells) - 2.0 * crease + 0.3 * mott
    normal = height_to_normal(hgt, 0.8 * S / 1024)
    base = np.array([0.22, 0.13, 0.075])[None, None] * (1 + 0.18 * mott[..., None]) * (0.8 + 0.25 * cells[..., None]) * (1 - 0.3 * crease[..., None])
    worn = np.clip((n01(fbm(S, S, r, base=S / 3, octaves=3)) - 0.6) * 2.5, 0, 1)
    base = base * (1 - worn[..., None] * 0.35) + np.array([0.32, 0.22, 0.14]) * worn[..., None] * 0.35
    smooth = np.clip(0.42 + 0.1 * mott - 0.2 * crease + 0.12 * worn, 0.1, 0.7)
    return TexSet(np.clip(base, 0, 1), normal, np.zeros((S, S)), smooth, cavity_ao(hgt, 3, 0.2))


def chainmail(S=1024, seed=41, rings=16):
    """Maille 4-en-1 : anneaux rivetés en rangées alternées. rings anneaux par tuile (tuile ≈ 0,12 m)."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S * rings
    hgt = np.full((S, S), -1.0); occ = np.zeros((S, S))
    R, w = 0.55, 0.16
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cy = np.floor(yy) + oy; row = cy.astype(int)
            cx = np.floor(xx - 0.5 * (row % 2)) + ox + 0.5 * (row % 2)
            dx = xx - (cx + 0.5); dy = (yy - (cy + 0.5)) * 1.25
            tilt = np.where(row % 2 == 0, 1.0, -1.0)
            rr = np.sqrt(dx ** 2 + dy ** 2)
            ring = np.clip(1 - np.abs(rr - R) / w, 0, 1)
            z = np.sqrt(ring) * (0.6 + 0.35 * tilt * dx / (R + 1e-6))
            better = z > hgt; hgt = np.where(better, z, hgt)
    gap = hgt <= 0
    hgt = np.where(gap, -0.6, hgt)
    j = noise(S, S, 3, r) * 0.05
    normal = height_to_normal(gaussian_filter(hgt, 0.8, mode='wrap') * S / rings * 0.35 + j, 0.6)
    tar = n01(fbm(S, S, r, base=S / 3, octaves=3))
    base = np.where(gap[..., None], np.array([0.03, 0.03, 0.03]), np.array([0.46, 0.46, 0.47]) * (0.75 + 0.3 * hgt[..., None]) * (1 - 0.3 * tar[..., None]))
    metal = np.where(gap, 0.0, 1.0)
    smooth = np.where(gap, 0.1, np.clip(0.55 + 0.2 * hgt - 0.2 * tar, 0.2, 0.85))
    ao = np.clip(np.where(gap, 0.15, 0.55 + 0.45 * hgt), 0, 1)
    return TexSet(np.clip(base, 0, 1), normal, metal, smooth, ao)


def gambeson(S=1024, seed=51):
    """Gambison de lin matelassé (piqûres verticales tous les 3 cm), teinte noir-bordeaux. Tuile ≈ 0,15 m."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    ch = 5                                                             # canaux par tuile
    u = (xx * ch) % 1.0
    puff = np.sin(np.pi * u) ** 0.6
    stitch = (np.abs(u - 0.0) < 0.012) | (np.abs(u - 1.0) < 0.012)
    dots = (np.sin(yy * S / 6 * np.pi) > 0.2)
    weave = 0.5 * np.sin(xx * S * np.pi / 2.0) * np.sin(yy * S * np.pi / 2.0) + 0.3 * noise(S, S, 1.5, r)
    hgt = 6.0 * puff + 0.6 * weave + 1.2 * fbm(S, S, r, base=S / 6, octaves=3) - 2.0 * (stitch & dots)
    normal = height_to_normal(gaussian_filter(hgt, 0.7, mode='wrap'), 0.5 * S / 1024)
    base = np.array([0.16, 0.075, 0.07])[None, None] * (0.75 + 0.3 * puff[..., None]) * (1 + 0.08 * weave[..., None])
    base = np.where((stitch & dots)[..., None], np.array([0.30, 0.25, 0.20]), base)
    dirt = np.clip(n01(fbm(S, S, r, base=S / 3, octaves=4)) - 0.5, 0, 1)
    base = base * (1 - 0.4 * dirt[..., None])
    return TexSet(np.clip(base, 0, 1), normal, np.zeros((S, S)), np.clip(0.18 + 0.05 * puff, 0, 1), np.clip(0.55 + 0.45 * puff, 0, 1))


def cloak(W=1024, H=2048, seed=61):
    """Cape de laine rouge (texture unique, non répétée) : u = largeur (0 gauche → 1 droite), v = hauteur (0 bas → 1 col).
    Sergé de laine, feutrage, galon brodé d'or en bas et sur les côtés, usure et boue sur l'ourlet."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    v = 1 - yy / (H - 1); u = xx / (W - 1)
    twill = np.sin((xx + yy) * np.pi / 3.0) * 0.6 + np.sin((xx - yy * 0.5) * np.pi / 1.5) * 0.2
    fuzz = fbm(H, W, r, base=18, octaves=3); big = fbm(H, W, r, base=300, octaves=4)
    hgt = 0.8 * twill + 0.9 * fuzz
    crimson = np.array([0.42, 0.045, 0.05])
    base = crimson[None, None] * (1 + 0.10 * big[..., None] + 0.06 * fuzz[..., None] + 0.05 * twill[..., None])
    # galon brodé : bande en bas (v < 0.055) et sur les bords (u < 0.035, u > 0.965)
    band = np.zeros((H, W))
    bh = 0.055
    band_b = (v < bh) & (v > 0.012)
    band_s = ((u < 0.032) | (u > 0.968)) & (v > 0.012)
    band[band_b | band_s] = 1
    # motif brodé : losanges et points (fil d'or)
    bu = np.where(band_s, (v * H / 64.0), (u * W / 64.0)); bv = np.where(band_s, np.minimum(u, 1 - u) / 0.032, (v - 0.012) / (bh - 0.012))
    diamond = (np.abs((bu % 1.0) - 0.5) + np.abs(bv - 0.5) < 0.38) & ~(np.abs((bu % 1.0) - 0.5) + np.abs(bv - 0.5) < 0.24)
    border = (np.abs(bv - 0.08) < 0.05) | (np.abs(bv - 0.92) < 0.05)
    thread = band * (diamond | border)
    thread_h = gaussian_filter(thread.astype(float), 1.0)
    gold = np.array([0.72, 0.52, 0.20]) * (0.85 + 0.25 * np.sin(yy * np.pi / 1.7)[..., None] * np.sin(xx * np.pi / 2.1)[..., None])
    base = np.where(thread[..., None] > 0, gold, base * np.where(band[..., None] > 0, 0.55, 1.0))
    hgt = hgt + 3.0 * thread_h
    # usure, déchirures sombres et boue sur le bas
    edge_wear = np.clip(1 - v / 0.12, 0, 1) * n01(fbm(H, W, r, base=40, octaves=4))
    mud = np.clip((1 - v / 0.22) * 1.3 - 0.25 + 0.4 * fbm(H, W, r, base=60, octaves=4) * 0.5, 0, 1) ** 1.5
    base = base * (1 - 0.35 * edge_wear[..., None]) * (1 - 0.55 * mud[..., None]) + np.array([0.12, 0.09, 0.06]) * 0.55 * mud[..., None]
    sun = np.clip(v - 0.6, 0, 1) * 0.15                                 # haut un peu passé
    base = base * (1 + sun[..., None])
    normal = height_to_normal(hgt, 0.6 * W / 1024)
    metal = thread * 0.85
    smooth = np.clip(0.12 + 0.45 * thread - 0.05 * mud, 0, 1)
    ao = np.clip(1 - 0.15 * np.clip(-fuzz, 0, 3) - 0.2 * mud, 0, 1)
    return TexSet(np.clip(base, 0, 1), normal, metal, smooth, ao)


def void(S=64):
    z = np.zeros((S, S))
    return TexSet(np.full((S, S, 3), 0.02), np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.05, z + 0.2)


def blade(S=1024, seed=71):
    """Lame polie : acier clair, rayures d'affûtage longitudinales, peu de rouille."""
    return steel(S, seed, tone=(0.58, 0.58, 0.60), polish=0.86, rust_amt=0.15)


def wood(S=1024, seed=81):
    """Chêne sombre (dos du bouclier) : veines longitudinales et pores."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    warp = fbm(S, S, r, base=S / 3, octaves=4)
    rings = np.sin((xx * 22 + warp * 1.6) * 2 * np.pi)
    grain = fbm(S, S, r, base=S / 2, octaves=5, aniso=(4.0, 0.15))
    hgt = 0.6 * rings + 1.2 * grain
    base = np.array([0.20, 0.12, 0.07])[None, None] * (1 + 0.25 * rings[..., None] + 0.15 * grain[..., None])
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.7 * S / 1024), np.zeros((S, S)), np.clip(0.30 + 0.05 * grain, 0, 1), cavity_ao(hgt, 4, 0.15))
