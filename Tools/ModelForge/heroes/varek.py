"""Varek — le Chasseur des cendres : rôdeur encapuchonné, cuir bouilli et cuir souple, cape verte déchiquetée,
carquois et arc dans le dos, arbalète en main."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, face as FC, garment as GM
from forge.geo import Loft, Mesh, spline, curve, tube, rivet, plate, plate_edge, concat, rigid, unit, mirror, smoothstep, blend_bones, rivets_on
from knight import body as BD, cape, anims, materials as KM
from knight.anims import Style, Pose, key, build_qclip, BONES, aim_wrist
from knight import anims as AN
from heroes.common import mh_human
from heroes import modular as MOD
from heroes import skill_anims as SA
from forge import humanparts as HP
from forge import mhbase as MB

NAME = 'Varek'
HEAD_VIEW = (0, 1.68, 0.06)
RECIPES = {
    'VR_Leather':  (lambda S: KM.leather(S, tone=(0.13, 0.085, 0.05)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'VR_Dark':     (lambda S: KM.gambeson(S, seed=55, color=(0.07, 0.06, 0.045)), 1024, 256, dict(metallic=0.0, smoothness=0.4)),
    'VR_Scale':    (lambda S: KM.scale_fabric(S, color=(0.20, 0.21, 0.10), vein=(0.50, 0.42, 0.19)), 1024, 512, dict(metallic=1.0, smoothness=1.0)),
    'VR_Tooled':   (lambda S: KM.tooled_leather(S, tone=(0.15, 0.095, 0.055)), 1024, 512, dict(metallic=0.25, smoothness=0.9)),
    'VR_Feather':  (lambda S: KM.gambeson(S, seed=58, color=(0.24, 0.17, 0.11)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'VR_Cloak':    (lambda W, H_: KM.cloak(W, H_, seed=64, color=(0.15, 0.17, 0.08), thread_color=(0.34, 0.33, 0.17), mud_amt=1.2), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'VR_Bronze':   (lambda S: KM.brass(S, seed=23, gold=(0.15, 0.10, 0.06), dark=(0.50, 0.36, 0.17)), 1024, 256, dict(metallic=0.8, smoothness=0.8)),
    'VR_Gold':     (lambda S: KM.brass(S, seed=24, gold=(0.62, 0.45, 0.22), dark=(0.10, 0.07, 0.04)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'VR_Metal':    (lambda S: KM.steel(S, seed=13, tone=(0.30, 0.29, 0.28), polish=0.5), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':     (KM.wood, 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'AshCrossbow')]
ty = lambda y: (y - 0.86) / 0.70


def crossbow():
    a = Asset('AshCrossbow')
    # fût : de la crosse (-Y) à l'arc (+Y), poignée à l'origine
    KY = [-0.32, -0.14, 0.0, 0.25, 0.43]; KF = [.048, .032, .022, .019, .019]; KS = [.020, .017, .016, .016, .016]; KZ = [-0.04, -0.032, 0.0, 0.012, 0.014]
    P = np.linspace(-0.32, 0.43, 30)
    st = Loft(np.stack([np.zeros(30), P, np.interp(P, KY, KZ)], 1), np.interp(P, KY, KF), np.interp(P, KY, KS), p=3.2, segs=20, ref=(0, 0, 1), cap0=2, cap1=2)
    a.add('fut', st.mesh(0.2), 'WP_Wood')
    # garnitures de bronze gravé : plaque de crosse, manchons, tête de l'arbrier
    for k, (y0, y1) in enumerate([(-0.33, -0.25), (-0.06, -0.02), (0.16, 0.22), (0.36, 0.45)]):
        P = np.linspace(y0, y1, 4)
        m = Loft(np.stack([np.zeros(4), P, np.interp(P, KY, KZ)], 1), np.interp(P, KY, KF) * 1.12 + 0.002, np.interp(P, KY, KS) * 1.15 + 0.002,
                 p=3.2, segs=20, ref=(0, 0, 1), cap0=1, cap1=1)
        a.add(f'garniture{k}', m.mesh(0.1), 'VR_Bronze')
    a.add('rail', Loft(spline([(0, 0.05, 0.03), (0, 0.42, 0.031)], 10), 0.003, 0.008, p=4, segs=8, ref=(0, 0, 1)).mesh(0.1), 'VR_Metal')
    # arbrier recourbé (corne et bronze) : branches qui reculent puis dont les pointes se retournent vers l'avant
    X = np.linspace(-0.33, 0.33, 41); ax = np.abs(X) / 0.33
    Y = 0.405 - 0.075 * ax ** 1.6 + 0.085 * smoothstep((ax - 0.72) / 0.28)
    Z = 0.03 + 0.012 * ax ** 2
    rf = 0.006 + 0.010 * (1 - ax) ** 0.8; rs = 0.010 + 0.012 * (1 - ax) ** 0.8
    a.add('arc', Loft(np.stack([X, Y, Z], 1), rf, rs, p=2.5, segs=12, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.1), 'VR_Bronze')
    for sx in (-1, 1):   # embouts dorés en crochet
        tp = np.array([0.33 * sx, Y[-1], Z[-1]])
        a.add(f'embout{sx}', Loft(spline([tp - (0.02 * sx, 0.012, 0), tp + (0.004 * sx, 0.03, 0)], 6), curve([(0, .007), (1, .001)], 6), curve([(0, .009), (1, .001)], 6), p=2, segs=8, ref=(0, 0, 1), cap1=1).mesh(0.05), 'VR_Gold')
    yk = float(np.interp(0.93, ax[20:], Y[20:]))
    a.add('corde', tube([(-0.307, yk, 0.03), (0, 0.10, 0.034), (0.307, yk, 0.03)], 0.0018, segs=5), 'VR_Dark')
    a.add('carreau', Loft(spline([(0, 0.10, 0.04), (0, 0.44, 0.04)], 8), 0.004, 0.004, p=2, segs=8, cap1=2).mesh(0.1), 'WP_Wood')
    a.add('pointe', Loft(spline([(0, 0.44, 0.04), (0, 0.49, 0.04)], 5), curve([(0, .009), (1, .001)], 5), curve([(0, .004), (1, .001)], 5), p=2, segs=8, cap1=1).mesh(0.05), 'VR_Metal')
    a.add('etrier', tube([(-0.035, 0.44, 0.03), (-0.03, 0.51, 0.03), (0.03, 0.51, 0.03), (0.035, 0.44, 0.03)], 0.004, segs=6), 'VR_Bronze')
    a.add('detente', Loft(spline([(0, -0.02, -0.02), (0, -0.04, -0.05)], 5), 0.003, 0.004, p=2, segs=6, cap1=1).mesh(0.05), 'VR_Metal')
    return a


class CrossbowStyle(Style):
    name = 'crossbow'
    def guard(self, p):
        p.arm('R', down=62, fwd=24, twist=-5, elbow=70, wrist=(-10, 0, -60))       # arbalète tenue basse, pointée vers l'avant
        p.arm('L', down=60, fwd=34, twist=30, elbow=75, wrist=(0, 0, 20))
        p.fist('R'); p.fist('L', 0.6)
        if AN._SK is not None: aim_wrist(AN._SK, p, 'R', (0, -0.3, 1), up=((0, 0, 1), (0, 1, 0)))
        return p

    def gait_arms(self, p, w, run, arm_amp):
        p.arm('R', down=62, fwd=24 + 0.3 * arm_amp * np.sin(w + np.pi), twist=-5, elbow=70 + (15 if run else 0), wrist=(-10, 0, -60))
        if AN._SK is not None: aim_wrist(AN._SK, p, 'R', (0, -0.3, 1), up=((0, 0, 1), (0, 1, 0)))
        p.arm('L', down=66, fwd=14 + arm_amp * np.sin(w), twist=20, elbow=35 + (35 if run else 0))

    def attack(self, sk):
        def P(rd, rf, rel, wr, ld, lf, lel, sp, tw, nod):
            p = Pose().spine(sp, tw, 0).head(nod, -tw * 0.8); self.guard(p)
            p.arm('R', down=rd, fwd=rf, twist=-10, elbow=rel, wrist=wr); p.arm('L', down=ld, fwd=lf, twist=40, elbow=lel, wrist=(0, 0, 20))
            p.leg('L', thigh=12, knee=16, spread=6, turn=-10); p.leg('R', thigh=-8, knee=12, spread=-8, turn=15)
            aim_wrist(sk, p, 'R', (0, -0.3 if rf < 50 else 0.05, 1), up=((0, 0, 1), (0, 1, 0)))
            return p
        poses = [(0.0, P(62, 24, 70, (-10, 0, -60), 60, 34, 75, 3, -6, -2)),
                 (0.25, P(20, 75, 95, (0, 0, -85), 25, 80, 60, 6, 10, -8)),       # mise en joue à l'épaule
                 (0.45, P(20, 75, 95, (0, 0, -85), 25, 80, 60, 6, 10, -8)),
                 (0.52, P(14, 85, 105, (10, 0, -85), 20, 88, 65, -2, 14, -14)),   # recul du tir
                 (0.90, P(62, 24, 70, (-10, 0, -60), 60, 34, 75, 3, -6, -2))]
        ks = [key(t, p, hips=(0, -0.05, 0), hips_rot=(0, 12, 0)) for t, p in poses]
        return build_qclip('Attack', ks, BONES(sk))

    def block(self, sk):
        ks = []
        for t, br in ((0, 0), (0.6, 1), (1.2, 0)):
            p = Pose().spine(14 + br, 10, 0).head(-10, -8)
            p.arm('L', down=20, fwd=70, twist=60, elbow=115, wrist=(0, 0, 0))       # avant-bras gauche levé devant le visage
            p.arm('R', down=55, fwd=20, twist=-5, elbow=80, wrist=(-10, 0, -60))
            p.fist('R'); p.fist('L', 1.0)
            aim_wrist(sk, p, 'R', (0, -0.3, 1), up=((0, 0, 1), (0, 1, 0)))
            p.leg('L', thigh=24, knee=34, spread=8, turn=-8); p.leg('R', thigh=-10, knee=26, spread=-8, turn=16)
            ks.append(key(t, p, hips=(0, -0.11, 0.02), hips_rot=(0, -12, 0)))
        return build_qclip('Block', ks, BONES(sk), loop=True)


def hood(a, add, body, marks, sk, cloth='VR_Cloak', lining='VR_Dark', depth=1.0, loose=0.0):
    """Capuche de rôdeur : surface lisse paramétrée autour de la vraie tête MakeHuman (enveloppe azimut × élévation),
    ouverture du visage à bord net roulé, sommet en pointe douce, bord avant avancé → visage dans l'ombre."""
    from scipy.ndimage import gaussian_filter, grey_dilation
    from forge.geo import vertex_normals
    V = body.V
    eye = (np.asarray(marks['eye_L']) + np.asarray(marks['eye_R'])) / 2
    ctr = np.array([0, eye[1] + 0.005, eye[2] - 0.090])
    hm = HP.bone_mask(body, sk, ['Head', 'Neck'], 0.3) & (V[:, 1] > 1.47)
    D = V[hm] - ctr; R = np.linalg.norm(D, axis=1)
    az = np.arctan2(D[:, 0], D[:, 2]) % (2 * np.pi); el = np.arcsin(np.clip(D[:, 1] / R, -1, 1))
    NA, NE = 96, 48
    E0, E1 = np.radians(-72), np.radians(90)
    ia = np.clip((az / (2 * np.pi) * NA).astype(int), 0, NA - 1); ie = np.clip(((el - E0) / (E1 - E0) * NE).astype(int), 0, NE - 1)
    env = np.zeros((NE, NA)); np.maximum.at(env, (ie, ia), R)
    have = env > 0
    for _ in range(6):
        w = gaussian_filter(have.astype(float), 2, mode=('nearest', 'wrap')); v = gaussian_filter(env * have, 2, mode=('nearest', 'wrap'))
        env = np.where(have, env, v / np.maximum(w, 1e-6)); have = have | (w > 1e-3)
    env = gaussian_filter(grey_dilation(env, size=(3, 5), mode='wrap'), (1.6, 2.4), mode=('nearest', 'wrap'))
    def r_env(A, Ev):
        fa = (A % (2 * np.pi)) / (2 * np.pi) * NA - 0.5; fe = np.clip((Ev - E0) / (E1 - E0) * NE - 0.5, 0, NE - 1.001)
        a0 = np.floor(fa).astype(int); e0 = np.floor(fe).astype(int); ta = fa - a0; te = fe - e0
        g = lambda i, j: env[np.clip(i, 0, NE - 1), j % NA]
        return (g(e0, a0) * (1 - ta) * (1 - te) + g(e0, a0 + 1) * ta * (1 - te) + g(e0 + 1, a0) * (1 - ta) * te + g(e0 + 1, a0 + 1) * ta * te)
    deg = np.radians
    def phi0(Ev):
        e = np.degrees(Ev)
        return deg(48) * np.where(e < -8, 1.0, np.sqrt(np.clip((18 - e) / 26, 0, 1)))
    nr, nc = 46, 85
    Ev = np.linspace(E0, deg(88), nr)
    S = np.linspace(0, 1, nc)
    EE, SS = np.meshgrid(Ev, S, indexing='ij')
    P0 = phi0(EE); AA = P0 + SS * (2 * np.pi - 2 * P0)
    dirs = np.stack([np.sin(AA) * np.cos(EE), np.sin(EE), np.cos(AA) * np.cos(EE)], -1)
    up = smoothstep((EE + deg(10)) / deg(80)); back = smoothstep(-np.cos(AA) / 0.6)
    tw = smoothstep((EE - deg(55)) / deg(33))
    # plis verticaux doux dans le tissu (surtout derrière et sur les côtés)
    folds = 0.006 * np.sin(AA * 7 + 0.8) * smoothstep((deg(40) - EE) / deg(60)) * (1 - smoothstep((deg(40) - np.abs(np.pi - AA)) / deg(40)) * 0)
    rr = r_env(AA, EE) + 0.034 + loose + 0.010 * up + 0.020 * back * (1 - tw) + folds
    G = ctr + dirs * rr[..., None]
    pk = smoothstep((EE - deg(35)) / deg(55)) * ((0.4 + 0.6 * back) * (1 - tw) + 0.75 * tw)
    G = G + np.stack([np.zeros_like(pk), -0.004 * pk ** 1.5, -0.030 * pk ** 1.3], -1)   # couronne arrondie, léger tombé du tissu vers l'arrière
    dfront = np.minimum(AA, 2 * np.pi - AA) - P0
    near = smoothstep((deg(28) - dfront) / deg(28)) * smoothstep((EE + deg(50)) / deg(30))
    hi = smoothstep((EE + deg(5)) / deg(30)) * smoothstep((deg(58) - EE) / deg(25))
    G = G + np.stack([-0.22 * (G[..., 0] - ctr[0]) * near * hi, -0.030 * near * hi, (0.030 + 0.085 * hi * depth) * near], -1)   # bord avancé : visage dans l'ombre
    # pôle : les dernières rangées convergent en douceur vers un sommet unique (pas d'épine)
    pole = G[-1].mean(0)
    for k in range(1, 7):
        f = smoothstep(np.array((k - 0.0) / 6.0)) ; G[-k] = G[-k] * (1 - (1 - f) * 0.9) + pole * (1 - f) * 0.9
    idx = np.arange(nr * nc).reshape(nr, nc)
    F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, :-1].ravel()], 1),
                        np.stack([idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()], 1)])
    UV = np.stack([0.08 + SS.ravel() * 0.84, 0.30 + (EE.ravel() - E0) / (E1 - E0) * 0.65], 1)   # hors du galon brodé de la texture de cape
    outer = Mesh(G.reshape(-1, 3), F, UV)
    n = vertex_normals(outer)
    if (n * (outer.V - ctr)).sum(1).mean() < 0: outer.F = outer.F[:, ::-1].copy(); n = vertex_normals(outer)
    inner = Mesh(outer.V - n * 0.006, outer.F[:, ::-1].copy(), UV.copy())
    nV = len(outer.V); rim = []
    op = np.where(P0[:, 0] > 1e-3)[0]
    for line in (idx[op, 0], idx[op, -1], idx[0, :]):
        for i, j in zip(line[:-1], line[1:]): rim += [(i, j, j + nV), (i, j + nV, i + nV)]
    rm = Mesh(np.vstack([outer.V, inner.V]), np.array(rim), np.vstack([UV, UV]))
    fn = np.cross(rm.V[rm.F[:, 1]] - rm.V[rm.F[:, 0]], rm.V[rm.F[:, 2]] - rm.V[rm.F[:, 0]])
    cen = rm.V[rm.F].mean(1); flip = (fn * (cen - ctr - np.array([0, 0, 0.12]) * (cen[:, 2:3] > ctr[2]))).sum(1) < 0
    rm.F[flip] = rm.F[flip][:, ::-1]
    m = concat([outer, rm])
    blend_bones(m, sk['Head'], sk['Neck'], smoothstep((1.60 - m.V[:, 1]) / 0.06))
    add('capuche', m, cloth)
    blend_bones(inner, sk['Head'], sk['Neck'], smoothstep((1.60 - inner.V[:, 1]) / 0.06))
    add('capuche_doublure', inner, lining)   # intérieur sombre : visage dans l'ombre de la capuche
    # bord avant roulé (repli épais du tissu) le long de l'ouverture du visage
    rows = np.where(P0[:, 0] > 1e-3)[0]; rows = np.r_[rows, rows[-1] + 1]          # le repli passe par la pointe du front (ferme l'encoche)
    line = np.concatenate([idx[rows[::-1], 0][::-1], idx[rows[::-1], -1]])
    G2 = outer.V; pts = G2[line] - n[line] * 0.003
    roll = tube(pts, 0.0065, segs=10, tile=0.08); blend_bones(roll, sk['Head'], sk['Neck'], smoothstep((1.60 - roll.V[:, 1]) / 0.06))
    add('capuche_repli', roll, cloth)
    # col / pèlerine plissée qui retombe en vagues sur les épaules
    path = spline([(0, 1.61, -0.02), (0, 1.54, -0.024), (0, 1.45, -0.026)], 20)
    lo = Loft(path, curve([(0, .090), (1, .145)], 20), curve([(0, .094), (1, .20)], 20), p=2.3, segs=120)
    jag = lambda q: 1.0 - 0.05 * np.abs(np.sin(q * np.pi * 7)) ** 0.7 - 0.30 * np.cos(np.pi * q) ** 2
    pm = plate(lo, -0.5, 0.5, 0.0, jag, off=lambda da, tr, qa, qt: 0.012 + 0.008 * qt ** 1.2 * (0.5 + 0.5 * np.sin(qa * 2 * np.pi * 9)), thick=0.005, bevel=0.008, na=160, nt=18, tile=0.3)
    y = pm.V[:, 1]; blend_bones(pm, sk['Neck'], sk['UpperChest'], smoothstep((1.56 - y) / 0.08)); add('pelerine', pm, cloth)


def build(bank, female=False):
    from forge import armorparts as AP
    sk = H.build(); a = Asset(NAME + ('_F' if female else '_M'), sk); sw = H.swap_table(sk)
    def add(n, m, mat, both=False):
        a.add(n, m, mat)
        if both: a.add(n + '_R', mirror(m, sw), mat)
    body, marks = MOD.base(sk, a, bank, 'VRF' if female else 'VRM', female=female, look=dict(hair='braid01', hair_tint=(0.10, 0.07, 0.05), skin_kw=dict(pubic=0.45, dirt=0.4)) if female else dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.09, 0.065, 0.045),
                                                     skin_kw=dict(pubic=0.75, pubic_col=(0.07, 0.05, 0.035), dirt=0.5, scars=[((-0.035, 1.70, 0.10), (0.3, -1, 0.0), 0.045)])))
    V = body.V
    W = lambda m: HP.weights_from(body, m)
    def sh(name, vmask, off, thick, mat, uv=10.0, smooth=0, restore=1.0, inflate=0.0):
        fm = HP.region(body, vmask)
        if fm.sum() == 0: print('  région vide :', name, vmask.sum()); return
        add(name, HP.shell(body, fm, offset=off, thick=thick, uv_scale=uv, smooth=smooth, restore=restore, inflate=inflate), mat)
    upper = HP.bone_mask(body, sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'LowerArm', 'Neck'], 0.3)
    legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
    hands = HP.bone_mask(body, sk, ['Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little'], 0.3)
    larm = HP.bone_mask(body, sk, ['LowerArm'], 0.5)
    uarm = HP.bone_mask(body, sk, ['UpperArm'], 0.5)
    ax = np.abs(V[:, 0])

    # ---- tunique en étoffe à écailles de feuilles, pantalon de cuir repoussé, bottes, gants
    sh('tunique', upper & (V[:, 1] > 0.80) & (V[:, 1] < 1.57) & (ax < 0.56), 0.006, 0.003, 'VR_Scale', 9, smooth=6)
    sh('pantalon', (legs | HP.bone_mask(body, sk, ['Hips'], 0.3)) & (V[:, 1] < 1.02) & (V[:, 1] > 0.36), 0.005, 0.003, 'VR_Leather', 8, smooth=6)
    sh('bottes', legs & (V[:, 1] < 0.50) & (V[:, 1] > 0.085), 0.007, 0.005, 'VR_Leather', 8, smooth=120, restore=0.002, inflate=0.006)
    for sd in ('Left', 'Right'): add('pied_botte_' + sd, HP.boot_last(body, sk, sd, margin=0.012, sole=0.014), 'VR_Leather')
    sh('bottes_revers', legs & (V[:, 1] < 0.53) & (V[:, 1] > 0.45), 0.016, 0.005, 'VR_Leather', 8, smooth=10)
    for y0 in (0.19, 0.30):   # sangles de bottes
        sh(f'botte_sangle{y0}', HP.bone_mask(body, sk, ['LowerLeg', 'Foot'], 0.3) & (V[:, 1] > y0) & (V[:, 1] < y0 + 0.05), 0.016, 0.004, 'VR_Dark', 6, smooth=6)
        for sx in (1, -1):
            q, N_, _ = HP.surface_point(body, (0.16 * sx, y0 + 0.013, -0.01), 0.022)
            add(f'botte_boucle{y0}{sx}', W(rivet(q, N_, r=0.010, h=0.004, segs=4, rings=2)), 'VR_Gold')
    sh('gants', hands | (larm & (ax > 0.69)), 0.0016, 0.0015, 'VR_Leather', 16)

    # ---- bras : spalières de cuir repoussé, brassards de bronze gravé à ailerons
    sh('spaliere', uarm & (ax > 0.24) & (ax < 0.43), 0.009, 0.004, 'VR_Tooled', 7, smooth=6, inflate=0.002)
    for x0 in (0.24, 0.41):
        sh(f'spaliere_bord{x0}', uarm & (ax > x0) & (ax < x0 + 0.022), 0.013, 0.003, 'VR_Gold', 6, smooth=3)
    sh('brassards', larm & (ax > 0.50) & (ax < 0.70), 0.010, 0.005, 'VR_Bronze', 8, smooth=8, inflate=0.004)
    for k, (x0, x1) in enumerate([(0.495, 0.527), (0.668, 0.70)]):
        sh(f'brassard_bord{k}', larm & (ax > x0) & (ax < x1), 0.016, 0.004, 'VR_Gold', 6, smooth=4)
    for k, (x, ln) in enumerate([(0.545, 0.075), (0.595, 0.065), (0.645, 0.050)]):
        q, N_, _ = HP.surface_point(body, (x, 1.52, -0.05), 0.016)
        fin = AP.leaf_spike(q - N_ * 0.004, unit(np.array([-0.75, 0.55, -0.35])), (0, 0, 1), length=ln, width=0.016, thick=0.004, curl=0.25)
        rigid(fin, sk['LeftLowerArm']); add(f'brassard_aileron{k}', fin, 'VR_Bronze', both=True)

    # ---- jambes : cuissards en écu, genouillères en écu pointé, jambières à deux plaques
    for side, sx in (('Left', 1), ('Right', -1)):
        def plate_at(name, p, off, Wd, Hh, top, tip, dome, ridge, wrap, mat='VR_Bronze', trim=0.0035, flare=0.0):
            q, N_, _ = HP.surface_point(body, p, off)
            pl, tr = AP.shield_plate(q, N_, (0, 1, 0), W=Wd, H=Hh, top=top, tip=tip, dome=dome, ridge=ridge, wrap=wrap, trim=trim, flare=flare)
            add(f'{name}_{side}', W(pl), mat)
            if tr is not None: add(f'{name}_bord_{side}', W(tr), 'VR_Gold')
        plate_at('cuissard', (0.105 * sx, 0.76, 0.09), 0.016, 0.068, 0.24, 0.5, 1.0, 0.010, 0.004, 0.085, mat='VR_Tooled')
        plate_at('genouillere', (0.10 * sx, 0.545, 0.07), 0.026, 0.070, 0.18, 0.42, 1.0, 0.036, 0.010, 0.070, flare=0.008)
        plate_at('jambiere_haut', (0.10 * sx, 0.375, 0.06), 0.022, 0.060, 0.15, 0.5, 0.0, 0.014, 0.006, 0.055)
        plate_at('jambiere_bas', (0.10 * sx, 0.245, 0.05), 0.026, 0.058, 0.16, 0.5, 0.0, 0.014, 0.006, 0.055)
        q, N_, _ = HP.surface_point(body, (0.10 * sx, 0.55, 0.07), 0.052)
        add(f'genou_clou_{side}', W(rivet(q, N_, r=0.011, h=0.008, segs=10, rings=4)), 'VR_Gold')
        # ailette latérale de la genouillère
        q, N_, _ = HP.surface_point(body, (0.155 * sx, 0.54, 0.03), 0.022)
        pl, tr = AP.shield_plate(q, N_, (0, 1, 0), W=0.030, H=0.09, top=0.5, tip=1.0, dome=0.008, ridge=0.003, wrap=0.06)
        add(f'genou_aile_{side}', W(pl), 'VR_Bronze'); add(f'genou_aile_bord_{side}', W(tr), 'VR_Gold')

    # ---- poitrine : grand baudrier diagonal à boucle, sangle croisée, anneau
    add('baudrier', HP.strap(body, [(0.15, 1.47, 0.08), (0.06, 1.32, 0.17), (-0.08, 1.13, 0.16), (-0.17, 0.99, 0.06)], 0.013, 0.058, thick=0.004), 'VR_Leather')
    add('baudrier_dos', HP.strap(body, [(0.15, 1.47, -0.06), (0.06, 1.30, -0.16), (-0.08, 1.12, -0.15), (-0.17, 0.99, -0.05)], 0.013, 0.058, thick=0.004), 'VR_Leather')
    add('sangle_croisee', HP.strap(body, [(-0.15, 1.47, 0.08), (-0.06, 1.33, 0.17), (0.05, 1.20, 0.17)], 0.018, 0.030), 'VR_Dark')
    add('sangle_croisee_dos', HP.strap(body, [(-0.15, 1.47, -0.06), (-0.06, 1.30, -0.16), (0.08, 1.12, -0.15), (0.17, 0.99, -0.05)], 0.018, 0.030), 'VR_Dark')
    q, N_, _ = HP.surface_point(body, (0.02, 1.27, 0.2), 0.020)
    pl, tr = AP.shield_plate(q, N_, (0.45, 1, 0), W=0.026, H=0.05, top=0.5, tip=0.0, dome=0.006, ridge=0.0, wrap=0.2, trim=0.003)
    add('baudrier_boucle', W(pl), 'VR_Gold'); add('baudrier_boucle_bord', W(tr), 'VR_Bronze')
    for k, (x, y) in enumerate([(0.11, 1.41), (-0.04, 1.19), (-0.11, 1.09)]):
        q, N_, _ = HP.surface_point(body, (x, y, 0.25), 0.019)
        add(f'baudrier_rivet{k}', W(rivet(q, N_, r=0.006, h=0.003)), 'VR_Gold')

    # ---- double ceinture cloutée, grosse boucle ornée, sacoches
    hips = HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)
    sh('ceinture', (V[:, 1] > 0.945) & (V[:, 1] < 1.02) & hips, 0.017, 0.006, 'VR_Leather', 6)
    tilt = 0.905 - 0.035 * V[:, 0] / 0.2
    sh('ceinture_basse', (V[:, 1] > tilt - 0.016) & (V[:, 1] < tilt + 0.016) & (HP.bone_mask(body, sk, ['Hips', 'UpperLeg'], 0.3)) & (V[:, 1] > 0.82), 0.022, 0.005, 'VR_Dark', 6)
    for k, th in enumerate(np.linspace(-1.9, 1.9, 14)):
        q, N_, _ = HP.surface_point(body, (np.sin(th) * 0.2, 0.982, np.cos(th) * 0.2), 0.024)
        if abs(th) > 0.25: add(f'clou{k}', W(rivet(q, N_, r=0.0055, h=0.004, segs=8, rings=3)), 'VR_Gold')
    q, N_, _ = HP.surface_point(body, (0, 0.982, 0.2), 0.024)
    pl, tr = AP.shield_plate(q, N_, (0, 1, 0), W=0.038, H=0.075, top=0.45, tip=1.0, dome=0.010, ridge=0.004, wrap=0.3, trim=0.004)
    add('boucle', W(pl), 'VR_Bronze'); add('boucle_bord', W(tr), 'VR_Gold')
    add('boucle_gemme', W(rivet(q + N_ * 0.012, N_, r=0.011, h=0.007, segs=12, rings=4)), 'VR_Gold')
    for k, (x, z, w, h, d) in enumerate([(0.14, 0.10, 0.07, 0.08, 0.035), (-0.16, 0.08, 0.06, 0.07, 0.03), (0.17, -0.07, 0.05, 0.09, 0.03)]):
        q, N_, _ = HP.surface_point(body, (x * 1.5, 0.95, z * 1.5), 0.028 + d / 2)
        lo_ = Loft(spline([q + (0, h / 2, 0), q - (0, h / 2, 0)], 6), d / 2, w / 2, p=4.0, segs=16, ref=tuple(N_), cap0=2, cap1=2)
        add(f'sacoche{k}', W(lo_.mesh(0.1)), 'VR_Tooled')

    # ---- pans de tabard en feuilles pointues (devant, côtés, derrière)
    env = AP.hip_envelope(body, (legs | HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)) & (ax < 0.30))
    pans = [(0.0, 0.50, 0.080, 0.030, 1), (0.48, 0.42, 0.070, 0.024, 2), (-0.48, 0.42, 0.070, 0.024, 3),
            (1.15, 0.36, 0.068, 0.022, 4), (-1.15, 0.36, 0.068, 0.022, 5), (np.pi, 0.48, 0.090, 0.030, 6),
            (np.pi - 0.62, 0.42, 0.075, 0.024, 7), (np.pi + 0.62, 0.42, 0.075, 0.024, 8)]
    for th, L, wd, off, sd_ in pans:
        m, edge = AP.lappet(env, th, 0.955, L, wd, off=off, flare=0.05, seed=sd_, notch=0.25)
        for mm, mat in ((m, 'VR_Scale'), (edge, 'VR_Leather')):
            yv = mm.V[:, 1]; f = smoothstep((0.90 - yv) / 0.35) * 0.75
            wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
            mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
            mm.J[:, 0] = sk['Hips']; mm.W[:, 0] = 1 - f
            mm.J[:, 1] = sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
            add(f'pan{sd_}' + ('' if mat == 'VR_Scale' else '_lisere'), mm, mat)

    # ---- épaulières : quatre lames de bronze gravé bordées d'or, lames relevées en pointes de feuilles
    ua = np.array(H.ARM['UpperArm'])
    pal = Loft(spline([ua + (-0.08, 0.035, 0), ua + (0.02, 0.025, 0), ua + (0.16, 0.0, 0)], 20), curve([(0, .102), (1, .078)], 20), curve([(0, .094), (1, .072)], 20), p=2.2, segs=60)
    def armL(m):
        f = smoothstep((m.V[:, 0] - 0.13) / 0.14); blend_bones(m, sk['LeftShoulder'], sk['LeftUpperArm'], f); return m
    for k, (lo, hi) in enumerate([(0.0, 0.42), (0.32, 0.62), (0.52, 0.82), (0.72, 1.0)]):
        m = plate(pal, 0.40, 1.10, lo, hi, off=lambda da, tr, qa, qt, k=k: 0.022 - 0.003 * k + 0.012 * qt ** 2 * (k == 0), thick=0.006, bevel=0.007, na=50, nt=12, dome=0.008, tile=0.3)
        add(f'epauliere{k}', armL(m), 'VR_Bronze', both=True)
        e = plate_edge(pal, 0.42, 1.08, hi - 0.012, 0.022 - 0.003 * k + 0.004, r=0.0045, n=50)
        add(f'epauliere_bord{k}', armL(e), 'VR_Gold', both=True)
    cr = plate(pal, 0.70, 0.80, 0.02, 0.40, off=lambda da, tr, qa, qt: 0.028 + 0.024 * np.sin(np.pi * qa) * (1 - 0.6 * qt), thick=0.004, bevel=0.004, na=12, nt=10, tile=0.2)
    add('epauliere_crete', armL(cr), 'VR_Bronze', both=True)
    for k, (t, ln, aa) in enumerate([(0.05, 0.135, 0.72), (0.19, 0.115, 0.75), (0.32, 0.090, 0.78), (0.08, 0.085, 0.58), (0.45, 0.070, 0.80)]):
        P_, N_, T_, _ = pal.frame(np.array([t]), np.array([aa]))
        P_, N_ = P_[0], unit(N_[0]); tang = unit(np.array([1.0, 0, 0]))
        base = P_ + N_ * 0.030
        sp = AP.leaf_spike(base, unit(N_ * 0.8 - tang * 0.55 + np.array([0, 0, -0.15])), tang, length=ln, width=0.026, thick=0.006, curl=-0.30)
        add(f'epauliere_pointe{k}', armL(sp), 'VR_Bronze', both=True)

    # ---- capuche, cape ample
    hood(a, add, body, marks, sk)
    cape.build(sk, a, mat='VR_Cloak', clasp='VR_Gold', length=1.16, width=1.25, tatter=1.4)

    # ---- carquois garni derrière l'épaule droite, arc dans le dos
    c0 = np.array([-0.09, 1.25, -0.20]); d = unit(np.array([-0.33, 1.0, -0.22]))
    q = Loft(spline([c0 - d * 0.32, c0 + d * 0.20], 12), 0.048, 0.056, p=2.4, segs=24, cap0=2)
    add('carquois', q.mesh(0.2), 'VR_Tooled')
    names = ['carquois']
    for k, t in enumerate((-0.27, -0.05, 0.18)):
        r_ = Loft(spline([c0 + d * (t - 0.012), c0 + d * (t + 0.012)], 3), 0.052, 0.060, p=2.4, segs=24)
        add(f'carquois_bague{k}', r_.mesh(0.1), 'VR_Bronze'); names.append(f'carquois_bague{k}')
    side = unit(np.cross(d, (0, 0, 1)))
    rng = np.random.default_rng(4)
    for k in range(20):
        ang = k * 2.39996; rad = 0.034 * np.sqrt((k + 0.5) / 20)
        o = np.array([np.cos(ang), 0, np.sin(ang)]) * rad
        fan = unit(d + (np.cos(ang) * side + np.sin(ang) * np.array([0, 0, -1.0])) * rad * 4.5)
        base = c0 + d * 0.10 + o; tip = base + fan * (0.27 + rng.uniform(-0.02, 0.03))
        add(f'fleche{k}', Loft(spline([base, tip], 4), 0.0032, 0.0032, p=2, segs=5).mesh(0.1), 'WP_Wood')
        dd = unit(tip - base); r0 = unit(np.cross(dd, side))
        for j in range(3):
            rv = unit(np.cos(j * 2.094 + k) * r0 + np.sin(j * 2.094 + k) * np.cross(dd, r0))
            add(f'empenne{k}_{j}', Loft(spline([tip - dd * 0.10, tip - dd * 0.008], 5), 0.0007, curve([(0, .003), (.35, .012), (1, .007)], 5), p=2, segs=4, ref=tuple(np.cross(dd, rv))).mesh(0.1), 'VR_Feather')
            names.append(f'empenne{k}_{j}')
        names.append(f'fleche{k}')
    bow_pts = [np.array([0.06, 1.18, -0.25]) + np.array([0.24 * s, 0.62 * s, 0]) * 0.9 + np.array([0, 0, -0.06 * (1 - (2 * s) ** 2)]) for s in np.linspace(-0.5, 0.5, 30)]
    add('arc_dos', Loft(np.array(bow_pts), curve([(0, .006), (.5, .014), (1, .006)], 30), curve([(0, .008), (.5, .016), (1, .008)], 30), p=2.3, segs=10, cap0=1, cap1=1).mesh(0.2), 'WP_Wood')
    add('corde_dos', tube([bow_pts[0], bow_pts[-1]], 0.0015, segs=4), 'VR_Dark')
    names += ['arc_dos', 'corde_dos']
    idx = {p[0]: i for i, p in enumerate(a.parts)}
    for n in names: rigid(a.parts[idx[n]][1], sk['UpperChest'])
    MOD.assign(a, RULES)
    MOD.covers(a, COVERS)
    anims.build(sk, a, CrossbowStyle())
    sk_clips = SA.build(sk, a, anims._STYLE, None)
    return a, {'AshCrossbow': crossbow()}, anims.CLIPS + sk_clips


RULES = [(r'^(capuche|pelerine)', 'Capuche'), (r'^cape', 'Cape'), (r'^(carquois|fleche|empenne|arc_dos|corde_dos)', 'Carquois'),
         (r'^tunique', 'Tunique'), (r'^pantalon', 'Pantalon'), (r'^(bottes|pied_botte|botte_)', 'Bottes'), (r'^gants', 'Gants'),
         (r'^spaliere', 'Spalieres'), (r'^brassard', 'Brassards'), (r'^(cuissard|genouillere|genou_|jambiere)', 'Jambieres'),
         (r'^(baudrier|sangle_croisee)', 'Baudrier'), (r'^(ceinture|clou|boucle|sacoche)', 'Ceinture'), (r'^pan', 'Pans'),
         (r'^epauliere', 'Epaulieres')]
COVERS = {'Capuche': ['Cheveux'], 'Tunique': ['Torse', 'Bras', 'SousVetementHaut'], 'Pantalon': ['Cuisses', 'Bassin', 'SousVetementBas', 'Anatomie'],
          'Bottes': ['Pieds', 'Jambes'], 'Gants': ['Mains'], 'Brassards': ['AvantBras'], 'Spalieres': [], 'Jambieres': [], 'Baudrier': [],
          'Ceinture': [], 'Pans': [], 'Epaulieres': [], 'Cape': [], 'Carquois': []}
