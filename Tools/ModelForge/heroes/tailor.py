"""Tailleur des PNJ médiévaux de Korvalune : chemises, cottes, surcots, chausses, robes, tabliers, ceintures, bottes,
chapeaux — coupés sur le corps DÉFINITIF du personnage (morphologie déjà appliquée), en couches qui ne se traversent
jamais (forge.drape.check sur les animations).

Couches (de la peau vers l'extérieur) :
  0 peau · 1 chausses · 2 bottes, chemise · 3 cotte / robe · 4 surcot, tablier · 5 ceinture, bijoux, sangles."""
import numpy as np
from scipy.spatial import cKDTree
from forge import humanparts as HP, drape as D
from forge.geo import Mesh, concat, vertex_normals, unit, rivet, tube, blend_bones

LAYER_OF = {}          # nom de pièce → couche


def shaped_body(a, body, marks, pre):
    """Corps après morphologie : positions reprises des zones de peau (pre = positions avant), normales soudées."""
    old = np.vstack([pre[n] for n, m, _ in a.parts if n in pre]); new = np.vstack([m.V for n, m, _ in a.parts if n in pre])
    tree = cKDTree(old)
    d, i = tree.query(body.V)
    b = Mesh(new[i], body.F.copy(), body.UV.copy(), body.J.copy(), body.W.copy())
    b.src = getattr(body, 'src', None)
    b.N = vertex_normals(b)
    # normales de la peau recalculées sur le corps entier (pas de cassure entre zones)
    t2 = cKDTree(b.V)
    for n, m, _ in a.parts:
        if n in pre: m.N = b.N[t2.query(m.V)[1]]
    mk = dict(marks)
    for k, v in marks.items():
        arr = np.asarray(v) if isinstance(v, (list, tuple, np.ndarray)) else None
        if arr is not None and arr.shape == (3,):
            j = tree.query(arr)[1]; mk[k] = arr + (new[j] - old[j])
    return b, mk


class Tailor:
    def __init__(self, sk, a, body, marks, female):
        self.sk, self.a, self.body, self.marks, self.female = sk, a, body, marks, female
        self.V = body.V
        self.P = {n: np.asarray(p, float) for n, p in zip(sk.names, sk.pos)}
        self.waist = 0.962 if female else 0.985
        self.tree = cKDTree(body.V)
        dom = body.J[np.arange(len(body.V)), np.argmax(body.W, 1)]
        nm = [n.replace('Left', '').replace('Right', '') for n in sk.names]
        self.is_arm_v = np.array([nm[j] in ('UpperArm', 'LowerArm', 'Hand') or nm[j][:5] in ('Thumb', 'Index', 'Middl', 'Ring', 'Littl') for j in dom])
        self.arms = HP.bone_mask(body, sk, ['UpperArm', 'LowerArm', 'Hand'], 0.25)
        self.legs = HP.bone_mask(body, sk, ['UpperLeg', 'LowerLeg'], 0.3)
        self.hips = HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.3)
        self.layers = {}; self.placed = []

    def add(self, n, m, mat, layer):
        self.a.add(n, m, mat, slot='Tenue'); self.layers.setdefault(layer, []).append(n)
        no = getattr(m, 'n_outer', len(m.F))
        used = np.unique(m.F[:no]); Nm = vertex_normals(m)
        self.placed.append((m.V[used], Nm[used], layer, n, m.J[used], m.W[used]))
        return m

    def inherit(self, m, layer, reach=0.035, ymax=9.0, names=None):
        """Le tissu reprend les poids du vêtement sur lequel il repose (il bouge avec lui : l'écart entre couches reste
        constant pendant les animations). Limité aux points à moins de `reach` d'un vêtement inférieur et sous ymax."""
        u = [x for x in self.placed if x[2] < layer and (names is None or x[3] in names)]
        if not u: return m
        V = np.vstack([x[0] for x in u]); J = np.vstack([x[4] for x in u]); W = np.vstack([x[5] for x in u])
        d, i = cKDTree(V).query(m.V)
        sel = (d < reach) & (m.V[:, 1] < ymax)
        m.J = m.J.copy(); m.W = m.W.copy(); m.J[sel] = J[i[sel]]; m.W[sel] = W[i[sel]]
        return m

    def under(self, layer, gap=0.004):
        """Surfaces extérieures des vêtements déjà posés sous `layer` (le nouveau tissu passe au-dessus)."""
        u = [(x[0], x[1]) for x in self.placed if x[2] < layer]
        if not u: return ()
        return [(np.vstack([x[0] for x in u]), np.vstack([x[1] for x in u]), gap)]

    def under_pts(self, layer, ymin=-1, ymax=9):
        u = [x[0] for x in self.placed if x[2] < layer]
        if not u: return None
        P = np.vstack(u); return P[(P[:, 1] > ymin) & (P[:, 1] < ymax) & (np.abs(P[:, 0]) < 0.30)]

    def arm_t(self, Q):
        """Position le long du bras : 0 à l'épaule, 1 au poignet (côté selon x)."""
        out = np.zeros(len(Q))
        for sd, sg in (('Left', 1), ('Right', -1)):
            a, b = self.P[sd + 'UpperArm'], self.P[sd + 'Hand']; d = b - a
            sel = np.sign(Q[:, 0]) == sg
            out[sel] = ((Q[sel] - a) @ d) / (d @ d)
        return out

    def arm_of(self, Q):
        return self.is_arm_v[self.tree.query(Q)[1]]

    # ------------------------------------------------------------------ haut
    def shirt(self, mat, hem, sleeve=0.98, neck=0.04, ease=0.012, clear=None, layer=2, name='chemise', back_neck=0.0, slit=0.0, max_ease=None):
        """Chemise / cotte (haut) : du bas `hem` à l'encolure ; manches jusqu'à `sleeve` (1 = poignet, 0,55 = coude).
        neck : profondeur de l'encolure devant ; slit : fente devant (lacée) en m."""
        Pn = self.P['Neck']
        def inside(Q):
            x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
            arm = self.arm_of(Q)
            # encolure ronde : suit la base du cou, plus basse devant
            ny = Pn[1] - 0.010 - neck * np.exp(-(x / 0.065) ** 2) * (z > 0) - back_neck * np.exp(-(x / 0.07) ** 2) * (z < 0)
            r_neck = np.hypot(x / 0.075, (z - Pn[2]) / 0.065)
            top = np.where(r_neck < 1.25, ny + 0.05 * np.clip(r_neck - 0.85, 0, None), 9)
            cut_slit = (slit > 0) & (z > 0) & (np.abs(x) < 0.004 + 0.002) & (y > Pn[1] - 0.02 - slit)
            body_ok = np.minimum(y - hem, top - y)
            arm_ok = sleeve - self.arm_t(Q)
            return np.where(arm, np.minimum(arm_ok * 0.6, top - y), body_ok) - cut_slit * 1.0
        coarse = (HP.bone_mask(self.body, self.sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Shoulder', 'UpperArm', 'LowerArm', 'UpperLeg'], 0.05)
                  & (self.V[:, 1] > hem - 0.05) & (self.V[:, 1] < Pn[1] + 0.06))
        e = ease
        m = D.drape(self.body, self.sk, coarse, inside, ease=e, clear=clear, iters=45, arm_mask=self.arm_of,
                    max_ease=max_ease if max_ease is not None else 2.6 * (e if not callable(e) else 0.02), under=self.under(layer, 0.008))
        self.inherit(m, layer, ymax=self.waist + 0.02)
        return self.add(name, m, mat, layer)

    def hose(self, mat, low=0.40, ease=0.006, layer=1, name='chausses'):
        """Chausses / braies : de la taille aux mollets (rentrées dans les bottes)."""
        def inside(Q): return np.minimum(Q[:, 1] - low, self.waist - 0.02 - Q[:, 1])
        coarse = (self.legs | self.hips) & ~self.arms & (self.V[:, 1] > low - 0.04) & (self.V[:, 1] < self.waist + 0.02)
        m = D.drape(self.body, self.sk, coarse, inside, ease=ease, clear=0.004, iters=25, thick=0.0025, max_ease=0.02, under=self.under(layer, 0.004))
        return self.add(name, m, mat, layer)

    def skirt(self, mat, y_top, y_hem, ease=0.03, flare=0.08, layer=3, name='jupe', front=None, waist_ease=None, follow=0.85, thick=0.004):
        """Pans de cotte, robe, jupe ; front = demi-angle (rad) pour un tablier."""
        mask = (self.legs | self.hips) & ~self.arms & (self.V[:, 1] < y_top + 0.06)
        m = D.skirt(self.body, self.sk, mask, y_top, y_hem, ease=ease, flare=flare, front_only=front, waist_ease=waist_ease, follow=follow, thick=thick,
                    extra=self.under_pts(layer, y_hem - 0.1, y_top + 0.06))
        return self.add(name, m, mat, layer)

    def belt(self, mat, buckle, y=None, width=0.040, ease=0.014, layer=5):
        y = (self.waist - 0.045) if y is None else y
        mask = (self.hips | self.legs) & ~self.arms & (np.abs(self.V[:, 1] - y) < 0.06)
        m = D.skirt(self.body, self.sk, mask, y + width / 2, y - width / 2, ease=ease, flare=0.0, thick=0.005, ny=4, follow=0.0,
                    extra=self.under_pts(layer, y - 0.05, y + 0.05))
        self.add('ceinture', m, mat, layer)
        if buckle is None: return
        # boucle devant, posée sur la ceinture
        Vb = m.V[(np.abs(m.V[:, 0]) < 0.02) & (m.V[:, 2] > 0)]
        q = Vb.mean(0) if len(Vb) else np.array([0, y, 0.15]); n_ = np.array([0, 0, 1.0])
        bk = rivet(q + n_ * 0.006, n_, r=0.019, h=0.006, segs=4, rings=2)
        bk.J = np.full((len(bk.V), 4), self.sk.names.index('Hips')); bk.W = np.zeros((len(bk.V), 4)); bk.W[:, 0] = 1
        self.add('boucle', bk, buckle, layer + 1)

    def boots(self, mat, top=0.46, layer=2, cuff=None):
        V = self.V
        self.add('bottes', HP.shell(self.body, HP.region(self.body, self.legs & (V[:, 1] < top) & (V[:, 1] > 0.085)), offset=0.011, thick=0.004,
                                    uv_scale=8, smooth=60, restore=0.002, inflate=0.004), mat, layer)
        for sd in ('Left', 'Right'): self.add('pied_' + sd, HP.boot_last(self.body, self.sk, sd, margin=0.011, sole=0.013), mat, layer)
        if cuff:
            mask = self.legs & (V[:, 1] < top + 0.05) & (V[:, 1] > top - 0.10)
            for sd, sg in (('Left', 1), ('Right', -1)):
                pass

    def apron(self, mat, strap_mat, y_top=None, y_hem=0.42, half=0.95, bib=True, layer=4):
        """Tablier de cuir : pan devant (enveloppe des jambes, ne passe pas entre elles), bavette, sangles cou/taille."""
        y_top = self.waist - 0.02 if y_top is None else y_top
        self.skirt(mat, y_top, y_hem, ease=0.028, flare=0.10, layer=layer, name='tablier', front=half, follow=0.85, thick=0.006)
        if bib:
            Pn = self.P['Neck']
            def bib_in(Q):
                x, y = Q[:, 0], Q[:, 1]
                w = 0.105 + 0.03 * np.clip((y_top + 0.05 - y) / 0.2, 0, 1)
                return np.minimum.reduce([y - (y_top + 0.005), Pn[1] - 0.17 - y, w - np.abs(x)])
            coarse = HP.bone_mask(self.body, self.sk, ['Spine', 'Chest', 'UpperChest', 'Hips'], 0.05) & (self.V[:, 2] > 0.0) & (self.V[:, 1] > y_top - 0.06) & (np.abs(self.V[:, 0]) < 0.17)
            m = D.drape(self.body, self.sk, coarse, bib_in, ease=0.032, clear=0.020, iters=30, thick=0.006, max_ease=0.06, under=self.under(layer, 0.010))
            self.inherit(m, layer, reach=0.06)
            self.add('tablier_bavette', m, mat, layer)
            top_y = Pn[1] - 0.17
            for sx in (-1, 1):
                pts = [(sx * 0.10, top_y + 0.01, 0.2), (sx * 0.075, Pn[1] - 0.02, 0.06), (0, Pn[1] + 0.005, -0.10)]
                self.add(f'tablier_sangle{sx}', HP.strap(self.body, pts, 0.046, 0.016, thick=0.003), strap_mat, layer + 1)

    # ------------------------------------------------------------------ contrôle
    def check(self, clips=('Idle', 'Walk'), frames=6):
        skin = [n for n, m, _ in self.a.parts if n.startswith('peau_')]      # peau VISIBLE (les zones cachées ne sont pas rendues)
        L = [skin] + [self.layers[k] for k in sorted(self.layers)]
        return D.check(self.a, L, clips, frames)
