"""Icônes des objets (inventaire) : une image peinte par type d'objet de base (31), reconnaissable au premier coup d'œil.
Dessin vectoriel suréchantillonné : métal en dégradés et reflets, cuir, bois, tissu, gemmes ; ombre portée et contour
sombre pour la lisibilité sur petit écran. Sortie : Assets/Korvalune/Resources/Icons/<id>.png (256 × 256, fond transparent).

  python3 icons/items.py
"""
import os, math, unicodedata
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 1024                      # dessin à 4× puis réduction (anticrénelage)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Assets', 'Korvalune', 'Resources', 'Icons')

STEEL = ((222, 226, 232), (120, 128, 140), (52, 56, 66))
DARKSTEEL = ((150, 154, 165), (78, 82, 94), (28, 30, 36))
GOLD = ((255, 226, 140), (205, 150, 60), (110, 70, 20))
BRONZE = ((240, 190, 130), (170, 110, 60), (80, 45, 20))
WOOD = ((170, 118, 72), (112, 72, 40), (58, 36, 18))
LEATHER = ((150, 98, 60), (100, 62, 36), (50, 30, 16))
CLOTH = ((120, 60, 150), (80, 36, 110), (36, 14, 54))
RED = ((230, 70, 60), (160, 30, 30), (70, 10, 10))
BLUEGEM = ((180, 230, 255), (60, 140, 230), (20, 50, 120))
GREENGEM = ((180, 255, 200), (40, 190, 110), (10, 80, 40))


def slug(s):
    s = unicodedata.normalize('NFKD', s).encode('ascii', 'ignore').decode()
    return ''.join(c if c.isalnum() else '_' for c in s).strip('_')


def P(pts, s=1.0, dx=0, dy=0):
    return [(x * S * s + dx * S, y * S * s + dy * S) for x, y in pts]


class Painter:
    def __init__(self):
        self.img = Image.new('RGBA', (S, S), (0, 0, 0, 0))

    def poly(self, pts, pal, light=(-0.6, -0.8), outline=6, gloss=True):
        """Polygone rempli d'un dégradé métallique orienté selon la lumière, contour sombre, reflet."""
        mask = Image.new('L', (S, S), 0); ImageDraw.Draw(mask).polygon(pts, fill=255)
        xs = np.array([p[0] for p in pts]); ys = np.array([p[1] for p in pts])
        yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
        cx, cy = xs.mean(), ys.mean(); ext = max(np.ptp(xs), np.ptp(ys), 1)
        t = ((xx - cx) * light[0] + (yy - cy) * light[1]) / ext + 0.5
        t = np.clip(t, 0, 1)
        hi, mid, lo = [np.array(c, np.float32) for c in pal]
        col = np.where(t[..., None] > 0.5, mid + (hi - mid) * ((t[..., None] - 0.5) * 2), lo + (mid - lo) * (t[..., None] * 2))
        if gloss:   # bande de reflet
            band = np.exp(-(((xx - cx) * 0.7 + (yy - cy) * 0.7) / ext * 3.2 + 0.6) ** 2 * 6)
            col = col + band[..., None] * 60
        layer = Image.fromarray(np.clip(np.dstack([col, np.full((S, S), 255, np.float32)]), 0, 255).astype(np.uint8), 'RGBA')
        self.img.paste(layer, (0, 0), mask)
        if outline: ImageDraw.Draw(self.img).line(pts + [pts[0]], fill=(16, 12, 10, 255), width=outline, joint='curve')

    def ellipse(self, box, pal, **kw):
        x0, y0, x1, y1 = [v * S for v in box]
        pts = [((x0 + x1) / 2 + (x1 - x0) / 2 * math.cos(a), (y0 + y1) / 2 + (y1 - y0) / 2 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)]
        self.poly(pts, pal, **kw)

    def line(self, pts, color, w):
        ImageDraw.Draw(self.img).line([(x * S, y * S) for x, y in pts], fill=color, width=int(w * S), joint='curve')

    def gem(self, c, r, pal=BLUEGEM):
        self.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), pal)
        self.ellipse((c[0] - r * 0.45, c[1] - r * 0.55, c[0] - r * 0.05, c[1] - r * 0.15), ((255, 255, 255), (255, 255, 255), (220, 240, 255)), outline=0, gloss=False)

    def done(self, rot=0):
        im = self.img.rotate(rot, resample=Image.BICUBIC, center=(S / 2, S / 2)) if rot else self.img
        a = im.split()[3]
        shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0)); shadow.putalpha(a.filter(ImageFilter.GaussianBlur(18)).point(lambda v: int(v * 0.6)))
        out = Image.new('RGBA', (S, S), (0, 0, 0, 0)); out.alpha_composite(shadow, (14, 18)); out.alpha_composite(im)
        return out.resize((256, 256), Image.LANCZOS)


# ------------------------------------------------------------------------------------------------ armes
def sword(long=True, curved=False):
    p = Painter(); L = 0.62 if long else 0.5
    blade = [(0.47, 0.08), (0.53, 0.08), (0.545, 0.1 + L), (0.455, 0.1 + L)] if not curved else [(0.5, 0.06), (0.6, 0.2), (0.56, 0.7), (0.46, 0.7), (0.5, 0.2)]
    blade = [(0.5, 0.04)] + blade
    p.poly(P(blade), STEEL); p.line([(0.5, 0.1), (0.5, 0.1 + L - 0.02)], (90, 96, 110, 255), 0.008)
    p.poly(P([(0.3, 0.72), (0.7, 0.72), (0.72, 0.76), (0.28, 0.76)]), GOLD)
    p.poly(P([(0.47, 0.76), (0.53, 0.76), (0.53, 0.9), (0.47, 0.9)]), LEATHER)
    p.ellipse((0.455, 0.89, 0.545, 0.96), GOLD)
    return p.done(-40)


def axe():
    p = Painter(); p.poly(P([(0.47, 0.12), (0.53, 0.12), (0.54, 0.95), (0.46, 0.95)]), WOOD)
    p.poly(P([(0.52, 0.14), (0.8, 0.06), (0.88, 0.28), (0.8, 0.46), (0.52, 0.36)]), STEEL)
    p.poly(P([(0.48, 0.16), (0.36, 0.2), (0.36, 0.32), (0.48, 0.34)]), DARKSTEEL)
    for y in (0.6, 0.7, 0.8): p.line([(0.46, y), (0.54, y + 0.02)], (40, 25, 12, 255), 0.012)
    return p.done(-30)


def mace():
    p = Painter(); p.poly(P([(0.475, 0.32), (0.525, 0.32), (0.53, 0.94), (0.47, 0.94)]), WOOD)
    for a in range(8):
        t = a / 8 * 2 * math.pi; c = (0.5 + 0.13 * math.cos(t), 0.2 + 0.13 * math.sin(t))
        p.poly(P([(c[0] + 0.07 * math.cos(t), c[1] + 0.07 * math.sin(t)), (c[0] + 0.035 * math.cos(t + 1.6), c[1] + 0.035 * math.sin(t + 1.6)), (c[0] + 0.035 * math.cos(t - 1.6), c[1] + 0.035 * math.sin(t - 1.6))]), DARKSTEEL)
    p.ellipse((0.36, 0.06, 0.64, 0.34), STEEL)
    return p.done(-35)


def staff():
    p = Painter(); p.poly(P([(0.48, 0.24), (0.52, 0.24), (0.53, 0.96), (0.47, 0.96)]), WOOD)
    p.poly(P([(0.5, 0.04), (0.6, 0.14), (0.56, 0.28), (0.44, 0.28), (0.4, 0.14)]), GOLD)
    p.gem((0.5, 0.16), 0.07, ((240, 200, 255), (160, 80, 240), (60, 20, 110)))
    return p.done(-25)


def crossbow():
    p = Painter(); p.poly(P([(0.46, 0.2), (0.54, 0.2), (0.56, 0.92), (0.44, 0.92)]), WOOD)
    pts = [(0.12 + 0.76 * i / 20, 0.32 - 0.12 * math.sin(math.pi * i / 20)) for i in range(21)]
    p.line(pts, (30, 22, 16, 255), 0.05); p.line(pts, (120, 128, 140, 255), 0.025)
    p.line([(0.12, 0.32), (0.5, 0.42), (0.88, 0.32)], (230, 225, 210, 255), 0.006)
    p.poly(P([(0.48, 0.1), (0.52, 0.1), (0.52, 0.5), (0.48, 0.5)]), STEEL)
    return p.done(-20)


def scythe():
    p = Painter(); p.poly(P([(0.6, 0.08), (0.65, 0.1), (0.42, 0.96), (0.37, 0.94)]), WOOD)
    p.poly(P([(0.62, 0.1), (0.4, 0.06), (0.16, 0.16), (0.1, 0.3), (0.3, 0.2), (0.6, 0.18)]), STEEL)
    return p.done(0)


# ------------------------------------------------------------------------------------------------ main gauche
def kite_shield(pal=STEEL, emblem=RED):
    p = Painter(); pts = [(0.2, 0.12), (0.8, 0.12), (0.8, 0.45), (0.5, 0.92), (0.2, 0.45)]
    p.poly(P(pts), GOLD); p.poly(P(pts, 0.86, 0.07, 0.06), pal)
    p.poly(P([(0.46, 0.2), (0.54, 0.2), (0.54, 0.4), (0.68, 0.4), (0.68, 0.47), (0.54, 0.47), (0.54, 0.72), (0.46, 0.72), (0.46, 0.47), (0.32, 0.47), (0.32, 0.4), (0.46, 0.4)]), emblem)
    return p.done()


def pavise():
    p = Painter(); pts = [(0.24, 0.06), (0.76, 0.06), (0.8, 0.9), (0.2, 0.9)]
    p.poly(P(pts), GOLD); p.poly(P(pts, 0.88, 0.06, 0.055), ((245, 240, 225), (200, 190, 170), (120, 110, 90)))
    for k in range(12):
        t = k / 12 * 2 * math.pi; p.line([(0.5, 0.45), (0.5 + 0.2 * math.cos(t), 0.45 + 0.2 * math.sin(t))], (220, 170, 60, 255), 0.018)
    p.ellipse((0.42, 0.37, 0.58, 0.53), GOLD)
    return p.done()


def grimoire():
    p = Painter(); p.poly(P([(0.22, 0.14), (0.76, 0.12), (0.8, 0.86), (0.24, 0.88)]), ((120, 40, 40), (80, 20, 24), (36, 8, 10)))
    p.poly(P([(0.76, 0.14), (0.82, 0.16), (0.84, 0.86), (0.8, 0.86)]), ((240, 230, 200), (210, 200, 170), (150, 140, 110)))
    for y in (0.22, 0.78): p.poly(P([(0.24, y - 0.02), (0.78, y - 0.03), (0.78, y + 0.02), (0.24, y + 0.03)]), GOLD)
    p.gem((0.51, 0.5), 0.09, RED)
    return p.done(8)


def orb():
    p = Painter(); p.poly(P([(0.38, 0.72), (0.62, 0.72), (0.68, 0.9), (0.32, 0.9)]), GOLD)
    p.gem((0.5, 0.42), 0.28, ((220, 200, 255), (130, 70, 230), (40, 10, 90)))
    for a in range(5): p.line([(0.5 + 0.18 * math.cos(a * 1.3), 0.42 + 0.18 * math.sin(a * 1.3)), (0.5 + 0.06 * math.cos(a * 1.3 + 2), 0.42 + 0.06 * math.sin(a * 1.3 + 2))], (240, 220, 255, 180), 0.008)
    return p.done()


# ------------------------------------------------------------------------------------------------ armures
def helm(kind):
    p = Painter()
    if kind == 'heaume':
        p.poly(P([(0.25, 0.85), (0.22, 0.4), (0.32, 0.18), (0.5, 0.1), (0.68, 0.18), (0.78, 0.4), (0.75, 0.85)]), STEEL)
        p.poly(P([(0.3, 0.44), (0.7, 0.44), (0.7, 0.5), (0.3, 0.5)]), ((30, 30, 34), (20, 20, 24), (8, 8, 10)), gloss=False)
        for x in np.linspace(0.36, 0.64, 6): p.line([(x, 0.56), (x, 0.74)], (20, 20, 24, 255), 0.012)
    elif kind == 'capuche':
        p.poly(P([(0.2, 0.9), (0.24, 0.42), (0.5, 0.08), (0.76, 0.42), (0.8, 0.9)]), ((90, 120, 70), (54, 78, 40), (22, 34, 16)))
        p.ellipse((0.34, 0.38, 0.66, 0.78), ((28, 24, 22), (16, 14, 12), (6, 5, 4)), gloss=False)
    elif kind == 'couronne':
        p.poly(P([(0.18, 0.7), (0.18, 0.4), (0.3, 0.55), (0.4, 0.3), (0.5, 0.52), (0.6, 0.3), (0.7, 0.55), (0.82, 0.4), (0.82, 0.7)]), GOLD)
        for x, c in ((0.33, RED), (0.5, BLUEGEM), (0.67, GREENGEM)): p.gem((x, 0.62), 0.045, c)
    else:   # bassinet
        p.poly(P([(0.24, 0.82), (0.24, 0.42), (0.5, 0.08), (0.76, 0.42), (0.76, 0.82)]), DARKSTEEL)
        p.poly(P([(0.3, 0.46), (0.7, 0.46), (0.5, 0.86)]), STEEL)
        for k in range(4): p.line([(0.4 + k * 0.065, 0.55), (0.4 + k * 0.065, 0.62)], (16, 16, 18, 255), 0.012)
    return p.done()


def chest(kind):
    p = Painter()
    body = [(0.24, 0.16), (0.38, 0.12), (0.5, 0.2), (0.62, 0.12), (0.76, 0.16), (0.86, 0.34), (0.74, 0.4), (0.72, 0.9), (0.28, 0.9), (0.26, 0.4), (0.14, 0.34)]
    pal = {'cuirasse': STEEL, 'brigandine': LEATHER, 'robe': CLOTH, 'haubert': DARKSTEEL}[kind]
    p.poly(P(body), pal)
    if kind == 'cuirasse':
        p.line([(0.5, 0.22), (0.5, 0.86)], (90, 96, 110, 255), 0.01); p.poly(P([(0.28, 0.6), (0.72, 0.6), (0.72, 0.66), (0.28, 0.66)]), GOLD)
    elif kind == 'brigandine':
        for x in np.linspace(0.34, 0.66, 5):
            for y in np.linspace(0.3, 0.82, 7): p.ellipse((x - 0.012, y - 0.012, x + 0.012, y + 0.012), GOLD, outline=2)
    elif kind == 'robe':
        p.poly(P([(0.46, 0.2), (0.54, 0.2), (0.56, 0.9), (0.44, 0.9)]), GOLD)
    else:
        for y in np.linspace(0.24, 0.86, 16):
            p.line([(0.28 + (0.02 if (int(y * 50) % 2) else 0), y), (0.72, y)], (40, 42, 48, 140), 0.006)
    return p.done()


def gloves(kind):
    p = Painter(); pal = {'gantelets': STEEL, 'cuir': LEATHER, 'mitaines': CLOTH}[kind]
    p.poly(P([(0.3, 0.9), (0.3, 0.55), (0.24, 0.42), (0.3, 0.38), (0.36, 0.46), (0.36, 0.2), (0.42, 0.18), (0.44, 0.4), (0.46, 0.14), (0.52, 0.14), (0.53, 0.4),
              (0.56, 0.16), (0.62, 0.18), (0.6, 0.42), (0.66, 0.24), (0.72, 0.26), (0.68, 0.6), (0.66, 0.9)]), pal)
    p.poly(P([(0.28, 0.78), (0.68, 0.78), (0.68, 0.92), (0.28, 0.92)]), GOLD if kind == 'gantelets' else LEATHER)
    return p.done(-8)


def legs(kind):
    p = Painter(); pal = {'jambieres': STEEL, 'cuissots': DARKSTEEL, 'chausses': LEATHER}[kind]
    if kind == 'cuissots':   # protège-cuisses seuls : deux plaques bombées et sangles
        for x in (0.36, 0.64):
            p.poly(P([(x - 0.13, 0.16), (x + 0.13, 0.16), (x + 0.11, 0.66), (x, 0.74), (x - 0.11, 0.66)]), DARKSTEEL)
            for y in (0.3, 0.5): p.poly(P([(x - 0.15, y), (x + 0.15, y), (x + 0.15, y + 0.04), (x - 0.15, y + 0.04)]), LEATHER)
            p.ellipse((x - 0.05, 0.68, x + 0.05, 0.78), GOLD)
        return p.done()
    p.poly(P([(0.26, 0.1), (0.74, 0.1), (0.72, 0.9), (0.56, 0.9), (0.5, 0.36), (0.44, 0.9), (0.28, 0.9)]), pal)
    p.poly(P([(0.26, 0.1), (0.74, 0.1), (0.74, 0.18), (0.26, 0.18)]), LEATHER)
    if kind != 'chausses':
        for x in (0.34, 0.64): p.ellipse((x - 0.06, 0.52, x + 0.06, 0.62), GOLD)
    return p.done()


def boots(kind):
    p = Painter(); pal = {'solerets': STEEL, 'bottes': LEATHER, 'sandales': WOOD}[kind]
    if kind == 'sandales':
        p.poly(P([(0.2, 0.8), (0.84, 0.76), (0.88, 0.86), (0.2, 0.9)]), WOOD)
        for x in (0.32, 0.5, 0.68): p.poly(P([(x - 0.03, 0.8), (x + 0.03, 0.8), (x + 0.07, 0.52), (x + 0.01, 0.52)]), LEATHER)
        p.poly(P([(0.3, 0.2), (0.4, 0.2), (0.42, 0.78), (0.32, 0.78)]), LEATHER)
        p.line([(0.28, 0.32), (0.44, 0.36)], (60, 36, 18, 255), 0.02); p.line([(0.28, 0.5), (0.44, 0.54)], (60, 36, 18, 255), 0.02)
        return p.done(-5); pal = {'solerets': STEEL, 'bottes': LEATHER, 'sandales': WOOD}[kind]
    p.poly(P([(0.32, 0.1), (0.56, 0.1), (0.56, 0.62), (0.84, 0.74), (0.86, 0.88), (0.3, 0.88)]), pal)
    p.poly(P([(0.28, 0.86), (0.88, 0.86), (0.88, 0.92), (0.28, 0.92)]), ((60, 40, 26), (40, 26, 16), (20, 12, 6)))
    if kind == 'solerets':
        for y in (0.3, 0.45, 0.6): p.line([(0.32, y), (0.56, y)], (60, 64, 74, 255), 0.01)
    return p.done(-5)


def amulet(kind):
    p = Painter(); pts = [(0.5 + 0.3 * math.cos(t), 0.1 + 0.42 * math.sin(t)) for t in np.linspace(0.1, math.pi - 0.1, 30)]
    p.line(pts, (200, 160, 70, 255), 0.018)
    if kind == 'amulette':
        p.poly(P([(0.5, 0.42), (0.68, 0.6), (0.5, 0.9), (0.32, 0.6)]), GOLD); p.gem((0.5, 0.64), 0.08, RED)
    else:
        p.ellipse((0.32, 0.44, 0.68, 0.84), BRONZE); p.gem((0.5, 0.64), 0.07, GREENGEM)
    return p.done()


def ring(kind):
    p = Painter()
    p.ellipse((0.22, 0.32, 0.78, 0.86), GOLD); p.ellipse((0.32, 0.44, 0.68, 0.78), ((0, 0, 0), (0, 0, 0), (0, 0, 0)), gloss=False)
    # trou : efface l'intérieur
    hole = Image.new('L', (S, S), 0); ImageDraw.Draw(hole).ellipse([0.34 * S, 0.46 * S, 0.66 * S, 0.76 * S], fill=255)
    p.img.putalpha(ImageChops.subtract(p.img.split()[3], hole))
    if kind == 'anneau': p.gem((0.5, 0.3), 0.1, BLUEGEM)
    else: p.poly(P([(0.38, 0.2), (0.62, 0.2), (0.64, 0.36), (0.36, 0.36)]), GOLD); p.line([(0.44, 0.28), (0.56, 0.28)], (110, 70, 20, 255), 0.012)
    return p.done()


ICONS = {
    'Épée longue': lambda: sword(True), 'Hache de guerre': axe, 'Masse d\'armes': mace, 'Bâton': staff, 'Arbalète': crossbow, 'Faux': scythe,
    'Écu': kite_shield, 'Pavois': pavise, 'Grimoire': grimoire, 'Orbe': orb,
    'Heaume': lambda: helm('heaume'), 'Capuche': lambda: helm('capuche'), 'Couronne': lambda: helm('couronne'), 'Bassinet': lambda: helm('bassinet'),
    'Cuirasse': lambda: chest('cuirasse'), 'Brigandine': lambda: chest('brigandine'), 'Robe': lambda: chest('robe'), 'Haubert': lambda: chest('haubert'),
    'Gantelets': lambda: gloves('gantelets'), 'Gants de cuir': lambda: gloves('cuir'), 'Mitaines': lambda: gloves('mitaines'),
    'Jambières': lambda: legs('jambieres'), 'Cuissots': lambda: legs('cuissots'), 'Chausses': lambda: legs('chausses'),
    'Solerets': lambda: boots('solerets'), 'Bottes': lambda: boots('bottes'), 'Sandales': lambda: boots('sandales'),
    'Amulette': lambda: amulet('amulette'), 'Talisman': lambda: amulet('talisman'), 'Anneau': lambda: ring('anneau'), 'Chevalière': lambda: ring('chevaliere'),
}

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new('RGBA', (8 * 256, 4 * 256), (30, 24, 20, 255))
    for i, (name, fn) in enumerate(ICONS.items()):
        im = fn(); im.save(os.path.join(OUT, slug(name) + '.png'))
        sheet.alpha_composite(im, ((i % 8) * 256, (i // 8) * 256))
        print(name, '->', slug(name))
    sheet.save('/tmp/claude-0/icones.png')
