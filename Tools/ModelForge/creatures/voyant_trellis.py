"""Voyant du Néant — modèle généré gratuitement (TRELLIS) d'après le concept validé : abomination flottante, un œil
géant violet dans une orbite de chair sombre, auréole brisée d'éclats de cristal noir, lambeaux d'étoffe pâle, longs
tentacules d'ombre. La génération l'a rendu plat : il est épaissi (volume), l'œil et les veines violettes sont
lumineux ; squelette flottant (corps, auréole, trois faisceaux de tentacules) et animations de lévitation."""
import numpy as np, os
from forge.rig import Asset, Skeleton, qaxis
from forge.geo import Mesh
from forge.anim import key, build_qclip
from imported import glb_creature as GC

NAME = 'VoyantNeant'
FOLDER = 'Enemies'
SCALE = 1.35
HEAD_VIEW = (0.0, 1.30, 0.10)
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'voyant', 'modele_s7.glb')
DEPTH = 2.4                                         # épaississement (le modèle généré n'a que 25 cm d'épaisseur)
_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC)[3]
    violet = lambda c: (np.clip((c[..., 2] - 0.35) * 3, 0, 1) * (c[..., 2] > c[..., 1] * 1.4))[..., None] * c
    return GC.texture(_cache['base'], S, gain=1.9, gamma=1.0, sat=1.15, relief=5.0, smooth=0.3, emit=violet)


RECIPES = {'VN_Corps': (_tex, 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.4, 2.4, 2.4)))}
GEAR = []
CH = {'L': 0.20, 'C': 0.0, 'R': -0.20}               # +x = gauche de la créature
TY = (0.92, 0.66, 0.42, 0.20, 0.0)


def skeleton():
    sk = Skeleton()
    sk.add('Hips', None, (0, 1.0, 0)); sk.add('Body', 'Hips', (0, 1.12, 0)); sk.add('Crown', 'Body', (0, 1.45, 0))
    for c, x in CH.items():
        par = 'Hips'
        for k in range(4):
            n = f'Tent{c}{k + 1}'; sk.add(n, par, (x * (1 + 0.15 * k), TY[k], 0)); par = n
    return sk


def segments(sk):
    P = lambda n: np.asarray(sk.pos[sk[n]], float)
    segs = [('Body', P('Hips'), P('Body')), ('Body', P('Body'), P('Crown')), ('Crown', P('Crown'), P('Crown') + (0, 0.4, 0))]
    for c, x in CH.items():
        for k in range(4):
            a = P(f'Tent{c}{k + 1}'); b = np.array([x * (1 + 0.15 * (k + 1)), TY[k + 1], 0])
            segs.append((f'Tent{c}{k + 1}', a, b))
    return segs


def _sway(t, amp, ph):
    return {f'Tent{c}{k + 1}': qaxis((0, 0, 1) if c != 'C' else (1, 0, 0), np.radians(amp * (1 + 0.5 * k) * np.sin(t + ph + k * 0.8 + i)))
            for i, c in enumerate(CH) for k in range(4)}


def clips(sk):
    bones = [n for n in sk.names if n != 'Hips']
    out = []
    ks = [key(i * 0.5, _sway(i * 0.5 * 2.1, 6, 0), hips=(0, 0.06 * np.sin(i * 0.5 * 2.1), 0)) for i in range(7)]
    out.append(build_qclip('Idle', ks, bones, loop=True))
    for name, T, lean in (('Walk', 1.2, 8), ('Run', 0.7, 16)):
        ks = []
        for i in range(9):
            t = i * T / 8; w = 2 * np.pi * t / T; p = _sway(w, 10, 0); p['Body'] = qaxis((1, 0, 0), np.radians(3 * np.sin(w)))
            ks.append(key(t, p, hips=(0, 0.05 * np.sin(2 * w), 0), hips_rot=(lean, 0, 4 * np.sin(w))))
        out.append(build_qclip(name, ks, bones, loop=True))
    lunge = dict(_sway(0, 18, 1.0)); lunge['Crown'] = qaxis((1, 0, 0), np.radians(-15))
    ks = [key(0, {}), key(0.3, {'Crown': qaxis((1, 0, 0), np.radians(12)), **_sway(1.5, 14, 0)}, hips=(0, 0.10, -0.15), hips_rot=(-12, 0, 0)),
          key(0.5, lunge, hips=(0, 0.0, 0.45), hips_rot=(18, 0, 0)), key(1.0, {})]
    out.append(build_qclip('Attack', ks, bones))
    out.append(build_qclip('Hit', [key(0, {}), key(0.12, _sway(2, 12, 0), hips=(0, 0.05, -0.12), hips_rot=(-10, 8, 0)), key(0.5, {})], bones))
    dead = {f'Tent{c}{k + 1}': qaxis((1, 0, 0), np.radians(25)) for c in CH for k in range(4)}
    out.append(build_qclip('Death', [key(0, {}), key(0.5, _sway(3, 20, 0), hips=(0, 0.2, 0), hips_rot=(-20, 0, 15)),
                                     key(1.3, dead, hips=(0, -0.85, 0.2), hips_rot=(80, 0, 30))], bones))
    return out


def build(bank, female=False):
    V, UV, F, base = GC.load(SRC, GC.BASE_H)
    _cache['base'] = base
    F, _ = GC.drop_islands(V, F, 0.004)
    used = np.unique(F); rm = -np.ones(len(V), int); rm[used] = np.arange(len(used)); V, UV, F = V[used], UV[used], rm[F]
    zc = 0.5 * (V[:, 2].min() + V[:, 2].max()); V[:, 2] = (V[:, 2] - zc) * DEPTH
    sk = skeleton(); segs = segments(sk)
    names = sorted({b for b, _, _ in segs}, key=sk.names.index)
    D = np.full((len(V), len(names)), 9.0)
    for b, a_, b_ in segs:
        ab = b_ - a_; t = np.clip(((V - a_) @ ab) / (ab @ ab + 1e-12), 0, 1); j = names.index(b)
        D[:, j] = np.minimum(D[:, j], np.linalg.norm((V - (a_ + t[:, None] * ab)) * (1, 1, 0.3), axis=1))
    Wt = 1.0 / np.maximum(D, 0.015) ** 4; Wt /= Wt.sum(1, keepdims=True)
    inv, Pm = GC._adjacency(V, F); n = inv.max() + 1
    Wv = np.zeros((n, len(names))); np.add.at(Wv, inv, Wt); Wv /= np.bincount(inv, minlength=n)[:, None]
    for _ in range(8): Wv = Pm @ Wv
    Wt = Wv[inv]; idx = np.argsort(-Wt, 1)[:, :4]; w = np.take_along_axis(Wt, idx, 1); w /= w.sum(1, keepdims=True)
    lut = np.array([sk.names.index(nm) for nm in names])
    m = Mesh(V, F, UV.copy(), lut[idx], w); m.UV[:, 1] = 1 - m.UV[:, 1]
    a = Asset(NAME, sk); a.add('corps', m, 'VN_Corps')
    for c in clips(sk): a.clips[c.name] = c
    print(f'   {NAME} : {len(V)} sommets, {len(F)} triangles', flush=True)
    return a, {}, a.clips
