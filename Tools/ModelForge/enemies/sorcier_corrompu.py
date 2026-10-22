"""Sorcier corrompu — ennemi de zone : mage émacié au teint cendré et aux yeux violets luisants, sous une capuche
profonde ; robe longue violette brodée d'or (pourpoint fermé, épaulières à lames, brassards, pans déchirés jusqu'aux
chevilles), cape ; bâton tordu de bois noir dont la tête en croissant de ronces enserre un cristal du Néant."""
import numpy as np
from forge.rig import Asset
from forge import humanoid as H
from forge.geo import Loft, spline, curve, unit, concat
from knight import anims, materials as KM
from heroes import female_kit as FK, varek as VR
from heroes import modular as MOD
from forge import armorparts as AP
from enemies import materials as EM

NAME = 'SorcierCorrompu'
FOLDER = 'Enemies'
HEAD_VIEW = (0, 1.66, 0.05)
RECIPES = {
    'SC_Robe':    (lambda S: KM.tooled_leather(S, seed=501, tone=(0.12, 0.035, 0.16), line=(0.72, 0.52, 0.22), n=44), 1024, 512, dict(metallic=0.4, smoothness=1.0)),
    'SC_Sleeve':  (lambda S: KM.gambeson(S, seed=502, color=(0.10, 0.03, 0.14)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'SC_Cloth':   (lambda W, H_: KM.cloak(W, H_, seed=503, color=(0.15, 0.04, 0.20), thread_color=(0.75, 0.56, 0.22), mud_amt=1.0), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'SC_Cloak':   (lambda W, H_: KM.cloak(W, H_, seed=504, color=(0.08, 0.025, 0.11), thread_color=(0.55, 0.30, 0.75), mud_amt=1.2), (1024, 2048), (256, 512), dict(metallic=0.0, smoothness=1.0)),
    'SC_Gold':    (lambda S: KM.brass(S, seed=505, gold=(0.70, 0.52, 0.24)), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'SC_Metal':   (lambda S: KM.steel(S, seed=506, tone=(0.16, 0.10, 0.20), polish=0.6, rust_amt=0.2), 1024, 256, dict(metallic=1.0, smoothness=1.0)),
    'SC_Leather': (lambda S: KM.leather(S, seed=507, tone=(0.05, 0.03, 0.05)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'SC_Crystal': (lambda S: KM.crystal(S, seed=508, color=(0.62, 0.22, 1.0), glow=1.3), 512, 256, dict(metallic=0.0, smoothness=1.0, emission=(2.0, 2.0, 2.0), emission_preview=(1.2, 0.6, 1.8))),
    'SC_Dark':    (lambda S: KM.gambeson(S, seed=509, color=(0.012, 0.008, 0.016)), 256, 128, dict(metallic=0.0, smoothness=0.25)),
    'SC_Wood':    (lambda S: EM.tinted(KM.wood(S, seed=510), (0.35, 0.28, 0.32)), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
}
GEAR = [('Socket_Weapon_R', 'CorruptStaff')]
M = dict(corset='SC_Robe', sleeve='SC_Sleeve', cloth='SC_Cloth', cloak='SC_Cloak', gold='SC_Gold', metal='SC_Metal', leather='SC_Leather',
         crystal='SC_Crystal', dark='SC_Dark', bone='SC_Gold', wood='SC_Wood')


def corrupt_staff(name='CorruptStaff'):
    """Bâton de bois noir tordu (nœuds, torsade), tête en croissant de deux ronces recourbées qui enserrent un cristal
    violet à facettes, épines, éclats en orbite. Prise à l'origine, tête vers +Y."""
    a = Asset(name)
    t = np.linspace(0, 1, 60)
    pts = np.c_[0.012 * np.sin(t * 9.0) + 0.006 * np.sin(t * 23), -0.80 + t * 1.80, 0.010 * np.cos(t * 7.0)]
    r = 0.016 + 0.004 * np.sin(t * 31) ** 2 + 0.006 * np.exp(-((t - 0.38) / 0.02) ** 2) + 0.007 * np.exp(-((t - 0.71) / 0.025) ** 2) + 0.004 * t
    shaft = Loft(pts, r, r * 0.92, p=2.2, segs=12, cap0=2)
    m = shaft.mesh(0.25); m.UV = np.c_[m.UV[:, 0] + m.UV[:, 1] * 0.8, m.UV[:, 1]]   # fil du bois en torsade
    a.add('fut', m, 'SC_Wood')
    for y in (-0.06, 0.14, 0.86):
        a.add(f'bague{y}', Loft(spline([(0, y - 0.012, 0), (0, y + 0.012, 0)], 3), 0.024, 0.024, p=2.2, segs=14, cap0=1, cap1=1).mesh(0.05), 'SC_Gold')
    top = np.array([pts[-1, 0], 1.00, pts[-1, 2]])
    for sx in (1, -1):   # deux ronces en croissant
        b = [top + (0.0, -0.02, 0), top + (0.045 * sx, 0.04, 0), top + (0.085 * sx, 0.13, 0.005), top + (0.075 * sx, 0.23, 0), top + (0.030 * sx, 0.29, -0.005), top + (0.004 * sx, 0.275, 0)]
        lo = Loft(spline(b, 30), curve([(0, .014), (.6, .008), (1, .002)], 30), curve([(0, .013), (.6, .007), (1, .002)], 30), p=2.2, segs=10, cap1=1)
        a.add(f'ronce{sx}', lo.mesh(0.2), 'SC_Wood')
        for k, tt in enumerate((0.25, 0.45, 0.62, 0.80)):   # épines
            i = int(tt * 29); c = lo.path[i]; out = unit(np.array([sx * 1.0, 0.25, 0.3 * (-1) ** k]))
            a.add(f'epine{sx}{k}', Loft(spline([c, c + out * 0.035], 5), curve([(0, .004), (1, .0006)], 5), curve([(0, .004), (1, .0006)], 5), p=2, segs=6, cap1=1).mesh(0.05), 'SC_Dark')
        # petites vrilles d'or enroulées autour des ronces
        v = [lo.path[int(q * 29)] + 0.015 * np.array([np.cos(q * 30), 0, np.sin(q * 30)]) for q in np.linspace(0.05, 0.55, 24)]
        a.add(f'vrille{sx}', Loft(np.array(v), 0.0022, 0.0022, p=2, segs=6).mesh(0.05), 'SC_Gold')
    c = top + (0, 0.15, 0)
    cr = Loft(spline([c - (0, 0.075, 0), c, c + (0, 0.085, 0)], 12), curve([(0, .003), (.45, .040), (1, .002)], 12), curve([(0, .003), (.45, .036), (1, .002)], 12),
              p=1.25, segs=4, cap0=1, cap1=1)   # octaèdre étiré
    a.add('cristal', cr.mesh(0.1), 'SC_Crystal')
    for k in range(5):
        an = k * 1.26; p = c + np.array([np.cos(an) * 0.075, 0.05 * np.sin(k * 2.1), np.sin(an) * 0.075])
        a.add(f'eclat{k}', Loft(spline([p - (0, 0.016, 0), p + (0, 0.016, 0)], 5), curve([(0, .002), (.5, .008), (1, .001)], 5), curve([(0, .002), (.5, .007), (1, .001)], 5), p=1.3, segs=4, cap0=1, cap1=1).mesh(0.05), 'SC_Crystal')
    return a


def build(bank, female=False):
    sk = H.build(); a = Asset(NAME, sk)
    body, marks = MOD.base(sk, a, bank, 'SC', female=False, look=dict(
        skin='middleage_caucasian_male', skin_tint=(0.66, 0.62, 0.72), hair=None,
        brows='eyebrow001', eye_color=(0.70, 0.25, 1.0), glow=1.0, skin_kw=dict(pale=0.55, makeup=(0.05, 0.015, 0.07), veins=(0.55, 0.20, 0.85), dirt=0.6, pubic=0.5)))
    # pas de sous-vêtements ni d'anatomie : entièrement vêtu
    a.parts = [p for p in a.parts if a.slot.get(p[0]) not in ('Anatomie', 'SousVetementBas', 'SousVetementHaut')]
    FK.build_outfit(a, body, marks, sk, 'SC', M, dict(male=True, front_panel=True, spikes=True, skulls=False, tatter=0.55, collar=False, cloak=True, cloak_len=1.12, cloak_tatter=1.5))
    def add(n, m, mat): a.add(n, m, mat, slot='Capuche')
    VR.hood(a, add, body, marks, sk, cloth='SC_Cloak', lining='SC_Dark', depth=1.25)
    MOD.assign(a, [])
    MOD.covers(a, FK.COVERS)
    anims.build(sk, a, FK.StaffStyle())
    return a, {'CorruptStaff': corrupt_staff()}, anims.CLIPS
