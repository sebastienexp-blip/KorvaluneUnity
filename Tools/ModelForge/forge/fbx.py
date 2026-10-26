"""Écriture FBX 7.4 (ASCII) : maillage multi-matériaux, normales, UV, squelette, skinning (clusters + bind pose),
points d'attache (nœuds Null), animations (une pile par clip). Lisible par le SDK FBX d'Autodesk (Unity, Blender, Maya).

Conventions du fichier : Y vers le haut, avant +Z, droitier ; unités = centimètres (UnitScaleFactor = 1).
Unity applique l'échelle 0,01 (« Convert Units ») : le modèle arrive en mètres, face à +Z.
"""
import numpy as np
from .geo import vertex_normals
from .rig import qmat, mat_euler_xyz

KTIME = 46186158000          # unités FBX par seconde
CM = 100.0


class _Ids:
    def __init__(self): self.n = 1000000
    def __call__(self): self.n += 1; return self.n


def _arr(vals, fmt='{:.6g}'):
    vals = np.asarray(vals).ravel()
    if vals.dtype.kind in 'iu': body = ','.join(str(int(v)) for v in vals)
    else: body = ','.join(fmt.format(float(v)) for v in vals)
    return f'*{len(vals)} {{\n\t\t\ta: {body}\n\t\t}}'


def _mat16(M):
    """Matrice 4x4 (vecteurs colonnes) → 16 nombres dans l'ordre FBX (translation en 12,13,14)."""
    return np.asarray(M, float).T.ravel()


def write_fbx(path, asset, creator='Korvalune ModelForge', binary=True):
    ids = _Ids()
    sk = asset.skeleton
    O = []      # objets
    C = []      # connexions
    fps = 30
    if asset.clips: fps = list(asset.clips.values())[0].fps
    time_mode = {24: 11, 25: 10, 30: 6, 60: 3}.get(fps, 6)

    # ------------------------------------------------------------------ squelette
    bone_ids = []
    if sk is not None:
        G = sk.rest_global()
        for i, name in enumerate(sk.names):
            mid = ids(); aid = ids(); bone_ids.append(mid)
            kind = 'Null' if sk.null[i] else 'LimbNode'
            t = sk.local_t(i) * CM
            e = mat_euler_xyz(qmat(sk.rest[i]))
            O.append(f'\tNodeAttribute: {aid}, "NodeAttribute::{name}", "{kind}" {{\n\t\tProperties70:  {{\n\t\t\tP: "Size", "double", "Number", "",{4 if kind == "LimbNode" else 2}\n\t\t}}\n\t\t'
                     + (f'TypeFlags: "Skeleton"\n' if kind == 'LimbNode' else 'TypeFlags: "Null"\n') + '\t}')
            O.append(f'\tModel: {mid}, "Model::{name}", "{kind}" {{\n\t\tVersion: 232\n\t\tProperties70:  {{\n'
                     f'\t\t\tP: "RotationActive", "bool", "", "",1\n\t\t\tP: "InheritType", "enum", "", "",1\n'
                     f'\t\t\tP: "ScalingMax", "Vector3D", "Vector", "",0,0,0\n\t\t\tP: "DefaultAttributeIndex", "int", "Integer", "",0\n'
                     f'\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",{t[0]:.6f},{t[1]:.6f},{t[2]:.6f}\n'
                     f'\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",{e[0]:.6f},{e[1]:.6f},{e[2]:.6f}\n'
                     f'\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1\n\t\t}}\n\t\tShading: Y\n\t\tCulling: "CullingOff"\n\t}}')
            C.append(f'\tC: "OO",{aid},{mid}')
            C.append(f'\tC: "OO",{mid},{bone_ids[sk.parents[i]] if sk.parents[i] >= 0 else 0}')

    # ------------------------------------------------------------------ maillages (un par emplacement d'équipement, multi-matériaux)
    # asset.slot[nom de pièce] = emplacement (ex. « Sexe », « SousVetementBas ») : chaque emplacement devient un objet séparé
    # (SkinnedMeshRenderer dans Unity) qu'on peut masquer ou retirer ; le reste forme le maillage principal « <Nom>_Mesh ».
    slots = {}
    for pn, pm_, pmat in asset.parts:
        slots.setdefault(getattr(asset, 'slot', {}).get(pn, ''), []).append((pm_, pmat))
    from .geo import concat as _concat
    mat_ids = {}
    G = sk.rest_global() if sk is not None else None
    pose_nodes = []
    for sname in sorted(slots, key=lambda k: (k != '', k)):
        groups = {}
        for pm_, pmat in slots[sname]: groups.setdefault(pmat, []).append(pm_)
        groups = {k: _concat(v) for k, v in groups.items()}
        mat_names = list(groups.keys())
        V_all, F_all, UV_all, N_all, MI_all, J_all, W_all = [], [], [], [], [], [], []
        off = 0
        for k, mn in enumerate(mat_names):
            m = groups[mn]
            N = m.N if m.N is not None else vertex_normals(m)
            V_all.append(m.V); F_all.append(m.F + off); UV_all.append(m.UV); N_all.append(N)
            MI_all.append(np.full(len(m.F), k)); J_all.append(m.J); W_all.append(m.W); off += len(m.V)
        V = np.vstack(V_all) * CM; F = np.vstack(F_all); UV = np.vstack(UV_all); N = np.vstack(N_all)
        MI = np.concatenate(MI_all)
        J = np.vstack(J_all) if sk is not None else None; W = np.vstack(W_all) if sk is not None else None
        pvi = F.copy(); pvi[:, 2] = -pvi[:, 2] - 1
        pn = N[F].reshape(-1, 3)
        mname = f'{asset.name}_Mesh' if not sname else sname
        gid = ids(); mesh_id = ids()
        O.append(f'\tGeometry: {gid}, "Geometry::{mname}", "Mesh" {{\n\t\tVertices: {_arr(V, "{:.5f}")}\n\t\tPolygonVertexIndex: {_arr(pvi)}\n'
                 f'\t\tGeometryVersion: 124\n'
                 f'\t\tLayerElementNormal: 0 {{\n\t\t\tVersion: 101\n\t\t\tName: ""\n\t\t\tMappingInformationType: "ByPolygonVertex"\n\t\t\tReferenceInformationType: "Direct"\n\t\t\tNormals: {_arr(pn, "{:.5f}")}\n\t\t}}\n'
                 f'\t\tLayerElementUV: 0 {{\n\t\t\tVersion: 101\n\t\t\tName: "UVMap"\n\t\t\tMappingInformationType: "ByPolygonVertex"\n\t\t\tReferenceInformationType: "IndexToDirect"\n'
                 f'\t\t\tUV: {_arr(UV, "{:.5f}")}\n\t\t\tUVIndex: {_arr(F.ravel())}\n\t\t}}\n'
                 f'\t\tLayerElementMaterial: 0 {{\n\t\t\tVersion: 101\n\t\t\tName: ""\n\t\t\tMappingInformationType: "ByPolygon"\n\t\t\tReferenceInformationType: "IndexToDirect"\n\t\t\tMaterials: {_arr(MI)}\n\t\t}}\n'
                 f'\t\tLayer: 0 {{\n\t\t\tVersion: 100\n'
                 f'\t\t\tLayerElement:  {{\n\t\t\t\tType: "LayerElementNormal"\n\t\t\t\tTypedIndex: 0\n\t\t\t}}\n'
                 f'\t\t\tLayerElement:  {{\n\t\t\t\tType: "LayerElementMaterial"\n\t\t\t\tTypedIndex: 0\n\t\t\t}}\n'
                 f'\t\t\tLayerElement:  {{\n\t\t\t\tType: "LayerElementUV"\n\t\t\t\tTypedIndex: 0\n\t\t\t}}\n\t\t}}\n\t}}')
        O.append(f'\tModel: {mesh_id}, "Model::{mname}", "Mesh" {{\n\t\tVersion: 232\n\t\tProperties70:  {{\n'
                 f'\t\t\tP: "DefaultAttributeIndex", "int", "Integer", "",0\n\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",0,0,0\n'
                 f'\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",0,0,0\n\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1\n'
                 f'\t\t}}\n\t\tShading: T\n\t\tCulling: "CullingOff"\n\t}}')
        C.append(f'\tC: "OO",{gid},{mesh_id}'); C.append(f'\tC: "OO",{mesh_id},0')
        for mn in mat_names:
            if mn not in mat_ids:
                md = asset.materials.get(mn)
                col = md.color[:3] if md else (0.8, 0.8, 0.8)
                mid = ids(); mat_ids[mn] = mid
                O.append(f'\tMaterial: {mid}, "Material::{mn}", "" {{\n\t\tVersion: 102\n\t\tShadingModel: "phong"\n\t\tMultiLayer: 0\n\t\tProperties70:  {{\n'
                         f'\t\t\tP: "DiffuseColor", "Color", "", "A",{col[0]:.4f},{col[1]:.4f},{col[2]:.4f}\n\t\t\tP: "DiffuseFactor", "Number", "", "A",1\n'
                         f'\t\t\tP: "SpecularFactor", "Number", "", "A",0.5\n\t\t\tP: "ShininessExponent", "Number", "", "A",20\n\t\t}}\n\t}}')
            C.append(f'\tC: "OO",{mat_ids[mn]},{mesh_id}')
        pose_nodes.append(f'\t\tPoseNode:  {{\n\t\t\tNode: {mesh_id}\n\t\t\tMatrix: {_arr(_mat16(np.eye(4)))}\n\t\t}}')
        # blendshapes (curseurs de morphologie) : une forme par curseur, indices creux
        shp = {}
        for k, mn in enumerate(mat_names):
            for sn, D in getattr(groups[mn], 'shapes', {}).items(): shp.setdefault(sn, {})[k] = D
        if shp:
            starts = np.cumsum([0] + [len(groups[mn].V) for mn in mat_names])
            bs_id = ids()
            O.append(f'\tDeformer: {bs_id}, "Deformer::Morphs_{mname}", "BlendShape" {{\n\t\tVersion: 100\n\t}}')
            C.append(f'\tC: "OO",{bs_id},{gid}')
            nshapes = 0
            for sn in sorted(shp):
                Dfull = np.zeros((len(V), 3))
                for k, D in shp[sn].items(): Dfull[starts[k]:starts[k + 1]] = D
                nz = np.where(np.abs(Dfull).max(1) > 1e-6)[0]
                if len(nz) == 0: continue
                sg = ids(); ch = ids(); nshapes += 1
                O.append(f'\tGeometry: {sg}, "Geometry::{sn}", "Shape" {{\n\t\tVersion: 100\n\t\tIndexes: {_arr(nz)}\n'
                         f'\t\tVertices: {_arr(Dfull[nz] * CM, "{:.4f}")}\n\t\tNormals: {_arr(np.zeros((len(nz), 3)), "{:.1f}")}\n\t}}')
                O.append(f'\tDeformer: {ch}, "SubDeformer::{sn}", "BlendShapeChannel" {{\n\t\tVersion: 100\n\t\tDeformPercent: 0\n'
                         f'\t\tFullWeights: *1 {{\n\t\t\ta: 100\n\t\t}}\n\t}}')
                C.append(f'\tC: "OO",{ch},{bs_id}'); C.append(f'\tC: "OO",{sg},{ch}')
        # skinning de ce maillage
        if sk is not None:
            skin_id = ids()
            O.append(f'\tDeformer: {skin_id}, "Deformer::Skin_{mname}", "Skin" {{\n\t\tVersion: 101\n\t\tLink_DeformAcuracy: 50\n\t}}')
            C.append(f'\tC: "OO",{skin_id},{gid}')
            for bi, name in enumerate(sk.names):
                if sk.null[bi]: continue
                sel = (J == bi) & (W > 1e-5)
                vidx, slot_ = np.nonzero(sel)
                if len(vidx) == 0: continue
                wts = W[vidx, slot_]
                L = G[bi].copy(); L[:3, 3] *= CM
                cid = ids()
                O.append(f'\tDeformer: {cid}, "SubDeformer::{name}_{mname}", "Cluster" {{\n\t\tVersion: 100\n\t\tUserData: "", ""\n'
                         f'\t\tIndexes: {_arr(vidx)}\n\t\tWeights: {_arr(wts, "{:.5f}")}\n'
                         f'\t\tTransform: {_arr(_mat16(np.linalg.inv(L)), "{:.8f}")}\n\t\tTransformLink: {_arr(_mat16(L), "{:.8f}")}\n\t}}')
                C.append(f'\tC: "OO",{cid},{skin_id}'); C.append(f'\tC: "OO",{bone_ids[bi]},{cid}')
    if sk is not None:
        pid = ids()
        nodes = list(pose_nodes)
        for bi in range(len(sk.names)):
            L = G[bi].copy(); L[:3, 3] *= CM
            nodes.append(f'\t\tPoseNode:  {{\n\t\t\tNode: {bone_ids[bi]}\n\t\t\tMatrix: {_arr(_mat16(L), "{:.8f}")}\n\t\t}}')
        O.append(f'\tPose: {pid}, "Pose::BIND_POSES", "BindPose" {{\n\t\tType: "BindPose"\n\t\tVersion: 100\n\t\tNbPoseNodes: {len(nodes)}\n' + '\n'.join(nodes) + '\n\t}')

    # ------------------------------------------------------------------ animations
    stop_all = 0; takes = []
    for cname, clip in asset.clips.items():
        st = ids(); ly = ids()
        stop = int(round(clip.duration * KTIME)); stop_all = max(stop_all, stop); takes.append((cname, stop))
        O.append(f'\tAnimationStack: {st}, "AnimStack::{cname}", "" {{\n\t\tProperties70:  {{\n\t\t\tP: "LocalStop", "KTime", "Time", "",{stop}\n'
                 f'\t\t\tP: "ReferenceStop", "KTime", "Time", "",{stop}\n\t\t}}\n\t}}')
        O.append(f'\tAnimationLayer: {ly}, "AnimLayer::BaseLayer", "" {{\n\t}}')
        C.append(f'\tC: "OO",{ly},{st}')
        times = (np.arange(clip.n) * KTIME // clip.fps).astype(np.int64)
        for bi, name in enumerate(sk.names):
            chans = []
            if name in clip.rot:
                Q = clip.rot[name]
                E = np.array([mat_euler_xyz(qmat(q)) for q in Q])
                E = np.degrees(np.unwrap(np.radians(E), axis=0))
                chans.append(('R', 'Lcl Rotation', E))
            if name in clip.pos:
                T = (np.asarray(clip.pos[name]) + sk.local_t(bi)) * CM
                chans.append(('T', 'Lcl Translation', T))
            for short, prop, D in chans:
                cn = ids()
                O.append(f'\tAnimationCurveNode: {cn}, "AnimCurveNode::{short}", "" {{\n\t\tProperties70:  {{\n'
                         f'\t\t\tP: "d|X", "Number", "", "A",{D[0, 0]:.5f}\n\t\t\tP: "d|Y", "Number", "", "A",{D[0, 1]:.5f}\n\t\t\tP: "d|Z", "Number", "", "A",{D[0, 2]:.5f}\n\t\t}}\n\t}}')
                C.append(f'\tC: "OO",{cn},{ly}'); C.append(f'\tC: "OP",{cn},{bone_ids[bi]}, "{prop}"')
                for ax, axn in enumerate('XYZ'):
                    cv = ids()
                    O.append(f'\tAnimationCurve: {cv}, "AnimCurve::", "" {{\n\t\tDefault: {D[0, ax]:.5f}\n\t\tKeyVer: 4009\n'
                             f'\t\tKeyTime: {_arr(times)}\n\t\tKeyValueFloat: {_arr(D[:, ax], "{:.5f}")}\n'
                             f'\t\tKeyAttrFlags: *1 {{\n\t\t\ta: 24836\n\t\t}}\n\t\tKeyAttrDataFloat: *4 {{\n\t\t\ta: 0,0,255790911,0\n\t\t}}\n'
                             f'\t\tKeyAttrRefCount: *1 {{\n\t\t\ta: {clip.n}\n\t\t}}\n\t}}')
                    C.append(f'\tC: "OP",{cv},{cn}, "d|{axn}"')

    head = f'''; FBX 7.4.0 project file
; Created by {creator}
; ----------------------------------------------------

FBXHeaderExtension:  {{
\tFBXHeaderVersion: 1003
\tFBXVersion: 7400
\tCreationTimeStamp:  {{
\t\tVersion: 1000
\t\tYear: 2026
\t\tMonth: 10
\t\tDay: 22
\t\tHour: 12
\t\tMinute: 0
\t\tSecond: 0
\t\tMillisecond: 0
\t}}
\tCreator: "{creator}"
}}
GlobalSettings:  {{
\tVersion: 1000
\tProperties70:  {{
\t\tP: "UpAxis", "int", "Integer", "",1
\t\tP: "UpAxisSign", "int", "Integer", "",1
\t\tP: "FrontAxis", "int", "Integer", "",2
\t\tP: "FrontAxisSign", "int", "Integer", "",1
\t\tP: "CoordAxis", "int", "Integer", "",0
\t\tP: "CoordAxisSign", "int", "Integer", "",1
\t\tP: "OriginalUpAxis", "int", "Integer", "",1
\t\tP: "OriginalUpAxisSign", "int", "Integer", "",1
\t\tP: "UnitScaleFactor", "double", "Number", "",1
\t\tP: "OriginalUnitScaleFactor", "double", "Number", "",1
\t\tP: "AmbientColor", "ColorRGB", "Color", "",0,0,0
\t\tP: "DefaultCamera", "KString", "", "", "Producer Perspective"
\t\tP: "TimeMode", "enum", "", "",{time_mode}
\t\tP: "TimeSpanStart", "KTime", "Time", "",0
\t\tP: "TimeSpanStop", "KTime", "Time", "",{max(stop_all, KTIME)}
\t\tP: "CustomFrameRate", "double", "Number", "",-1
\t}}
}}
Documents:  {{
\tCount: 1
\tDocument: 1, "", "Scene" {{
\t\tProperties70:  {{
\t\t\tP: "SourceObject", "object", "", ""
\t\t\tP: "ActiveAnimStackName", "KString", "", "", ""
\t\t}}
\t\tRootNode: 0
\t}}
}}
References:  {{
}}
'''
    # définitions (comptes par type)
    counts = {}
    for o in O:
        t = o.strip().split(':')[0]; counts[t] = counts.get(t, 0) + 1
    defs = '\n'.join(f'\tObjectType: "{t}" {{\n\t\tCount: {c}\n\t}}' for t, c in counts.items())
    body = (head + f'Definitions:  {{\n\tVersion: 100\n\tCount: {sum(counts.values()) + 1}\n\tObjectType: "GlobalSettings" {{\n\t\tCount: 1\n\t}}\n{defs}\n}}\n'
            + 'Objects:  {\n' + '\n'.join(O) + '\n}\n' + 'Connections:  {\n' + '\n'.join(C) + '\n}\n'
            + 'Takes:  {\n\tCurrent: "' + (takes[0][0] if takes else '') + '"\n'
            + ''.join(f'\tTake: "{n}" {{\n\t\tFileName: "{n}.tak"\n\t\tLocalTime: 0,{t}\n\t\tReferenceTime: 0,{t}\n\t}}\n' for n, t in takes) + '}\n')
    if binary:
        from .fbxbin import to_binary
        with open(path, 'wb') as f: f.write(to_binary(body))
    else:
        with open(path, 'w', encoding='utf-8') as f: f.write(body)
    tris = sum(len(m.F) for _, m, _ in asset.parts); nv = sum(len(m.V) for _, m, _ in asset.parts)
    return dict(path=path, polygons=int(tris), vertices=int(nv), materials=list(mat_ids.keys()), clips=list(asset.clips.keys()), meshes=len(slots))
