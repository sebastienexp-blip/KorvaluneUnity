"""Corps de référence du chevalier (pose T) : volumes anatomiques qui servent de gabarit à l'armure,
et couche visible sous les plates (gambison matelassé, chausses, maille aux articulations)."""
import numpy as np
from forge.geo import Loft, spline, curve, chain, rigid, blend_bones, smoothstep, concat, Mesh
from forge import humanoid as H

N = 40   # anneaux le long des lofts principaux


def torso_loft():
    """Tronc du bassin au cou. a=0 avant, 0.25 gauche (+X), 0.5 dos, 0.75 droite."""
    path = spline([(0, 0.86, 0.004), (0, 0.98, 0.0), (0, 1.10, 0.004), (0, 1.25, 0.008), (0, 1.40, 0.0), (0, 1.50, -0.010), (0, 1.56, -0.014)], N)
    # demi-profondeur (avant/arrière) et demi-largeur selon la hauteur (bassin → cou)
    rf = curve([(0, .118), (.15, .118), (.32, .108), (.50, .118), (.66, .128), (.80, .122), (.92, .095), (1, .060)], N)
    rs = curve([(0, .160), (.15, .168), (.32, .145), (.50, .158), (.66, .178), (.80, .182), (.92, .125), (1, .062)], N)
    of = curve([(0, 0.0), (.5, 0.006), (.7, 0.012), (.85, 0.0), (1, -0.004)], N)
    return Loft(path, rf, rs, p=2.35, of=of, segs=72)


def neck_loft():
    path = spline([(0, 1.50, -0.012), (0, 1.58, -0.006), (0, 1.66, 0.0)], 12)
    return Loft(path, curve([(0, .062), (1, .056)], 12), curve([(0, .066), (1, .058)], 12), p=2.2, segs=32)


def head_loft():
    """Crâne (gabarit du casque)."""
    path = spline([(0, 1.585, 0.006), (0, 1.65, 0.012), (0, 1.73, 0.010), (0, 1.80, 0.002), (0, 1.835, -0.006)], 30)
    rf = curve([(0, .080), (.25, .102), (.55, .108), (.8, .092), (.95, .055), (1, .02)], 30)
    rs = curve([(0, .070), (.25, .084), (.55, .090), (.8, .080), (.95, .048), (1, .018)], 30)
    return Loft(path, rf, rs, p=2.1, segs=64)


def arm_loft():
    """Bras gauche en pose T (le long de +X). a=0 avant (+Z), 0.25 dessous (-Y), 0.5 arrière, 0.75 dessus (+Y)."""
    sh, el, wr = np.array(H.ARM['UpperArm']), np.array(H.ARM['LowerArm']), np.array(H.ARM['Hand'])
    path = spline([sh + (-0.04, 0.0, 0.0), sh + (0.06, 0, 0), el, wr + (-0.03, 0, 0), wr + (0.01, 0, 0)], N)
    rf = curve([(0, .060), (.1, .058), (.3, .052), (.48, .043), (.58, .046), (.75, .038), (.92, .029), (1, .028)], N)
    rs = curve([(0, .064), (.1, .060), (.3, .054), (.48, .045), (.58, .049), (.75, .041), (.92, .023), (1, .022)], N)
    return Loft(path, rf, rs, p=2.3, segs=48)


def leg_loft():
    """Jambe gauche, de la hanche à la cheville. a=0 avant, 0.25 intérieur (-X), 0.5 arrière, 0.75 extérieur (+X)."""
    hip, kn, an = np.array(H.LEG['UpperLeg']), np.array(H.LEG['LowerLeg']), np.array(H.LEG['Foot'])
    path = spline([hip + (0, 0.05, 0), hip + (0.002, -0.05, 0.004), kn, an + (0, 0.04, 0), an + (0, 0.01, 0)], N)
    rf = curve([(0, .095), (.12, .090), (.35, .072), (.5, .058), (.62, .062), (.75, .055), (.9, .040), (1, .038)], N)
    rs = curve([(0, .098), (.12, .092), (.35, .076), (.5, .060), (.62, .058), (.75, .050), (.9, .036), (1, .035)], N)
    of = curve([(0, 0.0), (.45, 0.0), (.6, -0.010), (.75, -0.004), (1, 0.0)], N)
    return Loft(path, rf, rs, p=2.3, of=of, segs=48)


def foot_loft():
    an, to = np.array(H.LEG['Foot']), np.array(H.LEG['Toes'])
    path = spline([an + (0, 0.035, -0.06), an + (0, -0.03, -0.035), an + (0, -0.05, 0.03), to + (0, -0.002, 0.0), to + (0, -0.006, 0.085)], 30)
    rf = curve([(0, .05), (.25, .06), (.6, .042), (.85, .034), (1, .022)], 30)    # hauteur (sens vertical : ref Y)
    rs = curve([(0, .042), (.25, .046), (.6, .050), (.85, .048), (1, .036)], 30)
    return Loft(path, rf, rs, p=3.0, segs=40, ref=(0, 1, 0), cap1=4)


def finger_loft(name, scale=1.0):
    pts = H.FINGERS[name]
    r = {'Thumb': .0105, 'Index': .0095, 'Middle': .0098, 'Ring': .0092, 'Little': .0082}[name] * scale
    path = spline([np.array(pts[0]) - (0.01, 0, 0) if name != 'Thumb' else pts[0]] + list(pts[1:]), 18)
    return Loft(path, curve([(0, r * 1.08), (1, r * 0.82)], 18), curve([(0, r * 1.12), (1, r * 0.86)], 18), p=2.4, segs=16, ref=(0, 1, 0), cap1=3)


def palm_loft():
    hd = np.array(H.ARM['Hand'])
    path = spline([hd + (-0.03, 0, 0), hd + (0.03, -0.002, 0.0), hd + (0.095, -0.003, 0.0)], 16)
    return Loft(path, curve([(0, .028), (.5, .024), (1, .021)], 16), curve([(0, .036), (.5, .046), (1, .048)], 16), p=3.0, segs=32, ref=(0, 1, 0))


# ------------------------------------------------------------------------------------------- pondérations
def skin_torso(m, sk):
    P = [H.CORE['Hips'], H.CORE['Spine'], H.CORE['Chest'], H.CORE['UpperChest'], H.CORE['Neck'], H.CORE['Head']]
    return chain(m, [(0, 0.86, 0)] + P[1:], [sk['Hips'], sk['Spine'], sk['Chest'], sk['UpperChest'], sk['Neck']], blend=0.06)


def skin_arm(m, sk, side='Left'):
    sh, ua, la, hd = (np.array(H.ARM[k]) for k in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand'))
    chain(m, [sh, ua, la, hd, hd + (0.2, 0, 0)], [sk[f'{side}Shoulder'], sk[f'{side}UpperArm'], sk[f'{side}LowerArm'], sk[f'{side}Hand']], blend=0.045)
    return m


def skin_leg(m, sk, side='Left'):
    hp, kn, an, to = (np.array(H.LEG[k]) for k in ('UpperLeg', 'LowerLeg', 'Foot', 'Toes'))
    chain(m, [hp + (0, 0.12, 0), hp, kn, an, to, to + (0, 0, 0.1)],
          [sk['Hips'], sk[f'{side}UpperLeg'], sk[f'{side}LowerLeg'], sk[f'{side}Foot'], sk[f'{side}Toes']], blend=0.05)
    return m


def skin_finger(m, sk, name, side='Left'):
    pts = [np.array(p) for p in H.FINGERS[name]]
    bones = [sk[f'{side}{name}{s}'] for s in ('Proximal', 'Intermediate', 'Distal')]
    hd = np.array(H.ARM['Hand'])
    return chain(m, [hd] + pts, [sk[f'{side}Hand']] + bones, blend=0.008)
