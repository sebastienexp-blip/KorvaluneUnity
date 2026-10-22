"""Loup des Ténèbres (monstre de référence) : grand loup corrompu, échine hérissée d'une crinière rouge sombre,
yeux rouges luisants, crocs et griffes d'os. Squelette quadrupède (rig Generic Unity), fourrure en mèches modélisées."""
import numpy as np
from forge.rig import Skeleton, Asset, qeuler
from forge.geo import Loft, Mesh, spline, curve, concat, chain, rigid, blend_bones, smoothstep, unit, mirror, vertex_normals, rivet

FUR, MANE, BONE, EYE, MOUTH = 'WF_Fur', 'WF_Mane', 'WF_Bone', 'WF_Eye', 'WF_Mouth'

J = {
    'Hips': (0, 0.92, -0.45), 'Spine1': (0, 0.95, -0.22), 'Spine2': (0, 0.97, 0.03), 'Chest': (0, 0.99, 0.26),
    'Neck1': (0, 1.04, 0.46), 'Neck2': (0, 1.13, 0.60), 'Head': (0, 1.20, 0.71), 'Jaw': (0, 1.13, 0.77),
    'Tail1': (0, 0.97, -0.62), 'Tail2': (0, 0.90, -0.80), 'Tail3': (0, 0.79, -0.95), 'Tail4': (0, 0.66, -1.07),
    'Scapula': (0.085, 1.00, 0.36), 'UpperArm': (0.135, 0.80, 0.40), 'LowerArm': (0.130, 0.50, 0.34), 'Wrist': (0.128, 0.17, 0.37), 'FrontPaw': (0.128, 0.045, 0.43),
    'Thigh': (0.115, 0.90, -0.46), 'Shin': (0.135, 0.60, -0.30), 'Hock': (0.130, 0.30, -0.56), 'BackPaw': (0.128, 0.050, -0.48),
    'Ear': (0.065, 1.33, 0.69),
}
TIP = {'FrontPaw': (0.128, 0.015, 0.53), 'BackPaw': (0.128, 0.015, -0.38), 'Tail': (0, 0.55, -1.16), 'Head': (0, 1.11, 1.06), 'Ear': (0.085, 1.45, 0.66)}
def mx(p): return (-p[0], p[1], p[2])


def skeleton():
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
    sk.add('Socket_Mouth', 'Jaw', (0, 1.10, 0.98), null=True)
    sk.add('Socket_Chest', 'Chest', (0, 0.95, 0.30), null=True)
    return sk


def swap(sk):
    sw = {}
    for i, n in enumerate(sk.names):
        if n.endswith('_L'): j = sk.names.index(n[:-2] + '_R'); sw[i] = j; sw[j] = i
    return sw


# ------------------------------------------------------------------------------------------- volumes
def body_loft():
    """Tronc de la croupe au cou (a=0 dessus, 0.25 flanc gauche, 0.5 ventre)."""
    path = spline([(0, 0.93, -0.66), (0, 0.94, -0.48), (0, 0.93, -0.22), (0, 0.92, 0.05), (0, 0.93, 0.28), (0, 1.01, 0.47), (0, 1.12, 0.62), (0, 1.19, 0.70)], 46)
    t = np.linspace(0, 1, 46)
    rf = curve([(0, .07), (.08, .15), (.25, .15), (.45, .15), (.62, .22), (.76, .19), (.9, .135), (1, .12)], 46)    # demi-hauteur
    rs = curve([(0, .06), (.08, .135), (.25, .125), (.45, .115), (.62, .155), (.76, .14), (.9, .11), (1, .10)], 46)   # demi-largeur
    of = curve([(0, 0), (.25, -0.01), (.45, 0.02), (.62, -0.06), (.76, -0.04), (.9, -0.01), (1, 0)], 46)              # ventre levé, poitrail profond
    return Loft(path, rf, rs, p=2.2, of=of, segs=48, ref=(0, 1, 0))


def head_loft():
    path = spline([(0, 1.20, 0.66), (0, 1.22, 0.76), (0, 1.19, 0.86), (0, 1.15, 0.96), (0, 1.125, 1.05)], 30)
    rf = curve([(0, .110), (.2, .115), (.45, .085), (.75, .058), (1, .040)], 30)
    rs = curve([(0, .105), (.2, .108), (.45, .070), (.75, .045), (1, .032)], 30)
    of = curve([(0, 0), (.3, .01), (.6, .018), (1, .02)], 30)
    return Loft(path, rf, rs, p=2.3, of=of, segs=40, ref=(0, 1, 0), cap1=4)


def jaw_loft():
    path = spline([(0, 1.12, 0.74), (0, 1.095, 0.86), (0, 1.085, 0.97), (0, 1.085, 1.03)], 20)
    return Loft(path, curve([(0, .045), (.5, .032), (1, .020)], 20), curve([(0, .075), (.5, .048), (1, .028)], 20), p=2.2, segs=28, ref=(0, 1, 0), cap1=3)


def leg_loft(front=True):
    if front:
        pts = [(0.10, 1.00, 0.38), J['UpperArm'], J['LowerArm'], J['Wrist'], J['FrontPaw']]
        rf = curve([(0, .09), (.25, .085), (.5, .055), (.75, .036), (1, .032)], 34); rs = curve([(0, .06), (.25, .065), (.5, .045), (.75, .033), (1, .030)], 34)
    else:
        pts = [(0.10, 0.96, -0.46), J['Shin'], J['Hock'], J['BackPaw']]
        rf = curve([(0, .12), (.25, .105), (.45, .06), (.7, .038), (1, .033)], 34); rs = curve([(0, .075), (.25, .07), (.45, .05), (.7, .034), (1, .031)], 34)
    return Loft(spline(pts, 34), rf, rs, p=2.2, segs=24, ref=(0, 0, 1))


def tail_loft():
    pts = [(0, 0.97, -0.60), J['Tail2'], J['Tail3'], J['Tail4'], TIP['Tail']]
    return Loft(spline(pts, 30), curve([(0, .06), (.3, .075), (.7, .07), (1, .02)], 30), curve([(0, .055), (.3, .07), (.7, .065), (1, .018)], 30), p=2.2, segs=20, cap1=3)


def paw(center, fwd=1, bone=None, sk=None):
    ms = []
    pad = Loft(spline([np.array(center) + (0, 0.035, -0.035 * fwd), np.array(center) + (0, 0.02, 0.0), np.array(center) + (0, 0.01, 0.05 * fwd)], 10),
               curve([(0, .03), (.5, .035), (1, .022)], 10), curve([(0, .035), (.5, .045), (1, .040)], 10), p=2.4, segs=20, ref=(0, 1, 0), cap0=2, cap1=2)
    ms.append((pad.mesh(0.08), FUR))
    for k, dx in enumerate((-0.03, -0.011, 0.011, 0.03)):
        base = np.array(center) + (dx, 0.012, 0.045 * fwd)
        tip = base + (dx * 0.25, -0.012, 0.035 * fwd)
        cl = Loft(spline([base, base + (0, 0.004, 0.012 * fwd), tip], 8), curve([(0, .007), (1, .0012)], 8), curve([(0, .006), (1, .001)], 8), p=2.0, segs=8, cap1=1)
        ms.append((cl.mesh(0.03), BONE))
    return ms


# ------------------------------------------------------------------------------------------- fourrure
def tufts(loft, sk, weights_of, n, rng, length, width, region, mat_name, back_bias=0.6, droop=0.3, t_range=(0, 1)):
    """Mèches de poils posées sur un loft : petites lames effilées, couchées vers l'arrière et vers le bas."""
    ms = []
    count = 0; tries = 0
    while count < n and tries < n * 20:
        tries += 1
        t = rng.uniform(*t_range); a = rng.uniform(0, 1)
        dens = region(t, a)
        if rng.uniform() > dens: continue
        P, N, Tt, Ta = loft.frame(np.array(t), np.array(a))
        back = -loft.T[min(int(t * (loft.n - 1)), loft.n - 1)]
        L = length(t, a) * rng.uniform(0.7, 1.25)
        d = unit(N * (1 - back_bias) + back * back_bias + np.array([0, -1.0, 0]) * droop + rng.normal(0, 0.15, 3))
        side = unit(np.cross(d, N) + 1e-6)
        mid = P + d * L * 0.5 + N * L * 0.12
        tip = P + d * L + N * L * 0.02 - np.array([0, 0.25, 0]) * L * droop
        w = width * rng.uniform(0.7, 1.3)
        pth = spline([P - d * 0.004, mid, tip], 5)
        tf = Loft(pth, curve([(0, w * 0.35), (1, w * 0.06)], 5), curve([(0, w), (.5, w * 0.7), (1, w * 0.05)], 5), p=2.0, segs=5, ref=tuple(N))
        m = tf.mesh(1.0)
        m.UV = np.c_[(np.arange(len(m.V)) % 6) / 5.0 * 0.25 + rng.uniform(0, 0.75), np.repeat(np.linspace(0, 1, len(m.V) // 6 + 1), 6)[:len(m.V)]]
        J, W = weights_of(P)
        m.J = np.tile(J, (len(m.V), 1)); m.W = np.tile(W, (len(m.V), 1))
        ms.append(m); count += 1
    return concat(ms)


def nearest_weights(m):
    from scipy.spatial import cKDTree
    tree = cKDTree(m.V)
    def f(P):
        _, i = tree.query(P); return m.J[i].copy(), m.W[i].copy()
    return f


def build(seed=5):
    sk = skeleton(); a = Asset('ShadowWolf', sk); sw = swap(sk)
    rng = np.random.default_rng(seed)
    body = body_loft()
    m = body.mesh(0.25)
    chain(m, [(0, 0.93, -0.70), J['Hips'], J['Spine1'], J['Spine2'], J['Chest'], J['Neck1'], J['Neck2'], J['Head'], (0, 1.25, 0.8)],
          [sk['Hips'], sk['Hips'], sk['Spine1'], sk['Spine2'], sk['Chest'], sk['Neck1'], sk['Neck2'], sk['Head']], blend=0.08)
    a.add('corps', m, FUR)
    hd = head_loft(); m = hd.mesh(0.2); rigid(m, sk['Head']); a.add('tete', m, FUR)
    jw = jaw_loft(); m = jw.mesh(0.2); rigid(m, sk['Jaw']); a.add('machoire', m, FUR)
    # gueule : gencives sombres, crocs
    for z, h in ((0.80, 0.020), (0.86, 0.022), (0.92, 0.024), (0.97, 0.028), (1.02, 0.040)):
        for sx in (1, -1):
            x = sx * (0.040 if z < 1.0 else 0.024)
            up = Loft(spline([(x, 1.118, z), (x, 1.118 - h * 0.5, z + 0.004), (x, 1.118 - h, z + 0.006)], 6), curve([(0, .0055), (1, .0008)], 6), curve([(0, .005), (1, .0007)], 6), p=2.0, segs=8, cap1=1)
            mm = up.mesh(0.03); rigid(mm, sk['Head']); a.add(f'croc_h{z}{sx}', mm, BONE)
            lo = Loft(spline([(x * 0.93, 1.092, z - 0.01), (x * 0.93, 1.092 + h * 0.45, z - 0.006), (x * 0.93, 1.092 + h * 0.85, z - 0.004)], 6), curve([(0, .005), (1, .0007)], 6), curve([(0, .0045), (1, .0006)], 6), p=2.0, segs=8, cap1=1)
            mm = lo.mesh(0.03); rigid(mm, sk['Jaw']); a.add(f'croc_b{z}{sx}', mm, BONE)
    gum = Loft(spline([(0, 1.105, 0.76), (0, 1.104, 0.9), (0, 1.104, 1.03)], 10), 0.010, curve([(0, .06), (1, .022)], 10), p=2.2, segs=16, ref=(0, 1, 0), cap1=2)
    mm = gum.mesh(0.1); blend_bones(mm, sk['Head'], sk['Jaw'], 0.5); a.add('gueule', mm, MOUTH)
    # yeux luisants (sous une arcade marquée)
    for sx in (1, -1):
        P = np.array([0.052 * sx, 1.245, 0.835]); N = unit(np.array([0.55 * sx, 0.15, 0.82]))
        e = rivet(P, N, r=0.016, h=0.006, segs=14, rings=4); rigid(e, sk['Head']); a.add(f'oeil{sx}', e, EYE)
        brow = Loft(spline([P + (-0.022 * sx, 0.020, -0.012), P + (0.0, 0.026, 0.0), P + (0.022 * sx, 0.016, 0.006)], 8), 0.007, 0.012, p=2.2, segs=10, cap0=1, cap1=1)
        mm = brow.mesh(0.05); rigid(mm, sk['Head']); a.add(f'arcade{sx}', mm, FUR)
    # oreilles
    ear = Loft(spline([(0.06, 1.30, 0.70), (0.07, 1.37, 0.69), (0.085, 1.45, 0.66)], 10), curve([(0, .012), (1, .002)], 10), curve([(0, .035), (.6, .022), (1, .002)], 10), p=2.0, segs=10, ref=(0, 0, 1), cap0=1)
    m = ear.mesh(0.1); rigid(m, sk['Ear_L']); a.add('oreille', m, FUR); a.add('oreille_R', mirror(m, sw), FUR)
    # pattes
    fl = leg_loft(True); m = fl.mesh(0.2)
    chain(m, [(0.09, 1.08, 0.36), J['Scapula'], J['UpperArm'], J['LowerArm'], J['Wrist'], J['FrontPaw'], TIP['FrontPaw']],
          [sk['Chest'], sk['Scapula_L'], sk['UpperArm_L'], sk['LowerArm_L'], sk['Wrist_L'], sk['FrontPaw_L']], blend=0.04)
    a.add('patte_av', m, FUR); a.add('patte_av_R', mirror(m, sw), FUR)
    bl = leg_loft(False); m = bl.mesh(0.2)
    chain(m, [(0.10, 1.05, -0.46), J['Thigh'], J['Shin'], J['Hock'], J['BackPaw'], TIP['BackPaw']],
          [sk['Hips'], sk['Thigh_L'], sk['Shin_L'], sk['Hock_L'], sk['BackPaw_L']], blend=0.05)
    a.add('patte_ar', m, FUR); a.add('patte_ar_R', mirror(m, sw), FUR)
    for (c, fwd, bone, nm) in ((J['FrontPaw'], 1, 'FrontPaw_L', 'pied_av'), (J['BackPaw'], 1, 'BackPaw_L', 'pied_ar')):
        for k, (mm, mat) in enumerate(paw(np.array(c) + (0, -0.02, 0.0), fwd)):
            rigid(mm, sk[bone]); a.add(f'{nm}{k}', mm, mat); a.add(f'{nm}{k}_R', mirror(mm, sw), mat)
    # queue
    tl = tail_loft(); m = tl.mesh(0.2)
    chain(m, [(0, 0.97, -0.58), J['Tail1'], J['Tail2'], J['Tail3'], J['Tail4'], TIP['Tail']], [sk['Hips'], sk['Tail1'], sk['Tail2'], sk['Tail3'], sk['Tail4']], blend=0.05)
    a.add('queue', m, FUR)

    # ---- fourrure : mèches (crinière rouge sombre sur l'échine et le cou, pelage sombre ailleurs)
    wb = nearest_weights(concat([p[1] for p in a.parts if p[0] in ('corps',)]))
    mane = lambda t, a_: np.exp(-((a_ % 1.0) / 0.10) ** 2) + np.exp(-(((a_ % 1.0) - 1.0) / 0.10) ** 2)
    m = tufts(body, sk, wb, 420, rng, lambda t, a_: 0.07 + 0.11 * np.exp(-((t - 0.78) / 0.18) ** 2), 0.020, lambda t, a_: min(1.0, mane(t, a_) * (0.3 + 0.9 * t)),
              MANE, back_bias=0.55, droop=0.15, t_range=(0.10, 0.98))
    a.add('criniere', m, MANE)
    side = lambda t, a_: 0.45 * (1 - mane(t, a_)) * (0.6 + 0.8 * np.exp(-((t - 0.72) / 0.15) ** 2))
    m = tufts(body, sk, wb, 650, rng, lambda t, a_: 0.045 + 0.05 * np.exp(-((t - 0.72) / 0.15) ** 2), 0.017, side, FUR, back_bias=0.7, droop=0.45, t_range=(0.05, 0.98))
    a.add('pelage', m, FUR)
    m = tufts(tl, sk, nearest_weights(a.parts[[p[0] for p in a.parts].index('queue')][1]), 160, rng, lambda t, a_: 0.06 + 0.04 * (1 - t), 0.02,
              lambda t, a_: 0.9, FUR, back_bias=0.8, droop=0.1, t_range=(0.05, 0.95))
    a.add('queue_poils', m, FUR)
    for nm, lf in (('patte_av', fl), ('patte_ar', bl)):
        src = a.parts[[p[0] for p in a.parts].index(nm)][1]
        m = tufts(lf, sk, nearest_weights(src), 90, rng, lambda t, a_: 0.05 * (1 - t) + 0.02, 0.015, lambda t, a_: 1.0 - 0.8 * t, FUR, back_bias=0.6, droop=0.6, t_range=(0.0, 0.6))
        a.add(nm + '_poils', m, FUR); a.add(nm + '_poils_R', mirror(m, sw), FUR)
    # os saillants le long de l'échine (corruption)
    for k, t in enumerate(np.linspace(0.30, 0.66, 6)):
        P, N, _, _ = body.frame(np.array(t), np.array(0.0))
        base = P + N * 0.01; tip = base + unit(N + np.array([0, 0, -0.5])) * (0.05 + 0.05 * np.sin(np.pi * (t - 0.3) / 0.36))
        sp = Loft(spline([base, (base + tip) / 2, tip], 6), curve([(0, .014), (1, .002)], 6), curve([(0, .010), (1, .0015)], 6), p=2.0, segs=8, cap1=1)
        mm = sp.mesh(0.04); J_, W_ = wb(P); mm.J = np.tile(J_, (len(mm.V), 1)); mm.W = np.tile(W_, (len(mm.V), 1)); a.add(f'epine{k}', mm, BONE)
    return sk, a
