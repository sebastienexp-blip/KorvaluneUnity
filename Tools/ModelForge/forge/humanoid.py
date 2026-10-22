"""Squelette humanoïde compatible Unity Mecanim (noms HumanBodyBones), pose T, doigts complets, chaînes de cape, points d'attache.

Repère : Y haut, regard +Z, gauche +X. Toutes les rotations de repos des os sont nulles (axes du monde) ;
seuls les points d'attache (nœuds Null) portent une rotation de repos.
"""
import numpy as np
from .rig import Skeleton, qeuler

# positions globales de repos (côté gauche ; le côté droit est symétrique en X)
CORE = {
    'Hips': (0, 1.000, 0.000), 'Spine': (0, 1.100, 0.004), 'Chest': (0, 1.225, 0.000), 'UpperChest': (0, 1.350, -0.006),
    'Neck': (0, 1.535, -0.012), 'Head': (0, 1.625, -0.002),
}
ARM = {
    'Shoulder': (0.035, 1.478, -0.016), 'UpperArm': (0.185, 1.468, -0.026), 'LowerArm': (0.472, 1.466, -0.034), 'Hand': (0.735, 1.466, -0.026),
}
# doigts : (proximal, intermédiaire, distal, bout)
FINGERS = {
    'Thumb': [(0.758, 1.446, 0.022), (0.787, 1.436, 0.048), (0.812, 1.433, 0.066), (0.834, 1.432, 0.078)],
    'Index': [(0.828, 1.462, 0.027), (0.874, 1.462, 0.030), (0.902, 1.461, 0.031), (0.926, 1.460, 0.032)],
    'Middle': [(0.833, 1.464, 0.004), (0.884, 1.464, 0.004), (0.916, 1.463, 0.004), (0.942, 1.462, 0.004)],
    'Ring': [(0.828, 1.462, -0.018), (0.874, 1.461, -0.020), (0.904, 1.460, -0.021), (0.928, 1.459, -0.022)],
    'Little': [(0.818, 1.458, -0.038), (0.853, 1.457, -0.041), (0.876, 1.456, -0.043), (0.896, 1.455, -0.044)],
}
LEG = {
    'UpperLeg': (0.094, 0.962, 0.000), 'LowerLeg': (0.100, 0.528, 0.012), 'Foot': (0.104, 0.090, -0.026), 'Toes': (0.104, 0.020, 0.112),
}
CAPE = {   # trois chaînes (gauche, centre, droite) de quatre os
    'C': [(0.0, 1.470, -0.135), (0.0, 1.170, -0.180), (0.0, 0.870, -0.205), (0.0, 0.560, -0.215)],
    'L': [(0.150, 1.462, -0.125), (0.170, 1.160, -0.170), (0.190, 0.860, -0.195), (0.205, 0.555, -0.205)],
}
TIPS = {'Cape_C': (0.0, 0.26, -0.225), 'Cape_L': (0.215, 0.26, -0.215)}


def mx(p): return (-p[0], p[1], p[2])


def build(weapon_rot=(90, 0, 0), shield_rot=(-90, 180, 0), weapon_pos=None, shield_pos=None):
    sk = Skeleton()
    sk.add('Hips', None, CORE['Hips'])
    sk.add('Spine', 'Hips', CORE['Spine']); sk.add('Chest', 'Spine', CORE['Chest']); sk.add('UpperChest', 'Chest', CORE['UpperChest'])
    sk.add('Neck', 'UpperChest', CORE['Neck']); sk.add('Head', 'Neck', CORE['Head'])
    for side, f in (('Left', lambda p: p), ('Right', mx)):
        sk.add(f'{side}Shoulder', 'UpperChest', f(ARM['Shoulder']))
        sk.add(f'{side}UpperArm', f'{side}Shoulder', f(ARM['UpperArm']))
        sk.add(f'{side}LowerArm', f'{side}UpperArm', f(ARM['LowerArm']))
        sk.add(f'{side}Hand', f'{side}LowerArm', f(ARM['Hand']))
        for fn, pts in FINGERS.items():
            par = f'{side}Hand'
            for k, seg in enumerate(('Proximal', 'Intermediate', 'Distal')):
                nm = f'{side}{fn}{seg}'; sk.add(nm, par, f(pts[k])); par = nm
    for side, f in (('Left', lambda p: p), ('Right', mx)):
        sk.add(f'{side}UpperLeg', 'Hips', f(LEG['UpperLeg'])); sk.add(f'{side}LowerLeg', f'{side}UpperLeg', f(LEG['LowerLeg']))
        sk.add(f'{side}Foot', f'{side}LowerLeg', f(LEG['Foot'])); sk.add(f'{side}Toes', f'{side}Foot', f(LEG['Toes']))
    for ch, pts in (('C', CAPE['C']), ('L', CAPE['L']), ('R', [mx(p) for p in CAPE['L']])):
        par = 'UpperChest'
        for k, p in enumerate(pts):
            nm = f'Cape_{ch}{k + 1}'; sk.add(nm, par, p); par = nm
    # points d'attache
    wp = weapon_pos or (-0.800, 1.452, 0.000)
    sk.add('Socket_Weapon_R', 'RightHand', wp, rest=qeuler(*weapon_rot), null=True)
    sp = shield_pos or (0.610, 1.530, -0.034)
    sk.add('Socket_Shield_L', 'LeftLowerArm', sp, rest=qeuler(*shield_rot), null=True)
    sk.add('Socket_Back', 'UpperChest', (0.0, 1.30, -0.20), rest=qeuler(0, 180, 0), null=True)
    return sk


def swap_table(sk):
    sw = {}
    for i, n in enumerate(sk.names):
        for a, b in (('Left', 'Right'), ('Cape_L', 'Cape_R')):
            if n.startswith(a):
                j = sk.names.index(b + n[len(a):]); sw[i] = j; sw[j] = i
    return sw


def finger_tip(name):
    for fn, pts in FINGERS.items():
        if fn in name: return pts
    return None
