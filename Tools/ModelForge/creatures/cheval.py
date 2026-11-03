"""Cheval de guerre — anatomie dédiée (et non un loup déformé), aux proportions d'un cheval de selle de 1,60 m au garrot.

Squelette : même nommage que les quadrupèdes de Korvalune (animations communes), mais articulations placées comme chez
le cheval : omoplate oblique, pointe de l'épaule, coude contre le thorax, genou (carpe), canon, boulet, paturon, sabot ;
hanche, grasset, jarret anguleux. Volumes sculptés : garrot, dos court, rein, croupe ronde, fesse, cuisse et gaskin,
avant-bras musclé, canons secs avec tendons, boulets marqués. Tête : chignon, ganaches rondes, chanfrein droit, bout du
nez charnu, naseaux creusés, lèvres, menton, yeux latéraux. Robe baie, extrémités noires (bas des jambes, bout du nez).
Crins : crinière tombant d'un seul côté, toupet, longue queue. Harnachement : tapis, selle (pommeau, troussequin,
quartiers), sangle, étriers, bride (têtière, muserolle, montants), mors et rênes. Point d'attache Socket_Saddle."""
import numpy as np
from forge.rig import Skeleton, Asset, qeuler
from forge.geo import Mesh, Loft, spline, curve, concat, rigid, unit, mirror, smoothstep
from forge import sculpt as SC
from forge.anim import key, build_clip
from creatures import common as CC, quadruped as QA

NAME = 'ChevalDeGuerre'

J = {
    'Hips': (0, 1.44, -0.55), 'Spine1': (0, 1.47, -0.26), 'Spine2': (0, 1.49, 0.04), 'Chest': (0, 1.46, 0.34),
    'Neck1': (0, 1.50, 0.58), 'Neck2': (0, 1.74, 0.86), 'Head': (0, 1.97, 1.04), 'Jaw': (0, 1.84, 1.08),
    'Tail1': (0, 1.50, -0.86), 'Tail2': (0, 1.38, -0.95), 'Tail3': (0, 1.18, -1.00), 'Tail4': (0, 0.98, -1.02),
    'Ear': (0.065, 2.03, 1.02),
    'Scapula': (0.12, 1.50, 0.34), 'UpperArm': (0.165, 1.13, 0.60), 'LowerArm': (0.165, 0.93, 0.43), 'Wrist': (0.155, 0.50, 0.465), 'FrontPaw': (0.155, 0.20, 0.47),
    'Thigh': (0.17, 1.33, -0.62), 'Shin': (0.19, 1.04, -0.40), 'Hock': (0.155, 0.60, -0.72), 'BackPaw': (0.155, 0.20, -0.665),
}
TIP = {'FrontPaw': (0.155, 0.0, 0.56), 'BackPaw': (0.155, 0.0, -0.58), 'Tail': (0, 0.70, -1.04), 'Head': (0, 1.56, 1.44), 'Ear': (0.09, 2.19, 0.99)}


def skeleton():
    mx = lambda p: (-p[0], p[1], p[2])
    sk = Skeleton()
    sk.add('Hips', None, J['Hips']); sk.add('Spine1', 'Hips', J['Spine1']); sk.add('Spine2', 'Spine1', J['Spine2']); sk.add('Chest', 'Spine2', J['Chest'])
    sk.add('Neck1', 'Chest', J['Neck1']); sk.add('Neck2', 'Neck1', J['Neck2']); sk.add('Head', 'Neck2', J['Head']); sk.add('Jaw', 'Head', J['Jaw'])
    par = 'Hips'
    for k in range(1, 5): sk.add(f'Tail{k}', par, J[f'Tail{k}']); par = f'Tail{k}'
    for s, f in (('L', lambda p: p), ('R', mx)):
        sk.add(f'Ear_{s}', 'Head', f(J['Ear']))
        sk.add(f'Scapula_{s}', 'Chest', f(J['Scapula'])); sk.add(f'UpperArm_{s}', f'Scapula_{s}', f(J['UpperArm']))
        sk.add(f'LowerArm_{s}', f'UpperArm_{s}', f(J['LowerArm'])); sk.add(f'Wrist_{s}', f'LowerArm_{s}', f(J['Wrist'])); sk.add(f'FrontPaw_{s}', f'Wrist_{s}', f(J['FrontPaw']))
        sk.add(f'Thigh_{s}', 'Hips', f(J['Thigh'])); sk.add(f'Shin_{s}', f'Thigh_{s}', f(J['Shin'])); sk.add(f'Hock_{s}', f'Shin_{s}', f(J['Hock'])); sk.add(f'BackPaw_{s}', f'Hock_{s}', f(J['BackPaw']))
    sk.add('Socket_Saddle', 'Spine2', (0, 1.66, 0.02), null=True)
    sk.add('Socket_Chest', 'Chest', (0, 1.20, 0.62), null=True)
    sk.add('Socket_Mouth', 'Jaw', (0, 1.58, 1.42), null=True)
    return sk


def P(n, side=1): p = np.array(J[n], float); p[0] *= side; return p


def body(sk):
    s = SC.Sculpt(k=0.035)
    # ---------------- tronc
    s.ell('Spine2', (0, 1.22, 0.00), (0.255, 0.34, 0.50))                          # coffre (côtes)
    s.ell('Chest', (0, 1.20, 0.34), (0.235, 0.36, 0.30))                           # poitrine
    s.ell('Chest', (0, 1.13, 0.60), (0.20, 0.21, 0.12))                            # poitrail
    s.ell('Chest', (0, 1.53, 0.30), (0.085, 0.10, 0.22), fwd=(0, 0.35, 1))         # garrot
    s.ell('Spine1', (0, 1.38, -0.28), (0.235, 0.19, 0.30))                         # rein
    s.ell('Spine2', (0, 1.00, -0.08), (0.22, 0.16, 0.42), k=0.05)                  # ventre (ligne qui remonte vers l'aine)
    s.ell('Hips', (0, 1.31, -0.56), (0.27, 0.27, 0.30))                            # arrière-main
    s.ell('Hips', (0, 1.50, -0.60), (0.19, 0.10, 0.26))                            # croupe
    s.ell('Hips', (0, 1.30, -0.80), (0.21, 0.22, 0.12))                            # fesse
    for sx in (1, -1):
        s.ell('Hips', (0.15 * sx, 1.45, -0.42), (0.07, 0.06, 0.08))               # pointe de la hanche
    # ---------------- encolure (plate sur les côtés, crête, gorge)
    s.cone('Neck1', (0, 1.40, 0.48), (0, 1.80, 0.92), 0.25, 0.135, squash=(0.62, (1, 0, 0)))
    s.ell('Neck1', (0, 1.66, 0.62), (0.075, 0.11, 0.30), fwd=(0, 0.75, 0.66))      # crête
    s.cone('Neck1', (0, 1.25, 0.64), (0, 1.66, 1.00), 0.13, 0.075, squash=(0.70, (1, 0, 0)))   # gorge
    s.cone('Neck2', (0, 1.74, 0.86), (0, 1.94, 1.02), 0.13, 0.11, squash=(0.72, (1, 0, 0)))
    # ---------------- tête
    s.ell('Head', (0, 1.93, 1.07), (0.095, 0.105, 0.115))                          # chignon, front
    s.cone('Head', (0, 1.91, 1.11), (0, 1.62, 1.40), 0.092, 0.062, squash=(0.78, (1, 0, 0)))   # chanfrein
    s.ell('Head', (0, 1.585, 1.425), (0.068, 0.070, 0.075))                        # bout du nez
    s.cone('Jaw', (0, 1.77, 1.06), (0, 1.56, 1.37), 0.07, 0.046, squash=(0.72, (1, 0, 0)), k=0.02)   # mâchoire
    s.ell('Jaw', (0, 1.535, 1.39), (0.045, 0.035, 0.05), k=0.015)                  # lèvre inférieure, menton
    for sx in (1, -1):
        s.ell('Jaw', (0.058 * sx, 1.79, 1.07), (0.045, 0.105, 0.10), fwd=(0, -0.55, 0.83), k=0.02)   # ganaches
        s.ell('Head', (0.085 * sx, 1.90, 1.13), (0.028, 0.032, 0.034), k=0.012)  # arcades
        s.ell('Head', (0.045 * sx, 1.605, 1.475), (0.016, 0.024, 0.022), sub=True)   # naseaux
    # ---------------- oreilles
    s.cone('Ear_L', (0.060, 2.00, 1.03), (0.090, 2.19, 0.995), 0.046, 0.014, squash=(0.55, (0.3, 0, 1)), k=0.016)
    # ---------------- antérieur gauche
    s.ell('Scapula_L', (0.15, 1.35, 0.44), (0.07, 0.24, 0.13), fwd=(0, -0.80, 0.60), up=(0, 0.6, 0.8))   # épaule
    s.cone('UpperArm_L', (0.165, 1.14, 0.60), (0.165, 0.95, 0.44), 0.12, 0.095)    # bras
    s.ell('UpperArm_L', (0.165, 1.02, 0.43), (0.085, 0.12, 0.10))                  # triceps / coude
    s.cone('LowerArm_L', (0.162, 0.92, 0.44), (0.155, 0.53, 0.465), 0.085, 0.048)  # avant-bras
    s.ell('LowerArm_L', (0.162, 0.80, 0.45), (0.070, 0.13, 0.075))
    s.ell('Wrist_L', (0.155, 0.50, 0.468), (0.048, 0.055, 0.052), k=0.015)        # genou
    s.cone('Wrist_L', (0.155, 0.48, 0.475), (0.155, 0.23, 0.475), 0.036, 0.034, squash=(0.78, (1, 0, 0)), k=0.012)   # canon
    s.cone('Wrist_L', (0.155, 0.46, 0.448), (0.155, 0.24, 0.452), 0.020, 0.022, k=0.012)   # tendons
    s.ell('FrontPaw_L', (0.155, 0.195, 0.462), (0.044, 0.048, 0.054), k=0.012)    # boulet
    s.cone('FrontPaw_L', (0.155, 0.18, 0.478), (0.155, 0.075, 0.53), 0.035, 0.040, k=0.012)   # paturon
    # ---------------- postérieur gauche
    s.ell('Thigh_L', (0.17, 1.20, -0.57), (0.115, 0.26, 0.21), fwd=(0, -0.55, 0.45))   # cuisse
    s.ell('Thigh_L', (0.15, 1.12, -0.74), (0.08, 0.20, 0.09))                         # fesse / ischio
    s.ell('Shin_L', (0.19, 1.03, -0.40), (0.065, 0.075, 0.07))                        # grasset
    s.cone('Shin_L', (0.18, 1.02, -0.43), (0.155, 0.64, -0.70), 0.105, 0.058)        # jambe
    s.ell('Shin_L', (0.168, 0.85, -0.60), (0.070, 0.14, 0.085), fwd=(0, -0.8, -0.55))   # gaskin
    s.ell('Hock_L', (0.155, 0.60, -0.72), (0.045, 0.07, 0.065), k=0.015)            # jarret
    s.ell('Hock_L', (0.155, 0.645, -0.785), (0.026, 0.035, 0.03), k=0.012)          # pointe du jarret
    s.cone('Hock_L', (0.155, 0.57, -0.70), (0.155, 0.23, -0.668), 0.038, 0.035, squash=(0.78, (1, 0, 0)), k=0.012)   # canon
    s.cone('Hock_L', (0.155, 0.55, -0.735), (0.155, 0.24, -0.692), 0.020, 0.022, k=0.012)
    s.ell('BackPaw_L', (0.155, 0.195, -0.672), (0.044, 0.048, 0.054), k=0.012)
    s.cone('BackPaw_L', (0.155, 0.18, -0.658), (0.155, 0.075, -0.60), 0.035, 0.040, k=0.012)
    # ---------------- tronçon de queue
    s.cone('Tail1', (0, 1.50, -0.83), (0, 1.33, -0.955), 0.062, 0.040, k=0.02)
    sw = lambda b: b[:-2] + '_R' if b.endswith('_L') else b
    s.mirror(sw)
    detail = lambda X: 0.0018 * SC.noise3(X, 0.05, seed=17, octaves=2)
    m = s.mesh(res=0.020, detail=detail, smooth=6)
    s.skin(m, sk, kw=0.04)
    SC.box_uv(m, 0.45)
    return m


def subset(m, keep_face):
    out = Mesh(m.V.copy(), m.F[keep_face].copy(), m.UV.copy() if m.UV is not None else None)
    out.J, out.W = m.J.copy(), m.W.copy()
    return out


# ---------------------------------------------------------------- textures
def coat(S, seed, base=(0.36, 0.17, 0.07), light=(0.52, 0.28, 0.12)):
    from forge.texgen import TexSet, fbm, n01, height_to_normal
    r = np.random.default_rng(seed)
    hair = fbm(S, S, r, base=S / 90, octaves=3, aniso=(0.08, 1.0))           # poil ras couché
    dapple = n01(fbm(S, S, r, base=S / 7, octaves=4))
    col = np.array(base)[None, None] * (0.85 + 0.3 * dapple[..., None]) + (np.array(light) - np.array(base))[None, None] * 0.35 * n01(hair)[..., None]
    return TexSet(np.clip(col, 0, 1), height_to_normal(hair, 0.25), np.zeros((S, S)), np.full((S, S), 0.45), np.clip(0.8 + 0.2 * n01(hair), 0, 1))


RECIPES = {
    'CH_Robe':   (lambda S: coat(S, 901), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'CH_Noir':   (lambda S: coat(S, 902, base=(0.035, 0.028, 0.025), light=(0.10, 0.08, 0.07)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'CH_Nez':    (lambda S: coat(S, 904, base=(0.12, 0.07, 0.05), light=(0.20, 0.12, 0.08)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'CH_Crins':  (lambda S: CC.fur_texture(S, seed=903, root=(0.012, 0.010, 0.009), tip=(0.075, 0.055, 0.04), density=1.6), 1024, 512, dict(metallic=0.0, smoothness=1.0, cutoff=0.4)),
    'CH_Sabot':  (lambda S: _horn(S), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'CH_Oeil':   (lambda S: _eye(S), 128, 64, dict(metallic=0.0, smoothness=1.0)),
}
from knight import materials as KM
from enemies import materials as EM
RECIPES.update({
    'CH_Cuir':   (lambda S: KM.leather(S, seed=911, tone=(0.26, 0.13, 0.06)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'CH_CuirN':  (lambda S: KM.leather(S, seed=912, tone=(0.06, 0.045, 0.035)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'CH_Tapis':  (lambda S: _blanket(S), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'CH_Metal':  (lambda S: KM.bone(S, seed=913, tone=(0.62, 0.62, 0.64)), 256, 128, dict(metallic=0.95, smoothness=1.0)),
    'CH_Laiton': (lambda S: KM.bone(S, seed=914, tone=(0.72, 0.56, 0.24)), 256, 128, dict(metallic=0.95, smoothness=1.0)),
})


def _horn(S):
    from forge.texgen import TexSet, fbm, n01, height_to_normal
    r = np.random.default_rng(7); st = fbm(S, S, r, base=S / 40, octaves=3, aniso=(1.0, 0.06))
    col = np.array((0.09, 0.08, 0.07))[None, None] * (0.8 + 0.5 * n01(st)[..., None])
    return TexSet(np.clip(col, 0, 1), height_to_normal(st, 0.4), np.zeros((S, S)), np.full((S, S), 0.55), np.ones((S, S)))


def _eye(S):
    from forge.texgen import TexSet
    yy, xx = np.mgrid[0:S, 0:S] / S
    r = np.hypot(xx - 0.5, yy - 0.5)
    col = np.where(r < 0.33, 0.03, 0.06)[..., None] * np.array((1.0, 0.75, 0.55))[None, None] + np.where(r < 0.2, 0.0, 0.03)[..., None]
    return TexSet(np.clip(col, 0, 1), np.dstack([np.full((S, S), .5), np.full((S, S), .5), np.ones((S, S))]), np.zeros((S, S)), np.full((S, S), 0.95), np.ones((S, S)))


def _blanket(S):
    from forge.texgen import TexSet, fbm, n01, height_to_normal
    r = np.random.default_rng(11); weave = fbm(S, S, r, base=S / 120, octaves=2)
    yy, xx = np.mgrid[0:S, 0:S] / S
    col = np.array((0.42, 0.05, 0.05))[None, None] * (0.85 + 0.25 * n01(weave)[..., None])
    border = (np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy)) < 0.06)
    col = np.where(border[..., None], np.array((0.78, 0.60, 0.22))[None, None], col)
    return TexSet(np.clip(col, 0, 1), height_to_normal(weave, 0.3), np.zeros((S, S)), np.full((S, S), 0.2), np.ones((S, S)))


# ---------------------------------------------------------------- assemblage
def _on_skin(m):
    from scipy.spatial import cKDTree
    from forge.geo import vertex_normals
    tree = cKDTree(m.V); N = vertex_normals(m)
    def f(p, sink=0.0):
        i = tree.query(p)[1]; return m.V[i] - N[i] * sink, N[i]
    return f, tree


SKIN = None


def strap(points, w, t, bone_of, sk, segs=6, hug=0.0):
    """Courroie plate le long d'une suite de points (largeur w, épaisseur t) ; pondérée sur l'os donné.
    hug > 0 : chaque point est plaqué sur la peau (décollé de hug) — bride, sangle."""
    if hug > 0 and SKIN is not None:
        on = SKIN; pts = []
        for p_ in spline(points, max(10, len(points) * 5)):
            q, nn = on(np.asarray(p_, float)); pts.append(q + nn * hug)
        points = pts
    n = max(8, len(points) * 6)
    L = Loft(spline(points, n), curve([(0, t), (1, t)], n), curve([(0, w), (1, w)], n), p=6.0, segs=segs, cap1=1).mesh(0.05)
    rigid(L, sk[bone_of]); return L


def build(bank=None):
    sk = skeleton(); a = Asset(NAME, sk); sw = {}
    for i, n in enumerate(sk.names):
        if n.endswith('_L'): j = sk.names.index(n[:-2] + '_R'); sw[i] = j; sw[j] = i
    rng = np.random.default_rng(41)
    m = body(sk)
    # robe baie ; extrémités noires : bas des jambes (sous le genou / le jarret) et bout du nez
    C = m.V[m.F].mean(1)
    dark = (C[:, 1] < 0.50) & (np.abs(C[:, 0]) > 0.07)
    nose = (C[:, 2] > 1.41) & (C[:, 1] < 1.70) & ~dark
    a.add('corps', subset(m, ~dark & ~nose), 'CH_Robe'); a.add('extremites', subset(m, dark), 'CH_Noir'); a.add('nez', subset(m, nose), 'CH_Nez')
    on, tree = _on_skin(m)
    global SKIN; SKIN = on
    # ---- yeux (globe sombre brillant, paupière)
    for sx in (1, -1):
        q, n_ = on(np.array([0.098 * sx, 1.885, 1.135]), 0.012)
        fw = unit(n_ + (0, 0, 0.4))
        e = SC.Sculpt(k=0.004).ell('Head', q, (0.030, 0.024, 0.036), fwd=fw).mesh(res=0.003, smooth=2)
        ax = unit(np.cross((0, 1, 0), fw)); ay = np.cross(fw, ax)
        e.UV = np.c_[0.5 + (e.V - q) @ ax / 0.07, 0.5 + (e.V - q) @ ay / 0.05]
        rigid(e, sk['Head']); a.add(f'oeil{sx}', e, 'CH_Oeil')
    # ---- sabots : corne évasée, plus haute en pince
    for leg, bone, z0 in (('FrontPaw', 'FrontPaw_L', 0.535), ('BackPaw', 'BackPaw_L', -0.605)):
        h = Loft(spline([(0.155, 0.085, z0 - 0.008), (0.155, 0.04, z0 + 0.004), (0.155, 0.0, z0 + 0.012)], 8),
                 curve([(0, .040), (1, .060)], 8), curve([(0, .043), (1, .064)], 8), p=2.4, segs=24, cap1=1).mesh(0.1)
        rigid(h, sk[bone]); a.add(f'sabot_{leg}', h, 'CH_Sabot'); a.add(f'sabot_{leg}_R', mirror(h, sw), 'CH_Sabot')
    # ---- crinière : tombe du côté droit le long de la crête ; toupet sur le front
    mid = np.abs(m.V[:, 0]) < 0.03
    zb = np.linspace(0.42, 1.02, 30)
    crest = np.array([m.V[mid & (np.abs(m.V[:, 2] - zz) < 0.02), 1].max(initial=-9) for zz in zb])
    def w_mane(Cc):
        x, y, z = Cc.T
        return ((z > 0.40) & (z < 1.03) & (np.abs(x) < 0.07) & (y > np.interp(z, zb, crest) - 0.05)) * 1.0
    mflow = lambda P_, N_: unit(np.c_[np.where(P_[:, 0] >= 0, 0.7, -0.7), np.full(len(P_), -1.0), np.full(len(P_), -0.25)])   # retombe de part et d'autre de la crête
    def lmane(P_, N_): return 0.26 + 0.06 * np.sin(P_[:, 2] * 9)
    mane = CC.fur_cards(m, 2400, rng, mflow, lmane, width=0.045, lift=0.30, weight=w_mane, curl=0.35, jitter=0.12, double=True)
    a.add('criniere', mane, 'CH_Crins')
    def w_fore(Cc):
        x, y, z = Cc.T; return ((z > 1.02) & (z < 1.10) & (y > 1.98) & (np.abs(x) < 0.05)) * 1.0
    fore = CC.fur_cards(m, 260, rng, lambda P_, N_: unit(np.c_[np.zeros(len(P_)), np.full(len(P_), -0.6), np.full(len(P_), 0.8)]), lambda P_, N_: np.full(len(P_), 0.16),
                        width=0.03, lift=0.10, weight=w_fore, curl=0.1, jitter=0.15, double=True)
    a.add('toupet', fore, 'CH_Crins')
    # ---- queue : gerbe de crins depuis le tronçon, jusqu'au jarret
    t0 = np.array((0, 1.45, -0.88)); tl = Loft(spline([t0, t0 + (0, -0.10, -0.10), t0 + (0, -0.45, -0.18), t0 + (0, -0.85, -0.16)], 18),
                                                curve([(0, .05), (.25, .08), (.7, .10), (1, .07)], 18), curve([(0, .045), (.25, .07), (.7, .085), (1, .06)], 18), p=2.0, segs=16, cap1=1).mesh(0.05)
    tl.J = np.zeros((len(tl.V), 4), np.int64); tl.W = np.zeros((len(tl.V), 4))
    yv = tl.V[:, 1]; f2 = np.clip((1.45 - yv) / 0.8, 0, 1)
    tl.J[:, 0] = sk['Tail1']; tl.J[:, 1] = sk['Tail2']; tl.J[:, 2] = sk['Tail3']; tl.W[:, 0] = np.clip(1 - 2 * f2, 0, 1); tl.W[:, 1] = 1 - np.abs(2 * f2 - 1) ; tl.W[:, 2] = np.clip(2 * f2 - 1, 0, 1)
    tl.W = tl.W / tl.W.sum(1, keepdims=True)
    tail = CC.fur_cards(tl, 1100, rng, lambda P_, N_: unit(np.c_[np.zeros(len(P_)), np.full(len(P_), -1.0), np.full(len(P_), -0.15)]), lambda P_, N_: np.full(len(P_), 0.32),
                        width=0.045, lift=0.04, curl=0.06, jitter=0.10, double=True)
    a.add('queue', tail, 'CH_Crins')
    # ---- tapis de selle, selle, sangle, étriers
    def drape(V, lift):
        d, i = tree.query(V); out = V.copy(); out[:, 1] = np.maximum(V[:, 1], m.V[i][:, 1] + lift); return out, i
    def onto_body(mesh, lift):
        mesh.V, i = drape(mesh.V, lift); mesh.J, mesh.W = m.J[i].copy(), m.W[i].copy(); return mesh
    from forge.geo import vertex_normals
    Nm = vertex_normals(m)
    def shell(sel_face, off, uvfn):
        keepv = np.unique(m.F[sel_face]); remap = -np.ones(len(m.V), np.int64); remap[keepv] = np.arange(len(keepv))
        V = m.V[keepv] + Nm[keepv] * off
        out = Mesh(V, remap[m.F[sel_face]], uvfn(m.V[keepv])); out.J, out.W = m.J[keepv].copy(), m.W[keepv].copy()
        return out
    Cf = m.V[m.F].mean(1)
    tap = (np.abs(Cf[:, 2] - 0.02) < 0.34) & (Cf[:, 1] > 1.22) & (Cf[:, 2] < 0.40 - 0.25 * np.clip((1.50 - Cf[:, 1]) / 0.3, 0, 1))
    blanket = shell(tap, 0.010, lambda V: np.c_[(V[:, 2] + 0.32) / 0.70, 0.5 + 0.5 * np.sign(V[:, 0]) * np.clip((1.70 - V[:, 1]) / 0.48, 0, 1)])
    edge = shell(tap, 0.004, lambda V: np.c_[(V[:, 2] + 0.32) / 0.70, 0.5 + 0.5 * np.sign(V[:, 0]) * np.clip((1.70 - V[:, 1]) / 0.48, 0, 1)])
    edge.F = edge.F[:, ::-1]
    a.add('tapis', concat([blanket, edge]), 'CH_Tapis')
    qt = (np.abs(Cf[:, 2] - 0.05) < 0.15) & (Cf[:, 1] > 1.30) & (Cf[:, 1] < 1.62) & (np.abs(Cf[:, 0]) > 0.12)
    a.add('quartiers', shell(qt, 0.024, lambda V: np.c_[V[:, 2] * 2, V[:, 1] * 2]), 'CH_Cuir')
    # selle : siège (coque sur le dos), pommeau et troussequin roulés qui suivent la courbe du dos, panneaux de cuir
    st_ = (np.abs(Cf[:, 2] - 0.02) < 0.22) & (Cf[:, 1] > 1.48) & (np.abs(Cf[:, 0]) < 0.17)
    a.add('selle', shell(st_, 0.040, lambda V: np.c_[V[:, 2] * 3, V[:, 0] * 3]), 'CH_Cuir')
    for zc, rise, r_ in ((0.20, 0.035, 0.040), (-0.19, 0.055, 0.042)):
        arc = []
        for xx in np.linspace(-0.19, 0.19, 13):
            q, nn = on(np.array((xx, 1.75, zc)))
            arc.append(q + nn * 0.04 + np.array((0, rise * (1 - (xx / 0.19) ** 2), 0)))
        roll = Loft(spline(arc, 26), curve([(0, r_ * 0.6), (.5, r_), (1, r_ * 0.6)], 26), curve([(0, r_ * 0.6), (.5, r_ * 0.9), (1, r_ * 0.6)], 26), p=2.2, segs=12, cap0=1, cap1=1, ref=(0, 0, 1)).mesh(0.1)
        _, ii = tree.query(roll.V); roll.J, roll.W = m.J[ii].copy(), m.W[ii].copy()
        a.add(f'selle_arcon{zc}', roll, 'CH_Cuir')
    for sx in (1, -1):
        # étrivière et étrier
        p0 = np.array((sx * 0.25, 1.60, 0.06)); p1 = np.array((sx * 0.31, 1.12, 0.06))
        a.add(f'etriviere{sx}', strap([p0, (p0 + p1) / 2 + (sx * 0.02, 0, 0), p1], 0.018, 0.004, 'Spine2', sk), 'CH_CuirN')
        st = Loft(spline([p1 + (0, 0, -0.05), p1 + (0, -0.09, -0.05), p1 + (0, -0.11, 0), p1 + (0, -0.09, 0.05), p1 + (0, 0, 0.05)], 14), curve([(0, .008), (1, .008)], 14), curve([(0, .008), (1, .008)], 14), p=2.0, segs=8).mesh(0.05)
        rigid(st, sk['Spine2']); a.add(f'etrier{sx}', st, 'CH_Metal')
    # sangle sous le ventre
    ring = [(np.sin(t) * 0.30, 1.22 + np.cos(t) * 0.38, 0.12) for t in np.linspace(-np.pi * 0.55, np.pi * 0.55, 1) ]
    girth_pts = [(x, y, 0.12) for x, y in [(0.27, 1.50), (0.29, 1.20), (0.22, 0.95), (0, 0.86), (-0.22, 0.95), (-0.29, 1.20), (-0.27, 1.50)]]
    g = strap(girth_pts, 0.045, 0.006, 'Spine2', sk, hug=0.008); g.J, g.W = m.J[tree.query(g.V)[1]].copy(), m.W[tree.query(g.V)[1]].copy(); a.add('sangle', g, 'CH_CuirN')
    # ---- bride : têtière (derrière les oreilles), montants, muserolle, frontal, mors et rênes
    hb = 'Head'
    def ring_around(center, rx, ry, axis_z=True, n=20):
        t = np.linspace(0, 2 * np.pi, n)
        return [center + np.array((np.cos(a_) * rx, np.sin(a_) * ry, 0)) for a_ in t]
    head_pts = []
    for sx in (1, -1):
        cheek = [(sx * 0.07, 2.00, 1.00), (sx * 0.095, 1.86, 1.10), (sx * 0.085, 1.70, 1.25), (sx * 0.075, 1.62, 1.33)]
        a.add(f'montant{sx}', strap(cheek, 0.018, 0.005, hb, sk, hug=0.008), 'CH_CuirN')
    a.add('teteiere', strap([(-0.075, 2.00, 1.00), (0, 2.035, 0.995), (0.075, 2.00, 1.00)], 0.02, 0.005, hb, sk, hug=0.008), 'CH_CuirN')
    a.add('frontal', strap([(-0.09, 1.99, 1.06), (0, 2.00, 1.10), (0.09, 1.99, 1.06)], 0.016, 0.005, hb, sk, hug=0.008), 'CH_CuirN')
    nose_c = np.array((0, 1.665, 1.32))
    nb = [nose_c + (np.cos(t) * 0.085, np.sin(t) * 0.075, -np.sin(t) * 0.05) for t in np.linspace(0, 2 * np.pi, 22)]
    a.add('muserolle', strap(nb, 0.022, 0.005, hb, sk, hug=0.008), 'CH_CuirN')
    for sx in (1, -1):
        bit = SC.Sculpt(k=0.003).ell('Jaw', (sx * 0.072, 1.58, 1.37), (0.006, 0.022, 0.022)).mesh(res=0.003, smooth=2)
        rigid(bit, sk['Jaw']); a.add(f'mors{sx}', bit, 'CH_Laiton')
        reins = strap([(sx * 0.075, 1.58, 1.37), (sx * 0.12, 1.55, 1.10), (sx * 0.16, 1.58, 0.70), (sx * 0.17, 1.66, 0.36), (sx * 0.12, 1.72, 0.24)], 0.014, 0.004, 'Neck1', sk)
        a.add(f'renes{sx}', onto_body(reins, 0.012), 'CH_CuirN')
    # ---- animations
    build_anims(sk, a)
    return a


# ---------------------------------------------------------------- animations du cheval
LEGF = ('UpperArm', 'LowerArm', 'Wrist', 'FrontPaw'); LEGB = ('Thigh', 'Shin', 'Hock', 'BackPaw')


def bones(sk): return [n for n in sk.names if not n.startswith('Socket') and n != 'Hips']


def gait(sk, name, T, amp, phase, lift, bob, head_bob, n=13, pitch=0.0):
    keys = []
    for k in range(n):
        t = k * T / (n - 1); w = 2 * np.pi * t / T; p = {}
        for side, s_ in (('L', 0.0), ('R', 0.5)):
            for leg, off in (('F', phase[0]), ('B', phase[1])):
                ph = w + 2 * np.pi * (off + s_)
                sw = np.sin(ph); up = max(0.0, -np.cos(ph))
                if leg == 'F':
                    p[f'UpperArm_{side}'] = (amp * sw * 0.8, 0, 0)
                    p[f'LowerArm_{side}'] = (-amp * 0.25 * max(0, -sw) - lift * 0.25 * up, 0, 0)
                    p[f'Wrist_{side}'] = (lift * up * 1.6, 0, 0)                     # le genou plie en levant le pied
                    p[f'FrontPaw_{side}'] = (lift * up * 0.8 - 12 * max(0, sw), 0, 0)
                else:
                    p[f'Thigh_{side}'] = (amp * 0.85 * sw, 0, 0)
                    p[f'Shin_{side}'] = (-lift * up * 0.5, 0, 0)
                    p[f'Hock_{side}'] = (lift * up * 1.1 + 6 * max(0, sw), 0, 0)
                    p[f'BackPaw_{side}'] = (-lift * up * 0.9 + 10 * max(0, sw), 0, 0)
        p['Neck1'] = (-head_bob * np.sin(2 * w), 0, 0); p['Head'] = (head_bob * 0.5 * np.sin(2 * w + 0.5), 0, 0)
        for i in range(1, 5): p[f'Tail{i}'] = (4 + 3 * np.sin(w + i * 0.6), 4 * np.sin(w + i * 0.7), 0)
        keys.append(key(t, p, hips=(0, -bob * abs(np.cos(w)), 0), hips_rot=(pitch * np.sin(2 * w), 2 * np.sin(w), 0)))
    return build_clip(name, keys, bones(sk), loop=True)


def idle(sk):
    keys = []
    for k in range(9):
        t = k * 0.6; w = 2 * np.pi * t / 4.8
        p = {'Chest': (0.8 * np.sin(2 * w), 0, 0), 'Neck1': (4 + 3 * np.sin(w), 4 * np.sin(w * 0.5), 0), 'Head': (-2 + 2 * np.sin(w + 1), 3 * np.sin(w * 0.5 + 0.4), 0),
             'Ear_L': (8 * np.sin(w * 3), 0, 10 * np.sin(w * 2)), 'Ear_R': (8 * np.sin(w * 3 + 1), 0, -10 * np.sin(w * 2 + 0.5)), 'Jaw': (2 * max(0, np.sin(w * 4)), 0, 0)}
        for i in range(1, 5): p[f'Tail{i}'] = (2, 14 * np.sin(w * 2 + i * 0.5) * (1 if k % 4 else 0.3), 0)    # coups de queue
        p['Hock_R'] = (6 * max(0, np.sin(w)), 0, 0); p['BackPaw_R'] = (-4 * max(0, np.sin(w)), 0, 0)              # repose une jambe arrière
        keys.append(key(t, p, hips=(0, -0.004 * np.sin(2 * w), 0), hips_rot=(0, 0, 1.0 * np.sin(w))))
    return build_clip('Idle', keys, bones(sk), loop=True)


def rear(sk, name='Howl'):
    """Le cheval se cabre : arrière-main fléchie, avant-main levée, antérieurs repliés, puis retombe."""
    up = {'Neck1': (-18, 0, 0), 'Head': (12, 0, 0), 'Jaw': (10, 0, 0)}
    for s in ('L', 'R'):
        up[f'UpperArm_{s}'] = (-55, 0, 0); up[f'LowerArm_{s}'] = (40, 0, 0); up[f'Wrist_{s}'] = (95, 0, 0); up[f'FrontPaw_{s}'] = (20, 0, 0)
        up[f'Thigh_{s}'] = (38, 0, 0); up[f'Shin_{s}'] = (-25, 0, 0); up[f'Hock_{s}'] = (20, 0, 0)
    for i in range(1, 5): up[f'Tail{i}'] = (-12, 0, 0)
    ks = [key(0, {}), key(0.45, up, hips=(0, 0.10, -0.25), hips_rot=(-42, 0, 0)), key(1.0, up, hips=(0, 0.12, -0.27), hips_rot=(-46, 0, 0)), key(1.6, {})]
    return build_clip(name, ks, bones(sk))


def strike(sk):
    """Attaque : demi-cabrade et frappe des antérieurs au sol."""
    up = {'Neck1': (-10, 0, 0)}
    hit = {'Neck1': (12, 0, 0), 'Head': (6, 0, 0)}
    for s in ('L', 'R'):
        up[f'UpperArm_{s}'] = (-60, 0, 0); up[f'Wrist_{s}'] = (90, 0, 0); up[f'Thigh_{s}'] = (25, 0, 0); up[f'Hock_{s}'] = (15, 0, 0)
        hit[f'UpperArm_{s}'] = (-20, 0, 0); hit[f'Wrist_{s}'] = (5, 0, 0)
    ks = [key(0, {}), key(0.30, up, hips=(0, 0.06, -0.15), hips_rot=(-26, 0, 0)), key(0.55, hit, hips=(0, -0.03, 0.05), hips_rot=(4, 0, 0)), key(0.95, {})]
    return build_clip('Attack', ks, bones(sk))


def hit_(sk):
    ks = [key(0, {}), key(0.15, {'Neck1': (-12, 18, 0), 'Head': (6, 12, 0)}, hips_rot=(0, -6, 3), hips=(0, -0.02, -0.06)), key(0.6, {})]
    return build_clip('Hit', ks, bones(sk))


def death(sk):
    e = {'Neck1': (25, 0, 30), 'Head': (20, 0, 15)}
    for s in ('L', 'R'): e[f'UpperArm_{s}'] = (-25, 0, 0); e[f'Wrist_{s}'] = (30, 0, 0); e[f'Thigh_{s}'] = (-15, 0, 0)
    ks = [key(0, {}), key(0.5, {'Neck1': (15, 0, 0)}, hips=(0, -0.35, 0), hips_rot=(0, 0, 25)), key(1.3, e, hips=(0, -0.95, 0), hips_rot=(0, 0, 86))]
    return build_clip('Death', ks, bones(sk))


def build_anims(sk, a):
    clips = [idle(sk),
             gait(sk, 'Walk', 1.25, 20, (0.25, 0.0), 32, 0.012, 4),                 # pas : quatre temps
             gait(sk, 'Run', 0.62, 36, (0.12, 0.62), 55, 0.06, 7, pitch=5),          # galop
             strike(sk), rear(sk), hit_(sk), death(sk)]
    for c in clips: a.clips[c.name] = c
