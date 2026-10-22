"""Humanoïdes de Korvalune : squelette commun, armures modulaires, animations procédurales."""
import numpy as np
import geo
from geo import Part, tube, superellipsoid, lathe, merge, mirror, rot_x, rot_y, rot_z, box

H = {  # os : (parent, tête monde) ; personnage face à +Z, sa gauche = +X
    "Root": (None, (0, 0, 0)), "Hips": ("Root", (0, 0.98, 0)), "Spine": ("Hips", (0, 1.08, 0)), "Chest": ("Spine", (0, 1.28, 0)), "Neck": ("Chest", (0, 1.55, 0)), "Head": ("Neck", (0, 1.63, 0)),
    "Cape": ("Chest", (0, 1.50, -0.12)),
    "UpperArm_L": ("Chest", (0.31, 1.46, 0)), "Forearm_L": ("UpperArm_L", (0.47, 1.18, 0.02)), "Hand_L": ("Forearm_L", (0.58, 0.94, 0.06)),
    "Thigh_L": ("Hips", (0.12, 0.96, 0)), "Shin_L": ("Thigh_L", (0.13, 0.54, 0.01)), "Foot_L": ("Shin_L", (0.13, 0.10, -0.02)), "Toe_L": ("Foot_L", (0.13, 0.045, 0.13)),
}
for k in [k for k in H if k.endswith("_L")]:
    p, h = H[k]; H[k[:-2] + "_R"] = (p[:-2] + "_R" if p.endswith("_L") else p, (-h[0], h[1], h[2]))
BONES = {k: H[k] for k in H}


def limb(pts, rad, bones, trans, mat, up=(0, 0, 1), sides=14):
    t = tube(pts, rad, sides=sides, up=up, mat=mat)
    p0 = np.array(pts[0]); d = np.array(pts[-1]) - p0
    return t.skin_chain(bones, p0, d, trans)


def sup_at(radii, n1, n2, res, center, mat, R=None):
    return superellipsoid(radii, n1, n2, res=res, mat=mat).xf(R, t=center)


def side(part_l, both=True):
    return [part_l, mirror(part_l)] if both else [part_l]


def build(cfg, M):
    """cfg: dict. M: noms de matériaux -> indices. Retourne la liste des Part."""
    P = []
    skin_m, cloth_m, leather_m, steel_m, dark_m, gold_m = M["skin"], M["cloth"], M["leather"], M["steel"], M["dark"], M["gold"]
    robe = cfg.get("robe", False); bulk = cfg.get("bulk", 1.0); heavy = cfg.get("heavy", True)
    under = cloth_m if cfg.get("cloth_under", True) else leather_m
    plate = M[cfg.get("plate", "steel")] if heavy else leather_m
    # ---- torse
    ax = np.array([0, 1.0, 0])
    torso = tube([(0, 0.92, 0), (0, 1.10, 0), (0, 1.32, 0), (0, 1.50, 0)], [(0.19 * bulk, 0.12), (0.17 * bulk, 0.11), (0.23 * bulk, 0.14), (0.22 * bulk, 0.13)], sides=18, up=(0, 0, 1), mat=under)
    P.append(torso.skin_chain(["Hips", "Spine", "Chest"], (0, 0.9, 0), ax, [(1.0, 1.16), (1.2, 1.34)]))
    if heavy:
        br = superellipsoid((0.255 * bulk, 0.24, 0.17), 0.7, 0.8, res=(14, 22), center=(0, 1.38, 0.015), mat=plate)
        P.append(br.skin("Chest"))
        P.append(superellipsoid((0.20 * bulk, 0.10, 0.145), 0.6, 0.7, res=(10, 18), center=(0, 1.19, 0.01), mat=plate).skin("Spine"))
        P.append(superellipsoid((0.04, 0.05, 0.02), 0.6, 0.6, res=(8, 10), center=(0, 1.42, 0.178), mat=gold_m).skin("Chest"))
    # ceinture et jupe
    belt = tube([(-0.21 * bulk, 1.0, 0), (0, 1.0, 0.0)], [(0.01, 0.01), (0.01, 0.01)], sides=6, mat=leather_m)
    ring = lathe([(0.205 * bulk, 1.03), (0.215 * bulk, 1.0), (0.215 * bulk, 0.96), (0.205 * bulk, 0.94)], sides=22, mat=leather_m, scale=(1, 1, 0.66))
    P.append(ring.skin("Hips"))
    P.append(superellipsoid((0.045, 0.045, 0.02), 0.5, 0.5, res=(8, 10), center=(0, 0.985, 0.145), mat=gold_m).skin("Hips"))
    if robe:
        sk = lathe([(0.27 * bulk, 1.0), (0.30, 0.55), (0.36, 0.12), (0.37, 0.05)], sides=26, mat=cloth_m, scale=(1, 1, 0.9)); P.append(sk.skin_chain(["Hips", "Thigh_L"], (0, 1.0, 0), (0, -1, 0), [(0.2, 0.5)]).skin("Hips"))
    else:
        for sgn in (1, -1):
            P.append(sup_at((0.085, 0.13, 0.03), 0.5, 0.5, (8, 12), (sgn * 0.13, 0.84, 0.115), plate if heavy else leather_m, rot_x(-0.12)).skin("Hips"))
        P.append(sup_at((0.075, 0.15, 0.03), 0.5, 0.5, (8, 12), (0, 0.80, 0.13), cloth_m, rot_x(-0.08)).skin("Hips"))
    # ---- cou et tête
    P.append(tube([(0, 1.50, 0), (0, 1.60, 0.01)], [(0.065, 0.065), (0.06, 0.06)], sides=10, mat=skin_m).skin_chain(["Chest", "Neck"], (0, 1.5, 0), (0, 1, 0), [(0.0, 0.12)]))
    helm = cfg.get("helm", "great")
    head = superellipsoid((0.105, 0.125, 0.12), 0.85, 0.9, res=(14, 22), center=(0, 1.73, 0.01), mat=skin_m)
    P.append(head.skin("Head"))
    if helm == "great":
        h1 = superellipsoid((0.125, 0.15, 0.14), 0.55, 0.6, res=(16, 24), center=(0, 1.745, 0.005), mat=plate); P.append(h1.skin("Head"))
        slit = superellipsoid((0.10, 0.012, 0.02), 0.5, 0.5, res=(6, 12), center=(0, 1.745, 0.138), mat=dark_m); P.append(slit.skin("Head"))
        vslit = superellipsoid((0.012, 0.07, 0.02), 0.5, 0.5, res=(6, 12), center=(0, 1.72, 0.138), mat=dark_m); P.append(vslit.skin("Head"))
        crest = tube([(0, 1.87, -0.09), (0, 1.97, -0.05), (0, 1.95, 0.05), (0, 1.89, 0.10)], [(0.008, 0.035), (0.01, 0.045), (0.01, 0.045), (0.008, 0.03)], sides=8, mat=M["cloth"]); P.append(crest.skin("Head"))
    elif helm == "hood":
        hd = lathe([(0.08, 1.60), (0.14, 1.70), (0.135, 1.82), (0.07, 1.90), (0.0, 1.93)], sides=22, mat=cloth_m, scale=(1, 1, 1.15), center=(0, 0, -0.02)); P.append(hd.skin("Head"))
        P.append(superellipsoid((0.1, 0.12, 0.05), 0.8, 0.8, res=(10, 16), center=(0, 1.725, 0.07), mat=dark_m).skin("Head"))
    elif helm == "crown":
        P.append(superellipsoid((0.11, 0.07, 0.125), 0.6, 0.6, res=(10, 18), center=(0, 1.79, 0.0), mat=gold_m).skin("Head"))
        for i in range(5):
            a = i / 5 * 2 * np.pi; P.append(superellipsoid((0.012, 0.045, 0.012), 0.6, 0.6, res=(6, 8), center=(np.cos(a) * 0.1, 1.86, np.sin(a) * 0.11), mat=gold_m).skin("Head"))
    elif helm == "horns":
        for sg in (1, -1):
            P.append(tube([(sg * 0.10, 1.79, 0.0), (sg * 0.20, 1.88, 0.0), (sg * 0.26, 2.0, 0.02)], [(0.03, 0.03), (0.022, 0.022), (0.006, 0.006)], sides=8, mat=M["bone"]).skin("Head"))
        P.append(superellipsoid((0.11, 0.08, 0.125), 0.7, 0.7, res=(10, 16), center=(0, 1.78, 0.0), mat=dark_m).skin("Head"))
    if cfg.get("beard"): P.append(superellipsoid((0.07, 0.11, 0.06), 0.8, 0.8, res=(10, 14), center=(0, 1.62, 0.09), mat=M["hide"]).skin("Head"))
    # ---- épaulières, bras
    for sgn in (1, -1):
        s = "L" if sgn > 0 else "R"
        arm = limb([(sgn * 0.30, 1.47, 0), (sgn * 0.40, 1.32, 0.0), (sgn * 0.475, 1.18, 0.02), (sgn * 0.53, 1.06, 0.04), (sgn * 0.585, 0.95, 0.065)],
                   [(0.058, 0.058), (0.052, 0.052), (0.046, 0.046), (0.04, 0.04), (0.036, 0.036)], ["UpperArm_" + s, "Forearm_" + s, "Hand_" + s], [(1.2 - 0.05, 1.32 - 0.05), (1.04, 1.1)], under, up=(0, 0, 1), sides=12)
        # transitions le long de l'axe -y : on utilise direction (0,-1,0) depuis l'épaule
        arm.skin_chain(["UpperArm_" + s, "Forearm_" + s, "Hand_" + s], (0, 1.47, 0), (0, -1, 0), [(0.25, 0.31), (0.50, 0.54)])
        P.append(arm)
        if heavy:
            P.append(sup_at((0.115, 0.075, 0.115), 0.55, 0.65, (12, 18), (sgn * 0.33, 1.51, 0), plate, rot_z(-sgn * 0.45)).skin("UpperArm_" + s))
            P.append(sup_at((0.085, 0.05, 0.08), 0.5, 0.6, (8, 14), (sgn * 0.34, 1.575, 0), gold_m, rot_z(-sgn * 0.45)).skin("UpperArm_" + s))
            vb = tube([(sgn * 0.495, 1.15, 0.025), (sgn * 0.535, 1.05, 0.04), (sgn * 0.57, 0.975, 0.058)], [(0.06, 0.06), (0.054, 0.054), (0.047, 0.047)], sides=12, mat=plate); P.append(vb.skin("Forearm_" + s))
            P.append(superellipsoid((0.06, 0.06, 0.055), 0.6, 0.6, res=(8, 12), center=(sgn * 0.475, 1.19, 0.045), mat=plate).skin("Forearm_" + s))
        else:
            P.append(tube([(sgn * 0.52, 1.1, 0.03), (sgn * 0.575, 0.99, 0.06)], [(0.052, 0.052), (0.045, 0.045)], sides=10, mat=leather_m).skin("Forearm_" + s))
        P.append(superellipsoid((0.047, 0.065, 0.05), 0.7, 0.7, res=(10, 14), center=(sgn * 0.595, 0.915, 0.07), mat=M["steel"] if heavy else leather_m).skin("Hand_" + s))
    # ---- jambes
    for sgn in (1, -1):
        s = "L" if sgn > 0 else "R"
        leg = tube([(sgn * 0.12, 0.99, 0), (sgn * 0.125, 0.76, 0.0), (sgn * 0.13, 0.54, 0.01), (sgn * 0.13, 0.32, -0.005), (sgn * 0.13, 0.12, -0.01)],
                   [(0.088, 0.09), (0.078, 0.08), (0.062, 0.064), (0.056, 0.058), (0.046, 0.05)], sides=14, up=(0, 0, 1), mat=leather_m if not robe else cloth_m)
        leg.skin_chain(["Thigh_" + s, "Shin_" + s, "Foot_" + s], (0, 0.99, 0), (0, -1, 0), [(0.39, 0.47), (0.82, 0.88)]); P.append(leg)
        if heavy:
            P.append(superellipsoid((0.075, 0.075, 0.07), 0.6, 0.6, res=(10, 14), center=(sgn * 0.13, 0.55, 0.055), mat=plate).skin_chain(["Thigh_" + s, "Shin_" + s], (0, 0.99, 0), (0, -1, 0), [(0.42, 0.46)]).skin("Shin_" + s) if False else superellipsoid((0.075, 0.075, 0.07), 0.6, 0.6, res=(10, 14), center=(sgn * 0.13, 0.55, 0.055), mat=plate).skin("Shin_" + s))
            gr = tube([(sgn * 0.13, 0.50, 0.012), (sgn * 0.13, 0.30, 0.0), (sgn * 0.13, 0.15, -0.01)], [(0.068, 0.07), (0.062, 0.066), (0.052, 0.055)], sides=12, mat=plate); P.append(gr.skin("Shin_" + s))
            P.append(tube([(sgn * 0.12, 0.93, 0.0), (sgn * 0.125, 0.70, 0.0)], [(0.095, 0.098), (0.083, 0.086)], sides=12, mat=plate).skin_chain(["Hips", "Thigh_" + s], (0, 0.93, 0), (0, -1, 0), [(0.05, 0.12)]))
        boot = superellipsoid((0.065, 0.06, 0.13), 0.55, 0.65, res=(10, 16), center=(sgn * 0.13, 0.065, 0.05), mat=leather_m); P.append(boot.skin("Foot_" + s))
        P.append(superellipsoid((0.058, 0.05, 0.08), 0.6, 0.7, res=(8, 12), center=(sgn * 0.13, 0.04, 0.12), mat=dark_m).skin("Toe_" + s))
        P.append(superellipsoid((0.07, 0.1, 0.07), 0.6, 0.6, res=(8, 12), center=(sgn * 0.13, 0.15, -0.015), mat=leather_m).skin("Foot_" + s))
    # ---- cape
    if cfg.get("cape", True):
        cp = lathe([(0.0, 0.0), (0.22, 0.0), (0.26, -0.55), (0.30, -1.05), (0.32, -1.25)], sides=18, mat=cloth_m, arc=(np.pi * 1.12, np.pi * 1.88), scale=(1, 1, 1))
        cp.xf(None, t=(0, 0, 0)); cp.pos = np.stack([cp.pos[:, 0], cp.pos[:, 1] + 1.52, cp.pos[:, 2] * 0.55 + 0.0 - 0.06], -1)
        cp.skin_chain(["Chest", "Cape"], (0, 1.52, 0), (0, -1, 0), [(0.0, 0.25)]); P.append(cp)
    # ---- arme et bouclier
    w = cfg.get("weapon", "sword")
    if w == "sword":
        hand = np.array(H["Hand_R"][1]) + np.array([0, -0.02, 0.07]); g = merge([
            tube([(0, 0, -0.07), (0, 0, 0.07)], [(0.018, 0.018), (0.018, 0.018)], sides=8, mat=leather_m),
            superellipsoid((0.02, 0.02, 0.02), 0.7, 0.7, res=(8, 10), center=(0, 0, -0.09), mat=gold_m),
            superellipsoid((0.09, 0.012, 0.02), 0.5, 0.5, res=(8, 12), center=(0, 0, 0.085), mat=gold_m),
            tube([(0, 0, 0.09), (0, 0, 0.55), (0, 0, 0.92), (0, 0, 1.05)], [(0.01, 0.026), (0.008, 0.026), (0.007, 0.02), (0.003, 0.004)], sides=6, mat=steel_m, up=(1, 0, 0))])
        g.xf(rot_x(-0.55)); g.xf(None, t=hand); P.append(g.skin("Hand_R"))
    elif w == "staff":
        hand = np.array(H["Hand_R"][1]) + np.array([0, 0, 0.07]); g = merge([tube([(0, -0.9, 0), (0, 0.4, 0), (0, 1.1, 0)], [(0.02, 0.02), (0.022, 0.022), (0.024, 0.024)], sides=8, mat=leather_m),
            superellipsoid((0.055, 0.055, 0.055), 0.9, 0.9, res=(12, 16), center=(0, 1.22, 0), mat=M["crystal"])] + [superellipsoid((0.012, 0.09, 0.012), 0.6, 0.6, res=(6, 8), center=(np.cos(a) * 0.06, 1.15, np.sin(a) * 0.06), mat=dark_m).xf(None) for a in np.linspace(0, 2 * np.pi, 4, endpoint=False)])
        g.xf(None, t=hand); P.append(g.skin("Hand_R"))
    elif w == "dagger":
        for sgn, s in ((1, "L"), (-1, "R")):
            hand = np.array(H["Hand_" + s][1]) + np.array([0, -0.01, 0.06]); g = merge([tube([(0, 0, -0.05), (0, 0, 0.05)], [(0.015, 0.015), (0.015, 0.015)], sides=8, mat=leather_m), superellipsoid((0.05, 0.01, 0.015), 0.5, 0.5, res=(6, 10), center=(0, 0, 0.06), mat=gold_m),
                tube([(0, 0, 0.065), (0, 0, 0.3), (0, 0, 0.42)], [(0.006, 0.02), (0.005, 0.016), (0.002, 0.002)], sides=6, mat=steel_m, up=(1, 0, 0))])
            g.xf(rot_x(-0.5)); g.xf(None, t=hand); P.append(g.skin("Hand_" + s))
    elif w == "bow":
        hand = np.array(H["Hand_L"][1]) + np.array([0, 0, 0.06]); n = 12; t_ = np.linspace(-1, 1, n)
        g = tube([(0, y * 0.62, -0.18 * (1 - y * y) + 0.0) for y in t_], [(0.016, 0.016)] * n, sides=8, mat=M["wood"]); g.xf(None, t=hand)
        P.append(g.skin("Hand_L"))
        string = tube([(0, -0.62, 0.0), (0, 0.62, 0.0)], [(0.003, 0.003)] * 2, sides=4, mat=M["cloth"], cap_start=False, cap_end=False); string.xf(None, t=hand); P.append(string.skin("Hand_L"))
    if cfg.get("shield"):
        hand = np.array(H["Forearm_L"][1]) + np.array([0.09, -0.12, 0.13])
        sh = merge([superellipsoid((0.30, 0.38, 0.035), 0.5, 0.6, res=(12, 20), mat=plate), superellipsoid((0.31, 0.39, 0.02), 0.35, 0.4, res=(10, 18), center=(0, 0, -0.012), mat=gold_m), superellipsoid((0.08, 0.08, 0.06), 0.9, 0.9, res=(10, 14), center=(0, 0, 0.04), mat=gold_m)])
        sh.xf(rot_y(1.2), s=(0.8, 0.8, 0.8)); sh.xf(None, t=hand); P.append(sh.skin("Forearm_L"))
    for p in P: _fix_winding(p); p.finalize_uv()
    return P


def _fix_winding(p):
    a, b, c = p.pos[p.idx[:, 0]], p.pos[p.idx[:, 1]], p.pos[p.idx[:, 2]]; gn = np.cross(b - a, c - a); vn = p.nrm[p.idx].sum(1)
    if (np.einsum("ij,ij->i", gn, vn) < 0).mean() > 0.5: p.idx = p.idx[:, ::-1]


# -------------------------------------------------------------------------------- animations
def qe(x=0, y=0, z=0):
    def q(ax, a): s = np.sin(a / 2); return np.array([ax[0] * s, ax[1] * s, ax[2] * s, np.cos(a / 2)])
    def mul(a, b):
        x1, y1, z1, w1 = a; x2, y2, z2, w2 = b
        return np.array([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2])
    r = mul(mul(q((0, 0, 1), z), q((0, 1, 0), y)), q((1, 0, 0), x)); return r / np.linalg.norm(r)


def clip(name, dur, fn, fps=30, loop=True, bones=None):
    n = int(dur * fps) + 1; times = [i / fps for i in range(n)]; rot = {}; pos = {}
    for i, t in enumerate(times):
        ph = (t / dur) if not loop else (t / dur) % 1.0
        r, tr = fn(t / dur if not loop else ph)
        for b, e in r.items():
            q = qe(*e) if not isinstance(e, np.ndarray) else e
            rot.setdefault(b, ([], []))[0].append(t); rot[b][1].append(q.tolist())
        for b, v in tr.items(): pos.setdefault(b, ([], []))[0].append(t); pos[b][1].append(list(np.array(BONES[b][1]) - np.array(BONES[BONES[b][0]][1]) + np.array(v)))
    # tous les os animés partagent les mêmes temps
    return {"name": name, "rot": rot, "pos": pos}


S = np.sin; C = np.cos; TAU = 2 * np.pi


def _walk(amp, arm, bob, lean=0.0, knee=1.0):
    def f(ph):
        a = ph * TAU; l = S(a); r = -l
        rot = {"Thigh_L": (-l * amp, 0, 0), "Thigh_R": (-r * amp, 0, 0), "Shin_L": (max(0, S(a + 1.0)) * amp * 1.4 * knee + 0.05, 0, 0), "Shin_R": (max(0, S(a + 1.0 + np.pi)) * amp * 1.4 * knee + 0.05, 0, 0),
               "Foot_L": (max(0, -S(a + 1.0)) * 0.2, 0, 0), "Foot_R": (max(0, -S(a + 1.0 + np.pi)) * 0.2, 0, 0),
               "UpperArm_L": (r * arm, 0, 0.12), "UpperArm_R": (l * arm, 0, -0.12), "Forearm_L": (-0.25 - max(0, r) * arm * 0.5, 0, 0), "Forearm_R": (-0.25 - max(0, l) * arm * 0.5, 0, 0),
               "Spine": (lean, S(a) * 0.08, 0), "Chest": (0, -S(a) * 0.1, S(a) * 0.02), "Head": (-lean * 0.5, S(a) * 0.04, 0), "Cape": (0.1 + 0.25 * abs(S(a)) * amp + lean * 0.6, 0, 0)}
        return rot, {"Hips": (0, abs(C(a)) * -bob, 0)}
    return f


def idle(ph):
    a = ph * TAU; b = S(a)
    rot = {"Chest": (0.015 * b, 0, 0), "Spine": (0.01 * b, 0, 0), "Head": (-0.01 * b, S(a * 0.5) * 0.05, 0), "UpperArm_L": (0.02 * b, 0, 0.06), "UpperArm_R": (-0.02 * b, 0, -0.06),
           "Forearm_L": (-0.25 + 0.02 * b, 0, 0), "Forearm_R": (-0.32 + 0.02 * b, 0, 0), "Thigh_L": (0, 0, 0.02), "Thigh_R": (0, 0, -0.02), "Cape": (0.04 * b, 0, 0)}
    return rot, {"Hips": (0, -0.004 * (1 - b) , 0)}


def _lerp(keys, ph):
    """keys: [(phase, valeur)] valeurs numériques tuples ; interpolation douce."""
    for i in range(len(keys) - 1):
        p0, v0 = keys[i]; p1, v1 = keys[i + 1]
        if p0 <= ph <= p1:
            t = (ph - p0) / max(p1 - p0, 1e-6); t = t * t * (3 - 2 * t); return tuple(np.atleast_1d(np.array(v0, float) * (1 - t) + np.array(v1, float) * t))
    return tuple(np.atleast_1d(np.array(keys[-1][1], float)))


def attack(kind=1):
    def f(ph):
        if kind == 1:  # coup ample de droite à gauche
            ua = _lerp([(0, (-0.1, 0, -0.1)), (0.3, (-2.3, 0.4, -0.4)), (0.5, (-0.9, -0.5, 0.2)), (0.7, (-0.5, -0.3, 0.1)), (1, (-0.1, 0, -0.1))], ph)
            fa = _lerp([(0, (-0.3, 0, 0)), (0.3, (-0.9, 0, 0)), (0.5, (-0.2, 0, 0)), (1, (-0.3, 0, 0))], ph)
            ch = _lerp([(0, (0, 0, 0)), (0.3, (0.1, 0.55, 0)), (0.5, (0.0, -0.6, 0)), (0.75, (0, -0.2, 0)), (1, (0, 0, 0))], ph)
            lean = _lerp([(0, 0), (0.3, -0.1), (0.5, 0.25), (1, 0)], ph)[0]
        else:  # estoc
            ua = _lerp([(0, (-0.1, 0, -0.1)), (0.3, (-0.6, 0.2, -0.3)), (0.5, (-1.5, 0, 0.0)), (0.75, (-1.4, 0, 0)), (1, (-0.1, 0, -0.1))], ph)
            fa = _lerp([(0, (-0.3, 0, 0)), (0.3, (-1.6, 0, 0)), (0.5, (-0.1, 0, 0)), (1, (-0.3, 0, 0))], ph)
            ch = _lerp([(0, (0, 0, 0)), (0.3, (0, 0.3, 0)), (0.5, (0, -0.2, 0)), (1, (0, 0, 0))], ph)
            lean = _lerp([(0, 0), (0.3, -0.12), (0.5, 0.3), (1, 0)], ph)[0]
        step = _lerp([(0, 0), (0.4, 0.5), (0.6, 0.5), (1, 0)], ph)[0]
        rot = {"UpperArm_R": ua, "Forearm_R": fa, "Chest": ch, "Spine": (lean, 0, 0), "Thigh_L": (-step * 0.9, 0, 0), "Thigh_R": (step * 0.5, 0, 0), "Shin_R": (step * 0.7, 0, 0), "Shin_L": (step * 0.3, 0, 0),
               "UpperArm_L": (-0.3 - step * 0.2, 0, 0.3), "Forearm_L": (-0.6, 0, 0), "Head": (-lean * 0.5, -ch[1] * 0.4, 0), "Cape": (0.3 + step * 0.4, 0, 0)}
        return rot, {"Hips": (0, -0.05 * step, 0.12 * step)}
    return f


def hit(ph):
    k = np.sin(min(ph, 1) * np.pi) ** 0.6
    return {"Chest": (-0.35 * k, 0.15 * k, 0), "Spine": (-0.2 * k, 0, 0), "Head": (0.3 * k, -0.2 * k, 0), "UpperArm_L": (0.5 * k, 0, 0.5 * k), "UpperArm_R": (0.5 * k, 0, -0.5 * k), "Cape": (0.6 * k, 0, 0)}, {"Hips": (0, 0, -0.1 * k)}


def death(ph):
    t = min(ph / 0.7, 1.0); e = t * t * (3 - 2 * t); fall = _lerp([(0, 0), (0.7, 1)], ph)[0]
    rot = {"Hips": (-1.45 * e, 0, 0), "Chest": (0.2 * e, 0, 0), "Head": (0.4 * e, 0.3 * e, 0), "UpperArm_L": (-0.2, 0, 0.9 * e), "UpperArm_R": (-0.5 * e, 0, -1.1 * e), "Thigh_L": (-0.4 * e, 0, 0.1), "Thigh_R": (-0.1 * e, 0, -0.2), "Shin_L": (0.7 * e, 0, 0), "Cape": (-0.3 * e, 0, 0)}
    return rot, {"Hips": (0, -0.55 * e, -0.15 * e)}


def cast(ph):
    up = _lerp([(0, 0), (0.35, 1), (0.7, 1), (1, 0)], ph)[0]
    rot = {"UpperArm_R": (-2.4 * up, 0, -0.3 * up), "Forearm_R": (-0.5 * up, 0, 0), "UpperArm_L": (-1.3 * up, 0, 0.5 * up), "Forearm_L": (-0.7 * up, 0, 0), "Chest": (-0.15 * up, 0, 0), "Head": (-0.2 * up, 0, 0), "Cape": (0.3 * up, 0, 0)}
    return rot, {}


def block(ph):
    b = _lerp([(0, 0), (0.2, 1), (0.8, 1), (1, 0)], ph)[0]
    return {"UpperArm_L": (-1.1 * b, -0.3 * b, 0.4 * b), "Forearm_L": (-1.4 * b, 0, 0), "Chest": (0.1 * b, 0.3 * b, 0), "Spine": (0.15 * b, 0, 0), "Thigh_L": (-0.3 * b, 0, 0), "Thigh_R": (0.2 * b, 0, 0), "Shin_R": (0.4 * b, 0, 0)}, {"Hips": (0, -0.08 * b, 0)}


def dodge(ph):
    e = np.sin(ph * np.pi); rot = {"Hips": (0.9 * e, 0, 0), "Spine": (0.4 * e, 0, 0), "Thigh_L": (-1.0 * e, 0, 0), "Thigh_R": (-1.0 * e, 0, 0), "Shin_L": (1.4 * e, 0, 0), "Shin_R": (1.4 * e, 0, 0), "UpperArm_L": (-0.6 * e, 0, 0.3), "UpperArm_R": (-0.6 * e, 0, -0.3), "Cape": (0.8 * e, 0, 0)}
    return rot, {"Hips": (0, -0.25 * e, 0.0)}


def all_anims():
    return [clip("Idle", 2.4, idle), clip("Walk", 1.0, _walk(0.5, 0.45, 0.025)), clip("Run", 0.66, _walk(0.95, 0.9, 0.06, 0.18, 1.0)), clip("Attack1", 0.8, attack(1), loop=False), clip("Attack2", 0.8, attack(2), loop=False),
            clip("Hit", 0.45, hit, loop=False), clip("Death", 1.6, death, loop=False), clip("Cast", 0.9, cast, loop=False), clip("Block", 0.8, block, loop=False), clip("Dodge", 0.5, dodge, loop=False)]
