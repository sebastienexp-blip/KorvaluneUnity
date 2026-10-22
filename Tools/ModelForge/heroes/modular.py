"""Personnages modulaires : un corps de base (homme / femme, MakeHuman CC0) découpé en zones de peau, et des pièces
d'équipement séparées (un maillage par emplacement, même squelette). Retirer les pièces une à une fait réapparaître
le corps ; une pièce équipée masque la peau et les pièces qu'elle recouvre (relation « hides » écrite dans le manifeste
et appliquée dans Unity par CharacterEquipment).
"""
import re
import numpy as np
from forge import mhbase as MB, anatomy as ANA, humanparts as HP
from forge.geo import vertex_normals
from knight import materials as KM
from heroes.common import mh_character

MALE_BODY = dict(targets=MB.MALE, extra=[('universal-male-young-maxmuscle-averageweight', 0.45), ('stomach-pregnant-decr', 0.5), ('stomach-tone-incr', 0.6)],
                 skin='middleage_caucasian_male', hair='short02', hair_tint=(0.10, 0.07, 0.05), brows='eyebrow001', lashes='eyelashes01',
                 eye_color=(0.62, 0.05, 0.04), skin_kw=dict(pubic=0.75, pubic_col=(0.07, 0.05, 0.035)))
FEMALE_BODY = dict(targets=MB.FEMALE, extra=[('female-young-averagemuscle-averageweight-maxcup-maxfirmness', 0.75), ('stomach-pregnant-decr', 1.0), ('stomach-tone-incr', 0.7)],
                   skin='young_caucasian_female', hair='culturalibre_hair_01', brows='eyebrow010', lashes='eyelashes02',
                   eye_color=(0.45, 0.14, 0.72), skin_kw=dict(pubic=0.45, pubic_col=(0.04, 0.035, 0.035)))

ZONES = ['Tete', 'Torse', 'Bassin', 'Bras', 'AvantBras', 'Mains', 'Cuisses', 'Jambes', 'Pieds']


def _zone_of_bone(n):
    b = n.replace('Left', '').replace('Right', '')
    if b in ('Head', 'Neck') or b.startswith('Eye') or b.startswith('Jaw'): return 'Tete'
    if b in ('Spine', 'Chest', 'UpperChest', 'Shoulder'): return 'Torse'
    if b == 'Hips': return 'Bassin'
    if b == 'UpperArm': return 'Bras'
    if b == 'LowerArm': return 'AvantBras'
    if b == 'Hand' or any(b.startswith(f) for f in ('Thumb', 'Index', 'Middle', 'Ring', 'Little')): return 'Mains'
    if b == 'UpperLeg': return 'Cuisses'
    if b == 'LowerLeg': return 'Jambes'
    if b in ('Foot', 'Toes'): return 'Pieds'
    return 'Torse'


def base(sk, a, bank, prefix, female=False, look=None):
    """Corps de base complet : zones de peau (emplacements Peau_*), cheveux (Cheveux), anatomie (Anatomie, masquée par
    défaut dans Unity), sous-vêtements (SousVetementBas / SousVetementHaut). look : surcharges de MALE_BODY / FEMALE_BODY."""
    cfg = dict(FEMALE_BODY if female else MALE_BODY); cfg.update(look or {})
    body, marks = mh_character(sk, a, bank, prefix, skin_part=False, **cfg)
    body.N = vertex_normals(body)
    # zones de peau : chaque face va à la zone de l'os dominant de ses sommets
    zb = np.array([ZONES.index(_zone_of_bone(n)) for n in sk.names])
    dom = body.J[np.arange(len(body.V)), np.argmax(body.W, 1)]
    vz = zb[dom]
    fz = np.array([np.bincount(r, minlength=len(ZONES)).argmax() for r in vz[body.F]])
    for k, z in enumerate(ZONES):
        sel = fz == k
        if sel.any(): a.add('peau_' + z.lower(), HP.submesh(body, sel), prefix + '_Skin', slot='Peau_' + z)
    a.slot['cheveux'] = 'Cheveux'
    # anatomie et sous-vêtements
    a.add('sexe', ANA.female(body, sk) if female else ANA.male(body, sk), prefix + '_Skin', slot='Anatomie')
    V = body.V
    waist = 0.962 if female else 0.985
    def brief_in(Q):
        hole = 0.884 + 0.050 * np.clip((np.abs(Q[:, 0]) - 0.028) / 0.11, 0, 1) ** 0.9
        return np.minimum(waist - Q[:, 1], Q[:, 1] - hole)
    coarse = HP.bone_mask(body, sk, ['Hips', 'Spine', 'UpperLeg'], 0.2) & (V[:, 1] > 0.84) & (V[:, 1] < waist + 0.03) & (np.abs(V[:, 0]) < 0.25)
    bump = None if female else (lambda Q: 0.030 * np.exp(-((Q[:, 0] / 0.034) ** 2 + ((Q[:, 1] - 0.872) / 0.038) ** 2)) * np.clip(Q[:, 2] / 0.03, 0, 1))
    a.add('sous_vetement_bas', HP.cloth_cut(body, coarse, brief_in, 0.0024, bump=bump), prefix + '_Under', slot='SousVetementBas')
    if female:
        ch = HP.bone_mask(body, sk, ['Chest', 'UpperChest', 'Spine'], 0.25)
        cen = []
        for sx in (1, -1):
            s_ = ch & (V[:, 2] > 0.05) & (V[:, 0] * sx > 0) & (V[:, 1] > 1.18) & (V[:, 1] < 1.40)
            cen.append(V[s_][np.argmax(V[s_][:, 2])])
        marks['breasts'] = cen
        def bra_in(Q):
            cups = np.max([0.072 - np.sqrt((Q[:, 0] - c[0]) ** 2 + ((Q[:, 1] - c[1] - 0.006) / 1.05) ** 2) for c in cen], 0)
            cups = np.where(Q[:, 2] > cen[0][2] - 0.10, cups, -1)
            band = 0.018 - np.abs(Q[:, 1] - (cen[0][1] - 0.062))
            straps = np.max([0.008 - np.abs(Q[:, 0] - c[0] * 0.95) for c in cen], 0)
            straps = np.where((Q[:, 1] > cen[0][1]) & (Q[:, 1] < 1.47), straps, -1)
            return np.maximum(np.maximum(cups, band), straps)
        coarse = (ch | HP.bone_mask(body, sk, ['Shoulder'], 0.3)) & (V[:, 1] > 1.12) & (V[:, 1] < 1.50) & (np.abs(V[:, 0]) < 0.20)
        a.add('sous_vetement_haut', HP.cloth_cut(body, coarse, bra_in, 0.0024), prefix + '_Under', slot='SousVetementHaut')
    bank.recipes[prefix + '_Under'] = (lambda S: KM.linen(S, color=(0.60, 0.55, 0.46)), 512, 256, dict(metallic=0.0, smoothness=1.0))
    a.hides = getattr(a, 'hides', {})
    a.hides['SousVetementBas'] = ['Anatomie']
    a.base_body, a.base_marks, a.base_female = body, marks, female
    return body, marks


def assign(a, rules, default='Equipement'):
    """Range les pièces encore sans emplacement : rules = [(motif regex sur le nom de pièce, emplacement), …]."""
    body_parts = {'yeux', 'cils', 'sourcils', 'dents', 'langue'}
    for n, m, mat in a.parts:
        if n in a.slot or n in body_parts: continue
        for pat, slot in rules:
            if re.search(pat, n):
                a.slot[n] = slot; break
        else:
            a.slot[n] = default


def covers(a, table):
    """table = {emplacement: [emplacements ou zones (Tete, Torse… → Peau_*) masqués quand il est équipé]}."""
    a.hides = getattr(a, 'hides', {})
    for s, lst in table.items():
        a.hides.setdefault(s, [])
        for x in lst:
            t = 'Peau_' + x if x in ZONES else x
            if t not in a.hides[s]: a.hides[s].append(t)


# ------------------------------------------------------------------------------ armure de plates (knight.armor)
KNIGHT_RULES = [
    (r'^casque', 'Casque'), (r'^gorgerin', 'Gorgerin'),
    (r'^(plastron|arete|pansiere|dossiere|sangle|boucle|fauld|culet)', 'Plastron'),
    (r'^epaule', 'Epaulieres'), (r'^(canon|cubitiere)', 'Brassards'),
    (r'^(gantelet|gant_|doigt_|paume)', 'Gantelets'), (r'^tassette', 'Tassettes'),
    (r'^cuissot', 'Cuissots'), (r'^genou', 'Genouilleres'), (r'^greve', 'Greves'),
    (r'^(soleret|talonniere|soulier)', 'Solerets'), (r'^gambison', 'Gambison'),
    (r'^(col$|col_|manche_maille|chausse_maille|jupe_maille)', 'Maille'),
    (r'^cape', 'Cape'), (r'^tabard', 'Tabard'),
]
KNIGHT_COVERS = {
    'Casque': ['Tete', 'Cheveux'], 'Gorgerin': [], 'Plastron': ['Torse', 'SousVetementHaut'], 'Gambison': ['Torse', 'SousVetementHaut'],
    'Maille': ['Bras', 'AvantBras', 'Cuisses', 'Jambes', 'Bassin', 'SousVetementBas', 'Anatomie'],
    'Epaulieres': [], 'Brassards': ['AvantBras'], 'Gantelets': ['Mains'], 'Tassettes': [], 'Cuissots': ['Cuisses'],
    'Genouilleres': [], 'Greves': ['Jambes'], 'Solerets': ['Pieds'], 'Cape': [], 'Tabard': [],
}


def morph(a, body, marks, female):
    """Curseurs de morphologie (blendshapes) sur toutes les pièces de l'asset ; liste des curseurs pour le manifeste."""
    from forge import morphs as MO
    import os
    hands = '/tmp/mhpacks2/hands01_cc0/targets/hands'
    vend = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'third_party', 'makehuman', 'targets', 'hands')
    sl = MO.sliders(female, extra_dirs=[d for d in (vend, hands) if os.path.isdir(d)][:1])
    D = MO.shape_deltas(marks, sl)
    MO.apply(a, body, D)
    keep = {k.rsplit('_', 1)[0] for k in D}
    a.morphs = [m for m in MO.manifest(sl, female) if m['id'] in keep]
    return a.morphs
