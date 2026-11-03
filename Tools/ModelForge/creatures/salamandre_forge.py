"""Salamandre-forge — colosse rampant des Terres de Cendrefer (d'après la planche de concepts) : énorme salamandre
basse et trapue (2,8 m du museau à la queue), pattes courtes écartées à cinq doigts griffus, large tête plate au
museau arrondi, gueule rougeoyante ; dos, flancs, cou et queue cuirassés de plaques d'obsidienne martelée rivetées
comme une forge ; rangée de cheminées de fer qui sortent de l'échine et crachent feu et fumée ; ventre de lave en
fusion, fissures incandescentes entre les plaques. Rig quadrupède (os du loup) et animations communes."""
import numpy as np
from scipy.spatial import cKDTree
from forge.rig import Asset
from forge import sculpt as SC
from forge.geo import Loft, spline, curve, unit, concat, rigid, vertex_normals
from forge.texgen import TexSet, fbm, n01, height_to_normal, cavity_ao
from creatures import felin as FE, quadruped as QA
from creatures import cheval as CH
from heroes.female_kit import _blob
from enemies import materials as EM
from knight import materials as KM

NAME = 'SalamandreForge'
FOLDER = 'Enemies'
SCALE = 1.15
MAX_TRIS = 170000
HEAD_VIEW = (0, 0.70, 1.05)
J = {
    'Hips': (0, 0.55, -0.55), 'Spine1': (0, 0.58, -0.25), 'Spine2': (0, 0.60, 0.05), 'Chest': (0, 0.60, 0.35),
    'Neck1': (0, 0.60, 0.62), 'Neck2': (0, 0.61, 0.78), 'Head': (0, 0.62, 0.92), 'Jaw': (0, 0.52, 0.98),
    'Tail1': (0, 0.52, -0.85), 'Tail2': (0, 0.44, -1.25), 'Tail3': (0, 0.36, -1.65), 'Tail4': (0, 0.28, -2.00),
    'Ear': (0.12, 0.72, 0.90),
    'Scapula': (0.20, 0.62, 0.38), 'UpperArm': (0.34, 0.50, 0.40), 'LowerArm': (0.48, 0.32, 0.43), 'Wrist': (0.50, 0.09, 0.47), 'FrontPaw': (0.52, 0.03, 0.57),
    'Thigh': (0.20, 0.56, -0.55), 'Shin': (0.36, 0.46, -0.50), 'Hock': (0.48, 0.26, -0.58), 'BackPaw': (0.50, 0.03, -0.50),
}
TIP = (0, 0.22, -2.38)


def hammered(S=1024, seed=1901):
    """Obsidienne martelée : noir vitreux, cupules de martelage, arêtes usées plus claires, reflets de braise."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    dents = np.zeros((S, S))
    for _ in range(260):
        cx, cy = r.uniform(0, 1, 2); rad = r.uniform(0.02, 0.05)
        d = np.sqrt(np.minimum(np.abs(xx - cx), 1 - np.abs(xx - cx)) ** 2 + np.minimum(np.abs(yy - cy), 1 - np.abs(yy - cy)) ** 2)
        dents = np.maximum(dents, np.clip(1 - d / rad, 0, 1) ** 2)
    g = n01(fbm(S, S, r, base=S / 10, octaves=5))
    base = np.array([0.045, 0.040, 0.042])[None, None] * (0.8 + 0.4 * g[..., None]) + np.array([0.10, 0.08, 0.07])[None, None] * (1 - dents)[..., None] * 0.3
    hgt = -1.0 * dents + 0.3 * g
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 1.0), np.full((S, S), 0.35), np.clip(0.75 - 0.25 * dents, 0, 1), np.clip(cavity_ao(hgt, 4, 0.1), 0, 1))


RECIPES = {
    'SF_Peau':     (lambda S: EM.lava_rock(S, seed=1902, tone=(0.08, 0.06, 0.055), lava=(1.0, 0.38, 0.06), cells=36, glow_amt=0.6), 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(1.8, 1.8, 1.8), emission_preview=(1.0, 1.0, 1.0))),
    'SF_Ventre':   (lambda S: EM.lava_rock(S, seed=1903, tone=(0.20, 0.07, 0.03), lava=(1.0, 0.55, 0.10), cells=20, glow_amt=1.8), 1024, 512, dict(metallic=0.0, smoothness=1.0, emission=(3.5, 3.5, 3.5), emission_preview=(2.0, 2.0, 2.0))),
    'SF_Plaque':   (lambda S: hammered(S, seed=1904), 1024, 512, dict(metallic=0.6, smoothness=1.0)),
    'SF_Fer':      (lambda S: KM.steel(S, seed=1905, tone=(0.16, 0.13, 0.11), polish=0.3, rust_amt=2.4, grime_amt=2.2), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'SF_Feu':      (lambda S: EM.glow(S, (1.0, 0.45, 0.06)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(6.0, 6.0, 6.0), emission_preview=(3.5, 3.5, 3.5))),
    'SF_Oeil':     (lambda S: EM.glow(S, (1.0, 0.75, 0.15)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.5, 2.5, 2.5))),
    'SF_Griffe':   (lambda S: KM.steel(S, seed=1906, tone=(0.03, 0.03, 0.035), polish=0.9, rust_amt=0.0, grime_amt=0.2), 256, 128, dict(metallic=0.2, smoothness=1.0)),
}
GEAR = []


def skeleton():
    old = FE.J, FE.TIP
    FE.J = J; FE.TIP = {'Tail': TIP}
    try: sk = FE.skeleton()
    finally: FE.J, FE.TIP = old
    return sk


def Pt(n, side=1): p = np.array(J[n], float); p[0] *= side; return p


def body(sk):
    s = SC.Sculpt(k=0.035)
    s.ell('Chest', (0, 0.58, 0.36), (0.30, 0.19, 0.32))
    s.ell('Spine2', (0, 0.57, 0.05), (0.34, 0.18, 0.40))
    s.ell('Spine1', (0, 0.57, -0.27), (0.29, 0.18, 0.30))
    s.ell('Hips', (0, 0.54, -0.58), (0.26, 0.17, 0.26))
    s.cone('Neck1', (0, 0.58, 0.55), (0, 0.60, 0.82), 0.22, 0.19)
    # tête large et plate, museau arrondi, mâchoire massive, bourrelets au-dessus des yeux
    s.ell('Head', (0, 0.63, 0.95), (0.21, 0.10, 0.20))
    s.ell('Head', (0, 0.60, 1.16), (0.16, 0.07, 0.20))
    s.ell('Head', (0, 0.585, 1.32), (0.12, 0.055, 0.10))
    for sx in (1, -1): s.ell('Head', (0.11 * sx, 0.70, 0.96), (0.055, 0.04, 0.06), k=0.015)
    s.ell('Jaw', (0, 0.52, 1.10), (0.18, 0.055, 0.28))
    for side, sx in (('L', 1), ('R', -1)):
        # pattes courtes, épaisses, écartées (posture de salamandre)
        s.cone(f'UpperArm_{side}', Pt('Scapula', sx), Pt('UpperArm', sx), 0.15, 0.12)
        s.cone(f'LowerArm_{side}', Pt('UpperArm', sx), Pt('LowerArm', sx), 0.12, 0.10)
        s.cone(f'Wrist_{side}', Pt('LowerArm', sx), Pt('Wrist', sx), 0.10, 0.08)
        s.ell(f'FrontPaw_{side}', Pt('FrontPaw', sx) + (0, 0.02, 0), (0.12, 0.05, 0.12))
        s.cone(f'Thigh_{side}', Pt('Thigh', sx), Pt('Shin', sx), 0.16, 0.12)
        s.cone(f'Shin_{side}', Pt('Shin', sx), Pt('Hock', sx), 0.12, 0.10)
        s.cone(f'Hock_{side}', Pt('Hock', sx), Pt('BackPaw', sx), 0.10, 0.08)
        s.ell(f'BackPaw_{side}', Pt('BackPaw', sx) + (0, 0.02, 0), (0.12, 0.05, 0.13))
    for a_, b_, ra, rb in (('Hips', 'Tail1', 0.20, 0.17), ('Tail1', 'Tail2', 0.17, 0.13), ('Tail2', 'Tail3', 0.13, 0.09), ('Tail3', 'Tail4', 0.09, 0.06)):
        s.cone(a_ if a_ != 'Hips' else 'Tail1', Pt(a_), Pt(b_), ra, rb, squash=(0.75, (0, 1, 0)), k=0.03)
    s.cone('Tail4', Pt('Tail4'), np.array(TIP), 0.06, 0.02, squash=(0.7, (0, 1, 0)), k=0.02)
    detail = lambda X: 0.006 * SC.noise3(X, 0.06, seed=91, octaves=3)
    m = s.mesh(res=0.016, detail=detail, smooth=4)
    s.skin(m, sk, kw=0.04)
    SC.box_uv(m, 0.45)
    return m


def top_point(tree, V, x, z):
    near = V[(np.abs(V[:, 0] - x) < 0.035) & (np.abs(V[:, 2] - z) < 0.035)]
    if len(near) == 0: return None
    return near[np.argmax(near[:, 1])]


def nearest_bone(z):
    order = [('Tail3', -1.45), ('Tail2', -1.05), ('Tail1', -0.75), ('Hips', -0.45), ('Spine1', -0.15), ('Spine2', 0.20), ('Chest', 0.50), ('Neck1', 0.72), ('Head', 9)]
    for b, lim in order:
        if z < lim: return b
    return 'Head'


def build(bank, female=False):
    sk = skeleton(); a = Asset(NAME, sk); rng = np.random.default_rng(1900)
    m = body(sk)
    N = vertex_normals(m); C = m.V[m.F].mean(1); Nf = N[m.F].mean(1)
    belly = (Nf[:, 1] < -0.35) & (C[:, 1] < 0.55)
    from forge.humanparts import submesh
    a.add('corps', submesh(m, ~belly), 'SF_Peau'); a.add('ventre', submesh(m, belly), 'SF_Ventre')
    tree = cKDTree(m.V)
    # plaques d'obsidienne martelée : elles couvrent dos, flancs, cou, haut des pattes et queue en se chevauchant
    # (semis régulier sur la surface, une plaque bombée orientée selon la peau, rivets aux coins)
    from creatures import common as CC
    plates, rivets = [], []
    P, Nn = CC.sample_surface(m, 2600, rng, weight=lambda C: ((C[:, 1] > 0.28) & ~((C[:, 2] > 0.88) & (C[:, 1] < 0.62))) * 1.0)
    keep = []
    for idx in np.argsort(-P[:, 1]):
        p = P[idx]
        if Nn[idx][1] < -0.25: continue
        if any(np.sum((p - q) ** 2) < 0.095 ** 2 for q in keep[-400:]): continue
        keep.append(p)
        n = Nn[idx]; z = p[2]
        sz = 0.075 if z > -0.8 else max(0.035, 0.075 * (1 + (z + 0.8) / 2.2))
        if np.abs(p[0]) > 0.30 and p[1] < 0.45: sz *= 0.75                       # pattes : plaques plus petites
        pl = SC.Sculpt(k=0.004).ell('Spine2', (0, 0, 0), (sz * 1.15, sz * 1.05, 0.05)).mesh(res=0.008, smooth=1)
        pl.V[:, 2] = np.clip(pl.V[:, 2], -0.012, 0.020); pl.V[:, 0] = np.clip(pl.V[:, 0], -sz, sz); pl.V[:, 1] = np.clip(pl.V[:, 1], -sz * 0.9, sz * 0.9)
        R = SC.rot_from(unit(n), up=(0, 0, 1) if abs(n[2]) < 0.9 else (0, 1, 0))
        pl.V = pl.V @ R.T + p + n * 0.006
        SC.box_uv(pl, 0.2)
        from forge.humanparts import weights_from
        pl = weights_from(m, pl); plates.append(pl)
        for cx, cy in ((-0.72, -0.72), (0.72, -0.72), (-0.72, 0.72), (0.72, 0.72)):
            q = p + n * 0.028 + R @ np.array([cx * sz, cy * sz * 0.9, 0])
            rv = weights_from(m, _blob(q, (0.008, 0.008, 0.008), n=6)); rivets.append(rv)
    a.add('plaques', concat(plates), 'SF_Plaque'); a.add('rivets', concat(rivets), 'SF_Fer')
    # cheminées de fer le long de l'échine, inclinées vers l'arrière, gueule incandescente
    chim, fire = [], []
    for k, z in enumerate((0.48, 0.34, 0.20, 0.06, -0.08, -0.22, -0.36, -0.50, -0.64)):
        p = top_point(tree, m.V, 0.0, z)
        if p is None: continue
        h = 0.22 + 0.16 * np.sin(np.pi * k / 8) + 0.06 * np.sin(k * 2.3); r0 = 0.040 + 0.008 * (k % 2)
        top = p + np.array([0.02 * (-1) ** k, h, -0.05])
        tube = Loft(spline([p - (0, 0.03, 0), (p + top) / 2, top], 8), curve([(0, r0 * 1.15), (1, r0)], 8), curve([(0, r0 * 1.15), (1, r0)], 8), p=2.2, segs=14).mesh(0.1)
        rim = Loft(np.array([[top[0] + (r0 + 0.012) * np.cos(u), top[1], top[2] + (r0 + 0.012) * np.sin(u)] for u in np.linspace(0, 2 * np.pi, 20)]), 0.012, 0.008, p=4, segs=6, ref=(0, 1, 0)).mesh(0.05)
        band = Loft(np.array([[p[0] + (r0 * 1.2 + 0.008) * np.cos(u), p[1] + h * 0.35, p[2] - 0.018 + (r0 * 1.2 + 0.008) * np.sin(u)] for u in np.linspace(0, 2 * np.pi, 20)]), 0.010, 0.010, p=4, segs=6, ref=(0, 1, 0)).mesh(0.05)
        b = nearest_bone(z)
        for mm in (tube, rim, band): rigid(mm, sk[b]); chim.append(mm)
        f = _blob(top - (0, 0.012, 0), (r0 * 0.85, 0.012, r0 * 0.85), n=12); rigid(f, sk[b]); fire.append(f)
        fl = Loft(spline([top, top + (0.01, 0.07, -0.02), top + (0.0, 0.16, -0.05)], 8), curve([(0, r0 * 0.8), (.4, r0 * 0.7), (1, 0.002)], 8), curve([(0, r0 * 0.8), (.4, r0 * 0.6), (1, 0.002)], 8), p=2, segs=10, cap1=1).mesh(0.05)
        rigid(fl, sk[b]); fire.append(fl)                                          # flamme qui sort de la cheminée
    a.add('cheminees', concat(chim), 'SF_Fer'); a.add('braises', concat(fire), 'SF_Feu')
    # yeux de braise, gueule incandescente, griffes d'obsidienne
    eyes = concat([_blob(np.array((0.13 * sx, 0.69, 1.02)), (0.022, 0.016, 0.020), n=12) for sx in (1, -1)]); rigid(eyes, sk['Head']); a.add('yeux', eyes, 'SF_Oeil')
    mouth = SC.Sculpt(k=0.01).ell('Head', (0, 0.555, 1.15), (0.15, 0.012, 0.26)).mesh(res=0.008, smooth=2); rigid(mouth, sk['Head']); a.add('gueule', mouth, 'SF_Feu')
    claws = []
    for side, sx in (('L', 1), ('R', -1)):
        for paw in ('FrontPaw', 'BackPaw'):
            c0 = Pt(paw, sx)
            for k, dx in enumerate((-0.08, -0.04, 0.0, 0.04, 0.08)):
                b = c0 + np.array([dx * sx + 0.02 * sx, -0.01, 0.10 - 0.02 * abs(k - 2)])
                cl = Loft(spline([b, b + (0.01 * dx * 10, 0, 0.04), b + (0.015 * dx * 10, -0.03, 0.065)], 8), curve([(0, .016), (1, .002)], 8), curve([(0, .014), (1, .0015)], 8), p=2, segs=8, cap1=1).mesh(0.03)
                rigid(cl, sk[f'{paw}_{side}']); claws.append(cl)
    a.add('griffes', concat(claws), 'SF_Griffe')
    QA.build(sk, a, kind='wolf')
    return a, {}, a.clips
