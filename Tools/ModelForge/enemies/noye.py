"""Noyé des Abysses — mort-vivant de la Côte des Naufrages : marin englouti au teint blafard et verdâtre, veines
bleutées, yeux d'un vert d'eau luisant ; vareuse et longs pans de drap de mer détrempés et déchirés, ceinturon de cuir
racorni, ferrures rongées de vert-de-gris ; grappes de balanes et de coquillages incrustées sur les épaules, les
avant-bras et les tibias, lanières d'algues qui pendent des épaules, de la ceinture et des bras ; harpon de
baleinier rouillé à pointe barbelée, hampe enroulée de cordage."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, humanparts as HP
from forge.geo import Loft, spline, curve, unit, concat, Mesh, vertex_normals
from knight import anims, materials as KM
from heroes import female_kit as FK, modular as MOD
from heroes.female_kit import _blob
from enemies import materials as EM

NAME = 'NoyeAbysses'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.66, 0.05)
RECIPES = {
    'NO_Robe':    (lambda S: EM.tinted(KM.tooled_leather(S, seed=1101, tone=(0.06, 0.09, 0.10), line=(0.25, 0.32, 0.26), n=30), (1, 1, 1)), 1024, 512, dict(metallic=0.1, smoothness=1.0)),
    'NO_Sleeve':  (lambda S: KM.gambeson(S, seed=1102, color=(0.10, 0.14, 0.16)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'NO_Cloth':   (lambda W, H_: KM.cloak(W, H_, seed=1103, color=(0.08, 0.13, 0.15), thread_color=(0.30, 0.36, 0.28), mud_amt=2.6), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'NO_Cloak':   (lambda W, H_: KM.cloak(W, H_, seed=1104, color=(0.06, 0.10, 0.09), thread_color=(0.20, 0.26, 0.18), mud_amt=3.0), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'NO_Gold':    (lambda S: KM.brass(S, seed=1105, gold=(0.30, 0.42, 0.30), dark=(0.05, 0.08, 0.06)), 1024, 256, dict(metallic=0.8, smoothness=1.0)),   # bronze au vert-de-gris
    'NO_Metal':   (lambda S: KM.steel(S, seed=1106, tone=(0.22, 0.20, 0.18), polish=0.3, rust_amt=3.2, grime_amt=2.0), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'NO_Leather': (lambda S: KM.leather(S, seed=1107, tone=(0.08, 0.06, 0.04)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'NO_Crystal': (lambda S: KM.crystal(S, seed=1108, color=(0.25, 0.85, 0.75), glow=0.8), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(1.4, 1.4, 1.4), emission_preview=(0.8, 0.8, 0.8))),
    'NO_Dark':    (lambda S: KM.gambeson(S, seed=1109, color=(0.01, 0.015, 0.015)), 256, 128, dict(metallic=0.0, smoothness=0.4)),
    'NO_Bone':    (lambda S: KM.bone(S, seed=1110, tone=(0.55, 0.55, 0.47)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'NO_Wood':    (lambda S: EM.tinted(KM.wood(S, seed=1111), (0.45, 0.45, 0.40)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'NO_Balane':  (lambda S: EM.barnacle(S, seed=1112), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'NO_Algue':   (lambda W, H_: EM.seaweed(W, H_, seed=1113), (512, 1024), (128, 256), dict(metallic=0.0, smoothness=1.0)),
    'NO_Corde':   (lambda S: KM.gambeson(S, seed=1114, color=(0.36, 0.31, 0.22)), 512, 128, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'Harpon')]
M = dict(corset='NO_Robe', sleeve='NO_Sleeve', cloth='NO_Cloth', cloak='NO_Cloak', gold='NO_Gold', metal='NO_Metal', leather='NO_Leather',
         crystal='NO_Crystal', dark='NO_Dark', bone='NO_Bone', wood='NO_Wood')


def harpoon(name='Harpon'):
    """Harpon de baleinier : hampe de bois blanchi par le sel, fer à pointe barbelée et douille rivetée, cordage enroulé
    et nœud, dent de squale attachée. Prise à l'origine, pointe vers +Y."""
    a = Asset(name)
    t = np.linspace(0, 1, 40)
    pts = np.c_[0.004 * np.sin(t * 7), -0.70 + t * 1.75, 0.003 * np.cos(t * 5)]
    a.add('hampe', Loft(pts, 0.017, 0.017, p=2.2, segs=12, cap0=1).mesh(0.25), 'NO_Wood')
    top = pts[-1]
    a.add('douille', Loft(spline([top - (0, 0.10, 0), top + (0, 0.02, 0)], 6), curve([(0, .019), (1, .015)], 6), curve([(0, .019), (1, .015)], 6), p=2.2, segs=12, cap1=1).mesh(0.05), 'NO_Metal')
    tip = Loft(spline([top + (0, 0.02, 0), top + (0, 0.12, 0), top + (0, 0.24, 0)], 12), curve([(0, .012), (.3, .028), (1, .001)], 12), curve([(0, .006), (.3, .006), (1, .0008)], 12),
               p=1.6, segs=6, ref=(0, 0, 1), cap0=1, cap1=1)
    a.add('fer', tip.mesh(0.05), 'NO_Metal')
    for sx in (1, -1):   # barbillons recourbés vers l'arrière
        b = top + (0.020 * sx, 0.08, 0)
        a.add(f'barbillon{sx}', Loft(spline([b, b + (0.030 * sx, -0.020, 0), b + (0.036 * sx, -0.065, 0)], 8), curve([(0, .006), (1, .001)], 8), curve([(0, .003), (1, .0008)], 8), p=1.8, segs=6, cap1=1).mesh(0.05), 'NO_Metal')
    # cordage enroulé sur la hampe puis qui pend
    v = [np.array([0.022 * np.cos(q * 40), 0.25 + q * 0.18, 0.022 * np.sin(q * 40)]) for q in np.linspace(0, 1, 80)]
    a.add('corde', Loft(np.array(v), 0.0045, 0.0045, p=2, segs=6).mesh(0.05), 'NO_Corde')
    hang = spline([(0.02, 0.25, 0), (0.05, 0.12, 0.02), (0.04, -0.05, 0.03), (0.07, -0.20, 0.01)], 20)
    a.add('corde_pendante', Loft(hang, 0.004, 0.004, p=2, segs=6, cap1=1).mesh(0.05), 'NO_Corde')
    return a


def barnacles(body, sk, rng, zones):
    """Grappes de balanes (cônes tronqués percés) sur la peau et le drap : chaque grappe = 5 à 12 coquilles serrées."""
    V, N = body.V, vertex_normals(body)
    out = []
    for c, r, n in zones:
        near = np.where(((V - np.asarray(c)) ** 2).sum(1) < r * r)[0]
        if len(near) == 0: continue
        for k in range(n):
            i = rng.choice(near); nn = N[i]; p = V[i] + nn * 0.010          # posées sur le drap
            s = rng.uniform(0.010, 0.024)
            sh = Loft(spline([p - nn * 0.002, p + nn * s * 0.9], 4), curve([(0, s), (1, s * 0.45)], 4), curve([(0, s * rng.uniform(0.8, 1.0)), (1, s * 0.4)], 4), p=2.0, segs=9, ref=unit(np.cross(nn, (0, 1, 0)) + 1e-3)).mesh(0.03)
            out.append(sh)
    m = concat(out)
    return HP.weights_from(body, m)


def strands(body, sk, rng, anchors, length, width):
    """Lanières d'algues : rubans qui tombent sous la gravité depuis des points d'ancrage, ondulent et s'effilent."""
    out = []
    from scipy.spatial import cKDTree
    tree = cKDTree(body.V); N = vertex_normals(body)
    for p0 in anchors:
        p0 = np.asarray(p0, float); i = tree.query(p0)[1]; p0 = body.V[i] + N[i] * 0.022     # par-dessus les vêtements
        L = length * rng.uniform(0.6, 1.2)
        pts = [p0 + (0.012 * np.sin(q * 9 + rng.uniform(0, 6)), -L * q, 0.010 * np.cos(q * 7) + 0.01 * q) for q in np.linspace(0, 1, 14)]
        w = curve([(0, width), (0.6, width * 0.8), (1, width * 0.15)], 14)
        lo = Loft(spline(pts, 14), w, curve([(0, 0.0012), (1, 0.0008)], 14), p=2.0, segs=4, ref=(0, 0, 1))
        out.append(lo.mesh(0.1))
    return HP.weights_from(body, concat(out))


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); rng = np.random.default_rng(1100)
    body, marks = MOD.base(sk, a, bank, 'NO', female=False, look=dict(
        skin='middleage_caucasian_male', skin_tint=EM.drowned_skin_tint(), hair='short02', hair_tint=(0.05, 0.06, 0.05),
        brows='eyebrow001', eye_color=(0.35, 0.95, 0.80), glow=1.2,
        skin_kw=dict(pale=0.15, makeup=(0.03, 0.07, 0.07), veins=(0.18, 0.36, 0.42), dirt=1.6, pubic=0.5)))
    a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'SousVetementHaut')]
    # chemise de marin en lambeaux : toile sombre détrempée
    bank.recipes['NO_Rags'] = (lambda S: MOD.rag_cloth(S, 1131, (0.11, 0.15, 0.16), patches=True, dirt=1.6), 1024, 256, dict(metallic=0.0, smoothness=1.0))
    FK.build_outfit(a, body, marks, sk, 'NO', M, dict(male=True, front_panel=False, spikes=False, skulls=False, tatter=1.6, collar=False, cloak=True,
                                                    cloak_len=0.95, cloak_tatter=2.4, neck=3.2))
    # balanes : épaules, avant-bras, tibias, flanc
    V = body.V
    zones = [((0.20 * sx, 1.47, 0.02), 0.08, 16) for sx in (1, -1)] + [((0.55 * sx, 1.44, 0.0), 0.07, 12) for sx in (1, -1)] + \
            [((0.11 * sx, 0.40, 0.05), 0.07, 12) for sx in (1, -1)] + [((0.14, 1.12, 0.10), 0.06, 10), ((-0.06, 1.30, 0.12), 0.05, 8)]
    a.add('balanes', barnacles(body, sk, rng, zones), 'NO_Balane', slot='Balanes')
    # algues : des épaules, de la ceinture, des avant-bras
    anchors = [(0.17 * sx, 1.48, 0.02 + 0.03 * k) for sx in (1, -1) for k in range(3)] + [(0.08 * sx, 1.36, 0.12) for sx in (1, -1)] + \
              [(0.20 * np.sin(t), 1.00, 0.13 * np.cos(t)) for t in np.linspace(-1.4, 1.4, 7)] + [(0.30 * sx, 1.46, 0.03) for sx in (1, -1)]
    a.add('algues', strands(body, sk, rng, anchors, 0.34, 0.030), 'NO_Algue', slot='Algues')
    MOD.assign(a, [])
    MOD.covers(a, FK.COVERS)
    anims.build(sk, a)
    return a, {'Harpon': harpoon()}, anims.CLIPS
