"""Couches de détail (« double couche ») des personnages et des ennemis : une par matière, répétée finement par-dessus
les textures principales. Albédo neutre autour de 128 (multiplication ×2 dans le shader), normale de micro-relief.
  Peau   : pores, ridules croisées, grain
  Metal  : rayures fines dans tous les sens, piqûres
  Tissu  : trame et chaîne des fils, bouloches
  Cuir   : grain perlé (cellules), plis
  Os     : stries de croissance, piqûres, fêlures
  Pierre : grain minéral, cristaux, éclats
  Chitine: micro-écailles, soies
Tout est raccordable (la texture se répète sans couture).

  python3 world/detail_layers.py <dossier de sortie>"""
import os, sys
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from forge.texgen import fbm, n01, height_to_normal, voronoi_edges


def _scratches(S, r, n, length, width=1.0):
    m = np.zeros((S, S))
    for _ in range(n):
        x0, y0 = r.uniform(0, S, 2); a = r.uniform(0, np.pi); L = length * r.uniform(0.3, 1.0); d = r.uniform(0.3, 1.0)
        t = np.linspace(0, L, int(L) + 2)
        xs = np.mod(np.round(x0 + np.cos(a) * t).astype(int), S); ys = np.mod(np.round(y0 + np.sin(a) * t).astype(int), S)
        np.maximum.at(m, (ys, xs), d)
    return gaussian_filter(m, 0.4 * width, mode='wrap')


def _dots(S, r, n, rad):
    m = np.zeros((S, S)); xs = r.integers(0, S, n); ys = r.integers(0, S, n)
    m[ys, xs] = r.uniform(0.5, 1.0, n)
    return gaussian_filter(m, rad, mode='wrap') * (rad * rad * 6)


def peau(S, r):
    pores = np.clip(_dots(S, r, S * S // 70, 0.9), 0, 1)
    yy, xx = np.mgrid[0:S, 0:S] / S
    w1 = np.abs(np.sin((xx * 0.7 + yy) * np.pi * 2 * 40 + 3 * fbm(S, S, r, base=S / 8, octaves=3)))
    w2 = np.abs(np.sin((xx - yy * 0.6) * np.pi * 2 * 34 + 3 * fbm(S, S, r, base=S / 8, octaves=3)))
    wr = np.clip(1 - np.minimum(w1, w2) * 8, 0, 1) * 0.5
    g = n01(fbm(S, S, r, base=S / 24, octaves=4))
    h = 0.4 * g - 1.0 * pores - 0.5 * wr
    return 0.5 + 0.07 * (g - 0.5) - 0.10 * pores - 0.05 * wr, h, 0.9


def metal(S, r):
    sc = np.clip(_scratches(S, r, 900, S / 5) + 0.6 * _scratches(S, r, 2500, S / 18), 0, 1)
    pits = np.clip(_dots(S, r, S * S // 400, 1.0), 0, 1)
    g = n01(fbm(S, S, r, base=S / 16, octaves=4))
    return 0.5 + 0.10 * sc - 0.10 * pits + 0.04 * (g - 0.5), -0.8 * sc - 0.8 * pits + 0.2 * g, 0.5


def tissu(S, r):
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    n = 96
    warp = 0.5 + 0.5 * np.sin(xx / S * np.pi * 2 * n); weft = 0.5 + 0.5 * np.sin(yy / S * np.pi * 2 * n)
    over = (np.sin(xx / S * np.pi * n) * np.sin(yy / S * np.pi * n) > 0)
    h = np.where(over, warp, weft) * 0.8 + 0.2 * n01(fbm(S, S, r, base=S / 30, octaves=3))
    fuzz = np.clip(_dots(S, r, S * S // 900, 1.5), 0, 1)
    return 0.5 + 0.09 * (h - 0.5) + 0.05 * fuzz, h + 0.4 * fuzz, 1.0


def cuir(S, r):
    cells = n01(gaussian_filter(voronoi_edges(S, S, r, 900), 0.8, mode='wrap'))
    creases = np.clip(1 - np.abs(fbm(S, S, r, base=S / 6, octaves=4)) * 14, 0, 1) ** 2
    g = n01(fbm(S, S, r, base=S / 20, octaves=4))
    return 0.5 - 0.08 * cells - 0.08 * creases + 0.05 * (g - 0.5), -0.9 * cells - 0.8 * creases + 0.2 * g, 0.9


def os_(S, r):
    yy = np.mgrid[0:S, 0:S][0] / S
    lines = 0.5 + 0.5 * np.sin((yy * 70 + 2 * fbm(S, S, r, base=S / 6, octaves=3)) * np.pi * 2)
    pits = np.clip(_dots(S, r, S * S // 300, 1.2), 0, 1)
    cracks = np.clip(1 - np.abs(fbm(S, S, r, base=S / 5, octaves=5)) * 20, 0, 1) ** 3
    return 0.5 + 0.05 * (lines - 0.5) - 0.10 * pits - 0.12 * cracks, 0.3 * lines - 0.8 * pits - 1.0 * cracks, 0.8


def pierre(S, r):
    g = n01(fbm(S, S, r, base=S / 10, octaves=6)); grain = n01(r.random((S, S)))
    grain = gaussian_filter(grain, 0.7, mode='wrap')
    chips = np.clip(1 - np.abs(fbm(S, S, r, base=S / 7, octaves=4)) * 16, 0, 1) ** 3
    return 0.5 + 0.08 * (g - 0.5) + 0.08 * (grain - 0.5) - 0.10 * chips, 0.6 * g + 0.4 * grain - 0.9 * chips, 1.0


def chitine(S, r):
    sc = n01(gaussian_filter(voronoi_edges(S, S, r, 1800), 0.6, mode='wrap'))
    hairs = np.clip(_scratches(S, r, 1500, S / 40, 0.7), 0, 1)
    return 0.5 - 0.07 * sc - 0.08 * hairs, -0.7 * sc + 0.5 * hairs, 0.7


KINDS = {'Peau': peau, 'Metal': metal, 'Tissu': tissu, 'Cuir': cuir, 'Os': os_, 'Pierre': pierre, 'Chitine': chitine}


def save(out, name, alb, h, strength, S):
    a = np.clip(alb, 0, 1)
    rgb = np.dstack([a, a, a])
    Image.fromarray((rgb * 255 + 0.5).astype(np.uint8)).save(os.path.join(out, f'Detail_{name}_Albedo.png'), optimize=True)
    hn = (h - h.min()) / (np.ptp(h) + 1e-6)
    nrm = height_to_normal(hn * 2.0, strength)
    Image.fromarray((np.clip(nrm, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(out, f'Detail_{name}_Normal.png'), optimize=True)


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else 'out/detail'
    os.makedirs(out, exist_ok=True); S = 512
    for k, (name, fn) in enumerate(KINDS.items()):
        alb, h, st = fn(S, np.random.default_rng(3000 + k))
        save(out, name, alb, h, st, S); print(name, flush=True)
