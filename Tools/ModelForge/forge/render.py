"""Rendu logiciel de contrôle (numpy) : skinning, pose d'animation, textures PBR (couleur, normales, métal/lissé, occlusion),
lumière principale + contre-jour + ambiance, ombre portée au sol. Sert à vérifier les modèles sans moteur ni GPU."""
import numpy as np
from PIL import Image, ImageDraw
from .geo import vertex_normals


def _sample(img, uv):
    h, w = img.shape[:2]
    u = (uv[:, 0] % 1.0) * w - 0.5; v = ((1.0 - uv[:, 1]) % 1.0) * h - 0.5     # UV origine en bas à gauche (FBX / Unity)
    x0 = np.floor(u).astype(int); y0 = np.floor(v).astype(int); fx = (u - x0)[:, None]; fy = (v - y0)[:, None]
    x0 %= w; y0 %= h; x1 = (x0 + 1) % w; y1 = (y0 + 1) % h
    img = img if img.ndim == 3 else img[..., None]
    return img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x1] * fx * (1 - fy) + img[y1, x0] * (1 - fx) * fy + img[y1, x1] * fx * fy


def posed_meshes(asset, clip=None, frame=0, attachments=()):
    """Liste de (V, N, UV, F, MaterialDef) en pose. attachments : [(asset_arme, nom_socket)]."""
    sk = asset.skeleton
    out = []
    groups = asset.by_material()
    if sk is not None:
        lr = {} if clip is None else {b: q[min(frame, clip.n - 1)] for b, q in clip.rot.items()}
        off = None if clip is None or 'Hips' not in clip.pos else clip.pos['Hips'][min(frame, clip.n - 1)]
        G = sk.fk(lr, off); R = sk.rest_global()
        Mk = np.stack([G[i] @ np.linalg.inv(R[i]) for i in range(len(G))])
    for mn, m in groups.items():
        N0 = m.N if m.N is not None else vertex_normals(m)
        if sk is not None:
            M = np.einsum('vk,vkij->vij', m.W, Mk[m.J])
            V = np.einsum('vij,vj->vi', M[:, :3, :3], m.V) + M[:, :3, 3]
            N = np.einsum('vij,vj->vi', M[:, :3, :3], N0)
        else:
            V, N = m.V.copy(), N0.copy()
        N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-12
        out.append((V, N, m.UV, m.F, asset.materials[mn]))
    for att, sock in attachments:
        Ms = G[sk.names.index(sock)]
        for V, N, UV, F, md in posed_meshes(att):
            out.append((V @ Ms[:3, :3].T + Ms[:3, 3], N @ Ms[:3, :3].T, UV, F, md))
    return out


def render(prims, size=(720, 900), yaw=25.0, pitch=8.0, target=(0, 0.95, 0), dist=4.4, fov=26.0, ss=2,
           bg=(0.105, 0.10, 0.115), ground=True, light=(-0.45, 0.80, 0.50)):
    W, H = size[0] * ss, size[1] * ss
    target = np.asarray(target, float)
    yr, pr = np.radians(yaw), np.radians(pitch)
    eye = target + dist * np.array([np.sin(yr) * np.cos(pr), np.sin(pr), np.cos(yr) * np.cos(pr)])
    fwd = target - eye; fwd /= np.linalg.norm(fwd)
    right = np.cross(fwd, [0, 1, 0]); right /= np.linalg.norm(right); up = np.cross(right, fwd)
    f = 1 / np.tan(np.radians(fov / 2))
    L = np.asarray(light, float); L /= np.linalg.norm(L)

    def proj(V):
        d = V - eye; x = d @ right; y = d @ up; z = d @ fwd
        return np.stack([x / z * f * H / 2 + W / 2, -y / z * f * H / 2 + H / 2, z], 1)

    zb = np.full((H, W), np.inf); tid = np.full((H, W), -1, np.int64); bary = np.zeros((H, W, 2), np.float32)
    tri_p, tri_l = [], []; base = 0
    # ombre portée : carte de profondeur vue de la lumière (orthographique)
    allV = np.vstack([p[0] for p in prims])
    lx = np.cross(L, [0, 0, 1.0]); lx /= np.linalg.norm(lx); ly = np.cross(lx, L)
    SM = 1024; ext = np.abs(allV - allV.mean(0)).max() * 1.4 + 0.1; c0 = allV.mean(0)
    def lproj(V):
        d = V - c0
        return np.stack([(d @ lx) / ext * SM / 2 + SM / 2, (d @ ly) / ext * SM / 2 + SM / 2, -(d @ L)], 1)
    smap = np.full((SM, SM), np.inf)
    for V, N, UV, F, md in prims:
        if getattr(md, 'cutoff', None) is not None: continue   # mèches : pas d'ombre grossière dans l'aperçu
        S = lproj(V)[F]
        for k in range(3):
            pass
        # éclaboussure de points (rapide) : sommets + centres de facettes
        rb = np.random.default_rng(0).dirichlet((1, 1, 1), size=(len(S), 10))
        e1_ = S[:, 1, :2] - S[:, 0, :2]; e2_ = S[:, 2, :2] - S[:, 0, :2]; area_px = np.abs(e1_[:, 0] * e2_[:, 1] - e1_[:, 1] * e2_[:, 0]) * 0.5
        extra = np.einsum('tkj,tjd->tkd', rb, S)[area_px > 1.5].reshape(-1, 3)
        pts = np.vstack([S.reshape(-1, 3), S.mean(1), (S[:, 0] + S[:, 1]) / 2, (S[:, 1] + S[:, 2]) / 2, (S[:, 0] + S[:, 2]) / 2, extra])
        xi = np.clip(pts[:, 0].astype(int), 0, SM - 1); yi = np.clip(pts[:, 1].astype(int), 0, SM - 1)
        np.minimum.at(smap, (yi, xi), pts[:, 2])
    from scipy.ndimage import minimum_filter
    smap = minimum_filter(smap, 3)

    for pi, (V, N, UV, F, md) in enumerate(prims):
        S = proj(V)
        A, B, C = S[F[:, 0]], S[F[:, 1]], S[F[:, 2]]
        area = (B[:, 0] - A[:, 0]) * (C[:, 1] - A[:, 1]) - (B[:, 1] - A[:, 1]) * (C[:, 0] - A[:, 0])
        xmn = np.floor(np.minimum(np.minimum(A[:, 0], B[:, 0]), C[:, 0])).astype(int); xmx = np.ceil(np.maximum(np.maximum(A[:, 0], B[:, 0]), C[:, 0])).astype(int)
        ymn = np.floor(np.minimum(np.minimum(A[:, 1], B[:, 1]), C[:, 1])).astype(int); ymx = np.ceil(np.maximum(np.maximum(A[:, 1], B[:, 1]), C[:, 1])).astype(int)
        ok = (np.abs(area) > 1e-9) & (xmx >= 0) & (ymx >= 0) & (xmn < W) & (ymn < H) & (np.minimum(np.minimum(A[:, 2], B[:, 2]), C[:, 2]) > 0.05)
        amap = md.images.get('alpha') if getattr(md, 'cutoff', None) is not None else None
        if not md.double_sided: ok &= area < 0
        for k in np.nonzero(ok)[0]:
            x0, x1 = max(xmn[k], 0), min(xmx[k], W - 1); y0, y1 = max(ymn[k], 0), min(ymx[k], H - 1)
            X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            a, b, c, ar = A[k], B[k], C[k], area[k]
            w0 = ((b[0] - X) * (c[1] - Y) - (b[1] - Y) * (c[0] - X)) / ar
            w1 = ((c[0] - X) * (a[1] - Y) - (c[1] - Y) * (a[0] - X)) / ar
            w2 = 1 - w0 - w1
            ins = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not ins.any(): continue
            z = 1 / (w0 / a[2] + w1 / b[2] + w2 / c[2])
            sub = zb[y0:y1 + 1, x0:x1 + 1]; m = ins & (z < sub)
            if amap is not None and m.any():
                pw0 = (w0 / a[2]) * z; pw1 = (w1 / b[2]) * z; pw2 = 1 - pw0 - pw1
                uvp = (UV[F[k, 0]][None, None] * pw0[..., None] + UV[F[k, 1]][None, None] * pw1[..., None] + UV[F[k, 2]][None, None] * pw2[..., None]) * np.array(md.tiling)
                al = _sample(amap, uvp.reshape(-1, 2)).reshape(m.shape)
                m &= al >= md.cutoff
            if not m.any(): continue
            sub[m] = z[m]; tid[y0:y1 + 1, x0:x1 + 1][m] = base + k
            bb = bary[y0:y1 + 1, x0:x1 + 1]
            bb[..., 0][m] = (w0 / a[2] * z)[m]; bb[..., 1][m] = (w1 / b[2] * z)[m]
        tri_p.append(np.full(len(F), pi)); tri_l.append(np.arange(len(F))); base += len(F)
    tri_p = np.concatenate(tri_p); tri_l = np.concatenate(tri_l)

    img = np.zeros((H, W, 3), np.float32)
    gy = np.linspace(0, 1, H)[:, None, None]
    img[:] = np.array(bg) * (1.3 - 0.55 * gy)
    # sol
    if ground:
        ys, xs = np.nonzero(tid < 0)
        dirs = (xs[:, None] - W / 2) / (f * H / 2) * right + (-(ys[:, None] - H / 2) / (f * H / 2)) * up + fwd
        tgr = -eye[1] / np.where(np.abs(dirs[:, 1]) < 1e-9, -1e-9, dirs[:, 1])
        hit = tgr > 0
        P = eye + dirs * tgr[:, None]
        rr = np.linalg.norm(P[:, [0, 2]], axis=1)
        sh = _shadow(P, lproj, smap, 0.01)
        g = np.array([0.15, 0.14, 0.13]) * (0.35 + 0.65 * sh[:, None]) * np.exp(-rr * 0.35)[:, None] * 1.6
        img[ys[hit], xs[hit]] = img[ys[hit], xs[hit]] * 0.35 + g[hit] * 0.65
    Lc = np.array([1.0, 0.94, 0.86]) * 3.2; L2 = np.array([0.65, 0.25, -0.70]); L2 /= np.linalg.norm(L2); L2c = np.array([0.55, 0.65, 1.0]) * 1.5
    for pi, (V, N, UV, F, md) in enumerate(prims):
        sel = (tid >= 0) & (tri_p[np.maximum(tid, 0)] == pi)
        if not sel.any(): continue
        ys, xs = np.nonzero(sel); t = tri_l[tid[ys, xs]]; Ft = F[t]
        b0 = bary[ys, xs, 0][:, None].astype(float); b1 = bary[ys, xs, 1][:, None].astype(float); b2 = 1 - b0 - b1
        Nn = N[Ft[:, 0]] * b0 + N[Ft[:, 1]] * b1 + N[Ft[:, 2]] * b2; Nn /= np.linalg.norm(Nn, axis=1, keepdims=True) + 1e-12
        uv = UV[Ft[:, 0]] * b0 + UV[Ft[:, 1]] * b1 + UV[Ft[:, 2]] * b2
        P = V[Ft[:, 0]] * b0 + V[Ft[:, 1]] * b1 + V[Ft[:, 2]] * b2
        uvs = uv * np.array(md.tiling)
        im = md.images
        base_c = (_sample(im['base'], uvs) if 'base' in im else np.ones((len(t), 3))) * np.array(md.color[:3])
        metal = np.full((len(t), 1), md.metallic); smooth = np.full((len(t), 1), md.smoothness); ao = np.ones((len(t), 1))
        if 'mask' in im:
            mk = _sample(im['mask'], uvs); metal = mk[:, 0:1]; smooth = mk[:, 3:4] * md.smoothness
        if 'ao' in im: ao = _sample(im['ao'], uvs)[:, :1]
        Vd = eye - P; Vd /= np.linalg.norm(Vd, axis=1, keepdims=True)
        if md.double_sided:
            fl = (Nn * Vd).sum(1) < 0; Nn[fl] *= -1
        if 'normal' in im:
            P0, P1, P2 = V[Ft[:, 0]], V[Ft[:, 1]], V[Ft[:, 2]]; U0, U1, U2 = UV[Ft[:, 0]], UV[Ft[:, 1]], UV[Ft[:, 2]]
            e1, e2 = P1 - P0, P2 - P0; d1, d2 = U1 - U0, U2 - U0
            r = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]; r = np.where(np.abs(r) < 1e-14, 1e-14, r)
            Tg = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / r[:, None]; Bg = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / r[:, None]
            Tg -= Nn * (Tg * Nn).sum(1, keepdims=True); Tg /= np.linalg.norm(Tg, axis=1, keepdims=True) + 1e-12
            hand = np.sign((np.cross(Nn, Tg) * Bg).sum(1, keepdims=True)); hand[hand == 0] = 1
            Bt = np.cross(Nn, Tg) * hand
            nt = _sample(im['normal'], uvs) * 2 - 1; nt[:, :2] *= md.normal_scale
            Nn = Tg * nt[:, 0:1] + Bt * nt[:, 1:2] + Nn * nt[:, 2:3]; Nn /= np.linalg.norm(Nn, axis=1, keepdims=True) + 1e-12
        rough = np.clip(1 - smooth, 0.05, 1); metal = np.clip(metal, 0, 1)
        F0 = 0.04 * (1 - metal) + base_c * metal; diff = base_c * (1 - metal)
        sh = _shadow(P + Nn * 0.008, lproj, smap, 0.02)[:, None]
        col = np.zeros_like(base_c)
        for Ld, Lcol, s in ((L, Lc, sh), (L2, L2c, 1.0)):
            ndl = np.clip(Nn @ Ld, 0, 1)[:, None]
            Hh = Ld + Vd; Hh /= np.linalg.norm(Hh, axis=1, keepdims=True)
            ndh = np.clip((Nn * Hh).sum(1), 0, 1)[:, None]; ndv = np.clip((Nn * Vd).sum(1), 1e-3, 1)[:, None]
            a2 = rough ** 4; D = a2 / (np.pi * (ndh ** 2 * (a2 - 1) + 1) ** 2)
            kk = (rough + 1) ** 2 / 8; Gs = ndl / (ndl * (1 - kk) + kk) * ndv / (ndv * (1 - kk) + kk)
            vdh = np.clip((Vd * Hh).sum(1), 0, 1)[:, None]; Fr = F0 + (1 - F0) * (1 - vdh) ** 5
            col += (diff / np.pi * (1 - Fr) + D * Gs * Fr / (4 * ndl * ndv + 1e-4)) * Lcol * ndl * s
        sky = 0.5 + 0.5 * Nn[:, 1:2]
        amb = np.array([0.30, 0.32, 0.38]) * sky + np.array([0.11, 0.09, 0.08]) * (1 - sky)
        R = 2 * (Nn * Vd).sum(1, keepdims=True) * Nn - Vd
        env = (np.array([0.62, 0.64, 0.70]) * np.clip(R[:, 1:2] * 0.6 + 0.45, 0, 1) ** 1.5 + 0.05 + 0.25 * np.clip(-R[:, 2:3], 0, 1) * np.array([0.9, 0.7, 0.5]))
        env = env * (1 - rough) ** 1.5 + np.array([0.20, 0.21, 0.24]) * rough
        ndv = np.clip((Nn * Vd).sum(1), 0, 1)[:, None]
        Fe = F0 + (np.maximum(1 - rough, F0) - F0) * (1 - ndv) ** 5
        col += (diff * amb + Fe * env) * ao
        if md.emission is not None: col += np.array(md.emission) * (_sample(im['emit'], uvs) if 'emit' in im else 1)
        img[ys, xs] = col
    x = img
    img = np.clip((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0, 1) ** (1 / 2.2)
    out = Image.fromarray((img * 255).astype(np.uint8))
    return out.resize(size, Image.LANCZOS) if ss > 1 else out


def _shadow(P, lproj, smap, bias):
    S = lproj(P); SM = smap.shape[0]
    xi = np.clip(S[:, 0].astype(int), 0, SM - 1); yi = np.clip(S[:, 1].astype(int), 0, SM - 1)
    occ = smap[yi, xi] < S[:, 2] - bias
    return np.where(occ, 0.0, 1.0)


def sheet(images, cols, labels=None):
    w, h = images[0].size; rows = (len(images) + cols - 1) // cols
    out = Image.new('RGB', (w * cols, h * rows), (20, 20, 22)); d = ImageDraw.Draw(out)
    for i, im in enumerate(images):
        out.paste(im, ((i % cols) * w, (i // cols) * h))
        if labels: d.text(((i % cols) * w + 8, (i // cols) * h + 6), labels[i], fill=(235, 225, 205))
    return out
