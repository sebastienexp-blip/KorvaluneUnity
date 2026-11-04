"""Créatures humanoïdes importées d'un GLB généré à partir des concepts (TRELLIS, gratuit) : mise à l'échelle,
squelette Mecanim ajusté à la silhouette (repères posés à la main), poids de peau, remise en pose T (pour que les
animations du jeu s'appliquent comme aux autres ennemis), textures retravaillées (couleur éclaircie, relief tiré de
la couleur, occlusion des creux).

Repères (coordonnées du modèle remis à l'échelle de 1,83 m, pieds au sol, regard +Z, gauche du personnage +X) :
  Hips, Chest, Neck, Head (tête : centre du crâne), HeadTop,
  L/R_Shoulder (épaule = UpperArm), L/R_Elbow, L/R_Wrist, L/R_HandTip, L/R_Hip (= UpperLeg), L/R_Knee, L/R_Ankle, L/R_Toe
rigid : [(boîte (min, max), os)] — sommets forcés à 100 % sur un os (crosse tenue, pince…)."""
import numpy as np
from scipy.ndimage import gaussian_filter, zoom
from scipy.sparse import coo_matrix, diags
from forge.glb import GLB
from forge.geo import Mesh
from forge.rig import Asset
from forge.texgen import TexSet, height_to_normal, srgb_to_lin, lin_to_srgb
from forge import humanoid as H

BASE_H = 1.83


def load(path, height=BASE_H, yaw=0.0):
    g = GLB(path); parts = []
    for V, N, UV, F, mi in g.meshes():
        parts.append((V, UV, F, mi))
    V = np.vstack([p[0] for p in parts]); UV = np.vstack([p[1] for p in parts])
    F = np.vstack([p[2] + sum(len(q[0]) for q in parts[:k]) for k, p in enumerate(parts)])
    if yaw:
        c, s = np.cos(np.radians(yaw)), np.sin(np.radians(yaw)); V = V @ np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]]).T
    lo, hi = V.min(0), V.max(0); sc = height / (hi[1] - lo[1])
    V = (V - np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])) * sc
    base, mr, nrm, fac = g.material(parts[0][3])
    return V, UV, F, base


def drop_islands(V, F, keep_min=0.002):
    """Retire les petits morceaux flottants (débris de génération) : composantes de moins de keep_min des faces."""
    from scipy.sparse.csgraph import connected_components
    key = np.round(V / 1e-4).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.ravel()
    n = inv.max() + 1; Fw = inv[F]
    A = coo_matrix((np.ones(len(Fw) * 3), (np.r_[Fw[:, 0], Fw[:, 1], Fw[:, 2]], np.r_[Fw[:, 1], Fw[:, 2], Fw[:, 0]])), shape=(n, n))
    nc, lab = connected_components(A, directed=False)
    fl = lab[Fw[:, 0]]; cnt = np.bincount(fl, minlength=nc)
    ok = cnt[fl] >= keep_min * len(F)
    return F[ok], int((~ok).sum())


def _adjacency(V, F):
    key = np.round(V / 1e-4).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.ravel()
    n = inv.max() + 1; Fw = inv[F]
    r = np.r_[Fw[:, 0], Fw[:, 1], Fw[:, 2], Fw[:, 1], Fw[:, 2], Fw[:, 0]]; c = np.r_[Fw[:, 1], Fw[:, 2], Fw[:, 0], Fw[:, 0], Fw[:, 1], Fw[:, 2]]
    A = coo_matrix((np.ones(len(r)), (r, c)), shape=(n, n)).tocsr(); A.data[:] = 1
    deg = np.asarray(A.sum(1)).ravel(); P = diags(1 / np.maximum(deg, 1)) @ A
    return inv, P


def _rotation_between(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b); v = np.cross(a, b); c = float(np.dot(a, b))
    if c < -0.9999:
        ax = np.cross(a, [0, 0, 1.0]) if abs(a[2]) < 0.9 else np.cross(a, [1.0, 0, 0]); ax /= np.linalg.norm(ax)
        return 2 * np.outer(ax, ax) - np.eye(3)
    K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + K + K @ K / (1 + c)


# os déformants (doigts → main, orteils → pied, chaînes de cape ignorées)
CHAIN = [('Hips', 'Spine'), ('Spine', 'Chest'), ('Chest', 'UpperChest'), ('UpperChest', 'Neck'), ('Neck', 'Head'), ('Head', 'HeadTop'),
         ('LeftShoulder', 'LeftUpperArm'), ('LeftUpperArm', 'LeftLowerArm'), ('LeftLowerArm', 'LeftHand'), ('LeftHand', 'LeftHandTip'),
         ('RightShoulder', 'RightUpperArm'), ('RightUpperArm', 'RightLowerArm'), ('RightLowerArm', 'RightHand'), ('RightHand', 'RightHandTip'),
         ('LeftUpperLeg', 'LeftLowerLeg'), ('LeftLowerLeg', 'LeftFoot'), ('LeftFoot', 'LeftToes'),
         ('RightUpperLeg', 'RightLowerLeg'), ('RightLowerLeg', 'RightFoot'), ('RightFoot', 'RightToes')]


def fit(L):
    """Repères (pose du modèle) → (positions de repos en pose T, positions posées, rotation posée de chaque os déformant)."""
    L = {k: np.asarray(v, float) for k, v in L.items()}
    P = {}                                       # positions des articulations dans la pose du modèle
    P['Hips'] = L['Hips']; P['UpperChest'] = L['Chest']; P['Neck'] = L['Neck']; P['Head'] = L['Head']; P['HeadTop'] = L['HeadTop']
    P['Spine'] = P['Hips'] + (P['UpperChest'] - P['Hips']) * 0.3; P['Chest'] = P['Hips'] + (P['UpperChest'] - P['Hips']) * 0.65
    for s, S in (('L', 'Left'), ('R', 'Right')):
        P[f'{S}UpperArm'] = L[f'{s}_Shoulder']; P[f'{S}LowerArm'] = L[f'{s}_Elbow']; P[f'{S}Hand'] = L[f'{s}_Wrist']; P[f'{S}HandTip'] = L[f'{s}_HandTip']
        P[f'{S}Shoulder'] = P['UpperChest'] + (P[f'{S}UpperArm'] - P['UpperChest']) * np.array([0.25, 0.6, 0.6]) + np.array([0, 0.05, 0])
        P[f'{S}UpperLeg'] = L[f'{s}_Hip']; P[f'{S}LowerLeg'] = L[f'{s}_Knee']; P[f'{S}Foot'] = L[f'{s}_Ankle']; P[f'{S}Toes'] = L[f'{s}_Toe']
    # pose T : colonne telle quelle ; bras à l'horizontale le long de ±X ; jambes verticales ; pieds vers +Z
    R = {}
    for k in ('Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'HeadTop'): R[k] = P[k].copy()
    for S, sx in (('Left', 1), ('Right', -1)):
        R[f'{S}Shoulder'] = P[f'{S}Shoulder'].copy()
        R[f'{S}UpperArm'] = np.array([P[f'{S}UpperArm'][0], P[f'{S}Shoulder'][1] - 0.01, P[f'{S}Shoulder'][2]])
        x = R[f'{S}UpperArm']
        for a, b in (('UpperArm', 'LowerArm'), ('LowerArm', 'Hand'), ('Hand', 'HandTip')):
            ln = np.linalg.norm(P[f'{S}{b}'] - P[f'{S}{a}']); x = x + np.array([sx * ln, 0, 0]); R[f'{S}{b}'] = x
        R[f'{S}UpperLeg'] = P[f'{S}UpperLeg'].copy(); x = R[f'{S}UpperLeg']
        for a, b in (('UpperLeg', 'LowerLeg'), ('LowerLeg', 'Foot')):
            ln = np.linalg.norm(P[f'{S}{b}'] - P[f'{S}{a}']); x = x + np.array([0, -ln, 0]); R[f'{S}{b}'] = x
        ln = np.linalg.norm(P[f'{S}Toes'] - P[f'{S}Foot']); R[f'{S}Toes'] = R[f'{S}Foot'] + np.array([0, -R[f'{S}Foot'][1] * 0.75, ln])
    # rotation posée de chaque os (repos → pose) : alignement de la direction de l'os ; la main suit l'avant-bras
    Rot = {}
    for a, b in CHAIN: Rot[a] = _rotation_between(R[b] - R[a], P[b] - P[a])
    return R, P, Rot


def skin(V, F, R, P, Rot, rigid=(), smooth=12, exclude=()):
    """Poids (4 influences) calculés dans la pose du modèle ; renvoie (J indices dans `names`, W, V en pose T, names)."""
    names = [a for a, _ in CHAIN if a not in exclude]
    segs = [(P[a], P[b]) for a, b in CHAIN if a not in exclude]
    D = np.zeros((len(V), len(names)))
    for k, (a, b) in enumerate(segs):
        ab = b - a; t = np.clip(((V - a) @ ab) / (ab @ ab + 1e-12), 0, 1)
        D[:, k] = np.linalg.norm(V - (a + t[:, None] * ab), axis=1)
    Wt = 1.0 / np.maximum(D, 0.01) ** 4
    for (lo, hi), bone in rigid:
        m = np.all((V >= lo) & (V <= hi), axis=1); Wt[m] = 0; Wt[m, names.index(bone)] = 1
    Wt /= Wt.sum(1, keepdims=True)
    inv, Pm = _adjacency(V, F)
    n = inv.max() + 1; Wv = np.zeros((n, len(names))); np.add.at(Wv, inv, Wt); Wv /= np.bincount(inv, minlength=n)[:, None]
    lock = np.zeros(n, bool)
    for (lo, hi), bone in rigid: lock[inv[np.all((V >= lo) & (V <= hi), axis=1)]] = True
    W0 = Wv.copy()
    for _ in range(smooth):
        Wv = Pm @ Wv; Wv[lock] = W0[lock]
    Wt = Wv[inv]
    idx = np.argsort(-Wt, 1)[:, :4]; w = np.take_along_axis(Wt, idx, 1); w /= w.sum(1, keepdims=True)
    # pose du modèle → pose T : inverse du skinning linéaire (matrice mélangée par sommet)
    Ms = []
    for nm in names:
        M = np.eye(4); M[:3, :3] = Rot[nm]; M[:3, 3] = P[nm] - Rot[nm] @ R[nm]; Ms.append(M)
    Ms = np.stack(Ms)
    B = np.einsum('vk,vkij->vij', w, Ms[idx])
    Vh = np.c_[V, np.ones(len(V))]
    Vt = np.einsum('vij,vj->vi', np.linalg.inv(B), Vh)[:, :3]
    return idx, w, Vt, names


def skeleton(R):
    """Squelette du jeu (noms Mecanim, doigts, cape, attaches) déplacé sur les positions de repos ajustées."""
    sk = H.build()
    old = {n: p.copy() for n, p in zip(sk.names, sk.pos)}
    def move(n, p): sk.pos[sk.names.index(n)] = np.asarray(p, float)
    for n in ('Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Head'): move(n, R[n])
    for S in ('Left', 'Right'):
        for n in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand', 'UpperLeg', 'LowerLeg', 'Foot', 'Toes'): move(S + n, R[S + n])
        hs = np.linalg.norm(R[S + 'HandTip'] - R[S + 'Hand']) / np.linalg.norm(np.asarray(H.FINGERS['Middle'][3]) - np.asarray(H.ARM['Hand']))
        for i, n in enumerate(sk.names):          # doigts et attache d'arme : suivent la main (taille de main ajustée)
            if n.startswith(S) and any(f in n for f in H.FINGERS):
                move(n, R[S + 'Hand'] + (old[n] - old[S + 'Hand']) * hs)
    d = R['UpperChest'] - old['UpperChest']
    for i, n in enumerate(sk.names):
        if n.startswith('Cape_') or n == 'Socket_Back': move(n, old[n] + d)
    move('Socket_Weapon_R', R['RightHand'] + (old['Socket_Weapon_R'] - old['RightHand']))
    move('Socket_Shield_L', R['LeftLowerArm'] + (old['Socket_Shield_L'] - old['LeftLowerArm']))
    sk._rg = None
    return sk


def texture(base_img, size=2048, gain=1.0, gamma=1.0, sat=1.0, relief=6.0, smooth=0.28, metal_mask=None, emit=None):
    """TexSet à partir de la couleur générée : niveaux (gain, gamma, saturation), relief et occlusion déduits de la
    luminance (détails fins), lissé faible (tissus, os), métal optionnel (masque fonction de la couleur)."""
    im = base_img.convert('RGB')
    if im.size[0] != size: im = im.resize((size, size), 3)
    c = np.asarray(im, np.float32) / 255.0
    lin = srgb_to_lin(c) * gain
    lum = lin.mean(-1, keepdims=True); lin = np.clip(lum + (lin - lum) * sat, 0, None)
    c2 = lin_to_srgb(np.clip(lin, 0, 1)) ** gamma
    y = c2 @ np.array([0.3, 0.59, 0.11])
    hg = y - gaussian_filter(y, 6) * 0.6 - gaussian_filter(y, 1.2) * 0.4 + (gaussian_filter(y, 1.2) - gaussian_filter(y, 3))
    nrm = height_to_normal(gaussian_filter(hg, 0.7) * 255.0 / 40.0, relief)
    cav = np.clip(gaussian_filter(y, 5) - gaussian_filter(y, 1), 0, None)
    ao = np.clip(1 - cav * 3.0, 0.45, 1)
    metal = np.zeros(y.shape) if metal_mask is None else metal_mask(c2)
    sm = np.full(y.shape, smooth) * (0.7 + 0.6 * y) + metal * 0.25
    em = None if emit is None else emit(c2)
    return TexSet(np.clip(c2, 0, 1), nrm, np.clip(metal, 0, 1), np.clip(sm, 0, 1), ao, emit=em)


def build(name, path, landmarks, rigid=(), yaw=0.0, mat='Peau', smooth=12, min_island=0.002):
    V, UV, F, base = load(path, BASE_H, yaw)
    F, dropped = drop_islands(V, F, min_island)
    R, P, Rot = fit(landmarks)
    J, W, Vt, names = skin(V, F, R, P, Rot, rigid, smooth)
    sk = skeleton(R)
    lut = np.array([sk.names.index(n) for n in names]); J = lut[J]
    a = Asset(name, sk)
    m = Mesh(Vt, F, UV, J, W); m.UV = m.UV.copy(); m.UV[:, 1] = 1 - m.UV[:, 1]     # glTF (origine en haut) → FBX/Unity
    a.add('corps', m, mat)
    print(f'   {name} : {len(Vt)} sommets, {len(F)} triangles ({dropped} retirés en débris)', flush=True)
    return a, base
