"""Animation par poses clés : angles d'Euler en degrés par os (relatifs au repos), interpolation Hermite monotone,
échantillonnage à fréquence fixe → Clip (quaternions locaux), cape simulée (chaînes pendulaires) à partir du mouvement du corps."""
import numpy as np
from .rig import Clip, qeuler, qmul, qmat, qaxis


def key(t, pose=None, hips=None, hips_rot=None):
    """t : secondes ; pose : {os: (rx, ry, rz)} ; hips : décalage du bassin (m) ; hips_rot : rotation du bassin (deg)."""
    return dict(t=t, p=pose or {}, h=np.zeros(3) if hips is None else np.asarray(hips, float),
                hr=np.zeros(3) if hips_rot is None else np.asarray(hips_rot, float))


def _hermite(ts, vals, t):
    n = len(ts)
    if t <= ts[0]: return vals[0]
    if t >= ts[-1]: return vals[-1]
    i = np.searchsorted(ts, t) - 1; i = min(max(i, 0), n - 2)
    h = ts[i + 1] - ts[i]; u = (t - ts[i]) / h
    def tan(j):
        if j <= 0 or j >= n - 1: return np.zeros_like(vals[0])
        d0 = (vals[j] - vals[j - 1]) / (ts[j] - ts[j - 1]); d1 = (vals[j + 1] - vals[j]) / (ts[j + 1] - ts[j])
        out = np.where(d0 * d1 <= 0, 0.0, 2 * d0 * d1 / np.where(d0 + d1 == 0, 1, d0 + d1))
        return out
    m0, m1 = tan(i) * h, tan(i + 1) * h
    u2, u3 = u * u, u * u * u
    return (2 * u3 - 3 * u2 + 1) * vals[i] + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * vals[i + 1] + (u3 - u2) * m1


def _hermite_loop(ts, vals, t, period):
    # boucle : clés répétées avant/après pour des tangentes continues au raccord
    T = np.concatenate([ts[:-1] - period, ts, ts[1:] + period])
    V = vals[:-1] + vals + vals[1:]
    return _hermite(T, V, t)


def qlog(q):
    q = np.asarray(q, float); q = q if q[3] >= 0 else -q
    v = q[:3]; s = np.linalg.norm(v)
    if s < 1e-9: return np.zeros(3)
    return v / s * 2 * np.arctan2(s, q[3])


def qexp(r):
    a = np.linalg.norm(r)
    if a < 1e-9: return np.array([0, 0, 0, 1.0])
    return np.concatenate([r / a * np.sin(a / 2), [np.cos(a / 2)]])


def build_qclip(name, keys, bones, fps=30, loop=False):
    """Poses clés en QUATERNIONS (key['p'][os] = quat) : interpolation Hermite monotone des vecteurs de rotation (log),
    rotation du bassin en degrés (key['hr'], ordre XYZ), translation du bassin (key['h'])."""
    keys = sorted(keys, key=lambda k: k['t'])
    dur = keys[-1]['t']; n = int(round(dur * fps)) + 1
    ts = np.array([k['t'] for k in keys]); tb = np.arange(n) / fps
    clip = Clip(name, fps, n, loop)
    I = np.array([0, 0, 0, 1.0])
    for b in bones:
        vals = [qlog(k['p'].get(b, I)) for k in keys]
        # continuité des vecteurs de rotation (évite les sauts de 2π)
        for i in range(1, len(vals)):
            a = np.linalg.norm(vals[i])
            if a > 1e-6:
                alt = vals[i] - vals[i] / a * 2 * np.pi
                if np.linalg.norm(alt - vals[i - 1]) < np.linalg.norm(vals[i] - vals[i - 1]): vals[i] = alt
        clip.rot[b] = np.array([qexp(_hermite_loop(ts, vals, t, dur) if loop else _hermite(ts, vals, t)) for t in tb])
    hv = [k['h'] for k in keys]; hr = [k['hr'] for k in keys]
    clip.pos['Hips'] = np.array([(_hermite_loop(ts, hv, t, dur) if loop else _hermite(ts, hv, t)) for t in tb])
    clip.rot['Hips'] = np.array([qeuler(*(_hermite_loop(ts, hr, t, dur) if loop else _hermite(ts, hr, t))) for t in tb])
    return clip


def build_clip(name, keys, bones, fps=30, loop=False, base=None, extra=None):
    """keys triées ; bones : liste des os animés ; base : pose ajoutée partout (ex. mains fermées) ; extra(t, pose) : retouche procédurale."""
    keys = sorted(keys, key=lambda k: k['t'])
    dur = keys[-1]['t']; n = int(round(dur * fps)) + 1
    ts = np.array([k['t'] for k in keys])
    clip = Clip(name, fps, n, loop)
    base = base or {}
    tb = np.arange(n) / fps
    ang = {b: np.zeros((n, 3)) for b in bones}
    hip = np.zeros((n, 3)); hipr = np.zeros((n, 3))
    for b in bones:
        vals = [np.asarray(k['p'].get(b, (0, 0, 0)), float) for k in keys]
        for i, t in enumerate(tb):
            ang[b][i] = (_hermite_loop(ts, vals, t, dur) if loop else _hermite(ts, vals, t)) + np.asarray(base.get(b, (0, 0, 0)), float)
    hv = [k['h'] for k in keys]; hr = [k['hr'] for k in keys]
    for i, t in enumerate(tb):
        hip[i] = _hermite_loop(ts, hv, t, dur) if loop else _hermite(ts, hv, t)
        hipr[i] = _hermite_loop(ts, hr, t, dur) if loop else _hermite(ts, hr, t)
    if extra is not None:
        for i, t in enumerate(tb): extra(t, i, ang, hip, hipr)
    for b in bones:
        clip.rot[b] = np.array([qeuler(*a) for a in ang[b]])
    clip.rot['Hips'] = np.array([qeuler(*a) for a in hipr])
    clip.pos['Hips'] = hip
    return clip


def simulate_cape(sk, clip, chains, gravity=9.81, damping=6.0, stiffness=14.0, wind=None, pre_roll=None):
    """Cape : chaque chaîne d'os est simulée comme un pendule amorti dont l'ancrage suit le buste animé.
    Les rotations obtenues sont écrites dans clip.rot (rotations locales des os de cape).
    stiffness : rappel vers la pose de repos (tissu épais) ; pre_roll : nombre de cycles simulés avant d'enregistrer (boucles)."""
    fps = clip.fps; n = clip.n; dt = 1.0 / fps
    cycles = (pre_roll if pre_roll is not None else (3 if clip.loop else 0))
    names = sk.names
    def pose_globals(i):
        lr = {b: clip.rot[b][i] for b in clip.rot}
        off = clip.pos.get('Hips', np.zeros((n, 3)))[i]
        return sk.fk(lr, off)
    rest = sk.rest_global()
    sub = 4
    results = {b: np.zeros((n, 4)) for ch in chains for b in ch}
    for ch in chains:
        idx = [names.index(b) for b in ch]
        par = sk.parents[idx[0]]
        # positions de repos (relatives au parent) des joints de la chaîne + bout
        pts_rest = [sk.pos[i] for i in idx]
        tip = pts_rest[-1] + (pts_rest[-1] - pts_rest[-2])
        pts_rest = np.array(pts_rest + [tip])
        Lseg = np.linalg.norm(np.diff(pts_rest, axis=0), axis=1)
        Gp0 = rest[par]
        local_rest = (np.linalg.inv(Gp0) @ np.c_[pts_rest, np.ones(len(pts_rest))].T).T[:, :3]
        P = None; Pprev = None
        total = n * (cycles + 1) if clip.loop else n + int(0.0 * fps)
        start_rec = total - n
        for step in range(total):
            i = step % n
            G = pose_globals(i)
            Gp = G[par]
            target = (Gp @ np.c_[local_rest, np.ones(len(local_rest))].T).T[:, :3]
            if P is None: P = target.copy(); Pprev = P.copy()
            for _ in range(sub):
                h = dt / sub
                vel = (P - Pprev) / h
                acc = np.zeros_like(P); acc[:, 1] -= gravity
                acc += stiffness * (target - P) * np.linspace(0.6, 1.0, len(P))[:, None] * 10
                acc -= damping * vel
                if wind is not None: acc += wind(step * dt)
                Pn = P + vel * h * 0.995 + acc * h * h
                Pn[0] = target[0]
                # contraintes de longueur + collision simple avec le corps (rester derrière le dos / les jambes)
                for _k in range(3):
                    for s in range(len(Pn) - 1):
                        d = Pn[s + 1] - Pn[s]; L = np.linalg.norm(d) + 1e-9
                        Pn[s + 1] = Pn[s] + d / L * Lseg[s]
                    hips = G[0][:3, 3]; fwd = G[0][:3, 2]
                    for s in range(1, len(Pn)):
                        rel = Pn[s] - hips; dz = rel @ fwd
                        lim = -0.17 - 0.02 * s
                        if dz > lim: Pn[s] -= fwd * (dz - lim)
                Pprev, P = P, Pn
            if step >= start_rec:
                # rotations : chaque os oriente son segment de repos vers le segment simulé
                accum = Gp[:3, :3].copy()
                for s, bi in enumerate(idx):
                    rest_dir = pts_rest[s + 1] - pts_rest[s]; rest_dir /= np.linalg.norm(rest_dir)
                    cur = P[s + 1] - P[s]; cur /= np.linalg.norm(cur)
                    local_rest_dir = rest_dir                        # repos : axes du monde (rotations nulles)
                    local_cur = accum.T @ cur
                    q = _from_to(local_rest_dir, local_cur)
                    results[ch[s]][i] = q
                    accum = accum @ qmat(q)
    for b, Q in results.items(): clip.rot[b] = Q
    return clip


def _from_to(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b)
    c = np.cross(a, b); d = np.dot(a, b)
    if d < -0.9999:
        ax = np.cross(a, [1, 0, 0]);
        if np.linalg.norm(ax) < 1e-6: ax = np.cross(a, [0, 1, 0])
        return qaxis(ax, np.pi)
    q = np.array([c[0], c[1], c[2], 1 + d]); return q / np.linalg.norm(q)
