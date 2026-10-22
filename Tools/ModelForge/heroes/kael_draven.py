"""Kael Draven — le Chevalier déchu : armure de plates d'acier bruni rehaussée d'or, cape et tabard rouges, épée longue, écu."""
from forge.rig import Asset
from forge import humanoid as H
from knight import armor, cape, anims, weapons, materials as KM

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


def build(bank):
    sk = H.build()
    a = Asset(NAME, sk)
    armor.build(sk, a)
    cape.build(sk, a); cape.build_tabard(sk, a)
    anims.build(sk, a)
    gear = {'Longsword': weapons.sword(), 'HeaterShield': weapons.shield()}
    return a, gear, anims.CLIPS
