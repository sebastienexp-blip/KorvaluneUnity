"""Salamandre-forge — modèle généré gratuitement (TRELLIS) d'après le concept validé : grande salamandre basse
cuirassée de plaques de fer martelé et rivetées, rangée de cheminées sur le dos, fissures de lave incandescentes,
gueule large et plate. Nettoyé (pan sombre et cage voisins retirés), lave et braises lumineuses, squelette à
pattes suivies automatiquement sur le maillage et animations."""
import numpy as np, os
from creatures.basilic_trellis import multileg
from imported import glb_creature as GC

NAME = 'SalamandreForge'
FOLDER = 'Enemies'
SCALE = 1.25
HEAD_VIEW = (0.0, 0.55, 1.4)
YAW = -64.3
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'salamandre', 'modele_s7.glb')


def KEEP(V, C):
    # dans le repère tourné : pan sombre et cage orange voisins au-dessus de l'avant du corps
    return ~((V[:, 1] > 1.5) | ((V[:, 1] > 1.26) & (V[:, 2] > 0.62)) | ((V[:, 1] > 1.02) & (V[:, 2] > 1.0)) | ((V[:, 1] > 0.68) & (V[:, 2] > 1.35)))


BODY = {'Head': (0.0, 0.55, 1.55), 'Thorax': (0.0, 0.70, 0.80), 'Hips': (0.0, 0.78, 0.10), 'Abdomen': (0.0, 0.74, -0.50),
        'Spinneret': (0.0, 0.58, -1.00), 'TailTip': (0.0, 0.52, -1.68)}
FEET = {'L': [(0.15, 0.82), (0.36, 0.08), (0.03, -0.45), None],
        'R': [(-0.38, 0.85), None, None, (-0.43, -0.52)]}
_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC, GC.BASE_H, YAW)[3]
    lava = lambda c: (np.clip((c[..., 0] - 0.45) * 3, 0, 1) * (c[..., 0] > c[..., 2] * 2.2))[..., None] * c * 1.4
    return GC.texture(_cache['base'], S, gain=1.6, gamma=1.0, sat=1.05, relief=7.0, smooth=0.25, emit=lava,
                      metal_mask=lambda c: np.clip(0.55 - (c @ np.array([0.3, 0.59, 0.11])) * 2.5, 0, 0.55))


RECIPES = {'SF_Corps': (_tex, 2048, 1024, dict(metallic=1.0, smoothness=1.0, emission=(4.5, 4.5, 4.5), emission_preview=(2.6, 2.6, 2.6)))}
GEAR = []


def build(bank, female=False):
    return multileg(NAME, SRC, KEEP, BODY, FEET, (0.0, 0.0, 0.0), 'SF_Corps', _cache, yaw=YAW, body_r=0.42, body_bias=0.55, anim_scale=0.7)
