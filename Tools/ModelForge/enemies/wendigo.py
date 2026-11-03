"""Wendigo de givre — géant décharné de la Toundra (d'après la planche de concepts) : très grand et maigre, crâne de
cerf fendu (orbites où brillent deux yeux bleus, long museau, rangée de dents), immenses bois ramifiés givrés d'où
pendent des glaçons ; torse creusé où la cage thoracique perce la peau, stalactites de glace sous les côtes ;
peau grise tendue sur les os, crevasses givrées, sang séché ; épaisse fourrure pâle en lambeaux sur les épaules, le
dos, les avant-bras et les hanches ; griffes noires démesurées aux mains et aux pieds."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, sculpt as SC, humanparts as HP
from forge.geo import Loft, spline, curve, unit, concat, rigid, mirror, vertex_normals
from knight import anims, materials as KM
from heroes import modular as MOD
from heroes.female_kit import _blob
from creatures import common as CC
from enemies import materials as EM, bones as BN, goule as GO

NAME = 'WendigoGivre'
FOLDER = 'Enemies'
SCALE = 1.45
MAX_TRIS = 200000
HEAD_VIEW = (0, 1.70, 0.12)
RECIPES = {
    'WG_Bone':   (lambda S: EM.tinted(KM.bone(S, seed=1801, tone=(0.70, 0.68, 0.60)), (1, 1, 1)), 1024, 512, dict(metallic=0.0, smoothness=0.7)),
    'WG_Antler': (lambda S: KM.bone(S, seed=1802, tone=(0.62, 0.60, 0.55)), 1024, 256, dict(metallic=0.0, smoothness=0.6)),
    'WG_Hollow': (lambda S: KM.void(S), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    'WG_Lueur':    (lambda S: EM.glow(S, (0.45, 0.85, 1.0)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.5, 4.5, 4.5), emission_preview=(2.8, 2.8, 2.8))),
    'WG_Ice':    (lambda S: KM.crystal(S, seed=1803, color=(0.70, 0.90, 1.0), glow=0.35), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(0.6, 0.6, 0.6), emission_preview=(0.4, 0.4, 0.4))),
    'WG_Fur':    (lambda S: CC.fur_texture(S, seed=1804, root=(0.10, 0.09, 0.085), tip=(0.50, 0.49, 0.47), density=1.4), 512, 256, dict(metallic=0.0, smoothness=1.0, cutoff=0.45)),
    'WG_Claw':   (lambda S: KM.bone(S, seed=1805, tone=(0.05, 0.045, 0.045)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'WG_Hide':   (lambda W, H_: KM.cloak(W, H_, seed=1806, color=(0.40, 0.39, 0.37), thread_color=(0.50, 0.48, 0.45), mud_amt=1.2), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'WG_Leather': (lambda S: KM.leather(S, seed=1807, tone=(0.12, 0.10, 0.09)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = []
HEAD_PARTS = ('peau_tete', 'yeux', 'cils', 'sourcils', 'dents', 'langue', 'cheveux', 'sexe', 'sous_vetement_bas', 'haillons_haut', 'haillons_corde', 'haillons_bas', 'haillons_pieds')


def deer_skull(sk, rng):
    """Crâne de cerf : boîte crânienne, front plat, orbites latérales creuses, long museau qui descend en pointe,
    ouverture nasale, rangée de dents, fêlure sur le front. Centré sur l'os Head (regard vers +Z)."""
    hc = np.array([0.0, 1.66, 0.03])
    s = SC.Sculpt(k=0.012)
    s.ell('Head', hc, (0.075, 0.072, 0.085))                                            # boîte crânienne
    s.ell('Head', hc + (0, 0.035, 0.03), (0.070, 0.035, 0.07))                          # front plat
    s.cone('Head', hc + (0, -0.005, 0.06), hc + (0, -0.075, 0.24), 0.060, 0.022)        # museau long, en pointe vers le bas
    s.ell('Head', hc + (0, -0.03, 0.12), (0.055, 0.04, 0.07))                           # maxillaire
    for sx in (1, -1): s.ell('Head', hc + (0.062 * sx, 0.005, 0.055), (0.030, 0.030, 0.032), sub=True)   # orbites (creusées)
    s.ell('Head', hc + (0, -0.04, 0.20), (0.016, 0.012, 0.05), sub=True)                # ouverture nasale
    m = s.mesh(res=0.004, smooth=3)
    m.V += 0.0015 * np.c_[np.sin(m.V[:, 1] * 300), np.cos(m.V[:, 2] * 280), np.sin(m.V[:, 0] * 260)]
    SC.box_uv(m, 0.12); rigid(m, sk['Head'])
    holes = []
    for sx in (1, -1): holes.append(_blob(hc + (0.058 * sx, 0.005, 0.052), (0.020, 0.020, 0.012), n=12))
    holes.append(_blob(hc + (0, -0.045, 0.205), (0.010, 0.008, 0.03), n=10))
    hm = concat(holes); rigid(hm, sk['Head'])
    eyes = concat([_blob(hc + (0.066 * sx, 0.006, 0.066), (0.013, 0.011, 0.007), n=12) for sx in (1, -1)]); rigid(eyes, sk['Head'])
    teeth = []
    for sx in (1, -1):
        for k in range(7):
            p = hc + (0.022 * sx, -0.070 - 0.004 * k, 0.10 + 0.018 * k)
            teeth.append(Loft(spline([p, p + (0, -0.018, 0.002)], 3), curve([(0, .005), (1, .0012)], 3), curve([(0, .005), (1, .0012)], 3), p=2, segs=6, cap1=1).mesh(0.02))
    tm = concat(teeth); rigid(tm, sk['Head'])
    return m, hm, eyes, tm


def antlers(sk, rng):
    """Bois de cerf immenses en couronne : merrain qui monte en s'écartant puis se recourbe vers l'intérieur, huit
    andouillers dont certains fourchus, surface striée ; glaçons pendus aux branches."""
    beams, ice = [], []
    def branch(c, d, L, r0, depth):
        q = np.linspace(0, 1, 10)
        bend = unit(np.cross(d, (0, 0, 1)) + 1e-3) * 0.15
        pts = [c + d * L * t + bend * L * t * t + np.array([0, 0.06 * L * t * t, 0]) for t in q]
        beams.append(Loft(spline(pts, 10), curve([(0, r0), (1, r0 * 0.18 + 0.002)], 10), curve([(0, r0 * 0.9), (1, r0 * 0.16 + 0.002)], 10), p=2.2, segs=8, cap1=1).mesh(0.05))
        if depth > 0 and L > 0.12:
            tip = pts[6]; dd = unit(d + rng.normal(0, 0.45, 3) + (0, 0.5, 0))
            branch(tip, dd, L * 0.5, r0 * 0.45, depth - 1)
        if rng.uniform() < 0.6:
            p = pts[int(rng.integers(2, 7))]
            ice.append(Loft(spline([p, p - (0, rng.uniform(0.05, 0.13), 0)], 4), curve([(0, r0 * 0.5), (1, .001)], 4), curve([(0, r0 * 0.5), (1, .001)], 4), p=1.2, segs=6, cap1=1).mesh(0.03))
    for sx in (1, -1):
        b0 = np.array([0.050 * sx, 1.72, 0.02])
        q = np.linspace(0, 1, 40)
        main = np.c_[b0[0] + sx * (0.46 * np.sin(q * 2.0) * (1 - 0.30 * q) + 0.05 * q), b0[1] + 0.62 * q, b0[2] - 0.10 * np.sin(q * 2.0)]
        r = 0.028 * (1 - 0.6 * q) + 0.005
        lo = Loft(spline(main, 40), r, r * 0.9, p=2.2, segs=10, cap0=1, cap1=1); beams.append(lo.mesh(0.1))
        for j, t in enumerate((0.10, 0.22, 0.34, 0.46, 0.58, 0.70, 0.82, 0.93)):
            i = int(t * 39); c = lo.path[i]
            out = sx * (0.6 if j % 2 == 0 else -0.25)
            d = unit(np.array([out, 1.0, 0.55 if j % 3 == 0 else -0.35]))
            branch(c, d, 0.22 + 0.20 * np.sin(np.pi * t), r[i] * 0.8, 1 if j in (2, 4, 6) else 0)
    bm = concat(beams); rigid(bm, sk['Head']); im = concat(ice); rigid(im, sk['Head'])
    return bm, im


def ribcage(body, sk):
    """Cage thoracique qui perce la peau du torse : côtes et sternum à fleur de peau, colonne ; la peau du torse est
    creusée entre les côtes. Stalactites de glace sous les côtes."""
    T = BN.Torso(body, sk)
    sp, ca = BN.spine_and_ribs(body, sk, T)
    # côtes poussées vers l'extérieur pour affleurer
    for m in (sp, ca):
        c = np.array([0, 0, -0.01]); d = m.V - np.c_[np.zeros(len(m.V)), m.V[:, 1], np.full(len(m.V), c[2])]
        m.V += unit(d) * 0.018 * ((m.V[:, 1] > 1.08) & (m.V[:, 1] < 1.45))[:, None]
    ice = []
    rng = np.random.default_rng(1810)
    V = sp.V
    cand = V[(V[:, 2] > 0.04) & (V[:, 1] > 1.10) & (V[:, 1] < 1.42)]
    for p in cand[rng.choice(len(cand), 28, replace=False)]:
        L = rng.uniform(0.03, 0.09)
        ice.append(Loft(spline([p, p - (0, L, -0.004)], 4), curve([(0, .007), (1, .0008)], 4), curve([(0, .007), (1, .0008)], 4), p=1.2, segs=6, cap1=1).mesh(0.03))
    im = HP.weights_from(body, concat(ice))
    return sp, ca, im


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk); rng = np.random.default_rng(1800)
    body, marks = MOD.base(sk, a, bank, 'WG', female=False, look=dict(
        extra=[('universal-male-young-minmuscle-minweight', 1.0), ('stomach-pregnant-decr', 1.0)], hair=None, brows=None, lashes=None,
        skin='middleage_caucasian_male', skin_tint=(0.38, 0.37, 0.36), skin_kw=dict(pale=0.4, dirt=1.5, pubic=0.0)))
    bank.recipes['WG_Skin'] = (lambda S: EM.frost_fur_skin(S, seed=1808), 2048, 1024, dict(metallic=0.0, smoothness=1.0))
    a.parts = [p for p in a.parts if p[0] not in HEAD_PARTS]
    # torse creusé entre les côtes
    for n, m, mat in a.parts:
        if n == 'peau_torse':
            N = vertex_normals(m); front = (m.V[:, 1] > 1.08) & (m.V[:, 1] < 1.45)
            m.V -= N * 0.022 * front[:, None]
    sp, ca, ice = ribcage(body, sk)
    a.add('cotes', sp, 'WG_Bone'); a.add('cartilages', ca, 'WG_Bone'); a.add('stalactites', ice, 'WG_Ice')
    skull, holes, eyes, teeth = deer_skull(sk, rng)
    a.add('crane', skull, 'WG_Bone'); a.add('crane_creux', holes, 'WG_Hollow'); a.add('yeux_givre', eyes, 'WG_Lueur'); a.add('dents', teeth, 'WG_Bone')
    bm, im = antlers(sk, rng)
    a.add('bois', bm, 'WG_Antler'); a.add('glacons', im, 'WG_Ice')
    for k, c in enumerate(GO.claws(sk)):
        c.V = c.V + (c.V - c.V.mean(0)) * 1.6                                    # griffes démesurées
        a.add(f'griffe{k}', c, 'WG_Claw'); a.add(f'griffe{k}_R', mirror(c, sw), 'WG_Claw')
    toes = []
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        for dx in (-0.035, 0.0, 0.035):
            b = np.array([0.104 * sx + dx, 0.03, 0.15])
            c = Loft(spline([b, b + (0, 0.0, 0.04), b + (0, -0.03, 0.07)], 8), curve([(0, .012), (1, .0015)], 8), curve([(0, .011), (1, .0012)], 8), p=2, segs=8, cap1=1).mesh(0.03)
            rigid(c, sk[f'{side}Toes']); toes.append(c)
    a.add('griffes_pieds', concat(toes), 'WG_Claw')
    # fourrure : crinière autour du cou (cache la jonction du crâne), épaules, dos, avant-bras, hanches
    skin = [m for n, m, mat in a.parts if n.startswith('peau_')]
    from forge.geo import concat as cc
    sm = cc(skin)
    def wfur(C):
        y, x, z = C[:, 1], np.abs(C[:, 0]), C[:, 2]
        chest_bare = (z > 0.03) & (y > 1.08) & (y < 1.46) & (x < 0.16)               # la cage thoracique reste à nu
        w = np.ones(len(C)) * 1.0
        w += ((y > 1.42) & (y < 1.62) & (x < 0.30)) * 2.5                            # crinière du cou et des épaules
        w += ((x > 0.18) & (y > 1.30)) * 0.6                                          # bras
        w *= ~chest_bare
        return w
    def flow(P, N):
        arm = (np.abs(P[:, 0]) > 0.20) & (P[:, 1] > 1.30)                     # bras (pose en T) : le poil suit le bras vers la main
        d = np.c_[np.sign(P[:, 0]) * 0.25, np.full(len(P), -1.0), np.full(len(P), -0.15)]
        d[arm] = np.c_[np.sign(P[arm, 0]) * 1.0, np.full(arm.sum(), -0.35), np.full(arm.sum(), -0.1)]
        return unit(d)
    flen = lambda P, N: np.where((P[:, 1] > 1.40) & (np.abs(P[:, 0]) < 0.25), 0.13, np.where(np.abs(P[:, 0]) > 0.22, 0.06, 0.08))
    fur = concat([CC.fur_cards(sm, 7000, rng, flow, flen, width=0.035, lift=0.18, weight=wfur, curl=0.25, jitter=0.3),
                  CC.fur_cards(sm, 5000, rng, flow, lambda P, N: flen(P, N) * 0.6, width=0.028, lift=0.08, weight=wfur, curl=0.15, jitter=0.35)])
    a.add('fourrure', fur, 'WG_Fur')
    MOD.assign(a, [])
    anims.build(sk, a)
    return a, {}, anims.CLIPS
