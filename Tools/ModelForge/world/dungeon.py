"""Kit de la Crypte de Korvalune (premier donjon, sous le vieux cimetière) : dalles de sol, murs de pierre à assises
irrégulières, arche, pilier, sarcophage, brasero, applique de torche, escalier. Pièces modulaires sur une grille de 4 m,
assemblées dans Unity par Korvalune.EditorTools.DungeonBuilder. Murs bas (3,2 m) : la caméra plonge comme dans Diablo."""
import numpy as np
from forge.rig import Asset
from forge.geo import Mesh, Loft, spline, curve, concat, unit
from world.props import box, cylinder
from world import materials as WM
from forge.texgen import fbm, n01

STONE, FLAG, IRON, WOOD, DARK, BONE = 'D_Stone', 'D_Flag', 'D_Iron', 'W_Wood', 'W_Dark', 'D_Bone'
TILE = 4.0
WALL_H = 3.2


def _static(m):
    m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1)); return m


def floor(seed=0):
    """Dalle de 4 × 4 m : 9 à 12 pierres plates de tailles variées, légèrement inclinées, joints creux."""
    r = np.random.default_rng(seed); parts = []
    # découpe en bandes puis en pierres de longueurs variables
    z = -TILE / 2
    while z < TILE / 2 - 0.05:
        dz = min(r.uniform(0.9, 1.5), TILE / 2 - z); x = -TILE / 2
        while x < TILE / 2 - 0.05:
            dx = min(r.uniform(0.8, 1.7), TILE / 2 - x)
            h = r.uniform(0.10, 0.16); g = 0.035
            slab = box((x + dx / 2, -h / 2 + r.uniform(-0.012, 0.012), z + dz / 2), (dx - g, h, dz - g), rot=r.uniform(-0.015, 0.015), uv=0.5)
            # bords émoussés : on resserre légèrement le dessus
            top = slab.V[:, 1] > slab.V[:, 1].mean()
            ctr = np.array([x + dx / 2, 0, z + dz / 2])
            slab.V[top, 0] = ctr[0] + (slab.V[top, 0] - ctr[0]) * (1 - 0.03 / dx)
            slab.V[top, 2] = ctr[2] + (slab.V[top, 2] - ctr[2]) * (1 - 0.03 / dz)
            parts.append(slab); x += dx
        z += dz
    bed = box((0, -0.2, 0), (TILE, 0.12, TILE), uv=0.5)          # mortier sombre sous les dalles
    a = Asset(f'D_Sol{seed}'); a.add('dalles', _static(concat(parts)), FLAG); a.add('mortier', _static(bed), DARK)
    return a


def wall(seed=0, length=TILE):
    """Mur de 4 m : assises de blocs de hauteurs et profondeurs variées (relief réel), couronnement usé."""
    r = np.random.default_rng(seed); parts = []; y = 0.0
    while y < WALL_H - 0.05:
        bh = min(r.uniform(0.38, 0.6), WALL_H - y); x = -length / 2 + r.uniform(0, 0.4) - 0.4
        while x < length / 2:
            bw = r.uniform(0.7, 1.3); x0 = max(x, -length / 2); x1 = min(x + bw, length / 2)
            if x1 - x0 > 0.08:
                d = 0.7 + r.uniform(-0.05, 0.06)                     # blocs en saillie ou en retrait
                blk = box(((x0 + x1) / 2, y + bh / 2, r.uniform(-0.025, 0.03)), (x1 - x0 - 0.03, bh - 0.03, d), uv=0.5)
                parts.append(blk)
            x += bw
        y += bh
    core = box((0, WALL_H / 2, 0), (length, WALL_H, 0.6), uv=0.5)
    a = Asset(f'D_Mur{seed}'); a.add('blocs', _static(concat(parts)), STONE); a.add('coeur', _static(core), DARK)
    return a


def pillar():
    """Pilier à base moulurée, fût octogonal, chapiteau."""
    parts = [box((0, 0.15, 0), (1.0, 0.3, 1.0), uv=0.5), box((0, 0.38, 0), (0.85, 0.16, 0.85), uv=0.5),
             cylinder((0, 0.46, 0), 0.32, WALL_H - 0.9, segs=8, uv=0.5),
             box((0, WALL_H - 0.36, 0), (0.85, 0.16, 0.85), uv=0.5), box((0, WALL_H - 0.14, 0), (1.0, 0.28, 1.0), uv=0.5)]
    a = Asset('D_Pilier'); a.add('pilier', _static(concat(parts)), STONE); return a


def arch():
    """Passage voûté de 4 m : deux piédroits et un arc en plein cintre de claveaux."""
    parts = [box((-1.55, WALL_H / 2, 0), (0.9, WALL_H, 0.75), uv=0.5), box((1.55, WALL_H / 2, 0), (0.9, WALL_H, 0.75), uv=0.5)]
    n = 11; R0, R1, yc = 1.1, 1.55, 1.9
    for k in range(n):
        a0 = np.pi * k / n; a1 = np.pi * (k + 1) / n; am = (a0 + a1) / 2
        c = np.array([np.cos(am) * (R0 + R1) / 2, yc + np.sin(am) * (R0 + R1) / 2, 0])
        v = box(c, (R1 - R0, (a1 - a0) * (R0 + R1) / 2 - 0.03, 0.8), rot=0, uv=0.5)
        # oriente le claveau selon le rayon
        ang = am - np.pi / 2; ca, sa = np.cos(ang), np.sin(ang)
        P = v.V - c; v.V = np.stack([P[:, 0] * ca - P[:, 1] * sa, P[:, 0] * sa + P[:, 1] * ca, P[:, 2]], 1) + c
        parts.append(v)
    parts.append(box((0, WALL_H - 0.2, 0), (4.0, 0.4, 0.7), uv=0.5))
    a = Asset('D_Arche'); a.add('arche', _static(concat(parts)), STONE); return a


def sarcophagus(seed=0):
    r = np.random.default_rng(seed)
    body = box((0, 0.45, 0), (1.0, 0.9, 2.3), uv=0.5)
    lid = box((0, 0.98, 0), (1.1, 0.16, 2.4), uv=0.5)
    ridge = box((0, 1.1, 0), (0.5, 0.1, 2.0), uv=0.5)
    feet = [box((sx * 0.42, 0.05, sz * 1.02), (0.22, 0.1, 0.22), uv=0.5) for sx in (-1, 1) for sz in (-1, 1)]
    a = Asset(f'D_Sarcophage{seed}'); a.add('cuve', _static(concat([body] + feet)), STONE); a.add('couvercle', _static(concat([lid, ridge])), FLAG)
    return a


def brazier():
    bowl = Loft(spline([(0, 0.9, 0), (0, 1.15, 0)], 6), curve([(0, 0.18), (0.5, 0.38), (1, 0.42)], 6), curve([(0, 0.18), (0.5, 0.38), (1, 0.42)], 6), p=2, segs=16, cap0=1).mesh(0.5)
    legs = [box((0.28 * np.cos(t), 0.45, 0.28 * np.sin(t)), (0.06, 0.9, 0.06), rot=-t, uv=0.5) for t in np.linspace(0, 2 * np.pi, 4)[:3]]
    coals = cylinder((0, 1.05, 0), 0.34, 0.06, segs=12, uv=0.5)
    a = Asset('D_Brasero'); a.add('fer', _static(concat([bowl] + legs)), IRON); a.add('braises', _static(coals), DARK); return a


def sconce():
    """Applique de torche : platine et bras de fer, torche de bois (la flamme est un effet de particules dans Unity)."""
    plate = box((0, 2.0, 0.02), (0.18, 0.32, 0.04), uv=0.5)
    arm = box((0, 1.95, 0.14), (0.05, 0.05, 0.26), uv=0.5)
    ring = cylinder((0, 1.92, 0.26), 0.07, 0.06, segs=10, uv=0.5)
    torch = cylinder((0, 1.75, 0.26), 0.035, 0.55, segs=8, uv=0.5, r_top=0.05)
    a = Asset('D_Torche'); a.add('fer', _static(concat([plate, arm, ring])), IRON); a.add('bois', _static(torch), WOOD); return a


def stairs():
    """Escalier d'entrée (descente depuis le cimetière) : 8 marches de 4 m de large."""
    parts = [box((0, 0.1 + k * 0.2, -1.6 + k * 0.4), (3.6, 0.2 + k * 0.4, 0.42), uv=0.5) for k in range(8)]
    parts += [box((sx * 1.95, 1.0, 0), (0.3, 2.0, 3.6), uv=0.5) for sx in (-1, 1)]
    a = Asset('D_Escalier'); a.add('marches', _static(concat(parts)), FLAG); return a


def bones(seed=0):
    """Tas d'ossements épars (crânes et os longs stylisés)."""
    r = np.random.default_rng(seed); parts = []
    for k in range(9):
        L = r.uniform(0.25, 0.45); c = (r.uniform(-0.5, 0.5), 0.03, r.uniform(-0.5, 0.5))
        parts.append(box(c, (0.05, 0.05, L), rot=r.uniform(0, np.pi), uv=0.5))
    for k in range(2):
        c = (r.uniform(-0.4, 0.4), 0.09, r.uniform(-0.4, 0.4)); parts.append(cylinder((c[0], 0, c[2]), 0.1, 0.18, segs=10, uv=0.5, r_top=0.07))
    a = Asset(f'D_Os{seed}'); a.add('os', _static(concat(parts)), BONE); return a


# ------------------------------------------------------------------------------------------- matériaux
def flagstones(S=1024, seed=2001):
    """Dalles de crypte : pierre sombre usée, polie au centre par les pas, salpêtre et mousse dans les joints."""
    r = np.random.default_rng(seed)
    g = n01(fbm(S, S, r, base=S / 10, octaves=5)); fine = n01(fbm(S, S, r, base=S / 80, octaves=3))
    wear = n01(fbm(S, S, r, base=S / 4, octaves=3))
    col = np.array([0.30, 0.29, 0.28])[None, None] * (0.75 + 0.35 * g[..., None]) * (0.9 + 0.2 * fine[..., None])
    nitre = np.clip((n01(fbm(S, S, r, base=S / 20, octaves=4)) - 0.68) * 3, 0, 1)
    col = col * (1 - nitre[..., None] * 0.2) + np.array([0.55, 0.55, 0.50]) * nitre[..., None] * 0.2
    hgt = 0.8 * g + 0.4 * fine
    return WM._tex(col, hgt, 0.25 + 0.3 * wear, 0.8, 5, 0.12)


def bone_tex(S=256, seed=2002):
    r = np.random.default_rng(seed); g = n01(fbm(S, S, r, base=S / 8, octaves=4))
    col = np.array([0.74, 0.69, 0.58])[None, None] * (0.8 + 0.25 * g[..., None])
    return WM._tex(col, g, 0.3, 0.5, 3, 0.1)


RECIPES = {
    'D_Stone': (lambda S: WM.stone_blocks(S, seed=2003, rows=6, tone=(0.40, 0.38, 0.35)), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'D_Flag':  (lambda S: flagstones(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'D_Iron':  (lambda S: __import__('knight.materials', fromlist=['x']).steel(S), 512, 256, dict(metallic=1.0, smoothness=1.0)),
    'D_Bone':  (lambda S: bone_tex(S), 256, 128, dict(metallic=0.0, smoothness=1.0)),
}


def pieces():
    out = [floor(0), floor(1), floor(2), wall(0), wall(1), wall(2), pillar(), arch(), sarcophagus(0), brazier(), sconce(), stairs(), bones(0), bones(1)]
    return out
