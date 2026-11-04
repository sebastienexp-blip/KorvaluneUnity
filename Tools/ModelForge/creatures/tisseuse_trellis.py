"""Tisseuse des brumes — modèle généré gratuitement (TRELLIS) d'après le concept validé : horreur des marais
perchée sur quatre longues pattes-échasses, deux paires de bras-faux, cages rouillées où brillent des feux follets
verts. Nettoyé (planche et pans de tissu voisins retirés), retexturé (feux follets lumineux), squelette à quatre
paires de membres (suivis automatiquement sur le maillage) et animations."""
import numpy as np, os
from creatures.basilic_trellis import multileg
from imported import glb_creature as GC

NAME = 'TisseuseBrumes'
FOLDER = 'Enemies'
SCALE = 1.45
HEAD_VIEW = (0.0, 1.05, 0.10)
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'tisseuse', 'modele_s7.glb')
DROP = 0.27                                                   # hauteur de la planche retirée : les pieds reviennent au sol


def KEEP(V, C):
    plank = V[:, 1] < 0.26                                  # planche de bois générée sous les pattes
    banners = np.abs(V[:, 0]) > 0.52                        # pans de tissu des créatures voisines
    return ~(plank | banners)


BODY = {'Head': (0.0, 1.32, 0.10), 'Thorax': (0.0, 1.12, 0.04), 'Hips': (0.0, 0.98, 0.0), 'Abdomen': (0.0, 0.80, -0.04),
        'Spinneret': (0.0, 1.45, 0.0), 'TailTip': (0.0, 1.82, 0.0)}
FEET = {'L': [(0.48, 0.50, 0.21), (0.30, 0.30, 0.21), (0.30, 0.30, -0.21), (0.48, 0.50, -0.21)],
        'R': [(-0.48, 0.50, 0.21), (-0.30, 0.30, 0.21), (-0.30, 0.30, -0.21), (-0.48, 0.50, -0.21)]}
_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC)[3]
    wisp = lambda c: (np.clip((c[..., 1] - 0.35) * 3, 0, 1) * (c[..., 1] > c[..., 0] * 1.3) * (c[..., 1] > c[..., 2] * 1.3))[..., None] * c
    return GC.texture(_cache['base'], S, gain=2.8, gamma=0.95, sat=1.15, relief=6.0, smooth=0.18, emit=wisp)


from enemies import abbe_trellis as AT, materials as EM
from knight import materials as KM
RECIPES = {'TB_Corps': (_tex, 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.4, 2.4, 2.4))),
           'AN_Bone': (lambda S: EM.tinted(KM.bone(S, seed=2210, tone=(0.46, 0.45, 0.36)), (0.9, 0.95, 0.85)), 1024, 512, dict(metallic=0.0, smoothness=0.5)),
           'AN_Hollow': AT.RECIPES['AN_Hollow'],
           'AN_Eye': (lambda S: EM.glow(S, (0.45, 1.0, 0.35)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.4, 2.4, 2.4)))}
# visage : crâne décharné sous la cage avant, orbites où luit la même lueur verte que les feux follets
FACE = dict(width=0.13, cx=0.0, top=1.29, front=0.43)
GEAR = []


def build(bank, female=False):
    a, gear, clips = multileg(NAME, SRC, KEEP, BODY, FEET, (0.0, DROP, 0.0), 'TB_Corps', _cache)
    parts, _ = AT.head_parts(bank)
    skull = next(m for n, m, _ in parts if n == 'crane'); lo, hi = skull.V.min(0), skull.V.max(0)
    k = FACE['width'] / (hi[0] - lo[0])
    off = np.array([FACE['cx'] - (lo[0] + hi[0]) / 2 * k, FACE['top'] - DROP - hi[1] * k, FACE['front'] - hi[2] * k])
    tb = a.skeleton['Thorax']
    for n, m, mat in parts:
        m.V = m.V * k + off; m.J = np.full((len(m.V), 4), tb); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1)); a.add(n, m, mat)
    return a, gear, clips
