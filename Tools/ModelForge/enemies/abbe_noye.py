"""Abbé noyé — prélat englouti de la Côte des Naufrages (d'après la planche de concepts) : grand et décharné, crâne
de noyé verdi sous une haute mitre déchirée cerclée de bronze rongé ; une barbe de tentacules de pieuvre pourpres,
ventouses en dessous, qui pend jusqu'à la poitrine et s'enroule ; chasuble et étole pourries, détrempées, aux bords en
lambeaux, croix de bronze ; épaules couvertes de balanes ; le bras GAUCHE remplacé par une énorme pince de crabe
(carapace segmentée rouge et brune, tubercules, épines) ; crosse de bois flotté recourbée en volute d'où pend une
lanterne-cage contenant un leurre lumineux de baudroie ; algues qui pendent de partout."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, sculpt as SC, humanparts as HP
from forge.geo import Loft, spline, curve, unit, concat, rigid, mirror, vertex_normals
from knight import anims, materials as KM
from heroes import female_kit as FK, modular as MOD
from heroes.female_kit import _blob
from enemies import materials as EM, bones as BN, noye as NO
from enemies.common import template_body
from knight import cape

NAME = 'AbbeNoye'
FOLDER = 'Enemies'
SCALE = 1.12
HEAD_VIEW = (0, 1.70, 0.06)
RECIPES = {
    'AN_Robe':    (lambda S: KM.tooled_leather(S, seed=1701, tone=(0.12, 0.10, 0.06), line=(0.42, 0.40, 0.22), n=36), 1024, 512, dict(metallic=0.15, smoothness=1.0)),
    'AN_Sleeve':  (lambda S: KM.gambeson(S, seed=1702, color=(0.13, 0.12, 0.08)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'AN_Cloth':   (lambda S: MOD.rag_cloth(S, 1703, (0.30, 0.25, 0.16), patches=True, dirt=1.8), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'AN_Rag':     (lambda S: MOD.rag_cloth(S, 1716, (0.22, 0.20, 0.13), patches=True, dirt=2.2), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'AN_Cloak':   (lambda W, H_: KM.cloak(W, H_, seed=1704, color=(0.17, 0.15, 0.10), thread_color=(0.30, 0.34, 0.20), mud_amt=3.4), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'AN_Gold':    (lambda S: KM.brass(S, seed=1705, gold=(0.36, 0.46, 0.30), dark=(0.06, 0.09, 0.06)), 1024, 256, dict(metallic=0.85, smoothness=1.0)),
    'AN_Metal':   (lambda S: KM.steel(S, seed=1706, tone=(0.22, 0.20, 0.18), polish=0.3, rust_amt=3.0, grime_amt=2.0), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'AN_Leather': (lambda S: KM.leather(S, seed=1707, tone=(0.07, 0.06, 0.04)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'AN_Crystal': (lambda S: KM.crystal(S, seed=1708, color=(0.35, 0.95, 0.55), glow=1.0), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.9, 0.9, 0.9))),
    'AN_Dark':    (lambda S: KM.gambeson(S, seed=1709, color=(0.01, 0.012, 0.01)), 256, 128, dict(metallic=0.0, smoothness=0.4)),
    'AN_Bone':    (lambda S: EM.tinted(KM.bone(S, seed=1710, tone=(0.52, 0.55, 0.44)), (0.92, 1.0, 0.9)), 1024, 512, dict(metallic=0.0, smoothness=0.8)),
    'AN_Hollow':  (lambda S: KM.void(S), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    'AN_Eye':     (lambda S: EM.glow(S, (0.40, 1.0, 0.60)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(3.5, 3.5, 3.5), emission_preview=(2.2, 2.2, 2.2))),
    'AN_Tentacle': (lambda S: EM.tentacle_flesh(S, seed=1711, tone=(0.30, 0.22, 0.25), belly=(0.36, 0.27, 0.29), cups=False), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'AN_Crab':    (lambda S: EM.crab_shell(S, seed=1712, tone=(0.46, 0.17, 0.10), dark=(0.13, 0.05, 0.04)), 2048, 1024, dict(metallic=0.0, smoothness=1.0)),
    'AN_Balane':  (lambda S: EM.barnacle(S, seed=1713), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'AN_Algue':   (lambda W, H_: EM.seaweed(W, H_, seed=1714), (512, 1024), (128, 256), dict(metallic=0.0, smoothness=1.0)),
    'AN_Wood':    (lambda S: EM.tinted(KM.wood(S, seed=1715), (0.58, 0.58, 0.52)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'AN_Lure':    (lambda S: EM.glow(S, (0.85, 1.0, 0.45)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(5.0, 5.0, 5.0), emission_preview=(3.0, 3.0, 3.0))),
}
GEAR = [('Socket_Weapon_R', 'CrosseNoyee')]
M = dict(corset='AN_Robe', sleeve='AN_Sleeve', cloth='AN_Cloth', cloak='AN_Cloak', gold='AN_Gold', metal='AN_Metal', leather='AN_Leather',
         crystal='AN_Crystal', dark='AN_Dark', bone='AN_Bone', wood='AN_Wood')


def mitre(sk, top, cz):
    """Mitre haute à deux pans pointus (avant et arrière), déchirée sur un côté, bandeau et galon vertical de bronze,
    croix sur le devant, deux fanons qui pendent derrière."""
    out = []
    t = np.linspace(0, 1, 30)
    y = top - 0.075 + t * 0.34
    rx = 0.106 * np.clip(1 - t ** 1.7, 0, 1) ** 0.65 + 0.003                         # de face : arc brisé (épaules arrondies puis pointe)
    rz = 0.100 * (1 - t) ** 1.6 + 0.022                                        # de profil : mince (deux pans)
    pts = np.c_[np.zeros_like(t), y, np.full_like(t, cz) - 0.01 * t]
    body = Loft(pts, rx, rz, p=2.6, segs=20, ref=(1, 0, 0), cap0=0, cap1=1).mesh(0.1)
    # entaille déchirée sur le côté droit, fendue du haut
    body.V[:, 1] -= 0.03 * np.clip((body.V[:, 1] - (top + 0.12)) / 0.15, 0, 1) * (body.V[:, 0] < -0.02)
    out.append(('mitre', body, 'AN_Robe'))
    band = Loft(np.array([[0.102 * np.cos(q), top - 0.058, cz + 0.106 * np.sin(q)] for q in np.linspace(0, 2 * np.pi, 40)]), 0.034, 0.008, p=4, segs=6, ref=(0, 1, 0)).mesh(0.1)
    out.append(('mitre_bandeau', band, 'AN_Gold'))
    stripe = Loft(spline([(0, top - 0.05, cz + 0.102), (0, top + 0.10, cz + 0.085), (0, top + 0.26, cz + 0.035)], 12), 0.012, 0.004, p=4, segs=6, ref=(0, 0, 1)).mesh(0.1)
    out.append(('mitre_galon', stripe, 'AN_Gold'))
    cross = concat([Loft(spline([(0, top + 0.02, cz + 0.098), (0, top + 0.13, cz + 0.088)], 4), 0.008, 0.005, p=4, segs=6, cap0=1, cap1=1).mesh(0.05),
                    Loft(spline([(-0.035, top + 0.095, cz + 0.093), (0.035, top + 0.095, cz + 0.093)], 4), 0.008, 0.005, p=4, segs=6, cap0=1, cap1=1).mesh(0.05)])
    out.append(('mitre_croix', cross, 'AN_Gold'))
    out.append(('mitre_joyau', _blob(np.array([0, top - 0.058, cz + 0.112]), (0.016, 0.020, 0.010), n=12), 'AN_Crystal'))
    for sx in (1, -1):
        f = Loft(spline([(0.04 * sx, top - 0.06, cz - 0.10), (0.05 * sx, top - 0.16, cz - 0.12), (0.055 * sx, top - 0.32, cz - 0.13)], 10),
                 curve([(0, 0.022), (1, 0.018)], 10), 0.003, p=4, segs=4, ref=(0, 0, 1), cap1=1).mesh(0.05)
        out.append((f'mitre_fanon{sx}', f, 'AN_Cloth'))
    res = []
    for n, m, mat in out: rigid(m, sk['Head']); res.append((n, m, mat))
    return res


def tentacles(sk, skull, rng):
    """Barbe de tentacules : neuf bras charnus qui partent du bas du visage, pendent sur la poitrine et s'enroulent
    au bout ; épais à la racine, effilés ; ventouses en relief sous chaque bras."""
    V = skull.V
    front = V[V[:, 2] > np.percentile(V[:, 2], 80)]
    jaw_y = np.percentile(front[:, 1], 8); jaw_z = front[:, 2].max() - 0.02
    arms, suck = [], []
    for k, x in enumerate(np.linspace(-0.060, 0.060, 15)):
        root = np.array([x, jaw_y + 0.015 + 0.010 * (1 - abs(x) / 0.05), jaw_z - 0.015 * abs(x) / 0.05])
        L = 0.26 + 0.26 * (1 - abs(x) / 0.06) + rng.uniform(-0.06, 0.06)
        side = np.sign(x) if abs(x) > 1e-3 else rng.choice([-1, 1])
        spread = x * 2.4 + rng.uniform(-0.03, 0.03)
        ph = rng.uniform(0, 6.28); turns = rng.uniform(1.4, 2.8)
        q = np.linspace(0, 1, 28)
        curl = np.clip((q - 0.55) / 0.45, 0, 1)
        pts = np.c_[root[0] + spread * q + side * 0.06 * curl * np.sin(curl * np.pi * turns) + 0.018 * np.sin(q * 6 + ph),
                    root[1] - L * q + 0.07 * curl * (1 - np.cos(curl * np.pi * turns)),
                    root[2] + 0.06 * q - 0.07 * q ** 2 + 0.05 * curl * np.cos(curl * np.pi * turns) + 0.012 * np.cos(q * 5 + ph)]
        r = 0.019 * (1 - q) ** 0.9 + 0.0022
        lo = Loft(spline(pts, 28), r, r * 0.88, p=2.2, segs=14, cap1=1)
        m = lo.mesh(0.1)
        m.UV = np.c_[m.UV[:, 0], m.UV[:, 1] * 3]                                 # ventouses de la texture le long du bras
        arms.append(m)
        for j in range(3, 26, 2):                                                 # ventouses en relief, face intérieure (vers le corps)
            c = lo.path[j]; rr = r[j] * 0.42
            suck.append(_blob(c + np.array([0, 0, -1]) * r[j] * 0.80, (rr, rr, rr * 0.55), n=8))
    a = concat(arms); s = concat(suck)
    rigid(a, sk['Head']); rigid(s, sk['Head'])
    return a, s


def crab_claw(sk, rng):
    """Bras gauche en pince de crabe (pose en T, le long de +X) : avant-bras cuirassé en trois anneaux de carapace,
    poignet articulé, énorme paume bombée, doigt fixe et doigt mobile recourbés, bords dentés ; épines et tubercules."""
    la = np.array(H.ARM['LowerArm']); hd = np.array(H.ARM['Hand'])
    s = SC.Sculpt(k=0.012)
    for k in range(3):                                                            # anneaux de carapace de l'avant-bras
        x0 = la[0] + 0.02 + k * 0.08
        s.ell('LeftLowerArm', (x0 + 0.04, la[1] + 0.002, la[2]), (0.055, 0.068 + 0.006 * k, 0.066 + 0.006 * k), fwd=(1, 0, 0), k=0.008)
    s.ell('LeftHand', (hd[0] + 0.01, hd[1], hd[2]), (0.048, 0.068, 0.066))           # poignet
    s.ell('LeftHand', (hd[0] + 0.17, hd[1] + 0.008, hd[2] + 0.005), (0.20, 0.12, 0.075), fwd=(1, 0, 0), k=0.02)   # paume (propode)
    s.ell('LeftHand', (hd[0] + 0.20, hd[1] + 0.07, hd[2]), (0.12, 0.035, 0.05), fwd=(1, 0, 0), k=0.01)               # arête dorsale
    m = s.mesh(res=0.008, smooth=3); s.skin(m, sk, kw=0.02)
    # tubercules et épines sur la carapace
    N = vertex_normals(m); spikes = []
    idx = rng.choice(len(m.V), 160, replace=False)
    for i in idx:
        if N[i][1] < -0.2: continue
        p = m.V[i]; n = N[i]; h = rng.uniform(0.012, 0.040)
        spikes.append(Loft(spline([p - n * 0.003, p + n * h], 4), curve([(0, h * 0.35), (1, 0.001)], 4), curve([(0, h * 0.35), (1, 0.001)], 4), p=2, segs=6, cap1=1).mesh(0.03))
    sp = concat(spikes); sp = HP.weights_from(m, sp)
    # doigts : fixe (dessous) et mobile (dessus), recourbés l'un vers l'autre, bords internes dentés
    base = np.array([hd[0] + 0.33, hd[1], hd[2] + 0.005])
    fingers = []
    for up, L, bend in ((-1, 0.34, 0.06), (1, 0.31, 0.09)):
        q = np.linspace(0, 1, 18)
        pts = np.c_[base[0] + L * q, base[1] + up * (0.030 + 0.02 * q - bend * q ** 2), base[2] + 0.01 * np.sin(q * 3)]
        r = 0.066 * (1 - q) ** 0.7 + 0.005
        f = Loft(spline(pts, 18), r * 0.9, r, p=2.3, segs=12, cap1=1).mesh(0.08)
        # dents sur le bord interne
        for j in range(3, 15, 2):
            c = pts[j] + np.array([0, -up * r[j] * 0.85, 0])
            f = concat([f, Loft(spline([c, c + (0.004, -up * 0.016, 0)], 3), curve([(0, .006), (1, .001)], 3), curve([(0, .006), (1, .001)], 3), p=2, segs=5, cap1=1).mesh(0.02)])
        rigid(f, sk['LeftHand']); fingers.append(f)
    SC.box_uv(m, 0.30)
    for f in fingers: SC.box_uv(f, 0.30)
    return m, sp, concat(fingers)


def crozier(name='CrosseNoyee'):
    """Crosse de bois flotté : hampe torse et noueuse, sommet enroulé en volute ; d'une chaîne pend une lanterne-cage
    (cerclages de bronze, barreaux) qui renferme le leurre lumineux d'une baudroie ; algues entortillées."""
    a = Asset(name)
    t = np.linspace(0, 1, 50)
    pts = np.c_[0.010 * np.sin(t * 9), -0.90 + t * 2.0, 0.008 * np.cos(t * 6)]
    r = 0.020 + 0.004 * np.sin(t * 37) ** 2 + 0.006 * np.exp(-((t - 0.3) / 0.02) ** 2) + 0.005 * np.exp(-((t - 0.62) / 0.03) ** 2)
    m = Loft(pts, r, r * 0.9, p=2.2, segs=12, cap0=2).mesh(0.25); m.UV = np.c_[m.UV[:, 0] + m.UV[:, 1] * 0.6, m.UV[:, 1]]
    a.add('hampe', m, 'AN_Wood')
    top = pts[-1]
    q = np.linspace(0, 1, 40); ang = q * np.pi * 1.6; rad = 0.13 * (1 - 0.55 * q)
    vol = np.c_[top[0] + rad * np.sin(ang), top[1] + rad * (1 - np.cos(ang)) * 0.9 + 0.02, np.full_like(q, top[2])]
    a.add('volute', Loft(spline(vol, 40), curve([(0, .019), (1, .008)], 40), curve([(0, .019), (1, .008)], 40), p=2.2, segs=12, cap1=1).mesh(0.1), 'AN_Wood')
    hang = vol[-1]
    chain = []
    for k in range(6):
        c = hang - np.array([0, 0.025 * k + 0.02, 0])
        chain.append(Loft(np.array([[c[0] + 0.008 * np.cos(u) * (k % 2), c[1] + 0.012 * np.sin(u), c[2] + 0.008 * np.cos(u) * ((k + 1) % 2)] for u in np.linspace(0, 2 * np.pi, 12)]), 0.0025, 0.0025, p=2, segs=5).mesh(0.03))
    a.add('chaine', concat(chain), 'AN_Gold')
    lc = hang - np.array([0, 0.26, 0]); S_ = 1.6
    bars = [Loft(spline([lc + (0.08 * np.cos(u), 0.095, 0.08 * np.sin(u)), lc + (0.095 * np.cos(u), 0, 0.095 * np.sin(u)), lc + (0.08 * np.cos(u), -0.095, 0.08 * np.sin(u))], 8), 0.003, 0.003, p=2, segs=5).mesh(0.03) for u in np.linspace(0, 2 * np.pi, 9)[:-1]]
    a.add('cage', concat(bars), 'AN_Gold')
    for y in (0.10, -0.10):
        a.add(f'cerclage{y}', Loft(np.array([[0.083 * np.cos(u), lc[1] + y, 0.083 * np.sin(u)] + np.array([lc[0], 0, lc[2]]) for u in np.linspace(0, 2 * np.pi, 24)]), 0.006, 0.006, p=2, segs=6).mesh(0.03), 'AN_Gold')
    a.add('leurre', _blob(lc, (0.050, 0.056, 0.050), n=18), 'AN_Lure')
    a.add('leurre_tige', Loft(spline([lc + (0, 0.03, 0), lc + (0.01, 0.055, 0.0), lc + (0, 0.06, 0)], 6), 0.003, 0.003, p=2, segs=5).mesh(0.02), 'AN_Tentacle')
    alg = [Loft(spline([(0.02 * np.cos(u), 0.65 - 0.02 * k, 0.02 * np.sin(u)), (0.03 * np.cos(u), 0.45, 0.03 * np.sin(u) + 0.01), (0.025 * np.cos(u), 0.25 - 0.04 * k, 0.03 * np.sin(u))], 10),
                curve([(0, .012), (1, .002)], 10), 0.001, p=2, segs=4, ref=(0, 0, 1)).mesh(0.05) for k, u in enumerate((0.3, 2.4, 4.2))]
    a.add('algues', concat(alg), 'AN_Algue')
    return a


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); rng = np.random.default_rng(1700)
    body, marks = template_body(sk, a, bank)
    # crâne de noyé (verdi), orbites creuses où brillent deux lueurs vertes
    s, hm = BN.skull(body, sk, marks)
    a.add('crane', s, 'AN_Bone'); a.add('crane_creux', hm, 'AN_Hollow')
    eL, eR = np.asarray(marks['eye_L']), np.asarray(marks['eye_R'])
    for e in (eL, eR):
        g = _blob(e + np.array([0, 0, -0.010]), (0.010, 0.008, 0.006), n=10); rigid(g, sk['Head']); a.add(f'lueur{e[0]:.2f}', g, 'AN_Eye')
    # vêtements : chasuble, étole (pan avant), manches, cape, tout en lambeaux
    FK.build_outfit(a, body, marks, sk, 'AN', M, dict(male=True, front_panel=True, spikes=False, skulls=False, tatter=1.8, collar=True, cloak=True,
                                                    cloak_len=1.42, cloak_tatter=2.2, neck=3.0))
    # longue chasuble jusqu'au sol, devant et derrière, bords déchirés (par-dessus la robe)
    cape.build_tabard(sk, a, mat='AN_Cloak', length=1.48, width=1.30, tatter=1.6, seed=31)
    # bras gauche : plus de gant ni de main (la pince les remplace)
    a.parts = [p for p in a.parts if not (a.slot.get(p[0]) == 'Gants' and np.mean(p[1].V[:, 0]) > 0)]
    top = s.V[:, 1].max(); cz = s.V[s.V[:, 1] > top - 0.03][:, 2].mean()
    for n, m, mat in mitre(sk, top, cz): a.add(n, m, mat, slot='Mitre')
    ten, suck = tentacles(sk, s, rng)
    a.add('barbe_tentacules', ten, 'AN_Tentacle'); a.add('barbe_ventouses', suck, 'AN_Tentacle')
    claw, spk, fingers = crab_claw(sk, rng)
    a.add('pince', claw, 'AN_Crab'); a.add('pince_epines', spk, 'AN_Crab'); a.add('pince_doigts', fingers, 'AN_Crab')
    zones = [((0.20, 1.48, 0.02), 0.09, 26), ((-0.20, 1.48, 0.02), 0.09, 18), ((0.33, 1.47, 0.0), 0.06, 10), ((0.0, 1.42, -0.12), 0.08, 10)]
    a.add('balanes', NO.barnacles(body, sk, rng, zones), 'AN_Balane')
    anchors = [(0.17 * sx, 1.48, 0.0 + 0.03 * k) for sx in (1, -1) for k in range(3)] + [(0.20 * np.sin(t), 1.00, 0.13 * np.cos(t)) for t in np.linspace(-1.2, 1.2, 6)]
    a.add('algues', NO.strands(body, sk, rng, anchors, 0.38, 0.030), 'AN_Algue')
    # lambeaux d'étoffe pourrie : des dizaines de bandes déchirées qui pendent de la ceinture, du torse et des manches
    rag_anchors = [(0.21 * np.sin(t), 1.00 - 0.02 * rng.uniform(), 0.14 * np.cos(t)) for t in np.linspace(-2.9, 2.9, 22)] + \
                  [(0.16 * np.sin(t), 1.30, 0.12 * np.cos(t)) for t in np.linspace(-1.2, 1.2, 8)] + [(0.40 * sx, 1.40, 0.04) for sx in (-1,)]
    a.add('lambeaux', NO.strands(body, sk, rng, rag_anchors, 0.62, 0.060), 'AN_Rag')
    a.add('lambeaux_courts', NO.strands(body, sk, rng, rag_anchors[::2], 0.32, 0.045), 'AN_Cloth')
    MOD.assign(a, [])
    anims.build(sk, a, FK.StaffStyle())
    return a, {'CrosseNoyee': crozier()}, anims.CLIPS
