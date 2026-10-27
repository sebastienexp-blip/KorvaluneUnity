"""Pièces construites à partir du corps MakeHuman reporté : découpe (tête et cou, mains…), globes oculaires,
coques épousant le corps (vêtements ajustés, calotte de cheveux), mèches de cheveux longs, barbe."""
import numpy as np
from .geo import Mesh, Loft, spline, curve, concat, unit, vertex_normals, grid_faces, smoothstep


def submesh(m, face_mask):
    F = m.F[face_mask]
    used = np.unique(F); remap = -np.ones(len(m.V), np.int64); remap[used] = np.arange(len(used))
    s = Mesh(m.V[used], remap[F], m.UV[used], m.J[used], m.W[used])
    s.N = None if m.N is None else m.N[used]
    return s


def region(m, vert_mask, all_in=True):
    """Facettes dont tous (ou au moins un) sommets satisfont le masque."""
    vm = vert_mask[m.F]
    return vm.all(1) if all_in else vm.any(1)


def eyeballs(marks, r=0.0118, segs=24, rings=18):
    out = []
    for k in ('eye_L', 'eye_R'):
        c = np.asarray(marks[k]); d = unit(np.asarray(marks.get('eye_L_dir', (0, 0, 1))) * np.array([1 if k == 'eye_L' else -1, 1, 1]))
        d = unit(d * np.array([0.25, 0.6, 1.0]))
        t1 = unit(np.cross([0, 1, 0], d)); t2 = np.cross(d, t1)
        th = np.linspace(0, np.pi, rings); ph = np.linspace(0, 2 * np.pi, segs + 1)
        TH, PH = np.meshgrid(th, ph, indexing='ij')
        loc = np.stack([np.sin(TH) * np.cos(PH), np.sin(TH) * np.sin(PH), np.cos(TH)], -1)
        V = c + (loc[..., 0:1] * t1 + loc[..., 1:2] * t2 + loc[..., 2:3] * d) * r
        UV = np.stack([0.5 + 0.5 * loc[..., 0], 0.5 + 0.5 * loc[..., 1]], -1)
        m = Mesh(V.reshape(-1, 3), grid_faces(rings, segs + 1), UV.reshape(-1, 2))
        if (vertex_normals(m) * (m.V - c)).sum() < 0: m.flip()
        out.append(m)
    return concat(out)


def shell(body, face_mask, offset=0.004, thick=0.003, uv_scale=1.0, rim=True, smooth=0, inflate=0.0, restore=1.0):
    """Coque qui épouse une région du corps : face extérieure décalée le long des normales, face intérieure, chant."""
    s = submesh(body, face_mask)
    N = vertex_normals(s)
    if smooth:   # lissage laplacien : efface orteils, nombril… (vêtement ample, botte) en gardant les bords
        V = s.V.copy()
        E = np.concatenate([s.F[:, [0, 1]], s.F[:, [1, 2]], s.F[:, [2, 0]]])
        key = np.round(V * 2e4).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.reshape(-1)
        Es = np.sort(inv[E], 1); uq, cnt = np.unique(Es, axis=0, return_counts=True)
        border = np.zeros(inv.max() + 1, bool); border[np.unique(uq[cnt == 1])] = True
        nU = inv.max() + 1
        P = np.zeros((nU, 3)); np.add.at(P, inv, V); cntv = np.bincount(inv, minlength=nU)[:, None]; P /= cntv
        import scipy.sparse as sp
        A = sp.coo_matrix((np.ones(len(Es) * 2), (np.r_[Es[:, 0], Es[:, 1]], np.r_[Es[:, 1], Es[:, 0]])), shape=(nU, nU)).tocsr()
        A.data[:] = 1.0
        deg = np.asarray(A.sum(1)).ravel(); Lm = sp.diags(1 / np.maximum(deg, 1)) @ A
        free = ~border[:, None]
        for it in range(smooth):                       # Taubin : lisse sans rétrécir
            for lam in (0.5, -0.53):
                P = np.where(free, P + lam * (Lm @ P - P), P)
        Vs = P[inv]
        # le lissage rétrécit : on regonfle pour rester au-dessus de la peau
        d = ((V - Vs) * N).sum(1)
        s.V = Vs + N * np.clip(d, 0, restore)[:, None] + N * inflate
        N = vertex_normals(s)
    if isinstance(offset, (int, float)): off = np.full(len(s.V), offset)
    else: off = offset(s.V) if callable(offset) else offset
    outer = Mesh(s.V + N * (off + thick)[:, None], s.F.copy(), s.UV * uv_scale, s.J.copy(), s.W.copy())
    inner = Mesh(s.V + N * off[:, None], s.F[:, ::-1].copy(), s.UV * uv_scale, s.J.copy(), s.W.copy())
    parts = [outer, inner]
    if rim:   # bords libres : bande qui ferme l'épaisseur
        E = np.sort(np.concatenate([s.F[:, [0, 1]], s.F[:, [1, 2]], s.F[:, [2, 0]]]), 1)
        u, c = np.unique(E, axis=0, return_counts=True)
        b = u[c == 1]
        if len(b):
            nV = len(s.V)
            V = np.vstack([outer.V, inner.V]); UVr = np.vstack([outer.UV, inner.UV])
            F = np.concatenate([np.stack([b[:, 0], b[:, 1], b[:, 1] + nV], 1), np.stack([b[:, 0], b[:, 1] + nV, b[:, 0] + nV], 1)])
            rimm = Mesh(V, F, UVr, np.vstack([s.J, s.J]), np.vstack([s.W, s.W]))
            # orientation : vers l'extérieur du bord
            fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
            ctr = s.V.mean(0)
            flip = (fn * (V[F].mean(1) - ctr)).sum(1) < 0
            rimm.F[flip] = rimm.F[flip][:, ::-1]
            parts.append(rimm)
    return concat(parts)


def scalp_mask(body, marks, front_rise=0.045, side_drop=0.0, back_drop=0.06):
    """Sommets du cuir chevelu : au-dessus d'une ligne de cheveux qui remonte sur le front et descend dans la nuque."""
    V = body.V; eL, eR = marks['eye_L'], marks['eye_R']
    ey = (eL[1] + eR[1]) / 2; ez = (eL[2] + eR[2]) / 2
    ang = np.arctan2(V[:, 0], V[:, 2] - 0.0)                                  # 0 devant, ±π derrière
    fr = np.clip(np.cos(ang), 0, 1) ** 1.5
    line = ey + front_rise * fr + side_drop * (1 - fr) - back_drop * np.clip(-np.cos(ang), 0, 1)
    ear = (np.abs(V[:, 0]) > 0.060) & (V[:, 1] < ey + 0.02) & (V[:, 1] > ey - 0.06) & (np.abs(V[:, 2] - (ez - 0.09)) < 0.04)
    return (V[:, 1] > line) & (V[:, 1] > 1.54) & ~ear


def long_hair(body, marks, sk, n=140, length=0.55, width=0.035, seed=3, part=True, color_mat='hair', volume=0.012, front_locks=True):
    """Mèches longues : rubans épais groupés, partent du cuir chevelu, contournent la tête et retombent dans le dos et sur les épaules."""
    rng = np.random.default_rng(seed)
    top = np.asarray(marks['top']); eL, eR = marks['eye_L'], marks['eye_R']
    hc = np.array([0, (eL[1] + top[1]) / 2 - 0.01, (eL[2] + top[2]) / 2 - 0.045])          # centre approximatif du crâne
    out = []
    for k in range(n):
        ang = rng.uniform(-np.pi, np.pi)                                           # autour de la tête (0 = devant)
        if abs(ang) < 0.75 and not (front_locks and abs(ang) > 0.45): continue     # dégage le visage
        el = rng.uniform(0.25, 1.15)                                                # élévation de la racine
        rad = np.array([0.086, 0.104, 0.104])
        d0 = np.array([np.sin(ang) * np.cos(el), np.sin(el), np.cos(ang) * np.cos(el)])
        root = hc + d0 * rad
        side = np.sign(np.sin(ang)) if abs(np.sin(ang)) > 0.05 else rng.choice([-1, 1])
        outw = unit(np.array([np.sin(ang), 0, np.cos(ang)]))
        L = length * rng.uniform(0.75, 1.05)
        # contourne la tête (rayon croissant) puis tombe ; les mèches de devant passent devant les épaules
        front = np.cos(ang) > 0.0
        pts = [root - d0 * 0.004]
        for s in np.linspace(0.08, 1.0, 9):
            y = root[1] - s * L
            r_out = 0.105 + 0.06 * min(1, s * 3) + 0.03 * s
            wob = rng.normal(0, 0.006, 3) * s
            p = np.array([0.0, y, 0.0]) + outw * r_out * (1 if root[1] - s * L > 1.45 else 0.75) + np.array([0, 0, hc[2]])
            if root[1] - s * L < 1.50:   # sur les épaules / le dos
                p[0] = np.sign(p[0]) * max(abs(p[0]), 0.12 if front else 0.0) * (1.0 if front else 0.8)
                p[2] = (0.10 if front else -0.16) + 0.03 * np.cos(ang)
            pts.append(p + wob)
        pth = spline(pts, 18)
        w = width * rng.uniform(0.7, 1.2)
        lo = Loft(pth, curve([(0, 0.004), (.3, 0.006), (1, 0.002)], 18), curve([(0, w * 0.6), (.15, w), (.8, w * 0.8), (1, w * 0.15)], 18), p=2.0, segs=6,
                  ref=tuple(outw))
        m = lo.mesh(1.0)
        m.UV = np.c_[np.tile(np.linspace(0, 1, 7), 18)[:len(m.V)] * 0.25 + rng.uniform(0, 0.75), np.repeat(np.linspace(0, 1, 18), 7)[:len(m.V)] * 3]
        y = m.V[:, 1]; f = smoothstep((1.60 - y) / 0.12)
        m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4))
        m.J[:, 0] = sk['Head']; m.W[:, 0] = 1 - f; m.J[:, 1] = sk['UpperChest']; m.W[:, 1] = f
        out.append(m)
    return concat(out)


def beard(body, marks, sk, n=1400, length=0.016, seed=5, moustache=True):
    """Barbe courte et dense : petites mèches posées sur la mâchoire (sous la bouche, joues basses)."""
    rng = np.random.default_rng(seed)
    V = body.V; N = vertex_normals(body)
    mo = marks['mouth']; jaw = marks['jaw']
    mask = (V[:, 1] < mo[1] + 0.012) & (V[:, 1] > jaw[1] - 0.035) & (V[:, 2] > -0.005) & (np.abs(V[:, 0]) < 0.075) & (V[:, 1] > 1.52)
    mask &= ~((np.abs(V[:, 0]) < 0.024) & (np.abs(V[:, 1] - mo[1]) < 0.009) & (V[:, 2] > mo[2] - 0.02))
    idx = np.where(mask)[0]
    out = []
    for k in range(n):
        i = idx[rng.integers(len(idx))]
        root = V[i] + N[i] * 0.0005; nrm = N[i]
        L = length * rng.uniform(0.6, 1.3) * (1.3 if V[i][1] < jaw[1] + 0.01 else 1.0)
        dn = np.array([0, -1.0, 0.1]); tan = unit(dn - nrm * np.dot(dn, nrm))
        tip = root + unit(tan + nrm * 0.25) * L
        lo = Loft(spline([root, (root + tip) / 2 + nrm * 0.0015, tip], 4), curve([(0, .0018), (1, .0008)], 4), curve([(0, .0060), (1, .0018)], 4), p=2.0, segs=4, ref=tuple(nrm))
        m = lo.mesh(1.0); m.UV = rng.uniform(0, 1, (len(m.V), 2))
        out.append(m)
    if moustache:
        for s in (1, -1):
            for k in range(60):
                root = np.array([s * rng.uniform(0.002, 0.020), mo[1] + 0.010 + rng.uniform(-0.002, 0.004), mo[2] + 0.004 - 0.25 * rng.uniform(0, 0.02)])
                tip = root + np.array([s * rng.uniform(0.006, 0.016), -rng.uniform(0.008, 0.014), -0.002])
                lo = Loft(spline([root, tip], 3), 0.0014, curve([(0, .0035), (1, .0008)], 3), p=2, segs=4, ref=(0, 0, 1))
                m = lo.mesh(1.0); m.UV = rng.uniform(0, 1, (len(m.V), 2)); out.append(m)
    b = concat(out)
    b.J = np.zeros((len(b.V), 4), np.int64); b.J[:, 0] = sk['Head']; b.W = np.zeros((len(b.V), 4)); b.W[:, 0] = 1
    return b


def mh_eyes(marks):
    """Globes oculaires haute définition MakeHuman (cornée, iris) recalés sur les centres des yeux."""
    import os
    p = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'third_party', 'makehuman', 'eyes', 'high-poly.obj')
    V, VT, F, FT = [], [], [], []
    for l in open(p):
        if l.startswith('v '): V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith('vt '): VT.append([float(x) for x in l.split()[1:3]])
        elif l.startswith('f '):
            c = [q.split('/') for q in l.split()[1:]]
            vi = [int(a[0]) - 1 for a in c]; ti = [int(a[1]) - 1 for a in c]
            for k in range(1, len(vi) - 1): F.append((vi[0], vi[k], vi[k + 1])); FT.append((ti[0], ti[k], ti[k + 1]))
    V = np.array(V) * 0.1; VT = np.array(VT); F = np.array(F); FT = np.array(FT)
    key = F * 100000 + FT; uniq, inv = np.unique(key.ravel(), return_inverse=True)
    vid, tid = uniq // 100000, uniq % 100000
    Vv = V[vid].copy(); UV = VT[tid]; Fm = inv.reshape(-1, 3)
    # retire la cornée (calque transparent dans MakeHuman : disque bleu de la texture) — l'iris reste visible
    corn = (UV[Fm][:, :, 0] > 0.86).all(1) & (UV[Fm][:, :, 1] < 0.14).all(1)
    Fm = Fm[~corn]
    s = marks['head_scale']
    for side, k in ((1, 'eye_L'), (-1, 'eye_R')):
        sel = Vv[:, 0] * side > 0
        lo, hi = Vv[sel].min(0), Vv[sel].max(0)
        ctr = np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2] + (hi[1] - lo[1]) / 2])     # centre du globe (pas de la cornée)
        Vv[sel] = np.asarray(marks[k]) + (Vv[sel] - ctr) * s
    m = Mesh(Vv, Fm, UV)
    if (vertex_normals(m) * (m.V - m.V.mean(0)) * np.array([0, 0, 1])).sum() < 0: pass
    return m


def bone_mask(body, sk, bones, min_w=0.35):
    """Sommets dont un os de la liste (préfixes sans côté : 'UpperArm' → Left/RightUpperArm) pèse au moins min_w."""
    names = np.array(sk.names)
    def match(n):
        base = n.replace('Left', '').replace('Right', '')
        return any(base.startswith(b) for b in bones)
    ok = np.array([match(n) for n in names])
    w = (body.W * ok[body.J]).sum(1)
    return w >= min_w


def surface_point(body, p, off=0.0, _cache={}):
    from scipy.spatial import cKDTree
    key = id(body)
    if key not in _cache: _cache[key] = (cKDTree(body.V), vertex_normals(body))
    tree, N = _cache[key]
    i = tree.query(np.asarray(p, float))[1]
    return body.V[i] + N[i] * off, N[i], i


def surface_path(body, pts, off, n=30):
    P = spline(pts, n)
    out = []; nrm = []
    for p in P:
        q, N_, _ = surface_point(body, p, off); out.append(q); nrm.append(N_)
    return np.array(out), np.array(nrm)


def strap(body, pts, off, width, thick=0.003, n=30):
    """Sangle (cuir) qui suit la surface du corps."""
    P, N = surface_path(body, pts, off, n)
    m = Loft(spline(P, n), thick, width / 2, p=4.0, segs=8, ref=tuple(N.mean(0)))
    mm = m.mesh(0.1)
    from scipy.spatial import cKDTree
    i = cKDTree(body.V).query(mm.V)[1]
    mm.J, mm.W = body.J[i].copy(), body.W[i].copy()
    return mm


def weights_from(body, m):
    from scipy.spatial import cKDTree
    i = cKDTree(body.V).query(m.V)[1]
    m.J, m.W = body.J[i].copy(), body.W[i].copy()
    return m


def boot_last(body, sk, side='Left', margin=0.009, sole=0.012, toe_extra=0.012):
    """Pied de botte (forme de cordonnier) ajusté aux dimensions du pied : talon arrondi, cambrure, bout rond, semelle plate."""
    from scipy.ndimage import uniform_filter1d
    sx = 1 if side == 'Left' else -1
    feet = bone_mask(body, sk, ['Foot', 'Toes'], 0.3) & (body.V[:, 0] * sx > 0)
    P = body.V[feet]
    z0, z1 = P[:, 2].min() + 0.010, P[:, 2].max() + toe_extra - 0.012
    n = 26
    zs = np.linspace(z0, z1, n)
    top, hw, cx = np.zeros(n), np.zeros(n), np.zeros(n)
    for i, z in enumerate(zs):
        sel = np.abs(P[:, 2] - z) < (z1 - z0) / n * 1.5
        Q = P[sel] if sel.sum() > 3 else P[np.argsort(np.abs(P[:, 2] - z))[:8]]
        top[i] = Q[:, 1].max(); hw[i] = (Q[:, 0].max() - Q[:, 0].min()) / 2; cx[i] = (Q[:, 0].max() + Q[:, 0].min()) / 2
    hw = uniform_filter1d(hw, 7); cx = uniform_filter1d(cx, 7)
    t = np.linspace(0, 1, n)
    hw = hw + margin
    # profil de hauteur d'une forme de cordonnier (indépendant des sommets de cheville du maillage)
    tl = 0.16
    tip = np.clip((t - (1 - tl)) / tl, 0, 1); heel = smoothstep((0.12 - t) / 0.12)
    hw = hw * (0.34 + 0.66 * np.sqrt(np.clip(1 - tip ** 2, 0, 1))) * (1 - 0.18 * heel)
    hw = np.maximum(hw, 0.014)
    prof_t = [0.0, 0.10, 0.32, 0.50, 0.70, 0.86, 1.0]
    prof_y = [0.105, 0.118, 0.112, 0.082, 0.062, 0.054, 0.046]
    top = np.interp(t, prof_t, prof_y) * (1 + margin)
    sole_h = sole
    bot = np.full(n, sole_h)
    ymid = (top + bot) / 2
    path = np.stack([cx, ymid, zs], 1)
    rf = (top - bot) / 2
    lo = Loft(path, rf, hw, p=2.4, segs=40, ref=(0, 1, 0), cap0=3, cap1=4)
    m = lo.mesh(0.12)
    m.V[:, 1] = np.maximum(m.V[:, 1], sole_h * 0.6)
    # semelle débordante + talon (bloc plus épais à l'arrière)
    hs = hw + 0.003
    heelb = smoothstep((0.30 - t) / 0.06)                            # 1 sous le talon, 0 sous l'avant-pied
    st = sole_h * (1 + 0.9 * heelb)
    sp = np.stack([cx, st / 2, zs], 1)
    so = Loft(sp, st / 2 + 0.0005, hs, p=4.0, segs=32, ref=(0, 1, 0), cap0=2, cap1=3)
    ms = so.mesh(0.12)
    ms.V[:, 1] = np.clip(ms.V[:, 1], 0.0, None)
    m = concat([m, ms])
    y = m.V[:, 1]; z = m.V[:, 2]
    toes_z = np.asarray(sk.pos[sk[side + 'Toes']])[2]
    f = smoothstep((z - toes_z + 0.02) / 0.04)
    m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4))
    m.J[:, 0] = sk[side + 'Foot']; m.W[:, 0] = 1 - f; m.J[:, 1] = sk[side + 'Toes']; m.W[:, 1] = f
    return m


def cloth_cut(body, coarse_mask, inside, offset=0.0025, thick=0.0016, subdiv=1, uv_scale=10.0, bump=None):
    """Vêtement ajusté à bord net : région grossière subdivisée, découpée par la fonction continue inside(P) > 0, décalée
    le long des normales (+ bump(P) éventuel), double face (extérieur + intérieur). Poids hérités du corps."""
    from .hairgen import _subdivide
    s = submesh(body, region(body, coarse_mask, all_in=False))
    N0 = vertex_normals(s)
    # sommets dédoublés sur les coutures UV du corps : même normale des deux côtés (sinon le décalage ouvre une fente)
    key = np.round(s.V / 1e-5).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.ravel()
    acc = np.zeros((inv.max() + 1, 3)); np.add.at(acc, inv, N0); acc /= np.linalg.norm(acc, axis=1, keepdims=True) + 1e-12
    N0 = acc[inv]
    V, F, N, J, W = _subdivide(s.V, s.F, N0, s.J, s.W, subdiv)
    V, N, J, W, F = _clip_faces(V, N, J, W, F, inside(V))
    off = offset + (bump(V) if bump is not None else 0.0)
    P = V + N * np.asarray(off).reshape(-1, 1)
    UV = np.c_[P[:, 0] * uv_scale + P[:, 2] * uv_scale * 0.5, P[:, 1] * uv_scale]
    outer = Mesh(P, F.copy(), UV, J.copy(), W.copy())
    inner = Mesh(P - N * thick, F[:, ::-1].copy(), UV.copy(), J.copy(), W.copy())
    return concat([outer, inner])


def _clip_faces(V, N, J, W, F, f):
    """Découpe exacte d'un maillage par la ligne de niveau f = 0 (garde f > 0) : les triangles coupés sont recoupés
    aux points de passage sur leurs arêtes → bord net sans subdivision supplémentaire."""
    pos = f > 0
    cnt = pos[F].sum(1)
    out_F = [F[cnt == 3]]
    nV = [V]; nN = [N]; nJ = [J]; nW = [W]; base = len(V); cache = {}
    def cross(a, b):
        k = (a, b) if a < b else (b, a)
        if k not in cache:
            t = f[a] / (f[a] - f[b]); t = min(max(t, 0.0), 1.0)
            nV.append((V[a] + (V[b] - V[a]) * t)[None]); nN.append(unit(N[a] + (N[b] - N[a]) * t)[None])
            src = a if t < 0.5 else b; nJ.append(J[src][None]); nW.append(W[src][None])
            cache[k] = base + len(cache)
        return cache[k]
    extra = []
    for tri in F[(cnt == 1) | (cnt == 2)]:
        p = pos[tri]
        for r in range(3):                       # rotation : sommet « isolé » en première position
            a, b, c = np.roll(tri, -r); pa, pb, pc = np.roll(p, -r)
            if pa != pb and pa != pc: break
        ab, ac = cross(a, b), cross(a, c)
        if pa: extra.append((a, ab, ac))                         # un seul sommet dedans
        else: extra += [(ab, b, c), (ab, c, ac)]                  # deux sommets dedans
    if extra: out_F.append(np.array(extra, np.int64))
    V = np.vstack(nV); N = np.vstack(nN); J = np.vstack(nJ); W = np.vstack(nW); F = np.vstack(out_F)
    used = np.unique(F); remap = -np.ones(len(V), np.int64); remap[used] = np.arange(len(used))
    return V[used], N[used], J[used], W[used], remap[F]
