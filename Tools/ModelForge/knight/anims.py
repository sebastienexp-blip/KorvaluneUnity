"""Animations du chevalier (30 i/s) : Idle, Walk, Run, Attack (taille diagonale), Block (garde au bouclier), Hit, Dodge (roulade), Death.
Poses construites os par os (rotations composées en repère monde, côté droit par symétrie), pieds recalés au sol,
cape simulée comme un tissu lourd qui suit le mouvement du corps."""
import numpy as np
from forge.rig import qmul, qaxis, qeuler
from forge.anim import key, build_qclip, simulate_cape
from forge import humanoid as H

D = np.radians
I = np.array([0, 0, 0, 1.0])
def Rx(a): return qaxis((1, 0, 0), D(a))
def Ry(a): return qaxis((0, 1, 0), D(a))
def Rz(a): return qaxis((0, 0, 1), D(a))
def M(*qs):
    out = I
    for q in qs: out = qmul(out, q)
    return out
def mirror(q): return np.array([q[0], -q[1], -q[2], q[3]])


class Pose(dict):
    """Pose = {os: quaternion local}. Les angles sont exprimés pour le côté GAUCHE ; arm(..., side='R') applique la symétrie."""
    def put(self, bone, q, side='L'):
        if side == 'R': bone = bone.replace('Left', 'Right'); q = mirror(q)
        self[bone] = q; return self

    def spine(self, bend=0, twist=0, side=0, chest=None, upper=None):
        c = chest if chest is not None else (bend, twist, side)
        u = upper if upper is not None else (bend * 0.6, twist * 0.6, side * 0.5)
        self['Spine'] = qeuler(bend, twist, side); self['Chest'] = qeuler(*c); self['UpperChest'] = qeuler(*u); return self

    def head(self, nod=0, turn=0, tilt=0):
        self['Neck'] = qeuler(nod * 0.4, turn * 0.4, tilt * 0.4); self['Head'] = qeuler(nod * 0.6, turn * 0.6, tilt * 0.6); return self

    def arm(self, side='L', down=72, fwd=10, twist=0, elbow=20, shrug=0, wrist=(0, 0, 0)):
        """down : abaissement depuis la pose T ; fwd : bras porté vers l'avant (+) / l'arrière (−) ;
        twist : rotation du bras sur son axe ; elbow : flexion du coude ; wrist : (flexion, inclinaison, rotation)."""
        self.put('LeftShoulder', M(Rx(-fwd * 0.12), Rz(-down * 0.10 + shrug)), side)
        self.put('LeftUpperArm', M(Rx(-fwd), Rz(-down * 0.9), Rx(twist)), side)
        self.put('LeftLowerArm', M(Ry(-elbow)), side)
        self.put('LeftHand', M(Rz(wrist[0]), Ry(wrist[1]), Rx(wrist[2])), side)
        return self

    def fist(self, side='L', curl=1.0):
        for fn in ('Index', 'Middle', 'Ring', 'Little'):
            for k, seg in enumerate(('Proximal', 'Intermediate', 'Distal')):
                self.put(f'Left{fn}{seg}', Rz(-curl * (78, 92, 60)[k]), side)
        self.put('LeftThumbProximal', M(Ry(-curl * 25), Rz(-curl * 22)), side)
        self.put('LeftThumbIntermediate', Rz(-curl * 35), side)
        self.put('LeftThumbDistal', Rz(-curl * 30), side)
        return self

    def leg(self, side='L', thigh=0, knee=0, foot=0, spread=0, turn=0, toe=0):
        """thigh : cuisse vers l'avant (+) ; knee : flexion (+) ; foot : pointe vers le haut (+)."""
        self.put('LeftUpperLeg', M(Rz(spread), Ry(turn), Rx(-thigh)), side)
        self.put('LeftLowerLeg', Rx(knee), side)
        self.put('LeftFoot', Rx(-foot), side)
        self.put('LeftToes', Rx(toe), side)
        return self


class Style:
    """Style de combat : garde, bras pendant la marche, attaque, blocage. Le style par défaut est épée + écu."""
    name = 'sword_shield'
    def guard(self, p): return _guard_sword(p)
    def gait_arms(self, p, w, run, arm_amp):
        p.arm('R', down=72, fwd=16 + arm_amp * np.sin(w + np.pi), twist=-10, elbow=40 + (30 if run else 0), wrist=(-8, 0, -15))
        p.arm('L', down=64, fwd=22 + 0.35 * arm_amp * np.sin(w), twist=35, elbow=62 + (15 if run else 0))
    attack = None; block = None
    def idle_arms(self, p, br):
        """Respiration au repos : par défaut, l'épée oscille légèrement (les autres styles gardent leur garde)."""
        if self.name == 'sword_shield':
            p.arm('R', down=70 - 1.5 * br, fwd=18, twist=-10, elbow=38 + 2 * br, wrist=(-8, 0, -15))


_STYLE = Style()
_SK = None


def guard(p, crouch=0.0):
    return _STYLE.guard(p)


def _guard_sword(p):
    """Garde de base : épée basse devant, bouclier au bras gauche porté devant le flanc."""
    p.arm('R', down=70, fwd=18, twist=-10, elbow=38, wrist=(-8, 0, -15))
    p.arm('L', down=66, fwd=20, twist=35, elbow=60, wrist=(0, 0, 0))
    p.fist('R'); p.fist('L', 0.9)
    return p


def BONES(sk):
    return [n for i, n in enumerate(sk.names) if not sk.null[i] and n != 'Hips' and not n.startswith('Cape_')]


def ground(sk, clip, extra_drop=None, min_height=None):
    """Recale le bassin pour que le pied le plus bas touche le sol (pieds de repos à leur hauteur d'origine)."""
    idx = [sk['LeftToes'], sk['RightToes'], sk['LeftFoot'], sk['RightFoot']]
    rest = sk.rest_global(); y0 = min(rest[i][1, 3] for i in idx[:2]); yf0 = min(rest[i][1, 3] for i in idx[2:])
    for f in range(clip.n):
        G = sk.fk({b: q[f] for b, q in clip.rot.items()}, clip.pos['Hips'][f])
        low = min(min(G[i][1, 3] for i in idx[:2]) - y0, min(G[i][1, 3] for i in idx[2:]) - yf0)
        clip.pos['Hips'][f, 1] -= low
    return clip


# ------------------------------------------------------------------------------------------------ clips
def idle(sk):
    ks = []
    for t, br in ((0, 0), (1.2, 1), (2.4, 0)):
        p = Pose().spine(2 + 1.5 * br, -6, 0).head(-2 - 1 * br, 6)
        guard(p)
        _STYLE.idle_arms(p, br)
        p.leg('L', thigh=6, knee=8, foot=0, spread=4, turn=-8); p.leg('R', thigh=-4, knee=6, foot=0, spread=-6, turn=12)
        ks.append(key(t, p, hips=(0.01 * br, -0.03 - 0.006 * br, 0), hips_rot=(0, 10, 1.5 * br)))
    return build_qclip('Idle', ks, BONES(sk), loop=True)


def gait(sk, name, period, amp, knee, arm_amp, lean, bob, run=False):
    ks = []
    for k in range(9):
        ph = k / 8; w = 2 * np.pi * ph
        p = Pose().spine(lean, 8 * np.sin(w) * amp / 30, 0).head(-lean * 0.7, -6 * np.sin(w) * amp / 30)
        for side, off in (('L', 0), ('R', np.pi)):
            s = np.sin(w + off); c = np.cos(w + off)
            kb = knee * max(0.0, -c) ** 1.3 + 6 + (knee * 0.35 if run else 0) * max(0.0, s)
            p.leg(side, thigh=amp * s + (8 if run else 2), knee=kb, foot=-0.4 * amp * s * (s < 0) + 6 * (c > 0.6), spread=1)
        guard(p)
        _STYLE.gait_arms(p, w, run, arm_amp)
        ks.append(key(k * period / 8, p, hips=(0.018 * np.sin(w), -bob * np.cos(2 * w) - (0.06 if run else 0.02), 0),
                      hips_rot=(lean * 0.3, 6 * np.sin(w) * amp / 30, 3 * np.cos(w))))
    return build_qclip(name, ks, BONES(sk), loop=True)


def attack(sk):
    if _STYLE.attack: return _STYLE.attack(sk)
    def P(spine, twist, rdown, rfwd, rtw, rel, wr, lfwd, lel, lthigh, rthigh, lknee, rknee, hz, hry):
        p = Pose().spine(spine, twist, 0).head(-spine * 0.5, -twist * 0.7)
        guard(p)
        p.arm('R', down=rdown, fwd=rfwd, twist=rtw, elbow=rel, wrist=wr)
        p.arm('L', down=62, fwd=lfwd, twist=40, elbow=lel)
        p.leg('L', thigh=lthigh, knee=lknee, spread=4); p.leg('R', thigh=rthigh, knee=rknee, spread=-4)
        return p, hz, hry
    poses = [
        (0.00, P(3, -6, 70, 18, -10, 38, (-8, 0, -15), 28, 78, 6, -4, 8, 6, 0.0, 10)),
        (0.22, P(-4, 35, -35, -10, -60, 95, (30, 0, -40), 34, 85, 18, -16, 16, 10, -0.04, 30)),    # armé : épée au-dessus de l'épaule droite
        (0.36, P(14, -20, 10, 70, -80, 35, (-10, 0, -30), 18, 70, 30, -22, 30, 8, 0.12, -10)),     # frappe : taille diagonale
        (0.46, P(20, -38, 50, 40, -70, 15, (-20, 0, -20), 12, 64, 32, -24, 34, 10, 0.16, -25)),    # accompagnement
        (0.70, P(10, -20, 62, 26, -30, 30, (-12, 0, -18), 22, 72, 20, -14, 22, 10, 0.08, -8)),
        (0.95, P(3, -6, 70, 18, -10, 38, (-8, 0, -15), 28, 78, 6, -4, 8, 6, 0.0, 10)),
    ]
    ks = [key(t, p, hips=(0, -0.03 - 0.03 * (abs(hz) > 0.05), hz), hips_rot=(0, hry, 0)) for t, (p, hz, hry) in poses]
    return build_qclip('Attack', ks, BONES(sk))


def block(sk):
    if _STYLE.block: return _STYLE.block(sk)
    ks = []
    for t, br in ((0, 0), (0.6, 1), (1.2, 0)):
        p = Pose().spine(10 + br, 18, 0).head(-8, -14)
        p.arm('L', down=38, fwd=55, twist=40, elbow=95 + 2 * br, wrist=(0, 0, 0))        # bouclier levé devant le buste
        p.arm('R', down=40, fwd=-8, twist=-50, elbow=75, wrist=(20, 0, -35))               # épée ramenée, prête à riposter
        p.fist('R'); p.fist('L', 0.9)
        p.leg('L', thigh=22, knee=30, spread=6, turn=-10); p.leg('R', thigh=-10, knee=22, spread=-8, turn=20)
        ks.append(key(t, p, hips=(0, -0.10 - 0.005 * br, 0.02), hips_rot=(0, -20, 0)))
    return build_qclip('Block', ks, BONES(sk), loop=True)


def hit(sk):
    def P(bend, nod, tw, lthigh, rthigh, lk, rk):
        p = Pose().spine(bend, tw, 0).head(nod, tw * 0.5); guard(p)
        p.leg('L', thigh=lthigh, knee=lk, spread=4); p.leg('R', thigh=rthigh, knee=rk, spread=-4); return p
    ks = [key(0.00, P(3, -2, -6, 6, -4, 8, 6), hips=(0, -0.03, 0)),
          key(0.10, P(-16, 22, 14, -8, 14, 14, 20), hips=(0, -0.06, -0.10), hips_rot=(-10, 6, 0)),
          key(0.22, P(-8, 10, 8, -4, 12, 18, 22), hips=(0, -0.07, -0.13), hips_rot=(-4, 4, 0)),
          key(0.50, P(3, -2, -6, 6, -4, 8, 6), hips=(0, -0.03, -0.10))]
    return build_qclip('Hit', ks, BONES(sk))


def dodge(sk):
    """Roulade avant (déplacement réel du bassin : mouvement racine)."""
    tuck = Pose().spine(55, 0, 0).head(45, 0)
    tuck.arm('R', down=40, fwd=70, twist=-20, elbow=110); tuck.arm('L', down=40, fwd=70, twist=40, elbow=100)
    tuck.fist('R'); tuck.fist('L', 0.9)
    tuck.leg('L', thigh=115, knee=130, foot=-20); tuck.leg('R', thigh=115, knee=130, foot=-20)
    dive = Pose().spine(30, 0, 0).head(20, 0); guard(dive)
    dive.leg('L', thigh=55, knee=80); dive.leg('R', thigh=-10, knee=40)
    land = Pose().spine(25, 0, 0).head(-5, 0); guard(land)
    land.leg('L', thigh=70, knee=95, foot=10); land.leg('R', thigh=40, knee=110, foot=-10)
    start = Pose().spine(3, -6, 0).head(-2, 6); guard(start); start.leg('L', thigh=6, knee=8); start.leg('R', thigh=-4, knee=6)
    ks = [key(0.00, start, hips=(0, -0.03, 0.0)),
          key(0.10, dive, hips=(0, -0.22, 0.25)),
          key(0.20, tuck, hips=(0, -0.45, 0.65), hips_rot=(90, 0, 0)),
          key(0.30, tuck, hips=(0, -0.50, 1.05), hips_rot=(180, 0, 0)),
          key(0.40, tuck, hips=(0, -0.46, 1.45), hips_rot=(270, 0, 0)),
          key(0.52, land, hips=(0, -0.30, 1.85), hips_rot=(345, 0, 0)),
          key(0.75, start, hips=(0, -0.03, 2.1), hips_rot=(360, 0, 0))]
    return build_qclip('Dodge', ks, BONES(sk))


def death(sk):
    p0 = Pose().spine(3, -6, 0).head(-2, 6); guard(p0); p0.leg('L', thigh=6, knee=8); p0.leg('R', thigh=-4, knee=6)
    p1 = Pose().spine(-20, 10, 8).head(25, 15); guard(p1)
    p1.arm('R', down=40, fwd=-20, twist=0, elbow=20); p1.arm('L', down=45, fwd=-10, twist=20, elbow=40)
    p1.leg('L', thigh=10, knee=45); p1.leg('R', thigh=20, knee=60)
    p2 = Pose().spine(-10, 6, 4).head(30, 30)
    p2.arm('R', down=20, fwd=-30, twist=20, elbow=10, wrist=(20, 0, 0)); p2.arm('L', down=25, fwd=-25, twist=30, elbow=25)
    p2.fist('R', 0.3); p2.fist('L', 0.6)
    p2.leg('L', thigh=25, knee=20, foot=-20, spread=10); p2.leg('R', thigh=10, knee=30, foot=-30, spread=-12)
    ks = [key(0.00, p0, hips=(0, -0.03, 0)),
          key(0.25, p1, hips=(0, -0.25, -0.08), hips_rot=(-10, 10, 4)),
          key(0.55, p1, hips=(0, -0.55, -0.25), hips_rot=(-45, 15, 6)),
          key(0.85, p2, hips=(0, -0.84, -0.55), hips_rot=(-88, 18, 6)),
          key(1.00, p2, hips=(0, -0.80, -0.58), hips_rot=(-84, 18, 6)),
          key(1.60, p2, hips=(0, -0.83, -0.58), hips_rot=(-88, 18, 6))]
    return build_qclip('Death', ks, BONES(sk))


CLIPS = [('Idle', True, False, ()), ('Walk', True, False, (0.0, 0.5)), ('Run', True, False, (0.0, 0.5)),
         ('Attack', False, False, (0.40,)), ('Block', True, False, ()), ('Hit', False, False, ()),
         ('Dodge', False, True, ()), ('Death', False, False, (0.55,))]


def build(sk, asset, style=None):
    global _STYLE, _SK
    _STYLE = style or Style(); _SK = sk
    clips = [idle(sk), gait(sk, 'Walk', 1.05, 24, 55, 14, 4, 0.018), gait(sk, 'Run', 0.68, 38, 95, 30, 14, 0.035, run=True),
             attack(sk), block(sk), hit(sk), dodge(sk), death(sk)]
    for c in clips:
        if c.name not in ('Dodge', 'Death'): ground(sk, c)
        simulate_cape(sk, c, [[f'Cape_{ch}{k}' for k in range(1, 5)] for ch in ('L', 'C', 'R')])
        asset.clips[c.name] = c
    return asset


def aim_wrist(sk, pose, side, target, socket='Socket_Weapon_R', axis=(0, 1, 0), up=None, hips_rot=(0, 0, 0)):
    """Cherche la rotation du poignet (flexion, inclinaison, rotation) qui oriente l'axe de l'arme (axis, repère du socket)
    vers target (monde). up : second axe facultatif (ex. dessus de l'arbalète vers le haut). Modifie pose et renvoie l'angle."""
    from forge.rig import qmat
    target = np.asarray(target, float); target /= np.linalg.norm(target)
    hand = ('Right' if side == 'R' else 'Left') + 'Hand'
    si = sk[socket]; best = None
    base = dict(pose); base['Hips'] = qeuler(*hips_rot)
    def err_of(fx, ry, rx):
        q = M(Rz(fx), Ry(ry), Rx(rx))
        if side == 'R': q = mirror(q)
        base[hand] = q
        G = sk.fk(base)
        d = G[si][:3, :3] @ np.asarray(axis, float)
        e = 1 - d @ target
        if up is not None: e += 0.4 * (1 - (G[si][:3, :3] @ np.asarray(up[0], float)) @ np.asarray(up[1], float))
        return e, q
    for fx in range(-80, 81, 20):
        for ry in range(-60, 61, 30):
            for rx in range(-180, 180, 30):
                e, q = err_of(fx, ry, rx)
                if best is None or e < best[0]: best = (e, q, (fx, ry, rx))
    step = 10
    while step >= 2:
        fx0, ry0, rx0 = best[2]
        for dfx in (-step, 0, step):
            for dry in (-step, 0, step):
                for drx in (-step, 0, step):
                    e, q = err_of(fx0 + dfx, ry0 + dry, rx0 + drx)
                    if e < best[0]: best = (e, q, (fx0 + dfx, ry0 + dry, rx0 + drx))
        step //= 2
    pose[hand] = best[1]
    return best[0]
