"""Vêtements et accessoires souples : jupes et robes, capuche, bottes, brassards, ceinture et sacoches, lacets.
Tout est construit sur les volumes du corps (lofts) et pondéré sur le squelette humanoïde."""
import numpy as np
from .geo import Loft, Mesh, spline, curve, plate, plate_edge, tube, rivet, concat, rigid, blend_bones, smoothstep, unit, vertex_normals, grid_faces


def skin_lower(m, sk, hip_y=0.98, depth=0.55, amount=0.75):
    """Jupe / pans : bassin en haut, cuisses (gauche/droite selon X) vers le bas."""
    y = m.V[:, 1]; x = m.V[:, 0]
    f = smoothstep((hip_y - y) / depth) * amount
    wl = smoothstep((x + 0.05) / 0.10)
    m.J = np.zeros((len(y), 4), np.int64); m.W = np.zeros((len(y), 4))
    m.J[:, 0] = sk['Hips']; m.W[:, 0] = 1 - f
    m.J[:, 1] = sk['LeftUpperLeg']; m.W[:, 1] = f * wl
    m.J[:, 2] = sk['RightUpperLeg']; m.W[:, 2] = f * (1 - wl)
    return m


def skirt_loft(top=1.02, bottom=0.10, r_top=(0.118, 0.165), r_bot=(0.30, 0.36), bulge=0.0, n=40, segs=96):
    path = spline([(0, top, 0.0), (0, (top + bottom) / 2, -0.01), (0, bottom, -0.03)], n)
    t = np.linspace(0, 1, n)
    k = t ** 0.9
    rf = r_top[0] + (r_bot[0] - r_top[0]) * k + bulge * np.sin(np.pi * t) * 0.3
    rs = r_top[1] + (r_bot[1] - r_top[1]) * k + bulge * np.sin(np.pi * t) * 0.3
    return Loft(path, rf, rs, p=2.2, segs=segs)


def skirt(sk, asset, mat, loft, panels, off=0.004, thick=0.003, folds=0.012, nfolds=14, tatter=0.0, seed=1, name='jupe', hem=None):
    """panels : [(a0, a1)] portions d'angle (les intervalles libres forment des fentes). tatter : ourlet déchiqueté."""
    rng = np.random.default_rng(seed)
    for k, (a0, a1) in enumerate(panels):
        jag = np.interp(np.linspace(0, 1, 200), np.linspace(0, 1, 31), rng.uniform(0, 1, 31))
        hi = (lambda a, a0=a0, a1=a1, jag=jag: 1.0 - tatter * np.interp((a - a0) / (a1 - a0), np.linspace(0, 1, 200), jag)) if tatter else 1.0
        fo = lambda da, tr, qa, qt, a0=a0, a1=a1: off + folds * np.clip(tr, 0, 1) ** 1.2 * (0.5 + 0.5 * np.sin(2 * np.pi * nfolds * (a0 + da) + 0.7 * np.sin(5 * (a0 + da))))
        m = plate(loft, a0, a1, 0.0, hi, off=fo, thick=thick, bevel=0.004, na=int(140 * (a1 - a0)) + 8, nt=40, tile=0.35)
        skin_lower(m, sk); asset.add(f'{name}{k}', m, mat)
        if hem:
            he = plate(loft, a0, a1, (lambda a, hi=hi: (hi(a) if callable(hi) else hi) - 0.04), hi, off=lambda da, tr, qa, qt, fo=fo: fo(da, np.ones_like(tr), qa, qt) + thick * 0.6,
                       thick=0.0015, bevel=0.002, na=int(140 * (a1 - a0)) + 8, nt=4, tile=0.08)
            skin_lower(he, sk); asset.add(f'{name}{k}_ourlet', he, hem)
    return asset


def hood(sk, asset, mat, head_loft, opening=0.16, depth=0.035, peak=0.05, drape=True, lining=None, cone=0.0):
    """Capuche : enveloppe la tête en laissant le visage dégagé (a ∈ ±opening), pointe en arrière, retombe sur les épaules.
    cone > 0 : tout le sommet monte en pointe (capuche de rôdeur), la part « cone » du relief étant commune à tous les angles."""
    off = lambda da, tr, qa, qt: depth + 0.012 * np.sin(np.pi * qa) + peak * smoothstep((tr - 0.75) / 0.25) * (cone + (1 - cone) * np.clip(1 - np.abs(qa - 0.5) * 3, 0, 1))
    m = plate(head_loft, opening, 1 - opening, 0.0, 1.0, off=off, thick=0.006, bevel=0.01, na=70, nt=40, tile=0.3)
    y = m.V[:, 1]; f = smoothstep((1.60 - y) / 0.06)
    blend_bones(m, sk['Head'], sk['Neck'], f); asset.add('capuche', m, mat)
    # bord avant épais (repli du tissu)
    e = plate_edge(head_loft, opening + 0.005, 1 - opening - 0.005, lambda a: 0.06 + 0.9 * np.abs(np.sin(np.pi * a)) * 0.0 + 0.85 * 0, depth + 0.006, r=0.008, n=20)
    if lining:
        inner = plate(head_loft, opening, 1 - opening, 0.0, 1.0, off=lambda *a: depth * 0.6, thick=0.001, bevel=0.002, na=50, nt=28, tile=0.3)
        rigid(inner, sk['Head']); asset.add('capuche_doublure', inner, lining)
    if drape:   # pèlerine : col qui couvre les épaules
        path = spline([(0, 1.60, -0.02), (0, 1.52, -0.025), (0, 1.40, -0.03)], 20)
        lo = Loft(path, curve([(0, .085), (1, .17)], 20), curve([(0, .09), (1, .26)], 20), p=2.3, segs=96)
        jag = lambda a: 1.0 - 0.10 * np.abs(np.sin(a * np.pi * 9))
        m = plate(lo, -0.5, 0.5, 0.0, jag, off=0.012, thick=0.005, bevel=0.008, na=120, nt=16, tile=0.3)
        y = m.V[:, 1]; f = smoothstep((1.56 - y) / 0.08)
        blend_bones(m, sk['Neck'], sk['UpperChest'], f); asset.add('pelerine', m, mat)
    return asset


def boots(sk, asset, mat, leg, foot, top=0.62, cuff=None, trim=None, side='Left', both=True, mirror_fn=None):
    m = plate(leg, -0.5, 0.5, top, 1.0, off=0.006, thick=0.004, bevel=0.006, na=60, nt=30, tile=0.3)
    f = smoothstep((m.V[:, 1] - 0.50) / 0.06)
    blend_bones(m, sk['LeftLowerLeg'], sk['LeftUpperLeg'], f * (top < 0.5)); out = [('botte', m, mat)]
    fm = plate(foot, -0.5, 0.5, 0.0, 1.0, off=0.005, thick=0.004, bevel=0.005, na=40, nt=30, tile=0.25)
    z = fm.V[:, 2]; blend_bones(fm, sk['LeftFoot'], sk['LeftToes'], smoothstep((z - 0.06) / 0.05)); out.append(('botte_pied', fm, mat))
    if cuff:
        c = plate(leg, -0.5, 0.5, top - 0.03, top + 0.05, off=0.012, thick=0.004, bevel=0.004, na=60, nt=6, tile=0.2)
        blend_bones(c, sk['LeftLowerLeg'], sk['LeftUpperLeg'], float(top < 0.5)); out.append(('botte_revers', c, cuff))
    if trim:
        c = plate_edge(leg, -0.5, 0.5, top + 0.005, 0.012, r=0.003, n=60, tile=0.05)
        blend_bones(c, sk['LeftLowerLeg'], sk['LeftUpperLeg'], float(top < 0.5)); out.append(('botte_bord', c, trim))
    return out


def bracer(sk, arm, mat, t0=0.60, t1=0.88, off=0.008, laces=None):
    m = plate(arm, 0.0, 1.0, t0, t1, off=off, thick=0.004, bevel=0.005, na=48, nt=20, tile=0.25)
    rigid(m, sk['LeftLowerArm']); out = [('brassard', m, mat)]
    if laces:
        for k in range(5):
            t = t0 + 0.03 + (t1 - t0 - 0.06) * k / 4
            P0, N0, _, _ = arm.frame(np.array(t), np.array(0.22)); P1, N1, _, _ = arm.frame(np.array(t + 0.02), np.array(0.28))
            l = tube([P0 + N0 * (off + 0.005), P1 + N1 * (off + 0.005)], 0.0016, segs=5); rigid(l, sk['LeftLowerArm']); out.append((f'lacet{k}', l, laces))
    return out


def belt(sk, torso, t_of_y, mat, buckle=None, y=0.99, width=0.045, off=0.026, pouches=(), pouch_mat=None):
    out = []
    m = plate(torso, -0.5, 0.5, t_of_y(y - width / 2), t_of_y(y + width / 2), off=off, thick=0.004, bevel=0.003, na=100, nt=4, tile=0.2)
    out.append(('ceinture', m, mat))
    if buckle:
        P, N, _, _ = torso.frame(np.array(t_of_y(y)), np.array(0.0))
        out.append(('boucle', rivet(P + N * (off + 0.004), N, r=0.022, h=0.008, segs=4, rings=2), buckle))
    for k, (a, w, h, d) in enumerate(pouches):
        P, N, _, _ = torso.frame(np.array(t_of_y(y - 0.03)), np.array(a))
        c = P + N * (off + d * 0.5 + 0.004) - np.array([0, h * 0.35, 0])
        lo = Loft(spline([c + (0, h / 2, 0), c - (0, h / 2, 0)], 6), d / 2, w / 2, p=4.0, segs=16, ref=tuple(N), cap0=2, cap1=2)
        out.append((f'sacoche{k}', lo.mesh(0.1), pouch_mat or mat))
        flap = Loft(spline([c + (0, h / 2 + 0.004, 0), c + (0, h * 0.1, 0) + N * (d / 2 + 0.002)], 5), 0.002, w / 2 + 0.002, p=4.0, segs=12, ref=tuple(N))
        out.append((f'rabat{k}', flap.mesh(0.1), mat))
    return out
