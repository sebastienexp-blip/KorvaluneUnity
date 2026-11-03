"""Fauve de magma — grand prédateur des Terres de Cendrefer : anatomie de grand félin (poitrine profonde, omoplates
saillantes, pattes massives, longue queue), peau de roche volcanique noire fendue de lave incandescente, crête de
pointes d'obsidienne le long de l'échine et de la queue, griffes et crocs d'obsidienne, yeux de braise. Rig et
animations des quadrupèdes (Idle, Walk, Run, Attack, Howl, Hit, Death)."""
import numpy as np
from scipy.spatial import cKDTree
from forge.geo import Loft, spline, curve, unit, concat, rigid, vertex_normals
from creatures import felin as FE
from enemies import materials as EM
from knight import materials as KM

NAME = 'FauveMagma'
FOLDER = 'Enemies'
SCALE = 1.25
MAX_TRIS = 160000
HEAD_VIEW = (0, 1.06, 0.95)
RECIPES = {
    'FM_Peau':   (lambda S: EM.lava_rock(S, seed=1601, tone=(0.06, 0.05, 0.045), lava=(1.0, 0.40, 0.06), cells=40, glow_amt=1.1), 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(2.6, 2.6, 2.6), emission_preview=(1.4, 1.4, 1.4))),
    'FM_Oeil':   (lambda S: EM.glow(S, (1.0, 0.55, 0.08)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.5, 2.5, 2.5))),
    'FM_Obsid':  (lambda S: KM.steel(S, seed=1602, tone=(0.04, 0.035, 0.045), polish=0.9, rust_amt=0.0, grime_amt=0.3, scratch_amt=0.4), 512, 256, dict(metallic=0.2, smoothness=1.0)),
}
GEAR = []
REMAP = {'FE_Panthere': 'FM_Peau', 'FE_OeilV': 'FM_Oeil', 'FE_TruffeN': 'FM_Obsid', 'FE_Griffe': 'FM_Obsid'}
SADDLE = ('tapis', 'selle', 'arcon', 'sangle', 'etriviere', 'etrier')


def build(bank, female=False):
    a = FE.build('panthere', bank)
    a.name = NAME
    a.parts = [(n, m, REMAP.get(mat, mat)) for n, m, mat in a.parts if not n.startswith(SADDLE)]
    body = next(m for n, m, mat in a.parts if n == 'corps')
    tree = cKDTree(body.V); N = vertex_normals(body)
    sk = a.skeleton
    # crête d'obsidienne : pointes recourbées vers l'arrière, plus hautes au garrot, décroissantes sur la queue
    spikes = []
    for bone, (zs, ze), n, h in (('Neck1', (0.70, 0.52), 3, 0.08), ('Chest', (0.48, 0.20), 4, 0.13), ('Spine2', (0.18, -0.10), 3, 0.11),
                                 ('Spine1', (-0.12, -0.40), 3, 0.10), ('Hips', (-0.42, -0.70), 3, 0.08), ('Tail1', (-0.80, -1.00), 2, 0.06), ('Tail2', (-1.05, -1.35), 3, 0.045)):
        for z in np.linspace(zs, ze, n):
            cand = body.V[np.abs(body.V[:, 2] - z) < 0.03]
            cand = cand[np.abs(cand[:, 0]) < 0.03]
            if len(cand) == 0: continue
            p = cand[np.argmax(cand[:, 1])]; i = tree.query(p)[1]; nn = N[i]
            d = unit(nn * 1.0 + np.array([0, 0.2, -0.6]))
            m = Loft(spline([p - nn * 0.01, p + d * h * 0.6 + (0, 0, -0.01), p + d * h + (0, -0.01, -0.03)], 8),
                     curve([(0, h * 0.18), (1, 0.002)], 8), curve([(0, h * 0.10), (1, 0.002)], 8), p=1.6, segs=6, cap1=1, ref=(1, 0, 0)).mesh(0.05)
            rigid(m, sk[bone]); spikes.append(m)
    a.add('crete', concat(spikes), 'FM_Obsid')
    return a, {}, a.clips
