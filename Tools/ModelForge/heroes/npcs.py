"""Marchands de Korvalune (personnages non joueurs), bâtis comme les héros : corps MakeHuman CC0, visage modelé par les
curseurs de morphologie (traits nettement masculins ou féminins, âge, corpulence), barbe pour certains hommes, et
vêtements faits avec les mêmes outils que les tenues des héros :
  - tuniques et chemises découpées sur le corps (manches, col), pantalons, ceintures à boucle ;
  - bottes en coque lissée sur forme de cordonnier ;
  - robes et tabliers en PANS suspendus à l'enveloppe du bassin (le tissu tombe des hanches, ne rentre jamais entre les
    jambes, ne laisse pas passer les genoux) ;
  - capuche paramétrée autour de la vraie tête, châle, chapeau assis sur le crâne (contrôle de contact).
Chaque marchand est vérifié : proportions, contact des accessoires, rendus de face, profil, dos et gros plan."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, humanparts as HP, armorparts as AP, morphs as MO
from forge.geo import Mesh, concat, tube, blend_bones, smoothstep, vertex_normals, unit, rivet
from knight import anims, materials as KM
from heroes import modular as MOD
from heroes.varek import hood
from world.props import cylinder, cone

FOLDER = 'Characters'
HEAD_VIEW = (0, 1.66, 0.05)
GEAR = []


# ------------------------------------------------------------------------------------------- visages
MALE_FACE = {'chin_chin_width': 0.55, 'chin_chin_prominent': 0.45, 'chin_chin_jaw_drop': 0.25, 'head_head_square': 0.55, 'cheek_cheek_bones': 0.25,
             'nose_nose_scale_horiz': 0.30, 'nose_nose_volume': 0.35, 'nose_nose_scale_depth': 0.25, 'neck_neck_scale_horiz': 0.55,
             'mouth_mouth_scale_horiz': 0.15, 'torso_torso_vshape': 0.35, 'measure_measure_bust_circ': -0.6, 'torso_torso_muscle_pectoral': -0.3}
FEMALE_FACE = {'chin_chin_width': -0.45, 'chin_chin_triangle': 0.35, 'chin_chin_prominent': -0.2, 'head_head_oval': 0.45, 'nose_nose_scale_horiz': -0.35,
               'nose_nose_volume': -0.35, 'nose_nose_point_width': -0.3, 'mouth_mouth_upperlip_volume': 0.35, 'mouth_mouth_lowerlip_volume': 0.40,
               'cheek_cheek_volume': 0.25, 'neck_neck_scale_horiz': -0.45, 'eyes_eye_scale': 0.2}


def bake_shape(a, body, marks, female, values):
    """Applique un jeu de curseurs (id → -1..1) à TOUTES les pièces (corps, vêtements, cheveux) puis l'intègre au maillage."""
    sl = [s for s in MO.sliders(female) if s['id'] in values]
    D = MO.shape_deltas(marks, sl)
    MO.apply(a, body, D)
    for _, m, _ in a.parts:
        sh = getattr(m, 'shapes', {})
        for sid, v in values.items():
            k = sid + ('_plus' if v > 0 else '_moins')
            if k in sh: m.V = m.V + sh[k] * abs(v)
        m.shapes = {}


# ------------------------------------------------------------------------------------------- tailleur
class Tailor:
    def __init__(self, sk, a, body, marks, female):
        self.sk, self.a, self.body, self.marks, self.female = sk, a, body, marks, female
        self.V = body.V; self.ax = np.abs(self.V[:, 0])
        self.waist = 0.962 if female else 0.985
        self.arms = HP.bone_mask(body, sk, ['UpperArm', 'LowerArm', 'Hand'], 0.25)
        self.legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
        self.hips = HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)
        self.env = None

    def add(self, n, m, mat): self.a.add(n, m, mat, slot='Tenue'); return m

    def W(self, m): return HP.weights_from(self.body, m)

    # ---- haut
    def tunic(self, mat, hem=None, sleeve_x=0.66, neck=0.05, loose=0.010, collar=None):
        """Tunique / chemise : corps (de l'ourlet au col) et manches jusqu'à |x| = sleeve_x (0,66 poignet, 0,40 coude)."""
        hem = self.waist - 0.14 if hem is None else hem
        V = self.V; ax = self.ax
        def body_in(Q):
            x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
            top = np.where(z > 0, 1.505 - neck * np.exp(-(x / 0.06) ** 2) - 0.25 * np.clip(np.abs(x) - 0.07, 0, None), 1.535)
            return np.minimum(y - hem, top - y)
        coarse = HP.bone_mask(self.body, self.sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Shoulder', 'UpperLeg'], 0.05) & ~self.arms & (V[:, 1] > hem - 0.05) & (V[:, 1] < 1.56) & (ax < 0.24)
        drop = lambda Q: loose + 0.018 * np.clip((self.waist + 0.05 - Q[:, 1]) / 0.25, 0, 1)        # plus ample sous la taille
        self.add('tunique', HP.cloth_cut(self.body, coarse, body_in, 0.0, thick=0.003, uv_scale=6.0, bump=drop), mat)
        if sleeve_x > 0.16:
            def sl(Q): return np.minimum(np.abs(Q[:, 0]) - 0.15, sleeve_x - np.abs(Q[:, 0]))
            coarse = (self.arms | HP.bone_mask(self.body, self.sk, ['Shoulder'], 0.3)) & (ax > 0.12) & (ax < sleeve_x + 0.04)
            wide = lambda Q: loose * 0.8 + 0.010 * np.clip((np.abs(Q[:, 0]) - 0.45) / 0.2, 0, 1)        # manche qui s'évase au poignet
            self.add('manches', HP.cloth_cut(self.body, coarse, sl, 0.0, thick=0.0025, uv_scale=6.0, bump=wide), mat)
        if collar:
            self.add('col', HP.cloth_cut(self.body, HP.bone_mask(self.body, self.sk, ['Neck', 'UpperChest'], 0.2) & (V[:, 1] > 1.47) & (V[:, 1] < 1.57),
                                         lambda Q: np.minimum(Q[:, 1] - 1.49, 1.535 - Q[:, 1]), 0.014, thick=0.004, uv_scale=6.0), collar)

    def trousers(self, mat, low=0.40):
        self.add('pantalon', HP.cloth_cut(self.body, (self.legs | self.hips) & ~self.arms & (self.V[:, 1] > low - 0.04) & (self.V[:, 1] < self.waist + 0.01),
                                          lambda Q: np.minimum(Q[:, 1] - low, self.waist - 0.03 - Q[:, 1]), 0.006, thick=0.0025, uv_scale=6.0), mat)

    def boots(self, mat, top=0.50, cuff=None):
        V = self.V
        self.add('bottes', HP.shell(self.body, HP.region(self.body, self.legs & (V[:, 1] < top) & (V[:, 1] > 0.085)), offset=0.0085, thick=0.004, uv_scale=8, smooth=80, restore=0.002, inflate=0.004), mat)
        for sd in ('Left', 'Right'): self.add('pied_' + sd, HP.boot_last(self.body, self.sk, sd, margin=0.011, sole=0.013), mat)
        if cuff:
            self.add('bottes_revers', HP.cloth_cut(self.body, self.legs & (V[:, 1] < top + 0.04) & (V[:, 1] > top - 0.08),
                                                   lambda Q: np.minimum(Q[:, 1] - (top - 0.045), top + 0.005 - Q[:, 1]), 0.014, thick=0.004, uv_scale=6.0), cuff)

    def belt(self, mat, buckle):
        V = self.V; y0, y1 = self.waist - 0.075, self.waist - 0.033
        self.add('ceinture', HP.cloth_cut(self.body, (V[:, 1] > y0 - 0.03) & (V[:, 1] < y1 + 0.03) & self.hips, lambda Q: np.minimum(Q[:, 1] - y0, y1 - Q[:, 1]),
                                          0.012, thick=0.004, uv_scale=6.0), mat)
        q, n_, _ = HP.surface_point(self.body, (0, (y0 + y1) / 2, 0.2), 0.018)
        self.add('boucle', self.W(rivet(q + n_ * 0.004, n_, r=0.020, h=0.006, segs=4, rings=2)), buckle)

    # ---- pans suspendus au bassin (robes, tabliers)
    def envelope(self):
        if self.env is None:
            self.env = AP.hip_envelope(self.body, (self.legs | self.hips) & (self.ax < 0.30), y0=0.05)
        return self.env

    def _skin_panel(self, mm):
        yv = mm.V[:, 1]; f = smoothstep((self.waist - 0.10 - yv) / 0.40) * 0.85
        wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
        mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
        mm.J[:, 0] = self.sk['Hips']; mm.W[:, 0] = 1 - f
        mm.J[:, 1] = self.sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = self.sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
        return mm

    def skirt(self, mat, trim, length=0.84, n=14, flare=0.10, seed=0):
        """Robe longue : n pans qui se chevauchent tout autour du bassin (devant compris), bords droits, liseré."""
        env = self.envelope(); ytop = self.waist - 0.06
        for k in range(n):
            th = 2 * np.pi * k / n
            m, edge = AP.lappet(env, th, ytop, length, 0.135 * 14 / n * 1.15, off=0.016 + 0.002 * (k % 2), flare=flare, seed=seed + k, notch=0.0, tip=0.0)
            self.add(f'robe{k}', self._skin_panel(m), mat); self.add(f'robe_ourlet{k}', self._skin_panel(edge), trim)

    def apron(self, mat, trim, length=0.48, width=0.17, bib=True):
        """Tablier de cuir : pan large devant (de la taille aux genoux), bavette sur la poitrine, bretelles."""
        env = self.envelope(); ytop = self.waist - 0.05
        m, edge = AP.lappet(env, 0.0, ytop, length, width, off=0.034, flare=0.03, seed=91, notch=0.0, tip=0.0, thick=0.006)
        self.add('tablier', self._skin_panel(m), mat); self.add('tablier_bord', self._skin_panel(edge), trim)
        V = self.V
        if bib:
            def bib_in(Q):
                x, y = Q[:, 0], Q[:, 1]
                return np.minimum.reduce([y - (self.waist - 0.06), 1.36 - y, (0.11 + 0.04 * np.clip((self.waist + 0.1 - y) / 0.2, 0, 1)) - np.abs(x)])
            coarse = HP.bone_mask(self.body, self.sk, ['Spine', 'Chest', 'UpperChest', 'Hips'], 0.05) & (V[:, 2] > 0.03) & (V[:, 1] > self.waist - 0.09) & (V[:, 1] < 1.39) & (self.ax < 0.18)
            self.add('tablier_bavette', HP.cloth_cut(self.body, coarse, bib_in, 0.020, thick=0.005, uv_scale=5.0), mat)
            for sx in (-1, 1):
                self.add(f'tablier_bretelle{sx}', HP.strap(self.body, [(sx * 0.09, 1.35, 0.2), (sx * 0.10, 1.47, 0.0), (sx * 0.07, 1.30, -0.2), (sx * 0.02, self.waist - 0.04, -0.2)], 0.022, 0.022, thick=0.003), mat)

    # ---- tête
    def head_points(self):
        hm = HP.bone_mask(self.body, self.sk, ['Head'], 0.5)
        hair = [m.V for n, m, _ in self.a.parts if n == 'cheveux']
        return self.V[hm], (np.vstack(hair) if hair else np.zeros((0, 3)))

    def hat(self, mat, band, brim=0.065, h=0.12):
        """Chapeau assis sur le crâne : base à la hauteur où la tête (et les cheveux) atteignent la largeur intérieure ;
        les cheveux qui dépasseraient au-dessus de la base sont retirés. Contrôle : écart tête-chapeau ≤ 1,2 cm."""
        Hd, hair = self.head_points()
        top = Hd[:, 1].max()
        P = np.vstack([Hd, hair]) if len(hair) else Hd
        # assise : un peu au-dessus des sourcils ; ellipse ajustée sur le contour tête + cheveux à cette hauteur
        y_seat = top - (0.072 if not self.female else 0.068)
        ring = P[np.abs(P[:, 1] - y_seat) < 0.006]
        xr = np.abs(ring[:, 0]).max(); z0, z1 = ring[:, 2].min(), ring[:, 2].max(); zc = (z0 + z1) / 2
        Rx, Rz = xr + 0.005, (z1 - z0) / 2 + 0.005
        c = np.array([0, y_seat, zc])
        def oval(m, sx, sz):      # cylindre circulaire → ovale ajusté à la tête
            m.V[:, 0] = c[0] + (m.V[:, 0] - c[0]) * sx; m.V[:, 2] = c[2] + (m.V[:, 2] - c[2]) * sz; return m
        R = 1.0
        crown = oval(cylinder(c, R, h * 0.75, segs=32, r_top=0.92, cap_top=False), Rx, Rz)
        dome = oval(cone(c + (0, h * 0.75, 0), 0.92, h * 0.25, segs=32, flare=0.0), Rx, Rz)
        brm = oval(cylinder(c + (0, -0.004, 0), 1.0, 0.009, segs=36), Rx + brim, Rz + brim)
        brm.V[:, 1] += 0.014 * ((brm.V[:, 0] - c[0]) / (Rx + brim)) ** 2       # bord relevé sur les côtés
        rib = oval(cylinder(c + (0, 0.004, 0), 1.0, 0.026, segs=32, cap_top=False), Rx + 0.003, Rz + 0.003)
        hatm = concat([crown, dome, brm])
        for m in (hatm, rib): blend_bones(m, self.sk['Head'], self.sk['Head'], np.zeros(len(m.V)))
        self.add('chapeau', hatm, mat); self.add('chapeau_ruban', rib, band)
        R = max(Rx, Rz)
        # les cheveux sous le chapeau : on retire ce qui dépasse au-dessus de l'assise (sinon ils traversent la calotte)
        for n, m, mt in self.a.parts:
            if n == 'cheveux':
                cen = m.V[m.F].mean(1); keep = ~((cen[:, 1] > c[1] + 0.008) & (((cen[:, 0] / (Rx + 0.02)) ** 2 + ((cen[:, 2] - zc) / (Rz + 0.02)) ** 2) < 1))
                m.F = m.F[keep]
        # contrôle de contact : la base intérieure du chapeau touche la tête ou les cheveux
        from scipy.spatial import cKDTree
        t_ = np.linspace(0, 6.28, 48)
        ring = np.stack([np.sin(t_) * Rx, np.full(48, c[1]), zc + np.cos(t_) * Rz], 1)
        self.hat_gap = float(np.percentile(cKDTree(P).query(ring)[0], 75))
        assert self.hat_gap < 0.012, f'chapeau mal posé : écart {self.hat_gap * 100:.1f} cm'

    def shawl(self, mat):
        V = self.V
        def sh(Q):
            x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
            low = np.where(z > 0, 1.30 + 0.5 * np.abs(x) - 0.05 * np.cos(x * 12), 1.24 + 0.3 * np.abs(x))
            return np.minimum.reduce([y - low, 1.53 - y, 0.36 - np.abs(x)])
        coarse = HP.bone_mask(self.body, self.sk, ['Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'Neck'], 0.05) & (V[:, 1] > 1.20) & (self.ax < 0.40)
        self.add('chale', HP.cloth_cut(self.body, coarse, sh, 0.024, thick=0.004, uv_scale=4.0, bump=lambda Q: 0.004 * np.sin(Q[:, 0] * 40)), mat)

    def necklace(self, mat, gem=None, drop=0.13):
        pts = []
        for t in np.linspace(-1, 1, 31):
            a_ = t * 2.5; pts.append((np.sin(a_) * 0.072, 1.47 - drop * (1 - abs(t) ** 1.6), 0.02 + np.cos(a_) * 0.065 + 0.05 * (1 - abs(t))))
        Q = np.array([HP.surface_point(self.body, p, 0.004)[0] for p in pts])         # posée sur la peau (+ 4 mm)
        self.add('collier', self.W(tube(Q, 0.0035, segs=6, tile=0.05)), mat)
        if gem:
            q, n_, _ = HP.surface_point(self.body, pts[15], 0.010)
            self.add('pendentif', self.W(rivet(q, n_, r=0.013, h=0.008, segs=10, rings=4)), gem)

    def beard(self, mat_name):
        self.add('barbe', HP.beard(self.body, self.marks, self.sk), mat_name)


# ------------------------------------------------------------------------------------------- matériaux
def _wool(color, seed, dirt=0.6):
    return (lambda S: MOD.rag_cloth(S, seed, color, patches=False, dirt=dirt), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _leather(tone, seed=31):
    return (lambda S: KM.leather(S, seed=seed, tone=tone), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _gamb(color, seed):
    return (lambda S: KM.gambeson(S, seed=seed, color=color), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _cloth(color, seed, thread=(0.70, 0.52, 0.22)):
    return (lambda W_, H_: KM.cloak(W_, H_, seed=seed, color=color, thread_color=thread, mud_amt=0.3), (512, 1024), (256, 512), dict(metallic=0.0, smoothness=1.0))
GOLD = (lambda S: KM.brass(S, seed=301, gold=(0.80, 0.60, 0.28)), 256, 128, dict(metallic=1.0, smoothness=1.0))
IRON = (lambda S: KM.steel(S, seed=302, tone=(0.25, 0.25, 0.26), polish=0.5), 256, 128, dict(metallic=1.0, smoothness=1.0))
def _beard(tone, seed):
    return (lambda S: KM.leather(S, seed=seed, tone=tone), 256, 128, dict(metallic=0.0, smoothness=1.0))


# ------------------------------------------------------------------------------------------- les marchands
def make(name, female, look, shape, outfit, recipes, beard=None):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        lk = dict(look); lk.setdefault('eye_color', (0.22, 0.14, 0.07) if not female else (0.20, 0.30, 0.18))
        if beard:   # barbe peinte dans la peau (poils fins, bien plus naturels que des mèches en géométrie)
            kw = dict(lk.get('skin_kw', {})); kw.update(beard_paint=0.95, beard_col=beard, stubble=0.8); lk['skin_kw'] = kw
        body, marks = MOD.base(sk, a, bank, 'NP' + name[4:8].upper(), female=female, look=lk)
        a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'HaillonsHaut', 'HaillonsBas')]
        bank.recipes.update(recipes)
        T = Tailor(sk, a, body, marks, female)
        outfit(T)
        bake_shape(a, body, marks, female, shape)
        hidden = {'Peau_Torse', 'Peau_Bassin', 'SousVetementBas', 'SousVetementHaut'}       # peau entièrement couverte : retirée
        a.parts = [p for p in a.parts if a.slot.get(p[0]) not in hidden]
        anims.build(sk, a)
        a.npc_checks = dict(hat_gap=getattr(T, 'hat_gap', None))
        return a, {}, anims.CLIPS
    return build


CATALOG = {}
def reg(name, female, look, shape, outfit, recipes, beard=None): CATALOG[name] = (make(name, female, look, shape, outfit, recipes, beard), recipes)


# Forgeron : colosse musclé, crâne ras, barbe sombre, chemise de lin aux manches courtes, grand tablier de cuir, bottes
def _forgeron(T):
    T.tunic('NF_Linen', sleeve_x=0.36, neck=0.07); T.trousers('NF_Wool'); T.boots('NF_Boot', 0.46); T.belt('NF_Boot', 'NF_Iron')
    T.apron('NF_Apron', 'NF_Boot', length=0.50, width=0.19)
reg('PNJ_Forgeron', False, dict(skin='middleage_caucasian_male', hair='short04', hair_tint=(0.05, 0.04, 0.03), skin_kw=dict(stubble=0.6)),
    {**MALE_FACE, 'corps_musculature': 0.9, 'corps_corpulence': 0.35, 'armslegs_upperarm_muscle': 0.7, 'neck_neck_scale_horiz': 0.8},
    _forgeron, {'NF_Linen': _wool((0.66, 0.60, 0.50), 401), 'NF_Wool': _wool((0.20, 0.17, 0.13), 402, 1.0), 'NF_Boot': _leather((0.10, 0.07, 0.045)),
                'NF_Apron': _leather((0.22, 0.13, 0.07), 37), 'NF_Iron': IRON, 'NF_Beard': _beard((0.04, 0.03, 0.025), 5)},
    beard=(0.05, 0.035, 0.025))

# Armurier : homme dans la force de l'âge, doublet matelassé à col montant, ceinture à boucle de fer, bottes à revers
def _armurier(T):
    T.tunic('NA_Gamb', hem=T.waist - 0.20, sleeve_x=0.66, neck=0.0, loose=0.013, collar='NA_Gamb'); T.trousers('NA_Wool'); T.boots('NA_Boot', 0.50, cuff='NA_Boot')
    T.belt('NA_Boot', 'NA_Iron')
reg('PNJ_Armurier', False, dict(skin='young_caucasian_male', hair='short02', hair_tint=(0.16, 0.10, 0.06)),
    {**MALE_FACE, 'corps_musculature': 0.6}, _armurier,
    {'NA_Gamb': _gamb((0.24, 0.20, 0.14), 71), 'NA_Wool': _wool((0.15, 0.13, 0.11), 403), 'NA_Boot': _leather((0.09, 0.06, 0.04)), 'NA_Iron': IRON})

# Marchand : homme corpulent, tunique verte, tablier écru, chapeau de feutre à ruban, barbe brune
def _marchand(T):
    T.tunic('NM_Green', hem=T.waist - 0.24, sleeve_x=0.64, neck=0.04, loose=0.016); T.trousers('NM_Brown'); T.boots('NM_Boot', 0.40)
    T.belt('NM_Boot', 'NM_Gold'); T.apron('NM_Cream', 'NM_Cream', length=0.42, width=0.18, bib=False); T.hat('NM_Felt', 'NM_Band')
reg('PNJ_Marchand', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.22, 0.15, 0.09), skin_kw=dict(stubble=0.4)),
    {**MALE_FACE, 'corps_corpulence': 0.8, 'stomach_stomach_pregnant': 0.4}, _marchand,
    {'NM_Green': _wool((0.17, 0.30, 0.16), 404), 'NM_Brown': _wool((0.26, 0.19, 0.12), 405), 'NM_Cream': _wool((0.76, 0.71, 0.60), 406, 0.8), 'NM_Boot': _leather((0.13, 0.08, 0.05)),
     'NM_Gold': GOLD, 'NM_Felt': _wool((0.16, 0.11, 0.07), 407, 0.3), 'NM_Band': _wool((0.42, 0.10, 0.07), 408, 0.2), 'NM_Beard': _beard((0.16, 0.10, 0.06), 6)},
    beard=(0.20, 0.12, 0.07))

# Joaillier : vieil homme mince, cheveux gris, longue robe bleu nuit à liseré d'or, chaîne et pendentif
def _joaillier(T):
    T.tunic('NJ_Navy', hem=T.waist - 0.06, sleeve_x=0.66, neck=0.0, collar='NJ_Gold'); T.skirt('NJ_Navy', 'NJ_Gold', length=0.86, seed=10)
    T.boots('NJ_Boot', 0.30); T.belt('NJ_Gold', 'NJ_Gold'); T.necklace('NJ_Gold', 'NJ_Gem', drop=0.16)
reg('PNJ_Joaillier', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.62, 0.62, 0.62)),
    {**MALE_FACE, 'corps_corpulence': -0.6, 'corps_musculature': -0.4}, _joaillier,
    {'NJ_Navy': _cloth((0.06, 0.08, 0.22), 409), 'NJ_Gold': GOLD, 'NJ_Boot': _leather((0.05, 0.045, 0.04)),
     'NJ_Gem': (lambda S: KM.crystal(S, seed=410, color=(0.9, 0.1, 0.15)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(0.8, 0.8, 0.8), emission_preview=(0.6, 0.1, 0.1)))})

# Mystique : femme, longue robe violette à capuche, liseré d'or, collier de cristal
def _mystique(T):
    T.tunic('NY_Violet', hem=T.waist - 0.06, sleeve_x=0.66, neck=0.03); T.skirt('NY_Violet', 'NY_Gold', length=0.88, flare=0.14, seed=20)
    T.belt('NY_Gold', 'NY_Gold'); T.boots('NY_Dark', 0.30); T.necklace('NY_Gold', 'NY_Crystal', drop=0.12)
    hood(T.a, lambda n, m, mat: T.a.add(n, m, mat, slot='Tenue'), T.body, T.marks, T.sk, cloth='NY_Violet', lining='NY_Dark', depth=0.7)
reg('PNJ_Mystique', True, dict(skin='young_caucasian_female2', hair='long01', hair_tint=(0.07, 0.05, 0.05), brows='eyebrow010'),
    {**FEMALE_FACE}, _mystique,
    {'NY_Violet': _cloth((0.22, 0.08, 0.30), 411), 'NY_Gold': GOLD, 'NY_Dark': _leather((0.03, 0.025, 0.03)),
     'NY_Crystal': (lambda S: KM.crystal(S, seed=412, color=(0.62, 0.22, 1.0)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(1.4, 1.4, 1.4), emission_preview=(0.6, 0.3, 1.0)))})

# Alchimiste : femme, chemise de lin, robe vert mousse, tablier de cuir taché, tresse rousse
def _alchimiste(T):
    T.tunic('NL_Linen', hem=T.waist - 0.06, sleeve_x=0.55, neck=0.05); T.skirt('NL_Moss', 'NL_Moss', length=0.86, seed=30)
    T.apron('NL_Apron', 'NL_Boot', length=0.62, width=0.16); T.belt('NL_Boot', 'NL_Gold'); T.boots('NL_Boot', 0.30)
reg('PNJ_Alchimiste', True, dict(skin='young_caucasian_female', hair='braid01', hair_tint=(0.36, 0.17, 0.07), brows='eyebrow010'),
    {**FEMALE_FACE}, _alchimiste,
    {'NL_Linen': _wool((0.72, 0.68, 0.57), 413, 0.5), 'NL_Moss': _cloth((0.17, 0.24, 0.12), 414), 'NL_Apron': _leather((0.25, 0.17, 0.10), 39),
     'NL_Boot': _leather((0.11, 0.075, 0.05)), 'NL_Gold': GOLD})

# Marchande de curiosités : femme, chemisier écru, jupe rouge, châle sarcelle, bijoux d'or
def _curiosites(T):
    T.tunic('NC_Cream', hem=T.waist - 0.06, sleeve_x=0.50, neck=0.07); T.skirt('NC_Red', 'NC_Gold', length=0.86, flare=0.16, seed=40)
    T.shawl('NC_Teal'); T.belt('NC_Gold', 'NC_Gold'); T.necklace('NC_Gold', 'NC_Gem', drop=0.11); T.boots('NC_Boot', 0.30)
reg('PNJ_Curiosites', True, dict(skin='young_caucasian_female2', hair='o4saken_long01', hair_tint=(0.09, 0.06, 0.04), brows='eyebrow010'),
    {**FEMALE_FACE, 'corps_corpulence': 0.2}, _curiosites,
    {'NC_Cream': _wool((0.80, 0.74, 0.60), 415, 0.4), 'NC_Red': _cloth((0.40, 0.06, 0.06), 416), 'NC_Teal': _wool((0.07, 0.28, 0.30), 417, 0.3),
     'NC_Gold': GOLD, 'NC_Boot': _leather((0.10, 0.07, 0.05)),
     'NC_Gem': (lambda S: KM.crystal(S, seed=418, color=(0.1, 0.8, 0.4)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(0.8, 0.8, 0.8), emission_preview=(0.1, 0.6, 0.3)))})
