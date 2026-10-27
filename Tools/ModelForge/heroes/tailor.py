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


from knight import anims as KA


class Civil(KA.Style):
    """Allure civile (PNJ) : bras le long du corps, mains détendues (ni poing ni pouce levé), balancement naturel."""
    name = 'civil'
    def __init__(self, down=76, spread_hands=0.35):
        self.down, self.curl = down, spread_hands
    def guard(self, p):
        p.arm('R', down=self.down, fwd=6, twist=-4, elbow=14, wrist=(-4, 0, 0)); p.arm('L', down=self.down, fwd=6, twist=4, elbow=14, wrist=(-4, 0, 0))
        p.fist('R', self.curl); p.fist('L', self.curl)
        return p
    def gait_arms(self, p, w, run, arm_amp):
        a = arm_amp * 0.9
        p.arm('R', down=self.down, fwd=6 + a * np.sin(w + np.pi), twist=-4, elbow=16 + 10 * max(0, np.sin(w + np.pi)), wrist=(-4, 0, 0))
        p.arm('L', down=self.down, fwd=6 + a * np.sin(w), twist=4, elbow=16 + 10 * max(0, np.sin(w)), wrist=(-4, 0, 0))
    def idle_arms(self, p, br):
        p.arm('R', down=self.down - 0.8 * br, fwd=6, twist=-4, elbow=14 + br, wrist=(-4, 0, 0))
        p.arm('L', down=self.down - 0.8 * br, fwd=6, twist=4, elbow=14 + br, wrist=(-4, 0, 0))


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
        self.layers = {}; self.placed = []; self.force = []; self.long_skirt = False     # (zone, prédicat(V) → bool) : peau toujours cachée

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
    def shirt(self, mat, hem, sleeve=0.98, neck=0.04, ease=0.012, clear=None, layer=2, name='chemise', back_neck=0.0, slit=0.0, max_ease=None, hem_rise=0.035, iters=90):
        """Chemise / cotte (haut) : du bas `hem` à l'encolure ; manches jusqu'à `sleeve` (1 = poignet, 0,55 = coude).
        neck : profondeur de l'encolure devant ; slit : fente devant (lacée) en m."""
        Pn = self.P['Neck']
        def inside(Q):
            x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
            arm = self.arm_of(Q)
            # encolure ronde : suit la base du cou, plus basse devant
            ny = Pn[1] - 0.010 - neck * np.exp(-(x / 0.065) ** 2) * (z > 0) - back_neck * np.exp(-(x / 0.07) ** 2) * (z < 0)
            r_neck = np.hypot(x / 0.075, (z - Pn[2]) / 0.065)
            top = ny + 0.06 * np.clip(r_neck - 0.85, 0, None)          # encolure lisse (sans palier)
            cut_slit = (slit > 0) & (z > 0) & (np.abs(x) < 0.004 + 0.002) & (y > Pn[1] - 0.02 - slit)
            body_ok = np.minimum(y - (hem + hem_rise * np.exp(-(x / 0.07) ** 2)), top - y)      # ourlet relevé à l'entrejambe
            arm_ok = sleeve - self.arm_t(Q)
            return np.where(arm, np.minimum(arm_ok * 0.6, top - y), body_ok) - cut_slit * 1.0
        coarse = (HP.bone_mask(self.body, self.sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Shoulder', 'UpperArm', 'LowerArm', 'UpperLeg'], 0.05)
                  & (self.V[:, 1] > hem - 0.05) & (self.V[:, 1] < Pn[1] + 0.06))
        e = ease
        m = D.drape(self.body, self.sk, coarse, inside, ease=e, clear=clear, iters=iters, arm_mask=self.arm_of,
                    max_ease=max_ease if max_ease is not None else 2.6 * (e if not callable(e) else 0.02), under=self.under(layer, 0.008))
        self.inherit(m, layer, ymax=self.waist + 0.02)
        return self.add(name, m, mat, layer)

    def hose(self, mat, low=0.40, ease=0.006, layer=1, name='chausses'):
        """Chausses / braies : de la taille aux mollets (rentrées dans les bottes)."""
        def inside(Q): return np.minimum(Q[:, 1] - low, self.waist - 0.02 - Q[:, 1])
        coarse = (self.legs | self.hips) & ~self.arms & (self.V[:, 1] > low - 0.04) & (self.V[:, 1] < self.waist + 0.02)
        m = D.drape(self.body, self.sk, coarse, inside, ease=ease, clear=0.004, iters=25, thick=0.0025, max_ease=0.02, under=self.under(layer, 0.004))
        top = self.waist - 0.02
        self.force += [('peau_bassin', lambda V, t=top: V[:, 1] < t - 0.02), ('peau_cuisses', lambda V, l=low: V[:, 1] > l + 0.02)]
        return self.add(name, m, mat, layer)

    def skirt(self, mat, y_top, y_hem, ease=0.03, flare=0.08, layer=3, name='jupe', front=None, waist_ease=None, follow=None, thick=0.004, shin=None):
        """Pans de cotte, robe, jupe ; front = demi-angle (rad) pour un tablier."""
        mask = (self.legs | self.hips) & ~self.arms & (self.V[:, 1] < y_top + 0.06)
        long_ = y_hem < 0.35
        if follow is None: follow = 1.0 if long_ else 0.85
        if shin is None: shin = 0.75 if long_ else 0.0
        if long_ and front is None:
            self.long_skirt = True                               # marche à petits pas (robe longue)
            self.force += [('peau_bassin', lambda V, t=y_top: V[:, 1] < t - 0.02), ('peau_cuisses', lambda V: np.ones(len(V), bool)),
                           ('peau_jambes', lambda V, h=y_hem: V[:, 1] > h + 0.10)]
        m = D.skirt(self.body, self.sk, mask, y_top, y_hem, ease=ease, flare=flare, front_only=front, waist_ease=waist_ease, follow=follow, thick=thick, shin=shin,
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
        self.force += [('peau_pieds', lambda V: np.ones(len(V), bool)), ('peau_jambes', lambda V, t=top: V[:, 1] < t - 0.03)]
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
            sh = self.P['LeftShoulder'] if 'LeftShoulder' in self.P else self.P['LeftUpperArm']
            bib_w = 0.105
            for sx in (1, -1):                                  # bretelles : coins de la bavette → épaules → croisées dans le dos → ceinture
                path = [(sx * (bib_w - 0.012), top_y - 0.015, 0.15), (sx * 0.095, top_y + 0.06, 0.12), (sx * 0.10, sh[1] + 0.02, 0.05),
                        (sx * 0.10, sh[1] + 0.03, -0.04), (sx * 0.08, sh[1] - 0.06, -0.12), (0.0, self.waist + 0.17, -0.14),
                        (-sx * 0.075, self.waist + 0.01, -0.14)]
                self.add(f'tablier_bretelle{sx}', self.strap(path, 0.026, layer + 1, gap=0.011), strap_mat, layer + 1)

    def trim(self, garment, mat, r=0.0045, sel=None, name=None, layer=None, out=0.002):
        """Bordure roulée le long des bords libres d'un vêtement (encolure, poignets, ourlet) : finit le bord
        proprement. sel(centre de la boucle) → bool choisit les boucles (ex. encolure seulement)."""
        from forge.geo import spline
        nm = garment; m = next(mm for n, mm, _ in self.a.parts if n == nm)
        lay = next(k for k, v in self.layers.items() if nm in v) if layer is None else layer
        no = getattr(m, 'n_outer', len(m.F))
        idx, inv = D._weld(m.V, 1e-4); Fw = inv[m.F[:no]]; Vw = m.V[idx]
        Nw = vertex_normals(Mesh(Vw, Fw, np.zeros((len(Vw), 2))), weld=False)
        _, be = D._border(Fw, len(Vw))
        # boucles de bord
        adj = {}
        for a_, b_ in be: adj.setdefault(a_, []).append(b_); adj.setdefault(b_, []).append(a_)
        seen = set(); loops = []
        for st in adj:
            if st in seen: continue
            lp = [st]; seen.add(st); cur = st
            while True:
                nx = [q for q in adj[cur] if q not in seen]
                if not nx: break
                cur = nx[0]; lp.append(cur); seen.add(cur)
            if len(lp) > 8: loops.append(lp)
        parts = []
        Jw, Ww = m.J[idx], m.W[idx]
        for k, lp in enumerate(loops):
            P = Vw[lp]; c = P.mean(0)
            if sel is not None and not sel(c): continue
            Q = P + Nw[lp] * (r + out)
            Q = np.vstack([Q, Q[:1]])
            t = tube(Q, r, segs=8, tile=0.05, cap=False)
            i = cKDTree(Vw).query(t.V)[1]; t.J, t.W = Jw[i].copy(), Ww[i].copy()
            parts.append(t)
        if parts: self.add(name or nm + '_bord', concat(parts), mat, lay)

    def strap(self, rough, width, layer, gap=0.006, thick=0.003, n=48):
        """Sangle posée sur ce qui est porté dessous : chaque point du tracé est d'abord placé sur la peau, puis sur la
        surface extérieure la plus proche (vêtements), écarté de gap."""
        from forge.geo import spline, Loft
        from scipy.ndimage import uniform_filter1d
        lower = [x for x in self.placed if x[2] < layer]
        Vc = np.vstack([x[0] for x in lower]); Nc = np.vstack([x[1] for x in lower])
        Jc = np.vstack([x[4] for x in lower]); Wc = np.vstack([x[5] for x in lower])
        tr = cKDTree(Vc)
        P = spline([np.asarray(p, float) for p in rough], n)
        out = []; nrm = []
        for p in P:
            q, nb, _ = HP.surface_point(self.body, p, 0.0)          # sur la peau d'abord
            idx = tr.query_ball_point(q, 0.08)
            if idx:                                                  # puis la couche la plus extérieure au-dessus de ce point
                Q = Vc[idx]; h = (Q - q) @ nb; lat = np.linalg.norm((Q - q) - np.outer(h, nb), axis=1)
                ok = lat < 0.012
                if ok.any():
                    j = np.array(idx)[ok][np.argmax(h[ok])]; out.append(Vc[j] + Nc[j] * (gap + thick / 2)); nrm.append(Nc[j]); continue
            out.append(q + nb * (gap + 0.01)); nrm.append(nb)
        Q = uniform_filter1d(np.array(out), 9, axis=0, mode='nearest')
        for _ in range(3):                                           # le lissage coupe les arrondis : on reprojette dehors
            j = tr.query(Q)[1]; h = ((Q - Vc[j]) * Nc[j]).sum(1)
            Q = Q + Nc[j] * np.clip(gap + thick / 2 - h, 0, None)[:, None]
        mm = Loft(spline(Q, n), thick / 2, width / 2, p=4.0, segs=8, ref=tuple(np.mean(nrm, 0))).mesh(0.1)
        i = tr.query(mm.V)[1]; mm.J, mm.W = Jc[i].copy(), Wc[i].copy()
        return mm

    def hide_covered(self, reach=0.07, border=0.05):
        """Retire les faces de peau entièrement recouvertes (le dessous n'est jamais vu). Un sommet est couvert par un
        vêtement s'il est sous lui (ou tout près) ET loin de ses bords (encolure, manches, ourlets : la peau y reste,
        aucun trou n'est visible). Il suffit qu'un vêtement le couvre."""
        gar = []
        names = sum(self.layers.values(), [])
        for n, m, _ in self.a.parts:
            if n not in names: continue
            no = getattr(m, 'n_outer', len(m.F))
            idx, inv = D._weld(m.V, 1e-4); Fw = inv[m.F[:no]]
            used = np.unique(Fw); Vw = m.V[idx]
            Nw = vertex_normals(Mesh(Vw, Fw, np.zeros((len(Vw), 2))), weld=False)
            bb, _ = D._border(Fw, len(Vw))
            gar.append((cKDTree(Vw[used]), Vw[used], Nw[used], cKDTree(Vw[bb]) if bb.any() else None))
        removed = 0
        for n, m, mt in self.a.parts:
            if not n.startswith('peau_'): continue
            cov = np.zeros(len(m.V), bool)
            for tv, V, NV, tb in gar:
                d, j = tv.query(m.V)
                rel = m.V - V[j]; h = (rel * NV[j]).sum(1)
                lat = np.linalg.norm(rel - h[:, None] * NV[j], axis=1)   # le sommet doit être SOUS le tissu (pas au-delà d'un bord)
                c = (h < -0.002) & (d < 0.15) & (lat < 0.015)                         # sous le tissu (pas seulement à côté : le cou reste)
                if tb is not None: c &= tb.query(m.V)[0] > border
                deep = (h < -0.025) & (d < 0.15) & (lat < 0.015)                      # bien en dessous du tissu (creux enjambés) : jamais visible
                if tb is not None: deep &= tb.query(m.V)[0] > 0.02
                cov |= c | deep
            for z, f in self.force:
                if n == z: cov |= f(m.V)
            if cov.mean() > 0.80: cov[:] = True                   # zone presque entièrement couverte : retirée en entier
            keep = ~cov[m.F].all(1)
            removed += int((~keep).sum()); m.F = m.F[keep]
        self.a.parts = [p for p in self.a.parts if len(p[1].F)]
        return removed


    # ------------------------------------------------------------------ tête et accessoires
    def head_points(self):
        hm = HP.bone_mask(self.body, self.sk, ['Head'], 0.5)
        hair = [m.V for n, m, _ in self.a.parts if n == 'cheveux']
        return self.V[hm], (np.vstack(hair) if hair else np.zeros((0, 3)))

    def hat(self, mat, band, brim=0.065, h=0.12, layer=6):
        """Chapeau de feutre à large bord (pèlerin, marchand) assis sur le crâne : ovale ajusté sur le contour tête +
        cheveux à la hauteur d'assise ; les cheveux au-dessus de l'assise sont retirés. Contrôle : écart ≤ 1,2 cm."""
        from world.props import cylinder, cone
        Hd, hair = self.head_points()
        top = Hd[:, 1].max()
        P = np.vstack([Hd, hair]) if len(hair) else Hd
        y_seat = top - (0.072 if not self.female else 0.068)
        ring = P[np.abs(P[:, 1] - y_seat) < 0.006]
        xr = np.abs(ring[:, 0]).max(); z0, z1 = ring[:, 2].min(), ring[:, 2].max(); zc = (z0 + z1) / 2
        Rx, Rz = xr + 0.005, (z1 - z0) / 2 + 0.005
        c = np.array([0, y_seat, zc])
        def oval(m, sx, sz):
            m.V[:, 0] = c[0] + (m.V[:, 0] - c[0]) * sx; m.V[:, 2] = c[2] + (m.V[:, 2] - c[2]) * sz; return m
        crown = oval(cylinder(c, 1.0, h * 0.75, segs=40, r_top=0.92, cap_top=False), Rx, Rz)
        dome = oval(cone(c + (0, h * 0.75, 0), 0.92, h * 0.25, segs=40, flare=0.0), Rx, Rz)
        brm = oval(cylinder(c + (0, -0.004, 0), 1.0, 0.009, segs=48), Rx + brim, Rz + brim)
        brm.V[:, 1] += 0.014 * ((brm.V[:, 0] - c[0]) / (Rx + brim)) ** 2
        rib = oval(cylinder(c + (0, 0.004, 0), 1.0, 0.026, segs=40, cap_top=False), Rx + 0.003, Rz + 0.003)
        hatm = concat([crown, dome, brm])
        for m in (hatm, rib): blend_bones(m, self.sk['Head'], self.sk['Head'], np.zeros(len(m.V)))
        self.add('chapeau', hatm, mat, layer); self.add('chapeau_ruban', rib, band, layer + 1)
        for n, m, mt in self.a.parts:
            if n == 'cheveux':
                cen = m.V[m.F].mean(1); keep = ~((cen[:, 1] > c[1] + 0.006) & (((cen[:, 0] / (Rx + 0.02)) ** 2 + ((cen[:, 2] - zc) / (Rz + 0.02)) ** 2) < 1))
                m.F = m.F[keep]
        t_ = np.linspace(0, 6.28, 48)
        ring = np.stack([np.sin(t_) * Rx, np.full(48, c[1]), zc + np.cos(t_) * Rz], 1)
        self.hat_gap = float(np.percentile(cKDTree(P).query(ring)[0], 75))
        assert self.hat_gap < 0.012, f'chapeau mal posé : écart {self.hat_gap * 100:.1f} cm'

    def necklace(self, mat, gem=None, drop=0.13, layer=6, r=0.0035):
        """Chaîne posée sur ce qui est porté au cou et à la poitrine (peau ou vêtement), pendentif au bas."""
        Pn = self.P['Neck']
        pts = []
        for t in np.linspace(-1, 1, 33):
            a_ = t * 2.6
            pts.append((np.sin(a_) * 0.075, Pn[1] - 0.02 - drop * (1 - abs(t) ** 1.6), Pn[2] + np.cos(a_) * 0.07 + 0.06 * (1 - abs(t))))
        m = self.strap(pts, 2 * r, layer, gap=0.006, thick=2 * r, n=60)
        self.add('collier', m, mat, layer)
        if gem:
            lower = [x for x in self.placed if x[2] < layer and x[3] != 'collier']
            Vc = np.vstack([x[0] for x in lower]); Nc = np.vstack([x[1] for x in lower])
            q0 = m.V[np.argmin(m.V[:, 1])]; j = cKDTree(Vc).query(q0)[1]
            n_ = Nc[j]; q = Vc[j] + n_ * 0.006
            g = rivet(q, n_, r=0.013, h=0.008, segs=10, rings=4)
            i = j; Jc = np.vstack([x[4] for x in lower]); Wc = np.vstack([x[5] for x in lower])
            g.J = np.repeat(Jc[i][None], len(g.V), 0); g.W = np.repeat(Wc[i][None], len(g.V), 0)
            self.add('pendentif', g, gem, layer)              # même couche que la chaîne (posé dessus)

    def hood(self, cloth, lining, depth=0.7, layer=7):
        """Capuche ample (pèlerine comprise) ; les cheveux qui la traverseraient sont retirés."""
        from heroes.varek import hood as _hood
        names = []
        def add(n, m, mat): self.add(n, m, mat, layer); names.append(n)
        _hood(self.a, add, self.body, self.marks, self.sk, cloth=cloth, lining=lining, depth=depth)
        hm = next(m for n, m, _ in self.a.parts if n == 'capuche')
        no = len(hm.F)
        Nh = vertex_normals(hm); tr = cKDTree(hm.V)
        for n, m, mt in self.a.parts:
            if n != 'cheveux': continue
            d, j = tr.query(m.V); h = ((m.V - hm.V[j]) * Nh[j]).sum(1)
            out = (h > -0.008) & (d < 0.06)                         # au ras ou au-delà de la capuche
            m.F = m.F[~out[m.F].any(1)]

    def shawl(self, mat, low_front=0.22, low_back=0.30, ease=0.022, layer=5):
        """Châle drapé sur les épaules, pointe dans le dos, pans croisés devant."""
        Pn = self.P['Neck']
        def inside(Q):
            x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
            low = np.where(z > 0, Pn[1] - low_front + 0.45 * np.abs(x) ** 1.3, Pn[1] - low_back + 0.9 * np.abs(x))
            top = Pn[1] - 0.01 - 0.03 * np.exp(-(x / 0.07) ** 2) * (z > 0)
            return np.minimum.reduce([y - low, top - y, 0.26 - np.abs(x)])
        coarse = HP.bone_mask(self.body, self.sk, ['Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'Neck', 'Spine'], 0.05) & (self.V[:, 1] > Pn[1] - 0.36) & (self.V[:, 1] < Pn[1] + 0.05)
        m = D.drape(self.body, self.sk, coarse, inside, ease=ease, clear=0.012, iters=60, arm_mask=self.arm_of, max_ease=0.06, under=self.under(layer, 0.011), thick=0.004)
        self.inherit(m, layer, reach=0.06)
        return self.add('chale', m, mat, layer)

    # ------------------------------------------------------------------ contrôle
    def check(self, clips=('Idle', 'Walk'), frames=6):
        skin = [n for n, m, _ in self.a.parts if n.startswith('peau_')]      # peau VISIBLE (les zones cachées ne sont pas rendues)
        L = [skin] + [self.layers[k] for k in sorted(self.layers)]
        return D.check(self.a, L, clips, frames)
