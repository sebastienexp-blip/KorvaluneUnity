"""Wendigo des glaces — modèle généré gratuitement (TRELLIS) d'après le concept validé : crâne de cerf fendu, grands
bois, cage thoracique à nu, fourrure grise en lambeaux, longs bras griffus. Nettoyé (morceau de décor voisin retiré),
retexturé, yeux lumineux ajoutés dans les orbites, squelette et animations du jeu."""
import numpy as np, os
from imported import glb_creature as GC
from knight import anims
from heroes import female_kit as FK
from enemies import materials as EM

NAME = 'WendigoGivre'
FOLDER = 'Enemies'
SCALE = 2.9 / 1.83
HEAD_VIEW = (0, 1.40, 0.12)
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'wendigo', 'modele_s7.glb')
CX, CZ = -0.27, -0.16

LANDMARKS = {
    'Hips': (CX, 0.82, CZ), 'Chest': (CX, 1.18, -0.10), 'Neck': (CX, 1.31, 0.0), 'Head': (CX, 1.40, 0.12), 'HeadTop': (CX, 1.52, 0.15),
    'R_Shoulder': (-0.51, 1.25, -0.05), 'R_Elbow': (-0.55, 0.92, 0.0), 'R_Wrist': (-0.47, 0.60, 0.29), 'R_HandTip': (-0.44, 0.33, 0.41),
    'L_Shoulder': (-0.03, 1.25, -0.05), 'L_Elbow': (0.02, 0.92, 0.0), 'L_Wrist': (-0.06, 0.60, 0.29), 'L_HandTip': (-0.10, 0.33, 0.41),
    'R_Hip': (-0.38, 0.80, -0.18), 'R_Knee': (-0.40, 0.48, -0.12), 'R_Ankle': (-0.43, 0.13, -0.21), 'R_Toe': (-0.46, 0.03, 0.10),
    'L_Hip': (-0.16, 0.80, -0.18), 'L_Knee': (-0.13, 0.48, -0.12), 'L_Ankle': (-0.09, 0.13, -0.20), 'L_Toe': (-0.08, 0.03, 0.10),
}
RIGID = [(lambda V, C: V[:, 1] > 1.47, 'Head')]          # bois : rigides avec la tête
_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC)[3]
    return GC.texture(_cache['base'], S, gain=1.9, gamma=1.0, sat=1.05, relief=5.5, smooth=0.16)


RECIPES = {
    'WT_Fourrure': (_tex, 2048, 1024, dict(metallic=0.0, smoothness=1.0)),
    'WT_Lueur': (lambda S: EM.glow(S, (0.55, 0.85, 1.0)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.4, 2.4, 2.4))),
}
GEAR = []


def build(bank, female=False):
    a, base, gear = GC.build(NAME, SRC, LANDMARKS, RIGID, mat='WT_Fourrure', keep=lambda V, C: V[:, 0] < 0.13, center=(CX, CZ))
    _cache['base'] = base
    from heroes.female_kit import _blob
    sk = a.skeleton; hb = sk['Head']
    BV = a.parts[0][1].V
    for k, e in enumerate(EYES):
        e = np.asarray(e, float) - np.array([CX, 0, CZ])
        near = BV[np.linalg.norm(BV[:, :2] - e[:2], axis=1) < 0.012]
        if len(near): e[2] = near[:, 2].max() - 0.002          # posé à fleur de l'orbite (surface avant du crâne)
        g = _blob(e, (0.014, 0.011, 0.008), n=10)
        g.J = np.full((len(g.V), 4), hb); g.W = np.tile([1.0, 0, 0, 0], (len(g.V), 1)); a.add(f'oeil{k}', g, 'WT_Lueur')
    anims.build(sk, a)
    return a, {}, anims.CLIPS


EYES = [(-0.318, 1.452, 0.0), (-0.214, 1.448, 0.0)]
