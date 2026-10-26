"""Kael Draven — le Chevalier déchu : armure de plates d'acier bruni rehaussée d'or, cape et tabard rouges, épée longue, écu."""
from forge.rig import Asset
from forge import humanoid as H
from knight import armor, cape, anims, weapons, materials as KM
from forge import fitloft as FL
from heroes import modular as MOD
from heroes import skill_anims as SA

NAME = 'KaelDraven'
RECIPES = {
    'KN_Steel':    (KM.steel,     2048, 512, dict(metallic=1.0, smoothness=1.0)),
    'KN_Brass':    (KM.brass,     1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'KN_Leather':  (KM.leather,   1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'KN_Mail':     (KM.chainmail, 1024, 512, dict(metallic=1.0, smoothness=1.0)),
    'KN_Gambeson': (KM.gambeson,  1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'KN_Cloak':    (KM.cloak,     (1024, 2048), (256, 512), dict(metallic=1.0, smoothness=1.0)),
    'KN_Void':     (KM.void,      64, 64, dict(metallic=0.0, smoothness=1.0)),
    'WP_Blade':    (KM.blade,     1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'WP_Wood':     (KM.wood,      1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'Longsword'), ('Socket_Shield_L', 'HeaterShield')]


def build(bank, female=False):
    sk = H.build()
    a = Asset(NAME + ('_F' if female else '_M'), sk)
    # corps de base masculin (peau, yeux rouges, cheveux bruns) sous l'armure ; chaque pièce est détachable
    body, marks = MOD.base(sk, a, bank, 'KDF' if female else 'KDM', female=female, look=None if female else dict(hair='short02', hair_tint=(0.06, 0.045, 0.035)))
    with FL.fitted(body, sk, female=female):
        armor.build(sk, a)
    cape.build(sk, a); cape.build_tabard(sk, a)
    MOD.assign(a, MOD.KNIGHT_RULES)
    MOD.covers(a, MOD.KNIGHT_COVERS)
    anims.build(sk, a)
    sk_clips = SA.build(sk, a, anims._STYLE, None)
    gear = {'Longsword': weapons.sword(), 'HeaterShield': weapons.shield()}
    return a, gear, anims.CLIPS + sk_clips
