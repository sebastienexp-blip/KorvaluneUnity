"""Boutiques des marchands de Korvalune (décor seulement ; les marchands et le commerce sont gérés dans Unity) :
forgeron, armurier, marchand général, joaillier, mystique, alchimiste, marchande de curiosités (roulotte), coffre de
banque et Reliquaire Céleste. Chaque échoppe est un ensemble richement détaillé, façade ouverte vers −Z (côté client),
le marchand se tient derrière le comptoir (repère « vendor » dans meta). Dimensions en mètres."""
import numpy as np
from forge.geo import Mesh, concat, grid_faces, vertex_normals
from world.props import box, cylinder, cone
from world.town import (B, obox, prism, Rx, Ry, Rz, barrel, crate, lantern, window, door, _timber_frame,
                        STONE, ROOF, PLASTER, TIMBER, TILE, GLASS, IRON, EMBER, PLANT, HAY, WATER, CLOTH, DARK, WOOD)
from world import town as TW

GOLD, CRYSTAL, CANDLE, RUG, SACK = 'W_Gold', 'S_Crystal', 'S_Candle', 'S_Rug', 'S_Sack'
POTIONS = ['S_PotionRed', 'S_PotionBlue', 'S_PotionGreen', 'S_PotionViolet']
GEMS = ['S_GemRed', 'S_GemBlue', 'S_GemGreen', 'S_GemGold']


class SB(B):
    """Constructeur d'échoppe : le toit est une pièce à part (« <nom>_Toit ») que Unity masque quand le héros entre,
    pour qu'on voie le marchand et l'étal depuis la caméra plongeante."""
    def __init__(self, name): super().__init__(name); self.r = B(name + '_Toit')
    def roof(self, m, mat): self.r.add(m, mat); return m
    def done(self):
        a = super().done()
        if self.r.a.parts: a.roof = self.r.done(); a.meta_roof = a.roof.name
        return a


def sphere(c, r, segs=12, rings=8, sy=1.0):
    th = np.linspace(0, np.pi, rings + 1); ph = np.linspace(0, 2 * np.pi, segs + 1)
    T, Pp = np.meshgrid(th, ph, indexing='ij')
    V = np.stack([r * np.sin(T) * np.cos(Pp), r * sy * np.cos(T), r * np.sin(T) * np.sin(Pp)], -1).reshape(-1, 3) + np.asarray(c, float)
    UV = np.stack([Pp / (2 * np.pi) * 2 * np.pi * r, T * r], -1).reshape(-1, 2) * 0.5
    m = Mesh(V, grid_faces(rings + 1, segs + 1), UV)
    if (vertex_normals(m) * (m.V - np.asarray(c, float))).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def bottle(b, c, mat, h=0.26, r=0.07, kind=0):
    """Fiole : corps (rond, carré ou conique), col, bouchon de liège."""
    c = np.asarray(c, float)
    if kind == 0: b.add(sphere(c + (0, r, 0), r, 10, 6), mat)
    elif kind == 1: b.add(box(c + (0, h * 0.35, 0), (r * 1.6, h * 0.7, r * 1.6)), mat)
    else: b.add(cylinder(c, r * 1.2, h * 0.6, segs=10, r_top=r * 0.5), mat)
    b.add(cylinder(c + (0, h * 0.55 if kind else r * 1.8, 0), r * 0.35, h * 0.35, segs=8), GLASS)
    b.add(cylinder(c + (0, (h * 0.9 if kind else r * 1.8 + h * 0.35), 0), r * 0.42, 0.04, segs=8), WOOD)


def candle(b, c, h=0.22):
    c = np.asarray(c, float)
    b.add(cylinder(c, 0.035, h, segs=8), CANDLE)
    b.add(cone(c + (0, h, 0), 0.02, 0.06, segs=6, flare=0), EMBER)


def shelf(b, x0, x1, y, z, depth=0.35):
    b.add(box(((x0 + x1) / 2, y, z), (x1 - x0, 0.06, depth)), WOOD)
    for x in (x0 + 0.1, x1 - 0.1): b.add(obox((x, y - 0.18, z - depth * 0.2), (0.05, 0.4, 0.05), Rx(0.6)), IRON)


def stall_frame(b, w, d, h_front=2.6, h_back=3.1, roof_mat=TILE, cloth=None, posts=TIMBER):
    """Charpente d'échoppe : 4 poteaux, sablières, toit en appentis (tuiles) ou bâche, sol de planches."""
    b.add(box((0, 0.06, 0), (w + 0.4, 0.12, d + 0.4)), WOOD)
    for k in range(int(w / 0.4)): b.add(box((-w / 2 + (k + 0.5) * w / int(w / 0.4), 0.125, 0), (0.02, 0.01, d + 0.4)), TIMBER)   # joints de planches
    for sx in (-1, 1):
        for sz, hh in ((-1, h_front), (1, h_back)): b.add(box((sx * (w / 2 - 0.1), hh / 2, sz * (d / 2 - 0.1)), (0.2, hh, 0.2)), posts)
        b.add(obox((sx * (w / 2 - 0.1), (h_front + h_back) / 2 - 0.1, 0), (0.16, 0.2, d), Rx(-np.arctan2(h_back - h_front, d))), TIMBER)
    for sz, hh in ((-1, h_front), (1, h_back)): b.add(box((0, hh - 0.1, sz * (d / 2 - 0.1)), (w, 0.2, 0.2)), TIMBER)
    ang = np.arctan2(h_back - h_front, d + 0.8); L = np.hypot(d + 0.8, h_back - h_front)
    if cloth is None:
        b.roof(obox((0, (h_front + h_back) / 2 + 0.12, 0), (w + 0.8, 0.16, L + 0.2), Rx(-ang)), roof_mat)
        b.roof(obox((0, (h_front + h_back) / 2 + 0.01, 0), (w + 0.6, 0.06, L), Rx(-ang)), TIMBER)
    else:
        n = 9
        for k in range(n):
            x = -w / 2 - 0.4 + (k + 0.5) * (w + 0.8) / n
            b.roof(obox((x, (h_front + h_back) / 2 + 0.1, 0), ((w + 0.8) / n, 0.04, L + 0.2), Rx(-ang)), cloth[k % len(cloth)])
            b.add(box((x, h_front - 0.15, -d / 2 - 0.5), ((w + 0.8) / n - 0.02, 0.32 - 0.1 * (k % 2), 0.02)), cloth[k % len(cloth)])
    # lanternes aux poteaux avant
    for sx in (-1, 1):
        b.add(box((sx * (w / 2 + 0.05), h_front - 0.5, -d / 2 - 0.05), (0.06, 0.06, 0.4)), IRON); lantern(b, (sx * (w / 2 + 0.05), h_front - 0.8, -d / 2 - 0.25), small=True)


def counter(b, x, z, w, h=1.0, d=0.7, front=WOOD):
    b.add(box((x, h, z), (w + 0.1, 0.08, d + 0.1)), WOOD)
    b.add(box((x, h / 2, z), (w, h, d)), front)
    for k in range(int(w / 0.5)): b.add(box((x - w / 2 + (k + 0.5) * w / int(w / 0.5), h / 2, z - d / 2 - 0.02), (0.04, h - 0.1, 0.03)), TIMBER)


def back_wall(b, w, d, h, mat=PLASTER, framed=True):
    b.add(box((0, h / 2, d / 2 + 0.1), (w + 0.4, h, 0.2)), mat)
    if framed: _timber_frame(b, -w / 2 - 0.2, w / 2 + 0.2, 0, h, d / 2, -1, np.random.default_rng(1), braces=False)


def sign(b, c, mat_emblem, shape='disc'):
    """Enseigne pendue à une potence : panneau de bois, emblème en relief (sans texte)."""
    c = np.asarray(c, float)
    b.add(box(c + (0, 0.55, 0.45), (0.08, 0.08, 1.1)), IRON); b.add(obox(c + (0, 0.3, 0.75), (0.05, 0.6, 0.05), Rx(0.8)), IRON)
    for z in (-0.2, 0.2): b.add(box(c + (0, 0.35, z), (0.02, 0.35, 0.02)), IRON)
    b.add(box(c, (0.08, 0.6, 0.8)), WOOD)
    if shape == 'disc': b.add(sphere(c + (-0.05, 0, 0), 0.18, 10, 6, 1.0), mat_emblem)
    else: b.add(box(c + (-0.06, 0, 0), (0.04, 0.4, 0.12)), mat_emblem)


def mannequin(b, c, rot=0.0, plate=True):
    """Mannequin d'armure : socle, fût, cuirasse bombée, épaulières, casque."""
    c = np.asarray(c, float); R = Ry(rot)
    def P(x, y, z): return c + R @ np.array([x, y, z])
    b.add(cylinder(c, 0.3, 0.08, segs=10), WOOD)
    b.add(cylinder(c, 0.04, 1.35, segs=6), WOOD)
    b.add(obox(P(0, 1.3, 0), (0.7, 0.06, 0.06), R), WOOD)
    mat = IRON if plate else 'S_Leather'
    b.add(sphere(P(0, 1.12, 0.03), 0.26, 12, 8, 1.35), mat)                          # torse bombé
    for sx in (-1, 1): b.add(sphere(P(sx * 0.3, 1.33, 0), 0.14, 10, 6, 0.7), mat)      # épaulières
    b.add(box(P(0, 0.78, 0.0), (0.42, 0.18, 0.3)), mat)                                 # braconnière
    b.add(sphere(P(0, 1.62, 0), 0.15, 10, 8, 1.15), IRON)                              # casque
    b.add(box(P(0, 1.62, -0.14), (0.16, 0.03, 0.02)), DARK)
    b.add(box(P(0, 1.0, -0.27), (0.2, 0.3, 0.02)), 'T_ClothRed')                         # tabard


def sword_prop(b, c, R, L=1.0, mat=IRON):
    c = np.asarray(c, float)
    b.add(obox(c + R @ np.array([0, L * 0.35, 0]), (0.06, L * 0.7, 0.012), R), mat)
    b.add(obox(c + R @ np.array([0, 0.0, 0]), (0.24, 0.035, 0.04), R), GOLD)
    b.add(obox(c + R @ np.array([0, -0.11, 0]), (0.035, 0.2, 0.035), R), 'S_Leather')


def weapon_rack(b, c, rot=0.0, n=5, rng=None):
    c = np.asarray(c, float); R = Ry(rot); rng = rng or np.random.default_rng(0)
    for sx in (-1, 1): b.add(obox(c + R @ np.array([sx * 0.8, 0.7, 0]), (0.08, 1.4, 0.25), R), WOOD)
    for y in (0.35, 1.2): b.add(obox(c + R @ np.array([0, y, 0]), (1.7, 0.06, 0.2), R), WOOD)
    for k in range(n):
        x = -0.6 + k * 1.2 / (n - 1)
        kind = k % 3
        if kind == 0: sword_prop(b, c + R @ np.array([x, 0.55, 0.02]), R @ Rz(rng.uniform(-0.05, 0.05)), 1.1)
        elif kind == 1:                                                                      # hache
            b.add(obox(c + R @ np.array([x, 0.85, 0.02]), (0.05, 1.5, 0.05), R), WOOD)
            b.add(obox(c + R @ np.array([x + 0.1, 1.45, 0.02]), (0.22, 0.26, 0.03), R), IRON)
        else:                                                                                # lance
            b.add(obox(c + R @ np.array([x, 1.0, 0.02]), (0.04, 1.9, 0.04), R), WOOD)
            b.add(cone(c + R @ np.array([x, 1.95, 0.02]), 0.05, 0.3, segs=4, flare=0), IRON)


def shield_wall(b, x, y, z, k):
    mats = ['T_ClothRed', 'T_ClothBlue', 'T_ClothGreen', 'T_ClothCream']
    b.add(prism([(-0.32, 0.25), (0.32, 0.25), (0.32, -0.05), (0, -0.42), (-0.32, -0.05)], 0.06, (x, y, z), 'z'), mats[k % 4])
    b.add(prism([(-0.36, 0.29), (0.36, 0.29), (0.36, -0.07), (0, -0.48), (-0.36, -0.07)], 0.03, (x, y, z + 0.035), 'z'), IRON)
    b.add(box((x, y + 0.02, z - 0.04), (0.08, 0.5, 0.02)), GOLD)


def sack(b, c, s=0.5, mat=SACK, rot=0.0):
    c = np.asarray(c, float)
    b.add(sphere(c + (0, s * 0.45, 0), s * 0.45, 10, 6, 1.1), mat)
    b.add(cylinder(c + (0, s * 0.88, 0), s * 0.14, s * 0.18, segs=8, r_top=s * 0.2), mat)
    b.add(cylinder(c + (0, s * 0.92, 0), s * 0.16, s * 0.04, segs=8), TIMBER)


# ------------------------------------------------------------------------------------------- les échoppes
def blacksmith():
    """Forgeron : grand atelier à colombages ouvert, foyer rougeoyant sous hotte, soufflet de cuir, enclume avec épée
    en cours, bac de trempe, meule, râteliers d'outils, tas de charbon, lingots, armes en attente de réparation."""
    r = np.random.default_rng(11); b = SB('S_Forgeron'); w, d = 9.0, 6.0
    stall_frame(b, w, d, 3.4, 4.2, TILE)
    back_wall(b, w, d, 3.6, STONE, framed=False)
    for sx in (-1, 1): b.add(box((sx * (w / 2 + 0.1), 1.8, 0.8), (0.2, 3.6, d - 1.6)), STONE)
    # foyer et hotte
    b.add(box((2.6, 0.5, 2.0), (2.6, 1.0, 1.8)), STONE)
    b.add(box((2.6, 1.02, 2.0), (2.0, 0.05, 1.3)), EMBER)
    for k in range(10): b.add(box((2.6 + r.uniform(-0.9, 0.9), 1.08, 2.0 + r.uniform(-0.5, 0.5)), (0.14, 0.08, 0.12), rot=r.uniform(0, 3)), DARK)
    b.add(obox((2.6, 2.6, 2.3), (2.8, 1.4, 1.6), Rx(-0.15)), STONE)
    b.add(box((2.6, 5.0, 2.6), (1.4, 3.6, 1.2)), STONE); b.add(box((2.6, 6.9, 2.6), (1.7, 0.2, 1.5)), STONE)
    # soufflet
    b.add(obox((4.0, 1.25, 1.2), (0.7, 0.1, 1.2), Rx(0.12)), WOOD); b.add(obox((4.0, 1.0, 1.2), (0.7, 0.1, 1.2), Rx(-0.08)), WOOD)
    b.add(obox((4.0, 1.12, 1.2), (0.62, 0.22, 1.05), Rx(0.02)), 'S_Leather')
    b.add(obox((3.55, 1.08, 1.8), (0.08, 0.08, 0.6), Ry(-0.6)), IRON)
    b.add(obox((4.0, 1.6, 0.6), (0.06, 0.8, 0.06), Rx(0.3)), WOOD)
    # enclume sur billot, épée en cours de forge, marteaux
    b.add(cylinder((0.4, 0, -0.6), 0.38, 0.6, segs=12), WOOD)
    b.add(box((0.4, 0.75, -0.6), (0.85, 0.24, 0.32)), IRON); b.add(box((0.4, 0.62, -0.6), (0.4, 0.1, 0.25)), IRON)
    b.add(obox((-0.15, 0.78, -0.6), (0.3, 0.14, 0.14), Rz(0.25)), IRON)                    # bigorne
    sword_prop(b, (0.45, 0.9, -0.62), Rz(np.pi / 2) @ Rx(np.pi / 2), 1.0, EMBER)
    b.add(obox((0.7, 0.95, -0.3), (0.05, 0.05, 0.4), Ry(0.4)), WOOD); b.add(box((0.62, 0.95, -0.12), (0.1, 0.1, 0.16)), IRON)
    # bac de trempe
    b.add(box((-1.6, 0.4, -0.4), (1.4, 0.8, 0.8)), WOOD); b.add(box((-1.6, 0.76, -0.4), (1.25, 0.02, 0.65)), WATER)
    for x in (-2.25, -0.95): b.add(box((x, 0.4, -0.4), (0.06, 0.84, 0.84)), IRON)
    # meule
    wh = cylinder((0, -0.08, 0), 0.45, 0.16, segs=16); wh.V = wh.V @ Rz(np.pi / 2).T + np.array([-3.2, 0.95, -1.4]); b.add(wh, STONE)
    for sz in (-1, 1): b.add(box((-3.2, 0.5, -1.4 + sz * 0.25), (0.1, 1.0, 0.08)), WOOD)
    b.add(box((-3.2, 0.95, -1.4), (0.5, 0.05, 0.05)), IRON)
    # râtelier d'outils au mur, armes à réparer
    b.add(box((-1.5, 2.2, d / 2 - 0.05), (3.0, 0.1, 0.1)), WOOD)
    for k in range(8):
        x = -2.8 + k * 0.38
        b.add(box((x, 1.85, d / 2 - 0.08), (0.04, 0.6, 0.04)), WOOD); b.add(box((x, 1.55 if k % 2 else 2.15, d / 2 - 0.1), (0.16, 0.1 if k % 2 else 0.06, 0.06)), IRON)
    weapon_rack(b, (-2.6, 0.12, 1.6), 0.0, 5, r)
    # charbon et lingots
    for k in range(14): b.add(sphere((3.9 + r.uniform(-0.4, 0.4), 0.15 + r.uniform(0, 0.25), -1.6 + r.uniform(-0.4, 0.4)), 0.14, 6, 4, 0.7), DARK)
    for k in range(6): b.add(box((-0.8 + (k % 3) * 0.22, 1.08 + 0.06 * (k // 3), 1.6), (0.18, 0.06, 0.4)), IRON if k % 2 else GOLD)
    b.add(box((-0.6, 0.5, 1.6), (1.4, 1.0, 0.7)), WOOD)
    barrel(b, (3.9, 0.12, -2.6), 0.9, 0.38); crate(b, (-4.0, 0.12, -2.4), 0.7, 0.3)
    sign(b, (-w / 2 - 0.4, 2.6, -d / 2 + 0.2), IRON, 'bar')
    a = b.done(); a.meta = dict(vendor=(0.6, 0.12, 0.6), fire=(2.6, 1.3, 2.0), chimney=(2.6, 7.2, 2.6)); return a


def armorer():
    """Armurier : échoppe couverte, comptoir, mannequins en armure, râteliers d'armes, écus peints au mur, bannière."""
    r = np.random.default_rng(12); b = SB('S_Armurier'); w, d = 8.0, 5.0
    stall_frame(b, w, d, 3.0, 3.6, TILE)
    back_wall(b, w, d, 3.3)
    for sx in (-1, 1): b.add(box((sx * (w / 2 + 0.1), 1.5, 1.0), (0.2, 3.0, d - 2.0)), PLASTER)
    counter(b, 0, -0.8, 3.6)
    for k in range(3): sword_prop(b, (-1.2 + k * 1.1, 1.06, -0.8), Rx(np.pi / 2) @ Rz(0.3 * (k - 1)), 0.95)
    b.add(sphere((1.3, 1.17, -0.75), 0.15, 10, 8, 1.15), IRON)
    mannequin(b, (-3.0, 0.12, -1.6), 0.4); mannequin(b, (3.0, 0.12, -1.6), -0.4); mannequin(b, (-1.6, 0.12, 1.7), 0.0, plate=False)
    weapon_rack(b, (1.8, 0.12, 2.05), 0.0, 6, r)
    for k in range(5): shield_wall(b, -2.8 + k * 1.4, 2.6, d / 2 - 0.02, k)
    b.add(box((0, 1.6, d / 2 - 0.02), (1.0, 2.2, 0.03)), 'T_ClothRed')
    b.add(box((0, 1.9, d / 2 - 0.05), (0.45, 0.45, 0.02)), GOLD)
    sign(b, (w / 2 + 0.4, 2.3, -d / 2 + 0.2), IRON, 'bar')
    a = b.done(); a.meta = dict(vendor=(0, 0.12, 0.2)); return a


def general_store():
    """Marchand général : étal sous bâche rayée, comptoir chargé, sacs de grain, caisses, tonneaux, cordes, lanternes."""
    r = np.random.default_rng(13); b = SB('S_Marchand'); w, d = 6.0, 4.0
    stall_frame(b, w, d, 2.7, 3.2, cloth=['T_ClothGreen', 'T_ClothCream'])
    back_wall(b, w, d, 2.9)
    counter(b, 0, -0.6, 3.8)
    for k in range(6): bottle(b, (-1.6 + k * 0.6, 1.04, -0.65), POTIONS[k % 2], 0.3, 0.08, k % 3)
    shelf(b, -2.6, 2.6, 1.5, d / 2 - 0.25); shelf(b, -2.6, 2.6, 2.2, d / 2 - 0.25)
    for k in range(12):
        x = -2.4 + k * 0.42; y = 1.53 if k % 2 else 2.23
        if k % 3 == 0: bottle(b, (x, y, d / 2 - 0.25), POTIONS[k % 4], 0.26, 0.07, 1)
        elif k % 3 == 1: b.add(cylinder((x, y, d / 2 - 0.25), 0.1, 0.18, segs=8), STONE)
        else: crate(b, (x, y, d / 2 - 0.3), 0.25, 0.2)
    for k in range(5): sack(b, (-3.4 + 0.45 * (k % 3), 0.12, -1.8 + 0.5 * (k // 3)), 0.6)
    barrel(b, (3.3, 0.12, -1.6), 0.95, 0.38); barrel(b, (3.3, 0.12, -0.7), 0.95, 0.38)
    crate(b, (2.4, 0.12, 1.3), 0.7, 0.2); crate(b, (2.4, 0.82, 1.3), 0.55, 0.6)
    for k in range(3):                                                               # rouleaux de corde suspendus
        tor = cylinder((0, 0, 0), 0.22, 0.08, segs=12); tor.V = tor.V @ Rx(np.pi / 2).T + np.array([-1.5 + k * 1.5, 2.25, -d / 2 + 0.05]); b.add(tor, HAY)
    a = b.done(); a.meta = dict(vendor=(0, 0.12, 0.4)); return a


def jeweler():
    """Joaillier : petite boutique élégante, comptoir à vitrine (gemmes lumineuses sur velours), balance de précision,
    chandelier, coffrets ouverts, colliers suspendus."""
    r = np.random.default_rng(14); b = SB('S_Joaillier'); w, d = 5.0, 3.8
    stall_frame(b, w, d, 2.7, 3.2, cloth=['T_ClothBlue', 'T_ClothCream'])
    back_wall(b, w, d, 2.9)
    # vitrine : caisson de bois, velours, couvercle de verre
    b.add(box((0, 0.5, -0.6), (3.0, 1.0, 0.8)), WOOD)
    b.add(box((0, 1.02, -0.6), (2.8, 0.04, 0.65)), 'T_ClothBlue')
    for k in range(14):
        b.add(sphere((-1.25 + (k % 7) * 0.42, 1.08, -0.78 + (k // 7) * 0.3), 0.055, 8, 5, 0.8), GEMS[k % 4])
        b.add(cylinder((-1.25 + (k % 7) * 0.42, 1.04, -0.78 + (k // 7) * 0.3), 0.07, 0.02, segs=8), GOLD)
    for sx in (-1.45, 1.45): b.add(box((sx, 1.18, -0.6), (0.04, 0.3, 0.7)), GOLD)
    b.add(box((0, 1.34, -0.6), (2.95, 0.03, 0.7)), WOOD)
    # balance
    b.add(cylinder((1.0, 1.36, -0.5), 0.12, 0.03, segs=10), GOLD); b.add(cylinder((1.0, 1.36, -0.5), 0.015, 0.4, segs=6), GOLD)
    b.add(box((1.0, 1.76, -0.5), (0.5, 0.02, 0.02)), GOLD)
    for sx in (-1, 1): b.add(cylinder((1.0 + sx * 0.25, 1.55, -0.5), 0.08, 0.02, segs=10), GOLD)
    # chandelier
    b.add(cylinder((-1.1, 1.36, -0.45), 0.08, 0.04, segs=8), GOLD); b.add(cylinder((-1.1, 1.36, -0.45), 0.02, 0.3, segs=6), GOLD)
    for k in range(3): candle(b, (-1.25 + k * 0.15, 1.66, -0.45), 0.16)
    b.add(box((-1.1, 1.66, -0.45), (0.38, 0.02, 0.04)), GOLD)
    # coffrets et étagère de pierres
    for k in range(3):
        x = -1.6 + k * 1.6
        b.add(box((x, 1.55, d / 2 - 0.3), (0.5, 0.25, 0.32)), WOOD)
        b.add(obox((x, 1.82, d / 2 - 0.15), (0.5, 0.3, 0.03), Rx(-0.3)), WOOD)
        for q in range(4): b.add(sphere((x - 0.15 + q * 0.1, 1.7, d / 2 - 0.3), 0.04, 6, 4), GEMS[(k + q) % 4])
    shelf(b, -2.2, 2.2, 1.4, d / 2 - 0.25)
    for k in range(4):                                                   # colliers suspendus
        x = -1.5 + k
        for q in range(6):
            t = q / 5; b.add(sphere((x + (t - 0.5) * 0.4, 2.4 - 0.25 * np.sin(np.pi * t), -d / 2 + 0.1), 0.025, 6, 4), GOLD)
        b.add(sphere((x, 2.1, -d / 2 + 0.1), 0.05, 8, 5), GEMS[k % 4])
    b.add(box((0, 2.5, -d / 2 + 0.1), (3.6, 0.04, 0.04)), GOLD)
    a = b.done(); a.meta = dict(vendor=(0, 0.12, 0.6)); return a


def mystic():
    """Mystique : grande tente octogonale de toile violette et or, ouverte devant ; tapis, coussins, table basse et boule
    de cristal lumineuse, bougies, cristaux dressés, brûle-encens, carillons de verre et talismans suspendus."""
    r = np.random.default_rng(15); b = SB('S_Mystique'); R0, H = 3.2, 3.0
    b.add(cylinder((0, 0, 0), R0 + 0.3, 0.1, segs=8), WOOD)
    b.add(cylinder((0, 0.1, 0), R0 - 0.2, 0.02, segs=8), RUG)
    n = 8
    for k in range(n):
        a0 = 2 * np.pi * (k + 0.5) / n
        p = np.array([R0 * np.sin(a0), 0, R0 * np.cos(a0)])
        b.add(box(p + (0, H / 2, 0), (0.14, H, 0.14)), WOOD)
        if not (np.cos(a0) < -0.3):                                              # pans fermés sauf à l'avant
            a1 = 2 * np.pi * (k + 1.5) / n; q = np.array([R0 * np.sin(a1), 0, R0 * np.cos(a1)])
            m = (p + q) / 2; L = np.linalg.norm(q - p); rot = np.arctan2((q - p)[0], (q - p)[2])
            b.add(box(m + (0, H / 2, 0), (0.04, H, L), rot=rot), 'S_Violet')
            b.add(box(m + (0, 0.25, 0) - m / np.linalg.norm(m) * 0.03, (0.05, 0.12, L), rot=rot), GOLD)
    # pans relevés et noués à l'avant
    for sx in (-1, 1): b.add(obox((sx * 1.6, H * 0.6, -R0 * 0.95), (0.9, 1.6, 0.06), Rz(sx * 0.4)), 'S_Violet')
    b.roof(cone((0, H, 0), R0 + 0.5, 2.6, segs=8, flare=0.18), 'S_Violet')
    b.roof(cylinder((0, H + 2.5, 0), 0.05, 0.9, segs=6), GOLD); b.roof(sphere((0, H + 3.45, 0), 0.12, 8, 6), CRYSTAL)
    for k in range(8):                                                           # liseré or au bord du toit
        a0 = 2 * np.pi * (k + 0.5) / 8; b.roof(sphere(((R0 + 0.45) * np.sin(a0), H - 0.15, (R0 + 0.45) * np.cos(a0)), 0.08, 6, 4), GOLD)
    # table basse, boule de cristal, cartes
    b.add(cylinder((0, 0.1, 0.5), 0.7, 0.45, segs=12), WOOD); b.add(cylinder((0, 0.55, 0.5), 0.75, 0.04, segs=16), 'S_Violet')
    b.add(cylinder((0, 0.59, 0.5), 0.14, 0.08, segs=10, r_top=0.1), GOLD)
    b.add(sphere((0, 0.82, 0.5), 0.18, 16, 10), CRYSTAL)
    for k in range(5): b.add(obox((-0.45 + k * 0.12, 0.6, 0.15 + 0.03 * k), (0.1, 0.005, 0.16), Ry(0.2 * k - 0.4)), 'T_ClothCream')
    for k in range(6): candle(b, (0.55 * np.cos(k), 0.6, 0.5 + 0.5 * np.sin(k) * 0.6), 0.1 + 0.08 * (k % 2))
    # coussins
    for k, (x, z) in enumerate([(-1.0, 1.4), (1.0, 1.4), (0, 1.6)]): b.add(box((x, 0.22, z), (0.7, 0.2, 0.7), rot=0.3 * k), 'T_ClothRed' if k % 2 else 'S_Violet')
    # cristaux dressés
    for k in range(7):
        x, z = r.uniform(-2.3, 2.3), r.uniform(0.6, 2.4)
        if np.hypot(x, z) > R0 - 0.5: continue
        hgt = r.uniform(0.3, 0.9); b.add(cone((x, 0.1, z), 0.1 + hgt * 0.1, hgt, segs=6, flare=0), CRYSTAL)
    # brûle-encens et talismans suspendus
    b.add(cylinder((2.0, 0.1, -1.5), 0.04, 1.1, segs=6), GOLD); b.add(cylinder((2.0, 1.2, -1.5), 0.2, 0.15, segs=10, r_top=0.12), GOLD)
    b.add(cylinder((2.0, 1.33, -1.5), 0.12, 0.02, segs=8), EMBER)
    for k in range(6):
        a0 = 2 * np.pi * k / 6 + 0.3; p = np.array([1.7 * np.sin(a0), 2.0, 1.7 * np.cos(a0)])
        b.add(cylinder(p, 0.01, 0.6, segs=4), GOLD)
        b.add(sphere(p + (0, -0.05, 0), 0.05, 6, 4), CRYSTAL if k % 2 else GEMS[k % 4])
    a = b.done(); a.meta = dict(vendor=(0, 0.12, 1.3), glow=(0, 0.82, 0.5)); return a


def alchemist():
    """Alchimiste : échoppe aux étagères chargées de fioles lumineuses, alambic de cuivre sur réchaud, chaudron bouillonnant,
    bouquets d'herbes séchant au plafond, grimoire ouvert, mortier."""
    r = np.random.default_rng(16); b = SB('S_Alchimiste'); w, d = 6.0, 4.0
    stall_frame(b, w, d, 2.8, 3.3, TILE)
    back_wall(b, w, d, 3.0)
    counter(b, 0, -0.6, 3.6)
    for k in range(7): bottle(b, (-1.5 + k * 0.5, 1.04, -0.65 + 0.08 * (k % 2)), POTIONS[k % 4], 0.3, 0.07, k % 3)
    b.add(box((1.3, 1.08, -0.55), (0.5, 0.06, 0.36), rot=0.2), 'S_Leather'); b.add(box((1.3, 1.12, -0.55), (0.46, 0.02, 0.32), rot=0.2), 'T_ClothCream')
    b.add(cylinder((-1.4, 1.04, -0.45), 0.1, 0.12, segs=10, r_top=0.12), STONE)
    for y in (1.3, 1.85, 2.4):
        shelf(b, -2.6, 2.6, y, d / 2 - 0.25)
        for k in range(10): bottle(b, (-2.4 + k * 0.52 + r.uniform(-0.05, 0.05), y + 0.03, d / 2 - 0.25), POTIONS[int(r.integers(0, 4))], 0.24, 0.06, int(r.integers(0, 3)))
    # alambic
    b.add(box((-2.2, 0.4, -1.5), (0.8, 0.8, 0.8)), STONE); b.add(box((-2.2, 0.82, -1.5), (0.5, 0.04, 0.5)), EMBER)
    b.add(sphere((-2.2, 1.1, -1.5), 0.25, 12, 8), GOLD)
    b.add(obox((-1.95, 1.45, -1.5), (0.05, 0.5, 0.05), Rz(-0.9)), GOLD)
    b.add(sphere((-1.6, 1.0, -1.5), 0.12, 10, 6), 'S_PotionGreen')
    # chaudron
    b.add(sphere((2.2, 0.5, -1.4), 0.45, 14, 8, 0.8), IRON); b.add(cylinder((2.2, 0.68, -1.4), 0.4, 0.02, segs=14), 'S_PotionGreen')
    for k in range(3):
        a0 = 2 * np.pi * k / 3; b.add(obox((2.2 + 0.35 * np.cos(a0), 0.1, -1.4 + 0.35 * np.sin(a0)), (0.06, 0.22, 0.06), Ry(a0)), IRON)
    # herbes séchées
    for k in range(8):
        x = -2.4 + k * 0.68
        b.add(cylinder((x, 2.2, -0.2), 0.008, 0.45, segs=4), HAY)
        b.add(cone((x, 1.85, -0.2), 0.09, 0.4, segs=6, flare=0), PLANT if k % 2 else HAY)
    b.add(box((0, 2.66, -0.2), (5.4, 0.04, 0.04)), TIMBER)
    a = b.done(); a.meta = dict(vendor=(0, 0.12, 0.4), cauldron=(2.2, 0.75, -1.4)); return a


def curio_wagon():
    """Marchande de curiosités : roulotte peinte (caisse arrondie sur roues), marchepied, auvent, coffres mystérieux,
    lanternes de couleur, babioles suspendues."""
    r = np.random.default_rng(17); b = SB('S_Curiosites')
    L, W, H = 5.0, 2.4, 2.2
    b.add(box((0, 1.0, 0), (W, 0.15, L)), WOOD)
    b.add(box((0, 1.0 + H / 2, 0), (W, H, L)), 'S_Violet')
    for z in np.linspace(-L / 2, L / 2, 6): b.add(box((0, 1.0 + H / 2, z), (W + 0.06, H, 0.1)), GOLD)
    for k in range(9):                                                        # toit cintré
        a0 = np.pi * (k + 0.5) / 9
        b.add(obox((np.cos(a0) * (W / 2 + 0.1), 1.0 + H + np.sin(a0) * 0.7, 0), (0.32, 0.08, L + 0.4), Rz(a0 - np.pi / 2)), 'S_Teal')
    b.add(cylinder((0.6, 1.0 + H + 0.4, 1.4), 0.12, 0.9, segs=8), IRON); b.add(cone((0.6, 1.0 + H + 1.3, 1.4), 0.2, 0.25, segs=8, flare=0), IRON)   # tuyau de poêle
    for sz in (-1, 1): b.add(prism([(-W / 2, 0), (W / 2, 0), (W / 2 * 0.7, 0.5), (0, 0.7), (-W / 2 * 0.7, 0.5)], 0.08, (0, 1.0 + H, sz * L / 2), 'z'), 'S_Violet')
    for sz in (-1, 1):                                                        # roues
        for sx in (-1, 1):
            wh = cylinder((0, -0.06, 0), 0.75, 0.12, segs=18); wh.V = wh.V @ Rz(np.pi / 2).T + np.array([sx * (W / 2 + 0.12), 0.75, sz * 1.5]); b.add(wh, 'S_Teal')
            for k in range(8): b.add(obox((sx * (W / 2 + 0.12), 0.75, sz * 1.5), (0.04, 1.4, 0.05), Rx(np.pi * k / 8)), GOLD)
    window(b, (-W / 2, 2.2, 0.8), '-x', 0.6, 0.6, True, False, r); window(b, (W / 2, 2.2, -0.8), '+x', 0.6, 0.6, True, False, r)
    b.add(box((0, 1.95, -L / 2 - 0.04), (0.9, 1.75, 0.06)), WOOD)
    for x in (-0.3, 0.0, 0.3): b.add(box((x, 1.95, -L / 2 - 0.08), (0.04, 1.7, 0.03)), GOLD)
    b.add(sphere((0, 2.5, -L / 2 - 0.09), 0.12, 10, 6, 0.4), GOLD); b.add(box((0.3, 1.9, -L / 2 - 0.1), (0.06, 0.12, 0.05)), IRON)
    for k in range(4): b.add(box((0, 0.9 - k * 0.22, -L / 2 - 0.3 - k * 0.25), (1.0, 0.06, 0.3)), WOOD)
    # auvent latéral et étal
    b.add(obox((-W / 2 - 1.1, 2.9, 0), (2.2, 0.05, 3.2), Rz(-0.25)), 'S_Teal')
    for sz in (-1, 1): b.add(box((-W / 2 - 2.1, 1.3, sz * 1.5), (0.08, 2.6, 0.08)), WOOD)
    for k in range(3):
        crate(b, (-W / 2 - 1.2, 0, -1.0 + k), 0.6, r.uniform(0, 1))
        b.add(box((-W / 2 - 1.2, 0.62, -1.0 + k), (0.62, 0.04, 0.62)), GOLD)
    for k in range(5): b.add(sphere((-W / 2 - 2.05, 2.3 - 0.1 * (k % 2), -1.2 + k * 0.6), 0.09, 8, 5), GEMS[k % 4])
    for z in (-1.2, 1.2): lantern(b, (-W / 2 - 2.1, 2.4, z), small=True)
    a = b.done(); a.meta = dict(vendor=(-W / 2 - 1.2, 0.0, -1.6)); return a


def stash_chest():
    b = SB('S_Coffre'); w, d, h = 1.6, 0.9, 0.75
    b.add(box((0, h / 2, 0), (w, h, d)), WOOD)
    for k in range(9):
        a0 = np.pi * k / 8
        b.add(obox((0, h + np.sin(a0) * 0.3, np.cos(a0) * d / 2 * 0.98), (w, 0.04, d * np.pi / 16 + 0.02), Rx(-(a0 - np.pi / 2))), WOOD)
    for x in (-w / 2 + 0.1, -0.25, 0.25, w / 2 - 0.1):
        b.add(box((x, h / 2, 0), (0.08, h + 0.02, d + 0.03)), IRON)
        for k in range(9):
            a0 = np.pi * k / 8; b.add(obox((x, h + np.sin(a0) * 0.31, np.cos(a0) * (d / 2 + 0.01)), (0.08, 0.04, d * np.pi / 16 + 0.03), Rx(-(a0 - np.pi / 2))), IRON)
    b.add(box((0, h - 0.05, -d / 2 - 0.03), (0.2, 0.26, 0.05)), GOLD); b.add(cylinder((0, h - 0.18, -d / 2 - 0.06), 0.04, 0.03, segs=8), DARK)
    for sx in (-1, 1): b.add(obox((sx * (w / 2 + 0.03), h * 0.6, 0), (0.05, 0.06, 0.3), None), IRON)
    return b.done()


def reliquary():
    """Reliquaire Céleste : estrade circulaire à trois degrés, quatre arcs de pierre runique, socle central portant un
    éclat d'étoile flottant (cristal lumineux), anneau de runes, trois réceptacles (arme, armure, bijou)."""
    b = SB('S_Reliquaire'); R0 = 4.5
    for k, (rr, hh) in enumerate(((R0, 0.25), (R0 - 0.7, 0.5), (R0 - 1.4, 0.75))): b.add(cylinder((0, 0, 0), rr, hh, segs=24), STONE)
    b.add(cylinder((0, 0.75, 0), R0 - 1.5, 0.02, segs=48), 'S_Runes')
    for k in range(4):
        a0 = np.pi / 4 + np.pi / 2 * k; p = np.array([np.sin(a0), 0, np.cos(a0)]) * (R0 - 0.5)
        for s in (-1, 1):
            t = np.array([np.cos(a0), 0, -np.sin(a0)]) * s * 1.1
            b.add(box(p + t + (0, 2.2, 0), (0.55, 4.0, 0.55), rot=a0), STONE)
            b.add(box(p + t + (0, 2.2, 0) - p / np.linalg.norm(p) * 0.29, (0.3, 3.2, 0.02), rot=a0), 'S_Runes')
        for q in range(9):
            a1 = np.pi * (q + 0.5) / 9
            b.add(obox(p + np.array([np.cos(a0), 0, -np.sin(a0)]) * np.cos(a1) * 1.1 + (0, 4.2 + np.sin(a1) * 0.8, 0), (0.45, 0.4, 0.5), Ry(a0) @ Rz(a1 - np.pi / 2)), STONE)
        b.add(sphere(p + (0, 5.15, 0), 0.18, 10, 6), CRYSTAL)
    b.add(cylinder((0, 0.75, 0), 0.7, 0.9, segs=8, r_top=0.5), STONE)
    b.add(cylinder((0, 1.65, 0), 0.75, 0.15, segs=8), GOLD)
    for k in range(4):
        a0 = np.pi / 2 * k; b.add(obox((0.62 * np.sin(a0), 1.95, 0.62 * np.cos(a0)), (0.08, 0.5, 0.08), Ry(a0) @ Rx(-0.4)), GOLD)
    # éclat d'étoile : double pointe facettée
    b.add(cone((0, 2.9, 0), 0.38, 0.9, segs=6, flare=0), CRYSTAL)
    top = cone((0, 0, 0), 0.38, 0.7, segs=6, flare=0); top.V = top.V @ Rx(np.pi).T + np.array([0, 2.9, 0]); top.F = top.F[:, ::-1].copy(); b.add(top, CRYSTAL)
    # trois réceptacles autour du socle
    for k in range(3):
        a0 = np.pi + (k - 1) * 0.9; p = np.array([np.sin(a0), 0, np.cos(a0)]) * 1.9
        b.add(cylinder(p + (0, 0.75, 0), 0.32, 0.7, segs=8, r_top=0.26), STONE)
        b.add(cylinder(p + (0, 1.45, 0), 0.3, 0.06, segs=10), GOLD)
        b.add(sphere(p + (0, 1.62, 0), 0.12, 10, 6), [GEMS[0], GEMS[1], GEMS[2]][k])
    a = b.done(); a.meta = dict(glow=(0, 2.9, 0), receptacles=3); return a


def _glow(color, strength):
    return lambda S: TW.flat(color, 0.9, emit=tuple(c * strength for c in color))


RECIPES = {
    CRYSTAL: (_glow((0.55, 0.85, 1.0), 1.0), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(2.0, 2.0, 2.0), emission_preview=(0.5, 0.8, 1.0))),
    CANDLE: (lambda S: TW.flat((0.86, 0.80, 0.66), 0.4), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    RUG: (lambda S: rug(S), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    SACK: (lambda S: TW.cloth((0.55, 0.46, 0.32), seed=9), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'S_Leather': (lambda S: __import__('knight.materials', fromlist=['x']).leather(S), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'S_Violet': (lambda S: TW.cloth((0.30, 0.10, 0.38), seed=21), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'S_Teal': (lambda S: TW.cloth((0.08, 0.30, 0.32), seed=22), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'S_Runes': (lambda S: runes(S), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.4, 0.7, 1.0))),
}
for nm, col in zip(POTIONS, [(0.9, 0.08, 0.06), (0.1, 0.3, 1.0), (0.2, 0.9, 0.25), (0.6, 0.15, 0.95)]):
    RECIPES[nm] = (_glow(col, 0.6), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(1.2, 1.2, 1.2), emission_preview=tuple(c * 0.6 for c in col)))
for nm, col in zip(GEMS, [(1.0, 0.1, 0.12), (0.15, 0.35, 1.0), (0.1, 1.0, 0.35), (1.0, 0.8, 0.2)]):
    RECIPES[nm] = (_glow(col, 0.8), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(1.5, 1.5, 1.5), emission_preview=tuple(c * 0.7 for c in col)))


def rug(S=512, seed=3101):
    """Tapis tissé : bordures à motifs géométriques rouge, or et indigo, médaillon central."""
    from forge.texgen import fbm, n01
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float) / S
    u, v = np.abs(xx - 0.5) * 2, np.abs(yy - 0.5) * 2; m = np.maximum(u, v)
    col = np.zeros((S, S, 3)) + np.array([0.45, 0.07, 0.06])
    band = (m > 0.78) & (m < 0.92); col[band] = (0.12, 0.10, 0.30)
    zig = (m > 0.82) & (m < 0.88) & (((xx + yy) * 40 % 2) < 1); col[zig] = (0.75, 0.55, 0.20)
    med = (u + v) < 0.55; col[med] = (0.12, 0.10, 0.30); col[(u + v) < 0.35] = (0.75, 0.55, 0.20); col[(u + v) < 0.18] = (0.45, 0.07, 0.06)
    g = n01(fbm(S, S, r, base=S / 40, octaves=3))
    col = col * (0.8 + 0.3 * g[..., None])
    return TW._t(col, g, 0.1, 0.4)


def runes(S=512, seed=3102):
    """Pierre gravée de runes lumineuses bleu-céleste (émission dans les sillons)."""
    from forge.texgen import fbm, n01
    from PIL import Image, ImageDraw
    r = np.random.default_rng(seed)
    im = Image.new('L', (S, S), 0); dr = ImageDraw.Draw(im)
    for row in range(6):
        for k in range(8):
            x0, y0 = k * S / 8 + S / 32, row * S / 6 + S / 24; w, h = S / 14, S / 9
            pts = [(x0 + r.uniform(0, w), y0 + r.uniform(0, h)) for _ in range(4)]
            dr.line(pts, fill=255, width=max(2, S // 128)); dr.line([(x0 + w / 2, y0), (x0 + w / 2, y0 + h)], fill=255, width=max(2, S // 128))
    g = np.asarray(im, np.float32) / 255
    st = n01(fbm(S, S, r, base=S / 10, octaves=4))
    col = np.array([0.36, 0.35, 0.38])[None, None] * (0.7 + 0.4 * st[..., None]) * (1 - 0.6 * g[..., None]) + np.array([0.4, 0.75, 1.0]) * g[..., None] * 0.6
    emit = np.array([0.35, 0.7, 1.0])[None, None] * g[..., None]
    return TW._t(col, st - 1.5 * g, 0.2, 0.8, emit)


def pieces():
    out = []
    for a in [blacksmith(), armorer(), general_store(), jeweler(), mystic(), alchemist(), curio_wagon(), stash_chest(), reliquary()]:
        out.append(a)
        if getattr(a, 'roof', None) is not None:
            a.meta['roof'] = a.roof.name; out.append(a.roof)
    return out
