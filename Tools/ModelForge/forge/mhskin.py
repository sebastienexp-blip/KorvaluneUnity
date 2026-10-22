"""Textures de peau peintes dans l'espace UV du corps MakeHuman : on rastérise le maillage dans ses UV pour connaître,
pour chaque texel, sa position 3D ; teint, rougeurs, lèvres, sourcils, barbe naissante, cicatrices et maquillage sont
ensuite décrits en 3D à partir des repères du visage (yeux, bouche)."""
import numpy as np
from scipy.ndimage import gaussian_filter, distance_transform_edt
from .texgen import TexSet, fbm, noise, n01, height_to_normal
from .geo import vertex_normals

G = lambda d2, s: np.exp(-d2 / (s * s))


def uv_raster(m, S):
    """Carte position (S,S,3), normale, masque de couverture. Ligne 0 = v = 1."""
    pos = np.zeros((S, S, 3)); nrm = np.zeros((S, S, 3)); cov = np.zeros((S, S), bool)
    N = vertex_normals(m)
    UV = m.UV.copy(); X = UV[:, 0] * S; Y = (1 - UV[:, 1]) * S
    for f in m.F:
        a, b, c = f
        xa, ya, xb, yb, xc, yc = X[a], Y[a], X[b], Y[b], X[c], Y[c]
        x0, x1 = int(max(0, np.floor(min(xa, xb, xc)))), int(min(S - 1, np.ceil(max(xa, xb, xc))))
        y0, y1 = int(max(0, np.floor(min(ya, yb, yc)))), int(min(S - 1, np.ceil(max(ya, yb, yc))))
        if x1 < x0 or y1 < y0: continue
        den = (yb - yc) * (xa - xc) + (xc - xb) * (ya - yc)
        if abs(den) < 1e-12: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        w0 = ((yb - yc) * (xs - xc) + (xc - xb) * (ys - yc)) / den
        w1 = ((yc - ya) * (xs - xc) + (xa - xc) * (ys - yc)) / den
        w2 = 1 - w0 - w1
        ins = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not ins.any(): continue
        p = w0[..., None] * m.V[a] + w1[..., None] * m.V[b] + w2[..., None] * m.V[c]
        n = w0[..., None] * N[a] + w1[..., None] * N[b] + w2[..., None] * N[c]
        sub = (slice(y0, y1 + 1), slice(x0, x1 + 1))
        pos[sub][ins] = p[ins]; nrm[sub][ins] = n[ins]; cov[sub][ins] = True
    # prolonge les îlots au-delà de leurs bords (pas de coutures visibles avec le filtrage)
    _, (iy, ix) = distance_transform_edt(~cov, return_indices=True)
    pos = pos[iy, ix]; nrm = nrm[iy, ix]
    return pos, nrm, cov


def skin(m, marks, S=2048, tone=(0.78, 0.60, 0.50), seed=1, female=False, stubble=0.0, beard_col=(0.12, 0.08, 0.05),
         lip=None, makeup=None, pale=0.0, scars=(), brows=(0.08, 0.055, 0.04), dirt=0.0, veins=None, photo=None, tint=None, beard_paint=0.0):
    """photo : texture de peau MakeHuman (CC0, même dépliage UV que le corps) utilisée comme base ; tint : multiplicateur RGB.
    Sans photo, le teint est peint (ancienne méthode). beard_paint : barbe courte peinte (0 → 1)."""
    r = np.random.default_rng(seed)
    pos, nrm, cov = uv_raster(m, S)
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    eL, eR, mo = marks['eye_L'], marks['eye_R'], marks['mouth']
    d2 = lambda c: ((pos - np.asarray(c)) ** 2).sum(-1)
    face = (z > 0.03) & (y > 1.55)
    if photo is not None:
        from PIL import Image
        base = np.asarray(Image.open(photo).convert('RGB').resize((S, S), Image.LANCZOS), float) / 255
        if tint is not None: base = base * np.array(tint)[None, None]
        base = base * (1 - pale) + np.array([0.80, 0.80, 0.84]) * pale * (0.85 + 0.15 * base.mean(-1, keepdims=True))
        return _skin_extras(base, pos, marks, S, r, stubble, beard_col, scars, dirt, veins, beard_paint, makeup)
    base = np.array(tone)[None, None] * (1 + 0.035 * fbm(S, S, r, base=S / 8, octaves=4)[..., None])
    base = base * (1 - pale) + np.array([0.80, 0.80, 0.84]) * pale
    # rougeurs : joues, nez, oreilles, articulations
    flush = G(d2(eL + (0.022, -0.03, 0.0)), 0.025) + G(d2(eR + (-0.022, -0.03, 0.0)), 0.025) + 0.7 * G(d2((0, mo[1] + 0.035, mo[2] + 0.02)), 0.015)
    base = base * (1 - 0.12 * flush[..., None]) + np.array([0.78, 0.40, 0.38]) * 0.12 * flush[..., None]
    # lèvres
    lipd = ((x / 0.024) ** 2 + ((y - mo[1] - 0.001) / 0.0068) ** 2)
    lips = np.clip(1.2 - lipd, 0, 1) ** 0.7 * (z > mo[2] - 0.02) * face
    lipc = np.array(tone) * np.array([0.92, 0.70, 0.68]) if lip is None else np.array(lip)
    base = base * (1 - 0.6 * lips[..., None]) + lipc * 0.6 * lips[..., None]
    mline = np.exp(-((y - mo[1] - 0.0005) / 0.0011) ** 2) * np.clip(1 - (x / 0.024) ** 2, 0, 1) * face
    base = base * (1 - 0.45 * mline[..., None])
    # orbites (léger creux sombre), cils (ligne sombre), maquillage
    for e in (eL, eR):
        ring = G(d2(e), 0.020) * face
        base = base * (1 - 0.18 * ring[..., None])
        lash = np.exp(-(((y - e[1] - 0.006) / 0.0025) ** 2)) * G((x - e[0]) ** 2, 0.016) * face * (z > e[2] - 0.01)
        base = base * (1 - 0.50 * np.clip(lash, 0, 1)[..., None])
        if makeup is not None:
            mk = G(d2(e + (0.004 * np.sign(e[0]), 0.006, 0)), 0.018) * face
            base = base * (1 - 0.65 * mk[..., None]) + np.array(makeup) * 0.65 * mk[..., None]
        # sourcils
        bx = (x - e[0] - 0.004 * np.sign(e[0])) / 0.024
        by = y - e[1] - 0.020 - 0.006 * (1 - bx ** 2) * (1 if female else 0.5)
        out_ = np.clip(bx * np.sign(e[0]), -1, 1)                 # -1 : tête du sourcil (côté nez), +1 : queue
        brow = np.exp(-(by / ((0.0035 if female else 0.0046) * (1.15 - 0.45 * np.clip(out_, 0, 1)))) ** 2) * np.clip(1 - np.abs(bx) ** 3, 0, 1) * face * (z > e[2] - 0.03)
        if 'strokes' not in locals(): strokes = n01(noise(S, S, 2.0, r, aniso=(0.35, 4.0)))   # traits de poils allongés (horizontaux en UV)
        brow *= 0.25 + 0.75 * np.clip((strokes - 0.42) * 4, 0, 1)
        base = base * (1 - 0.80 * np.clip(brow, 0, 1)[..., None]) + np.array(brows) * 0.80 * np.clip(brow, 0, 1)[..., None]
    hgt = 0.12 * noise(S, S, 1.0, r) + 0.10 * fbm(S, S, r, base=10, octaves=3)
    if stubble:
        jaw = marks['jaw']
        zone = (y < mo[1] + 0.018) & (y > jaw[1] - 0.04) & (z > -0.02) & (y > 1.50)
        zone = zone | ((y < mo[1] + 0.022) & (np.abs(x) < 0.03) & (y > mo[1]) & face)
        zone = gaussian_filter(zone.astype(float), 3) * (1 - np.clip(lips * 3, 0, 1))
        dots = (r.random((S, S)) < 0.35) * zone
        base = base * (1 - stubble * 0.55 * gaussian_filter(dots, 0.7)[..., None]) + np.array(beard_col) * stubble * 0.25 * zone[..., None]
        hgt += 0.4 * dots
    for (c, d, L) in scars:      # (point, direction, longueur)
        c = np.asarray(c); d = np.asarray(d) / np.linalg.norm(d)
        rel = pos - c; t = np.clip(rel @ d, 0, L); q = rel - t[..., None] * d
        sc = np.exp(-(q ** 2).sum(-1) / 0.0012 ** 2) * face
        base = base * (1 - 0.35 * sc[..., None]) + np.array([0.72, 0.45, 0.42]) * 0.35 * sc[..., None]; hgt += 4 * sc
    if veins is not None:   # veines lumineuses (nécromancienne)
        vn = np.clip(1 - np.abs(fbm(S, S, r, base=40, octaves=4, aniso=(1.0, 0.6))) * 6, 0, 1)
        base = base * (1 - 0.25 * vn[..., None]) + np.array(veins) * 0.25 * vn[..., None]
    if dirt:
        dm = np.clip(n01(fbm(S, S, r, base=S / 10, octaves=4)) - 0.55, 0, 1) * 2 * dirt
        base = base * (1 - 0.35 * dm[..., None])
    smooth = np.clip(0.34 + 0.08 * lips + 0.06 * face - 0.1 * (stubble > 0) * 0, 0, 1)
    nm = height_to_normal(hgt, 0.35 * S / 1024)
    return TexSet(np.clip(base, 0, 1), nm, np.zeros((S, S)), smooth, np.clip(1 - 0.12 * flush, 0, 1))


def _skin_extras(base, pos, marks, S, r, stubble, beard_col, scars, dirt, veins, beard_paint, makeup):
    """Effets ajoutés à une texture de peau photo : barbe peinte / naissante, cicatrices, saleté, veines, maquillage."""
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    eL, eR, mo, jaw = marks['eye_L'], marks['eye_R'], marks['mouth'], marks['jaw']
    face = (z > 0.03) & (y > 1.55)
    hgt = 0.10 * noise(S, S, 1.0, r) + 0.08 * fbm(S, S, r, base=10, octaves=3)
    lips = np.clip(1.2 - ((x / 0.024) ** 2 + ((y - mo[1] - 0.001) / 0.0068) ** 2), 0, 1) * (z > mo[2] - 0.02) * face
    amt = max(stubble, beard_paint)
    if amt:
        # zone de barbe : joues basses, menton, moustache, cou ; bord supérieur fondu (pas de ligne dure)
        top_ = mo[1] + 0.020 - 0.012 * np.clip(np.abs(x) / 0.055, 0, 1)
        under = jaw[1] - 0.010 - 0.025 * smooth01((z - (jaw[2] - 0.06)) / 0.06)       # sous le menton seulement, pas tout le cou
        zone = smooth01((top_ - y) / 0.014) * smooth01((y - under) / 0.012) * smooth01((z - (jaw[2] - 0.075)) / 0.03) * (y > 1.48)
        must = smooth01((mo[1] + 0.017 - y) / 0.004) * smooth01((y - mo[1] - 0.004) / 0.003) * smooth01((0.030 - np.abs(x)) / 0.008) * (z > mo[2] - 0.02)
        zone = np.clip(np.maximum(zone, must) * (1 - np.clip(lips * 2.5, 0, 1)), 0, 1)
        strokes = n01(noise(S, S, 1.2, r, aniso=(1.0, 0.4)))
        hairs = np.clip((strokes - (0.62 - 0.30 * beard_paint)) * 5, 0, 1) * zone
        cover = zone * (0.18 * stubble + 0.38 * beard_paint)
        dark = np.clip(cover + gaussian_filter(hairs, 0.6) * (0.30 * stubble + 0.40 * beard_paint), 0, 0.85)
        base = base * (1 - dark[..., None]) + np.array(beard_col) * dark[..., None]
        hgt += 0.5 * hairs
    for (c, d, L) in scars:
        c = np.asarray(c); d = np.asarray(d) / np.linalg.norm(d)
        rel = pos - c; t = np.clip(rel @ d, 0, L); q = rel - t[..., None] * d
        sc = np.exp(-(q ** 2).sum(-1) / 0.0012 ** 2) * face
        base = base * (1 - 0.30 * sc[..., None]) + np.array([0.72, 0.45, 0.42]) * 0.30 * sc[..., None]; hgt += 4 * sc
    if makeup is not None:
        for e in (eL, eR):
            mk = G(((pos - (np.asarray(e) + (0.004 * np.sign(e[0]), 0.006, 0))) ** 2).sum(-1), 0.016) * face
            base = base * (1 - 0.55 * mk[..., None]) + np.array(makeup) * 0.55 * mk[..., None]
    if veins is not None:
        vn = np.clip(1 - np.abs(fbm(S, S, r, base=40, octaves=4, aniso=(1.0, 0.6))) * 6, 0, 1)
        base = base * (1 - 0.22 * vn[..., None]) + np.array(veins) * 0.22 * vn[..., None]
    if dirt:
        dm = np.clip(n01(fbm(S, S, r, base=S / 10, octaves=4)) - 0.55, 0, 1) * 2 * dirt
        base = base * (1 - 0.30 * dm[..., None])
    smooth = np.clip(0.36 + 0.10 * lips + 0.05 * face, 0, 1)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.35 * S / 1024), np.zeros((S, S)), smooth, np.ones((S, S)))


def smooth01(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def eye_tex(S=512, iris=(0.30, 0.22, 0.12), glow=None):
    iris_color = iris
    """Texture de globe oculaire pour les UV des yeux MakeHuman (iris au centre de l'image)."""
    from PIL import Image
    import os
    p = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'third_party', 'makehuman', 'eyes', 'brown_eye.png')
    im = np.asarray(Image.open(p).convert('RGB').resize((S, S)), float) / 255.0
    r_, g_, b_ = im[..., 0], im[..., 1], im[..., 2]
    iris = np.clip((r_ - 0.5 * (g_ + b_) - 0.08) * 6, 0, 1)
    from scipy.ndimage import gaussian_filter as _gf
    iris = np.clip(_gf(iris, S / 400), 0, 1)[..., None]
    lum = (0.3 * r_ + 0.59 * g_ + 0.11 * b_)[..., None]
    base = im * (1 - iris) + np.clip(lum * 3.2 * np.array(iris_color), 0, 1) * iris
    sat = iris
    emit = None
    if glow is not None: emit = np.clip(sat * np.array(glow) * 1.5, 0, 1)
    z = np.zeros((S, S))
    return TexSet(np.clip(base, 0, 1), np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.85, z + 1, emit)
