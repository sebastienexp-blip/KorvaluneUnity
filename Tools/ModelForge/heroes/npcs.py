"""Marchands de Korvalune (personnages non joueurs) : corps MakeHuman CC0 (sans curseurs de morphologie, plus légers
que les héros) habillés selon leur métier — forgeron, armurier, marchand, joaillier, mystique, alchimiste, marchande de
curiosités. Vêtements découpés et ajustés sur le corps (chemises, braies, bottes, tablier de cuir, robes longues,
capuche, châle, chapeau de feutre, chaîne d'or), animation de repos. Noms et visuels originaux."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, humanparts as HP
from forge.geo import Loft, Mesh, spline, curve, concat, tube, blend_bones, smoothstep, vertex_normals
from knight import anims, materials as KM
from heroes import modular as MOD
from heroes.varek import hood
from heroes.modular import rag_cloth, rope
from world.props import cylinder

FOLDER = 'Characters'
HEAD_VIEW = (0, 1.66, 0.05)
GEAR = []


def _cloth(color, seed, patches=False, dirt=0.5):
    return (lambda S: rag_cloth(S, seed, color, patches=patches, dirt=dirt), 512, 256, dict(metallic=0.0, smoothness=1.0))


class Tailor:
    """Coupe des vêtements sur le corps de base (mêmes outils que la tenue de départ des héros)."""
    def __init__(self, sk, a, body, marks, female, prefix, bank):
        self.sk, self.a, self.body, self.marks, self.female, self.prefix, self.bank = sk, a, body, marks, female, prefix, bank
        self.P = {n: np.asarray(p, float) for n, p in zip(sk.names, sk.pos)}
        self.waist = 0.962 if female else 0.985
        self.V = body.V

    def along(self, Q, a_, b_):
        d = self.P[b_] - self.P[a_]; return ((Q - self.P[a_]) @ d) / (d @ d)

    def side(self, Q, l, r): return np.where(Q[:, 0] > 0, l, r)

    def mat(self, name, recipe):
        n = self.prefix + '_' + name; self.bank.recipes[n] = recipe; return n

    def shirt(self, mat, sleeve=0.9, hem=None, off=0.009, collar=True):
        """Chemise / tunique : manches jusqu'à sleeve (0 épaule → 1 coude → 2 poignet), ourlet à hem (m)."""
        hem = self.waist - 0.14 if hem is None else hem
        neck_y = self.P['Neck'][1] - 0.03
        def inside(Q):
            arm = self.side(Q, self.along(Q, 'LeftUpperArm', 'LeftLowerArm'), self.along(Q, 'RightUpperArm', 'RightLowerArm'))
            fore = self.side(Q, self.along(Q, 'LeftLowerArm', 'LeftHand'), self.along(Q, 'RightLowerArm', 'RightHand'))
            a = np.where(arm > 1.0, 1.0 + np.clip(fore, 0, 2), arm)
            v = neck_y - Q[:, 1] - (0.04 * np.exp(-(Q[:, 0] / 0.05) ** 2) * (Q[:, 2] > 0) if collar else 0)
            return np.minimum(np.minimum(Q[:, 1] - hem, v), sleeve - a)
        bones = ['Hips', 'Spine', 'Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'UpperLeg', 'Neck'] + (['LowerArm'] if sleeve > 1 else [])
        coarse = HP.bone_mask(self.body, self.sk, bones, 0.15) & (self.V[:, 1] > hem - 0.08)
        loose = lambda Q: off + 0.012 * np.clip((self.waist + 0.1 - Q[:, 1]) / 0.3, 0, 1)
        self.a.add('chemise', HP.cloth_cut(self.body, coarse, inside, 0.0, thick=0.003, uv_scale=1.6, bump=loose), mat, slot='Tenue')

    def trousers(self, mat, length=1.0, off=0.008):
        """Braies : de la taille jusqu'à length (0 genou → 1 cheville)."""
        def inside(Q):
            t = self.side(Q, self.along(Q, 'LeftLowerLeg', 'LeftFoot'), self.along(Q, 'RightLowerLeg', 'RightFoot'))
            return np.minimum(self.waist + 0.015 - Q[:, 1], length - t)
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'UpperLeg', 'LowerLeg', 'Spine'], 0.15) & (self.V[:, 1] < self.waist + 0.05)
        self.a.add('braies', HP.cloth_cut(self.body, coarse, inside, off, thick=0.003, uv_scale=1.6), mat, slot='Tenue')

    def boots(self, mat, top=0.45):
        def inside(Q):
            t = self.side(Q, self.along(Q, 'LeftLowerLeg', 'LeftFoot'), self.along(Q, 'RightLowerLeg', 'RightFoot'))
            return t - top
        coarse = HP.bone_mask(self.body, self.sk, ['LowerLeg', 'Foot', 'Toes'], 0.2)
        self.a.add('bottes', HP.cloth_cut(self.body, coarse, inside, 0.011, thick=0.004, uv_scale=3.0), mat, slot='Tenue')

    def belt(self, mat, buckle=None):
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'Spine'], 0.2) & (np.abs(self.V[:, 1] - self.waist) < 0.12)
        self.a.add('ceinture', HP.cloth_cut(self.body, coarse, lambda Q: 0.024 - np.abs(Q[:, 1] - (self.waist - 0.03)), 0.022, thick=0.006, uv_scale=6.0), mat, slot='Tenue')
        if buckle:
            b = cylinder((0, 0, 0), 0.03, 0.012, segs=8); b.V = b.V @ np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]]).T
            zf = self.V[np.abs(self.V[:, 1] - (self.waist - 0.03)) < 0.01][:, 2].max() + 0.03
            b.V += np.array([0, self.waist - 0.03, zf]); blend_bones(b, self.sk['Hips'], self.sk['Hips'], np.zeros(len(b.V)))
            self.a.add('boucle', b, buckle, slot='Tenue')

    def apron(self, mat, top=None, bottom=None, width=0.17):
        """Tablier : panneau avant de la poitrine (ou de la taille) jusqu'aux genoux, qui tombe droit."""
        top = self.waist + 0.30 if top is None else top
        bottom = 0.50 if bottom is None else bottom
        def inside(Q):
            return np.minimum(np.minimum(top - Q[:, 1], Q[:, 1] - bottom), width - np.abs(Q[:, 0]))
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'UpperLeg'], 0.15) & (self.V[:, 2] > 0.0) & (self.V[:, 1] > bottom - 0.05) & (self.V[:, 1] < top + 0.05)
        zb = self.V[(self.V[:, 1] > self.waist + 0.15) & (np.abs(self.V[:, 0]) < 0.1)][:, 2].max()
        def hang(Q):   # sous la poitrine le tablier tombe droit (pas collé au ventre ni entre les jambes)
            tgt = zb - 0.01 - 0.06 * np.clip((self.waist - Q[:, 1]) / 0.5, 0, 1)
            return 0.016 + np.clip(tgt - Q[:, 2], 0, 0.25) * np.clip((self.waist + 0.15 - Q[:, 1]) / 0.1, 0, 1)
        self.a.add('tablier', HP.cloth_cut(self.body, coarse, inside, 0.0, thick=0.004, uv_scale=1.4, bump=hang), mat, slot='Tenue')

    def skirt(self, mat, bottom=0.10, flare=1.0, top=None):
        """Robe longue (jupe) : tube elliptique de la taille aux chevilles, évasé, plis ; suit le bassin."""
        top = (self.waist + 0.02) if top is None else top
        hy = self.V[np.abs(self.V[:, 1] - (self.waist - 0.10)) < 0.015]
        rx0 = np.abs(hy[:, 0]).max() + 0.025; zc = (hy[:, 2].max() + hy[:, 2].min()) / 2; rz0 = (hy[:, 2].max() - hy[:, 2].min()) / 2 + 0.03
        L = top - bottom; n = 24
        path = np.stack([np.zeros(n), np.linspace(top, bottom, n), np.full(n, zc)], 1)
        rx = curve([(0, rx0 * 0.9), (0.25, rx0 * 1.05), (1, rx0 * (1.2 + 0.25 * flare))], n)
        rz = curve([(0, rz0 * 0.9), (0.25, rz0 * 1.05), (1, rz0 * (1.25 + 0.3 * flare))], n)
        m = Loft(path, rx, rz, p=2.0, segs=48).mesh(1.0)
        ang = np.arctan2(m.V[:, 0], m.V[:, 2] - zc); dy = np.clip((top - m.V[:, 1]) / L, 0, 1)
        r = np.hypot(m.V[:, 0], m.V[:, 2] - zc); k = 1 + 0.035 * np.sin(ang * 14) * dy
        m.V[:, 0] *= k; m.V[:, 2] = zc + (m.V[:, 2] - zc) * k
        blend_bones(m, self.sk['Hips'], self.sk['Spine'], np.clip((m.V[:, 1] - self.waist) / 0.1, 0, 1))
        inner = Mesh(m.V.copy(), m.F[:, ::-1].copy(), m.UV.copy()); inner.J, inner.W = m.J.copy(), m.W.copy()
        nrm = vertex_normals(m); inner.V = inner.V - nrm * 0.004
        self.a.add('robe', concat([m, inner]), mat, slot='Tenue')

    def shawl(self, mat):
        def inside(Q):
            return np.minimum(Q[:, 1] - (self.P['UpperChest'][1] - 0.10 - 0.06 * np.abs(np.sin(Q[:, 0] * 9))), 0.55 - self.side(Q, self.along(Q, 'LeftUpperArm', 'LeftLowerArm'), self.along(Q, 'RightUpperArm', 'RightLowerArm')))
        coarse = HP.bone_mask(self.body, self.sk, ['Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'Neck'], 0.2)
        self.a.add('chale', HP.cloth_cut(self.body, coarse, inside, 0.026, thick=0.004, uv_scale=2.0), mat, slot='Tenue')

    def hat(self, mat, brim=0.17, h=0.12):
        top = self.V[:, 1].max(); c = np.array([0, top - 0.045, self.V[self.V[:, 1] > top - 0.05][:, 2].mean()])
        crown = cylinder(c, 0.105, h, segs=20, r_top=0.09); br = cylinder(c + (0, -0.005, 0), brim, 0.012, segs=24)
        m = concat([crown, br]); blend_bones(m, self.sk['Head'], self.sk['Head'], np.zeros(len(m.V)))
        self.a.add('chapeau', m, mat, slot='Tenue')

    def chain(self, mat, drop=0.16):
        n0 = self.P['Neck']; pts = []
        for t in np.linspace(-1, 1, 25):
            a = t * 2.4; y = n0[1] - 0.02 - drop * (1 - abs(t) ** 1.5)
            pts.append((np.sin(a) * 0.075 * (1 + 0.3 * (1 - abs(t))), y, 0.03 + np.cos(a) * 0.07 + 0.06 * (1 - abs(t))))
        m = tube(np.array(pts), 0.006, segs=6, tile=0.05); blend_bones(m, self.sk['UpperChest'], self.sk['UpperChest'], np.zeros(len(m.V)))
        self.a.add('chaine', m, mat, slot='Tenue')


def npc(name, female, look, outfit, recipes):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        prefix = 'NP' + name[4:7].upper()
        body, marks = MOD.base(sk, a, bank, prefix, female=female, look=look)
        # pas de tenue de départ ni d'anatomie : seuls le corps visible et les vêtements du métier
        a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'HaillonsHaut', 'HaillonsBas')]
        bank.recipes.update(recipes)
        T = Tailor(sk, a, body, marks, female, prefix, bank)
        outfit(T)
        # peau cachée sous les vêtements couvrants : retirée du maillage (moins de triangles, pas de transparence)
        hidden = {'Peau_Torse', 'Peau_Bassin', 'SousVetementBas', 'SousVetementHaut'}
        a.parts = [p for p in a.parts if a.slot.get(p[0]) not in hidden]
        anims.build(sk, a)
        return a, {}, anims.CLIPS
    return build


L = KM.leather
CATALOG = {}


def _reg(name, female, look, outfit, recipes): CATALOG[name] = (npc(name, female, look, outfit, recipes), recipes)


# --- forgeron : colosse au crâne ras, chemise retroussée, grand tablier de cuir, gants
def _forgeron(T):
    T.shirt('PN_Linen', sleeve=0.75); T.trousers('PN_Wool', 1.0); T.boots('PN_Boot', 0.35); T.apron('PN_Apron', width=0.19)
    T.belt('PN_Boot', 'PN_Brass')
_reg('PNJ_Forgeron', False, dict(skin='middleage_caucasian_male', hair='short04', hair_tint=(0.05, 0.04, 0.03)), _forgeron,
     {'PN_Linen': _cloth((0.62, 0.56, 0.45), 201), 'PN_Wool': _cloth((0.22, 0.18, 0.14), 202, dirt=1.0), 'PN_Boot': (lambda S: L(S, tone=(0.12, 0.08, 0.05)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
      'PN_Apron': (lambda S: L(S, seed=37, tone=(0.20, 0.12, 0.07)), 512, 256, dict(metallic=0.0, smoothness=1.0)), 'PN_Brass': (lambda S: KM.brass(S), 256, 128, dict(metallic=1.0, smoothness=1.0))})


# --- armurier : doublet matelassé, ceinture à boucle, bottes hautes
def _armurier(T):
    T.shirt('PN_Gamb', sleeve=1.95, hem=T.waist - 0.20, off=0.014); T.trousers('PN_Wool2', 1.0); T.boots('PN_Boot', 0.10); T.belt('PN_Boot', 'PN_Brass')
_reg('PNJ_Armurier', False, dict(skin='young_caucasian_male', hair='short02', hair_tint=(0.18, 0.11, 0.06)), _armurier,
     {'PN_Gamb': (lambda S: KM.gambeson(S, seed=71, color=(0.20, 0.17, 0.12)), 512, 256, dict(metallic=0.0, smoothness=1.0)), 'PN_Wool2': _cloth((0.16, 0.14, 0.12), 203),
      'PN_Boot': (lambda S: L(S, tone=(0.10, 0.07, 0.045)), 512, 256, dict(metallic=0.0, smoothness=1.0)), 'PN_Brass': (lambda S: KM.brass(S), 256, 128, dict(metallic=1.0, smoothness=1.0))})


# --- marchand : tunique verte, tablier écru, chapeau de feutre
def _marchand(T):
    T.shirt('PN_Green', sleeve=1.9, hem=T.waist - 0.22); T.trousers('PN_Brown', 1.0); T.boots('PN_Boot', 0.25); T.apron('PN_Cream', top=T.waist + 0.02, bottom=0.45, width=0.2)
    T.belt('PN_Boot'); T.hat('PN_Felt')
_reg('PNJ_Marchand', False, dict(skin='middleage_caucasian_male', hair='long01', hair_tint=(0.25, 0.17, 0.10)), _marchand,
     {'PN_Green': _cloth((0.18, 0.30, 0.16), 204), 'PN_Brown': _cloth((0.28, 0.20, 0.13), 205), 'PN_Cream': _cloth((0.75, 0.70, 0.58), 206, dirt=0.8),
      'PN_Boot': (lambda S: L(S, tone=(0.14, 0.09, 0.05)), 512, 256, dict(metallic=0.0, smoothness=1.0)), 'PN_Felt': _cloth((0.18, 0.12, 0.08), 207)})


# --- joaillier : robe longue bleu nuit, chaîne d'or, cheveux gris
def _joaillier(T):
    T.shirt('PN_Navy', sleeve=1.95, hem=T.waist - 0.05, off=0.012); T.skirt('PN_Navy', 0.08, 0.6); T.belt('PN_Gold', 'PN_Gold'); T.chain('PN_Gold')
    T.boots('PN_Boot', 0.75)
_reg('PNJ_Joaillier', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.55, 0.55, 0.55)), _joaillier,
     {'PN_Navy': _cloth((0.08, 0.10, 0.25), 208, dirt=0.3), 'PN_Gold': (lambda S: KM.brass(S, gold=(0.85, 0.65, 0.3)), 256, 128, dict(metallic=1.0, smoothness=1.0)),
      'PN_Boot': (lambda S: L(S, tone=(0.06, 0.05, 0.04)), 512, 256, dict(metallic=0.0, smoothness=1.0))})


# --- mystique : longue robe violette à capuche, liseré d'or
def _mystique(T):
    T.shirt('PN_Violet', sleeve=1.95, hem=T.waist - 0.05, off=0.014); T.skirt('PN_Violet', 0.05, 1.2); T.belt('PN_Gold', 'PN_Gold')
    def add(nm, m, mat): T.a.add(nm, m, mat, slot='Tenue')
    hood(T.a, add, T.body, T.marks, T.sk, cloth='PN_Violet', lining='PN_Dark', depth=0.8)
    T.boots('PN_Dark', 0.75)
_reg('PNJ_Mystique', True, dict(skin='young_caucasian_female2', hair='long01', hair_tint=(0.08, 0.06, 0.06)), _mystique,
     {'PN_Violet': _cloth((0.26, 0.10, 0.34), 209, dirt=0.3), 'PN_Dark': _cloth((0.05, 0.04, 0.06), 210, dirt=0.2),
      'PN_Gold': (lambda S: KM.brass(S, gold=(0.85, 0.65, 0.3)), 256, 128, dict(metallic=1.0, smoothness=1.0))})


# --- alchimiste : robe verte, tablier de cuir taché, tresse
def _alchimiste(T):
    T.shirt('PN_Linen2', sleeve=1.6, hem=T.waist - 0.05); T.skirt('PN_Moss', 0.10, 0.8); T.apron('PN_Apron2', top=T.waist + 0.25, bottom=0.35, width=0.18)
    T.belt('PN_Boot2', 'PN_Brass'); T.boots('PN_Boot2', 0.7)
_reg('PNJ_Alchimiste', True, dict(skin='young_caucasian_female', hair='braid01', hair_tint=(0.35, 0.18, 0.08)), _alchimiste,
     {'PN_Linen2': _cloth((0.70, 0.66, 0.55), 211), 'PN_Moss': _cloth((0.20, 0.27, 0.15), 212), 'PN_Apron2': (lambda S: L(S, seed=39, tone=(0.24, 0.16, 0.10)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
      'PN_Boot2': (lambda S: L(S, tone=(0.12, 0.08, 0.05)), 512, 256, dict(metallic=0.0, smoothness=1.0)), 'PN_Brass': (lambda S: KM.brass(S), 256, 128, dict(metallic=1.0, smoothness=1.0))})


# --- marchande de curiosités : jupe rouge, chemisier écru, châle sarcelle, bijoux
def _curiosites(T):
    T.shirt('PN_Cream2', sleeve=1.3, hem=T.waist - 0.05, off=0.012); T.skirt('PN_Red', 0.06, 1.4); T.shawl('PN_Teal'); T.belt('PN_Gold', 'PN_Gold'); T.chain('PN_Gold', 0.12)
    T.boots('PN_Boot3', 0.75)
_reg('PNJ_Curiosites', True, dict(skin='young_caucasian_female2', hair='o4saken_long01', hair_tint=(0.10, 0.07, 0.05)), _curiosites,
     {'PN_Cream2': _cloth((0.80, 0.74, 0.60), 213, dirt=0.4), 'PN_Red': _cloth((0.45, 0.08, 0.07), 214, patches=True, dirt=0.5), 'PN_Teal': _cloth((0.08, 0.30, 0.32), 215),
      'PN_Gold': (lambda S: KM.brass(S, gold=(0.85, 0.65, 0.3)), 256, 128, dict(metallic=1.0, smoothness=1.0)), 'PN_Boot3': (lambda S: L(S, tone=(0.10, 0.07, 0.05)), 512, 256, dict(metallic=0.0, smoothness=1.0))})
