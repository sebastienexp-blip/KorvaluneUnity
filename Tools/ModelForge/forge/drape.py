"""Vêtements médiévaux « tombés » sur le corps, sans interpénétration.

  drape()  : haut du corps (cotte, chemise, surcot, manches) — la région du corps est subdivisée, découpée exactement
             (bords nets), écartée de la peau d'une aisance, puis lissée comme une étoffe : le tissu passe AU-DESSUS des
             creux (pectoraux, nombril, colonne) au lieu de les mouler ; une contrainte de distance minimale le garde
             toujours hors de la peau. Coutures d'emmanchure : UV cylindriques autour du tronc et autour de chaque bras.
  skirt()  : partie basse (pans de cotte, robe, tablier) — tube fermé défini par l'enveloppe convexe du bassin et des
             jambes, tranche par tranche, jamais rentrant (ne passe pas entre les jambes), évasé vers le bas.
  pose()   : chaque pièce en pose (même calcul que le skinning Unity).
  check()  : contrôle d'interpénétration couche par couche sur des images d'animations (repos, marche…)."""
import numpy as np
from scipy.spatial import cKDTree
import scipy.sparse as sp
from .geo import Mesh, concat, vertex_normals, unit
from .hairgen import _subdivide
from .humanparts import submesh, region, _clip_faces


# ------------------------------------------------------------------------------------------------ outils
def _weld(V, tol=1e-5):
    key = np.round(V / tol).astype(np.int64)
    _, idx, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    return idx, inv.ravel()


def _laplacian(F, n):
    E = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    A = sp.coo_matrix((np.ones(len(E) * 2), (np.r_[E[:, 0], E[:, 1]], np.r_[E[:, 1], E[:, 0]])), shape=(n, n)).tocsr()
    A.data[:] = 1.0
    deg = np.asarray(A.sum(1)).ravel()
    return sp.diags(1 / np.maximum(deg, 1)) @ A


def _border(F, n):
    E = np.sort(np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]), 1)
    u, c = np.unique(E, axis=0, return_counts=True)
    b = np.zeros(n, bool); b[np.unique(u[c == 1])] = True
    return b, u[c == 1]


def _body_frame(body):
    """Normales soudées du corps (mêmes de part et d'autre des coutures UV) + arbre de recherche."""
    idx, inv = _weld(body.V)
    N = vertex_normals(body)
    acc = np.zeros((inv.max() + 1, 3)); np.add.at(acc, inv, N)
    acc /= np.linalg.norm(acc, axis=1, keepdims=True) + 1e-12
    return cKDTree(body.V), acc[inv]


def smooth_weights(J, W, F, nb, iters=6):
    """Poids de skinning diffusés le long du vêtement (le tissu se déforme sans cassure), 4 os max."""
    n = len(J)
    Wf = np.zeros((n, nb)); np.add.at(Wf, (np.repeat(np.arange(n), J.shape[1]), J.ravel()), W.ravel())
    L = _laplacian(F, n)
    for _ in range(iters): Wf = 0.5 * Wf + 0.5 * (L @ Wf)
    order = np.argsort(-Wf, 1)[:, :4]; ww = np.take_along_axis(Wf, order, 1)
    ww /= np.maximum(ww.sum(1, keepdims=True), 1e-9)
    return order.astype(np.int64), ww


def thicken(P, F, UV, J, W, thick, rim_uv=0.02):
    """Face extérieure + face intérieure (vers la peau) + chant fermé aux bords libres → étoffe épaisse et propre."""
    N = vertex_normals(Mesh(P, F, UV))
    outer = Mesh(P, F.copy(), UV.copy(), J.copy(), W.copy())
    inner = Mesh(P - N * thick, F[:, ::-1].copy(), UV.copy(), J.copy(), W.copy())
    _, be = _border(F, len(P))
    parts = [outer, inner]
    if len(be):
        # orientation des arêtes de bord comme dans les faces (pour un chant tourné vers l'extérieur)
        Fe = {}
        for f in F:
            for a, b in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0])): Fe[(a, b)] = True
        ed = np.array([(a, b) if (a, b) in Fe else (b, a) for a, b in be])
        n = len(P)
        V = np.vstack([P, P - N * thick]); UVr = np.vstack([UV, UV + [0, rim_uv]])
        Fr = np.concatenate([np.stack([ed[:, 1], ed[:, 0], ed[:, 0] + n], 1), np.stack([ed[:, 1], ed[:, 0] + n, ed[:, 1] + n], 1)])
        parts.append(Mesh(V, Fr, UVr, np.vstack([J, J]), np.vstack([W, W])))
    out = concat(parts); out.n_outer = len(F)                      # faces extérieures en tête (contrôles)
    return out


# ------------------------------------------------------------------------------------------------ haut du corps
def drape(body, sk, coarse_mask, inside, ease, clear=None, iters=40, subdiv=1, thick=0.003, arm_mask=None, tile=6.0,
          smooth_w=6, max_ease=None, under=(), shrink=0.5):
    """Étoffe tombée sur une région du corps. inside(P) > 0 : partie gardée (bords exacts). ease : aisance (m) ou
    fonction(P) → aisance. clear : distance minimale à la peau (défaut 0,6 × aisance, au moins 6 mm).
    arm_mask(P) → bool : sommets des manches (UV autour du bras, couture d'emmanchure).
    under : vêtements déjà posés [(V, N, écart)] : le tissu reste au-dessus d'eux d'au moins « écart »."""
    s = submesh(body, region(body, coarse_mask, all_in=False))
    tree, BN = _body_frame(body)
    N0 = BN[tree.query(s.V)[1]]
    V, F, N, J, W = _subdivide(s.V, s.F, N0, s.J, s.W, subdiv)
    V, N, J, W, F = _clip_faces(V, N, J, W, F, inside(V))
    # soudure : un seul sommet par position (les UV du corps ne servent plus)
    idx, inv = _weld(V)
    V, N, J, W = V[idx], N[idx], J[idx], W[idx]; F = inv[F]
    F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
    e = ease(V) if callable(ease) else np.full(len(V), float(ease))
    c = np.maximum(0.6 * e, 0.006) if clear is None else (clear(V) if callable(clear) else np.full(len(V), float(clear)))
    P = V + N * e[:, None]
    und = [(Vu, Nu, cu, cKDTree(Vu)) for Vu, Nu, cu in under]
    L = _laplacian(F, len(P)); brd, _ = _border(F, len(P))
    rate = np.where(brd, 0.35, 1.0)[:, None]
    for it in range(iters):
        # lissage rétrécissant + contrainte de distance : le tissu se tend sur les reliefs et enjambe les creux
        # (omoplates, colonne, pectoraux) comme une vraie étoffe, au lieu de mouler l'anatomie
        P = P + rate * shrink * (L @ P - P)
        d, i = tree.query(P)
        s_ = ((P - body.V[i]) * BN[i]).sum(1)
        need = c - s_
        P = P + BN[i] * np.clip(need, 0, None)[:, None]           # jamais plus près de la peau que « clear »
        for (Vu, Nu, cu, tu) in und:                               # ni d'un vêtement de dessous
            du, iu = tu.query(P)
            su = ((P - Vu[iu]) * Nu[iu]).sum(1)
            near = du < 0.05
            P = P + Nu[iu] * (np.clip(cu - su, 0, None) * near)[:, None]
        if max_ease is not None:                                   # ni trop loin (pas de toile tendue sous les aisselles)
            far = s_ - max_ease
            P = P - BN[i] * np.clip(far, 0, None)[:, None] * 0.5
    # poids : ceux du corps sous chaque point, diffusés le long du tissu
    i = tree.query(P)[1]
    J, W = smooth_weights(body.J[i], body.W[i], F, len(sk.names), smooth_w)
    UV, P, F, J, W = _uv_cyl(P, F, J, W, sk, arm_mask, tile)
    return thicken(P, F, UV, J, W, thick)


def _uv_cyl(P, F, J, W, sk, arm_mask, tile):
    """UV en mètres × tile : tronc = cylindre vertical (couture au milieu du dos), manches = cylindre autour du bras
    (couture sous le bras) ; sommets dédoublés aux coutures."""
    pos = {n: np.asarray(p, float) for n, p in zip(sk.names, sk.pos)}
    isarm = arm_mask(P) if arm_mask is not None else np.zeros(len(P), bool)
    farm = isarm[F].sum(1) >= 2                                   # face de manche
    U = np.zeros((len(F), 3, 2))
    # tronc
    th = np.arctan2(P[:, 0], P[:, 2])                              # 0 devant, ±pi au milieu du dos
    r = np.hypot(P[:, 0], P[:, 2])
    ut = th * 0.16; vt = P[:, 1]
    tf = F[~farm]
    U_t = np.stack([ut[tf], vt[tf]], -1)
    span = U_t[..., 0].max(1) - U_t[..., 0].min(1)
    wrap = span > 0.16 * np.pi                                     # face à cheval sur la couture du dos
    U_t[wrap, :, 0] = np.where(U_t[wrap, :, 0] < 0, U_t[wrap, :, 0] + 0.32 * np.pi, U_t[wrap, :, 0])
    U[~farm] = U_t
    # manches
    for sd, sg in (('Left', 1), ('Right', -1)):
        sel = farm & (np.sign(P[F].mean(1)[:, 0]) == sg)
        if not sel.any(): continue
        a = pos[sd + 'UpperArm']; b = pos[sd + 'Hand']; ax = unit(b - a)
        ref = unit(np.cross(ax, [0, 0, 1.0])); ref2 = np.cross(ax, ref)
        Q = P - a
        t = Q @ ax; ang = np.arctan2(Q @ ref2, Q @ ref)              # couture sous le bras (ref ≈ vers le bas)
        ua = ang * 0.05; va = t
        fa = F[sel]
        U_a = np.stack([ua[fa], va[fa]], -1)
        span = U_a[..., 0].max(1) - U_a[..., 0].min(1)
        wrap = span > 0.05 * np.pi
        U_a[wrap, :, 0] = np.where(U_a[wrap, :, 0] < 0, U_a[wrap, :, 0] + 0.10 * np.pi, U_a[wrap, :, 0])
        U[sel] = U_a + [3.0, 0]                                    # îlot à part
    # dédoublement : un sommet par (sommet, UV)
    flat = F.ravel(); uvf = U.reshape(-1, 2)
    key = np.c_[flat, np.round(uvf * 1e5).astype(np.int64)]
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    P2 = P[flat[first]]; J2 = J[flat[first]]; W2 = W[flat[first]]; UV2 = uvf[first] * tile
    return UV2, P2, inv.reshape(-1, 3), J2, W2


# ------------------------------------------------------------------------------------------------ bas : pans et robes
def skirt(body, sk, mask, y_top, y_hem, ease=0.020, flare=0.06, thick=0.004, nth=72, ny=28, tile=6.0, front_only=None,
          follow=0.85, waist_ease=None, extra=None):
    """Jupe / pans de cotte / tablier : tube fermé (ou portion devant si front_only = demi-angle en radians) autour de
    l'enveloppe convexe du bassin et des jambes, de y_top à y_hem, évasé de « flare » à l'ourlet.
    follow : part du mouvement des cuisses suivie par le bas du tissu (le reste suit le bassin)."""
    from .armorparts import hip_envelope
    src = body if extra is None or not len(extra) else Mesh(np.vstack([body.V[mask], extra]), np.zeros((0, 3), np.int64), np.zeros((mask.sum() + len(extra), 2)))
    msk = mask if src is body else np.ones(len(src.V), bool)
    env = hip_envelope(src, msk, y0=min(y_hem, 0.06), y1=max(y_top + 0.02, 1.06))
    th0, th1 = (-front_only, front_only) if front_only else (-np.pi, np.pi)
    th = np.linspace(th0, th1, nth + 1)                             # colonne doublée à la couture du dos (UV propres)
    ys = np.linspace(y_top, y_hem, ny)
    T, Y = np.meshgrid(th, ys)
    f = (y_top - Y) / max(y_top - y_hem, 1e-6)                     # 0 en haut, 1 à l'ourlet
    we = ease if waist_ease is None else waist_ease
    R = env(T, Y) + we + (ease - we) * np.clip(f / 0.25, 0, 1) + flare * f ** 1.6
    P = np.stack([np.sin(T) * R, Y, np.cos(T) * R], -1)
    nr, nc = P.shape[:2]
    idx = np.arange(nr * nc).reshape(nr, nc)
    a_, b_, c_, d_ = idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()
    F = np.concatenate([np.stack([a_, d_, b_], 1), np.stack([b_, d_, c_], 1)])
    P = P.reshape(-1, 3)
    Rm = R.mean(0)                                                  # UV : tour en mètres (rayon moyen), hauteur en mètres
    circ = np.c_[(T * Rm[None]).ravel(), Y.ravel()]
    # poids : bassin en haut, cuisse du même côté vers le bas (devant/derrière partagés selon x)
    H = sk.names.index('Hips'); Lu = sk.names.index('LeftUpperLeg'); Ru = sk.names.index('RightUpperLeg')
    fv = np.clip((y_top - P[:, 1]) / max(y_top - y_hem, 1e-6), 0, 1)
    g = follow * np.clip((fv - 0.05) / 0.6, 0, 1) ** 1.2
    wl = np.clip(0.5 + P[:, 0] / 0.16, 0, 1)
    J = np.zeros((len(P), 4), np.int64); W = np.zeros((len(P), 4))
    J[:, 0] = H; W[:, 0] = 1 - g; J[:, 1] = Lu; W[:, 1] = g * wl; J[:, 2] = Ru; W[:, 2] = g * (1 - wl); J[:, 3] = H
    return thicken(P, F, circ * tile, J, W, thick)


# ------------------------------------------------------------------------------------------------ pose et contrôles
def pose(asset, clip=None, frame=0, outer_only=False):
    """{nom de pièce: (V, N)} en pose (skinning linéaire, comme Unity)."""
    sk = asset.skeleton
    lr = {} if clip is None else {b: q[min(frame, clip.n - 1)] for b, q in clip.rot.items()}
    off = None if clip is None or 'Hips' not in clip.pos else clip.pos['Hips'][min(frame, clip.n - 1)]
    G = sk.fk(lr, off); R = sk.rest_global()
    Mk = np.stack([G[i] @ np.linalg.inv(R[i]) for i in range(len(G))])
    out = {}
    for n, m, mt in asset.parts:
        M = np.einsum('vk,vkij->vij', m.W, Mk[m.J])
        V = np.einsum('vij,vj->vi', M[:, :3, :3], m.V) + M[:, :3, 3]
        out[n] = (V, m.F[:getattr(m, 'n_outer', len(m.F))] if outer_only else m.F)
    return out


def pose_mesh(sk, m, clip=None, frame=0):
    lr = {} if clip is None else {b: q[min(frame, clip.n - 1)] for b, q in clip.rot.items()}
    off = None if clip is None or 'Hips' not in clip.pos else clip.pos['Hips'][min(frame, clip.n - 1)]
    G = sk.fk(lr, off); R = sk.rest_global()
    Mk = np.stack([G[i] @ np.linalg.inv(R[i]) for i in range(len(G))])
    M = np.einsum('vk,vkij->vij', m.W, Mk[m.J])
    return np.einsum('vij,vj->vi', M[:, :3, :3], m.V) + M[:, :3, 3], m.F


def penetration(outer, inner, tol=0.0015, reach=0.04):
    """Points de la couche « outer » situés SOUS la couche « inner » (traversée) : renvoie (nombre, profondeur max).
    Test local : point le plus proche de inner et normale de inner en ce point."""
    Vo, Fo = outer; Vi, Fi = inner
    # sommets soudés (coutures UV) : normales et bords calculés sur la surface continue
    idx, inv = _weld(Vi, 1e-4)
    Fw = inv[Fi]; Vw = Vi[idx]
    Ni = vertex_normals(Mesh(Vw, Fw, np.zeros((len(Vw), 2))), weld=False)
    brd, _ = _border(Fw, len(Vw))
    tree = cKDTree(Vw)
    d, i = tree.query(Vo)
    s = ((Vo - Vw[i]) * Ni[i]).sum(1)
    bad = (s < -tol) & (d < reach) & ~brd[i]              # hors des bords de la couche intérieure (test non défini)
    return int(bad.sum()), float(-s[bad].min()) if bad.any() else 0.0, Vo[bad]


def check(asset, layers, clips=('Idle', 'Walk'), frames=6, tol=0.0015, body=None):
    """layers : liste de groupes de noms de pièces, de la peau (0) vers l'extérieur. Chaque pièce d'une couche ne doit
    traverser AUCUNE pièce des couches inférieures, sur `frames` images de chaque animation. Renvoie un rapport."""
    rep = []
    for cn in clips:
        clip = asset.clips.get(cn) if cn else None
        n = 1 if clip is None else clip.n
        for fr in np.linspace(0, n - 1, frames if clip is not None else 1).astype(int):
            Pz = pose(asset, clip, fr)
            Po = pose(asset, clip, fr, outer_only=True)
            if body is not None: Pz['corps'] = pose_mesh(asset.skeleton, body, clip, fr)
            for li in range(1, len(layers)):
                for on in layers[li]:
                    if on not in Pz: continue
                    for lj in range(li):
                        for inn in layers[lj]:
                            if inn not in Pz: continue
                            k, dmax, pts = penetration(Pz[on], Po.get(inn, Pz[inn]), tol)
                            if k: rep.append((cn, int(fr), on, inn, k, dmax, pts))
    return rep
