"""Animations procédurales du loup (squelette creatures.wolf) : Idle (respiration, queue, oreilles), Walk (amble latéral),
Run (galop rotatoire), Attack (bond et morsure), Howl, Hit, Death. Angles en degrés relatifs au repos ;
rx > 0 : la patte part vers l'arrière, la tête s'abaisse."""
import numpy as np
from forge.anim import key, build_clip

LEGS = ('UpperArm', 'LowerArm', 'Wrist', 'FrontPaw', 'Thigh', 'Shin', 'Hock', 'BackPaw')


def bones(sk):
    return [n for n in sk.names if not n.startswith('Socket') and n != 'Hips']


def _gait(sk, name, T, amp, phase, lift, body_bob, spine_flex=0.0, head_bob=4.0, n=9):
    keys = []
    for k in range(n):
        t = k * T / (n - 1); w = 2 * np.pi * t / T
        p = {}
        for side, s_ in (('L', 0.0), ('R', 0.5)):
            for leg, off in (('F', phase[0]), ('B', phase[1])):
                ph = w + 2 * np.pi * (off + s_)
                sw = np.sin(ph)                                  # +1 : patte en arrière
                up = max(0.0, -np.cos(ph))                       # phase aérienne (retour vers l'avant)
                if leg == 'F':
                    p[f'UpperArm_{side}'] = (amp * sw, 0, 0)
                    p[f'LowerArm_{side}'] = (-lift * up * 0.6, 0, 0)
                    p[f'Wrist_{side}'] = (lift * up * 1.4 + 10 * max(0, sw), 0, 0)
                    p[f'FrontPaw_{side}'] = (-15 * max(0, sw), 0, 0)
                else:
                    p[f'Thigh_{side}'] = (amp * 0.9 * sw, 0, 0)
                    p[f'Shin_{side}'] = (lift * up * 0.8, 0, 0)
                    p[f'Hock_{side}'] = (-lift * up * 1.2 - 8 * max(0, sw), 0, 0)
                    p[f'BackPaw_{side}'] = (12 * max(0, sw), 0, 0)
        p['Spine1'] = (spine_flex * np.sin(2 * w), 0, 0); p['Spine2'] = (spine_flex * np.sin(2 * w), 0, 0)
        p['Neck1'] = (14 - head_bob * np.sin(2 * w), 0, 0); p['Head'] = (-8 + head_bob * 0.6 * np.sin(2 * w + 0.6), 0, 0)
        for i in range(1, 5): p[f'Tail{i}'] = (-8 + 6 * np.sin(w + i * 0.5), 10 * np.sin(w + i * 0.6), 0)
        keys.append(key(t, p, hips=(0, -body_bob * abs(np.cos(w)), 0), hips_rot=(spine_flex * 0.5 * np.sin(2 * w), 3 * np.sin(w), 0)))
    return build_clip(name, keys, bones(sk), loop=True)


def idle(sk):
    keys = []
    for k in range(7):
        t = k * 0.5; w = 2 * np.pi * t / 3.0
        p = {'Chest': (1.5 * np.sin(w), 0, 0), 'Neck1': (20 + 2 * np.sin(w + 0.4), 3 * np.sin(w * 0.5), 0), 'Head': (-12 + 2 * np.sin(w + 1.0), 6 * np.sin(w * 0.5), 0),
             'Jaw': (3 * np.sin(w * 2), 0, 0), 'Ear_L': (0, 0, 6 * np.sin(w * 2)), 'Ear_R': (0, 0, -6 * np.sin(w * 2 + 0.3))}
        for i in range(1, 5): p[f'Tail{i}'] = (-10, 6 * np.sin(w + i * 0.5), 0)
        for sd in ('L', 'R'): p[f'UpperArm_{sd}'] = (-8, 0, 0); p[f'LowerArm_{sd}'] = (-10, 0, 0); p[f'Wrist_{sd}'] = (14, 0, 0); p[f'Thigh_{sd}'] = (-6, 0, 0); p[f'Shin_{sd}'] = (10, 0, 0); p[f'Hock_{sd}'] = (-8, 0, 0)
        keys.append(key(t, p, hips=(0, -0.045 - 0.006 * np.sin(w), 0), hips_rot=(4, 0, 0)))
    return build_clip('Idle', keys, bones(sk), loop=True)


def attack(sk):
    def P(crouch, reach, jaw, neck, fl, bl):
        p = {'Neck1': (neck, 0, 0), 'Head': (-neck * 0.5, 0, 0), 'Jaw': (jaw, 0, 0)}
        for s in ('L', 'R'):
            p[f'UpperArm_{s}'] = (fl, 0, 0); p[f'LowerArm_{s}'] = (-abs(fl) * 0.5, 0, 0); p[f'Wrist_{s}'] = (abs(fl) * 0.6, 0, 0)
            p[f'Thigh_{s}'] = (bl, 0, 0); p[f'Shin_{s}'] = (abs(bl) * 0.7, 0, 0); p[f'Hock_{s}'] = (-abs(bl) * 0.9, 0, 0)
        return p
    ks = [key(0.0, P(0, 0, 8, -4, 0, 0)),
          key(0.25, P(1, 0, 10, 10, 22, -18), hips=(0, -0.10, -0.05), hips_rot=(-6, 0, 0)),          # ramassé
          key(0.45, P(0, 1, 40, -18, -45, 30), hips=(0, 0.10, 0.35), hips_rot=(-14, 0, 0)),          # bond, gueule grande ouverte
          key(0.60, P(0, 1, 2, 12, -20, 10), hips=(0, -0.02, 0.45), hips_rot=(8, 0, 0)),             # morsure (mâchoire fermée)
          key(1.00, P(0, 0, 8, -4, 0, 0), hips=(0, 0, 0.0))]
    return build_clip('Attack', ks, bones(sk))


def howl(sk):
    ks = [key(0, {'Neck1': (0, 0, 0)}), key(0.6, {'Neck1': (-40, 0, 0), 'Neck2': (-15, 0, 0), 'Head': (-25, 0, 0), 'Jaw': (25, 0, 0)}, hips_rot=(-6, 0, 0)),
          key(2.2, {'Neck1': (-42, 0, 0), 'Neck2': (-16, 0, 0), 'Head': (-28, 0, 0), 'Jaw': (28, 0, 0)}, hips_rot=(-6, 0, 0)), key(2.8, {})]
    return build_clip('Howl', ks, bones(sk))


def hit(sk):
    ks = [key(0, {}), key(0.12, {'Neck1': (15, 20, 0), 'Head': (10, 15, 0), 'Spine2': (0, 8, 0)}, hips=(0, -0.03, -0.08), hips_rot=(0, -8, 4)), key(0.5, {})]
    return build_clip('Hit', ks, bones(sk))


def death(sk):
    p_end = {'Neck1': (35, 0, 25), 'Head': (20, 0, 10), 'Jaw': (20, 0, 0)}
    for s, d in (('L', 1), ('R', -1)):
        p_end[f'UpperArm_{s}'] = (-30, 0, 0); p_end[f'Thigh_{s}'] = (-25, 0, 0); p_end[f'LowerArm_{s}'] = (-30, 0, 0)
    ks = [key(0, {}), key(0.4, {'Neck1': (20, 0, 0)}, hips=(0, -0.20, 0), hips_rot=(0, 0, 30)), key(1.0, p_end, hips=(0, -0.55, 0), hips_rot=(0, 0, 88))]
    return build_clip('Death', ks, bones(sk))


def build(sk, asset, kind='wolf'):
    clips = [idle(sk), _gait(sk, 'Walk', 1.0, 22, (0.25, 0.0), 30, 0.010, spine_flex=1.5, head_bob=3),
             _gait(sk, 'Run', 0.5, 42, (0.10, 0.0), 55, 0.035, spine_flex=9, head_bob=6),
             attack(sk), howl(sk), hit(sk), death(sk)]
    for c in clips: asset.clips[c.name] = c
    return clips
