"""Animations de compétences propres à chaque classe (30 i/s, sur place : le déplacement est géré par le jeu).

Chaque héros reçoit ~10 clips « Sk_* » construits sur son propre style de combat (épée + écu, marteau + pavois,
arbalète, bâton arcanique, bâton nécromantique) : la garde de départ et d'arrivée est celle du héros, l'arme reste
orientée correctement (aim_wrist) et les sauts sont ajoutés après le recalage des pieds au sol."""
import numpy as np
from knight import anims as AN
from knight.anims import Pose, key, build_qclip, BONES, aim_wrist, ground
from forge.anim import simulate_cape

# ------------------------------------------------------------------------------------------------ appuis
STANCE = {   # (cuisse G, genou G, écart G, rotation G), (cuisse D, genou D, écart D, rotation D), hauteur du bassin
    'stand':  ((6, 8, 4, -8), (-4, 6, -6, 12), -0.03),
    'wide':   ((16, 24, 12, -10), (-12, 22, -12, 16), -0.09),
    'lunge':  ((42, 48, 6, -6), (-28, 22, -6, 14), -0.16),      # pied gauche en avant, appui profond
    'lungeR': ((-26, 22, 6, -12), (40, 46, -6, 8), -0.16),
    'crouch': ((48, 78, 10, -8), (30, 82, -10, 10), -0.30),
    'kneel':  ((82, 92, 8, -6), (-18, 104, -8, 10), -0.48),     # genou droit au sol
    'tuck':   ((70, 100, 6, 0), (55, 110, -6, 0), -0.10),       # en l'air, jambes repliées
    'kickR':  ((-6, 18, 6, -10), (88, 12, -4, 0), -0.06),
    'back':   ((-20, 30, 6, -6), (10, 40, -6, 10), -0.10),      # recul : poids sur l'arrière
}


class Seq:
    """Suite de poses clés d'une compétence pour un style donné."""
    def __init__(self, st, sk, aim_up=None, bias=(0, 0, 0)):
        self.st, self.sk, self.ks, self.lift = st, sk, [], []
        self.aim_up = aim_up; self.bias = bias          # bias : posture propre au personnage (ex. nécromancienne voûtée)

    def k(self, t, sp=(0, 0, 0), head=None, R=None, L=None, legs='stand', fist=(1.0, None), aim=None,
          hz=0.0, hy=None, hrx=0.0, hry=0.0, hrz=0.0, lift=0.0, lowered=None):
        hd = head if head is not None else (-sp[0] * 0.5, -sp[1] * 0.7, 0)
        sp = tuple(a + b for a, b in zip(sp, self.bias))
        p = Pose().spine(*sp)
        p.head(hd[0] + self.bias[0] * 0.6, hd[1], hd[2] - self.bias[2] * 1.5)
        self.st.guard(p)
        if R: p.arm('R', **R)
        if L: p.arm('L', **L)
        if fist[0] is not None: p.fist('R', fist[0])
        if fist[1] is not None: p.fist('L', fist[1])
        (lt, lk, ls, lr), (rt, rk, rs, rr), y0 = STANCE[legs]
        p.leg('L', thigh=lt, knee=lk, spread=ls, turn=lr, foot=(-20 if legs == 'tuck' else 0))
        p.leg('R', thigh=rt, knee=rk, spread=rs, turn=rr, foot=(-35 if legs == 'kneel' else (-20 if legs == 'tuck' else 0)))
        if aim is not None: aim_wrist(self.sk, p, 'R', aim, up=self.aim_up)
        self.ks.append(key(t, p, hips=(0, y0 if hy is None else hy, hz), hips_rot=(hrx, hry, hrz)))
        self.lift.append((t, lift))
        return self

    def guard(self, t, legs='stand'):
        return self.k(t, legs=legs)

    def clip(self, name):
        c = build_qclip(name, self.ks, BONES(self.sk))
        ground(self.sk, c)
        ts = np.array([a for a, _ in self.lift]); ls = np.array([b for _, b in self.lift])
        if ls.any():                                   # saut : ajouté après le recalage au sol (parabole lissée)
            tb = np.arange(c.n) / c.fps
            c.pos['Hips'][:, 1] += np.interp(tb, ts, ls)
        return c


def A(down=70, fwd=16, twist=0, elbow=30, wrist=(0, 0, 0), shrug=0):
    return dict(down=down, fwd=fwd, twist=twist, elbow=elbow, wrist=wrist, shrug=shrug)


# =============================================================================================== KAEL (épée + écu)
def kael(st, sk):
    out = []
    S = lambda: Seq(st, sk)
    SW_LOW = A(78, -25, -40, 20, (10, 0, -30))
    # Taille montante : épée basse en arrière puis remontée verticale
    out.append(S().guard(0).k(0.18, (16, 20, 0), R=SW_LOW, legs='crouch', hry=15)
               .k(0.34, (-12, -15, 0), R=A(-50, 40, -70, 25, (-20, 0, -30)), legs='lungeR', hry=-10, lift=0.10)
               .k(0.48, (-16, -20, 0), R=A(-62, 30, -70, 20, (-25, 0, -30)), legs='lungeR', hry=-15, lift=0.04)
               .guard(0.85).clip('Sk_Uppercut'))
    # Taille circulaire complète (360°)
    s = S().guard(0).k(0.15, (6, 40, 0), R=A(40, -40, -60, 40, (0, 0, -20)), legs='wide', hry=30)
    for i, a in enumerate((-60, -150, -240, -330)):
        s.k(0.24 + i * 0.09, (12, -20, 0), R=A(10, 20, -80, 10, (-10, 0, -10)), L=A(50, 30, 40, 60), legs='wide', hry=a)
    out.append(s.k(0.70, (8, -10, 0), legs='wide', hry=-360).k(0.95, legs='stand', hry=-360).clip('Sk_Spin'))
    # Coup de bouclier : écu projeté avec un pas en avant
    out.append(S().guard(0).k(0.15, (-4, 30, 0), L=A(55, 20, 40, 100), legs='back', hry=20)
               .k(0.30, (20, -25, 0), L=A(30, 85, 40, 25), R=A(70, -10, -20, 40), legs='lunge', hz=0.25, hry=-15)
               .k(0.42, (22, -28, 0), L=A(28, 88, 40, 22), legs='lunge', hz=0.28, hry=-18)
               .guard(0.80).clip('Sk_ShieldBash'))
    # Bond fracassant : appel accroupi, saut épée au-dessus de la tête, réception écrasante
    out.append(S().guard(0).k(0.15, (25, 0, 0), R=A(60, -30, -30, 40), legs='crouch')
               .k(0.32, (-15, 10, 0), R=A(-70, 5, -40, 70, (30, 0, -20)), L=A(40, 30, 40, 70), legs='tuck', lift=0.75)
               .k(0.46, (-5, 5, 0), R=A(-75, 10, -40, 60, (30, 0, -20)), legs='tuck', lift=0.80)
               .k(0.60, (40, -5, 0), R=A(40, 80, -20, 10, (-40, 0, 0)), legs='crouch', lift=0.0)
               .k(0.80, (35, -5, 0), R=A(45, 75, -20, 10, (-40, 0, 0)), legs='crouch')
               .guard(1.15).clip('Sk_LeapSlam'))
    # Estoc : fente profonde, bras tendu
    out.append(S().guard(0).k(0.16, (-6, 30, 0), R=A(60, -40, -90, 110, (0, 0, -60)), legs='back', hry=25)
               .k(0.30, (22, -20, 0), R=A(15, 88, -90, 4, (0, 0, -70)), L=A(60, -10, 30, 30), legs='lunge', hz=0.30, hry=-20, aim=(0.25, 0.05, 1))
               .k(0.45, (24, -22, 0), R=A(14, 90, -90, 2, (0, 0, -70)), legs='lunge', hz=0.32, hry=-22, aim=(0.25, 0.05, 1))
               .guard(0.82).clip('Sk_Thrust'))
    # Cri de guerre : buste cambré, bras écartés, tête relevée
    out.append(S().guard(0).k(0.20, (20, 0, 0), (25, 0, 0), R=A(70, 20, -20, 70), L=A(70, 20, 40, 70), legs='wide')
               .k(0.42, (-18, 0, 0), (-30, 0, 0), R=A(5, -15, -40, 25), L=A(5, -15, 40, 25), legs='wide')
               .k(0.95, (-16, 0, 0), (-28, 0, 0), R=A(8, -12, -40, 25), L=A(8, -12, 40, 25), legs='wide')
               .guard(1.30).clip('Sk_WarCry'))
    # Épée brandie vers le ciel (invocations, auras, étendard)
    out.append(S().guard(0).k(0.25, (-8, 0, 0), (-25, 0, 0), R=A(-80, 10, -30, 10, (-10, 0, -10)), legs='wide')
               .k(0.90, (-10, 0, 0), (-28, 0, 0), R=A(-82, 10, -30, 8, (-10, 0, -10)), legs='wide')
               .guard(1.25).clip('Sk_Raise'))
    # Enchaînement de trois coups
    out.append(S().guard(0).k(0.14, (-4, 30, 0), R=A(-30, -10, -60, 95, (30, 0, -40)), legs='stand', hry=25)
               .k(0.26, (14, -25, 0), R=A(45, 50, -70, 15, (-20, 0, -20)), legs='lunge', hz=0.1, hry=-20)
               .k(0.40, (8, -35, 0), R=A(20, 10, 40, 60, (0, 0, 40)), legs='lunge', hz=0.12, hry=-30)
               .k(0.54, (12, 25, 0), R=A(25, 60, -10, 10, (-10, 0, 0)), legs='lunge', hz=0.16, hry=20)
               .k(0.66, (-10, 0, 0), R=A(-55, 20, -40, 60, (30, 0, -20)), legs='stand', hz=0.16)
               .k(0.80, (32, 0, 0), R=A(40, 80, -20, 10, (-40, 0, 0)), legs='lunge', hz=0.22)
               .guard(1.15).clip('Sk_Combo'))
    # Charge : buste en avant derrière l'écu
    out.append(S().guard(0).k(0.12, (30, 15, 0), L=A(30, 70, 40, 70), R=A(70, -30, -20, 50), legs='lunge', hry=-10)
               .k(0.50, (32, 15, 0), L=A(30, 72, 40, 68), R=A(70, -30, -20, 50), legs='lungeR', hry=-10)
               .guard(0.75).clip('Sk_Charge'))
    # Épée plantée dans le sol (zones, ondes)
    out.append(S().guard(0).k(0.20, (-8, 0, 0), R=A(-60, 30, -30, 80, (30, 0, -20)), L=A(60, 30, 40, 60), legs='stand')
               .k(0.40, (38, 0, 0), R=A(55, 60, -10, 10, (-60, 0, 0)), legs='kneel', aim=(0, -1, 0.2))
               .k(0.85, (36, 0, 0), R=A(56, 60, -10, 10, (-60, 0, 0)), legs='kneel', aim=(0, -1, 0.2))
               .guard(1.25).clip('Sk_GroundStab'))
    return out


# =============================================================================================== THARION (marteau + pavois)
def tharion(st, sk):
    out = []; S = lambda: Seq(st, sk)
    out.append(S().guard(0).k(0.30, (-20, 10, 0), R=A(-80, 0, -30, 60, (40, 0, 0)), L=A(30, 40, 30, 80), legs='wide', hy=-0.02, lift=0.1)
               .k(0.48, (45, 0, 0), R=A(50, 85, -20, 5, (-50, 0, 0)), legs='crouch')
               .k(0.75, (42, 0, 0), R=A(52, 82, -20, 5, (-50, 0, 0)), legs='crouch')
               .guard(1.15).clip('Sk_GroundPound'))
    s = S().guard(0).k(0.18, (8, 45, 0), R=A(50, -50, -40, 30), legs='wide', hry=35)
    for i, a in enumerate((-70, -160, -250, -340)):
        s.k(0.28 + i * 0.1, (15, -15, 0), R=A(15, 30, -70, 10, (-30, 0, 0)), legs='wide', hry=a)
    out.append(s.k(0.78, (10, -10, 0), legs='wide', hry=-360).k(1.05, legs='stand', hry=-360).clip('Sk_Spin'))
    out.append(S().guard(0).k(0.16, (-5, 30, 0), L=A(55, 20, 35, 95), legs='back', hry=20)
               .k(0.32, (24, -20, 0), L=A(30, 85, 35, 25), legs='lunge', hz=0.25, hry=-15)
               .k(0.46, (26, -22, 0), L=A(28, 88, 35, 20), legs='lunge', hz=0.28, hry=-18)
               .guard(0.85).clip('Sk_PaviseBash'))
    out.append(S().guard(0).k(0.16, (20, 0, 0), R=A(60, -30, -30, 60), legs='crouch')
               .k(0.34, (-18, 0, 0), R=A(-80, 0, -30, 60, (40, 0, 0)), legs='tuck', lift=0.7)
               .k(0.48, (-8, 0, 0), R=A(-80, 5, -30, 50, (40, 0, 0)), legs='tuck', lift=0.75)
               .k(0.62, (45, 0, 0), R=A(50, 85, -20, 5, (-50, 0, 0)), legs='crouch', lift=0)
               .k(0.85, (42, 0, 0), R=A(52, 82, -20, 5, (-50, 0, 0)), legs='crouch')
               .guard(1.2).clip('Sk_LeapSlam'))
    out.append(S().guard(0).k(0.22, (-10, 40, 0), R=A(-20, -60, -60, 110, (40, 0, 0)), legs='back', hry=30)
               .k(0.38, (25, -30, 0), R=A(30, 95, -60, 5, (-30, 0, 0)), legs='lunge', hry=-25)
               .k(0.42, (26, -32, 0), R=A(35, 95, -60, 5, (-30, 0, 0)), fist=(0.2, None), legs='lunge', hry=-26)
               .guard(0.85).clip('Sk_Throw'))
    out.append(S().guard(0).k(0.30, (20, 0, 0), (30, 0, 0), R=A(40, 60, -30, 110, (-60, 0, 0)), L=A(55, 40, 40, 90), legs='kneel')
               .k(1.10, (22, 0, 0), (32, 0, 0), R=A(40, 62, -30, 112, (-60, 0, 0)), L=A(55, 40, 40, 90), legs='kneel')
               .guard(1.5).clip('Sk_Prayer'))
    out.append(S().guard(0).k(0.20, (25, 5, 0), L=A(40, 55, 35, 60), R=A(60, 10, -20, 80), legs='crouch')
               .k(0.95, (26, 5, 0), L=A(40, 56, 35, 60), R=A(60, 10, -20, 82), legs='crouch')
               .guard(1.25).clip('Sk_Brace'))
    out.append(S().guard(0).k(0.28, (-10, 0, 0), (-30, 0, 0), R=A(-82, 10, -30, 10, (20, 0, 0)), legs='wide')
               .k(0.90, (-12, 0, 0), (-32, 0, 0), R=A(-84, 10, -30, 8, (20, 0, 0)), legs='wide')
               .guard(1.25).clip('Sk_Raise'))
    out.append(S().guard(0).k(0.18, (25, 25, 0), R=A(80, -30, -40, 20, (20, 0, 0)), legs='crouch', hry=20)
               .k(0.36, (-15, -15, 0), R=A(-60, 50, -50, 20, (40, 0, 0)), legs='lungeR', hry=-10, lift=0.08)
               .k(0.50, (-18, -20, 0), R=A(-70, 40, -50, 15, (40, 0, 0)), legs='lungeR', hry=-12)
               .guard(0.9).clip('Sk_Uppercut'))
    out.append(S().guard(0).k(0.12, (30, 10, 0), L=A(30, 72, 35, 65), legs='lunge', hry=-10)
               .k(0.50, (32, 10, 0), L=A(30, 74, 35, 64), legs='lungeR', hry=-10)
               .guard(0.75).clip('Sk_Charge'))
    return out


# =============================================================================================== VAREK (arbalète)
def varek(st, sk):
    out = []; up = ((0, 0, 1), (0, 1, 0)); S = lambda: Seq(st, sk, aim_up=up)
    AIM = A(20, 75, -10, 95, (0, 0, -85)); SUP = A(25, 80, 40, 60, (0, 0, 20))
    out.append(S().guard(0).k(0.25, (-20, 10, 0), (-35, 0, 0), R=A(-20, 60, -10, 95), L=A(-10, 70, 40, 60), aim=(0, 1.2, 1), legs='wide')
               .k(0.45, (-22, 10, 0), (-38, 0, 0), R=A(-20, 60, -10, 95), L=A(-10, 70, 40, 60), aim=(0, 1.2, 1), legs='wide')
               .k(0.52, (-26, 12, 0), (-40, 0, 0), R=A(-28, 55, -10, 100), L=A(-15, 66, 40, 62), aim=(0, 1.4, 0.8), legs='wide')
               .guard(0.95).clip('Sk_SkyShot'))
    out.append(S().guard(0).k(0.18, (12, 12, 0), R=AIM, L=SUP, aim=(0, 0, 1), legs='kneel', hry=12)
               .k(0.27, (12, 12, 0), R=AIM, L=SUP, aim=(0, 0, 1), legs='kneel', hry=12)
               .k(0.33, (4, 16, 0), (-12, 0, 0), R=A(14, 85, -10, 105), L=SUP, aim=(0, 0.15, 1), legs='kneel', hry=14)
               .guard(0.75).clip('Sk_KneelShot'))
    s = S().guard(0)
    for i in range(3):
        t0 = 0.12 + i * 0.18
        s.k(t0, (6, 10, 0), R=AIM, L=SUP, aim=(0, 0, 1), legs='wide', hry=12)
        s.k(t0 + 0.06, (-2, 14, 0), (-10, 0, 0), R=A(14, 85, -10, 105), L=SUP, aim=(0, 0.15, 1), legs='wide', hry=14)
    out.append(s.guard(0.85).clip('Sk_RapidFire'))
    out.append(S().guard(0).k(0.12, (20, 0, 0), legs='crouch')
               .k(0.26, (-40, 0, 0), legs='tuck', hz=-0.1, hrx=-120, lift=0.7)
               .k(0.40, (-20, 0, 0), legs='tuck', hz=-0.15, hrx=-260, lift=0.6)
               .k(0.55, (20, 0, 0), R=AIM, L=SUP, aim=(0, 0, 1), legs='crouch', hz=-0.1, hrx=-360, lift=0)
               .k(0.80, (8, 10, 0), R=AIM, L=SUP, aim=(0, 0, 1), legs='wide', hz=0, hrx=-360)
               .k(1.00, hrx=-360).clip('Sk_BackFlip'))
    out.append(S().guard(0).k(0.22, (-14, 35, 0), L=A(-40, -30, 30, 110, (30, 0, 0)), fist=(1, 1.0), legs='back', hry=25)
               .k(0.38, (24, -25, 0), L=A(30, 95, 30, 10), fist=(1, 0.1), legs='lunge', hry=-20)
               .guard(0.80).clip('Sk_Throw'))
    out.append(S().guard(0).k(0.25, (50, 0, 0), L=A(70, 60, 30, 20, (30, 0, 0)), fist=(1, 0.2), legs='crouch')
               .k(0.55, (52, 0, 0), L=A(72, 62, 30, 15, (30, 0, 0)), fist=(1, 0.0), legs='crouch')
               .guard(0.95).clip('Sk_Trap'))
    out.append(S().guard(0).k(0.14, (-10, 0, 0), legs='back')
               .k(0.30, (-25, 0, 0), legs='kickR', hz=0.08)
               .k(0.40, (-24, 0, 0), legs='kickR', hz=0.1)
               .guard(0.75).clip('Sk_Kick'))
    s = S().guard(0)
    for i, a in enumerate((40, 15, -15, -40)):
        s.k(0.12 + i * 0.14, (6, a * 0.4, 0), R=AIM, L=SUP, aim=(np.sin(np.radians(a)) * 0.3, 0, 1), legs='wide', hry=a)
    out.append(s.guard(0.9).clip('Sk_Sweep'))
    out.append(S().guard(0).k(0.20, (-4, 0, 0), (-10, 0, 0), L=A(30, 55, 30, 140, (40, 0, 0)), fist=(1, 0.6), legs='stand')
               .k(0.55, (-6, 0, 0), (-14, 0, 0), L=A(30, 56, 30, 142, (40, 0, 0)), fist=(1, 0.6), legs='stand')
               .k(0.75, (10, -10, 0), L=A(10, 85, 30, 5), fist=(1, 0.0), legs='lunge', hry=-8)
               .guard(1.1).clip('Sk_Whistle'))
    out.append(S().guard(0).k(0.12, (35, 0, 0), legs='lunge').k(0.45, (36, 0, 0), legs='lungeR').guard(0.65).clip('Sk_Dash'))
    return out


# =============================================================================================== BÂTONS (Elyra, Morvanna)
def _staff_common(st, sk, out, palm_l, bias=(0, 0, 0)):
    S = lambda: Seq(st, sk, bias=bias)
    VERT = (0.06, 1.0, 0.12)
    out.append(S().guard(0).k(0.30, (-6, 20, 0), R=A(40, 30, -12, 70), L=A(30, 40, 10, 80, (25, 0, 0)), aim=(0, 1, -0.25), fist=(1, 0.05), legs='stand')
               .k(0.45, (-22, 0, 0), (-25, 0, 0), R=A(-50, 20, -12, 40), L=A(-60, 10, 10, 30, (40, 0, 0)), aim=(0, 1, 0.3), fist=(1, 0.0), legs='wide', lift=0.03)
               .k(0.62, (40, 0, 0), R=A(55, 70, -12, 20), L=A(65, 60, 10, 20), aim=(0, -1, 0.15), legs='crouch')
               .k(0.95, (38, 0, 0), R=A(56, 70, -12, 20), L=A(66, 60, 10, 20), aim=(0, -1, 0.15), legs='crouch')
               .guard(1.3).clip('Sk_StaffSlam'))
    out.append(S().guard(0).k(0.35, (-14, 0, 0), (-35, 0, 0), R=A(-70, 10, -12, 15), L=A(-70, 10, 10, 15, (40, 0, 0)), aim=(0, 1, 0.1), fist=(1, 0.0), legs='wide')
               .k(0.95, (-16, 0, 0), (-38, 0, 0), R=A(-72, 10, -12, 12), L=A(-74, 10, 10, 12, (40, 0, 0)), aim=(0, 1, 0.1), fist=(1, 0.0), legs='wide')
               .k(1.10, (10, 0, 0), (5, 0, 0), R=A(20, 60, -12, 30), L=A(20, 75, 10, 10, palm_l), aim=(0, 0.6, 1), fist=(1, 0.0), legs='lunge')
               .guard(1.45).clip('Sk_SkyCall'))
    out.append(S().guard(0).k(0.20, (2, 40, 0), R=A(40, 10, -12, 50), aim=(-1, 0.4, 0.2), legs='wide', hry=35)
               .k(0.40, (14, -35, 0), R=A(20, 70, -12, 10), L=A(60, -10, 10, 40), aim=(1, 0.2, 0.6), legs='lunge', hry=-30)
               .k(0.50, (14, -40, 0), R=A(25, 60, -12, 10), aim=(1, 0.2, 0.3), legs='lunge', hry=-35)
               .guard(0.90).clip('Sk_Sweep'))
    out.append(S().guard(0).k(0.25, (6, 0, 0), R=A(25, 60, -20, 75), L=A(20, 70, 20, 60, (40, 0, 0)), aim=VERT, fist=(1, 0.05), legs='wide')
               .k(0.90, (8, 0, 0), R=A(25, 62, -20, 76), L=A(18, 72, 20, 58, (40, 0, 0)), aim=VERT, fist=(1, 0.05), legs='wide')
               .guard(1.2).clip('Sk_Ward'))
    s = S().guard(0)
    for i, a in enumerate((-90, -180, -270, -360)):
        s.k(0.18 + i * 0.13, (-6, 0, 0), (-15, 0, 0), R=A(0, 20, -12, 20), L=A(0, 20, 10, 20, palm_l), aim=(0.3, 1, 0.3), fist=(1, 0.0), legs='wide', hry=a)
    out.append(s.k(0.95, legs='stand', hry=-360).clip('Sk_Spiral'))
    out.append(S().guard(0).k(0.18, (30, 0, 0), R=A(60, 30, -12, 60), L=A(70, 40, 10, 90), aim=VERT, legs='crouch')
               .k(0.32, (-10, 0, 0), (-15, 0, 0), R=A(45, 20, -12, 40), L=A(30, -20, 10, 30, palm_l), aim=VERT, fist=(1, 0.0), legs='wide', lift=0.15)
               .guard(0.70).clip('Sk_Blink'))
    out.append(S().guard(0).k(0.15, (-4, 20, 0), L=A(40, 30, 10, 100, (30, 0, 0)), legs='stand', hry=10)
               .k(0.28, (12, -15, 0), L=A(20, 85, 10, 5, palm_l), fist=(1, 0.0), legs='lunge', hry=-12)
               .k(0.40, (12, -16, 0), L=A(20, 86, 10, 5, palm_l), fist=(1, 0.0), legs='lunge', hry=-12)
               .guard(0.72).clip('Sk_CastFwd'))
    out.append(S().guard(0).k(0.25, (16, -10, 0), R=A(30, 70, -12, 25), L=A(25, 80, 10, 10, palm_l), aim=(0, 0.3, 1), fist=(1, 0.0), legs='lunge')
               .k(1.30, (18, -12, 0), R=A(30, 72, -12, 22), L=A(24, 82, 10, 8, palm_l), aim=(0, 0.3, 1), fist=(1, 0.0), legs='lunge')
               .guard(1.6).clip('Sk_Channel'))


def elyra(st, sk):
    out = []; S = lambda: Seq(st, sk)
    PALM = (60, 0, 0)
    _staff_common(st, sk, out, PALM, bias=(-3, 0, 0))            # Elyra : port droit, altier
    out.append(S().guard(0).k(0.10, (0, 15, 0), L=A(50, 50, 10, 80, (30, 0, 0)), legs='stand')
               .k(0.18, (6, -10, 0), L=A(25, 85, 10, 10, (60, 0, 0)), fist=(1, 0.0), legs='stand', hry=-8)
               .k(0.30, (0, 10, 0), L=A(45, 55, 10, 70, (30, 0, 0)), legs='stand')
               .k(0.38, (6, -12, 0), L=A(25, 85, 10, 10, (60, 0, 0)), fist=(1, 0.0), legs='stand', hry=-10)
               .guard(0.65).clip('Sk_Flick'))
    out.append(S().guard(0).k(0.30, (30, 0, 0), R=A(60, 30, -12, 60), L=A(80, 50, 10, 20, (-60, 0, 0)), fist=(1, 0.0), legs='crouch')
               .k(0.75, (-15, 0, 0), (-25, 0, 0), R=A(-30, 30, -12, 50), L=A(-40, 30, 10, 30, (-60, 0, 0)), fist=(1, 0.0), legs='wide')
               .k(1.05, (-16, 0, 0), (-28, 0, 0), R=A(-32, 30, -12, 48), L=A(-42, 30, 10, 28, (-60, 0, 0)), fist=(1, 0.0), legs='wide')
               .guard(1.4).clip('Sk_Summon'))
    return out


def morvanna(st, sk):
    out = []; S = lambda: Seq(st, sk)
    CLAW = (40, 0, 0)
    _staff_common(st, sk, out, CLAW, bias=(9, 0, 5))             # Morvanna : voûtée, tête inclinée
    out.append(S().guard(0).k(0.30, (45, 0, 0), L=A(75, 55, 10, 15, (-50, 0, 0)), fist=(1, 0.0), legs='kneel')
               .k(0.60, (40, 0, 0), L=A(78, 50, 10, 10, (-50, 0, 0)), fist=(1, 0.3), legs='kneel')
               .k(1.00, (-15, 0, 0), (-30, 0, 0), L=A(-40, 40, 10, 40, (-60, 0, 0)), fist=(1, 0.0), legs='wide')
               .guard(1.4).clip('Sk_RaiseDead'))
    out.append(S().guard(0).k(0.20, (-6, 25, 0), L=A(45, 30, 10, 100, (20, 0, 0)), fist=(1, 0.4), legs='stand', hry=15)
               .k(0.38, (20, -15, 0), (-10, 0, 0), L=A(20, 85, 10, 25, (50, 0, 0)), fist=(1, 0.55), legs='lunge', hry=-12)
               .k(0.65, (22, -16, 0), (-12, 0, 0), L=A(20, 86, 10, 30, (55, 0, 0)), fist=(1, 0.7), legs='lunge', hry=-12)
               .guard(1.0).clip('Sk_Curse'))
    out.append(S().guard(0).k(0.25, (14, -10, 0), L=A(22, 85, 10, 10, (50, 0, 0)), fist=(1, 0.3), legs='lunge')
               .k(0.70, (-6, 15, 0), L=A(40, 50, 10, 95, (50, 0, 0)), fist=(1, 0.9), legs='back', hry=12)
               .k(0.95, (-8, 16, 0), L=A(42, 48, 10, 100, (50, 0, 0)), fist=(1, 1.0), legs='back', hry=12)
               .guard(1.3).clip('Sk_Drain'))
    out.append(S().guard(0).k(0.25, (-16, 35, 0), L=A(-50, -30, 10, 100, (20, 0, 0)), fist=(1, 0.7), legs='back', hry=25)
               .k(0.40, (26, -25, 0), L=A(25, 95, 10, 5, CLAW), fist=(1, 0.0), legs='lunge', hry=-20)
               .guard(0.80).clip('Sk_BoneSpear'))
    out.append(S().guard(0).k(0.25, (20, 0, 0), (20, 0, 0), R=A(60, 40, -12, 90), L=A(70, 40, 10, 100), aim=(0.06, 1, 0.12), fist=(1, 0.8), legs='stand')
               .k(0.45, (-20, 0, 0), (-35, 0, 0), R=A(0, -10, -12, 20), L=A(0, -10, 10, 20, CLAW), aim=(0.4, 1, 0), fist=(1, 0.0), legs='wide')
               .k(0.95, (-18, 0, 0), (-33, 0, 0), R=A(2, -10, -12, 20), L=A(2, -10, 10, 20, CLAW), aim=(0.4, 1, 0), fist=(1, 0.0), legs='wide')
               .guard(1.3).clip('Sk_Embrace'))
    out.append(S().guard(0).k(0.30, (40, 0, 0), (40, 0, 0), L=A(60, 60, 10, 120, (30, 0, 0)), fist=(1, 0.6), legs='kneel')
               .k(1.10, (42, 0, 0), (45, 0, 0), L=A(60, 62, 10, 122, (30, 0, 0)), fist=(1, 0.8), legs='kneel')
               .guard(1.5).clip('Sk_Sacrifice'))
    return out


# ------------------------------------------------------------------------------------------------ assemblage
BY_STYLE = {'sword_shield': kael, 'hammer_shield': tharion, 'crossbow': varek}


def build(sk, asset, style, who=None):
    """Ajoute les clips de compétences à asset.clips et renvoie leurs entrées de manifeste."""
    AN._STYLE = style; AN._SK = sk
    fn = {'elyra': elyra, 'morvanna': morvanna}.get(who) or BY_STYLE.get(style.name)
    if fn is None: return []
    clips = fn(style, sk)
    for c in clips:
        simulate_cape(sk, c, [[f'Cape_{ch}{k}' for k in range(1, 5)] for ch in ('L', 'C', 'R')])
        asset.clips[c.name] = c
    return [(c.name, False, False, ()) for c in clips]
