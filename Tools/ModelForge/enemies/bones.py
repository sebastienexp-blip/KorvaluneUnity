"""Squelette humain anatomique modélisé sur le gabarit Corvalune (mêmes articulations que les héros) : crâne tiré de la
tête MakeHuman (chairs retirées : orbites creusées, cavité nasale, joues émaciées, lèvres rétractées sur les dents,
oreilles effacées), colonne de 24 vertèbres à apophyses, 12 paires de côtes qui suivent la cage thoracique réelle,
sternum et cartilages, clavicules, omoplates, bassin (ailes iliaques, sacrum, pubis), humérus, radius et cubitus, os du
carpe, métacarpes et phalanges, fémurs, rotules, tibias, péronés, tarses, métatarses et orteils."""
import numpy as np
from scipy.spatial import ConvexHull
from forge.geo import Loft, Mesh, spline, curve, concat, rigid, unit, mirror, vertex_normals, smoothstep
from forge import humanoid as H, humanparts as HP

BONE, DARK, CART = 'SK_Bone', 'SK_Dark', 'SK_Cartilage'


def blob(c, r, n=14, rot=None):
    th = np.linspace(0, np.pi, n // 2 + 1); ph = np.linspace(0, 2 * np.pi, n + 1)
    TH, PH = np.meshgrid(th, ph, indexing='ij')
    P = np.stack([np.sin(TH) * np.cos(PH) * r[0], np.cos(TH) * r[1], np.sin(TH) * np.sin(PH) * r[2]], -1).reshape(-1, 3)
    if rot is not None: P = P @ np.asarray(rot).T
    P = P + np.asarray(c, float)
    R, C = TH.shape; idx = np.arange(R * C).reshape(R, C)
    F = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                        np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
    m = Mesh(P, F, np.stack([PH.ravel() / (2 * np.pi), TH.ravel() / np.pi], 1) * 0.6)
    if (vertex_normals(m) * (P - np.asarray(c))).sum(1).mean() < 0: m.F = m.F[:, ::-1].copy()
    return m


def frame_rot(axis, up=(0, 1, 0)):
    """Rotation qui envoie Y sur axis."""
    y = unit(np.asarray(axis, float)); x = np.cross(np.asarray(up, float), y)
    if np.linalg.norm(x) < 1e-6: x = np.cross((1, 0, 0), y)
    x = unit(x); z = np.cross(x, y)
    return np.stack([x, y, z], 1)


def long_bone(p0, p1, r_mid, r0, r1, n=24, flat=1.0, knob0=None, knob1=None, segs=12):
    """Os long : diaphyse étranglée entre deux épiphyses élargies ; knob = (rayon x, rayon y, rayon z) d'une tête articulaire."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    t = np.linspace(0, 1, n)
    pts = p0[None] + (p1 - p0)[None] * t[:, None]
    prof = r_mid + (r0 - r_mid) * np.exp(-(t / 0.13) ** 2) + (r1 - r_mid) * np.exp(-((1 - t) / 0.13) ** 2)
    d = unit(p1 - p0); ref = np.cross(d, (0, 0, 1)) if abs(d[2]) < 0.9 else np.cross(d, (1, 0, 0))
    lo = Loft(pts, prof * flat, prof, p=2.2, segs=segs, ref=tuple(unit(ref)), cap0=2, cap1=2)
    parts = [lo.mesh(0.12)]
    R = frame_rot(d)
    if knob0 is not None: parts.append(blob(p0, knob0, rot=R))
    if knob1 is not None: parts.append(blob(p1, knob1, rot=R))
    return concat(parts)


def tube_var(pts, rf, rs, n=24, ref=(0, 1, 0), segs=10, caps=True):
    P = spline(np.asarray(pts, float), n)
    return Loft(P, curve(rf, n) if isinstance(rf, list) else rf, curve(rs, n) if isinstance(rs, list) else rs, p=2.2, segs=segs, ref=ref,
                cap0=1 if caps else 0, cap1=1 if caps else 0).mesh(0.1)


# ------------------------------------------------------------------------------------------- coupes du tronc
class Torso:
    """Enveloppe convexe des coupes horizontales du tronc réel (bras exclus) : rayon polaire r(y, θ), θ=0 devant, π/2 côté gauche."""
    def __init__(self, body, sk):
        V = body.V; ax = np.abs(V[:, 0])
        arms = HP.bone_mask(body, sk, ['UpperArm', 'LowerArm', 'Hand'], 0.2)
        self.V = V[~arms & (ax < 0.22) & (V[:, 1] > 0.85) & (V[:, 1] < 1.62)]
        self.cache = {}

    def section(self, y, h=0.010):
        k = round(y, 3)
        if k in self.cache: return self.cache[k]
        sel = np.abs(self.V[:, 1] - y) < h
        xz = self.V[sel][:, [0, 2]]
        hull = xz[ConvexHull(xz).vertices]
        c = np.array([0.0, hull[:, 1].mean()])
        self.cache[k] = (c, hull)
        return c, hull

    def r(self, y, th):
        c, hull = self.section(y)
        hp = hull - c; hq = np.roll(hp, -1, 0); e = hq - hp
        out = []
        for t in np.atleast_1d(th):
            u = np.array([np.sin(t), np.cos(t)])
            den = u[0] * e[:, 1] - u[1] * e[:, 0]; ok = np.abs(den) > 1e-12
            s = np.where(ok, (hp[:, 0] * e[:, 1] - hp[:, 1] * e[:, 0]) / np.where(ok, den, 1), -1)
            w = np.where(ok, (hp[:, 0] * u[1] - hp[:, 1] * u[0]) / np.where(ok, den, 1), -1)
            hit = ok & (s > 0) & (w >= -1e-9) & (w <= 1 + 1e-9)
            out.append(s[hit].max() if hit.any() else 0.1)
        return np.array(out)

    def point(self, y, th, inset):
        c, _ = self.section(y)
        rr = self.r(y, th) - inset
        th = np.atleast_1d(th)
        return np.stack([rr * np.sin(th), np.full_like(rr, y), c[1] + rr * np.cos(th)], -1)


# ------------------------------------------------------------------------------------------- crâne
def skull(body, sk, marks):
    """Crâne obtenu en « décharnant » la tête MakeHuman. Renvoie (os, creux sombres)."""
    V, F = body.V, body.F
    eL, eR, mo = (np.asarray(marks[k]) for k in ('eye_L', 'eye_R', 'mouth'))
    eye_y = 0.5 * (eL[1] + eR[1])
    head = (HP.bone_mask(body, sk, ['Head'], 0.3) | HP.bone_mask(body, sk, ['Neck'], 0.3)) & (V[:, 1] > mo[1] - 0.062)
    head &= ~((V[:, 1] < eye_y - 0.050) & (V[:, 2] < eL[2] - 0.080))         # la nuque s'arrête sous l'occiput (trou occipital)
    fm = HP.region(body, head)
    s = HP.submesh(body, fm)
    P = s.V.copy(); N = vertex_normals(s)
    hc = np.array([0.0, eye_y + 0.01, eL[2] - 0.085])                    # centre approximatif de la boîte crânienne
    d = np.full(len(P), 0.004)
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    front = z > eL[2] - 0.045
    cheek = smoothstep((0.024 - np.abs(np.abs(x) - 0.046)) / 0.020) * smoothstep((0.030 - np.abs(y - (eye_y - 0.050))) / 0.022) * front
    d += 0.016 * cheek
    temple = smoothstep((0.03 - np.abs(y - (eye_y + 0.005))) / 0.025) * (np.abs(x) > 0.052) * (z < eL[2] - 0.005) * (z > eL[2] - 0.07)
    d += 0.008 * temple
    P = P - N * d[:, None]
    # lèvres et chairs du museau ramenées au plan des dents
    lipz = mo[2] - 0.010
    lip = smoothstep((0.036 - np.abs(P[:, 0])) / 0.012) * smoothstep((0.024 - np.abs(P[:, 1] - mo[1])) / 0.012) * (P[:, 2] > lipz)
    P[:, 2] = P[:, 2] - (P[:, 2] - lipz) * lip
    # nez : aplani jusqu'à l'os puis cavité en cœur renversé
    nreg = smoothstep((0.024 - np.abs(P[:, 0])) / 0.010) * smoothstep((0.030 - np.abs(P[:, 1] - (eye_y - 0.035))) / 0.012)
    zface = eL[2] + 0.004
    P[:, 2] = np.where(P[:, 2] > zface, P[:, 2] - (P[:, 2] - zface) * nreg, P[:, 2])
    nz = smoothstep((0.012 - np.abs(P[:, 0]) * (1.0 + 2.0 * np.clip((P[:, 1] - (eye_y - 0.045)) / 0.03, 0, 1))) / 0.005) * smoothstep((0.018 - np.abs(P[:, 1] - (eye_y - 0.042))) / 0.008)
    P[:, 2] -= 0.022 * nz * (P[:, 2] > eL[2] - 0.02)
    # orbites : cratères profonds aux bords nets
    for e in (eL, eR):
        dx = P[:, 0] - e[0]; dy = (P[:, 1] - e[1] + 0.002) * 1.10
        rr = np.sqrt(dx ** 2 + dy ** 2)
        f = smoothstep((0.025 - rr) / 0.006) * (P[:, 2] > e[2] - 0.04)
        P[:, 2] = P[:, 2] - f * (P[:, 2] - (e[2] - 0.034)) * 0.95
    # oreilles effacées
    ear = (np.abs(P[:, 0]) > 0.054) & (np.abs(P[:, 1] - (eye_y - 0.01)) < 0.07) & (P[:, 2] < eL[2] - 0.020)
    P[ear, 0] = np.sign(P[ear, 0]) * 0.056
    # mâchoires plus étroites que le visage de chair (maxillaire, mandibule)
    low = smoothstep(((eye_y - 0.045) - P[:, 1]) / 0.03) * (P[:, 2] > eL[2] - 0.075)
    P[:, 0] *= 1 - 0.16 * low
    # occiput arrondi : la nuque rentre sous le crâne
    yo = eye_y - 0.015
    back = (P[:, 2] < hc[2] + 0.02) & (P[:, 1] < yo)
    k = np.clip((yo - P[:, 1]) / 0.045, 0, 1)
    P[back, 2] += (hc[2] + 0.02 - P[back, 2]) * smoothstep(k[back])
    P[back, 0] *= 1 - 0.35 * smoothstep(k[back])
    s.V = P
    c = P.mean(0)
    s.UV = np.c_[np.arctan2(P[:, 0] - c[0], P[:, 2] - c[2]) / np.pi * 1.2, (P[:, 1] - c[1]) / 0.10]
    # creux sombres : fond des orbites et de la cavité nasale
    holes = [blob(e + (0, 0, -0.030), (0.019, 0.016, 0.010)) for e in (eL, eR)]
    holes.append(blob((0, eye_y - 0.040, eL[2] - 0.016), (0.010, 0.016, 0.008)))
    hm = concat(holes)
    s = concat([s, blob((0, mo[1] + 0.004, eL[2] - 0.070), (0.044, 0.022, 0.046))])     # base du crâne (ferme le dessous)
    for m in (s, hm): rigid(m, sk['Head'])
    return s, hm


# ------------------------------------------------------------------------------------------- colonne, côtes, sternum
def spine_and_ribs(body, sk, T):
    W = lambda m: HP.weights_from(body, m)
    bone, cart = [], []
    # centre de la colonne : à profondeur fixe sous la peau du dos
    def spine_c(y):
        if y > 1.50:
            c, hull = T.section(min(y, 1.58)); zb = c[1] - T.r(min(y, 1.58), np.pi)[0]
            return np.array([0, y, zb + 0.032])
        c, _ = T.section(y); zb = c[1] - T.r(y, np.pi)[0]
        return np.array([0, y, zb + (0.055 if y < 1.12 else 0.046)])
    ys = np.r_[np.linspace(0.97, 1.10, 5, endpoint=False), np.linspace(1.10, 1.47, 12, endpoint=False), np.linspace(1.47, 1.60, 7)]
    for k, y in enumerate(ys):
        c = spine_c(y); lum = y < 1.10; cerv = y >= 1.47
        rc = 0.021 if lum else (0.012 if cerv else 0.016)
        hgt = 0.011 if lum else (0.007 if cerv else 0.009)
        bone.append(blob(c, (rc * 1.15, hgt, rc)))
        # apophyse épineuse : vers l'arrière et le bas
        L = 0.030 if lum else (0.020 if cerv else 0.034)
        tip = c + np.array([0, -0.6 * L if not lum else -0.2 * L, -L])
        bone.append(tube_var([c + (0, 0, -0.008), (c + tip) / 2, tip], [(0, .007), (1, .004)], [(0, .004), (1, .0025)], n=8, ref=(1, 0, 0), segs=8))
        # apophyses transverses
        for sx in (1, -1):
            tp = c + np.array([sx * (0.034 if lum else 0.026 if not cerv else 0.020), 0.0, -0.010])
            bone.append(tube_var([c, tp], [(0, .004), (1, .003)], [(0, .004), (1, .003)], n=5, ref=(0, 1, 0), segs=6))
    # côtes : 12 paires
    for i in range(12):
        yv = 1.445 - i * 0.029
        drop = 0.045 + 0.011 * i
        c0 = spine_c(yv)
        th_end = 0.42 if i < 7 else (0.62 if i < 10 else (1.45 if i == 10 else 1.75))
        ths = np.linspace(np.pi - 0.22, th_end, 26)
        yy = yv - drop * (1 - (ths - th_end) / (np.pi - 0.22 - th_end)) ** 0 * smoothstep((np.pi - ths) / (np.pi - th_end)) ** 0.8
        ins = 0.010 + 0.018 * smoothstep((ths - 2.2) / 0.8)
        pts = np.vstack([T.point(yy[j], ths[j], ins[j]) for j in range(len(ths))])
        for sx in (1, -1):
            pp = pts.copy(); pp[:, 0] *= sx
            path = np.vstack([c0 + (sx * 0.012, 0, -0.004), c0 + (sx * 0.030, -0.002, -0.020), pp])
            w_ = 0.0075 if i < 10 else 0.006
            m = tube_var(path, [(0, .006), (.15, w_), (.85, w_ * 1.05), (1, w_ * 0.9)], [(0, .006), (.15, .0040), (1, .0035)], n=40, ref=(0, 1, 0), segs=8)
            bone.append(m)
            if i < 10:   # cartilage costal vers le sternum (ou la côte supérieure)
                end = pp[-1]
                if i < 7: tgt = np.array([sx * 0.016, 1.40 - i * 0.026, 0]); tgt[2] = T.point(tgt[1], 0.0, 0.012)[0, 2]
                else: tgt = np.array([sx * 0.030, 1.21, 0]); tgt[2] = T.point(1.21, 0.15, 0.012)[0, 2]
                mid = 0.5 * (end + tgt) + np.array([0, -0.004, 0.004])
                cart.append(tube_var([end, mid, tgt], [(0, .0065), (1, .0055)], [(0, .0035), (1, .0030)], n=12, ref=(0, 1, 0), segs=8))
    # sternum : manubrium, corps, appendice xiphoïde
    sy = np.linspace(1.43, 1.19, 14)
    sp = np.vstack([T.point(y, 0.0, 0.011) for y in sy])
    bone.append(Loft(spline(sp, 20), curve([(0, .006), (.3, .005), (1, .003)], 20), curve([(0, .022), (.25, .015), (.6, .017), (.9, .012), (1, .004)], 20),
                     p=2.4, segs=12, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.1))
    mb, mc = concat(bone), concat(cart)
    return W(mb), W(mc)


# ------------------------------------------------------------------------------------------- ceintures, membres
def girdles(body, sk, T):
    out = []   # (mesh, bone)
    ua = np.array(H.ARM['UpperArm'])
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        f = lambda p: np.array([p[0] * sx, p[1], p[2]])
        # clavicule en S, du manubrium à l'acromion
        st = T.point(1.432, 0.10 * sx, 0.012)[0]
        acr = f((0.168, 1.478, -0.012))
        cl = tube_var([st, st * 0.6 + acr * 0.4 + np.array([0, 0.012, 0.012]), st * 0.25 + acr * 0.75 + np.array([0, 0.008, -0.008]), acr],
                      [(0, .008), (.5, .005), (1, .007)], [(0, .007), (.5, .005), (1, .009)], n=18, ref=(0, 1, 0), segs=8)
        out.append((cl, f'{side}Shoulder'))
        # omoplate : lame triangulaire collée au dos, épine et acromion
        A = f((0.155, 1.470, -0.030)); B = f((0.070, 1.435, 0)); C = f((0.085, 1.250, 0))
        for P_ in (B, C): P_[2] = T.point(P_[1], np.pi - 0.45 * sx, 0.016)[0, 2]
        A[2] = T.point(1.44, np.pi - 1.1 * sx, 0.020)[0, 2] if False else A[2] - 0.035
        uu, vv = np.meshgrid(np.linspace(0, 1, 9), np.linspace(0, 1, 9))
        Q = A[None, None] * (1 - uu - vv)[..., None].clip(0) + B[None, None] * uu[..., None] + C[None, None] * vv[..., None]
        msk = (uu + vv) <= 1.0001
        Q = np.where(msk[..., None], Q, A[None, None])
        for i_ in range(9):
            for j_ in range(9):
                yv = Q[i_, j_, 1]; xv = Q[i_, j_, 0]
                th = np.arctan2(xv, -1e-3) if False else np.pi - np.arctan2(abs(xv), 0.10) * sx
                Q[i_, j_, 2] = T.point(np.clip(yv, 0.9, 1.6), th, 0.018)[0, 2]
        pl = []
        for dz in (0.0, 0.006):
            Pq = Q.copy(); Pq[..., 2] += dz
            R, Cc = Pq.shape[:2]; idx = np.arange(R * Cc).reshape(R, Cc)
            Fq = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                                 np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
            mm = Mesh(Pq.reshape(-1, 3), Fq if dz else Fq[:, ::-1], np.c_[uu.ravel(), vv.ravel()] * 0.5)
            pl.append(mm)
        out.append((concat(pl), f'{side}Shoulder'))
        spn = tube_var([A + (0, 0, 0.004), 0.5 * (A + B) + np.array([0, -0.035, -0.012]), B + np.array([0, -0.045, -0.008])],
                       [(0, .010), (1, .004)], [(0, .006), (1, .003)], n=12, ref=(0, 1, 0), segs=8)
        out.append((spn, f'{side}Shoulder'))
        # humérus
        el = f(H.ARM['LowerArm']); hd = f(H.ARM['UpperArm']) + np.array([-0.005 * sx, 0, 0])
        out.append((long_bone(hd + f((0.012, 0, 0)), el, 0.0105, 0.017, 0.016, flat=0.75, knob0=(0.024, 0.024, 0.024), knob1=(0.012, 0.022, 0.014)), f'{side}UpperArm'))
        # radius et cubitus (olécrane derrière le coude)
        wr = f(H.ARM['Hand'])
        out.append((long_bone(el + np.array([0.010 * sx, 0, 0.008]), wr + np.array([-0.008 * sx, 0, 0.012]), 0.0065, 0.009, 0.012, knob0=(0.009, 0.009, 0.009)), f'{side}LowerArm'))
        out.append((long_bone(el + np.array([-0.012 * sx, 0, -0.012]), wr + np.array([-0.010 * sx, 0, -0.010]), 0.006, 0.011, 0.007, knob0=(0.011, 0.012, 0.011)), f'{side}LowerArm'))
        # carpe et main
        hand = [blob(wr + np.array([0.018 * sx, 0, 0]), (0.020, 0.011, 0.028))]
        for fn, pts in H.FINGERS.items():
            p = [f(q) for q in pts]
            base_ = wr + np.array([0.028 * sx, 0, 0]) + (p[0] - wr) * np.array([0, 1, 0.55])
            if fn == 'Thumb': base_ = wr + np.array([0.020 * sx, -0.006, 0.020])
            hand.append(long_bone(base_, p[0], 0.0042, 0.0058, 0.0062, n=12, segs=8, knob1=(0.006, 0.006, 0.006)))
            out.append((concat(hand), f'{side}Hand')); hand = []
            for k, seg in enumerate(('Proximal', 'Intermediate', 'Distal')):
                rr_ = (0.0042, 0.0036, 0.0030)[k]
                out.append((long_bone(p[k], p[k + 1], rr_, rr_ * 1.35, rr_ * 1.25, n=10, segs=8, knob1=(rr_ * 1.2,) * 3 if k < 2 else (rr_ * 0.9, rr_ * 0.9, rr_ * 0.9)), f'{side}{fn}{seg}'))
    # bassin : ailes iliaques en coupe, sacrum, arc pubien
    pel = []
    for sx in (1, -1):
        az = np.linspace(np.radians(-120), np.radians(35), 18); el_ = np.linspace(np.radians(-10), np.radians(62), 10)
        AZ, EL = np.meshgrid(az, el_)
        c = np.array([sx * 0.030, 0.985, -0.010])
        rad = np.array([0.112, 0.100, 0.085])
        for dr, flip in ((0.0, False), (-0.006, True)):
            Pw = np.stack([c[0] + sx * (rad[0] + dr) * np.cos(EL) * np.cos(AZ), c[1] + (rad[1] + dr) * np.sin(EL), c[2] + (rad[2] + dr) * np.cos(EL) * np.sin(AZ)], -1)
            R, Cc = Pw.shape[:2]; idx = np.arange(R * Cc).reshape(R, Cc)
            Fq = np.concatenate([np.stack([idx[:-1, :-1].ravel(), idx[1:, :-1].ravel(), idx[:-1, 1:].ravel()], 1),
                                 np.stack([idx[:-1, 1:].ravel(), idx[1:, :-1].ravel(), idx[1:, 1:].ravel()], 1)])
            mm = Mesh(Pw.reshape(-1, 3), Fq, np.c_[AZ.ravel(), EL.ravel()] * 0.4)
            n_ = vertex_normals(mm)
            if ((n_ * (mm.V - c)).sum(1).mean() < 0) != flip: mm.F = mm.F[:, ::-1].copy()
            pel.append(mm)
        crest = np.stack([c[0] + sx * rad[0] * np.cos(el_[-1]) * np.cos(az), np.full_like(az, c[1] + rad[1] * np.sin(el_[-1])), c[2] + rad[2] * np.cos(el_[-1]) * np.sin(az)], -1)
        pel.append(Loft(spline(crest, 30), 0.0055, 0.0055, p=2, segs=8, cap0=1, cap1=1).mesh(0.1))
        # acétabulum, pubis, ischion (anneau obturateur)
        ac = np.array([sx * 0.088, 0.952, 0.012])
        pel.append(blob(ac, (0.022, 0.022, 0.022)))
        ring = [ac + (0, -0.004, 0.012), np.array([sx * 0.045, 0.918, 0.062]), np.array([sx * 0.008, 0.905, 0.070]), np.array([sx * 0.030, 0.878, 0.050]),
                np.array([sx * 0.062, 0.868, 0.000]), ac + (0, -0.022, -0.012)]
        pel.append(tube_var(ring, [(0, .010), (.4, .008), (1, .011)], [(0, .010), (.4, .008), (1, .011)], n=24, ref=(0, 1, 0), segs=8))
    sac = tube_var([(0, 1.03, -0.090), (0, 0.97, -0.098), (0, 0.91, -0.085), (0, 0.885, -0.068)], [(0, .012), (1, .006)], [(0, .046), (.6, .030), (1, .008)], n=16, ref=(0, 0, 1), segs=10)
    pel.append(sac)
    out.append((concat(pel), 'Hips'))
    # jambes
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        f = lambda p: np.array([p[0] * sx, p[1], p[2]])
        ac = np.array([sx * 0.088, 0.952, 0.012])
        kn = f(H.LEG['LowerLeg']); an = f(H.LEG['Foot']); toe = f(H.LEG['Toes'])
        tro = np.array([sx * 0.135, 0.935, 0.000])
        fem = [blob(ac, (0.022, 0.022, 0.022)), tube_var([ac, 0.5 * (ac + tro) + np.array([0, -0.010, 0]), tro], [(0, .012), (1, .016)], [(0, .012), (1, .016)], n=10, segs=10),
               blob(tro, (0.014, 0.022, 0.016)),
               long_bone(tro + np.array([-0.012 * sx, -0.01, 0.004]), kn + np.array([0, 0.012, -0.004]), 0.0135, 0.018, 0.022, n=30, knob1=(0.040, 0.020, 0.030))]
        out.append((concat(fem), f'{side}UpperLeg'))
        out.append((blob(kn + np.array([0, 0.012, 0.042]), (0.019, 0.022, 0.009)), f'{side}LowerLeg'))   # rotule
        tib = long_bone(kn + np.array([0, -0.012, 0.004]), an + np.array([-0.006 * sx, 0.030, 0.012]), 0.012, 0.030, 0.017, n=30, knob0=(0.038, 0.012, 0.030), knob1=(0.018, 0.016, 0.016))
        fib = long_bone(kn + np.array([0.030 * sx, -0.035, -0.012]), an + np.array([0.026 * sx, 0.020, -0.004]), 0.0055, 0.009, 0.011, n=24, knob1=(0.010, 0.016, 0.010))
        out.append((concat([tib, fib]), f'{side}LowerLeg'))
        # pied : astragale, calcanéum, métatarses ; orteils
        ft = [blob(an + np.array([0, -0.012, 0.004]), (0.020, 0.016, 0.024)), blob(an + np.array([0, -0.050, -0.040]), (0.017, 0.020, 0.030)),
              blob(an + np.array([0.004 * sx, -0.042, 0.040]), (0.024, 0.014, 0.020))]
        mt = []
        for k, dx in enumerate((-0.026, -0.013, 0.0, 0.012, 0.024)):
            tipm = toe + np.array([sx * dx, -0.002, -0.004 + (0.004 if k == 0 else -0.004 * k)])
            start = an + np.array([sx * dx * 0.5, -0.040, 0.055])
            ft.append(long_bone(start, tipm, 0.0045 if k else 0.0065, 0.0065, 0.0065 if k else 0.008, n=12, segs=8, knob1=(0.006, 0.006, 0.006)))
            ln = 0.040 if k == 0 else 0.032 - 0.003 * k
            mt.append(long_bone(tipm, tipm + np.array([sx * dx * 0.1, -0.006, ln]), 0.0038 if k else 0.0055, 0.0048, 0.0042, n=10, segs=8))
        out.append((concat(ft), f'{side}Foot')); out.append((concat(mt), f'{side}Toes'))
    for m, b in out:
        if m is not None: rigid(m, sk[b])
    return out
