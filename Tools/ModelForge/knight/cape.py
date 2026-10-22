"""Cape de laine rouge : drap à plis profonds attaché sous les épaulières, deux faces (extérieur / doublure),
pondérée sur trois chaînes d'os (gauche, centre, droite) pour suivre la simulation de tissu cuite dans les animations."""
import numpy as np
from forge.geo import Mesh, concat, rivet, rigid, smoothstep, unit, vertex_normals
from forge import humanoid as H

CLOAK = 'KN_Cloak'


def sheet(nu=40, nv=56, length=1.0, width=1.0, tatter=0.0, seed=2):
    u = np.linspace(0, 1, nu); v = np.linspace(0, 1, nv)              # v : 0 = col, 1 = ourlet
    U, Vv = np.meshgrid(u, v)
    s = 2 * U - 1
    top_y, bot_y = 1.475, 1.475 - (1.475 - 0.27) * length
    hw = 0.165 + 0.27 * width * Vv ** 0.85
    x = s * hw
    y = top_y - Vv * (top_y - bot_y)
    # col : s'enroule derrière la nuque, puis le drap s'écarte du dos
    z = -0.118 - 0.058 * (1 - s ** 2) ** 0.6 * np.exp(-Vv * 6) - 0.035 * (1 - s ** 2) ** 0.6 - 0.07 * Vv ** 0.7 - 0.05 * Vv * (s ** 2)
    # plis : creux et bosses qui s'ouvrent vers le bas, irréguliers
    A = 0.006 + 0.050 * Vv ** 1.1
    ph = 2 * np.pi * (4.5 * U) + 0.9 * np.sin(3.1 * U + 1.2) + 0.6 * Vv
    z += A * np.sin(ph) + 0.35 * A * np.sin(2.3 * ph + 0.7)
    x += 0.25 * A * np.cos(ph) * np.sign(s + 1e-9) * 0.5
    # les bords latéraux retombent un peu vers l'avant
    z += 0.05 * Vv * np.abs(s) ** 3
    # ourlet légèrement ondulé
    y += (Vv > 0.97) * 0.0 + 0.012 * np.sin(2 * np.pi * 7 * U) * Vv ** 8
    if tatter:   # ourlet déchiqueté : pointes irrégulières
        rr = np.random.default_rng(seed)
        jag = np.interp(U[0], np.linspace(0, 1, 23), rr.uniform(0, 1, 23)) ** 1.5
        y += tatter * (jag[None, :] * 0.18 - 0.02) * smoothstep((Vv - 0.75) / 0.25)
    P = np.stack([x, y, z], -1)
    idx = np.arange(nu * nv).reshape(nv, nu)
    a = idx[:-1, :-1].ravel(); b = idx[:-1, 1:].ravel(); c = idx[1:, :-1].ravel(); d = idx[1:, 1:].ravel()
    F = np.concatenate([np.stack([a, c, b], 1), np.stack([b, c, d], 1)])
    UV = np.stack([U.ravel(), 1 - Vv.ravel()], 1)
    m = Mesh(P.reshape(-1, 3), F, UV)
    # extérieur : normales vers l'arrière (-Z)
    n = vertex_normals(m, weld=False)
    if n[:, 2].mean() > 0: m.F = m.F[:, ::-1].copy()
    n = vertex_normals(m, weld=False)
    inner = Mesh(m.V + n * 0.005, m.F[:, ::-1].copy(), m.UV.copy())
    # chant de l'ourlet et des bords (épaisseur visible)
    return m, inner, U.ravel(), Vv.ravel()


def skin(m, sk, U):
    """Trois chaînes : poids horizontaux (gauche/centre/droite) × poids verticaux le long de chaque chaîne."""
    s = 2 * U - 1
    wh = {'L': np.clip(s, 0, 1), 'C': 1 - np.abs(s), 'R': np.clip(-s, 0, 1)}
    ys = [p[1] for p in H.CAPE['C']]                                   # 1.47, 1.17, 0.87, 0.56
    y = m.V[:, 1]
    nV = len(y); acc = {}
    def addw(bone, w):
        acc.setdefault(bone, np.zeros(nV)); acc[bone] += w
    for ch, w in wh.items():
        # le long de la chaîne : os k actif entre joint k et joint k+1, fondu de 0.07 m autour des joints
        prev = None
        for k in range(4):
            yk = ys[k]
            f_in = smoothstep((yk + 0.035 - y) / 0.07)                   # 1 sous le joint k
            nxt = smoothstep((ys[k + 1] + 0.035 - y) / 0.07) if k < 3 else np.zeros(nV)
            wk = np.clip(f_in - nxt, 0, 1)
            addw(sk[f'Cape_{ch}{k + 1}'], w * wk)
        addw(sk['UpperChest'], w * (1 - smoothstep((ys[0] + 0.035 - y) / 0.07)))
    bones = np.array(list(acc.keys())); Wm = np.stack([acc[b] for b in bones], 1)
    order = np.argsort(-Wm, axis=1)[:, :4]
    J = bones[order]; W = np.take_along_axis(Wm, order, 1)
    W /= W.sum(1, keepdims=True) + 1e-12
    m.J, m.W = J.astype(np.int64), W
    return m


def build(sk, asset, mat=CLOAK, clasp='KN_Brass', **kw):
    outer, inner, U, V = sheet(**kw)
    skin(outer, sk, U); skin(inner, sk, U)
    asset.add('cape_dehors', outer, mat); asset.add('cape_doublure', inner, mat)
    # fermoirs : deux disques de laiton sur le haut des épaules + chaînette
    for sx in (1, -1):
        P = np.array([0.150 * sx, 1.468, -0.045]); N = unit(np.array([0.25 * sx, 1.0, 0.35]))
        m = rivet(P, N, r=0.016, h=0.006, segs=16, rings=5); rigid(m, sk['UpperChest']); asset.add(f'cape_fermoir{sx}', m, clasp)
    return asset


def tabard_panel(sk, front=True, nu=14, nv=30, length=1.0, width=1.0, tatter=0.0, seed=5):
    """Pan de tabard rouge qui tombe sous les faulds (devant et derrière), entre les tassettes."""
    u = np.linspace(0, 1, nu); v = np.linspace(0, 1, nv)
    U, Vv = np.meshgrid(u, v); s = 2 * U - 1
    y = 1.00 - Vv * (1.00 - 0.50) * length
    hw = (0.085 + 0.025 * Vv) * width
    x = s * hw
    zf = 0.150 + 0.045 * np.clip((1.0 - y) / 0.3, 0, 1) - 0.05 * np.clip((0.70 - y) / 0.2, 0, 1)
    z = (zf + 0.012 * np.sin(2 * np.pi * 2 * U) * Vv) * (1 if front else -1.05)
    z += -0.012 * s ** 2
    if tatter:   # bas déchiqueté
        rr = np.random.default_rng(seed + (0 if front else 7))
        jag = np.interp(U[0], np.linspace(0, 1, 9), rr.uniform(0, 1, 9)) ** 1.3
        y += tatter * jag[None, :] * 0.12 * smoothstep((Vv - 0.6) / 0.4)
    P = np.stack([x, y, z], -1)
    idx = np.arange(nu * nv).reshape(nv, nu)
    a = idx[:-1, :-1].ravel(); b = idx[:-1, 1:].ravel(); c = idx[1:, :-1].ravel(); d = idx[1:, 1:].ravel()
    F = np.concatenate([np.stack([a, c, b], 1), np.stack([b, c, d], 1)])
    m = Mesh(P.reshape(-1, 3), F, np.stack([0.30 + 0.4 * U.ravel(), 0.32 * (1 - Vv.ravel())], 1))
    n = vertex_normals(m, weld=False)
    if (n[:, 2].mean() < 0) == front: m.F = m.F[:, ::-1].copy()
    n = vertex_normals(m, weld=False)
    back = Mesh(m.V - n * 0.004, m.F[:, ::-1].copy(), m.UV.copy())
    for mm in (m, back):
        yv = mm.V[:, 1]; f = smoothstep((0.90 - yv) / 0.35) * 0.7
        wl = smoothstep((mm.V[:, 0] + 0.05) / 0.10)
        mm.J = np.zeros((len(yv), 4), np.int64); mm.W = np.zeros((len(yv), 4))
        mm.J[:, 0] = sk['Hips']; mm.W[:, 0] = 1 - f
        mm.J[:, 1] = sk['LeftUpperLeg']; mm.W[:, 1] = f * wl; mm.J[:, 2] = sk['RightUpperLeg']; mm.W[:, 2] = f * (1 - wl)
    return concat([m, back])


def build_tabard(sk, asset, mat=CLOAK, **kw):
    asset.add('tabard_avant', tabard_panel(sk, True, **kw), mat)
    asset.add('tabard_arriere', tabard_panel(sk, False, **kw), mat)
