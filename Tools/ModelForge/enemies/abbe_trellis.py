"""Abbé noyé — modèle généré gratuitement (TRELLIS) à partir du concept validé, puis squelette du jeu, poids de peau,
remise en pose T et animations de porteur de crosse. La crosse et la pince font partie du maillage : elles suivent la
main droite et la main gauche (pince) de façon rigide."""
import numpy as np, os
from imported import glb_creature as GC
from knight import anims
from heroes import female_kit as FK

NAME = 'AbbeNoye'
FOLDER = 'Enemies'
SCALE = 2.15 / 1.55            # le modèle mesure 1,55 m sans la crosse une fois ramené à 1,83 m crosse comprise
HEAD_VIEW = (0, 1.30, 0.04)
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'abbe', 'modele_s7.glb')

LANDMARKS = {
    'Hips': (0.0, 0.70, 0.0), 'Chest': (0.0, 1.06, 0.0), 'Neck': (0.0, 1.17, 0.01), 'Head': (-0.01, 1.27, 0.03), 'HeadTop': (-0.01, 1.40, 0.03),
    'R_Shoulder': (-0.17, 1.12, -0.01), 'R_Elbow': (-0.27, 0.95, 0.0), 'R_Wrist': (-0.23, 1.13, 0.12), 'R_HandTip': (-0.22, 1.24, 0.17),
    'L_Shoulder': (0.17, 1.12, -0.01), 'L_Elbow': (0.23, 0.89, 0.0), 'L_Wrist': (0.24, 0.73, 0.08), 'L_HandTip': (0.20, 0.42, 0.30),
    'R_Hip': (-0.09, 0.66, 0.0), 'R_Knee': (-0.10, 0.36, 0.03), 'R_Ankle': (-0.10, 0.07, 0.0), 'R_Toe': (-0.10, 0.02, 0.13),
    'L_Hip': (0.09, 0.66, 0.0), 'L_Knee': (0.10, 0.36, 0.03), 'L_Ankle': (0.10, 0.07, 0.0), 'L_Toe': (0.10, 0.02, 0.13),
}


def _red(V, C):        # carapace de la pince (rouge) sous l'épaule gauche
    return (C[:, 0] > C[:, 1] * 1.5) & (C[:, 0] > 0.18) & (V[:, 0] > 0.10) & (V[:, 1] < 0.86)


_shaft = GC.capsule((-0.30, -0.02, -0.04), (-0.24, 1.56, 0.13), 0.058)


def _crosse(V, C):     # hampe (sauf la main qui la serre), volute et lanterne au-dessus de la mitre
    hand = np.linalg.norm(V - np.array([-0.22, 1.19, 0.15]), axis=1) < 0.075
    top = (V[:, 1] > 1.53) & ~((np.abs(V[:, 0]) < 0.12) & (V[:, 1] < 1.56))
    return (_shaft(V, C) & ~hand) | top


RIGID = [(_red, 'LeftHand')]

_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC)[3]
    lum = lambda c: c @ np.array([0.3, 0.59, 0.11])
    emit = lambda c: np.clip((lum(c) - 0.62) * 4, 0, 1)[..., None] * c * 1.0
    return GC.texture(_cache['base'], S, gain=2.3, gamma=0.95, sat=1.15, relief=5.0, smooth=0.17, emit=emit)


from enemies import abbe_noye as AN
RECIPES = {'AT_Robe': (_tex, 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(2.5, 2.5, 2.5), emission_preview=(1.6, 1.6, 1.6)))}
for k in ('AN_Hollow', 'AN_Eye'): RECIPES[k] = AN.RECIPES[k]
from knight import materials as KM
from enemies import materials as EM
RECIPES['AN_Bone'] = (lambda S: EM.tinted(KM.bone(S, seed=1710, tone=(0.34, 0.33, 0.25)), (0.85, 0.92, 0.78)), 1024, 512, dict(metallic=0.0, smoothness=0.55))

# tête refaite à la main (le visage généré est illisible) : crâne de noyé décharné, orbites creuses, lueurs vertes,
# posés à la place du visage généré, sous la mitre conservée
HEAD_AT = np.array([-0.008, 1.300, 0.025]); HEAD_K = 0.74


def _face(V, C):        # visage et barbe générés (retirés), mitre et épaules gardées
    e = ((V - np.array([0.004, 1.28, 0.0])) / np.array([0.085, 0.10, 0.11]))
    beard = (np.abs(V[:, 0]) < 0.085) & (V[:, 1] > 1.06) & (V[:, 1] < 1.24) & (V[:, 2] > 0.06)
    return ((e ** 2).sum(1) < 1) & (V[:, 1] < 1.37) & (V[:, 1] > 1.21) & (V[:, 2] > -0.08)


def head_parts(bank):
    from forge import humanoid as H
    from forge.rig import Asset
    from forge.geo import rigid
    from enemies import bones as BN
    from enemies.common import template_body
    from heroes.female_kit import _blob
    sk0 = H.build(); a0 = Asset('tmp', sk0); rng = np.random.default_rng(1700)
    body, marks = template_body(sk0, a0, bank)
    s, hm = BN.skull(body, sk0, marks)
    out = [('crane', s, 'AN_Bone'), ('crane_creux', hm, 'AN_Hollow')]
    for e in (np.asarray(marks['eye_L']), np.asarray(marks['eye_R'])):
        out.append((f'lueur{e[0]:.2f}', _blob(e + np.array([0, 0, -0.010]), (0.011, 0.009, 0.007), n=10), 'AN_Eye'))
    h0 = np.asarray(sk0.pos[sk0['Head']])
    return [(n, m, mat) for n, m, mat in out], h0
GEAR = [('Socket_Weapon_R', 'CrosseAbbe')]


def build(bank, female=False):
    a, base, gear = GC.build(NAME, SRC, LANDMARKS, RIGID, mat='AT_Robe', cut=_face, weapon=(_crosse, 'CrosseAbbe', ((-0.30, -0.02, -0.04), (-0.24, 1.56, 0.13), (-0.225, 1.19, 0.15))),
                             keep=lambda V, C: ~((V[:, 0] > 0.29) & (V[:, 1] < 0.6) & (V[:, 2] < -0.15)))
    _cache['base'] = base
    parts, h0 = head_parts(bank)
    sk = a.skeleton; hb = sk['Head']
    body = a.parts[0][1]
    # ajustement sur la mitre générée : crâne centré sous son bandeau, sommet engagé dedans, largeur du bandeau
    skull = next(m for n, m, _ in parts if n == 'crane')
    lo, hi = skull.V.min(0), skull.V.max(0)
    k = 0.128 / (hi[0] - lo[0])
    top_at, cx, front_at = 1.405, 0.004, 0.072
    off = np.array([cx - (lo[0] + hi[0]) / 2 * k, top_at - hi[1] * k, front_at - hi[2] * k])
    for n, m, mat in parts: m.V = m.V * k + off
    for n, m, mat in parts:
        m.J = np.full((len(m.V), 4), hb); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
        a.add(n, m, mat)
    anims.build(a.skeleton, a, FK.StaffStyle())
    return a, gear, anims.CLIPS
