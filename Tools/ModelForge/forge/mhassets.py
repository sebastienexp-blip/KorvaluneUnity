"""Ressources MakeHuman CC0 (packs officiels « system assets », « hair 01 », « eyebrows 01 », « eyelashes 01ˮ) :
proxys (.mhclo + .obj) ajustés sur le corps de base déformé, et textures (.mhmat → PNG) converties en TexSet.

Un proxy MakeHuman décrit chacun de ses sommets par trois sommets du maillage de base (indices + poids barycentriques)
et un décalage mis à l'échelle par trois distances de référence (x_scale, y_scale, z_scale). Comme les héros de Corvalune
sont construits sur ce même maillage de base (morphé puis reporté sur notre squelette), les cheveux, sourcils, cils, dents
et langue s'ajustent exactement, et héritent des poids de skinning des sommets de référence.
"""
import os
import numpy as np
from PIL import Image
from .geo import Mesh
from .texgen import TexSet, height_to_normal

A = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'third_party', 'makehuman', 'assets')


def path(*p): return os.path.join(A, *p)


def _mhmat(fn):
    d = {}
    for l in open(fn, encoding='utf-8', errors='replace'):
        l = l.strip()
        if not l or l[0] in '#/': continue
        k, _, v = l.partition(' ')
        d[k] = v.strip()
    return d


def _obj(fn):
    V, VT, F, FT = [], [], [], []
    for l in open(fn, encoding='utf-8', errors='replace'):
        if l.startswith('v '): V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith('vt '): VT.append([float(x) for x in l.split()[1:3]])
        elif l.startswith('f '):
            c = [q.split('/') for q in l.split()[1:]]
            vi = [int(a[0]) - 1 for a in c]; ti = [int(a[1]) - 1 if len(a) > 1 and a[1] else int(a[0]) - 1 for a in c]
            for k in range(1, len(vi) - 1): F.append((vi[0], vi[k], vi[k + 1])); FT.append((ti[0], ti[k], ti[k + 1]))
    return np.array(V) * 0.1, np.array(VT) if VT else np.zeros((len(V), 2)), np.array(F), np.array(FT)


class Proxy:
    def __init__(self, folder):
        self.folder = folder if os.path.isabs(folder) else path(folder)
        mh = [f for f in os.listdir(self.folder) if f.endswith('.mhclo')][0]
        self.scale = {}; refs = []; obj = None; mat = None; inverts = False
        for l in open(os.path.join(self.folder, mh), encoding='utf-8', errors='replace'):
            s = l.strip()
            if not s or s[0] == '#': continue
            p = s.split()
            if inverts and p[0] not in ('delete_verts',):
                try:
                    nums = [float(x) for x in p]
                except ValueError:
                    nums = None
                if nums is not None:
                    if len(nums) == 1: refs.append(([int(nums[0])] * 3, [1.0, 0, 0], [0.0, 0, 0])); continue
                    if len(nums) >= 9: refs.append(([int(x) for x in nums[:3]], nums[3:6], nums[6:9])); continue
            if p[0] == 'delete_verts': inverts = False
            if p[0] in ('x_scale', 'y_scale', 'z_scale'): self.scale[p[0][0]] = (int(p[1]), int(p[2]), float(p[3]))
            elif p[0] == 'obj_file': obj = p[1]
            elif p[0] == 'material': mat = p[1]
            elif p[0] == 'verts': inverts = True
        self.idx = np.array([r[0] for r in refs], np.int64); self.w = np.array([r[1] for r in refs]); self.off = np.array([r[2] for r in refs])
        V, VT, F, FT = _obj(os.path.join(self.folder, obj))
        assert len(V) == len(refs), (folder, len(V), len(refs))
        # sommets « UV-uniques » pour l'export (un sommet par couple position/UV)
        key = F.astype(np.int64) * 1000000 + FT
        uniq, inv = np.unique(key.ravel(), return_inverse=True)
        self.vid = uniq // 1000000; self.UV = VT[uniq % 1000000]; self.F = inv.reshape(-1, 3)
        self.mat = _mhmat(os.path.join(self.folder, mat)) if mat else {}

    def fit(self, base, sk=None, J=None, W=None, push=0.0):
        """base : positions (m) de TOUS les sommets du maillage de base déformé (avec les aides). Renvoie un Mesh pondéré."""
        P = (base[self.idx] * self.w[..., None]).sum(1)
        s = np.ones(3)
        for k, ax in (('x', 0), ('y', 1), ('z', 2)):
            if k in self.scale:
                i, j, ref = self.scale[k]; s[ax] = abs(base[i, ax] - base[j, ax]) / ref
        P = P + self.off * s[None]
        m = Mesh(P[self.vid], self.F.copy(), self.UV.copy())
        if J is not None:
            # poids : mélange des poids des trois sommets de référence
            nb = int(J.max()) + 1
            acc = np.zeros((len(self.idx), nb))
            for c in range(3):
                for k in range(J.shape[1]):
                    np.add.at(acc, (np.arange(len(self.idx)), J[self.idx[:, c], k]), np.abs(self.w[:, c]) * W[self.idx[:, c], k])
            order = np.argsort(-acc, 1)[:, :4]; ww = np.take_along_axis(acc, order, 1); ww /= np.maximum(ww.sum(1, keepdims=True), 1e-9)
            m.J = order[self.vid].astype(np.int64); m.W = ww[self.vid]
        return m

    def tex(self, key='diffuseTexture'):
        f = self.mat.get(key)
        if f and os.path.exists(os.path.join(self.folder, os.path.basename(f))): return os.path.join(self.folder, os.path.basename(f))
        if key == 'normalmapTexture':   # carte de normales présente mais non déclarée
            c = [x for x in os.listdir(self.folder) if x.endswith('.png') and ('normal' in x or '_nrm' in x)]
            if c: return os.path.join(self.folder, c[0])
        return None


def _load(fn, S, mode):
    im = Image.open(fn).convert(mode)
    if S and im.size != (S, S): im = im.resize((S, S), Image.LANCZOS)
    return np.asarray(im, float) / 255


def texset(proxy_or_file, S=None, tint=None, smooth=0.35, alpha=True, normal_key='normalmapTexture', normal_strength=1.0, darken=1.0):
    """TexSet depuis une texture MakeHuman. tint : couleur cible (recoloration à luminance conservée, ex. cheveux noirs)."""
    if isinstance(proxy_or_file, Proxy):
        dif = proxy_or_file.tex('diffuseTexture'); nrm = proxy_or_file.tex(normal_key)
    else:
        dif, nrm = proxy_or_file, None
    rgba = _load(dif, S, 'RGBA'); S = rgba.shape[0]
    rgb = rgba[..., :3] * darken
    if tint is not None:
        lum = (rgb * np.array([0.3, 0.59, 0.11])).sum(-1, keepdims=True)
        ref = np.median(lum[rgba[..., 3] > 0.5]) if (rgba[..., 3] > 0.5).any() else 0.5
        rgb = np.clip(np.array(tint)[None, None] * (lum / max(ref, 1e-3)) ** 0.9, 0, 1)
    if nrm:
        n = _load(nrm, S, 'RGB')
        if normal_strength != 1.0:
            v = n * 2 - 1; v[..., :2] *= normal_strength; v /= np.linalg.norm(v, axis=-1, keepdims=True); n = v * 0.5 + 0.5
    else:
        lum = (rgb * np.array([0.3, 0.59, 0.11])).sum(-1)
        n = height_to_normal(lum * 2.0, 0.6)
    a = rgba[..., 3] if alpha else None
    sm = np.full((S, S), smooth)
    return TexSet(np.clip(rgb, 0, 1), n, np.zeros((S, S)), sm, np.ones((S, S)), alpha=a)


def eye_texset(color, S=1024, src='brownlight', glow=None):
    """Yeux MakeHuman recolorés : l'iris (2 plus grandes zones saturées de la texture) prend la teinte demandée,
    luminance et motif conservés ; pupille et sclérotique intactes. glow : émission de l'iris (yeux luisants)."""
    from scipy.ndimage import label, binary_fill_holes, gaussian_filter
    rgb = _load(path('eyes', 'materials', src + '_eye.png'), S, 'RGB')
    mx, mn = rgb.max(-1), rgb.min(-1); sat = (mx - mn) / np.maximum(mx, 1e-3)
    m = sat > 0.28
    lab, n = label(m)
    if n:
        sizes = np.bincount(lab.ravel())[1:]; keep = np.argsort(-sizes)[:2] + 1
        m = binary_fill_holes(np.isin(lab, keep))
    a = gaussian_filter(m.astype(float), S / 700)[..., None]
    lum = (rgb * np.array([0.3, 0.59, 0.11])).sum(-1, keepdims=True)
    ref = np.median(lum[m]) if m.any() else 0.4
    iris = np.clip(np.array(color)[None, None] * (lum / ref) ** 1.1, 0, 1)
    lum3 = np.repeat(lum, 3, -1)
    sclera = rgb * 0.45 + lum3 * 0.55 * np.array([1.04, 1.0, 0.97])          # sclérotique moins rosée (coins de l'œil)
    base = sclera * (1 - a) + iris * a
    emit = np.clip(iris * a * np.array(glow)[None, None], 0, 1) if glow is not None else None
    z = np.zeros((S, S))
    return TexSet(np.clip(base, 0, 1), np.dstack([z + 0.5, z + 0.5, z + 1]), z, z + 0.85, z + 1, emit)
