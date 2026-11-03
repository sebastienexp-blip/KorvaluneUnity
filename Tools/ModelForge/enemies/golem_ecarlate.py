"""Golem écarlate — gardien du Canyon Écarlate : colosse de grès rouge strié (même anatomie massive que le démon), plaques
de roche fendues d'où suinte une lueur d'ambre en fusion ; rochers soudés aux épaules et au dos, dalles taillées sur
la poitrine et les avant-bras gravées de runes anciennes, éclats de cristal ambré ; pas d'arme : il frappe de ses
poings de pierre. Yeux d'ambre incandescents, mâchoire de pierre."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, sculpt as SC
from forge.geo import concat, rigid, mirror, unit
from knight import anims, materials as KM
from enemies import materials as EM, demon_brute as DB, colosse_givre as CG

NAME = 'GolemEcarlate'
FOLDER = 'Enemies'
SCALE = 1.55
MAX_TRIS = 180000
HEAD_VIEW = (0, 1.62, 0.10)
RECIPES = {
    'GE_Skin':    (lambda S: EM.stone_golem(S, seed=1401), 2048, 1024, dict(metallic=0.0, smoothness=1.0, emission=(2.0, 2.0, 2.0), emission_preview=(1.1, 1.1, 1.1))),
    'GE_Rock':    (lambda S: EM.stone_golem(S, seed=1402), 1024, 512, dict(metallic=0.0, smoothness=1.0, emission=(1.4, 1.4, 1.4), emission_preview=(0.8, 0.8, 0.8))),
    'GE_Slab':    (lambda S: EM.tinted(EM.lava_rock(S, seed=1403, tone=(0.50, 0.36, 0.26), lava=(1.0, 0.70, 0.25), cells=12, glow_amt=0.35), (1.0, 0.95, 0.9)), 1024, 512, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.9, 0.9, 0.9))),
    'GE_Eye':     (lambda S: EM.glow(S, (1.0, 0.62, 0.15)), 64, 64, dict(metallic=0.0, smoothness=1.0, emission=(4.0, 4.0, 4.0), emission_preview=(2.5, 2.5, 2.5))),
    'GE_Claw':    (lambda S: KM.bone(S, seed=1404, tone=(0.18, 0.10, 0.07)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'GE_Tooth':   (lambda S: KM.bone(S, seed=1405, tone=(0.45, 0.30, 0.22)), 256, 128, dict(metallic=0.0, smoothness=1.0)),
    'GE_Ambre':   (lambda S: KM.crystal(S, seed=1406, color=(1.0, 0.55, 0.12), glow=0.9), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(1.6, 1.6, 1.6), emission_preview=(0.9, 0.9, 0.9))),
}
GEAR = []


def boulders(sk, rng):
    """Rochers soudés : grappes de blocs bosselés sur l'épaule (gauche ; la droite par symétrie) et le dos."""
    out = {}
    for bone, c, n, r in (('LeftShoulder', (0.24, 1.58, -0.03), 4, 0.09), ('UpperChest', (0.0, 1.50, -0.16), 5, 0.10)):
        s = SC.Sculpt(k=0.015)
        for k in range(n):
            q = np.asarray(c) + rng.normal(0, r * 0.6, 3); rr = r * rng.uniform(0.6, 1.1)
            s.ell(bone, q, (rr, rr * rng.uniform(0.55, 0.9), rr * rng.uniform(0.6, 0.9)), fwd=unit(rng.normal(0, 1, 3)))
        m = s.mesh(res=0.014, smooth=1)
        # facettes : on écrase les sommets sur quelques plans de clivage (roche taillée, pas des bulles)
        for _ in range(6):
            nrm = unit(rng.normal(0, 1, 3)); c0 = np.asarray(c) + nrm * r * rng.uniform(0.6, 0.9)
            d = (m.V - c0) @ nrm; m.V -= np.outer(np.clip(d, 0, None), nrm)
        rigid(m, sk[bone]); SC.box_uv(m, 0.35); out[bone] = m
    return out


def slabs(sk):
    """Dalles taillées : un large plastron de pierre plat aux arêtes vives sur la poitrine, brassards sur les avant-bras."""
    def plate(c, half, n, bone):
        # dalle taillée : bloc arrondi puis tranché à plat devant / derrière et sur les côtés (arêtes vives)
        hx, hy, hz = half
        m = SC.Sculpt(k=0.004).ell(bone, (0, 0, 0), (hx * 1.15, hy * 1.2, hz * 3.0)).mesh(res=0.006, smooth=1)
        m.V[:, 2] = np.clip(m.V[:, 2], -hz, hz); m.V[:, 0] = np.clip(m.V[:, 0], -hx, hx); m.V[:, 1] = np.clip(m.V[:, 1], -hy, hy)
        R = SC.rot_from(unit(np.asarray(n, float)))
        m.V = m.V @ R.T + np.asarray(c)
        rigid(m, sk[bone]); SC.box_uv(m, 0.25); return m
    chest = concat([plate((0, 1.33, 0.175), (0.17, 0.10, 0.022), (0, 0.15, 1), 'Chest'), plate((0, 1.18, 0.150), (0.12, 0.05, 0.018), (0, -0.1, 1), 'Spine')])
    arm = plate((0.55, 1.535, 0.0), (0.11, 0.022, 0.06), (0, 1, 0), 'LeftLowerArm')
    return chest, arm


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk); sw = H.swap_table(sk); rng = np.random.default_rng(1400)
    m = DB.body(sk)
    a.add('corps', m, 'GE_Skin')
    e, tt, claws, toes = DB.face_details(sk, m)
    a.add('yeux', e, 'GE_Eye'); a.add('crocs', tt, 'GE_Tooth')
    for k, c in enumerate(claws): a.add(f'griffe{k}', c, 'GE_Claw'); a.add(f'griffe{k}_R', mirror(c, sw), 'GE_Claw')
    a.add('griffes_pied', toes, 'GE_Claw'); a.add('griffes_pied_R', mirror(toes, sw), 'GE_Claw')
    for bone, bm in boulders(sk, rng).items():
        a.add(f'rocher_{bone}', bm, 'GE_Rock')
        if bone.startswith('Left'): a.add(f'rocher_{bone}_R', mirror(bm, sw), 'GE_Rock')
    chest, arm = slabs(sk)
    a.add('plastron', chest, 'GE_Slab'); a.add('brassard', arm, 'GE_Slab'); a.add('brassard_R', mirror(arm, sw), 'GE_Slab')
    for bone, gm in CG.ice_growths(sk, rng).items():                              # éclats d'ambre (mêmes amas que la glace du colosse)
        a.add(f'ambre_{bone}', gm, 'GE_Ambre')
        if bone.startswith('Left'): a.add(f'ambre_{bone}_R', mirror(gm, sw), 'GE_Ambre')
    anims.build(sk, a)
    return a, {}, anims.CLIPS
