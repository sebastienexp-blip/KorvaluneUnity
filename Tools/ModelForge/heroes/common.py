"""Outils communs aux héros : génération des textures (aperçu réduit ou export), matériaux d'aperçu, rendus de contrôle."""
import os, time
import numpy as np
from forge.rig import MaterialDef
from forge.render import posed_meshes, render, sheet

PREV = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'out')


class TexBank:
    """Textures générées à la demande, une seule fois par matériau. recipes[nom] = (fonction, taille export, taille aperçu, réglages)."""
    def __init__(self, recipes, preview=False):
        self.recipes, self.preview, self.sets = recipes, preview, {}

    def __getitem__(self, name):
        if name not in self.sets:
            fn, big, small, _ = self.recipes[name]
            sz = small if self.preview else big
            t0 = time.time()
            self.sets[name] = fn(*sz) if isinstance(sz, tuple) else fn(sz)
            print(f'    texture {name} {sz} {time.time() - t0:.1f}s', flush=True)
        return self.sets[name]

    def specs(self):
        return {n: r[3] for n, r in self.recipes.items()}

    def bind(self, asset):
        for mn in {p[2] for p in asset.parts}:
            sp = self.recipes[mn][3]
            asset.materials[mn] = MaterialDef(mn, images=self[mn].images(), metallic=sp.get('metallic', 1.0), smoothness=sp.get('smoothness', 1.0),
                                              emission=sp.get('emission_preview'), cutoff=sp.get('cutoff'))
        return asset


def turnaround(asset, tag, attachments=(), clip='Idle', frame=0, head=(0, 1.66, 0.04), headdist=1.2):
    c = asset.clips.get(clip) if clip else None
    pm = posed_meshes(asset, c, frame, attachments)
    ims = [render(pm, size=(480, 640), yaw=y, pitch=6, target=(0, 0.95, 0), dist=4.6) for y in (15, -40, 180)]
    ph = posed_meshes(asset, c, frame)
    ims.append(render(ph, size=(480, 640), yaw=12, pitch=3, target=head, dist=headdist))
    os.makedirs(PREV, exist_ok=True)
    sheet(ims, 4).save(os.path.join(PREV, f'{tag}_still.png'))


def anim_sheet(asset, tag, attachments=(), frames=5, yaw=40, clips=None):
    ims, labels = [], []
    for name, c in asset.clips.items():
        if clips and name not in clips: continue
        for k in range(frames):
            f = int(round(k * (c.n - 1) / (frames - 1)))
            off = c.pos['Hips'][f] if 'Hips' in c.pos else np.zeros(3)
            tgt = (off[0], 0.95, off[2]) if name == 'Dodge' else (0, 0.9, 0)
            ims.append(render(posed_meshes(asset, c, f, attachments), size=(300, 380), yaw=yaw, pitch=8, target=tgt, dist=5.2, ss=1))
            labels.append(f'{name} {f}/{c.n - 1}')
    sheet(ims, frames, labels).save(os.path.join(PREV, f'{tag}_anims.png'))


def add_head(sk, asset, fp, mats, hair_style='short', beard=False, center=(0, 1.705, 0.012), neck=True, hair_len=0.45, seed=3, skip_hair=False):
    """Tête complète (visage, yeux, oreilles, cheveux, barbe, cou) pondérée sur Head / Neck / UpperChest.
    mats : {'skin':…, 'eye':…, 'hair':…}."""
    from forge import face as FC
    from forge.geo import rigid, smoothstep, Loft, spline, curve
    c = np.array(center)
    hm, marks = FC.head_mesh(fp, c); rigid(hm, sk['Head']); asset.add('tete', hm, mats['skin'])
    e = FC.eye_meshes(fp, c, marks); rigid(e, sk['Head']); asset.add('yeux', e, mats['eye'])
    ea = FC.ears(fp, c); rigid(ea, sk['Head']); asset.add('oreilles', ea, mats['skin'])
    def hairw(m):
        y = m.V[:, 1]; f = smoothstep((1.60 - y) / 0.10)                 # mèches longues : suivent le buste plus bas
        m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4))
        m.J[:, 0] = sk['Head']; m.W[:, 0] = 1 - f; m.J[:, 1] = sk['UpperChest']; m.W[:, 1] = f
        return m
    if not skip_hair:
        h = FC.hair(fp, c, hair_style, np.random.default_rng(seed), length=hair_len); asset.add('cheveux', hairw(h), mats['hair'])
    if beard:
        b = FC.beard(fp, c); rigid(b, sk['Head']); asset.add('barbe', b, mats['hair'])
    if neck:
        path = spline([(0, 1.48, -0.012), (0, 1.56, -0.006), (0, 1.63, 0.0)], 12)
        lo = Loft(path, curve([(0, .060), (1, .052)], 12), curve([(0, .064), (1, .055)], 12), p=2.2, segs=32)
        m = lo.mesh(0.1)
        y = m.V[:, 1]; f = smoothstep((y - 1.52) / 0.10)
        m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4)); m.J[:, 0] = sk['Neck']; m.W[:, 0] = f; m.J[:, 1] = sk['UpperChest']; m.W[:, 1] = 1 - f
        asset.add('cou', m, mats['skin'])
    return marks


def mh_human(sk, asset, bank, prefix, targets, extra=(), skin_kw=None, iris=(0.3, 0.22, 0.12), glow=None, hair=None, hair_color=(0.05, 0.035, 0.03),
             beard=False, cut_y=1.50, body_mask=None, thickness=1.04, hair_len=0.55, hair_seed=3, brows=False, gray=0.0, brow_bands=(0, 1, 2), lashes=False):
    """Personnage MakeHuman au standard jeu : peau peinte, yeux HD, cils, sourcils, cheveux (« short » : couches ;
    « long » : couches + cartes), barbe en couches à contour lisse. body_mask(V) : zones de peau gardées (défaut : tête et cou)."""
    from forge import mhbase as MB, mhskin as SKN, humanparts as HP, hairgen as HG
    from forge.geo import vertex_normals, unit, spline, smoothstep
    body, _, marks = MB.build(sk, targets, extra, thickness=thickness)
    V = body.V
    keep = body_mask(V) if body_mask is not None else ((V[:, 1] > cut_y) & (np.abs(V[:, 0]) < 0.16))
    asset.add('peau', HP.submesh(body, HP.region(body, keep)), prefix + '_Skin')
    def headw(m):
        m.J = np.zeros((len(m.V), 4), np.int64); m.J[:, 0] = sk['Head']; m.W = np.zeros((len(m.V), 4)); m.W[:, 0] = 1; return m
    asset.add('yeux', headw(HP.mh_eyes(marks)), prefix + '_Eye')
    kw = dict(skin_kw or {})
    bank.recipes[prefix + '_Skin'] = (lambda S, kw=kw: SKN.skin(body, marks, S=S, **kw), 2048, 1024, dict(metallic=0.0, smoothness=1.0))
    bank.recipes[prefix + '_Eye'] = (lambda S: SKN.eye_tex(S, iris=iris, glow=glow), 1024, 256, dict(metallic=0.0, smoothness=1.0, emission=(0.6, 0.6, 0.6) if glow else (0, 0, 0)))
    bank.recipes[prefix + '_Hair'] = (lambda S: HG.strand_atlas(S, color=hair_color, gray=gray, curl=0.9 if hair == 'long' else 0.5), 2048, 1024,
                                      dict(metallic=0.0, smoothness=1.0, cutoff=0.45))
    HM = prefix + '_Hair'
    eL, eR, mo, jaw, top = (np.asarray(marks[k]) for k in ('eye_L', 'eye_R', 'mouth', 'jaw', 'top'))
    ey = (eL[1] + eR[1]) / 2
    la = marks['lashes']
    if lashes and len(la.V):   # cils géométriques (désactivés : l'aide MakeHuman tombe mal et dessine un trait d'eye-liner)
        uu = la.UV[:, 0]; vv = la.UV[:, 1]
        upper = vv > 0.955
        vloc = np.where(upper, (vv - 0.967) / (0.985 - 0.967), (vv - 0.927) / (0.947 - 0.927))
        la.UV = np.c_[(uu - 0.70) * 6.0, 1 - (0 + 1 - np.clip(vloc, 0.02, 0.98)) / 4.0]
        keepf = la.V[la.F].mean(1)[:, 1] > ey + 0.0015                 # cils du haut seulement (ceux du bas font un trait d'eye-liner)
        la.F = la.F[keepf]
        asset.add('cils', headw(la), HM)
    if brows:
        def brow_in(P):
            best = np.full(len(P), -1.0)
            for e in (eL, eR):
                s_ = np.sign(e[0]); t = (P[:, 0] - (e[0] - s_ * 0.021)) * s_ / 0.050
                yc = e[1] + 0.0195 + 0.0075 * np.sin(np.pi * np.clip(t * 1.2, 0, 1)) - 0.003 * t
                half = 0.0040 * (1 - 0.55 * np.clip(t, 0, 1) ** 1.5)
                d = half - np.abs(P[:, 1] - yc)
                d = np.where((t > -0.05) & (t < 1.0) & (P[:, 2] > e[2] - 0.025), d, -1)
                best = np.maximum(best, d)
            return best
        cmb = np.zeros(len(V), bool)
        for e in (eL, eR): cmb |= (np.abs(V[:, 0] - e[0]) < 0.040) & (np.abs(V[:, 1] - e[1] - 0.022) < 0.016) & (V[:, 2] > e[2] - 0.03)
        uvb = lambda P: (P[:, 1] / (0.010 if brow_bands[0] == 0 else 0.045), np.clip((np.abs(P[:, 0]) - 0.008) / 0.055, 0, 1))
        asset.add('sourcils', headw(HG.smooth_shells(body, cmb, brow_in, uvb, [0.0005, 0.0011, 0.0017][:len(brow_bands)], list(brow_bands), [0.0, 0.0008, 0.0016] if brow_bands[0] == 0 else [0.0, 0.0012, 0.0024], subdiv=3)), HM)
    if hair:
        fr_ = 0.050 if hair == 'short' else 0.045
        def scalp_in(P):
            ang_ = np.arctan2(P[:, 0], P[:, 2] - 0.0)
            front = np.clip(np.cos(ang_), 0, 1) ** 1.5
            line = ey + fr_ * front - 0.07 * np.clip(-np.cos(ang_), 0, 1) + 0.0025 * np.sin(ang_ * 23) + 0.0015 * np.sin(ang_ * 57)
            ear = np.exp(-(((np.abs(P[:, 0]) - 0.072) / 0.02) ** 2 + ((P[:, 1] - ey + 0.015) / 0.035) ** 2 + ((P[:, 2] - (eL[2] - 0.085)) / 0.035) ** 2)) * 0.03
            return np.minimum(P[:, 1] - line - ear, P[:, 1] - 1.545)
        cm = HP.scalp_mask(body, marks, front_rise=fr_ - 0.012, back_drop=0.085)
        ang = lambda P: np.arctan2(P[:, 0], P[:, 2] - 0.01)
        def uvs(P):
            hl = ey + 0.045
            return ang(P) / (2 * np.pi) * 9, np.clip((top[1] - P[:, 1]) / (top[1] - hl + 0.06), 0, 1)
        offs = [0.0012, 0.0030, 0.0050, 0.0072, 0.0095] if hair == 'short' else [0.0015, 0.004, 0.007, 0.010, 0.013, 0.016]
        bands = [0, 0, 1, 2, 3] if hair == 'short' else [0, 0, 0, 1, 2, 3]
        shr = [-0.002, 0.0, 0.002, 0.004, 0.006, 0.008][:len(offs)]
        asset.add('cheveux', headw(HG.smooth_shells(body, cm, scalp_in, uvs, offs, bands, shr, subdiv=1)), HM)
        if hair == 'long':
            rng = np.random.default_rng(hair_seed)
            paths, widths, normals = [], [], []
            hc = np.array([0, (ey + top[1]) / 2 - 0.005, (eL[2] + top[2]) / 2 - 0.05])
            for k in range(170):
                a = rng.uniform(-np.pi, np.pi)
                if abs(a) < 0.62: continue                                        # visage dégagé
                el = rng.uniform(0.15, 1.2)
                d0 = np.array([np.sin(a) * np.cos(el), np.sin(el), np.cos(a) * np.cos(el)])
                root = hc + d0 * np.array([0.090, 0.112, 0.108])
                ow = unit(np.array([np.sin(a), 0, np.cos(a)]))
                L = hair_len * rng.uniform(0.8, 1.05)
                pts = [root]
                for s in np.linspace(0.08, 1.0, 12):
                    y = root[1] - s * L
                    r = 0.100 + 0.05 * min(1, s * 2.5) + 0.02 * s
                    p = np.array([0, y, hc[2]]) + ow * r
                    if y < 1.52:                                                   # épaules : devant ou dans le dos
                        front = np.cos(a) > -0.2 and abs(np.sin(a)) > 0.5 and rng.random() < 0.5
                        p[0] = np.sign(np.sin(a)) * min(abs(p[0]) + 0.02, 0.16 + 0.04 * rng.random())
                        p[2] = 0.11 if front else -0.15 - 0.03 * rng.random()
                    pts.append(p + rng.normal(0, 0.004, 3) * s)
                P = spline(pts, 20)
                paths.append(P); widths.append(rng.uniform(0.035, 0.06)); normals.append(ow)
            cm = HG.cards(paths, widths, normals, bands=[int(rng.integers(0, 2)) for _ in paths], seed=hair_seed)
            y = cm.V[:, 1]; f = smoothstep((1.60 - y) / 0.12)
            cm.J = np.zeros((len(y), 4), np.int64); cm.W = np.zeros((len(y), 4)); cm.J[:, 0] = sk['Head']; cm.W[:, 0] = 1 - f; cm.J[:, 1] = sk['UpperChest']; cm.W[:, 1] = f
            asset.add('meches', cm, HM)
    if beard:
        def beard_in(P):
            x, y, z = P[:, 0], P[:, 1], P[:, 2]
            up = (mo[1] + 0.018 - 0.010 * np.clip(np.abs(x) / 0.05, 0, 1) - y)               # bord supérieur (joues)
            low = y - (jaw[1] - 0.030 + 0.02 * np.clip(-z / 0.05, 0, 1))                       # sous le menton → cou
            side = np.minimum(0.070 - np.abs(x), z + 0.005)
            lip = np.sqrt((x / 0.028) ** 2 + ((y - mo[1] + 0.001) / 0.0095) ** 2) - 1.0
            lip = np.where(z > mo[2] - 0.02, lip * 0.01, 1.0)
            return np.minimum(np.minimum(np.minimum(up, low), side), lip)
        cm = (V[:, 1] < mo[1] + 0.03) & (V[:, 1] > jaw[1] - 0.05) & (V[:, 2] > -0.04) & (np.abs(V[:, 0]) < 0.08) & (V[:, 1] > 1.50)
        uvbd = lambda P: (np.arctan2(P[:, 0], P[:, 2]) / (2 * np.pi) * 14, np.clip((mo[1] + 0.02 - P[:, 1]) / 0.08, 0, 1))
        asset.add('barbe', headw(HG.smooth_shells(body, cm, beard_in, uvbd, [0.0008, 0.0020, 0.0034, 0.0048], [0, 1, 2, 3], [0.0, 0.0015, 0.003, 0.0045], subdiv=1)), HM)
    return body, marks


def mh_character(sk, asset, bank, prefix, targets, extra=(), skin='young_caucasian_male', skin_tint=None, skin_kw=None,
                 eyes='brown', iris=None, glow=None, hair='short02', hair_tint=None, brows='eyebrow001', brow_tint=None,
                 lashes='eyelashes01', cut_y=1.50, body_mask=None, thickness=1.04, hair_cutoff=0.40, mouth=True, eye_color=None):
    """Personnage bâti avec les ressources MakeHuman CC0 : texture de peau photo (même dépliage UV que le corps), yeux HD,
    cheveux / sourcils / cils en proxys ajustés exactement sur le corps (poids de skinning hérités). Retourne (corps, repères)."""
    from forge import mhbase as MB, mhskin as SKN, humanparts as HP, mhassets as MA
    body, _, marks = MB.build(sk, targets, extra, thickness=thickness)
    V = body.V
    keep = body_mask(V) if body_mask is not None else ((V[:, 1] > cut_y) & (np.abs(V[:, 0]) < 0.16))
    asset.add('peau', HP.submesh(body, HP.region(body, keep)), prefix + '_Skin')
    def headw(m):
        m.J = np.zeros((len(m.V), 4), np.int64); m.J[:, 0] = sk['Head']; m.W = np.zeros((len(m.V), 4)); m.W[:, 0] = 1; return m
    asset.add('yeux', headw(HP.mh_eyes(marks)), prefix + '_Eye')
    sdir = MA.path('skins', skin); photo = os.path.join(sdir, [f for f in os.listdir(sdir) if f.endswith('.png')][0])
    kw = dict(skin_kw or {})
    bank.recipes[prefix + '_Skin'] = (lambda S, kw=kw: SKN.skin(body, marks, S=S, photo=photo, tint=skin_tint, **kw), 2048, 1024, dict(metallic=0.0, smoothness=1.0))
    if eye_color is not None:   # iris MakeHuman recoloré (rouge, violet…), éventuellement luisant
        bank.recipes[prefix + '_Eye'] = (lambda S: MA.eye_texset(eye_color, S, glow=glow), 1024, 512,
                                         dict(metallic=0.0, smoothness=1.0, emission=(1, 1, 1) if glow is not None else (0, 0, 0)))
    elif glow is not None or iris is not None:
        bank.recipes[prefix + '_Eye'] = (lambda S: SKN.eye_tex(S, iris=iris or (0.3, 0.22, 0.12), glow=glow), 1024, 512,
                                         dict(metallic=0.0, smoothness=1.0, emission=(0.6, 0.6, 0.6) if glow else (0, 0, 0)))
    else:
        ef = MA.path('eyes', 'materials', eyes + '_eye.png')
        bank.recipes[prefix + '_Eye'] = (lambda S: MA.texset(ef, S, smooth=0.85, alpha=False), 1024, 512, dict(metallic=0.0, smoothness=1.0))
    base = (marks['base_V'], marks['base_J'], marks['base_W'])
    for part, folder, mat, tint, cut, sm in (('cheveux', hair and 'hair/' + hair, '_Hair', hair_tint, hair_cutoff, 0.30),
                                             ('sourcils', brows and 'eyebrows/' + brows, '_Brow', brow_tint, 0.30, 0.25),
                                             ('cils', lashes and 'eyelashes/' + lashes, '_Lash', None, 0.30, 0.25),
                                             ('dents', mouth and 'teeth/teeth_base', '_Teeth', None, None, 0.70),
                                             ('langue', mouth and 'tongue/tongue01', '_Tongue', None, None, 0.55)):
        if not folder: continue
        p = MA.Proxy(folder)
        m = p.fit(base[0], J=base[1], W=base[2])
        asset.add(part, m, prefix + mat)
        spec = dict(metallic=0.0, smoothness=1.0)
        if cut is not None: spec['cutoff'] = cut
        bank.recipes[prefix + mat] = (lambda S, p=p, tint=tint, sm=sm, cut=cut: MA.texset(p, S, tint=tint, smooth=sm, alpha=cut is not None),
                                      2048 if part == 'cheveux' else 1024, 1024 if part == 'cheveux' else 512, spec)
    return body, marks
