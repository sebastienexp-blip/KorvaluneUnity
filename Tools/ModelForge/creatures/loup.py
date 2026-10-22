"""Loup des Ténèbres — créature sauvage : loup géant (1 m au garrot), corps sculpté d'après l'anatomie du loup (cage
thoracique profonde et étroite, ventre levé, croupe, épaules et cuisses musclées, tête longue au stop marqué), pelage
de mèches sombres hirsutes, longue crinière rouge sang sur la nuque et l'échine, yeux rouges luisants, babines
retroussées sur des crocs jaunis, griffes noires. Squelette quadrupède (rig Generic)."""
import numpy as np
from forge.rig import Asset
from forge.geo import Loft, spline, curve, concat, rigid, unit, mirror, blend_bones
from forge import sculpt as SC
from forge.texgen import TexSet
from knight import materials as KM
from creatures import wolf as WB, common as CC, quadruped as QA
from enemies import materials as EM

NAME = 'LoupTenebres'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.20, 0.80)
RECIPES = {
    'WF_Skin':  (lambda S: CC.under_fur(S, seed=701, color=(0.035, 0.032, 0.033), streak=(0.20, 0.19, 0.19)), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'WF_Fur':   (lambda S: CC.fur_texture(S, seed=702, root=(0.025, 0.022, 0.022), tip=(0.34, 0.33, 0.31), accent=(0.52, 0.50, 0.46), accent_amt=0.3), 1024, 512,
                 dict(metallic=0.0, smoothness=1.0, cutoff=0.45)),
    'WF_Mane':  (lambda S: CC.ember(CC.fur_texture(S, seed=703, root=(0.05, 0.012, 0.010), tip=(0.62, 0.07, 0.03), accent=(0.30, 0.28, 0.26), accent_amt=0.45)), 1024, 512,
                 dict(metallic=0.0, smoothness=1.0, cutoff=0.45, emission=(0.8, 0.8, 0.8), emission_preview=(0.4, 0.4, 0.4))),
    'WF_Bone':  (lambda S: KM.bone(S, seed=704, tone=(0.62, 0.55, 0.40)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'WF_Claw':  (lambda S: KM.bone(S, seed=705, tone=(0.08, 0.07, 0.065)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'WF_Gum':   (lambda S: EM.tinted(KM.leather(S, seed=706, tone=(0.20, 0.04, 0.05)), (1.0, 1.0, 1.0)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'WF_Nose':  (lambda S: KM.leather(S, seed=707, tone=(0.02, 0.018, 0.02)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'WF_Eye':   (lambda S: EM.glow(S, (1.0, 0.12, 0.04)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(3.0, 3.0, 3.0), emission_preview=(2.5, 2.5, 2.5))),
}
GEAR = []
J = WB.J


def P(name, side=1):
    p = np.array(J[name], float); p[0] *= side; return p


def body(sk):
    s = SC.Sculpt(k=0.022)
    # ---- tronc : cage thoracique profonde et étroite, ventre levé, reins, croupe
    s.ell('Chest', (0, 0.86, 0.22), (0.19, 0.29, 0.29), fwd=(0, -0.25, 1))
    s.ell('Chest', (0, 1.04, 0.30), (0.150, 0.12, 0.22))                     # garrot
    s.ell('Spine2', (0, 0.92, -0.02), (0.130, 0.150, 0.22), fwd=(0, 0.15, 1))
    s.ell('Spine1', (0, 0.95, -0.25), (0.120, 0.120, 0.18), fwd=(0, 0.25, 1))
    s.ell('Hips', (0, 0.95, -0.44), (0.135, 0.150, 0.17))
    s.ell('Hips', (0, 0.99, -0.52), (0.110, 0.100, 0.11))                   # sacrum / départ de queue
    s.cone('Spine2', (0, 0.80, 0.18), (0, 0.86, -0.18), 0.09, 0.06)         # ligne du ventre qui remonte
    # ---- cou puissant et tête
    s.cone('Neck1', (0, 1.00, 0.36), (0, 1.12, 0.58), 0.17, 0.12)
    s.cone('Neck2', (0, 1.12, 0.58), (0, 1.19, 0.68), 0.105, 0.09)
    s.ell('Neck1', (0, 0.94, 0.42), (0.10, 0.11, 0.10))                      # poitrail
    # tête de loup : crâne large, arcades, stop marqué, chanfrein plat, museau étroit ; gueule ouverte (grognement)
    s.ell('Head', (0, 1.230, 0.715), (0.088, 0.078, 0.098))                   # boîte crânienne
    s.ell('Head', (0.055, 1.200, 0.735), (0.045, 0.052, 0.062))              # masséters / joues
    s.ell('Head', (0.050, 1.258, 0.800), (0.030, 0.020, 0.030))              # arcades sourcilières
    s.cone('Head', (0, 1.225, 0.80), (0, 1.195, 0.990), 0.060, 0.034, squash=(0.72, (0, 1, 0.15)))   # chanfrein et museau (dessus plat)
    s.cone('Head', (0, 1.185, 0.80), (0, 1.165, 0.975), 0.054, 0.031, squash=(0.80, (1, 0, 0)))    # lèvre supérieure
    s.ell('Head', (0, 1.195, 1.000), (0.027, 0.023, 0.022))                   # base de la truffe
    # mâchoire inférieure ouverte d'environ 22°
    s.cone('Jaw', (0, 1.112, 0.79), (0, 1.045, 0.950), 0.042, 0.022, squash=(0.70, (1, 0, 0)), k=0.010)
    s.ell('Jaw', (0, 1.090, 0.80), (0.045, 0.030, 0.050), k=0.010)
    # ---- oreilles
    s.cone('Ear_L', (0.055, 1.290, 0.700), (0.080, 1.420, 0.670), 0.030, 0.006, squash=(0.35, (0.2, 0, 1)))
    # ---- membres antérieurs : omoplate, bras, coude, avant-bras, carpe, paturon, pied
    s.ell('Scapula_L', (0.105, 1.00, 0.34), (0.045, 0.13, 0.085), fwd=(0, 0.6, 1))
    s.cone('UpperArm_L', (0.115, 0.92, 0.38), P('LowerArm') + (0, 0.02, -0.01), 0.105, 0.062)
    s.ell('UpperArm_L', (0.120, 0.76, 0.36), (0.065, 0.11, 0.085))           # triceps
    s.cone('LowerArm_L', P('LowerArm'), P('Wrist'), 0.060, 0.034)
    s.ell('LowerArm_L', (0.128, 0.42, 0.35), (0.052, 0.10, 0.055))
    s.cone('Wrist_L', P('Wrist'), P('FrontPaw') + (0, 0.01, -0.01), 0.034, 0.034)
    s.ell('FrontPaw_L', P('FrontPaw') + (0, -0.012, 0.035), (0.050, 0.030, 0.065), fwd=(0, -0.15, 1))
    # ---- membres postérieurs : cuisse, jarret, canon, pied
    s.ell('Thigh_L', (0.120, 0.82, -0.42), (0.090, 0.170, 0.150), fwd=(0, -0.35, 1))
    s.ell('Thigh_L', (0.110, 0.80, -0.53), (0.060, 0.120, 0.070))             # ischio-jambiers
    s.cone('Shin_L', P('Shin') + (0, 0.02, -0.02), P('Hock') + (0, 0.01, 0.01), 0.075, 0.034)
    s.ell('Shin_L', (0.130, 0.52, -0.37), (0.045, 0.10, 0.055), fwd=(0, -0.6, -0.5))
    s.cone('Hock_L', P('Hock'), P('BackPaw') + (0, 0.01, -0.01), 0.034, 0.033)
    s.ell('Hock_L', P('Hock') + (0, 0.0, -0.025), (0.020, 0.03, 0.022))       # pointe du jarret
    s.ell('BackPaw_L', P('BackPaw') + (0, -0.012, 0.035), (0.048, 0.029, 0.062), fwd=(0, -0.15, 1))
    # ---- queue
    for a_, b_, ra, rb in (('Tail1', 'Tail2', 0.050, 0.042), ('Tail2', 'Tail3', 0.042, 0.034), ('Tail3', 'Tail4', 0.034, 0.024)):
        s.cone(a_, P(a_), P(b_), ra, rb)
    s.cone('Tail4', P('Tail4'), np.array(WB.TIP['Tail']), 0.024, 0.010)
    sw = lambda b: b[:-2] + '_R' if b.endswith('_L') else b
    s.mirror(sw)
    detail = lambda X: 0.0025 * SC.noise3(X, 0.03, seed=7, octaves=2)
    m = s.mesh(res=0.006, detail=detail, smooth=10)
    s.skin(m, sk, kw=0.025)
    SC.box_uv(m, 0.30)
    return m, s


def flow(P_, N_):
    """Sens du poil : vers l'arrière sur le tronc, vers le bas sur les membres et le ventre, le long de la queue."""
    x, y, z = P_.T
    back = np.array([0, -0.15, -1.0])
    d = np.tile(back, (len(P_), 1))
    leg = (y < 0.78) & (np.abs(x) > 0.06)
    d[leg] = (0, -1, -0.15)
    tail = z < -0.58
    d[tail] = np.c_[np.zeros(tail.sum()), -0.9 * np.ones(tail.sum()), -0.6 * np.ones(tail.sum())]
    head = z > 0.62
    d[head] = (0, -0.1, -1)
    neck = (z > 0.30) & (z <= 0.62)
    d[neck] = (0, -0.6, -0.8)
    return unit(d)


def build(bank, female=False):
    sk = WB.skeleton(); a = Asset(NAME, sk); sw = WB.swap(sk)
    rng = np.random.default_rng(11)
    m, s = body(sk)
    a.add('corps', m, 'WF_Skin')
    x, y, z = m.V.T
    # ---- pelage : poil court sur la tête et les pattes, plus long sur les flancs, crinière rouge très longue sur la nuque et l'échine
    top = lambda P_, N_: np.clip(N_[:, 1], 0, 1) * (np.abs(P_[:, 0]) < 0.10)
    def len_fur(P_, N_):
        x, y, z = P_.T
        L = 0.055 + 0.03 * np.clip((0.75 - np.abs(z - 0.1)) / 0.75, 0, 1)
        L = np.where(y < 0.60, 0.022, L)                                          # bas des pattes : ras
        L = np.where(z > 0.70, 0.020 + 0.03 * np.clip((0.80 - z) / 0.1, 0, 1), L)  # tête
        L = np.where((z > 0.30) & (z < 0.70) & (y < 1.0), 0.09, L)              # fraise du cou / poitrail
        L = np.where(z < -0.60, 0.085, L)                                         # queue touffue
        return L
    def w_fur(C):
        mane_ = (np.abs(C[:, 0]) < 0.07) & (C[:, 1] > 0.98) & (C[:, 2] > -0.55) & (C[:, 2] < 0.70)
        return np.where(mane_, 0.25, 1.0) * np.where(C[:, 1] < 0.12, 0.0, 1.0)
    fur = CC.fur_cards(m, 8000 if not bank.preview else 5200, rng, flow, len_fur, width=0.022, lift=0.42, weight=w_fur, curl=0.25, jitter=0.35)
    a.add('pelage', fur, 'WF_Fur')
    def len_mane(P_, N_):
        z = P_[:, 2]
        return 0.06 + 0.07 * np.exp(-((z - 0.40) / 0.22) ** 2)
    def w_mane(C):
        x, y, z = C.T   # braises : flancs du cou et épaules, traînée le long de l'échine
        sh = np.exp(-((z - 0.36) / 0.12) ** 2) * np.exp(-((y - 1.02) / 0.10) ** 2) * (np.abs(x) > 0.05)
        sp = (np.abs(x) < 0.07) * (y > 0.98) * np.exp(-((z - 0.10) / 0.35) ** 2) * 0.5
        return sh + sp
    mflow = lambda P_, N_: unit(np.c_[np.sign(P_[:, 0]) * 0.25, np.full(len(P_), -0.35), np.full(len(P_), -1.0)])
    mane = CC.fur_cards(m, 900 if not bank.preview else 650, rng, mflow, len_mane, width=0.040, lift=0.55, weight=w_mane, curl=0.45, jitter=0.35)
    a.add('criniere', mane, 'WF_Mane')
    # ---- yeux luisants, truffe, gueule, crocs, griffes
    from scipy.spatial import cKDTree
    from forge.geo import vertex_normals
    tree = cKDTree(m.V); Nm = vertex_normals(m)
    def on_skin(p, sink=0.0):
        i = tree.query(p)[1]; return m.V[i] - Nm[i] * sink, Nm[i]
    for sx in (1, -1):
        q, n_ = on_skin(np.array([0.050 * sx, 1.245, 0.825]), 0.004)
        e = SC.Sculpt(k=0.003).ell('Head', q, (0.014, 0.008, 0.011), fwd=unit(n_ + (0, 0, 0.6)), up=(0, 1, 0)).mesh(res=0.0015, smooth=2)
        rigid(e, sk['Head']); a.add(f'oeil{sx}', e, 'WF_Eye')
    q, n_ = on_skin(np.array([0, 1.20, 1.03]), 0.004)
    nose = SC.Sculpt(k=0.004).ell('Head', q + (0, 0.004, 0.004), (0.024, 0.016, 0.014)).ell('Head', q + (0, -0.006, 0.004), (0.016, 0.010, 0.012)).mesh(res=0.002, smooth=3)
    rigid(nose, sk['Head']); a.add('truffe', nose, 'WF_Nose')
    # intérieur de la gueule : palais et langue rouge sombre
    pal = SC.Sculpt(k=0.006).cone('Head', (0, 1.150, 0.79), (0, 1.145, 0.95), 0.034, 0.018, squash=(0.35, (0, 1, 0))).mesh(res=0.003, smooth=3)
    rigid(pal, sk['Head']); a.add('palais', pal, 'WF_Gum')
    tg = SC.Sculpt(k=0.006).cone('Jaw', (0, 1.100, 0.80), (0, 1.060, 0.93), 0.030, 0.020, squash=(0.40, (0, 1, 0.4))).mesh(res=0.003, smooth=3)
    rigid(tg, sk['Jaw']); a.add('langue', tg, 'WF_Gum')
    teeth_u, teeth_l = [], []
    def tooth(base, d, h, w_):
        return Loft(spline([base, base + d * h * 0.55 + (0, 0, 0.002), base + d * h], 6), curve([(0, w_), (1, .0007)], 6), curve([(0, w_ * 0.9), (1, .0006)], 6), p=2.0, segs=8, cap1=1).mesh(0.03)
    for t_, h, w_ in ((0.06, 0.011, 0.0040), (0.18, 0.012, 0.0042), (0.30, 0.013, 0.0045), (0.42, 0.014, 0.0048), (0.56, 0.016, 0.0052), (0.70, 0.015, 0.0050), (0.82, 0.040, 0.0080), (0.93, 0.011, 0.0035)):
        for sx in (1, -1):
            z_ = 0.80 + t_ * 0.175; y_u = 1.163 - 0.018 * t_; xw = 0.040 - 0.020 * t_ ** 2
            teeth_u.append(tooth(np.array([sx * xw, y_u, z_]), np.array([0, -1, 0.15]), h, w_))
            zl = 0.80 + t_ * 0.15; yl = 1.108 - 0.062 * t_; xl = 0.036 - 0.018 * t_ ** 2
            teeth_l.append(tooth(np.array([sx * xl, yl + 0.006, zl]), np.array([0, 1, 0.25]), h * (0.9 if t_ < 0.8 else 0.85), w_))
    tu = concat(teeth_u); rigid(tu, sk['Head']); a.add('crocs_h', tu, 'WF_Bone')
    tl = concat(teeth_l); rigid(tl, sk['Jaw']); a.add('crocs_b', tl, 'WF_Bone')
    for leg, bone in (('FrontPaw', 'FrontPaw_L'), ('BackPaw', 'BackPaw_L')):
        c = P(leg)
        cl = []
        for dx in (-0.026, -0.009, 0.009, 0.026):
            base = c + (dx, -0.012, 0.080)
            cl.append(Loft(spline([base, base + (dx * 0.1, -0.010, 0.016), base + (dx * 0.15, -0.034, 0.024)], 8), curve([(0, .0055), (1, .0008)], 8), curve([(0, .0045), (1, .0007)], 8), p=2.0, segs=8, cap1=1).mesh(0.03))
        cm = concat(cl); rigid(cm, sk[bone]); a.add(f'griffes_{leg}', cm, 'WF_Claw'); a.add(f'griffes_{leg}_R', mirror(cm, sw), 'WF_Claw')
    QA.build(sk, a, kind='wolf')
    return a, {}, a.clips
