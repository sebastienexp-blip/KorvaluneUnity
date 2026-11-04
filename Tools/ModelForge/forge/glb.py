"""Lecture minimale d'un fichier GLB (glTF 2.0 binaire) en tableaux numpy : maillages, UV, texture de couleur,
métal/rugosité et normales. Sert à importer les modèles générés à partir des concepts (TRELLIS…) dans ModelForge."""
import io, json, struct
import numpy as np
from PIL import Image

_CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
_NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def _q2m(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


class GLB:
    def __init__(self, path):
        data = open(path, 'rb').read()
        magic, ver, length = struct.unpack_from('<III', data, 0)
        assert magic == 0x46546C67, 'pas un GLB'
        off = 12; self.bin = b''
        while off < length:
            clen, ctype = struct.unpack_from('<II', data, off); chunk = data[off + 8: off + 8 + clen]
            if ctype == 0x4E4F534A: self.js = json.loads(chunk.decode())
            elif ctype == 0x004E4942: self.bin = chunk
            off += 8 + clen

    def accessor(self, i):
        a = self.js['accessors'][i]; bv = self.js['bufferViews'][a['bufferView']]
        dt = _CT[a['componentType']]; nc = _NC[a['type']]; n = a['count']
        start = bv.get('byteOffset', 0) + a.get('byteOffset', 0); stride = bv.get('byteStride', 0)
        isz = np.dtype(dt).itemsize * nc
        if stride and stride != isz:
            raw = np.frombuffer(self.bin, np.uint8, count=stride * (n - 1) + isz, offset=start)
            arr = np.stack([np.frombuffer(raw[k * stride: k * stride + isz].tobytes(), dt) for k in range(n)])
        else:
            arr = np.frombuffer(self.bin, dt, count=n * nc, offset=start).reshape(n, nc) if nc > 1 else np.frombuffer(self.bin, dt, count=n, offset=start)
        arr = arr.astype(np.float64) if dt == np.float32 else arr
        if a.get('normalized') and dt != np.float32: arr = arr / float(np.iinfo(dt).max)
        return arr

    def image(self, tex_index):
        if tex_index is None: return None
        src = self.js['textures'][tex_index]['source']; im = self.js['images'][src]; bv = self.js['bufferViews'][im['bufferView']]
        b = self.bin[bv.get('byteOffset', 0): bv.get('byteOffset', 0) + bv['byteLength']]
        return Image.open(io.BytesIO(b)).convert('RGBA')

    def node_matrix(self, n):
        if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
        M = np.eye(4)
        if 'scale' in n: M = np.diag(list(n['scale']) + [1]) @ M
        if 'rotation' in n: R = np.eye(4); R[:3, :3] = _q2m(n['rotation']); M = R @ M
        if 'translation' in n: T = np.eye(4); T[:3, 3] = n['translation']; M = T @ M
        return M

    def meshes(self):
        """[(V, N, UV, F, material_index)] en coordonnées monde (Y en haut, comme Unity, mais X inversé à l'export FBX)."""
        out = []
        nodes = self.js.get('nodes', [])
        scene = self.js.get('scenes', [{}])[self.js.get('scene', 0)]

        def walk(ni, P):
            n = nodes[ni]; M = P @ self.node_matrix(n)
            if 'mesh' in n:
                for pr in self.js['meshes'][n['mesh']]['primitives']:
                    at = pr['attributes']
                    V = self.accessor(at['POSITION']); V = V @ M[:3, :3].T + M[:3, 3]
                    N = self.accessor(at['NORMAL']) @ M[:3, :3].T if 'NORMAL' in at else None
                    UV = self.accessor(at['TEXCOORD_0']) if 'TEXCOORD_0' in at else np.zeros((len(V), 2))
                    F = self.accessor(pr['indices']).reshape(-1, 3).astype(np.int64) if 'indices' in pr else np.arange(len(V)).reshape(-1, 3)
                    out.append((V, N, UV, F, pr.get('material')))
            for c in n.get('children', []): walk(c, M)
        for r in scene.get('nodes', range(len(nodes))): walk(r, np.eye(4))
        return out

    def material(self, mi):
        """(couleur RGBA, métal/rugosité RGBA, normales RGBA, facteurs) d'un matériau glTF."""
        if mi is None: return None, None, None, {}
        m = self.js['materials'][mi]; pb = m.get('pbrMetallicRoughness', {})
        g = lambda d: d.get('index') if d else None
        return (self.image(g(pb.get('baseColorTexture'))), self.image(g(pb.get('metallicRoughnessTexture'))),
                self.image(g(m.get('normalTexture'))),
                dict(base=pb.get('baseColorFactor', [1, 1, 1, 1]), metallic=pb.get('metallicFactor', 1.0), roughness=pb.get('roughnessFactor', 1.0)))
