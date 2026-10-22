"""Outils de textures procédurales tuilables (sans couture) et écriture au format URP Lit :
BaseMap (sRGB), Normal (OpenGL, Y+), MetallicSmoothness (R = métal, A = lissé), Occlusion (niveaux de gris)."""
import os
import numpy as np
from scipy.ndimage import gaussian_filter, map_coordinates
from PIL import Image, ImageDraw


def noise(h, w, scale, rng, aniso=(1.0, 1.0)):
    """Bruit lisse tuilable : scale = taille de motif en pixels."""
    n = rng.standard_normal((h, w))
    s = max(scale / 2.5, 0.3)
    n = gaussian_filter(n, (s * aniso[0], s * aniso[1]), mode='wrap')
    return (n - n.mean()) / (n.std() + 1e-9)


def fbm(h, w, rng, base=256, octaves=5, gain=0.55, aniso=(1.0, 1.0)):
    out = np.zeros((h, w)); a = 1.0; s = base; tot = 0
    for _ in range(octaves):
        out += a * noise(h, w, s, rng, aniso); tot += a; a *= gain; s /= 2
    out /= tot
    return (out - out.mean()) / (out.std() + 1e-9)


def n01(a):
    a = a - a.min(); return a / (a.max() + 1e-9)


def height_to_normal(hgt, strength):
    """hgt en « pixels de relief » × strength → normale OpenGL encodée (0..1)."""
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5 * strength
    gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5 * strength
    n = np.stack([-gx, gy, np.ones_like(hgt)], -1)         # image : ligne 0 en haut → Y+ vers le haut de l'image
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def cavity_ao(hgt, radius, strength=1.0):
    """Occlusion des creux : écart entre la hauteur et sa moyenne locale."""
    blur = gaussian_filter(hgt, radius, mode='wrap')
    c = np.clip(hgt - blur, None, 0)
    return np.clip(1 + c * strength, 0, 1)


def scratches(h, w, rng, n, length=(0.02, 0.15), width=(1, 2), angle=None, spread=0.4):
    im = Image.new('L', (w, h), 0); d = ImageDraw.Draw(im)
    for _ in range(n):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        L = rng.uniform(*length) * w
        a = (rng.uniform(0, np.pi) if angle is None else angle + rng.normal(0, spread))
        dx, dy = np.cos(a) * L, np.sin(a) * L
        v = int(rng.uniform(90, 255)); wd = int(rng.integers(width[0], width[1] + 1))
        # trait légèrement courbe, effilé
        pts = [(x + dx * t + rng.normal(0, 0.6), y + dy * t + rng.normal(0, 0.6)) for t in np.linspace(0, 1, 6)]
        for ox in (-w, 0, w):
            for oy in (-h, 0, h):
                d.line([(px + ox, py + oy) for px, py in pts], fill=v, width=wd)
    return np.asarray(im, float) / 255.0


def voronoi_edges(h, w, rng, cells):
    """Distance au 2e germe moins distance au 1er (bords de cellules), tuilable."""
    from scipy.spatial import cKDTree
    pts = rng.uniform(0, 1, (cells, 2)) * (w, h)
    tiles = np.vstack([pts + (ox * w, oy * h) for ox in (-1, 0, 1) for oy in (-1, 0, 1)])
    yy, xx = np.mgrid[0:h, 0:w]
    d, _ = cKDTree(tiles).query(np.c_[xx.ravel(), yy.ravel()], k=2)
    return (d[:, 1] - d[:, 0]).reshape(h, w)


def srgb_to_lin(c): c = np.clip(c, 0, 1); return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin_to_srgb(c): c = np.clip(c, 0, 1); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


class TexSet:
    """Jeu de textures d'un matériau (valeurs 0..1). base : sRGB ; normal : encodée ; metal, smooth, ao : (h, w)."""
    def __init__(self, base, normal, metal, smooth, ao, emit=None, alpha=None):
        self.base, self.normal, self.metal, self.smooth, self.ao, self.emit = base, normal, metal, smooth, ao, emit
        self.alpha = alpha       # découpe (cheveux, cils…) : stockée dans le canal A de la BaseMap

    def images(self):
        """Pour l'aperçu logiciel : couleur LINÉAIRE, normales, masque (R métal, A lissé), occlusion."""
        mask = np.zeros(self.base.shape[:2] + (4,)); mask[..., 0] = self.metal; mask[..., 3] = self.smooth
        d = {'base': srgb_to_lin(self.base), 'normal': self.normal, 'mask': mask, 'ao': self.ao[..., None]}
        if self.emit is not None: d['emit'] = srgb_to_lin(self.emit)
        if self.alpha is not None: d['alpha'] = self.alpha[..., None]
        return d

    def save(self, folder, prefix):
        os.makedirs(folder, exist_ok=True)
        u8 = lambda a: (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
        files = {}
        files['baseMap'] = f'{prefix}_BaseMap.png'
        if self.alpha is not None: Image.fromarray(u8(np.dstack([self.base, self.alpha])), 'RGBA').save(os.path.join(folder, files['baseMap']), optimize=True)
        else: Image.fromarray(u8(self.base)).save(os.path.join(folder, files['baseMap']), optimize=True)
        files['normalMap'] = f'{prefix}_Normal.png'; Image.fromarray(u8(self.normal)).save(os.path.join(folder, files['normalMap']), optimize=True)
        ms = np.dstack([self.metal, self.metal, self.metal, self.smooth])
        files['maskMap'] = f'{prefix}_MetallicSmoothness.png'; Image.fromarray(u8(ms), 'RGBA').save(os.path.join(folder, files['maskMap']), optimize=True)
        files['occlusionMap'] = f'{prefix}_Occlusion.png'; Image.fromarray(u8(self.ao), 'L').save(os.path.join(folder, files['occlusionMap']), optimize=True)
        if self.emit is not None:
            files['emissionMap'] = f'{prefix}_Emission.png'; Image.fromarray(u8(self.emit)).save(os.path.join(folder, files['emissionMap']), optimize=True)
        return files
