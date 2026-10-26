"""Araignée géante — créature cavernicole : céphalothorax cuirassé, abdomen énorme couvert d'écailles de chitine en
losanges cerclées d'orange, deux grands yeux de braise et six yeux secondaires, chélicères massives aux crochets
recourbés, pédipalpes, huit pattes articulées (coxa, fémur, tibia, métatarse, tarse) annelées et hérissées de soies.
Squelette dédié (rig Generic) et animations : Idle, Walk (tétrapode alterné), Run, Attack (cabrage et frappe), Hit, Death."""
import numpy as np
from forge.rig import Asset, Skeleton, qaxis, qmul
from forge.geo import Loft, spline, curve, concat, rigid, unit, mirror, vertex_normals
from forge import sculpt as SC
from forge.anim import key, build_qclip
from forge.texgen import TexSet, fbm, n01, height_to_normal, voronoi_edges, cavity_ao
from scipy.ndimage import gaussian_filter
from enemies import materials as EM
from knight import materials as KM

NAME = 'AraigneeGeante'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 0.62, 0.55)
SEGS = ('Coxa', 'Femur', 'Tibia', 'Meta', 'Tarsus')
ANG = (38, 72, 108, 142)               # direction des pattes (degrés depuis l'avant vers le côté)
ATT_Z = (0.36, 0.27, 0.18, 0.09)
LEN = (1.10, 1.0, 0.98, 1.08)


def chitin(S=1024, seed=901, tone=(0.36, 0.15, 0.09), dark=(0.08, 0.04, 0.03), rings=True):
    """Chitine brun-rouge : marbrures, micro-écailles, bandes sombres (anneaux des articles), vernis brillant."""
    r = np.random.default_rng(seed)
    mott = n01(fbm(S, S, r, base=S / 6, octaves=5))
    scales = gaussian_filter(voronoi_edges(S, S, r, 220), 0.8, mode='wrap')
    band = 0.5 + 0.5 * np.sin(np.linspace(0, 2 * np.pi * 3, S))[:, None] * np.ones((1, S)) if rings else np.zeros((S, S))
    t = np.clip(mott * 0.7 + 0.3 * (1 - band) * rings, 0, 1)
    base = np.array(dark)[None, None] * (1 - t[..., None]) + np.array(tone)[None, None] * t[..., None]
    base = base * (1 - 0.35 * n01(scales)[..., None])
    hgt = 1.2 * mott - 0.8 * n01(scales)
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.5), np.zeros((S, S)), np.clip(0.55 + 0.2 * mott - 0.2 * n01(scales), 0, 1), cavity_ao(hgt, 4, 0.1))


def carapace(S=2048, seed=902):
    """Écailles en losanges de l'abdomen : plaques gris-noir bombées, joints sombres, liserés et points orange luisants."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    u = (xx + yy) * 4; v = (xx - yy) * 4
    du = np.abs(u - np.round(u)); dv = np.abs(v - np.round(v))
    edge = np.clip(1 - np.minimum(du, dv) / 0.06, 0, 1)
    dome = np.clip(1 - np.maximum(du, dv) * 2, 0, 1)
    mott = n01(fbm(S, S, r, base=S / 8, octaves=5))
    base = np.array([0.10, 0.10, 0.11])[None, None] * (0.6 + 0.6 * mott[..., None]) * (0.75 + 0.5 * dome[..., None])
    rim = np.clip(edge * 1.4 - 0.35, 0, 1) * np.clip(n01(fbm(S, S, r, base=S / 5, octaves=3)) * 1.8 - 0.5, 0, 1)
    base = base * (1 - 0.7 * edge[..., None]) + np.array([0.55, 0.22, 0.05])[None, None] * rim[..., None]
    spots = np.clip(n01(fbm(S, S, r, base=S / 30, octaves=2)) - 0.82, 0, 1) * 6
    emit = np.clip(np.array([1.0, 0.45, 0.08])[None, None] * (0.6 * rim + spots)[..., None] * 0.7, 0, 1)
    hgt = 1.5 * dome - 1.5 * edge + 0.4 * mott
    return TexSet(np.clip(base, 0, 1), height_to_normal(hgt, 0.8), np.zeros((S, S)), np.clip(0.55 + 0.25 * dome, 0, 1), cavity_ao(hgt, 5, 0.12), emit)


RECIPES = {
    'SP_Chitin':  (lambda S: chitin(S, seed=901), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'SP_Shell':   (lambda S: carapace(S, seed=902), 2048, 1024, dict(metallic=0.1, smoothness=1.0, emission=(1.5, 1.5, 1.5), emission_preview=(0.8, 0.8, 0.8))),
    'SP_Fang':    (lambda S: KM.bone(S, seed=903, tone=(0.12, 0.08, 0.06)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'SP_Eye':     (lambda S: EM.glow(S, (1.0, 0.28, 0.04)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(3.5, 3.5, 3.5), emission_preview=(2.2, 2.2, 2.2))),
    'SP_Hair':    (lambda S: KM.bone(S, seed=904, tone=(0.06, 0.04, 0.035)), 128, 64, dict(metallic=0.0, smoothness=1.0)),
    'SP_Mouth':   (lambda S: KM.leather(S, seed=905, tone=(0.18, 0.04, 0.04)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = []


def leg_points(i, side=1):
    a = np.radians(ANG[i]); d = np.array([np.sin(a) * side, 0.0, np.cos(a)]); s = LEN[i]
    a0 = np.array([0.14 * side, 0.62, ATT_Z[i]])
    P = [a0, a0 + d * 0.11 * s + (0, 0.03, 0), a0 + d * 0.55 * s + (0, 0.42, 0), a0 + d * 0.98 * s + (0, 0.20, 0),
         a0 + d * 1.26 * s + (0, -0.36, 0), a0 + d * 1.38 * s]
    P[5][1] = 0.015
    return [np.asarray(p, float) for p in P], d


def skeleton():
    sk = Skeleton()
    sk.add('Hips', None, (0, 0.62, 0.20))
    sk.add('Thorax', 'Hips', (0, 0.64, 0.30))
    sk.add('Abdomen', 'Hips', (0, 0.70, 0.02))
    sk.add('Spinneret', 'Abdomen', (0, 0.75, -0.80))
    for s, sx in (('L', 1), ('R', -1)):
        sk.add(f'Chelicera_{s}', 'Thorax', (0.05 * sx, 0.58, 0.49)); sk.add(f'Fang_{s}', f'Chelicera_{s}', (0.05 * sx, 0.44, 0.55))
        sk.add(f'Palp1_{s}', 'Thorax', (0.08 * sx, 0.56, 0.46)); sk.add(f'Palp2_{s}', f'Palp1_{s}', (0.16 * sx, 0.62, 0.62))
        for i in range(4):
            P, _ = leg_points(i, sx)
            par = 'Thorax'
            for k, seg in enumerate(SEGS):
                n = f'Leg{i}_{seg}_{s}'; sk.add(n, par, P[k]); par = n
    return sk


def swap(sk):
    sw = {}
    for i, n in enumerate(sk.names):
        if n.endswith('_L'): j = sk.names.index(n[:-2] + '_R'); sw[i] = j; sw[j] = i
    return sw


def body(sk):
    s = SC.Sculpt(k=0.018)
    # céphalothorax : bouclier bombé, sillon, front relevé où sont les yeux
    s.ell('Thorax', (0, 0.66, 0.28), (0.20, 0.10, 0.25), k=0.025)
    s.ell('Thorax', (0, 0.70, 0.40), (0.12, 0.08, 0.12))
    s.ell('Thorax', (0, 0.57, 0.27), (0.16, 0.06, 0.20))                                   # sternum
    s.ell('Thorax', (0.05, 0.61, 0.47), (0.065, 0.075, 0.060))                             # base des chélicères (gauche)
    s.cone('Hips', (0, 0.66, 0.10), (0, 0.70, -0.02), 0.06, 0.07)                           # pédicelle
    # abdomen énorme, relevé, crête dorsale en losange
    s.ell('Abdomen', (0, 0.88, -0.42), (0.38, 0.36, 0.52), fwd=(0, 0.25, 1), k=0.03)
    s.cone('Abdomen', (0, 1.17, -0.20), (0, 1.12, -0.80), 0.050, 0.020, squash=(0.6, (1, 0, 0)))  # arête dorsale
    for k, (z, w) in enumerate([(-0.10, 0.22), (-0.30, 0.30), (-0.50, 0.30), (-0.68, 0.22)]):     # bourrelets transverses
        s.ell('Abdomen', (0, 0.98 + 0.05 * np.cos(k), z - 0.02), (w, 0.10, 0.07), fwd=(0, 0.6, 1), k=0.03)
    s.ell('Spinneret', (0, 0.74, -0.90), (0.06, 0.06, 0.07))
    for k, z in enumerate(np.linspace(-0.08, -0.72, 7)):                                     # épines sur l'arête dorsale
        y0 = 1.10 + 0.10 * np.cos((z + 0.40) * 3.0)
        s.cone('Abdomen', (0, y0, z), (0, y0 + 0.08 - 0.03 * abs(k - 3) / 3, z - 0.05), 0.030, 0.004, k=0.006)
    s.ell('Thorax', (0, 0.735, 0.47), (0.10, 0.030, 0.050), k=0.010)                         # arcade au-dessus des yeux
    # chélicères
    s.cone('Chelicera_L', (0.05, 0.60, 0.48), (0.05, 0.45, 0.56), 0.060, 0.040)
    # pédipalpes
    s.cone('Palp1_L', (0.08, 0.56, 0.46), (0.16, 0.62, 0.62), 0.028, 0.022)
    s.cone('Palp2_L', (0.16, 0.62, 0.62), (0.20, 0.50, 0.74), 0.022, 0.016)
    s.mirror(lambda b: b[:-2] + '_R' if b.endswith('_L') else b)
    detail = lambda X: 0.002 * SC.noise3(X, 0.03, seed=91, octaves=2)
    m = s.mesh(res=0.006, detail=detail, smooth=8)
    s.skin(m, sk, kw=0.02)
    SC.box_uv(m, 0.35)
    return m


def legs(sk):
    """Pattes : articles coniques à rebords (anneaux), genoux noueux, soies raides."""
    ms, hairs = [], []
    rng = np.random.default_rng(5)
    for i in range(4):
        P, d = leg_points(i, 1)
        s = SC.Sculpt(k=0.010)
        radii = [(0.070, 0.066), (0.066, 0.054), (0.054, 0.042), (0.040, 0.026), (0.026, 0.010)]
        for k, seg in enumerate(SEGS):
            b = f'Leg{i}_{seg}_L'
            a_, e_ = P[k], P[k + 1]
            ra, rb = radii[k]
            s.cone(b, a_, e_, ra, rb)
            s.ell(b, a_ + (e_ - a_) * 0.04, (ra * 1.12, ra * 1.12, ra * 1.12))                   # articulation noueuse
            s.ell(b, e_ - (e_ - a_) * 0.07, (rb * 1.25, rb * 1.25, rb * 1.25), k=0.006)          # rebord distal
            # soies : sur le dessus des articles
            L = np.linalg.norm(e_ - a_)
            for j in range(int(L / 0.05)):
                t = rng.uniform(0.1, 0.9); p = a_ + (e_ - a_) * t
                up = unit(np.cross(unit(e_ - a_), np.cross((0, 1, 0), unit(e_ - a_))) + rng.normal(0, 0.4, 3))
                r_ = ra + (rb - ra) * t
                base = p + up * r_ * 0.9; tip = base + unit(up + unit(e_ - a_) * 0.8) * rng.uniform(0.03, 0.06)
                h = Loft(spline([base, tip], 3), curve([(0, .0028), (1, .0004)], 3), curve([(0, .0028), (1, .0004)], 3), p=2, segs=4, cap1=1).mesh(0.05)
                rigid(h, sk[b]); hairs.append(h)
        m = s.mesh(res=0.0045, smooth=6); s.skin(m, sk, kw=0.008); SC.box_uv(m, 0.25)
        ms.append(m)
    return ms, concat(hairs)


def face(sk, m):
    from scipy.spatial import cKDTree
    tree = cKDTree(m.V); N = vertex_normals(m)
    def on(p, sink=0.0):
        i = tree.query(p)[1]; return m.V[i] - N[i] * sink, N[i]
    eyes = []
    for (x, y, z, r) in ((0.050, 0.705, 0.52, 0.040), (0.018, 0.745, 0.50, 0.018), (0.098, 0.715, 0.46, 0.020), (0.070, 0.760, 0.46, 0.014)):
        for sx in (1, -1):
            q, n = on(np.array([x * sx, y, z]), r * 0.35)
            eyes.append(SC.Sculpt(k=0.002).ell('Thorax', q, (r, r * 0.9, r * 0.8), fwd=n).mesh(res=max(0.0012, r / 10), smooth=2))
    e = concat(eyes); rigid(e, sk['Thorax'])
    fangs = []
    for sx in (1, -1):
        b = np.array([0.05 * sx, 0.44, 0.56])
        f = Loft(spline([b, b + (-0.012 * sx, -0.06, 0.02), b + (-0.035 * sx, -0.11, -0.005), b + (-0.05 * sx, -0.13, -0.04)], 14),
                 curve([(0, .020), (1, .0015)], 14), curve([(0, .016), (1, .0012)], 14), p=2.2, segs=10, cap1=1).mesh(0.05)
        rigid(f, sk[f'Fang_{"L" if sx > 0 else "R"}']); fangs.append(f)
    mouth = SC.Sculpt(k=0.006).ell('Thorax', (0, 0.50, 0.50), (0.045, 0.03, 0.03)).mesh(res=0.003, smooth=2); rigid(mouth, sk['Thorax'])
    return e, fangs, mouth


# ------------------------------------------------------------------------------------------- animations
def _leg_rot(i, side, lift, swing, curl=0.0):
    """Rotations (quaternions locaux) d'une patte : swing autour de Y (avant/arrière), lift autour de l'axe horizontal
    perpendiculaire à la patte (lève le fémur), curl replie tibia et métatarse."""
    a = np.radians(ANG[i]); d = np.array([np.sin(a) * side, 0, np.cos(a)])
    ax = unit(np.cross(d, (0, 1, 0)))                 # rotation positive : la patte se lève
    q = {}
    q[f'Leg{i}_Coxa_{"L" if side > 0 else "R"}'] = qaxis((0, 1, 0), np.radians(swing))
    q[f'Leg{i}_Femur_{"L" if side > 0 else "R"}'] = qaxis(ax, np.radians(-lift))
    q[f'Leg{i}_Tibia_{"L" if side > 0 else "R"}'] = qaxis(ax, np.radians(lift * 0.6 + curl))
    q[f'Leg{i}_Meta_{"L" if side > 0 else "R"}'] = qaxis(ax, np.radians(lift * 0.4 + curl * 0.8))
    return q


def _bones(sk): return [n for n in sk.names if n != 'Hips']


def walk(sk, name='Walk', T=1.1, amp=16, lift=22, bob=0.015):
    keys = []
    for k in range(9):
        t = k * T / 8; w = 2 * np.pi * t / T
        p = {}
        for i in range(4):
            for side in (1, -1):
                grp = (i + (0 if side > 0 else 1)) % 2      # tétrapode alterné : L0 R1 L2 R3 / R0 L1 R2 L3
                ph = w + np.pi * grp
                p.update(_leg_rot(i, side, lift * max(0.0, np.sin(ph)), amp * np.cos(ph) * side))
        p['Abdomen'] = qaxis((1, 0, 0), np.radians(2 * np.sin(2 * w)))
        p['Palp1_L'] = qaxis((1, 0, 0), np.radians(8 * np.sin(w))); p['Palp1_R'] = qaxis((1, 0, 0), np.radians(-8 * np.sin(w)))
        keys.append(key(t, p, hips=(0, bob * np.sin(2 * w), 0), hips_rot=(0, 2 * np.sin(w), 1.5 * np.sin(w))))
    return build_qclip(name, keys, _bones(sk), loop=True)


def idle(sk):
    keys = []
    for k in range(7):
        t = k * 0.5; w = 2 * np.pi * t / 3.0
        p = {'Abdomen': qaxis((1, 0, 0), np.radians(3 * np.sin(w))), 'Palp1_L': qaxis((1, 0, 0), np.radians(10 * np.sin(w * 2))),
             'Palp1_R': qaxis((1, 0, 0), np.radians(10 * np.sin(w * 2 + 1))), 'Fang_L': qaxis((0, 0, 1), np.radians(6 * np.sin(w * 3))),
             'Fang_R': qaxis((0, 0, 1), np.radians(-6 * np.sin(w * 3)))}
        for i in range(4):
            for side in (1, -1): p.update(_leg_rot(i, side, 2 * np.sin(w + i), 0))
        keys.append(key(t, p, hips=(0, -0.01 + 0.008 * np.sin(w), 0)))
    return build_qclip('Idle', keys, _bones(sk), loop=True)


def attack(sk):
    def P(rear, strike, fang):
        p = {'Fang_L': qaxis((0, 0, 1), np.radians(fang)), 'Fang_R': qaxis((0, 0, 1), np.radians(-fang))}
        for side in (1, -1):
            p.update(_leg_rot(0, side, 60 * rear - 20 * strike, -10 * side * rear))
            p.update(_leg_rot(1, side, 30 * rear - 10 * strike, 0))
            p.update(_leg_rot(3, side, -10 * rear, 0, curl=-15 * rear))
        return p
    ks = [key(0, P(0, 0, 0)), key(0.35, P(1, 0, 25), hips=(0, 0.10, -0.05), hips_rot=(-22, 0, 0)),
          key(0.55, P(0.6, 1, -10), hips=(0, 0.02, 0.25), hips_rot=(8, 0, 0)), key(1.0, P(0, 0, 0))]
    return build_qclip('Attack', ks, _bones(sk))


def hit(sk):
    ks = [key(0, {}), key(0.12, {'Abdomen': qaxis((0, 1, 0), np.radians(12))}, hips=(0, -0.04, -0.06), hips_rot=(6, -10, 0)), key(0.5, {})]
    return build_qclip('Hit', ks, _bones(sk))


def death(sk):
    p = {}
    for i in range(4):
        for side in (1, -1): p.update(_leg_rot(i, side, 40, 0, curl=70))
    ks = [key(0, {}), key(0.5, p, hips=(0, -0.25, 0), hips_rot=(0, 0, 10)), key(1.2, p, hips=(0, -0.45, 0), hips_rot=(0, 0, 160))]
    return build_qclip('Death', ks, _bones(sk))


def build(bank, female=False):
    sk = skeleton(); a = Asset(NAME, sk); sw = swap(sk)
    m = body(sk)
    abd = m.V[:, 2] < 0.05
    # deux matériaux sur le corps : carapace d'écailles (abdomen) et chitine (céphalothorax) — découpe par facette
    F_abd = abd[m.F].all(1)
    from forge.humanparts import submesh
    a.add('abdomen', submesh(m, F_abd), 'SP_Shell'); a.add('cephalothorax', submesh(m, ~F_abd), 'SP_Chitin')
    lm, hairs = legs(sk)
    for i, mm in enumerate(lm): a.add(f'patte{i}', mm, 'SP_Chitin'); a.add(f'patte{i}_R', mirror(mm, sw), 'SP_Chitin')
    a.add('soies', hairs, 'SP_Hair'); a.add('soies_R', mirror(hairs, sw), 'SP_Hair')
    e, fangs, mouth = face(sk, m)
    a.add('yeux', e, 'SP_Eye'); a.add('bouche', mouth, 'SP_Mouth')
    for k, f in enumerate(fangs): a.add(f'crochet{k}', f, 'SP_Fang')
    for c in (idle(sk), walk(sk), walk(sk, 'Run', 0.55, 24, 30, 0.03), attack(sk), hit(sk), death(sk)): a.clips[c.name] = c
    return a, {}, a.clips
