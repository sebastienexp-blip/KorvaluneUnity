"""Basilic écarlate — modèle généré gratuitement (TRELLIS) d'après le concept validé : lézard à pattes multiples,
écailles de grès rouge, collerette d'épines, queue relevée en arc terminée par une masse épineuse. Nettoyé (pan de
décor voisin et sol retirés), retexturé, yeux d'ambre lumineux ; squelette d'araignée géante adapté (quatre paires de
pattes suivies automatiquement le long du maillage, depuis chaque pied jusqu'au corps), animations du jeu."""
import numpy as np, os
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra
from forge.rig import Asset, Skeleton
from forge.geo import Mesh
from imported import glb_creature as GC
from creatures import araignee as AR
from enemies import materials as EM

NAME = 'BasilicEcarlate'
FOLDER = 'Enemies'
SCALE = 1.45
HEAD_VIEW = (0.0, 0.80, 0.85)
SRC = os.path.join(os.path.dirname(__file__), '..', '..', 'Meshy', 'out', 'basilic', 'modele_s7.glb')
CX, CZ = 0.20, 0.10


def KEEP(V, C):
    lum = C @ np.array([0.3, 0.59, 0.11]); red = C[:, 0] > C[:, 2] * 1.25
    other = (V[:, 0] < -0.17) & (V[:, 1] < 0.48) & (V[:, 2] > 0.0) & (lum < 0.16)          # petite bête voisine
    sheet = ((V[:, 0] > 0.53) & ((lum > 0.2) | ~red)) | (V[:, 0] > 0.62)                     # pan blanc voisin
    ground = ((V[:, 1] < 0.045) & ((V[:, 2] < -0.6) | (V[:, 0] < -0.35))) | (V[:, 1] < 0.028)   # traînée et plaques au sol
    blade = ~red & (lum < 0.12) & (V[:, 1] > 1.0) & (V[:, 2] > 0.2) & (V[:, 2] < 0.8)                  # lame noire voisine
    return ~(other | sheet | ground | blade)


# axe du corps (repères dans le modèle) : tête, poitrail, bassin, croupe, queue
BODY = {'Head': (0.31, 0.80, 0.90), 'Thorax': (0.24, 0.68, 0.45), 'Hips': (0.20, 0.64, 0.05), 'Abdomen': (0.16, 0.66, -0.35),
        'Spinneret': (0.10, 0.85, -0.75), 'TailTip': (0.05, 1.55, -1.15)}
# pieds (x, z au sol), de l'avant vers l'arrière, par côté (L = gauche du basilic, x grand)
FEET = {'L': [(0.48, 0.62), (0.50, 0.10), (0.47, -0.20), (0.21, -0.45)],
        'R': [(0.16, 0.78), (0.02, 0.33), (-0.07, -0.13), (-0.26, -0.26)]}
EYES = []                                  # les yeux d'ambre sont déjà dans la texture : rendus lumineux
_cache = {}


def _tex(S):
    if 'base' not in _cache: _cache['base'] = GC.load(SRC)[3]
    amber = lambda c: (np.clip((c[..., 0] - 0.55) * 4, 0, 1) * np.clip((c[..., 1] - 0.25) * 4, 0, 1) * (c[..., 2] < 0.3))[..., None] * c
    return GC.texture(_cache['base'], S, gain=1.7, gamma=1.0, sat=1.1, relief=6.0, smooth=0.2, emit=amber)


RECIPES = {
    'BT_Ecailles': (_tex, 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(3.0, 3.0, 3.0), emission_preview=(2.0, 2.0, 2.0))),
    'BT_Oeil': (lambda S: EM.glow(S, (1.0, 0.62, 0.12)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.4, 2.4, 2.4))),
}
GEAR = []


def _graph(V, F):
    key = np.round(V / 1e-4).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.ravel()
    n = inv.max() + 1; P = np.zeros((n, 3)); P[inv] = V
    Fw = inv[F]; e = np.r_[Fw[:, [0, 1]], Fw[:, [1, 2]], Fw[:, [2, 0]]]
    w = np.linalg.norm(P[e[:, 0]] - P[e[:, 1]], axis=1) + 1e-6
    G = coo_matrix((np.r_[w, w], (np.r_[e[:, 0], e[:, 1]], np.r_[e[:, 1], e[:, 0]])), shape=(n, n)).tocsr()
    return inv, P, G


def leg_chain(P, G, foot, body_pts, body_r=0.17):
    """Chemin le plus court sur la surface, du pied jusqu'au corps → 6 points (attache … pied) le long de la patte."""
    if len(foot) == 2:
        fx, fz = foot
        cand = np.nonzero((P[:, 1] < 0.12) & (np.hypot(P[:, 0] - fx, P[:, 2] - fz) < 0.10))[0]
    else:
        fx, fy, fz = foot
        cand = np.nonzero((np.abs(P[:, 1] - fy) < 0.06) & (np.hypot(P[:, 0] - fx, P[:, 2] - fz) < 0.08))[0]
    start = cand[np.argmin(P[cand, 1])]
    dist, pred = dijkstra(G, indices=start, return_predecessors=True)
    # cible : sommet du corps le plus proche (au sens du chemin) sur l'axe du corps
    ax = np.asarray(body_pts)
    def dbody(Q):
        best = np.full(len(Q), 9.0)
        for a, b in zip(ax[:-1], ax[1:]):
            ab = b - a; t = np.clip(((Q - a) @ ab) / (ab @ ab), 0, 1); best = np.minimum(best, np.linalg.norm(Q - (a + t[:, None] * ab), axis=1))
        return best
    inside = np.nonzero(dbody(P) < body_r)[0]
    tgt = inside[np.argmin(dist[inside])]
    path = [tgt]
    while path[-1] != start and pred[path[-1]] >= 0: path.append(pred[path[-1]])
    pts = P[path]                                            # du corps vers le pied
    # lissage + recentrage vers l'intérieur de la patte (le chemin longe la surface)
    k = 5; sm = np.array([pts[max(0, i - k): i + k + 1].mean(0) for i in range(len(pts))])
    L = np.r_[0, np.cumsum(np.linalg.norm(np.diff(sm, axis=0), axis=1))]
    out = [np.array([np.interp(t, L, sm[:, j]) for j in range(3)]) for t in L[-1] * np.array([0.0, 0.12, 0.42, 0.70, 0.90, 1.0])]
    out[-1][1] = max(out[-1][1], 0.02)
    return out


def build(bank, female=False):
    return multileg(NAME, SRC, KEEP, BODY, FEET, (CX, 0.0, CZ), 'BT_Ecailles', _cache, EYES, 'BT_Oeil')


def multileg(NAME, SRC, KEEP, BODY, FEET, center, MAT, _cache, EYES=(), EYE_MAT=None, anim_scale=1.0, yaw=0.0, body_r=0.17, body_bias=1.0):
    """Créature à quatre paires de membres (squelette de l'araignée géante) d'après un GLB généré."""
    V, UV, F, base = GC.load(SRC, GC.BASE_H, yaw)
    _cache['base'] = base
    C = GC.vertex_colors(UV, base)
    F = F[KEEP(V, C)[F].all(1)]
    F, _ = GC.drop_islands(V, F, 0.004)
    used = np.unique(F); rm = -np.ones(len(V), int); rm[used] = np.arange(len(used)); V, UV, F = V[used], UV[used], rm[F]
    inv, P, G = _graph(V, F)
    axis_pts = [BODY[k] for k in ('Head', 'Thorax', 'Hips', 'Abdomen', 'Spinneret')]
    sk = Skeleton()
    sk.add('Hips', None, BODY['Hips']); sk.add('Thorax', 'Hips', BODY['Thorax']); sk.add('Abdomen', 'Hips', BODY['Abdomen'])
    sk.add('Spinneret', 'Abdomen', BODY['Spinneret'])
    h = np.asarray(BODY['Head'])
    segs = [('Thorax', BODY['Thorax'], BODY['Head']), ('Hips', BODY['Hips'], BODY['Thorax']), ('Abdomen', BODY['Abdomen'], BODY['Hips']),
            ('Abdomen', BODY['Abdomen'], BODY['Spinneret']), ('Spinneret', BODY['Spinneret'], BODY['TailTip'])]
    angs = {}
    for s, sx in (('L', 1), ('R', -1)):
        sk.add(f'Chelicera_{s}', 'Thorax', h + (0.03 * sx, -0.08, 0.02)); sk.add(f'Fang_{s}', f'Chelicera_{s}', h + (0.03 * sx, -0.10, 0.06))
        sk.add(f'Palp1_{s}', 'Thorax', h + (0.05 * sx, -0.10, -0.02)); sk.add(f'Palp2_{s}', f'Palp1_{s}', h + (0.07 * sx, -0.10, 0.0))
        for i, foot in enumerate(FEET[s]):
            par = 'Thorax' if i < 2 else 'Abdomen'
            if foot is None:                     # membre absent : chaîne d'os sans influence, cachée dans le corps
                b0 = np.asarray(BODY[par], float)
                for k, seg in enumerate(AR.SEGS):
                    n = f'Leg{i}_{seg}_{s}'; sk.add(n, par, b0 + (0.02 * sx * (k + 1), -0.01 * k, 0)); par = n
                angs[(i, s)] = 90.0; continue
            pts = leg_chain(P, G, foot, axis_pts, body_r)
            for k, seg in enumerate(AR.SEGS):
                n = f'Leg{i}_{seg}_{s}'; sk.add(n, par, pts[k]); par = n
                segs.append((n, pts[k], pts[k + 1]))
            d = pts[-1] - pts[0]; angs[(i, s)] = np.degrees(np.arctan2(abs(d[0]), d[2]))
    sk._rg = None
    # poids : distance aux segments des os, lissés sur le maillage
    names = sorted({b for b, _, _ in segs}, key=sk.names.index)
    D = np.full((len(V), len(names)), 9.0)
    for b, a_, b_ in segs:
        a_, b_ = np.asarray(a_, float), np.asarray(b_, float); ab = b_ - a_
        t = np.clip(((V - a_) @ ab) / (ab @ ab + 1e-12), 0, 1); j = names.index(b)
        D[:, j] = np.minimum(D[:, j], np.linalg.norm(V - (a_ + t[:, None] * ab), axis=1))
    body = [j for j, nm in enumerate(names) if not nm.startswith('Leg')]
    D[:, body] *= body_bias                     # < 1 : le corps garde ses sommets (pattes courtes sous un gros corps)
    Wt = 1.0 / np.maximum(D, 0.012) ** 4; Wt /= Wt.sum(1, keepdims=True)
    inv2, Pm = GC._adjacency(V, F); n = inv2.max() + 1
    Wv = np.zeros((n, len(names))); np.add.at(Wv, inv2, Wt); Wv /= np.bincount(inv2, minlength=n)[:, None]
    for _ in range(6): Wv = Pm @ Wv
    Wt = Wv[inv2]; idx = np.argsort(-Wt, 1)[:, :4]; w = np.take_along_axis(Wt, idx, 1); w /= w.sum(1, keepdims=True)
    lut = np.array([sk.names.index(nm) for nm in names])
    c = np.asarray(center, float)
    m = Mesh(V - c, F, UV.copy(), lut[idx], w); m.UV[:, 1] = 1 - m.UV[:, 1]
    sk.pos = [p - c for p in sk.pos]; sk._rg = None
    a = Asset(NAME, sk); a.add('corps', m, MAT)
    from heroes.female_kit import _blob
    BV = m.V
    for k, e in enumerate(EYES):
        e = np.asarray(e, float) - c
        near = BV[np.linalg.norm(BV[:, [0, 1]] - e[[0, 1]], axis=1) < 0.015]
        if len(near): e[2] = near[:, 2].max() - 0.004
        g = _blob(e, (0.016, 0.011, 0.010), n=10); g.J = np.full((len(g.V), 4), sk['Thorax']); g.W = np.tile([1.0, 0, 0, 0], (len(g.V), 1))
        a.add(f'oeil{k}', g, EYE_MAT)
    # animations : pattes qui pivotent à la hanche avec une foulée à leur mesure (voir legged_anims)
    from creatures import legged_anims as LA
    LA.build(sk, a, anim_scale)
    print(f'   {NAME} : {len(V)} sommets, {len(F)} triangles', flush=True)
    return a, {}, a.clips
