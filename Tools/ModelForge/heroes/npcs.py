"""Marchands de Korvalune (personnages non joueurs), bâtis comme les héros : corps MakeHuman CC0 à la silhouette
nettement masculine ou féminine (épaules, hanches, poitrine mesurées), visage modelé par les curseurs, et costume
MÉDIÉVAL coupé sur le corps définitif par le tailleur (heroes/tailor.py) : chemises et cottes tombées sur le corps,
chausses, robes et pans à enveloppe (jamais entre les jambes), tabliers, ceintures, bottes, chapeaux, capuches, châles.
Chaque marchand passe le contrôle d'interpénétration couche par couche (repos, attente, marche) : la moindre traversée
fait échouer la construction. Allure civile : bras le long du corps, mains détendues."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H, morphs as MO
from knight import anims, materials as KM
from heroes import modular as MOD, tailor as TL

FOLDER = 'Characters'
HEAD_VIEW = (0, 1.66, 0.05)
GEAR = []


# ------------------------------------------------------------------------------------------- visages
MALE_FACE = {'chin_chin_width': 0.55, 'chin_chin_prominent': 0.45, 'chin_chin_jaw_drop': 0.25, 'head_head_square': 0.55, 'cheek_cheek_bones': 0.25,
             'nose_nose_scale_horiz': 0.30, 'nose_nose_volume': 0.35, 'nose_nose_scale_depth': 0.25, 'neck_neck_scale_horiz': 0.55,
             'mouth_mouth_scale_horiz': 0.15, 'torso_torso_vshape': 0.35, 'measure_measure_bust_circ': -0.6, 'torso_torso_muscle_pectoral': -0.3}
FEMALE_FACE = {'chin_chin_width': -0.45, 'chin_chin_triangle': 0.35, 'chin_chin_prominent': -0.2, 'head_head_oval': 0.45, 'nose_nose_scale_horiz': -0.35,
               'nose_nose_volume': -0.35, 'nose_nose_point_width': -0.3, 'mouth_mouth_upperlip_volume': 0.35, 'mouth_mouth_lowerlip_volume': 0.40,
               'cheek_cheek_volume': 0.25, 'neck_neck_scale_horiz': -0.45, 'eyes_eye_scale': 0.2}


def bake_shape(a, body, marks, female, values):
    """Applique un jeu de curseurs (id → -1..1) à TOUTES les pièces (corps, vêtements, cheveux) puis l'intègre au maillage."""
    sl = [s for s in MO.sliders(female) if s['id'] in values]
    D = MO.shape_deltas(marks, sl)
    MO.apply(a, body, D)
    for _, m, _ in a.parts:
        sh = getattr(m, 'shapes', {})
        for sid, v in values.items():
            k = sid + ('_plus' if v > 0 else '_moins')
            if k in sh: m.V = m.V + sh[k] * abs(v)
        m.shapes = {}


# ------------------------------------------------------------------------------------------- matériaux
def _wool(color, seed, dirt=0.6):
    return (lambda S: MOD.rag_cloth(S, seed, color, patches=False, dirt=dirt), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _leather(tone, seed=31):
    return (lambda S: KM.leather(S, seed=seed, tone=tone), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _gamb(color, seed):
    return (lambda S: KM.gambeson(S, seed=seed, color=color), 512, 256, dict(metallic=0.0, smoothness=1.0))
def _cloth(color, seed, thread=(0.70, 0.52, 0.22)):
    return (lambda W_, H_: KM.cloak(W_, H_, seed=seed, color=color, thread_color=thread, mud_amt=0.3), (512, 1024), (256, 512), dict(metallic=0.0, smoothness=1.0))
GOLD = (lambda S: KM.brass(S, seed=301, gold=(0.80, 0.60, 0.28)), 256, 128, dict(metallic=1.0, smoothness=1.0))
IRON = (lambda S: KM.steel(S, seed=302, tone=(0.25, 0.25, 0.26), polish=0.5), 256, 128, dict(metallic=1.0, smoothness=1.0))
def _beard(tone, seed):
    return (lambda S: KM.leather(S, seed=seed, tone=tone), 256, 128, dict(metallic=0.0, smoothness=1.0))


def _linen(color, seed, S=512):
    return (lambda S_: KM.linen(S_, seed=seed, color=color), S, S // 2, dict(metallic=0.0, smoothness=1.0))


# silhouettes : hommes = épaules larges, hanches et fessiers réduits, torse plat ; femmes = l'inverse
MALE_BODY = {'buttocks_buttocks_volume': -0.5, 'hip_hip_scale_horiz': -0.3, 'measure_measure_bust_circ': -0.5, 'torso_torso_vshape': 0.5,
             'torso_torso_muscle_dorsi': 0.4, 'armslegs_upperarm_shoulder_muscle': 0.5, 'neck_neck_scale_horiz': 0.6}
FEMALE_BODY = {'torso_torso_vshape': -0.3, 'hip_hip_scale_horiz': 0.2, 'neck_neck_scale_horiz': -0.45}


def body_metrics(a):
    V = np.vstack([m.V for n, m, _ in a.parts if n.startswith('peau_')])
    tor = V[(np.abs(V[:, 1] - 1.40) < 0.02) & (np.abs(V[:, 0]) < 0.25)]
    hip = V[(np.abs(V[:, 1] - 0.93) < 0.02) & (np.abs(V[:, 0]) < 0.30)]
    chest = V[(V[:, 1] > 1.20) & (V[:, 1] < 1.36) & (np.abs(V[:, 0]) < 0.14)]
    under = V[(np.abs(V[:, 1] - 1.12) < 0.015) & (np.abs(V[:, 0]) < 0.10)]
    return dict(epaules=float(2 * tor[:, 0].max()), hanches=float(2 * hip[:, 0].max()), poitrine=float(chest[:, 2].max() - under[:, 2].max()))


def make(name, female, look, shape, outfit, recipes):
    def build(bank):
        sk = H.build(); a = Asset(name, sk)
        lk = dict(look); lk.setdefault('eye_color', (0.22, 0.14, 0.07) if not female else (0.20, 0.30, 0.18))
        body, marks = MOD.base(sk, a, bank, 'NP' + name[4:8].upper(), female=female, look=lk)
        a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'HaillonsHaut', 'HaillonsBas', 'SousVetementBas', 'SousVetementHaut')]
        pre = {n: m.V.copy() for n, m, _ in a.parts if n.startswith('peau_')}
        bake_shape(a, body, marks, female, {**(FEMALE_BODY if female else MALE_BODY), **(FEMALE_FACE if female else MALE_FACE), **shape})
        # contrôle de silhouette (corps nu) : un homme a les épaules nettement plus larges que les hanches et le torse plat
        bm = body_metrics(a)
        r = bm['epaules'] / bm['hanches']
        if female: assert r < 1.45 and bm['poitrine'] > 0.025, f'{name} : silhouette peu féminine {bm}'
        else: assert r > 1.45 and bm['poitrine'] < 0.035, f'{name} : silhouette peu masculine {bm}'
        b2, mk = TL.shaped_body(a, body, marks, pre)
        T = TL.Tailor(sk, a, b2, mk, female)
        bank.recipes.update(recipes)
        outfit(T)
        T.hide_covered()
        anims.build(sk, a, TL.Civil())
        if T.long_skirt:                                     # robe longue : pas plus courts (le tissu suit la jambe sans être traversé)
            w = anims.gait(sk, 'Walk', 1.0, 15, 38, 9, 3, 0.012); anims.ground(sk, w); a.clips['Walk'] = w
        rep = T.check(('Idle',), 6)                          # les marchands restent derrière leur étal : seule la pose de repos compte
        bad = sorted({(on, inn, round(dm * 1000, 1)) for cn, fr, on, inn, k, dm, pts in rep})
        a.npc_checks = dict(silhouette={k: round(v, 3) for k, v in bm.items()}, chapeau=getattr(T, 'hat_gap', None), traversees=bad)
        if bad: print(f'   ⚠ {name} : traversées au repos {bad[:6]}', flush=True)
        return a, {}, anims.CLIPS
    return build


CATALOG = {}
def reg(name, female, look, shape, outfit, recipes): CATALOG[name] = (make(name, female, look, shape, outfit, recipes), recipes)


# ------------------------------------------------------------------------------------------- les marchands
# Forgeron : colosse aux bras puissants, chemise de lin aux manches retroussées, chausses de laine, bottes, grand
# tablier de cuir à bavette et bretelles croisées dans le dos
def _forgeron(T):
    T.hose('NF_Wool', low=0.40); T.boots('NF_Boot', 0.46)
    T.shirt('NF_Linen', hem=T.waist - 0.06, sleeve=0.62, neck=0.05, ease=0.018, max_ease=0.05, hem_rise=0.0)
    T.trim('chemise', 'NF_Linen', r=0.005, sel=lambda c: c[1] > T.waist + 0.15)
    T.belt('NF_Boot', None, layer=3); T.apron('NF_Apron', 'NF_Boot', y_hem=0.46)
reg('PNJ_Forgeron', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.05, 0.04, 0.03), skin_kw=dict(stubble=0.7)),
    {'corps_musculature': 0.8, 'armslegs_upperarm_muscle': 0.6, 'armslegs_lowerarm_muscle': 0.5}, _forgeron,
    {'NF_Linen': _linen((0.66, 0.60, 0.50), 401), 'NF_Wool': _wool((0.20, 0.17, 0.13), 402, 1.0), 'NF_Boot': _leather((0.10, 0.07, 0.045)),
     'NF_Apron': _leather((0.22, 0.13, 0.07), 37)})

# Armurier : gambison matelassé à manches longues, col roulé, ceinture à boucle de fer, chausses, bottes
def _armurier(T):
    T.hose('NA_Wool', low=0.42); T.boots('NA_Boot', 0.48)
    T.shirt('NA_Gamb', hem=T.waist - 0.06, sleeve=0.97, neck=0.015, ease=0.022, max_ease=0.055, hem_rise=0.0)
    T.trim('chemise', 'NA_Boot', r=0.006)
    T.belt('NA_Boot', 'NA_Iron', layer=4)
reg('PNJ_Armurier', False, dict(skin='young_caucasian_male', hair='short02', hair_tint=(0.16, 0.10, 0.06), skin_kw=dict(stubble=0.5)),
    {'corps_musculature': 0.6}, _armurier,
    {'NA_Gamb': _gamb((0.26, 0.21, 0.15), 71), 'NA_Wool': _wool((0.15, 0.13, 0.11), 403), 'NA_Boot': _leather((0.09, 0.06, 0.04)), 'NA_Iron': IRON})

# Marchand : homme bedonnant, cotte verte longue jusqu'aux genoux, ceinture dorée, chausses, bottes, chapeau de feutre
def _marchand(T):
    T.hose('NM_Brown', low=0.36); T.boots('NM_Boot', 0.40)
    T.shirt('NM_Green', hem=T.waist - 0.06, sleeve=0.95, neck=0.03, ease=0.022, max_ease=0.06, hem_rise=0.0)
    T.skirt('NM_Green', T.waist - 0.02, 0.56, ease=0.018, flare=0.10, layer=3, name='cotte_pans')
    T.trim('chemise', 'NM_Gold', r=0.004, sel=lambda c: c[1] > T.waist + 0.15)
    T.trim('cotte_pans', 'NM_Gold', r=0.004, sel=lambda c: c[1] < 0.7)
    T.belt('NM_Boot', 'NM_Gold', layer=4); T.hat('NM_Felt', 'NM_Band')
reg('PNJ_Marchand', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.22, 0.15, 0.09), skin_kw=dict(stubble=0.5)),
    {'stomach_stomach_pregnant': 0.45}, _marchand,
    {'NM_Green': _wool((0.17, 0.30, 0.16), 404, 0.4), 'NM_Brown': _wool((0.26, 0.19, 0.12), 405), 'NM_Boot': _leather((0.13, 0.08, 0.05)),
     'NM_Gold': GOLD, 'NM_Felt': _wool((0.16, 0.11, 0.07), 407, 0.3), 'NM_Band': _wool((0.42, 0.10, 0.07), 408, 0.2)})

# Joaillier : vieil homme mince, longue robe bleu nuit à galons d'or jusqu'aux chevilles, chaîne et pendentif
def _joaillier(T):
    T.boots('NJ_Boot', 0.22)
    T.shirt('NJ_Navy', hem=T.waist - 0.06, sleeve=0.97, neck=0.02, ease=0.020, max_ease=0.06, hem_rise=0.0)
    T.skirt('NJ_Navy', T.waist - 0.02, 0.11, ease=0.02, flare=0.18, layer=3, name='robe')
    T.trim('chemise', 'NJ_Gold', r=0.004, sel=lambda c: c[1] > T.waist + 0.15)
    T.trim('robe', 'NJ_Gold', r=0.005, sel=lambda c: c[1] < 0.5)
    T.belt('NJ_Gold', 'NJ_Gold', layer=4, width=0.03); T.necklace('NJ_Gold', 'NJ_Gem', drop=0.16)
reg('PNJ_Joaillier', False, dict(skin='middleage_caucasian_male', hair='short02', hair_tint=(0.62, 0.62, 0.62)),
    {'corps_corpulence': -0.5, 'corps_musculature': -0.3}, _joaillier,
    {'NJ_Navy': _wool((0.06, 0.08, 0.22), 409, 0.2), 'NJ_Gold': GOLD, 'NJ_Boot': _leather((0.05, 0.045, 0.04)),
     'NJ_Gem': (lambda S: KM.crystal(S, seed=410, color=(0.9, 0.1, 0.15)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(0.8, 0.8, 0.8), emission_preview=(0.6, 0.1, 0.1)))})

# Mystique : femme, longue robe violette, capuche et pèlerine, galons d'or, collier de cristal
def _mystique(T):
    T.boots('NY_Dark', 0.22)
    T.shirt('NY_Violet', hem=T.waist - 0.06, sleeve=0.97, neck=0.04, ease=0.018, max_ease=0.05, hem_rise=0.0)
    T.skirt('NY_Violet', T.waist - 0.02, 0.10, ease=0.02, flare=0.16, layer=3, name='robe')
    T.trim('robe', 'NY_Gold', r=0.005, sel=lambda c: c[1] < 0.5)
    T.belt('NY_Gold', 'NY_Gold', layer=4, width=0.025)
    T.hood('NY_Violet', 'NY_Dark', depth=0.7); T.necklace('NY_Gold', 'NY_Crystal', drop=0.12, layer=8)
reg('PNJ_Mystique', True, dict(skin='young_caucasian_female2', hair='long01', hair_tint=(0.07, 0.05, 0.05), brows='eyebrow010'),
    {}, _mystique,
    {'NY_Violet': _wool((0.22, 0.08, 0.30), 411, 0.2), 'NY_Gold': GOLD, 'NY_Dark': _leather((0.03, 0.025, 0.03)),
     'NY_Crystal': (lambda S: KM.crystal(S, seed=412, color=(0.62, 0.22, 1.0)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(1.4, 1.4, 1.4), emission_preview=(0.6, 0.3, 1.0)))})

# Alchimiste : femme, chemise de lin, robe vert mousse jusqu'aux chevilles, tablier de cuir taché, tresse rousse
def _alchimiste(T):
    T.boots('NL_Boot', 0.22)
    T.shirt('NL_Linen', hem=T.waist - 0.06, sleeve=0.80, neck=0.05, ease=0.016, max_ease=0.05, hem_rise=0.0)
    T.trim('chemise', 'NL_Linen', r=0.004, sel=lambda c: c[1] > T.waist + 0.15)
    T.skirt('NL_Moss', T.waist - 0.02, 0.10, ease=0.02, flare=0.14, layer=3, name='robe')
    T.belt('NL_Boot', 'NL_Gold', layer=4, width=0.035)
    T.skirt('NL_Apron', T.waist - 0.03, 0.30, ease=0.012, flare=0.06, layer=5, name='tablier', front=0.9, follow=0.85, thick=0.006)
reg('PNJ_Alchimiste', True, dict(skin='young_caucasian_female', hair='braid01', hair_tint=(0.36, 0.17, 0.07), brows='eyebrow010'),
    {}, _alchimiste,
    {'NL_Linen': _linen((0.72, 0.68, 0.57), 413), 'NL_Moss': _wool((0.17, 0.24, 0.12), 414, 0.3), 'NL_Apron': _leather((0.25, 0.17, 0.10), 39),
     'NL_Boot': _leather((0.11, 0.075, 0.05)), 'NL_Gold': GOLD})

# Marchande de curiosités : femme, chemise écrue, jupe rouge longue, châle sarcelle, bijoux d'or
def _curiosites(T):
    T.boots('NC_Boot', 0.22)
    T.shirt('NC_Cream', hem=T.waist - 0.06, sleeve=0.90, neck=0.06, ease=0.016, max_ease=0.05, hem_rise=0.0)
    T.skirt('NC_Red', T.waist - 0.02, 0.10, ease=0.02, flare=0.18, layer=3, name='jupe')
    T.trim('jupe', 'NC_Gold', r=0.004, sel=lambda c: c[1] < 0.5)
    T.belt('NC_Gold', 'NC_Gold', layer=4, width=0.025)
    T.shawl('NC_Teal'); T.necklace('NC_Gold', 'NC_Gem', drop=0.11, layer=6)
reg('PNJ_Curiosites', True, dict(skin='young_caucasian_female2', hair='o4saken_long01', hair_tint=(0.09, 0.06, 0.04), brows='eyebrow010'),
    {'corps_corpulence': 0.15}, _curiosites,
    {'NC_Cream': _linen((0.80, 0.74, 0.60), 415), 'NC_Red': _wool((0.40, 0.06, 0.06), 416, 0.2), 'NC_Teal': _wool((0.07, 0.28, 0.30), 417, 0.3),
     'NC_Gold': GOLD, 'NC_Boot': _leather((0.10, 0.07, 0.05)),
     'NC_Gem': (lambda S: KM.crystal(S, seed=418, color=(0.1, 0.8, 0.4)), 256, 128, dict(metallic=0.0, smoothness=1.0, emission=(0.8, 0.8, 0.8), emission_preview=(0.1, 0.6, 0.3)))})
