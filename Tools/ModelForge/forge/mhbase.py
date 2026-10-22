"""Corps humain de base MakeHuman (CC0) adapté au squelette de Corvalune.

1. Charge le maillage de base (quads, UV) et applique des morphes (homme / femme, musculature, poitrine…).
2. Calcule les articulations MakeHuman (centres des cubes « joint-* »).
3. Reporte le maillage sur le squelette Corvalune (pose T) : chaque os Corvalune reçoit la transformation qui amène
   son segment MakeHuman (tête → queue) sur son propre segment, avec contrôle de la torsion (paume vers le bas) ;
   les sommets suivent par skinning linéaire avec les poids MakeHuman regroupés par os Corvalune.
4. Renvoie des maillages pondérés prêts à l'export (corps, yeux) et les repères du visage (yeux, bouche…).
"""
import os, json
import numpy as np
from .geo import Mesh, unit
from . import humanoid as HUM

D = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'third_party', 'makehuman')
_cache = {}


def _load_obj():
    if 'obj' in _cache: return _cache['obj']
    V, VT, faces = [], [], []
    g = None
    for l in open(os.path.join(D, 'base.obj')):
        if l.startswith('v '): V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith('vt '): VT.append([float(x) for x in l.split()[1:3]])
        elif l.startswith('g '): g = l.split()[1]
        elif l.startswith('f '):
            c = [p.split('/') for p in l.split()[1:]]
            faces.append(([int(a[0]) - 1 for a in c], [int(a[1]) - 1 for a in c], g))
    _cache['obj'] = (np.array(V) * 0.1, np.array(VT), faces)
    return _cache['obj']


def _target(name):
    key = 't:' + name
    if key not in _cache:
        idx, d = [], []
        for l in open(os.path.join(D, 'targets', name + '.target')):
            if l[0] == '#' or not l.strip(): continue
            p = l.split(); idx.append(int(p[0])); d.append([float(x) for x in p[1:4]])
        _cache[key] = (np.array(idx, dtype=np.int64), np.array(d, dtype=float).reshape(-1, 3) * 0.1)
    return _cache[key]


def _skel():
    if 'skel' not in _cache:
        s = json.load(open(os.path.join(D, 'rigs', 'default.mhskel')))
        w = json.load(open(os.path.join(D, 'rigs', 'default_weights.mhw')))['weights']
        _cache['skel'] = (s, w)
    return _cache['skel']


MALE = [('universal-male-young-averagemuscle-averageweight', 1.0), ('caucasian-male-young', 1.0)]
FEMALE = [('universal-female-young-averagemuscle-averageweight', 1.0), ('caucasian-female-young', 1.0)]


class MHBody:
    def __init__(self, targets=MALE, extra=()):
        V0, self.VT, self.faces = _load_obj()
        V = V0.copy()
        for n, w in list(targets) + list(extra):
            i, d = _target(n); V[i] += w * d
        self.V = V
        s, self.weights = _skel()
        self.joint_idx = s['joints']; self.bones = s['bones']

    def J(self, name):
        return self.V[self.joint_idx[name]].mean(0)

    def head(self, b): return self.J(b + '____head')
    def tail(self, b): return self.J(b + '____tail')


# ---------------------------------------------------------------------------------------------- correspondances
def _mh_to_ours(b):
    """Os MakeHuman → os Corvalune (regroupement des poids)."""
    side = 'Left' if b.endswith('.L') else 'Right' if b.endswith('.R') else ''
    n = b[:-2] if side else b
    table = {'root': 'Hips', 'spine05': 'Hips', 'pelvis': 'Hips', 'spine04': 'Spine', 'spine03': 'Spine', 'spine02': 'Chest', 'breast': 'Chest',
             'spine01': 'UpperChest', 'neck01': 'Neck', 'neck02': 'Neck', 'neck03': 'Neck', 'clavicle': 'Shoulder', 'shoulder01': 'Shoulder',
             'upperarm01': 'UpperArm', 'upperarm02': 'UpperArm', 'lowerarm01': 'LowerArm', 'lowerarm02': 'LowerArm', 'wrist': 'Hand',
             'upperleg01': 'UpperLeg', 'upperleg02': 'UpperLeg', 'lowerleg01': 'LowerLeg', 'lowerleg02': 'LowerLeg', 'foot': 'Foot'}
    if n in table:
        t = table[n]
        return (side + t) if t in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand', 'UpperLeg', 'LowerLeg', 'Foot') else t
    if n.startswith('metacarpal'): return side + 'Hand'
    if n.startswith('finger'):
        f, k = n[6:].split('-'); fn = ['Thumb', 'Index', 'Middle', 'Ring', 'Little'][int(f) - 1]
        return side + fn + ['Proximal', 'Intermediate', 'Distal'][int(k) - 1]
    if n.startswith('toe'): return side + 'Toes'
    return 'Head'                                                     # visage, mâchoire, yeux, langue…


def _frame(d, s):
    """Repère orthonormé : axe principal d, axe secondaire s (projeté)."""
    d = unit(np.asarray(d, float)); s = np.asarray(s, float) - d * np.dot(s, d); s = unit(s)
    return np.stack([d, s, np.cross(d, s)], 1)


def retarget(mh, sk, thickness=1.0, head_scale=1.0, hand_scale=1.0):
    """Transformations par os Corvalune (matrices 4x4) qui amènent le corps MakeHuman sur le squelette Corvalune."""
    P = {n: np.array(p) for n, p in zip(sk.names, sk.pos)}
    T = {}
    def seg(b, mh_h, mh_t, my_h, my_t, sec_mh=None, sec_my=None, uniform=None, radial=None):
        dm = mh_t - mh_h; dy = my_t - my_h
        Lm, Ly = np.linalg.norm(dm), np.linalg.norm(dy)
        if sec_mh is None:
            ax = np.cross(unit(dm), unit(dy)); s = np.linalg.norm(ax); c = np.dot(unit(dm), unit(dy))
            if s < 1e-8: R = np.eye(3)
            else:
                k = ax / s; K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
                R = np.eye(3) + K * s + K @ K * (1 - c)
        else:
            R = _frame(dy, sec_my) @ _frame(dm, sec_mh).T
        if uniform is not None:
            S = np.eye(3) * uniform
        else:
            r = thickness if radial is None else radial
            u = unit(dm); S = np.eye(3) * r + np.outer(u, u) * (Ly / Lm - r)
        M = np.eye(4); M[:3, :3] = R @ S; M[:3, 3] = my_h - R @ S @ mh_h
        T[b] = M
    H_ = mh.head; T_ = mh.tail
    seg('Hips', H_('spine05'), H_('spine04'), P['Hips'], P['Spine'])
    seg('Spine', H_('spine04'), H_('spine02'), P['Spine'], P['Chest'])
    seg('Chest', H_('spine02'), H_('spine01'), P['Chest'], P['UpperChest'])
    seg('UpperChest', H_('spine01'), H_('neck01'), P['UpperChest'], P['Neck'])
    # tête : échelle uniforme, recalée sur le crâne du gabarit (sommet ≈ 1,835 m)
    top_mh = mh.V[:, 1].max(); chin_mh = T_('jaw')[1]
    s = (1.835 - 1.590) / (top_mh - chin_mh) * head_scale
    M = np.eye(4); M[:3, :3] *= s; M[:3, 3] = np.array([0, 1.835, 0.012]) - s * np.array([0, top_mh, H_('head')[2]])
    T['Head'] = M
    seg('Neck', H_('neck01'), H_('head'), P['Neck'], (M @ np.r_[H_('head'), 1])[:3], radial=thickness * 0.95)
    for side, sx in (('Left', 1), ('Right', -1)):
        l = '.L' if sx > 0 else '.R'
        pdown = np.array([0, -1.0, 0])
        # paume MakeHuman : normale de la main (de l'index vers l'auriculaire × direction de la main)
        wrist_mh = H_('wrist' + l); mid_mh = H_('finger3-1' + l)
        across_mh = H_('finger5-1' + l) - H_('finger2-1' + l)
        palm_mh = np.cross(across_mh, mid_mh - wrist_mh) * sx
        seg(side + 'Shoulder', H_('clavicle' + l), H_('upperarm01' + l), P[side + 'Shoulder'], P[side + 'UpperArm'])
        seg(side + 'UpperArm', H_('upperarm01' + l), H_('lowerarm01' + l), P[side + 'UpperArm'], P[side + 'LowerArm'])
        seg(side + 'LowerArm', H_('lowerarm01' + l), H_('wrist' + l), P[side + 'LowerArm'], P[side + 'Hand'], sec_mh=palm_mh, sec_my=pdown)
        seg(side + 'Hand', wrist_mh, mid_mh, P[side + 'Hand'], P[side + 'MiddleProximal'], sec_mh=palm_mh, sec_my=pdown, uniform=None, radial=hand_scale)
        for fi, fn in enumerate(['Thumb', 'Index', 'Middle', 'Ring', 'Little']):
            tips = [np.array(p) for p in HUM.FINGERS[fn]]
            if sx < 0: tips = [p * np.array([-1, 1, 1]) for p in tips]
            for k, sg in enumerate(['Proximal', 'Intermediate', 'Distal']):
                b = f'finger{fi + 1}-{k + 1}{l}'
                seg(side + fn + sg, H_(b), T_(b), tips[k], tips[k + 1], sec_mh=palm_mh, sec_my=pdown, radial=hand_scale)
        seg(side + 'UpperLeg', H_('upperleg01' + l), H_('lowerleg01' + l), P[side + 'UpperLeg'], P[side + 'LowerLeg'])
        seg(side + 'LowerLeg', H_('lowerleg01' + l), H_('foot' + l), P[side + 'LowerLeg'], P[side + 'Foot'])
        toe_mh = H_('toe3-1' + l)
        seg(side + 'Foot', H_('foot' + l), toe_mh, P[side + 'Foot'], P[side + 'Toes'], sec_mh=np.array([0, 1.0, 0]), sec_my=np.array([0, 1.0, 0]), radial=thickness)
        seg(side + 'Toes', toe_mh, T_('toe3-3' + l) if ('toe3-3' + l + '____tail') in mh.joint_idx else T_('toe3-1' + l),
            P[side + 'Toes'], P[side + 'Toes'] + np.array([0, -0.004, 0.075]), sec_mh=np.array([0, 1.0, 0]), sec_my=np.array([0, 1.0, 0]), radial=thickness)
    return T


def build(sk, targets=MALE, extra=(), thickness=1.04, head_scale=1.0, groups=('body',)):
    """Corps MakeHuman reporté sur le squelette sk. Renvoie (corps, yeux, repères)."""
    mh = MHBody(targets, extra)
    T = retarget(mh, sk, thickness, head_scale)
    names = sk.names
    nV = len(mh.V)
    Wm = {}
    for b, lst in mh.weights.items():
        ob = _mh_to_ours(b)
        if ob not in names: ob = 'Head'
        a = Wm.setdefault(ob, np.zeros(nV))
        for i, w in lst: a[i] += w
    bones = list(Wm.keys()); Wa = np.stack([Wm[b] for b in bones], 1)
    tot = Wa.sum(1); Wa[tot < 1e-6, bones.index('Hips')] = 1.0; Wa /= Wa.sum(1, keepdims=True)
    # position reportée = combinaison des transformations par os
    Vh = np.c_[mh.V, np.ones(nV)]
    Vn = np.zeros((nV, 3))
    for k, b in enumerate(bones):
        Vn += Wa[:, k:k + 1] * (Vh @ T[b].T)[:, :3]
    order = np.argsort(-Wa, 1)[:, :4]
    J = np.array([names.index(b) for b in bones])[order]
    W = np.take_along_axis(Wa, order, 1); W /= W.sum(1, keepdims=True)

    def make(group_filter):
        Fv, Ft = [], []
        for vi, ti, g in mh.faces:
            if not group_filter(g): continue
            if len(vi) == 4: Fv += [(vi[0], vi[1], vi[2]), (vi[0], vi[2], vi[3])]; Ft += [(ti[0], ti[1], ti[2]), (ti[0], ti[2], ti[3])]
            else: Fv.append(tuple(vi)); Ft.append(tuple(ti))
        Fv = np.array(Fv); Ft = np.array(Ft)
        key = Fv.astype(np.int64) * 100000 + Ft
        uniq, inv = np.unique(key.ravel(), return_inverse=True)
        vid = uniq // 100000; tid = uniq % 100000
        m = Mesh(Vn[vid], inv.reshape(-1, 3), mh.VT[tid], J[vid], W[vid])
        m.src = vid
        return m
    body = make(lambda g: g in groups)
    eyes = make(lambda g: g in ('helper-l-eye', 'helper-r-eye'))
    lashes = make(lambda g: g is not None and 'eyelashes' in g)
    # repères du visage (dans l'espace final)
    def tr(p, b='Head'): return (T[b] @ np.r_[p, 1])[:3]
    marks = {'eye_L': tr(mh.head('eye.L')), 'eye_R': tr(mh.head('eye.R')), 'eye_L_dir': unit(tr(mh.tail('eye.L')) - tr(mh.head('eye.L'))),
             'mouth': tr((mh.head('oris01') + mh.head('oris05')) / 2) if 'oris05____head' in mh.joint_idx else tr(mh.tail('jaw')) + np.array([0, 0.05, 0]),
             'jaw': tr(mh.tail('jaw')), 'top': tr(np.array([0, mh.V[:, 1].max(), mh.head('head')[2]])),
             'head_scale': float(np.cbrt(np.linalg.det(T['Head'][:3, :3]))), 'T_head': T['Head'], 'mh_eye_L': mh.head('eye.L'), 'mh_eye_R': mh.head('eye.R')}
    for k in ('oris01', 'oris05', 'oris06', 'oris03.L', 'oris04.L'):
        if k + '____head' in mh.joint_idx: marks[k] = tr(mh.head(k))
    marks['lashes'] = lashes
    marks['base_V'], marks['base_J'], marks['base_W'] = Vn, J, W          # tous les sommets (aides comprises) : ajustement des proxys
    return body, eyes, marks
