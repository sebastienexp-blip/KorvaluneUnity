"""Animations ajoutées pour le nouveau contenu, communes à tous les héros (30 i/s, sur place) :
  Sk_Poing  — combat à mains nues : garde haute, direct du gauche, puis crochet du droit en pivotant ;
  Sk_TirArc — tir à l'arc : bras gauche tendu vers la cible (l'arc est tenu à gauche), main droite qui encoche,
              bande la corde jusqu'à la joue, lâcher et léger recul.
Les poses partent de la garde du héros et y reviennent (fondu propre vers la locomotion)."""
import numpy as np
from heroes.skill_anims import Seq

GUARD_UP = dict(R=dict(down=80, fwd=34, twist=-35, elbow=128), L=dict(down=80, fwd=40, twist=35, elbow=124), fist=(1.0, 1.0))


def punch(sk, st):
    s = Seq(st, sk)
    s.k(0.00, **GUARD_UP)
    s.k(0.12, sp=(4, -14, 0), R=GUARD_UP['R'], L=dict(down=76, fwd=88, twist=10, elbow=8), fist=(1.0, 1.0), legs='stand', hry=-10)        # direct du gauche
    s.k(0.26, sp=(2, -4, 0), **GUARD_UP)
    s.k(0.44, sp=(8, 26, 0), R=dict(down=74, fwd=90, twist=-10, elbow=10), L=GUARD_UP['L'], fist=(1.0, 1.0), legs='wide', hry=24, hz=0.06)   # direct du droit
    s.k(0.62, sp=(6, 18, 0), R=dict(down=72, fwd=70, twist=-15, elbow=60), L=GUARD_UP['L'], fist=(1.0, 1.0), legs='wide', hry=14, hz=0.04)
    s.k(0.86, **GUARD_UP)
    return s.clip('Sk_Poing')


def bow_shot(sk, st):
    s = Seq(st, sk)
    aimL = dict(down=8, fwd=86, twist=0, elbow=6)                                       # bras d'arc tendu à hauteur d'épaule
    s.k(0.00)
    s.k(0.18, sp=(2, -20, 0), head=(0, 30, 0), L=aimL, R=dict(down=20, fwd=70, twist=0, elbow=70), fist=(0.6, 1.0), legs='wide', hry=-35)       # encoche
    s.k(0.42, sp=(0, -30, -4), head=(0, 45, 0), L=aimL, R=dict(down=6, fwd=40, twist=-20, elbow=150), fist=(0.6, 1.0), legs='wide', hry=-45)    # bander jusqu'à la joue
    s.k(0.52, sp=(0, -30, -4), head=(0, 45, 0), L=aimL, R=dict(down=6, fwd=40, twist=-20, elbow=150), fist=(0.6, 1.0), legs='wide', hry=-45)    # visée
    s.k(0.58, sp=(-3, -32, -4), head=(0, 45, 0), L=dict(down=6, fwd=84, twist=0, elbow=10), R=dict(down=10, fwd=15, twist=-25, elbow=120), fist=(0.2, 1.0), legs='wide', hry=-46, hz=-0.03)  # lâcher
    s.k(0.88, sp=(1, -10, 0), head=(0, 12, 0), legs='stand', hry=-12)
    s.k(1.05)
    return s.clip('Sk_TirArc')


def ride(sk, st):
    """En selle : assis, buste droit légèrement penché, mains basses aux rênes, petit balancement au rythme de la monture."""
    s = Seq(st, sk)
    reins = dict(down=55, fwd=48, twist=0, elbow=75)
    for i, t in enumerate((0.0, 0.25, 0.5, 0.75, 1.0)):
        b = 3 * np.sin(i * np.pi / 2)
        s.k(t, sp=(6 + b, 0, 0), head=(-4 - b * 0.5, 0, 0), R=reins, L=reins, fist=(1.0, 1.0), legs='ride')
    return s.clip('Sk_Monte')


def bow_hold(sk, st):
    """Arc en main (haut du corps, en boucle) : l'arc tenu bas dans la main gauche, la droite libre près du carquois."""
    s = Seq(st, sk)
    for i, t in enumerate((0.0, 1.0, 2.0)):
        b = 1.5 * np.sin(i * np.pi)
        s.k(t, sp=(2 + b, -6, 0), head=(-1, 6, 0), L=dict(down=70, fwd=22, twist=25, elbow=22), R=dict(down=78, fwd=6, twist=-15, elbow=24 + b * 2), fist=(0.45, 1.0))
    return s.clip('Sk_ArcGarde')


CLIPS = [('Sk_Poing', False, False, (0.12, 0.44)), ('Sk_TirArc', False, False, (0.58,)), ('Sk_Monte', True, False, ()), ('Sk_ArcGarde', True, False, ())]


def build(sk, style):
    return [punch(sk, style), bow_shot(sk, style), ride(sk, style), bow_hold(sk, style)]
