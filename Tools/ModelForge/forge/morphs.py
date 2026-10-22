"""Curseurs de morphologie du créateur de personnage : cibles officielles MakeHuman (CC0) regroupées en curseurs
bipolaires (−/+), exprimées dans l'espace de nos personnages, puis transférées à TOUTES les pièces (corps, cheveux, sourcils,
cils, dents, armures, vêtements) sous forme de blendshapes du même nom. Le squelette ne change pas : les cibles qui
allongent les membres ou déplacent la tête sont écartées (elles désaligneraient la peau et les os).
"""
import os, re, json, collections
import numpy as np
from scipy.spatial import cKDTree
from . import mhbase as MB

PAIRS = [('decr', 'incr'), ('down', 'up'), ('in', 'out'), ('backward', 'forward'), ('compress', 'uncompress'), ('less', 'more'),
         ('min', 'max'), ('concave', 'convex'), ('narrow', 'wide'), ('small', 'big'), ('close', 'far')]
NEG = {a for a, b in PAIRS}; POS = {b for a, b in PAIRS}
CATS = ['head', 'forehead', 'eyebrows', 'eyes', 'nose', 'mouth', 'chin', 'cheek', 'ears', 'neck', 'torso', 'hip', 'buttocks', 'stomach', 'pelvis',
        'armslegs', 'measure', 'bodyshapes']
# cibles exclues : changent la longueur des segments du squelette ou déplacent des parties entières
EXCLUDE = [r'scale-vert$', r'-height$', r'-length$', r'-trans$', r'lowerlegs-height', r'upperlegs-height', r'napetowaist', r'waisttohip', r'neck-height',
           r'^head-age$', r'foot-scale$', r'hand-scale$']
FR = {'head': 'Tête', 'forehead': 'Front', 'eyebrows': 'Sourcils', 'eyes': 'Yeux', 'nose': 'Nez', 'mouth': 'Bouche', 'chin': 'Menton', 'cheek': 'Joues',
      'ears': 'Oreilles', 'neck': 'Cou', 'torso': 'Torse', 'hip': 'Hanches', 'buttocks': 'Fessier', 'stomach': 'Ventre', 'pelvis': 'Bassin',
      'armslegs': 'Membres', 'measure': 'Mensurations', 'bodyshapes': 'Silhouette', 'macro': 'Corps', 'hands': 'Mains'}
FACE = {'head', 'forehead', 'eyebrows', 'eyes', 'nose', 'mouth', 'chin', 'cheek', 'ears'}


def _tdir():
    for d in MB.TDIRS[::-1]:
        if os.path.isdir(os.path.join(d, 'eyes')): return d
    raise FileNotFoundError('cibles MakeHuman : extraire makehuman/data/targets (git archive) dans /tmp/mh_targets_tmp')


def sliders(female, extra_dirs=()):
    """Liste des curseurs : dict(id, group, label, plus=[(cible, poids)], minus=[(cible, poids)])."""
    s = 'female' if female else 'male'
    out = []
    def mac(nm): return f'macrodetails/universal-{s}-young-{nm}'
    out.append(dict(id='corps_corpulence', group='macro', label='Corpulence',
                    plus=[(mac('averagemuscle-maxweight'), 1), (mac('averagemuscle-averageweight'), -1)],
                    minus=[(mac('averagemuscle-minweight'), 1), (mac('averagemuscle-averageweight'), -1)]))
    out.append(dict(id='corps_musculature', group='macro', label='Musculature',
                    plus=[(mac('maxmuscle-averageweight'), 1), (mac('averagemuscle-averageweight'), -1)],
                    minus=[(mac('minmuscle-averageweight'), 1), (mac('averagemuscle-averageweight'), -1)]))
    if female:
        b = 'breast/female-young-averagemuscle-averageweight-'
        out.append(dict(id='corps_poitrine', group='macro', label='Poitrine', plus=[(b + 'maxcup-averagefirmness', 1)], minus=[(b + 'mincup-averagefirmness', 1)]))
        out.append(dict(id='corps_fermete', group='macro', label='Fermeté de la poitrine', plus=[(b + 'averagecup-maxfirmness', 1)], minus=[(b + 'averagecup-minfirmness', 1)]))
    T = _tdir()
    groups = collections.OrderedDict()
    for cat in CATS:
        folder = os.path.join(T, cat)
        if not os.path.isdir(folder): continue
        for f in sorted(os.listdir(folder)):
            if not f.endswith('.target'): continue
            n = f[:-7]; side = ''
            m = re.match(r'^(r|l)-(.*)$', n)
            if m: n = m.group(2)
            if cat == 'bodyshapes' and (('-fem-' in n) != female): continue
            toks = n.split('-'); last = toks[-1]
            if last in NEG or last in POS: key = '-'.join(toks[:-1]); d = 'plus' if last in POS else 'minus'
            else: key = n; d = 'plus'
            if any(re.search(p, key) for p in EXCLUDE): continue
            # corps : un jeu raisonnable de curseurs (les cibles de mensurations et de silhouettes se recouvrent largement)
            if cat == 'armslegs' and not re.search(r'(fat|muscle|fingers|valgus|foot-scale)', key): continue
            if cat == 'measure' and not re.search(r'(bust|underbust|waist|hips|neck)-circ', key): continue
            if cat == 'bodyshapes': continue
            g = groups.setdefault((cat, key), dict(plus=[], minus=[]))
            g[d].append((f'{cat}/{f[:-7]}', 1))
    for (cat, key), g in groups.items():
        lab = key.replace(cat + '-', '').replace('elvs-fem-', '').replace('elvs-man-', '').replace('-', ' ')
        out.append(dict(id=f'{cat}_{key}'.replace('-', '_'), group=cat, label=f'{FR.get(cat, cat)} : {lab}', plus=g['plus'], minus=g['minus']))
    for d in extra_dirs:   # packs de cibles (ex. hands01) : un curseur unipolaire par fichier
        for f in sorted(os.listdir(d)):
            if f.endswith('.target'):
                MB.TDIRS.append(d) if d not in MB.TDIRS else None
                out.append(dict(id='mains_' + f[:-7].replace('-', '_'), group='hands', label='Mains : ' + f[:-7].split('_', 1)[-1].replace('_', ' '),
                                plus=[(f[:-7], 1)], minus=[]))
    return out


def shape_deltas(marks, sl):
    """{nom de blendshape : déplacement (n_base, 3)} pour tous les curseurs (« id+ » et « id- »)."""
    out = {}
    for s in sl:
        for sign, lst in (('+', s['plus']), ('-', s['minus'])):
            if not lst: continue
            try:
                d = MB.target_delta(marks, [t for t, _ in lst], [w for _, w in lst])
            except FileNotFoundError:
                continue
            if np.abs(d).max() > 1e-6: out[s['id'] + ('_plus' if sign == '+' else '_moins')] = d
    return out


def apply(asset, body, base_deltas, k=6, rigid_parts=('yeux',)):
    """Transfère les déplacements du maillage de base à toutes les pièces : interpolation inverse-distance des k sommets de
    peau les plus proches (exacte pour la peau elle-même, « enveloppe » pour cheveux, armures, vêtements). Les globes
    oculaires se déplacent d'un bloc. Résultat : mesh.shapes = {nom: (n, 3)}."""
    src = body.src
    tree = cKDTree(body.V)
    names = list(base_deltas)
    D = np.stack([base_deltas[n][src] for n in names], 0) if names else np.zeros((0, len(body.V), 3))   # (S, nbody, 3)
    for pn, m, mat in asset.parts:
        dist, idx = tree.query(m.V, k=k)
        w = 1.0 / (dist + 2e-4) ** 2; w /= w.sum(1, keepdims=True)
        sh = {}
        for si, n in enumerate(names):
            dd = (D[si][idx] * w[..., None]).sum(1)
            if pn in rigid_parts:   # translation rigide par œil (côté gauche / droit)
                for side in (m.V[:, 0] > 0, m.V[:, 0] <= 0):
                    if side.any(): dd[side] = dd[side].mean(0)
            if np.abs(dd).max() > 2e-5: sh[n] = dd
        m.shapes = sh


def manifest(sl, female):
    return [dict(id=s['id'], group=s['group'], label=s['label'], face=s['group'] in FACE, bipolar=bool(s['minus'])) for s in sl]
