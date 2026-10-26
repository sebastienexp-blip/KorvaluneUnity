"""Démon brute — ennemi lourd : colosse voûté (≈ 2,8 m une fois mis à l'échelle), musculature massive sculptée
(trapèzes, deltoïdes, pectoraux, abdominaux, avant-bras énormes), peau de roche noire fendue de lave incandescente,
cornes de bélier recourbées, arcade lourde, mâchoire à crocs, griffes ; pagne de cuir et ceinture à plaques ;
fendoir de lave dentelé à la lame fissurée."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, sculpt as SC
from forge.geo import Loft, Mesh, spline, curve, concat, rigid, unit, mirror, vertex_normals, grid_faces, smoothstep
from knight import anims, materials as KM
from enemies import materials as EM

NAME = 'DemonBrute'
FOLDER = 'Enemies'
SCALE = 1.5
MAX_TRIS = 180000
HEAD_VIEW = (0, 1.62, 0.10)
RECIPES = {
    'DB_Skin':    (lambda S: EM.lava_rock(S, seed=801), 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(2.5, 2.5, 2.5), emission_preview=(1.4, 1.4, 1.4))),
    'DB_Horn':    (lambda S: EM.tinted(KM.bone(S, seed=802, tone=(0.20, 0.17, 0.15)), (1, 1, 1)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'DB_Claw':    (lambda S: KM.bone(S, seed=803, tone=(0.10, 0.085, 0.075)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'DB_Leather': (lambda S: KM.leather(S, seed=804, tone=(0.09, 0.05, 0.03)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'DB_Iron':    (lambda S: KM.steel(S, seed=805, tone=(0.16, 0.13, 0.11), polish=0.4, rust_amt=2.0, grime_amt=1.5), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'DB_Eye':     (lambda S: EM.glow(S, (1.0, 0.55, 0.10)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.5, 2.5, 2.5))),
    'DB_Tooth':   (lambda S: KM.bone(S, seed=806, tone=(0.55, 0.47, 0.33)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'DB_Blade':   (lambda S: EM.lava_rock(S, seed=807, tone=(0.10, 0.09, 0.085), cells=22, glow_amt=1.3), 1024, 512, dict(metallic=0.4, smoothness=1.0, emission=(3.0, 3.0, 3.0), emission_preview=(1.6, 1.6, 1.6))),
}
GEAR = [('Socket_Weapon_R', 'LavaCleaver')]

CORE, ARM, LEG = H.CORE, H.ARM, H.LEG
def A(n): return np.array(ARM[n], float)
def L(n): return np.array(LEG[n], float)
def SW(b): return b.replace('Left', 'Right') if b.startswith('Left') else b


def body(sk):
    s = SC.Sculpt(k=0.019)
    # ---- tronc : cage énorme, pectoraux en deux faisceaux, abdominaux, obliques, dentelés, dos bossu de trapèzes
    s.ell('Chest', (0, 1.28, 0.00), (0.225, 0.20, 0.160), k=0.03)
    s.ell('UpperChest', (0.095, 1.375, 0.080), (0.115, 0.070, 0.072), fwd=(0.35, -0.2, 1))         # grand pectoral (faisceau claviculaire)
    s.ell('UpperChest', (0.090, 1.315, 0.090), (0.108, 0.060, 0.070), fwd=(0.30, -0.35, 1))        # faisceau sternal
    s.ell('UpperChest', (0, 1.43, -0.07), (0.20, 0.10, 0.12), k=0.03)                               # haut du dos
    s.ell('UpperChest', (0.105, 1.545, -0.025), (0.125, 0.085, 0.100), fwd=(1, 0.55, 0))            # trapèze (bosse)
    s.ell('Chest', (0.135, 1.29, -0.095), (0.085, 0.16, 0.070), fwd=(0.2, -1, 0.3))                 # grand dorsal
    for k, y in enumerate((1.215, 1.150, 1.085)):                                                    # abdominaux 3 × 2
        s.ell('Spine', (0.038, y, 0.128 - 0.008 * k), (0.036, 0.030, 0.028), k=0.008)
    s.ell('Spine', (0, 1.13, 0.02), (0.175, 0.12, 0.125), k=0.03)
    s.ell('Spine', (0.150, 1.10, 0.045), (0.050, 0.085, 0.060), fwd=(0.5, 0, 1), k=0.010)           # oblique
    for k, y in enumerate((1.30, 1.255, 1.21)):                                                      # dentelé
        s.ell('Chest', (0.170, y, 0.075), (0.030, 0.018, 0.030), fwd=(1, -0.4, 0.4), k=0.008)
    s.ell('Hips', (0, 0.99, -0.01), (0.19, 0.11, 0.14), k=0.03)
    s.ell('Hips', (0.085, 0.94, -0.090), (0.090, 0.090, 0.075))                                      # fessier
    # ---- cou de taureau projeté vers l'avant, grosse tête basse
    s.cone('Neck', (0, 1.47, -0.02), (0, 1.58, 0.09), 0.130, 0.105, k=0.03)
    s.ell('Head', (0, 1.640, 0.085), (0.105, 0.105, 0.110))                                          # crâne
    s.ell('Head', (0, 1.682, 0.175), (0.095, 0.034, 0.040), k=0.010)                                 # arcade lourde
    s.ell('Head', (0, 1.620, 0.185), (0.065, 0.048, 0.055))                                          # mufle
    s.ell('Head', (0, 1.565, 0.160), (0.085, 0.050, 0.075))                                          # mâchoire carrée
    s.ell('Head', (0.075, 1.605, 0.115), (0.040, 0.058, 0.055))                                      # masséters
    s.ell('Head', (0, 1.525, 0.165), (0.050, 0.030, 0.045))                                          # menton
    # ---- bras (pose T) : deltoïde, biceps, triceps, avant-bras, main énorme
    s.ell('LeftShoulder', (0.215, 1.500, 0.040), (0.085, 0.095, 0.070))                             # deltoïde antérieur
    s.ell('LeftShoulder', (0.235, 1.505, -0.020), (0.090, 0.100, 0.075))                            # deltoïde moyen
    s.ell('LeftShoulder', (0.210, 1.495, -0.085), (0.080, 0.090, 0.065))                            # deltoïde postérieur
    s.cone('LeftUpperArm', A('UpperArm') + (0.03, 0, 0), A('LowerArm'), 0.118, 0.088)
    s.ell('LeftUpperArm', (0.33, 1.495, 0.035), (0.095, 0.065, 0.060))                               # biceps
    s.ell('LeftUpperArm', (0.33, 1.455, -0.075), (0.105, 0.060, 0.060))                              # triceps
    s.cone('LeftLowerArm', A('LowerArm'), A('Hand') - (0.02, 0, 0), 0.098, 0.068)
    s.ell('LeftLowerArm', (0.545, 1.480, 0.030), (0.095, 0.070, 0.060))                              # fléchisseurs
    s.ell('LeftLowerArm', (0.545, 1.485, -0.035), (0.095, 0.065, 0.058))                             # extenseurs
    s.ell('LeftHand', A('Hand') + (0.050, -0.005, 0.0), (0.072, 0.040, 0.070))                       # paume
    for fn, pts in H.FINGERS.items():
        p = [np.array(q) for q in pts]
        r0 = 0.024 if fn != 'Little' else 0.019
        names = [f'Left{fn}{s_}' for s_ in ('Proximal', 'Intermediate', 'Distal')]
        for k in range(3):
            d = p[k + 1] - p[k]; e = p[k] + d * 1.25
            s.cone(names[k], p[k], e, r0 * (1 - 0.15 * k), r0 * (0.85 - 0.15 * k), k=0.010)
    # ---- jambes trapues
    s.cone('LeftUpperLeg', L('UpperLeg') + (0.01, 0.02, 0), L('LowerLeg') + (0, 0.04, 0), 0.135, 0.085)
    s.ell('LeftUpperLeg', (0.10, 0.78, 0.030), (0.075, 0.17, 0.080), fwd=(0, -1, 0.1))               # droit fémoral
    s.ell('LeftUpperLeg', (0.155, 0.75, 0.020), (0.060, 0.15, 0.075), fwd=(0, -1, 0.1))              # vaste externe
    s.ell('LeftUpperLeg', (0.075, 0.66, 0.015), (0.050, 0.09, 0.055), fwd=(0, -1, 0.1))              # vaste interne
    s.cone('LeftLowerLeg', L('LowerLeg'), L('Foot') + (0, 0.04, 0), 0.085, 0.055)
    s.ell('LeftLowerLeg', (0.130, 0.39, -0.045), (0.050, 0.12, 0.058))                               # jumeau externe
    s.ell('LeftLowerLeg', (0.080, 0.40, -0.045), (0.055, 0.12, 0.060))                               # jumeau interne
    s.ell('LeftFoot', L('Foot') + (0, -0.04, 0.05), (0.070, 0.045, 0.120))
    s.cone('LeftToes', L('Toes') + (0, 0.0, -0.03), L('Toes') + (0, -0.005, 0.06), 0.050, 0.035)
    s.mirror(SW)
    detail = lambda X: 0.004 * SC.noise3(X, 0.05, seed=81, octaves=3) + 0.0015 * SC.noise3(X, 0.012, seed=82, octaves=2)
    m = s.mesh(res=0.012, detail=detail, smooth=4)
    s.skin(m, sk, kw=0.03)
    SC.box_uv(m, 0.40)
    return m


def horns(sk):
    """Cornes de bélier : jaillissent des tempes, s'écartent, montent puis se recourbent vers l'avant ; anneaux de croissance."""
    out = []
    t = np.linspace(0, 1, 40)
    for sx in (1, -1):
        base = np.array([0.080 * sx, 1.700, 0.080])
        pts = np.stack([base[0] + sx * (0.18 * np.sin(t * 2.2) + 0.03 * t), base[1] + 0.26 * np.sin(t * 1.9) - 0.02 * t, base[2] - 0.10 * np.sin(t * 3.0) + 0.16 * t ** 2], 1)
        r = 0.060 * (1 - t) ** 0.8 + 0.004
        r = r * (1 + 0.06 * np.sin(t * 70))
        lo = Loft(pts, r, r * 0.85, p=2.2, segs=16, cap0=1, cap1=1)
        m = lo.mesh(0.15); rigid(m, sk['Head']); out.append(m)
    return concat(out)


def face_details(sk, m):
    from scipy.spatial import cKDTree
    tree = cKDTree(m.V); N = vertex_normals(m)
    def on(p, sink=0.0):
        i = tree.query(p)[1]; return m.V[i] - N[i] * sink, N[i]
    parts = []
    eyes = []
    for sx in (1, -1):
        q, n = on(np.array([0.042 * sx, 1.660, 0.20]), 0.006)
        eyes.append(SC.Sculpt(k=0.003).ell('Head', q, (0.016, 0.008, 0.010), fwd=unit(n + (0, 0, 0.5))).mesh(res=0.0015, smooth=2))
    teeth = []
    for sx in (1, -1):   # défenses inférieures et crocs
        q, n = on(np.array([0.048 * sx, 1.565, 0.225]), -0.002)
        teeth.append(Loft(spline([q, q + (0.006 * sx, 0.035, 0.006), q + (0.002 * sx, 0.060, -0.004)], 10), curve([(0, .011), (1, .0012)], 10), curve([(0, .010), (1, .001)], 10), p=2, segs=10, cap1=1).mesh(0.05))
        for k, dx in enumerate((0.012, 0.024)):
            q, n = on(np.array([dx * sx, 1.598, 0.235]), 0.004)
            teeth.append(Loft(spline([q, q + (0, -0.014, 0.003), q + (0, -0.024, 0.004)], 6), curve([(0, .005), (1, .0008)], 6), curve([(0, .005), (1, .0008)], 6), p=2, segs=8, cap1=1).mesh(0.03))
    e = concat(eyes); rigid(e, sk['Head']); tt = concat(teeth); rigid(tt, sk['Head'])
    # griffes : au bout de chaque doigt et orteil
    cl = []
    for fn, pts in H.FINGERS.items():
        tip = np.array(pts[3]) + (np.array(pts[3]) - np.array(pts[2])) * 0.25
        d = unit(np.array(pts[3]) - np.array(pts[2]))
        c = Loft(spline([tip - d * 0.01, tip + d * 0.025 + (0, -0.012, 0), tip + d * 0.040 + (0, -0.032, 0)], 8), curve([(0, .010), (1, .0012)], 8), curve([(0, .008), (1, .001)], 8), p=2, segs=8, cap1=1).mesh(0.03)
        rigid(c, sk[f'Left{fn}Distal']); cl.append(('Left', c))
    toes = []
    for dx in (-0.03, 0.0, 0.03):
        b = L('Toes') + (dx, -0.01, 0.075)
        toes.append(Loft(spline([b, b + (0, -0.006, 0.02), b + (0, -0.035, 0.035)], 8), curve([(0, .013), (1, .0015)], 8), curve([(0, .012), (1, .0012)], 8), p=2, segs=8, cap1=1).mesh(0.03))
    tm = concat(toes); rigid(tm, sk['LeftToes'])
    return e, tt, [c for _, c in cl], tm


def loincloth(sk):
    """Ceinture à plaques de fer et pagne de cuir avant / arrière."""
    out = []
    pts = np.array([[0.205 * np.sin(a), 1.035, 0.15 * np.cos(a) - 0.005] for a in np.linspace(0, 2 * np.pi, 48)])
    belt = Loft(pts, 0.035, 0.012, p=4, segs=8, ref=(0, 1, 0)).mesh(0.1)
    rigid(belt, sk['Hips']); out.append(('ceinture', belt, 'DB_Leather'))
    for k, a in enumerate(np.linspace(-1.0, 1.0, 5)):
        c = np.array([0.215 * np.sin(a), 1.035, 0.16 * np.cos(a)])
        n = unit(np.array([np.sin(a), 0, np.cos(a) * 0.7]))
        R = SC.rot_from(n)
        pl = SC.Sculpt(k=0.004).ell('Hips', c + n * 0.008, (0.034, 0.042, 0.008), fwd=n).mesh(res=0.003, smooth=2)
        rigid(pl, sk['Hips']); out.append((f'ceinture_plaque{k}', pl, 'DB_Iron'))
    for front, sgn in ((True, 1), (False, -1)):
        u = np.linspace(-1, 1, 12); v = np.linspace(0, 1, 20)
        U, Vv = np.meshgrid(u, v)
        x = U * (0.13 - 0.03 * Vv); y = 1.01 - Vv * 0.48
        z = sgn * (0.165 + 0.02 * Vv + 0.015 * np.cos(U * 1.5))
        tat = 0.06 * np.abs(np.sin(U * 9 + 1.3)) * (Vv > 0.85)
        y = y + tat
        P = np.stack([x, y, z], -1)
        m = Mesh(P.reshape(-1, 3), grid_faces(len(v), len(u)), np.c_[U.ravel() * 0.5 + 0.5, Vv.ravel()])
        if (vertex_normals(m)[:, 2].mean() * sgn) < 0: m.F = m.F[:, ::-1].copy()
        w = smoothstep((0.95 - m.V[:, 1]) / 0.4) * 0.7
        m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.zeros((len(m.V), 4))
        m.J[:, 0] = sk['Hips']; m.W[:, 0] = 1 - w; m.J[:, 1] = sk['LeftUpperLeg']; m.W[:, 1] = w * 0.5; m.J[:, 2] = sk['RightUpperLeg']; m.W[:, 2] = w * 0.5
        out.append(('pagne_avant' if front else 'pagne_arriere', m, 'DB_Leather'))
    return out


def cleaver(name='LavaCleaver'):
    """Fendoir de lave : large lame courbe à dos dentelé et tranchant ébréché, âme fissurée incandescente, poignée
    entourée de cuir, pommeau à pointe. Prise à l'origine, lame vers +Y."""
    a = Asset(name)
    n = 60; t = np.linspace(0, 1, n)
    y = 0.16 + t * 1.15
    w_front = 0.075 + 0.075 * np.sin(np.pi * np.clip(t * 1.1, 0, 1)) ** 0.8 - 0.07 * t ** 6     # tranchant
    w_back = 0.030 + 0.020 * np.sin(np.pi * t) + 0.018 * (np.sin(t * 60) > 0.3) * (t > 0.15) * (t < 0.9)   # dos à dents
    curve_x = 0.08 * t ** 2
    rows = 9
    u = np.linspace(-1, 1, rows)
    pts = []
    for i in range(n):
        xs = curve_x[i] + np.where(u >= 0, u * w_front[i], u * w_back[i])
        chip = 0.008 * (np.sin(i * 1.7) > 0.6) * (u > 0.85)
        xs = xs - chip
        th = 0.022 * (1 - np.abs(u) ** 1.5) * (1 - 0.6 * t[i]) + 0.0015
        pts.append(np.stack([xs, np.full(rows, y[i]), th], 1))
    P = np.array(pts)                       # (n, rows, 3)
    front = P.copy(); back = P.copy(); back[..., 2] *= -1
    def sheet(Q, flip):
        m = Mesh(Q.reshape(-1, 3), grid_faces(n, rows), np.c_[Q[..., 0].ravel() / 0.4 + 0.5, Q[..., 1].ravel() / 0.4])
        nz = vertex_normals(m)[:, 2].mean()
        if (nz < 0) != flip: m.F = m.F[:, ::-1].copy()
        return m
    a.add('lame', concat([sheet(front, False), sheet(back, True)]), 'DB_Blade')
    a.add('garde', Loft(spline([(-0.07, 0.15, 0), (0.0, 0.16, 0), (0.12, 0.15, 0)], 10), 0.030, 0.026, p=3.0, segs=12, ref=(0, 1, 0), cap0=1, cap1=1).mesh(0.1), 'DB_Iron')
    a.add('poignee', Loft(spline([(0, -0.28, 0), (0, 0.14, 0)], 12), 0.024, 0.024, p=2.2, segs=12).mesh(0.05), 'DB_Leather')
    a.add('pommeau', Loft(spline([(0, -0.28, 0), (0, -0.33, 0), (0, -0.40, 0)], 8), curve([(0, .038), (.5, .030), (1, .004)], 8), curve([(0, .038), (.5, .030), (1, .004)], 8), p=2.4, segs=10, cap1=1).mesh(0.05), 'DB_Iron')
    return a


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk)
    m = body(sk)
    a.add('corps', m, 'DB_Skin')
    a.add('cornes', horns(sk), 'DB_Horn')
    e, tt, claws, toes = face_details(sk, m)
    a.add('yeux', e, 'DB_Eye'); a.add('crocs', tt, 'DB_Tooth')
    for k, c in enumerate(claws): a.add(f'griffe{k}', c, 'DB_Claw'); a.add(f'griffe{k}_R', mirror(c, sw), 'DB_Claw')
    a.add('griffes_pied', toes, 'DB_Claw'); a.add('griffes_pied_R', mirror(toes, sw), 'DB_Claw')
    for n, mm, mat in loincloth(sk): a.add(n, mm, mat)
    anims.build(sk, a)
    return a, {'LavaCleaver': cleaver()}, anims.CLIPS
