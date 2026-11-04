"""Animations des créatures à pattes multiples importées (basilic, tisseuse, salamandre) : chaque patte pivote à sa
hanche (os Coxa) — balancier avant/arrière autour de l'axe latéral, levée en écartant le pied vers l'extérieur — avec
une amplitude calculée d'après la hauteur réelle de la hanche, pour que la foulée ait la bonne longueur. Démarche en
diagonales alternées, corps qui tangue, tête et queue qui suivent."""
import numpy as np
from forge.rig import qaxis, qmul
from forge.anim import key, build_qclip


def legs_of(sk):
    """[(i, côté, signe, hauteur de hanche)] des pattes présentes (os Coxa plus haut que le pied)."""
    out = []
    for s, sx in (('L', 1), ('R', -1)):
        for i in range(4):
            c, t = f'Leg{i}_Coxa_{s}', f'Leg{i}_Tarsus_{s}'
            if c not in sk.names: continue
            pc, pt = np.asarray(sk.pos[sk[c]]), np.asarray(sk.pos[sk[t]])
            if np.linalg.norm(pt - pc) < 0.12: continue           # membre absent (chaîne cachée)
            out.append((i, s, sx, max(0.15, pc[1] - min(pt[1], pc[1] - 0.1))))
    return out


def _leg(i, s, sx, swing, lift, curl=0.0):
    q = qmul(qaxis((1, 0, 0), np.radians(-swing)), qaxis((0, 0, 1), np.radians(lift * sx)))
    p = {f'Leg{i}_Coxa_{s}': q}
    if curl: p[f'Leg{i}_Tibia_{s}'] = qaxis((0, 0, 1), np.radians(-curl * sx)); p[f'Leg{i}_Meta_{s}'] = qaxis((0, 0, 1), np.radians(-curl * 0.7 * sx))
    return p


def _bones(sk): return [n for n in sk.names if n != 'Hips']


def gait(sk, name, T, stride, lift_h, bob, tail=8.0, head=3.0):
    L = legs_of(sk); ks = []
    for k in range(13):
        t = k * T / 12; w = 2 * np.pi * t / T; p = {}
        for i, s, sx, h in L:
            # chaque patte a son propre temps (onde qui remonte de l'arrière vers l'avant, côtés en opposition) :
            # jamais deux pattes d'un même côté levées ensemble
            ph = w + np.pi * (0 if sx > 0 else 1) + i * (np.pi / 2) * (1 if sx > 0 else -1) + 0.35 * i
            amp = np.degrees(np.arctan(0.5 * stride / h)); lift = np.degrees(np.arctan(lift_h / h))
            p.update(_leg(i, s, sx, amp * np.cos(ph), lift * max(0.0, np.sin(ph))))
        if 'Spinneret' in sk.names: p['Spinneret'] = qaxis((0, 1, 0), np.radians(tail * np.sin(w)))
        p['Abdomen'] = qaxis((0, 1, 0), np.radians(-0.4 * tail * np.sin(w)))
        p['Thorax'] = qaxis((1, 0, 0), np.radians(head * np.sin(2 * w)))
        ks.append(key(t, p, hips=(0, bob * abs(np.sin(w)), 0), hips_rot=(0, 3 * np.sin(w), 2 * np.sin(w))))
    return build_qclip(name, ks, _bones(sk), loop=True)


def idle(sk):
    L = legs_of(sk); ks = []
    for k in range(9):
        t = k * 0.5; w = 2 * np.pi * t / 4.0; p = {}
        for i, s, sx, h in L: p.update(_leg(i, s, sx, 1.5 * np.sin(w + 1.7 * i + (0 if sx > 0 else 2.3)), 1.0 + np.sin(w * 2 + 1.3 * i + sx)))
        if 'Spinneret' in sk.names: p['Spinneret'] = qaxis((0, 1, 0), np.radians(6 * np.sin(w)))
        p['Thorax'] = qaxis((1, 0, 0), np.radians(2.5 * np.sin(w * 2)))
        ks.append(key(t, p, hips=(0, -0.012 + 0.012 * np.sin(w * 2), 0)))
    return build_qclip('Idle', ks, _bones(sk), loop=True)


def attack(sk):
    L = legs_of(sk)
    def P(rear, strike):
        p = {}
        for i, s, sx, h in L:
            front = i < 2; d = 1.0 if sx > 0 else 0.8                 # gauche et droite légèrement décalées
            if front: p.update(_leg(i, s, sx, (35 * rear - 20 * strike) * d * (1.0 if i == 0 else 0.6), 30 * rear * d, curl=25 * rear * d))
            else: p.update(_leg(i, s, sx, -8 * rear * (1 + 0.3 * i), 3 * rear * (i - 1)))
        p['Thorax'] = qaxis((1, 0, 0), np.radians(-12 * rear + 22 * strike))
        if 'Spinneret' in sk.names: p['Spinneret'] = qaxis((1, 0, 0), np.radians(-15 * rear + 25 * strike))
        return p
    ks = [key(0, P(0, 0)), key(0.38, P(1, 0), hips=(0, 0.10, -0.08), hips_rot=(-18, 0, 0)),
          key(0.55, P(0.3, 1), hips=(0, -0.02, 0.28), hips_rot=(10, 0, 0)), key(0.75, P(0.1, 0.6), hips=(0, 0, 0.18), hips_rot=(5, 0, 0)), key(1.1, P(0, 0))]
    return build_qclip('Attack', ks, _bones(sk))


def hit(sk):
    p = {'Thorax': qaxis((1, 0, 0), np.radians(-10)), 'Abdomen': qaxis((0, 1, 0), np.radians(10))}
    ks = [key(0, {}), key(0.12, p, hips=(0, -0.04, -0.08), hips_rot=(-6, -8, 4)), key(0.5, {})]
    return build_qclip('Hit', ks, _bones(sk))


def death(sk):
    L = legs_of(sk); p = {}
    for i, s, sx, h in L: p.update(_leg(i, s, sx, (10 + 6 * i) * (1 if i < 2 else -1) * (1 if sx > 0 else 0.7), 35 + 8 * i, curl=50 + 7 * i))
    p['Thorax'] = qaxis((1, 0, 0), np.radians(15))
    lo = min(np.asarray(sk.pos[sk['Hips']])[1] * 0.55, 0.6)
    ks = [key(0, {}), key(0.45, p, hips=(0, -lo * 0.5, 0), hips_rot=(0, 0, 12)), key(1.2, p, hips=(0, -lo, 0), hips_rot=(0, 0, 150))]
    return build_qclip('Death', ks, _bones(sk))


def build(sk, asset, size=1.0):
    """size : longueur caractéristique (m) — foulée et levée des pattes en proportion."""
    for c in (idle(sk), gait(sk, 'Walk', 1.1, 0.42 * size, 0.10 * size, 0.02 * size),
              gait(sk, 'Run', 0.62, 0.75 * size, 0.18 * size, 0.05 * size, tail=14, head=5), attack(sk), hit(sk), death(sk)):
        asset.clips[c.name] = c
    return asset
