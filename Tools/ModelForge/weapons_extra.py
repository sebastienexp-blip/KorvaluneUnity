"""Armes et mains gauches supplémentaires (une par type d'objet) : arcs, carquois, faux, dague, hache, fléau, sceptre,
épée bâtarde, orbe, grimoire, phylactère. FBX statiques, origine au point de prise.
Convention : arme le long de +Y (prise à l'origine) ; arc tenu au centre, branches selon ±Y, corde vers −Z ;
objets de main gauche : origine dans la paume ; carquois : origine au point d'accroche dans le dos (+Y vers le haut).

  python3 weapons_extra.py            # export vers Assets/Korvalune/Art/Weapons/<Nom>
  python3 weapons_extra.py --preview  # planche d'aperçu seulement
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from forge.geo import Loft, Mesh, spline, curve, tube, rivet, concat, unit
from forge.rig import Asset
from forge import export as EX
from heroes.common import TexBank
from heroes import kael_draven as KD, varek as VR, elyra as EL

WOOD, STEEL, BRASS, LEATHER, BLADE = 'WP_Wood', 'KN_Steel', 'KN_Brass', 'KN_Leather', 'WP_Blade'
BRONZE, GOLD, METAL, DARK, CRYSTAL = 'VR_Bronze', 'VR_Gold', 'VR_Metal', 'VR_Dark', 'EL_Crystal'
RECIPES = {**KD.RECIPES, **VR.RECIPES, **EL.RECIPES}


def smooth(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def ring(y, r, w=0.004, mat=BRASS, a=None, x=0.0, z=0.0):
    pts = [(x + np.cos(t) * r, y, z + np.sin(t) * r) for t in np.linspace(0, 2 * np.pi, 24)]
    return tube(pts, w, segs=8, cap=False)


# ------------------------------------------------------------------------------------------------ arcs
def bow(name='ArcLong', H=1.55, recurve=0.0, mats=(WOOD, LEATHER, DARK)):
    """Arc : branches effilées qui s'incurvent vers l'avant (+Z), poignée gainée de cuir, corde tendue à l'arrière."""
    a = Asset(name)
    n = 61; t = np.linspace(-1, 1, n); ay = np.abs(t)
    Y = t * H / 2
    Z = 0.10 * ay ** 1.8 - recurve * 0.12 * smooth((ay - 0.78) / 0.22)          # courbure, puis pointes retournées (arc recourbé)
    rf = 0.011 * (1 - 0.65 * ay ** 1.3) + 0.0025                               # épaisseur
    rs = 0.020 * (1 - 0.55 * ay ** 1.5) + 0.004                                # largeur
    a.add('branches', Loft(np.stack([np.zeros(n), Y, Z], 1), rf, rs, p=2.4, segs=12, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.2), mats[0])
    a.add('poignee', Loft(spline([(0, -0.07, 0.0), (0, 0.07, 0.0)], 8), 0.017, 0.021, p=2.2, segs=16, ref=(0, 0, 1)).mesh(0.05), mats[1])
    a.add('repose_fleche', Loft(spline([(0.012, 0.075, -0.005), (0.022, 0.085, -0.005)], 4), 0.004, 0.006, p=2, segs=6, cap1=1).mesh(0.03), mats[1])
    tip = Y[-1] - 0.01; zt = Z[-1] - 0.004
    a.add('corde', tube([(0, -tip, zt - 0.006), (0, 0, -0.012), (0, tip, zt - 0.006)], 0.0013, segs=5), mats[2])
    for s in (-1, 1):
        a.add(f'embout{s}', Loft(spline([(0, s * (tip - 0.03), zt), (0, s * (tip + 0.012), zt)], 5), 0.006, 0.008, p=2, segs=8, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.03), BRASS)
    return a


def quiver(name='Carquois'):
    """Carquois de cuir porté dans le dos : fourreau tronconique, bandes de cuir, une douzaine de flèches empennées."""
    a = Asset(name); L = 0.62
    a.add('fourreau', Loft(spline([(0, -L, 0), (0, -L * 0.5, 0), (0, 0, 0)], 20), curve([(0, .035), (1, .055)], 20), curve([(0, .045), (1, .07)], 20),
                           p=2.2, segs=20, ref=(0, 0, 1), cap0=2).mesh(0.15), LEATHER)
    for y in (-0.55, -0.32, -0.08, 0.0):
        k = 1 + y / L * 0.4
        a.add(f'bande{y}', tube([(np.cos(t) * 0.058 * k, y, np.sin(t) * 0.073 * k) for t in np.linspace(0, 2 * np.pi, 24)], 0.006, segs=6, cap=False), BRONZE)
    rng = np.random.default_rng(3)
    for i in range(12):
        x, z = rng.uniform(-0.035, 0.035), rng.uniform(-0.045, 0.045); h = rng.uniform(0.16, 0.24)
        a.add(f'fleche{i}', tube([(x, -0.2, z), (x * 1.1, h, z * 1.1)], 0.004, segs=5), WOOD)
        for k in range(3):                                         # empennage : trois plumes
            ang = k * 2 * np.pi / 3 + rng.uniform(0, 1); d = np.array([np.cos(ang), 0, np.sin(ang)]) * 0.016
            base = np.array([x * 1.1, h - 0.07, z * 1.1])
            V = np.array([base, base + d, base + d + (0, 0.065, 0), base + (0, 0.075, 0)])
            a.add(f'plume{i}_{k}', Mesh(np.vstack([V, V]), np.array([[0, 1, 2], [0, 2, 3], [4, 6, 5], [4, 7, 6]]), np.tile([[0, 0], [1, 0], [1, 1], [0, 1]], (2, 1))), DARK)
    return a


# ------------------------------------------------------------------------------------------------ armes de mêlée
def scythe(name='Faux'):
    """Faux de nécromancienne : long manche de bois sombre, poignée latérale, lame courbe d'acier noirci."""
    a = Asset(name)
    a.add('manche', Loft(spline([(0, -0.55, 0), (0, 0.4, 0.02), (0, 1.35, -0.03)], 30), 0.016, 0.016, p=2, segs=12, cap0=2, cap1=2).mesh(0.3), WOOD)
    a.add('poignee', Loft(spline([(0, 0.25, 0.01), (0, 0.27, 0.12)], 6), 0.011, 0.011, p=2, segs=10, cap1=2).mesh(0.05), LEATHER)
    t = np.linspace(0, 1, 40)
    X = -0.75 * t ** 1.05; Y = 1.30 + 0.16 * np.sin(t * np.pi * 0.9) - 0.22 * t ** 2; Z = np.full(40, -0.03)
    a.add('lame', Loft(np.stack([X, Y, Z], 1), 0.004 * (1 - 0.7 * t) + 0.0008, 0.07 * (1 - t ** 1.4) + 0.004, p=1.6, segs=10, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.2), BLADE)
    a.add('collier', ring(1.30, 0.022, 0.007), BRONZE)
    a.add('talon', Loft(spline([(0, -0.55, 0), (0, -0.62, 0)], 4), curve([(0, .02), (1, .004)], 4), curve([(0, .02), (1, .004)], 4), p=2, segs=10, cap1=1).mesh(0.03), BRONZE)
    return a


def dagger(name='DagueRituelle'):
    """Dague rituelle : lame d'os courbe gravée, garde en croissant, poignée de cuir et pommeau en crâne stylisé."""
    a = Asset(name)
    t = np.linspace(0, 1, 30); X = 0.03 * t ** 2; Y = 0.07 + 0.26 * t
    a.add('lame', Loft(np.stack([X, Y, np.zeros(30)], 1), 0.004 * (1 - 0.7 * t) + 0.0006, 0.021 * (1 - t ** 1.5) + 0.002, p=1.7, segs=10, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.1), BRASS)
    a.add('garde', Loft(spline([(-0.05, 0.08, 0), (0, 0.06, 0), (0.05, 0.08, 0)], 12), 0.008, 0.01, p=2.2, segs=10, ref=(0, 0, 1), cap0=1, cap1=1).mesh(0.05), DARK)
    a.add('poignee', Loft(spline([(0, 0.06, 0), (0, -0.06, 0)], 10), 0.013, 0.013, p=2, segs=12).mesh(0.04), LEATHER)
    a.add('pommeau', Loft(spline([(0, -0.06, 0), (0, -0.10, 0)], 8), curve([(0, .014), (.5, .022), (1, .006)], 8), curve([(0, .014), (.5, .02), (1, .006)], 8), p=2.2, segs=14, cap1=1).mesh(0.04), BRASS)
    return a


def axe(name='HacheDeGuerre'):
    """Hache de guerre : manche frêné cerclé, fer à large tranchant en croissant, contre-pointe."""
    a = Asset(name)
    a.add('manche', Loft(spline([(0, -0.35, 0), (0, 0.3, 0), (0, 0.72, 0)], 20), 0.016, 0.018, p=2, segs=12, cap0=2, cap1=2).mesh(0.2), WOOD)
    for y in (-0.3, 0.0, 0.55): a.add(f'cercle{y}', ring(y, 0.02, 0.005), STEEL)
    u = np.linspace(-1, 1, 30)
    X = 0.06 + 0.17 * (1 - u ** 2) ** 0.35; Y = 0.62 + 0.13 * u
    a.add('fer', Loft(np.stack([X * 0 + 0.0, Y, np.zeros(30)], 1), 0.006, 0.01, p=2, segs=6).mesh(0.1), STEEL)
    # le fer : grande plaque en éventail, du manche au tranchant
    V = []; F = []; UV = []
    for i, uu in enumerate(u):
        y = 0.62 + 0.13 * uu * (0.4 + 0.6 * (1 - abs(uu)) ** 0.2); xe = 0.25 - 0.05 * uu ** 2
        for k, (x, th) in enumerate(((0.02, 0.012), (xe, 0.0015))):
            V.append((x, 0.62 + (y - 0.62) * (0.35 if k == 0 else 1.0), th)); V.append((x, 0.62 + (y - 0.62) * (0.35 if k == 0 else 1.0), -th))
            UV.append((k, (uu + 1) / 2)); UV.append((k, (uu + 1) / 2))
    V = np.array(V); n = len(u)
    for i in range(n - 1):
        a0, b0 = i * 4, (i + 1) * 4
        F += [[a0, b0, b0 + 2], [a0, b0 + 2, a0 + 2], [a0 + 1, a0 + 3, b0 + 3], [a0 + 1, b0 + 3, b0 + 1], [a0 + 2, b0 + 2, b0 + 3], [a0 + 2, b0 + 3, a0 + 3]]
    a.add('lame', Mesh(V, np.array(F), np.array(UV)), BLADE)
    a.add('contre_pointe', Loft(spline([(-0.02, 0.62, 0), (-0.11, 0.6, 0)], 6), curve([(0, .012), (1, .002)], 6), curve([(0, .03), (1, .004)], 6), p=2, segs=8, ref=(0, 0, 1), cap1=1).mesh(0.05), STEEL)
    return a


def flail(name='FleauDArmes'):
    """Fléau d'armes : manche, anneau, chaîne de maillons, boule hérissée de pointes et soleil gravé doré."""
    a = Asset(name)
    a.add('manche', Loft(spline([(0, -0.2, 0), (0, 0.28, 0)], 12), 0.017, 0.017, p=2, segs=12, cap0=2, cap1=2).mesh(0.1), WOOD)
    a.add('cercle', ring(0.26, 0.02, 0.006), GOLD)
    for k in range(6):
        y = 0.31 + k * 0.045
        a.add(f'maillon{k}', tube([(np.cos(t) * 0.012 * (k % 2 == 0) + 0.0, y + np.sin(t) * 0.022, np.cos(t) * 0.012 * (k % 2 == 1)) for t in np.linspace(0, 2 * np.pi, 16)], 0.0035, segs=6, cap=False), STEEL)
    c = np.array([0, 0.62, 0]); r = 0.075
    a.add('boule', Loft(spline([c - (0, r, 0), c, c + (0, r, 0)], 16), curve([(0, .0), (.5, r), (1, .0)], 16), curve([(0, .0), (.5, r), (1, .0)], 16), p=2, segs=20, cap0=1, cap1=1).mesh(0.1), STEEL)
    pts = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1), (.7, .7, 0), (-.7, .7, 0), (.7, -.7, 0), (-.7, -.7, 0), (0, .7, .7), (0, -.7, -.7), (.7, 0, .7), (-.7, 0, -.7)]
    for i, d in enumerate(pts):
        d = unit(np.array(d, float))
        a.add(f'pointe{i}', Loft(spline([c + d * r * 0.9, c + d * (r + 0.05)], 4), curve([(0, .012), (1, .001)], 4), curve([(0, .012), (1, .001)], 4), p=2, segs=8, cap1=1).mesh(0.02), STEEL)
    return a


def scepter(name='Sceptre'):
    """Sceptre d'archimage : hampe de métal noir torsadée d'or, griffes qui enserrent un cristal violet flottant."""
    a = Asset(name)
    a.add('hampe', Loft(spline([(0, -0.25, 0), (0, 0.3, 0), (0, 0.55, 0)], 20), curve([(0, .011), (.8, .013), (1, .02)], 20), curve([(0, .011), (.8, .013), (1, .02)], 20), p=2, segs=12, cap0=2, cap1=2).mesh(0.2), DARK)
    t = np.linspace(0, 1, 60)
    a.add('torsade', tube(np.stack([np.cos(t * 14) * 0.014, -0.2 + 0.7 * t, np.sin(t * 14) * 0.014], 1), 0.003, segs=6), GOLD)
    for k in range(4):
        ang = k * np.pi / 2
        a.add(f'griffe{k}', tube([(np.cos(ang) * 0.02, 0.55, np.sin(ang) * 0.02), (np.cos(ang) * 0.06, 0.62, np.sin(ang) * 0.06), (np.cos(ang) * 0.03, 0.72, np.sin(ang) * 0.03)], 0.0045, segs=6), GOLD)
    c = np.array([0, 0.665, 0])
    a.add('cristal', Loft(spline([c - (0, 0.06, 0), c, c + (0, 0.07, 0)], 8), curve([(0, .0), (.45, .035), (1, .0)], 8), curve([(0, .0), (.45, .035), (1, .0)], 8), p=1.2, segs=6, cap0=1, cap1=1).mesh(0.05), CRYSTAL)
    return a


def bastard_sword(name='EpeeBatarde'):
    """Épée bâtarde : l'épée longue, lame allongée et fusée à une main et demie."""
    from knight import weapons as KW
    a = KW.sword(); a.name = name
    for nm, m, mat in a.parts:
        if nm in ('lame',): m.V[:, 1] = np.where(m.V[:, 1] > 0.11, 0.115 + (m.V[:, 1] - 0.115) * 1.18, m.V[:, 1])
        if nm in ('fusee', 'pommeau', 'pommeau_rivet') or nm.startswith('bague'): m.V[:, 1] = np.where(m.V[:, 1] < 0.08, m.V[:, 1] * 1.35, m.V[:, 1])
    return a


# ------------------------------------------------------------------------------------------------ mains gauches
def orb(name='Orbe'):
    """Orbe arcanique tenu au-dessus de la paume : sphère de cristal, anneaux d'or en orbite."""
    a = Asset(name); c = np.array([0, 0.11, 0]); r = 0.065
    a.add('sphere', Loft(spline([c - (0, r, 0), c, c + (0, r, 0)], 16), curve([(0, .0), (.5, r), (1, .0)], 16), curve([(0, .0), (.5, r), (1, .0)], 16), p=2, segs=24, cap0=1, cap1=1).mesh(0.1), CRYSTAL)
    for k, tilt in enumerate((0.0, 1.0, 2.1)):
        pts = []
        for t in np.linspace(0, 2 * np.pi, 32):
            p = np.array([np.cos(t) * 0.09, 0, np.sin(t) * 0.09]); ct, st = np.cos(tilt), np.sin(tilt)
            pts.append(c + np.array([p[0], p[2] * st, p[2] * ct]))
        a.add(f'anneau{k}', tube(pts, 0.0035, segs=6, cap=False), GOLD)
    a.add('socle', Loft(spline([(0, 0.0, 0), (0, 0.04, 0)], 4), curve([(0, .02), (1, .035)], 4), curve([(0, .02), (1, .035)], 4), p=2, segs=12, cap0=1).mesh(0.03), GOLD)
    return a


def grimoire(name='Grimoire'):
    """Grimoire relié de cuir sombre, coins et fermoir de bronze, tranche de pages claires."""
    a = Asset(name); W, H, T = 0.17, 0.23, 0.06
    def box(nm, cx, cy, cz, sx, sy, sz, mat):
        V = np.array([[cx + x * sx / 2, cy + y * sy / 2, cz + z * sz / 2] for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)])
        F = np.array([[0, 1, 3], [0, 3, 2], [4, 6, 7], [4, 7, 5], [0, 4, 5], [0, 5, 1], [2, 3, 7], [2, 7, 6], [0, 2, 6], [0, 6, 4], [1, 5, 7], [1, 7, 3]])
        a.add(nm, Mesh(V, F, V[:, [0, 1]] * 4), mat)
    box('couverture', 0, 0.12, 0, W, H, T, LEATHER)
    box('pages', 0.004, 0.12, 0, W - 0.01, H - 0.012, T - 0.012, 'VR_Bone' if 'VR_Bone' in RECIPES else BRASS)
    box('dos', -W / 2, 0.12, 0, 0.012, H, T + 0.004, DARK)
    box('fermoir', W / 2, 0.12, 0, 0.012, 0.04, T + 0.006, BRONZE)
    for sy in (-1, 1):
        for sz in (-1, 1): box(f'coin{sy}{sz}', W / 2 - 0.015, 0.12 + sy * (H / 2 - 0.015), sz * T / 2, 0.035, 0.035, 0.006, BRONZE)
    return a


def phylactery(name='Phylactere'):
    """Phylactère de nécromancienne : fiole d'os et d'argent, âme verte captive, chaîne."""
    a = Asset(name); c = np.array([0, 0.1, 0])
    a.add('fiole', Loft(spline([c - (0, 0.07, 0), c, c + (0, 0.08, 0)], 16), curve([(0, .02), (.35, .045), (.8, .03), (1, .012)], 16), curve([(0, .02), (.35, .045), (.8, .03), (1, .012)], 16), p=2, segs=16, cap0=1, cap1=1).mesh(0.06), CRYSTAL)
    for k in range(4):
        ang = k * np.pi / 2
        a.add(f'arete{k}', tube([c + (np.cos(ang) * 0.02, -0.07, np.sin(ang) * 0.02), c + (np.cos(ang) * 0.05, 0.0, np.sin(ang) * 0.05), c + (np.cos(ang) * 0.015, 0.085, np.sin(ang) * 0.015)], 0.005, segs=6), BRASS)
    a.add('bouchon', Loft(spline([c + (0, 0.08, 0), c + (0, 0.12, 0)], 5), curve([(0, .016), (1, .008)], 5), curve([(0, .016), (1, .008)], 5), p=2, segs=10, cap1=1).mesh(0.03), BRONZE)
    a.add('chaine', tube([c + (0, 0.12, 0), c + (0.03, 0.17, 0), c + (0.07, 0.15, 0)], 0.0025, segs=5), METAL)
    return a


BUILDERS = {
    'ArcLong': lambda: bow('ArcLong', 1.55, 0.0), 'ArcDeChasse': lambda: bow('ArcDeChasse', 1.15, 1.0, (WOOD, LEATHER, DARK)),
    'Carquois': quiver, 'Faux': scythe, 'DagueRituelle': dagger, 'HacheDeGuerre': axe, 'FleauDArmes': flail,
    'Sceptre': scepter, 'EpeeBatarde': bastard_sword, 'Orbe': orb, 'Grimoire': grimoire, 'Phylactere': phylactery,
}

if __name__ == '__main__':
    preview = '--preview' in sys.argv
    names = [x for x in sys.argv[1:] if not x.startswith('--')] or list(BUILDERS)
    bank = TexBank(RECIPES, preview)
    if preview:
        from forge.render import render, posed_meshes
        from heroes.common import sheet
        ims = []
        for n in names:
            a = BUILDERS[n](); bank.bind(a)
            ext = np.vstack([m.V for _, m, _ in a.parts]); ctr = (ext.max(0) + ext.min(0)) / 2; size = float((ext.max(0) - ext.min(0)).max())
            ims.append(render(posed_meshes(a, None, 0, ()), size=(300, 360), yaw=35, pitch=15, target=tuple(ctr), dist=size * 2.6 + 0.3, ss=1))
        sheet(ims, 6, names).save('/tmp/claude-0/-home-claude/09ed4108-e263-59a9-be40-7611ce1efe40/scratchpad/armes.png')
    else:
        for n in names:
            a = BUILDERS[n](); bank.bind(a)
            for _, m, _ in a.parts: m.J = np.zeros((len(m.V), 4), np.int64); m.W = np.tile([1.0, 0, 0, 0], (len(m.V), 1))
            EX.export_prop(a, bank, bank.specs(), f'Weapons/{n}')
            print('exporté', n, flush=True)
