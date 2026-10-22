"""Gabarits d'armure ajustés au vrai corps : les lofts du chevalier (tronc, cou, crâne, bras, jambe, pied) gardent leur
chemin et leur paramétrage — toutes les pièces d'armure existantes restent valables — mais leurs rayons sont mesurés,
anneau par anneau, sur le corps MakeHuman (homme ou femme). Une superellipse englobe exactement chaque coupe : les plates
posées « à distance d du gabarit » sont donc à distance d de la peau réelle, sans la traverser.

Utilisation :  with fitted(body, sk): armor.build(...)   (remplace temporairement knight.body.*_loft)
"""
import contextlib
import numpy as np
from scipy.ndimage import gaussian_filter1d
from .geo import Loft, frames


def _fit(body, mask, loft, margin=0.002, smooth=1.2, slab=None):
    """Mesure rf/rs/of/os_ d'un loft existant sur les sommets mask du corps."""
    V = body.V[mask]
    P = loft.path; n = len(P)
    T, A, S = frames(P, getattr(loft, '_ref', (0, 0, 1)))
    seg = np.r_[np.linalg.norm(np.diff(P, axis=0), axis=1), 0]
    rf, rs, of, os_ = loft.rf.copy(), loft.rs.copy(), loft.of.copy(), loft.os.copy()
    p = float(np.mean(loft.p))
    found = np.zeros(n, bool)
    for i in range(n):
        d = V - P[i]
        h = slab if slab is not None else max(seg[i], seg[i - 1] if i else seg[i], 0.006) * 0.75
        sel = np.abs(d @ T[i]) < h
        if sel.sum() < 6: continue
        a = d[sel] @ A[i]; s = d[sel] @ S[i]
        fmax, fmin, smax, smin = a.max(), a.min(), s.max(), s.min()
        of[i] = (fmax + fmin) / 2; os_[i] = (smax + smin) / 2
        rf[i] = (fmax - fmin) / 2; rs[i] = (smax - smin) / 2
        # la superellipse doit contenir toute la coupe (coins compris)
        q = (np.abs(a - of[i]) / max(rf[i], 1e-4)) ** p + (np.abs(s - os_[i]) / max(rs[i], 1e-4)) ** p
        k = max(1.0, q.max()) ** (1 / p)
        rf[i] = rf[i] * k + margin; rs[i] = rs[i] * k + margin
        found[i] = True
    if found.sum() >= 2:
        idx = np.arange(n)
        for arr in (rf, rs, of, os_):
            arr[~found] = np.interp(idx[~found], idx[found], arr[found])
        for arr in (of, os_): arr[:] = gaussian_filter1d(arr, smooth, mode='nearest')
        for arr in (rf, rs):
            sm = gaussian_filter1d(arr, smooth, mode='nearest'); arr[:] = np.maximum(sm, arr * 0.985)
    return rf, rs, of, os_


def _refit(make, body, mask, ref=(0, 0, 1), **kw):
    lo = make()
    ref = lo._ref
    rf, rs, of, os_ = _fit(body, mask, lo, **kw)
    rf = np.clip(rf, lo.rf * 0.75, lo.rf * 1.35); rs = np.clip(rs, lo.rs * 0.75, lo.rs * 1.35)   # garde-fous (aisselles, entrejambe)
    out = Loft(lo.path, rf, rs, p=lo.p, of=of, os_=os_, segs=lo.segs, ref=ref,
               cap0=getattr(lo, '_cap0', 0), cap1=getattr(lo, '_cap1', 0))
    return out


def _hull_section(body, mask, loft, margin=0.004, nbin=96, vsmooth=1.0):
    """Section « moulée » : par anneau, enveloppe convexe de la coupe réelle du corps (poitrine galbée, taille cintrée,
    sans creux entre les seins), échantillonnée en rayon polaire. Renvoie une fonction section(i, ang, rf, rs) pour Loft."""
    from scipy.spatial import ConvexHull
    V = body.V[mask]
    P = loft.path; n = len(P)
    T, A, S = frames(P, getattr(loft, '_ref', (0, 0, 1)))
    seg = np.r_[np.linalg.norm(np.diff(P, axis=0), axis=1), 0]
    th = np.linspace(0, 2 * np.pi, nbin, endpoint=False)
    R = np.full((n, nbin), np.nan); C = np.zeros((n, 2))
    for i in range(n):
        d = V - P[i]
        h = max(seg[i], seg[i - 1] if i else seg[i], 0.006) * 0.75
        sel = np.abs(d @ T[i]) < h
        if sel.sum() < 12: continue
        xy = np.c_[d[sel] @ A[i], d[sel] @ S[i]]
        try: hull = xy[ConvexHull(xy).vertices]
        except Exception: continue
        c = hull.mean(0); C[i] = c
        # rayon de l'enveloppe dans chaque direction (intersection rayon / polygone convexe)
        hp = hull - c; hq = np.roll(hp, -1, 0)
        for k, t in enumerate(th):
            u = np.array([np.cos(t), np.sin(t)]); best = 0.0
            e = hq - hp
            den = u[0] * e[:, 1] - u[1] * e[:, 0]
            ok = np.abs(den) > 1e-12
            s = np.where(ok, (hp[:, 0] * e[:, 1] - hp[:, 1] * e[:, 0]) / np.where(ok, den, 1), -1)
            w = np.where(ok, (hp[:, 0] * u[1] - hp[:, 1] * u[0]) / np.where(ok, den, 1), -1)
            hit = ok & (s > 0) & (w >= -1e-9) & (w <= 1 + 1e-9)
            if hit.any(): best = s[hit].max()
            R[i, k] = best
    found = ~np.isnan(R[:, 0])
    idx = np.arange(n)
    for k in range(nbin): R[~found, k] = np.interp(idx[~found], idx[found], R[found, k])
    for j in range(2): C[~found, j] = np.interp(idx[~found], idx[found], C[found, j])
    R = gaussian_filter1d(R, 1.2, axis=1, mode='wrap')
    R = np.maximum(gaussian_filter1d(R, vsmooth, axis=0, mode='nearest'), R * 0.99) + margin
    C = gaussian_filter1d(C, 1.2, axis=0, mode='nearest')

    def section(i, ang, rf, rs):
        r = np.interp(np.mod(ang, 2 * np.pi), np.r_[th, 2 * np.pi], np.r_[R[i], R[i, 0]])
        return C[i, 0] + r * np.cos(ang), C[i, 1] + r * np.sin(ang)
    return section, R, C


def _conform(make, body, mask, margin=0.004):
    lo = make()
    sec, R, C = _hull_section(body, mask, lo, margin=margin)
    return Loft(lo.path, R.max(1) * 0 + lo.rf, lo.rs, p=lo.p, segs=lo.segs, ref=lo._ref, cap0=getattr(lo, '_cap0', 0), cap1=getattr(lo, '_cap1', 0), section=sec)


@contextlib.contextmanager
def fitted(body, sk, female=False, margin=0.002):
    """Remplace les gabarits du chevalier par des gabarits mesurés sur body, le temps du bloc with."""
    from knight import body as BD
    from .humanparts import bone_mask
    V = body.V
    orig = {k: getattr(BD, k) for k in ('torso_loft', 'neck_loft', 'head_loft', 'arm_loft', 'leg_loft', 'foot_loft')}
    arms = bone_mask(body, sk, ['UpperArm', 'LowerArm', 'Hand'], 0.25)
    legs = bone_mask(body, sk, ['UpperLeg', 'LowerLeg', 'Foot', 'Toes'], 0.35)
    ax = np.abs(V[:, 0])
    torso_m = (bone_mask(body, sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck'], 0.2) | (legs & (V[:, 1] > 0.80))) & ~arms & (V[:, 1] < 1.60) \
              & (ax < 0.215) & ~((V[:, 1] > 1.42) & (ax > 0.17 - 0.9 * np.clip(V[:, 1] - 1.42, 0, 1)))
    neck_m = bone_mask(body, sk, ['Neck', 'UpperChest', 'Head'], 0.2) & (V[:, 1] > 1.46) & (V[:, 1] < 1.70) & ~arms & (ax < 0.085)
    head_m = bone_mask(body, sk, ['Head'], 0.3) & (V[:, 1] > 1.55)
    larm = arms & (V[:, 0] > 0.10)
    lleg = bone_mask(body, sk, ['UpperLeg', 'LowerLeg', 'Foot'], 0.3) & (V[:, 0] > 0.0)
    lfoot = bone_mask(body, sk, ['Foot', 'Toes'], 0.3) & (V[:, 0] > 0.0)
    cache = {}
    def mk(name, fn, mask, ref=(0, 0, 1), **kw):
        def f(*a, **k):
            key = (name, a, tuple(sorted(k.items())))
            if key not in cache:
                cache[key] = _refit(lambda: orig[name](*a, **k), body, mask, ref=ref, margin=margin, **kw)
            return cache[key]
        return f
    if female:   # cuirasse moulée : galbe de la poitrine, taille cintrée, hanches
        def torso_f(*a, **k):
            key = ('torso_conform', a, tuple(sorted(k.items())))
            if key not in cache: cache[key] = _conform(lambda: orig['torso_loft'](*a, **k), body, torso_m, margin=margin + 0.002)
            return cache[key]
        BD.torso_loft = torso_f
    else:
        BD.torso_loft = mk('torso_loft', None, torso_m)
    BD.neck_loft = mk('neck_loft', None, neck_m)
    BD.head_loft = mk('head_loft', None, head_m)
    BD.arm_loft = mk('arm_loft', None, larm)
    BD.leg_loft = mk('leg_loft', None, lleg)
    BD.foot_loft = mk('foot_loft', None, lfoot, ref=(0, 1, 0))
    try:
        yield
    finally:
        for k, f in orig.items(): setattr(BD, k, f)
