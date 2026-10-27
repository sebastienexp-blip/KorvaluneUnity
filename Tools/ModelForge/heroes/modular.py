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
    rags(sk, a, bank, body, prefix, female, waist, marks)
    bank.recipes[prefix + '_Under'] = (lambda S: KM.linen(S, color=(0.60, 0.55, 0.46)), 512, 256, dict(metallic=0.0, smoothness=1.0))
    a.hides = getattr(a, 'hides', {})
    a.hides['SousVetementBas'] = ['Anatomie']
    a.base_body, a.base_marks, a.base_female = body, marks, female
    return body, marks


def rag_cloth(S=1024, seed=131, color=(0.52, 0.46, 0.36), patches=True, dirt=1.0):
    """Toile grossière usée : tissage lâche, pièces rapiécées (autres teintes) cousues à gros points, accrocs, crasse."""
    from forge.texgen import TexSet, fbm, n01, noise, height_to_normal
    from scipy.ndimage import gaussian_filter
    from PIL import Image, ImageDraw
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float); k = S / 180
    warp = np.sin(xx / k * np.pi) ** 2; weft = np.sin(yy / k * np.pi) ** 2
    hgt = np.where(((np.floor(xx / k) + np.floor(yy / k)) % 2) > 0, warp, weft) + 0.35 * noise(S, S, 2.0, r)
    slub = n01(noise(S, S, 10.0, r, aniso=(0.2, 3.0)))
    col = np.array(color)[None, None] * (0.82 + 0.18 * hgt[..., None]) * (0.88 + 0.18 * slub[..., None])
    if patches:
        im = Image.new('L', (S, S), 0); st = Image.new('L', (S, S), 0); d = ImageDraw.Draw(im); ds = ImageDraw.Draw(st)
        tints = []
        for q in range(5):
            w, h = r.uniform(0.10, 0.22) * S, r.uniform(0.08, 0.18) * S; x, y = r.uniform(0, S - w), r.uniform(0, S - h)
            d.rectangle([x, y, x + w, y + h], fill=q + 1); v_ = r.uniform(0.6, 1.2); tints.append(np.array([v_, v_ * r.uniform(0.92, 1.0), v_ * r.uniform(0.8, 0.95)]))
            for t in np.arange(0, 1, 0.04):                                              # points de couture
                for (x0, y0, x1, y1) in ((x + t * w, y, x + t * w + 6, y + 4), (x + t * w, y + h - 4, x + t * w + 6, y + h), (x, y + t * h, x + 4, y + t * h + 6), (x + w - 4, y + t * h, x + w, y + t * h + 6)):
                    ds.rectangle([x0, y0, x1, y1], fill=255)
        pm = np.asarray(im); sm = np.asarray(st, np.float32) / 255
        for q, tnt in enumerate(tints): col[pm == q + 1] *= tnt * np.array([1.0, 0.95, 0.9])
        col = col * (1 - 0.45 * sm[..., None]); hgt = hgt + 1.2 * (pm > 0) - 0.8 * sm
    grime = np.clip(n01(fbm(S, S, r, base=S / 4, octaves=5)) - 0.45, 0, 1) * 1.6 * dirt
    col = col * (1 - 0.45 * grime[..., None]) + np.array([0.18, 0.14, 0.10]) * 0.3 * grime[..., None]
    holes = np.clip((n01(fbm(S, S, r, base=S / 30, octaves=3)) - 0.86) * 12, 0, 1)
    col = col * (1 - 0.75 * holes[..., None])
    return TexSet(np.clip(col, 0, 1), height_to_normal(gaussian_filter(hgt, 0.7, mode='wrap'), 0.6), np.zeros((S, S)), np.full((S, S), 0.12), np.clip(0.7 + 0.3 * hgt / max(hgt.max(), 1e-6) - 0.4 * holes, 0, 1))


def rope(S=256, seed=133):
    from forge.texgen import TexSet, fbm, n01, height_to_normal
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    tw = (np.sin((xx + yy * 0.8) / S * 2 * np.pi * 10) + 1) / 2
    g = n01(fbm(S, S, r, base=S / 8, octaves=3))
    col = np.array([0.55, 0.45, 0.30])[None, None] * (0.6 + 0.5 * tw[..., None]) * (0.85 + 0.25 * g[..., None])
    return TexSet(np.clip(col, 0, 1), height_to_normal(tw * 2, 0.8), np.zeros((S, S)), np.full((S, S), 0.1), 0.7 + 0.3 * tw)


def rags(sk, a, bank, body, prefix, female, waist, marks=None):
    """Tenue de départ du héros (« presque un vagabond ») : tunique de toile écrue rapiécée à manches courtes et encolure
    lâche, ourlet effiloché sur les hanches, ceinture de corde, braies de laine brune s'arrêtant à mi-mollet, bandes de
    toile enroulées autour des pieds. Portée tant qu'aucun torse / aucune jambière n'est équipé (Unity : HeroOutfit)."""
    V = body.V; P = {n: np.asarray(p, float) for n, p in zip(sk.names, sk.pos)}
    def along(Q, a_, b_):           # position relative le long d'un segment d'os (0 au début, 1 à la fin)
        d = P[b_] - P[a_]; return ((Q - P[a_]) @ d) / (d @ d)
    def side(Q, left, right):       # valeur du côté du sommet (x > 0 : gauche du personnage)
        return np.where(Q[:, 0] > 0, left, right)
    fray = lambda Q, k, amp: amp * (np.sin(Q[:, 0] * k + Q[:, 2] * k * 0.7) + 0.6 * np.sin(Q[:, 0] * k * 2.3 - Q[:, 2] * k * 1.9 + 1.3))
    hem_y = waist - (0.15 if female else 0.16)
    neck_y = P['Neck'][1] - 0.035
    def tunic_in(Q):
        hem = Q[:, 1] - (hem_y + fray(Q, 38, 0.014))
        neck = neck_y - Q[:, 1] - 0.05 * np.exp(-(Q[:, 0] / 0.05) ** 2) * (Q[:, 2] > 0)          # encolure en V lâche devant
        sleeve = 0.45 + fray(Q, 60, 0.03) - side(Q, along(Q, 'LeftUpperArm', 'LeftLowerArm'), along(Q, 'RightUpperArm', 'RightLowerArm'))
        return np.minimum(np.minimum(hem, neck), sleeve)
    coarse = HP.bone_mask(body, sk, ['Hips', 'Spine', 'Chest', 'UpperChest', 'Shoulder', 'UpperArm', 'UpperLeg', 'Neck'], 0.15) & (V[:, 1] > hem_y - 0.08)
    # ample : décalage croissant vers l'ourlet (le tissu tombe), plus serré aux épaules
    loose0 = lambda Q: 0.004 + 0.022 * np.clip((waist + 0.12 - Q[:, 1]) / 0.3, 0, 1) + 0.006 * np.clip((Q[:, 1] - waist) / 0.3, 0, 1)
    if female and marks and 'breasts' in marks:
        # la toile tombe de la poitrine : comble le creux entre les seins et sous la poitrine (pas de tissu « collé »)
        zb = max(c[2] for c in marks['breasts']) - 0.012; yb = float(np.mean([c[1] for c in marks['breasts']]))
        def loose(Q):
            tgt = zb - np.clip(yb - Q[:, 1], 0, None) * 0.32
            w = np.clip((Q[:, 1] - (waist - 0.10)) / 0.08, 0, 1) * np.clip((yb + 0.03 - Q[:, 1]) / 0.03, 0, 1) * np.clip((0.15 - np.abs(Q[:, 0])) / 0.05, 0, 1) * np.clip((Q[:, 2] - (zb - 0.09)) / 0.03, 0, 1)
            return loose0(Q) + np.clip(tgt - Q[:, 2], 0, 0.14) * w
    else:
        loose = loose0
    a.add('haillons_haut', HP.cloth_cut(body, coarse, tunic_in, 0.008, thick=0.003, uv_scale=1.6, bump=loose), prefix + '_Rags', slot='HaillonsHaut')
    def belt_in(Q): return 0.022 - np.abs(Q[:, 1] - (waist - 0.035))
    coarse = HP.bone_mask(body, sk, ['Hips', 'Spine'], 0.2) & (np.abs(V[:, 1] - waist) < 0.12)
    a.add('haillons_corde', HP.cloth_cut(body, coarse, belt_in, 0.028, thick=0.012, uv_scale=8.0, bump=lambda Q: 0.004 * np.sin(Q[:, 0] * 90 + Q[:, 2] * 60)), prefix + '_Rope', slot='HaillonsHaut')
    def braies_in(Q):
        top = waist + 0.015 - Q[:, 1]
        calf = 0.5 + fray(Q, 55, 0.035) - side(Q, along(Q, 'LeftLowerLeg', 'LeftFoot'), along(Q, 'RightLowerLeg', 'RightFoot'))
        return np.minimum(top, calf)
    coarse = HP.bone_mask(body, sk, ['Hips', 'UpperLeg', 'LowerLeg', 'Spine'], 0.15) & (V[:, 1] < waist + 0.05)
    a.add('haillons_bas', HP.cloth_cut(body, coarse, braies_in, 0.0075, thick=0.003, uv_scale=1.6,
                                        bump=lambda Q: 0.006 * np.clip((0.75 - Q[:, 1]) / 0.3, 0, 1)), prefix + '_RagsDark', slot='HaillonsBas')
    def wrap_in(Q):                 # bandes molletières et chaussons de toile
        t = side(Q, along(Q, 'LeftLowerLeg', 'LeftFoot'), along(Q, 'RightLowerLeg', 'RightFoot'))
        return t - 0.74 + 0.012 * np.sin(Q[:, 0] * 70 + Q[:, 2] * 50)
    coarse = HP.bone_mask(body, sk, ['LowerLeg', 'Foot', 'Toes'], 0.2)
    a.add('haillons_pieds', HP.cloth_cut(body, coarse, wrap_in, 0.006, thick=0.003, uv_scale=3.0,
                                         bump=lambda Q: 0.003 * np.abs(np.sin(Q[:, 1] * 160))), prefix + '_RagsDark', slot='HaillonsBas')
    bank.recipes[prefix + '_Rags'] = (lambda S: rag_cloth(S, 131, (0.60, 0.54, 0.43)), 1024, 256, dict(metallic=0.0, smoothness=1.0))
    bank.recipes[prefix + '_RagsDark'] = (lambda S: rag_cloth(S, 132, (0.30, 0.22, 0.15), patches=True, dirt=1.3), 1024, 256, dict(metallic=0.0, smoothness=1.0))
    bank.recipes[prefix + '_Rope'] = (lambda S: rope(S), 256, 128, dict(metallic=0.0, smoothness=1.0))
    a.hides = getattr(a, 'hides', {})
    a.hides['HaillonsHaut'] = ['Peau_Torse', 'SousVetementHaut']
    a.hides['HaillonsBas'] = ['Peau_Bassin', 'SousVetementBas', 'Anatomie']


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
    # pas de blendshapes dans le FBX (trop lourds : chaque curseur déplacerait les 130 000 sommets, armure comprise) :
    # table compacte des déplacements du seul corps, appliquée dans Unity par MorphRig (les pièces suivent le corps)
    a.morph_table = (body.V.copy(), {n: d[body.src] for n, d in D.items()})
    keep = {k.rsplit('_', 1)[0] for k in D}
    a.morphs = [m for m in MO.manifest(sl, female) if m['id'] in keep]
    return a.morphs
