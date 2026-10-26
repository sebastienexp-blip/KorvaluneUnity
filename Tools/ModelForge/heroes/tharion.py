"""Tharion — le Gardien solaire : paladin en plates émaillées blanches et or, tabard et cape bleu roi,
tête nue (cheveux châtains courts, barbe), marteau de guerre à long manche, pavois orné d'un soleil."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, face as FC
from forge.geo import Loft, Mesh, spline, curve, tube, rivet, concat, unit, grid_faces, vertex_normals
from knight import armor, cape, anims, materials as KM
from knight.anims import Style, Pose, key, build_qclip, BONES
from heroes.common import mh_human
from heroes import modular as MOD
from heroes import skill_anims as SA
from forge import fitloft as FL
from forge import mhbase as MB

NAME = 'Tharion'
HEAD_VIEW = (0, 1.68, 0.05)
FP = FC.FaceParams(width=0.075, height=0.113, brow=1.3, jaw=1.15, chin=1.1, nose=1.05, cheek=1.1)
RECIPES = {
    'TH_Plate':    (lambda S: KM.steel(S, seed=12, tone=(0.86, 0.83, 0.75), polish=0.58, rust_amt=0.0, grime_amt=0.35, scratch_amt=0.35), 2048, 512, dict(metallic=1.0, smoothness=1.0)),
    'TH_Gold':     (lambda S: KM.brass(S, seed=22, gold=(0.95, 0.72, 0.32)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'TH_Leather':  (KM.leather, 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'TH_Mail':     (KM.chainmail, 1024, 512, dict(metallic=1.0, smoothness=1.0)),
    'TH_Gambeson': (lambda S: KM.gambeson(S, color=(0.05, 0.08, 0.20)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'TH_Cloth':    (lambda W, H_: KM.cloak(W, H_, seed=63, color=(0.04, 0.10, 0.32), thread_color=(0.85, 0.66, 0.28), mud_amt=0.5), (1024, 2048), (256, 512), dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':     (KM.wood, 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'SunHammer'), ('Socket_Shield_L', 'SunPavise')]
REMAP = {'KN_Steel': 'TH_Plate', 'KN_Brass': 'TH_Gold', 'KN_Leather': 'TH_Leather', 'KN_Mail': 'TH_Mail', 'KN_Gambeson': 'TH_Gambeson', 'KN_Void': 'TH_Leather', 'KN_Cloak': 'TH_Cloth'}


# ------------------------------------------------------------------------------------------- armes
def hammer():
    a = Asset('SunHammer')
    # manche : bois cerclé d'or, poignée de cuir ; prise à l'origine, tête vers +Y
    haft = Loft(spline([(0, -0.30, 0), (0, 0.2, 0), (0, 0.78, 0)], 30), 0.018, 0.018, p=2.2, segs=16, cap0=2)
    a.add('manche', haft.mesh(0.3), 'WP_Wood')
    grip = Loft(spline([(0, -0.22, 0), (0, 0.10, 0)], 16), 0.0205, 0.0205, p=2.2, segs=16)
    gm = grip.mesh(0.04); gm.UV = np.c_[gm.UV[:, 0] + gm.UV[:, 1] * 0.6, gm.UV[:, 1]]; a.add('poignee', gm, 'TH_Leather')
    for y in (-0.23, 0.11, 0.40, 0.62):
        a.add(f'cercle{y}', tube([(np.cos(t) * 0.021, y, np.sin(t) * 0.021) for t in np.linspace(0, 2 * np.pi, 20)], 0.004, segs=8, cap=False), 'TH_Gold')
    a.add('talon', Loft(spline([(0, -0.30, 0), (0, -0.36, 0)], 8), curve([(0, .024), (1, .008)], 8), curve([(0, .024), (1, .008)], 8), p=2.2, segs=16, cap1=2).mesh(0.05), 'TH_Gold')
    # tête : bloc octogonal à pans, faces de frappe bombées, bec arrière, couronne solaire dorée
    head = Loft(spline([(-0.12, 0.84, 0), (0.0, 0.84, 0), (0.12, 0.84, 0)], 24), curve([(0, .050), (.12, .058), (.5, .062), (.88, .058), (1, .050)], 24),
                curve([(0, .052), (.12, .060), (.5, .066), (.88, .060), (1, .052)], 24), p=3.2, segs=32, ref=(0, 1, 0), cap0=4, cap1=4)
    a.add('tete', head.mesh(0.2), 'TH_Plate')
    for x in (-0.085, 0.085):
        a.add(f'bande{x}', Loft(spline([(x - 0.012, 0.84, 0), (x + 0.012, 0.84, 0)], 4), 0.066, 0.071, p=3.2, segs=32, ref=(0, 1, 0)).mesh(0.05), 'TH_Gold')
    a.add('collet', Loft(spline([(0, 0.76, 0), (0, 0.79, 0)], 5), curve([(0, .026), (1, .040)], 5), curve([(0, .026), (1, .040)], 5), p=2.5, segs=20).mesh(0.05), 'TH_Gold')
    for z in (0.068, -0.068):
        s = np.sign(z)
        a.add(f'soleil{z}', rivet(np.array([0.0, 0.84, z]), np.array([0, 0, s]), r=0.028, h=0.008, segs=20, rings=5), 'TH_Gold')
        for k in range(12):
            ang = k * np.pi / 6; d = np.array([np.cos(ang), np.sin(ang), 0])
            p0 = np.array([0, 0.84, z]) + d * 0.030; p1 = np.array([0, 0.84, z]) + d * (0.052 if k % 2 == 0 else 0.043)
            a.add(f'rayon{z}{k}', Loft(spline([p0, p1], 5), 0.0035, curve([(0, .005), (1, .001)], 5), p=2.0, segs=6, ref=(0, 0, s), cap1=1).mesh(0.05), 'TH_Gold')
    a.add('pointe', Loft(spline([(0, 0.905, 0), (0, 0.99, 0)], 8), curve([(0, .022), (1, .002)], 8), curve([(0, .022), (1, .002)], 8), p=3.5, segs=8, cap1=1).mesh(0.05), 'TH_Gold')
    return a


def pavise(W=0.27, Hh=0.92, th=0.026, bend=0.4):
    a = Asset('SunPavise')
    nu, nv = 30, 50
    U, V = np.meshgrid(np.linspace(-1, 1, nu), np.linspace(0, 1, nv))
    top = 0.42
    hw = W * (1 - 0.18 * np.clip((V - 0.85) / 0.15, 0, 1) ** 2)
    def surf(dz):
        x = U * hw; y = top - V * Hh + 0.04 * (1 - U ** 2) * (V < 1e-6); z = -bend * x ** 2 + dz
        return np.stack([x, y, z], -1)
    for dz, flip, mat, nm in ((th / 2, False, 'TH_Plate', 'face'), (-th / 2, True, 'WP_Wood', 'dos')):
        P = surf(dz)
        m = Mesh(P.reshape(-1, 3), grid_faces(nv, nu), np.c_[P[..., 0].ravel() / 0.35 + 0.5, P[..., 1].ravel() / 0.35])
        n = vertex_normals(m, weld=False)
        if (n[:, 2].mean() < 0) != flip: m.flip()
        a.add(nm, m, mat)
    Pf = surf(0)
    edge = np.vstack([Pf[0, :], Pf[1:, -1], Pf[-1, ::-1], Pf[-2::-1, 0], Pf[:1, 0]])
    a.add('bord', tube(edge, th * 0.65, segs=10, tile=0.05, cap=False), 'TH_Gold')
    # bordure intérieure dorée et soleil central
    inner = Pf[3:-3, 3:-3]; e2 = np.vstack([inner[0, :], inner[1:, -1], inner[-1, ::-1], inner[-2::-1, 0], inner[:1, 0]]); e2[:, 2] += th / 2 + 0.002
    a.add('liseré', tube(e2, 0.006, segs=8, tile=0.05, cap=False), 'TH_Gold')
    c = np.array([0, top - Hh * 0.42, th / 2 + 0.003])
    a.add('soleil', rivet(c, np.array([0, 0, 1.0]), r=0.075, h=0.022, segs=28, rings=6), 'TH_Gold')
    for k in range(16):
        ang = k * np.pi / 8; d = np.array([np.cos(ang), np.sin(ang), 0]); L = 0.20 if k % 2 == 0 else 0.14
        pts = np.array([c + d * r_ for r_ in np.linspace(0.08, L, 8)]); pts[:, 2] = -bend * pts[:, 0] ** 2 + th / 2 + 0.002
        a.add(f'rayon{k}', Loft(pts, curve([(0, .006), (1, .002)], 8), curve([(0, .016), (1, .002)], 8), p=3, segs=8, ref=(0, 0, 1), cap1=1).mesh(0.05), 'TH_Gold')
    for y in (0.10, -0.15):
        pts = spline([(-0.11, y, -th / 2 - 0.002), (0, y, -th / 2 - 0.035), (0.11, y, -th / 2 - 0.002)], 16)
        a.add(f'enarme{y}', Loft(pts, 0.002, 0.014, p=4, segs=8, ref=(0, 0, 1)).mesh(0.1), 'TH_Leather')
    for _, mm, _ in a.parts: mm.V[:, 2] += 0.06
    return a


# ------------------------------------------------------------------------------------------- style de combat
class HammerStyle(Style):
    name = 'hammer_shield'
    def guard(self, p):
        p.arm('R', down=68, fwd=14, twist=-15, elbow=55, wrist=(-25, 0, -10))       # marteau tenu tête en haut, devant l'épaule
        p.arm('L', down=62, fwd=24, twist=35, elbow=66)
        p.fist('R'); p.fist('L', 0.9); return p

    def attack(self, sk):
        def P(sp, tw, rd, rf, rtw, rel, wr, lth, rth, lk, rk):
            p = Pose().spine(sp, tw, 0).head(-sp * 0.4, -tw * 0.6); self.guard(p)
            p.arm('R', down=rd, fwd=rf, twist=rtw, elbow=rel, wrist=wr)
            p.leg('L', thigh=lth, knee=lk, spread=5); p.leg('R', thigh=rth, knee=rk, spread=-5); return p
        poses = [(0.0, P(3, -6, 68, 14, -15, 55, (-25, 0, -10), 6, -4, 8, 6), 0, 8),
                 (0.30, P(-12, 25, -60, 20, -40, 110, (35, 0, -10), 14, -18, 14, 12), -0.05, 24),     # marteau levé au-dessus de la tête
                 (0.48, P(28, -10, 40, 85, -20, 20, (-35, 0, 0), 36, -26, 38, 12), 0.15, -6),        # frappe verticale
                 (0.62, P(32, -12, 55, 70, -20, 15, (-40, 0, 0), 38, -26, 42, 14), 0.16, -8),
                 (1.05, P(3, -6, 68, 14, -15, 55, (-25, 0, -10), 6, -4, 8, 6), 0, 8)]
        ks = [key(t, p, hips=(0, -0.03 - 0.07 * (hz > 0.1), hz), hips_rot=(0, ry, 0)) for t, p, hz, ry in poses]
        return build_qclip('Attack', ks, BONES(sk))


def build(bank, female=False):
    sk = H.build()
    a = Asset(NAME + ('_F' if female else '_M'), sk)
    body, marks = MOD.base(sk, a, bank, 'THF' if female else 'THM', female=female, look=dict(hair='long01', hair_tint=(0.30, 0.20, 0.10), skin='young_caucasian_female2') if female else dict(extra=[('universal-male-young-maxmuscle-averageweight', 0.7), ('stomach-pregnant-decr', 0.5), ('stomach-tone-incr', 0.7)],
                                                        skin='young_caucasian_male', hair='short02', hair_tint=(0.24, 0.15, 0.08),
                                                        skin_kw=dict(pubic=0.75, pubic_col=(0.12, 0.08, 0.05), scars=[((0.030, 1.735, 0.10), (0.25, -1, 0.1), 0.05)])))
    with FL.fitted(body, sk, female=female):
        armor.build(sk, a, remap=REMAP, helmet=False, skip=('gorgerin0', 'gorgerin_bord', 'col'))
    cape.build(sk, a, mat='TH_Cloth', clasp='TH_Gold', length=1.0, width=1.05)
    cape.build_tabard(sk, a, mat='TH_Cloth', length=1.25, width=1.25)
    MOD.assign(a, MOD.KNIGHT_RULES)
    MOD.covers(a, MOD.KNIGHT_COVERS)
    anims.build(sk, a, HammerStyle())
    sk_clips = SA.build(sk, a, anims._STYLE, None)
    return a, {'SunHammer': hammer(), 'SunPavise': pavise()}, anims.CLIPS + sk_clips
