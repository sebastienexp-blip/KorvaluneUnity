"""Écrivain GLB minimal : maillage skinné, matériaux PBR avec textures, animations."""
import json, struct, io
import numpy as np
from PIL import Image


class Glb:
    def __init__(self):
        self.bin = bytearray(); self.views = []; self.accs = []; self.images = []; self.textures = []; self.materials = []
        self.nodes = []; self.meshes = []; self.skins = []; self.anims = []; self.scene_nodes = []

    def _view(self, data, target=None):
        while len(self.bin) % 4: self.bin.append(0)
        off = len(self.bin); self.bin += data
        v = {"buffer": 0, "byteOffset": off, "byteLength": len(data)}
        if target: v["target"] = target
        self.views.append(v); return len(self.views) - 1

    def acc(self, arr, ctype, atype, target=None, minmax=False):
        arr = np.ascontiguousarray(arr)
        vi = self._view(arr.tobytes(), target)
        a = {"bufferView": vi, "componentType": ctype, "count": int(arr.shape[0]), "type": atype}
        if minmax: a["min"] = arr.min(axis=0).astype(float).tolist(); a["max"] = arr.max(axis=0).astype(float).tolist()
        self.accs.append(a); return len(self.accs) - 1

    def image(self, img):
        b = io.BytesIO(); img.save(b, "PNG"); vi = self._view(b.getvalue())
        self.images.append({"bufferView": vi, "mimeType": "image/png"})
        self.textures.append({"source": len(self.images) - 1, "sampler": 0}); return len(self.textures) - 1

    def material(self, name, albedo=None, normal=None, mr=None, base=(1, 1, 1, 1), emissive=None, emissive_tex=None, metallic=0.0, rough=1.0, alpha=None):
        pbr = {"baseColorFactor": list(base), "metallicFactor": metallic, "roughnessFactor": rough}
        m = {"name": name, "pbrMetallicRoughness": pbr, "doubleSided": True}
        if albedo is not None: pbr["baseColorTexture"] = {"index": self.image(albedo)}
        if mr is not None: pbr["metallicRoughnessTexture"] = {"index": self.image(mr)}
        if normal is not None: m["normalTexture"] = {"index": self.image(normal), "scale": 1.0}
        if emissive is not None: m["emissiveFactor"] = list(emissive)
        if emissive_tex is not None: m["emissiveTexture"] = {"index": self.image(emissive_tex)}
        if alpha: m["alphaMode"] = alpha
        self.materials.append(m); return len(self.materials) - 1

    def save(self, path):
        doc = {"asset": {"version": "2.0", "generator": "Korvalune ModelForge"}, "scene": 0, "scenes": [{"nodes": self.scene_nodes}],
               "nodes": self.nodes, "meshes": self.meshes, "accessors": self.accs, "bufferViews": self.views, "buffers": [{"byteLength": len(self.bin)}]}
        if self.skins: doc["skins"] = self.skins
        if self.anims: doc["animations"] = self.anims
        if self.materials: doc["materials"] = self.materials
        if self.images: doc["images"] = self.images; doc["textures"] = self.textures; doc["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
        js = json.dumps(doc, separators=(",", ":")).encode()
        while len(js) % 4: js += b" "
        b = bytes(self.bin)
        while len(b) % 4: b += b"\0"
        total = 12 + 8 + len(js) + 8 + len(b)
        with open(path, "wb") as f:
            f.write(struct.pack("<III", 0x46546C67, 2, total))
            f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
            f.write(struct.pack("<II", len(b), 0x004E4942)); f.write(b)


F32, U16, U32 = 5126, 5123, 5125


def add_skinned(g, name, parts, bones, mats, anims=None, extra_nodes=None):
    """parts: liste de Part (géométrie en pose de repos). bones: dict nom -> (parent, tête monde).
    Crée la hiérarchie d'os, le maillage skinné (une primitive par matériau) et les animations."""
    names = list(bones.keys()); idx = {n: i for i, n in enumerate(names)}
    base = len(g.nodes)
    for n in names:
        p, head = bones[n]; ph = bones[p][1] if p else (0, 0, 0)
        node = {"name": n, "translation": [float(head[0] - ph[0]), float(head[1] - ph[1]), float(head[2] - ph[2])]}
        g.nodes.append(node)
    for n in names:
        p = bones[n][0]
        if p: g.nodes[base + idx[p]].setdefault("children", []).append(base + idx[n])
    roots = [base + idx[n] for n in names if not bones[n][0]]
    ibm = np.zeros((len(names), 16), np.float32)
    for n in names:
        h = bones[n][1]; m = np.eye(4, dtype=np.float32); m[:3, 3] = -np.array(h, np.float32); ibm[idx[n]] = m.T.reshape(16)
    ibm_acc = g.acc(ibm.reshape(-1, 16).reshape(len(names), 16), F32, "MAT4") if False else None
    # MAT4 : tableau (n,16)
    vi = g._view(ibm.tobytes()); g.accs.append({"bufferView": vi, "componentType": F32, "count": len(names), "type": "MAT4"}); ibm_acc = len(g.accs) - 1
    g.skins.append({"inverseBindMatrices": ibm_acc, "joints": [base + i for i in range(len(names))], "skeleton": roots[0]})
    skin_i = len(g.skins) - 1
    prims = []
    bymat = {}
    for p in parts: bymat.setdefault(p.mat, []).append(p)
    for mat, ps in bymat.items():
        pos = np.concatenate([p.pos for p in ps]).astype(np.float32); nrm = np.concatenate([p.nrm for p in ps]).astype(np.float32)
        uv = np.concatenate([p.uv for p in ps]).astype(np.float32); jn = np.concatenate([[[idx[b] for b in row] for row in p.joints] for p in ps]).astype(np.uint16)
        w = np.concatenate([p.weights for p in ps]).astype(np.float32); w /= np.maximum(w.sum(1, keepdims=True), 1e-6)
        off = 0; ind = []
        for p in ps: ind.append(p.idx + off); off += len(p.pos)
        ind = np.concatenate(ind).astype(np.uint32).reshape(-1)
        attrs = {"POSITION": g.acc(pos, F32, "VEC3", 34962, True), "NORMAL": g.acc(nrm, F32, "VEC3", 34962), "TEXCOORD_0": g.acc(uv, F32, "VEC2", 34962),
                 "JOINTS_0": g.acc(jn, U16, "VEC4", 34962), "WEIGHTS_0": g.acc(w, F32, "VEC4", 34962)}
        prims.append({"attributes": attrs, "indices": g.acc(ind, U32, "SCALAR", 34963), "material": mat})
    g.meshes.append({"name": name, "primitives": prims})
    mesh_node = len(g.nodes); g.nodes.append({"name": name + "_mesh", "mesh": len(g.meshes) - 1, "skin": skin_i})
    root = len(g.nodes); g.nodes.append({"name": name, "children": roots + [mesh_node]}); g.scene_nodes.append(root)
    for an in (anims or []):
        samplers = []; channels = []
        for bone, (times, quats) in an["rot"].items():
            ti = g.acc(np.array(times, np.float32), F32, "SCALAR", None, True); qi = g.acc(np.array(quats, np.float32), F32, "VEC4")
            samplers.append({"input": ti, "output": qi, "interpolation": "LINEAR"}); channels.append({"sampler": len(samplers) - 1, "target": {"node": base + idx[bone], "path": "rotation"}})
        for bone, (times, trs) in an.get("pos", {}).items():
            ti = g.acc(np.array(times, np.float32), F32, "SCALAR", None, True); qi = g.acc(np.array(trs, np.float32), F32, "VEC3")
            samplers.append({"input": ti, "output": qi, "interpolation": "LINEAR"}); channels.append({"sampler": len(samplers) - 1, "target": {"node": base + idx[bone], "path": "translation"}})
        g.anims.append({"name": an["name"], "samplers": samplers, "channels": channels})
    return root
