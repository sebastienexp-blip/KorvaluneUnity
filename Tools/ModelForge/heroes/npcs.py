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
from world.props import cylinder, cone

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
        bones = ['Hips', 'Spine', 'Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'UpperLeg', 'Neck'] + (['LowerArm'] if sleeve > 0.8 else [])
        coarse = HP.bone_mask(self.body, self.sk, bones, 0.02) & (self.V[:, 1] > hem - 0.08)
        loose = lambda Q: off + 0.012 * np.clip((self.waist + 0.1 - Q[:, 1]) / 0.3, 0, 1)
        self.a.add('chemise', HP.cloth_cut(self.body, coarse, inside, 0.0, thick=0.003, uv_scale=1.6, bump=loose), mat, slot='Tenue')

    def trousers(self, mat, length=1.0, off=0.008):
        """Braies : de la taille jusqu'à length (0 genou → 1 cheville)."""
        def inside(Q):
            t = self.side(Q, self.along(Q, 'LeftLowerLeg', 'LeftFoot'), self.along(Q, 'RightLowerLeg', 'RightFoot'))
            return np.minimum(self.waist + 0.015 - Q[:, 1], length - t)
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'UpperLeg', 'LowerLeg', 'Spine'], 0.02) & (self.V[:, 1] < self.waist + 0.05)
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

    def section(self, y, band=0.012, bones=('Hips', 'UpperLeg', 'Spine')):
        """Contour du corps (jambes et bassin seulement, sans les bras) à la hauteur y : (xmin, xmax, zmin, zmax)."""
        m = HP.bone_mask(self.body, self.sk, list(bones), 0.3) & (np.abs(self.V[:, 1] - y) < band)
        Q = self.V[m]
        return Q[:, 0].min(), Q[:, 0].max(), Q[:, 2].min(), Q[:, 2].max()

    def apron(self, mat, top=None, bottom=None, width=0.17):
        """Tablier : bavette ajustée sur la poitrine (découpe sur le corps) + pan plat qui tombe droit de la taille aux
        genoux devant les jambes (surface réglée, légèrement galbée, petits plis), attaché par des liens dans le dos."""
        top = self.waist + 0.30 if top is None else top
        bottom = 0.50 if bottom is None else bottom
        yb = self.waist - 0.03
        if top > self.waist + 0.06:     # bavette
            def bib(Q): return np.minimum(np.minimum(top - Q[:, 1], Q[:, 1] - (yb - 0.01)), width * (0.75 + 0.25 * np.clip((Q[:, 1] - yb) / 0.2, 0, 1)) - np.abs(Q[:, 0]))
            coarse = HP.bone_mask(self.body, self.sk, ['Spine', 'Chest', 'UpperChest', 'Hips'], 0.02) & (self.V[:, 2] > 0.02) & (self.V[:, 1] > yb - 0.04) & (self.V[:, 1] < top + 0.04)
            self.a.add('tablier_bavette', HP.cloth_cut(self.body, coarse, bib, 0.012, thick=0.004, uv_scale=1.4), mat, slot='Tenue')
        # pan : du devant du bassin, tombant à la verticale (en avant des cuisses)
        x0, x1, z0, z1 = self.section(yb)
        zl = max(z1, self.section(0.75)[3], self.section(0.62, bones=('UpperLeg', 'LowerLeg'))[3]) + 0.02
        nu, nv = 14, 18
        U = np.linspace(-1, 1, nu); Vv = np.linspace(0, 1, nv)
        W = width * 1.25
        G = []
        for v in Vv:
            y = yb - v * (yb - bottom)
            zfront = z1 + 0.012 + (zl - z1) * np.clip(v * 3, 0, 1)            # du ventre jusqu'à l'aplomb des cuisses
            row = []
            for u in U:
                x = u * W * (0.85 + 0.2 * v)
                z = zfront - 0.06 * u * u * (1 - 0.5 * v) + 0.010 * np.sin(u * 11 + 0.7) * v ** 0.7   # galbe autour du bassin, plis qui s'ouvrent vers le bas
                if v > 0.88: y += (abs(u) ** 4) * 0.05 * (v - 0.88) / 0.12                      # coins arrondis
                row.append((x, y, z))
            G.append(row)
        G = np.array(G).reshape(-1, 3)
        idx = np.arange(nu * nv).reshape(nv, nu)
        F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1), np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
        UV = np.stack(np.meshgrid(U * W, Vv * (yb - bottom)), -1).reshape(-1, 2) * 1.4
        front = Mesh(G, F, UV)
        if vertex_normals(front)[:, 2].mean() < 0: front.F = front.F[:, ::-1].copy()
        back = Mesh(G - np.array([0, 0, 0.004]), front.F[:, ::-1].copy(), UV.copy())
        pan = concat([front, back])
        # le haut suit le bassin, le bas suit un peu les cuisses (gauche / droite)
        t = np.clip((yb - pan.V[:, 1]) / (yb - bottom), 0, 1)
        side = 'LeftUpperLeg' if True else None
        pan.J = np.zeros((len(pan.V), 4), np.int64); pan.W = np.zeros((len(pan.V), 4))
        pan.J[:, 0] = self.sk['Hips']; pan.J[:, 1] = np.where(pan.V[:, 0] > 0, self.sk['LeftUpperLeg'], self.sk['RightUpperLeg'])
        pan.W[:, 1] = 0.35 * t * np.clip(np.abs(pan.V[:, 0]) / W, 0, 1); pan.W[:, 0] = 1 - pan.W[:, 1]
        self.a.add('tablier', pan, mat, slot='Tenue')
        # liens noués dans le dos
        lace = lambda Q: 0.008 - np.abs(Q[:, 1] - yb)
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'Spine'], 0.02) & (np.abs(self.V[:, 1] - yb) < 0.05)
        self.a.add('tablier_liens', HP.cloth_cut(self.body, coarse, lace, 0.014, thick=0.004, uv_scale=6.0), mat, slot='Tenue')

    def skirt(self, mat, bottom=0.10, flare=1.0, top=None):
        """Robe longue : sections réelles du corps (bassin, hanches, cuisses, genoux) relevées sur les jambes seulement ;
        tube qui enveloppe le bassin puis tombe librement en s'évasant jusqu'aux chevilles, plis verticaux."""
        top = (self.waist + 0.02) if top is None else top
        levels = [top - 0.01, self.waist - 0.10, self.waist - 0.20, 0.70, 0.50]
        secs = [self.section(y, bones=('Hips', 'UpperLeg', 'Spine', 'LowerLeg')) for y in levels]
        cx = 0.0; zc = np.mean([(s_[2] + s_[3]) / 2 for s_ in secs[:3]])
        # rayons : jamais plus étroits que la plus large section en dessous (le tissu tombe des hanches)
        rx = [max(abs(s_[0]), abs(s_[1])) + 0.02 for s_ in secs]; rz = [max(zc - s_[2], s_[3] - zc) + 0.02 for s_ in secs]
        hip_rx, hip_rz = max(rx[1:4]), max(rz[1:4])
        n = 28; ys = np.linspace(top, bottom, n)
        def prof(y, r_top, r_hip):
            if y > self.waist - 0.12: return np.interp(y, [self.waist - 0.12, top], [r_hip, r_top])
            f = np.clip((self.waist - 0.12 - y) / (self.waist - 0.12 - bottom), 0, 1)
            return r_hip * (1 + (0.18 + 0.22 * flare) * f ** 1.3)
        RX = np.array([prof(y, rx[0], hip_rx) for y in ys]); RZ = np.array([prof(y, rz[0], hip_rz) for y in ys])
        # jamais de jambe qui traverse : à chaque hauteur, le tube englobe les jambes (pose de repos écartée) + marge
        legs = self.V[HP.bone_mask(self.body, self.sk, ['UpperLeg', 'LowerLeg', 'Foot', 'Toes'], 0.2)]
        for i, y in enumerate(ys):
            if y > self.waist - 0.12: continue
            L_ = legs[np.abs(legs[:, 1] - y) < 0.03]
            if not len(L_): continue
            need_x = np.abs(L_[:, 0]).max() + 0.045; need_z = np.abs(L_[:, 2] - zc).max() + 0.045
            k_ = max(1.0, need_x / RX[i], need_z / RZ[i]); RX[i] *= k_; RZ[i] *= k_
        RX = np.maximum.accumulate(RX); RZ = np.maximum.accumulate(RZ)          # la robe ne se resserre pas en descendant
        segs = 56; ang = np.linspace(0, 2 * np.pi, segs + 1)
        V = []; UV = []
        for i, y in enumerate(ys):
            dy = np.clip((self.waist - 0.12 - y) / 0.6, 0, 1)
            fold = 1 + 0.045 * dy * np.sin(ang * 13) + 0.02 * dy * np.sin(ang * 5 + 1.0)
            V.append(np.stack([np.sin(ang) * RX[i] * fold, np.full_like(ang, y), zc + np.cos(ang) * RZ[i] * fold], 1))
            UV.append(np.stack([ang * 0.3, np.full_like(ang, y)], 1))
        V = np.vstack(V); UV = np.vstack(UV) * 1.6
        idx = np.arange(n * (segs + 1)).reshape(n, segs + 1)
        F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, :-1].ravel()], 1), np.stack([idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()], 1)])
        m = Mesh(V, F, UV)
        if (vertex_normals(m)[:, [0, 2]] * (m.V[:, [0, 2]] - [0, zc])).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
        inner = Mesh(m.V * np.array([0.985, 1, 1]) + np.array([0, 0, zc * 0.015]), m.F[:, ::-1].copy(), m.UV.copy())
        sk_ = concat([m, inner])
        # haut : bassin ; bas : un peu les cuisses (le tissu suit la marche sans se déchirer)
        t = np.clip((self.waist - 0.12 - sk_.V[:, 1]) / 0.6, 0, 1)
        sk_.J = np.zeros((len(sk_.V), 4), np.int64); sk_.W = np.zeros((len(sk_.V), 4))
        sk_.J[:, 0] = self.sk['Hips']; sk_.J[:, 1] = np.where(sk_.V[:, 0] > 0, self.sk['LeftUpperLeg'], self.sk['RightUpperLeg'])
        sk_.W[:, 1] = 0.25 * t * np.clip(np.abs(sk_.V[:, 0]) / max(hip_rx, 1e-3), 0, 1); sk_.W[:, 0] = 1 - sk_.W[:, 1]
        self.a.add('robe', sk_, mat, slot='Tenue')

    def shawl(self, mat):
        def inside(Q):
            return np.minimum(Q[:, 1] - (self.P['UpperChest'][1] - 0.10 - 0.06 * np.abs(np.sin(Q[:, 0] * 9))), 0.55 - self.side(Q, self.along(Q, 'LeftUpperArm', 'LeftLowerArm'), self.along(Q, 'RightUpperArm', 'RightLowerArm')))
        coarse = HP.bone_mask(self.body, self.sk, ['Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'Neck'], 0.2)
        self.a.add('chale', HP.cloth_cut(self.body, coarse, inside, 0.026, thick=0.004, uv_scale=2.0), mat, slot='Tenue')

    def hat(self, mat, brim=0.16, h=0.13):
        """Chapeau de feutre posé sur la tête : la base s'assoit là où le crâne atteint la largeur du chapeau
        (au-dessus des oreilles), calotte bombée, bord légèrement relevé."""
        hm = HP.bone_mask(self.body, self.sk, ['Head'], 0.5); H_ = self.V[hm]
        top = H_[:, 1].max(); zc = H_[H_[:, 1] > top - 0.06][:, 2].mean()
        rad = 0.0
        for y in np.arange(top - 0.01, top - 0.16, -0.005):
            ring = H_[np.abs(H_[:, 1] - y) < 0.004]
            if len(ring) and np.hypot(ring[:, 0], ring[:, 2] - zc).max() >= 0.088: rad = np.hypot(ring[:, 0], ring[:, 2] - zc).max(); break
        c = np.array([0, y + 0.012, zc - 0.005])
        R = rad + 0.022                                                     # place pour les cheveux
        crown = cylinder(c, R, h * 0.75, segs=24, r_top=R * 0.92, cap_top=False)
        dome = cone(c + (0, h * 0.75, 0), R * 0.92, h * 0.25, segs=24, flare=0.0)
        brm = cylinder(c + (0, -0.006, 0), R + brim * 0.6, 0.010, segs=28, r_top=R + brim * 0.6)
        band = cylinder(c + (0, 0.004, 0), R + 0.002, 0.026, segs=24, cap_top=False)
        m = concat([crown, dome, brm]); blend_bones(m, self.sk['Head'], self.sk['Head'], np.zeros(len(m.V)))
        self.a.add('chapeau', m, mat, slot='Tenue')
        blend_bones(band, self.sk['Head'], self.sk['Head'], np.zeros(len(band.V))); self.a.add('chapeau_ruban', band, 'PN_Band', slot='Tenue')
        self.bank.recipes.setdefault('PN_Band', _cloth((0.35, 0.08, 0.06), 230, dirt=0.2))

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
