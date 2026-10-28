"""Découpe les planches d'icônes peintes (icons/planches/planche1..4.png, 4 × 2 objets sur fond noir) en icônes
individuelles 256 × 256 à fond transparent : Assets/Korvalune/Resources/Icons/<nom sans accents>.png.

  python3 icons/slice.py
"""
import os, unicodedata
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ND

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', '..', 'Assets', 'Korvalune', 'Resources', 'Icons')
SHEETS = [
    ['Épée longue', 'Hache de guerre', "Masse d'armes", 'Bâton', 'Arbalète', 'Faux', 'Écu', 'Pavois'],
    ['Grimoire', 'Orbe', 'Heaume', 'Capuche', 'Couronne', 'Bassinet', 'Cuirasse', 'Brigandine'],
    ['Robe', 'Haubert', 'Gantelets', 'Gants de cuir', 'Mitaines', 'Jambières', 'Cuissots', 'Chausses'],
    ['Solerets', 'Bottes', 'Sandales', 'Amulette', 'Talisman', 'Anneau', 'Chevalière', None],
    ['Carquois', 'Arc long', 'Phylactère', 'Sceptre', "Fléau d'armes", 'Épée bâtarde', 'Dague rituelle', 'Arc de chasse'],
]


def slug(s):
    s = unicodedata.normalize('NFKD', s).encode('ascii', 'ignore').decode()
    return ''.join(c if c.isalnum() else '_' for c in s).strip('_')


def cut(cell):
    a = np.asarray(cell.convert('RGB')).astype(np.float32)
    lum = a.max(2)
    m = lum > 14
    lab, n = ND.label(m)
    if n == 0: return None
    sizes = ND.sum(m, lab, range(1, n + 1))
    keep = np.isin(lab, 1 + np.nonzero(sizes >= max(60, sizes.max() * 0.02))[0])     # retire les poussières
    keep = ND.binary_fill_holes(ND.binary_closing(keep, iterations=3))
    keep = ND.binary_dilation(keep, iterations=2)
    ys, xs = np.nonzero(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    alpha = np.clip(ND.gaussian_filter(keep.astype(np.float32), 1.2) * 1.15, 0, 1)
    alpha = np.maximum(alpha * np.clip(lum / 22, 0.0, 1.0) ** 0.35, alpha * (lum > 40))  # lisière douce sur le fond noir
    rgba = np.dstack([a, alpha * 255]).astype(np.uint8)[y0:y1, x0:x1]
    im = Image.fromarray(rgba, 'RGBA')
    side = int(max(im.size) * 1.08)
    sq = Image.new('RGBA', (side, side), (0, 0, 0, 0)); sq.paste(im, ((side - im.size[0]) // 2, (side - im.size[1]) // 2))
    return sq.resize((256, 256), Image.LANCZOS)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    board = Image.new('RGBA', (8 * 128, 4 * 128), (34, 26, 20, 255))
    k = 0
    for i, names in enumerate(SHEETS):
        sh = Image.open(os.path.join(HERE, 'planches', f'planche{i + 1}.png'))
        W, H = sh.size; cw, ch = W // 4, H // 2
        for j, name in enumerate(names):
            if not name: continue
            ic = cut(sh.crop(((j % 4) * cw, (j // 4) * ch, (j % 4 + 1) * cw, (j // 4 + 1) * ch)))
            ic.save(os.path.join(OUT, slug(name) + '.png'))
            board.alpha_composite(ic.resize((128, 128), Image.LANCZOS), ((k % 8) * 128, (k // 8) * 128)); k += 1
    board.save('/tmp/claude-0/icones_peintes.png'); print(k, 'icônes')
