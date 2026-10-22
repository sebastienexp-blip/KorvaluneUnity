"""FBX binaire 7.4 (tableaux compressés zlib) à partir du FBX ASCII produit par fbx.py : même contenu, 5 à 10 fois plus léger,
import plus rapide dans Unity. Constantes d'en-tête / pied de fichier identiques à celles de l'exportateur FBX de Blender
(horodatage fixe et identifiants associés, exigés par le SDK Autodesk)."""
import re, struct, zlib
import numpy as np

_TIME_ID = b'1970-01-01 10:00:00:000'
_FILE_ID = b'\x28\xb3\x2a\xeb\xb6\x24\xcc\xc2\xbf\xc8\xb0\x2a\xa9\x2b\xfc\xf1'
_FOOT_ID = b'\xfa\xbc\xab\x09\xd0\xc8\xd4\x66\xb1\x76\xfb\x83\x1c\xf7\x26\x7e'
_FOOT_MAGIC = b'\xf8\x5a\x8c\x6a\xde\xf5\xd9\x7e\xec\xe9\x0c\xe3\x75\x8f\x29\x0b'
VERSION = 7400

ARRAY_TYPES = {'Vertices': 'd', 'PolygonVertexIndex': 'i', 'Normals': 'd', 'UV': 'd', 'UVIndex': 'i', 'Materials': 'i', 'Indexes': 'i',
               'Weights': 'd', 'Transform': 'd', 'TransformLink': 'd', 'Matrix': 'd', 'KeyTime': 'l', 'KeyValueFloat': 'f',
               'KeyAttrFlags': 'i', 'KeyAttrDataFloat': 'f', 'KeyAttrRefCount': 'i', 'FullWeights': 'd', 'Edges': 'i'}
DOUBLE_LEAVES = {'Default', 'DeformPercent', 'Link_DeformAcuracy'}
LONG_FIRST = {'Model', 'Geometry', 'Material', 'Deformer', 'NodeAttribute', 'Pose', 'AnimationStack', 'AnimationLayer', 'AnimationCurveNode',
              'AnimationCurve', 'Document', 'Texture', 'Video', 'Node', 'RootNode'}
P_INT = {'int', 'enum', 'bool', 'Integer', 'Bool', 'Visibility', 'Visibility Inheritance', 'ULongLong'}
P_LONG = {'KTime'}
P_STR = {'KString', 'object', 'Compound', 'DateTime', 'Url', 'XRefUrl'}


class Node:
    __slots__ = ('name', 'props', 'children')
    def __init__(self, name, props=None):
        self.name, self.props, self.children = name, props or [], []


def _split(s):
    out, cur, q = [], '', False
    for ch in s:
        if ch == '"': q = not q; cur += ch
        elif ch == ',' and not q: out.append(cur.strip()); cur = ''
        else: cur += ch
    if cur.strip() != '': out.append(cur.strip())
    return out


def _scalar(tok, name, idx, ptype=None):
    if tok.startswith('"'): return ('S', tok[1:-1].encode('utf-8'))
    if name == 'Shading' or tok in ('Y', 'T', 'N', 'W'): return ('C', tok in ('Y', 'T', 'W'))
    is_float = any(c in tok for c in '.eE') and not tok.lstrip('-').isdigit()
    if name == 'P' and ptype is not None:
        if ptype in P_LONG: return ('L', int(float(tok)))
        if ptype in P_INT: return ('I', int(float(tok)))
        return ('D', float(tok))
    if name in DOUBLE_LEAVES: return ('D', float(tok))
    if idx == 0 and name in LONG_FIRST: return ('L', int(tok))
    if name == 'C' and idx > 0: return ('L', int(tok))
    if is_float: return ('D', float(tok))
    v = int(tok)
    return ('L', v) if abs(v) > 2 ** 31 - 1 else ('I', v)


def parse(text):
    root = Node('')
    stack = [root]
    lines = text.split('\n')
    i, n = 0, len(lines)
    while i < n:
        line = lines[i].strip(); i += 1
        if not line or line.startswith(';'): continue
        if line == '}':
            stack.pop(); continue
        name, _, rest = line.partition(':')
        rest = rest.strip()
        m = re.match(r'^\*(\d+)\s*\{$', rest)
        if m:   # tableau
            data = lines[i].strip(); i += 1
            body = data[2:].strip() if data.startswith('a:') else ''
            t = ARRAY_TYPES.get(name, 'd')
            if body:
                if t == 'f' and name == 'KeyAttrDataFloat':
                    vals = np.array([int(x) for x in body.split(',')], np.int32).view(np.float32)
                else:
                    vals = np.array([float(x) for x in body.split(',')], {'d': np.float64, 'f': np.float32, 'i': np.int32, 'l': np.int64}[t])
            else:
                vals = np.zeros(0, {'d': np.float64, 'f': np.float32, 'i': np.int32, 'l': np.int64}[t])
            stack[-1].children.append(Node(name, [(t, vals)]))
            # ligne « } » qui ferme le tableau
            while i < n and lines[i].strip() != '}': i += 1
            i += 1
            continue
        opens = rest.endswith('{')
        if opens: rest = rest[:-1].strip()
        toks = _split(rest) if rest else []
        ptype = toks[1][1:-1] if name == 'P' and len(toks) > 1 else None
        props = []
        for k, t in enumerate(toks):
            if name == 'P' and k < 4: props.append(('S', t[1:-1].encode('utf-8'))); continue
            if k == 1 and name in LONG_FIRST and t.startswith('"') and '::' in t:
                cls, _, nm = t[1:-1].partition('::')          # binaire : « Nom\x00\x01Classe » au lieu de « Classe::Nom »
                props.append(('S', nm.encode('utf-8') + b'\x00\x01' + cls.encode('utf-8'))); continue
            props.append(_scalar(t, name, k, ptype))
        node = Node(name, props)
        stack[-1].children.append(node)
        if opens: stack.append(node)
    return root


def _prop_bytes(p):
    t, v = p
    if t == 'C': return b'C' + struct.pack('<?', bool(v))
    if t == 'I': return b'I' + struct.pack('<i', v)
    if t == 'L': return b'L' + struct.pack('<q', v)
    if t == 'D': return b'D' + struct.pack('<d', v)
    if t == 'F': return b'F' + struct.pack('<f', v)
    if t == 'S': return b'S' + struct.pack('<I', len(v)) + v
    if t == 'R': return b'R' + struct.pack('<I', len(v)) + v
    data = np.ascontiguousarray(v).tobytes()
    if len(data) > 128:
        comp = zlib.compress(data, 6)
        return t.encode() + struct.pack('<III', len(v), 1, len(comp)) + comp
    return t.encode() + struct.pack('<III', len(v), 0, len(data)) + data


def _write_node(out, node, offset):
    props = b''.join(_prop_bytes(p) for p in node.props)
    name = node.name.encode()
    head_len = 13 + len(name)
    body = bytearray()
    pos = offset + head_len + len(props)
    for c in node.children:
        b = _write_node(bytearray(), c, pos)
        body += b; pos += len(b)
    if node.children or not node.props:
        body += b'\0' * 13
    end = offset + head_len + len(props) + len(body)
    out += struct.pack('<IIIB', end, len(node.props), len(props), len(name)) + name + props + body
    return out


def to_binary(text):
    root = parse(text)
    # éléments de premier niveau exigés par le SDK pour les fichiers binaires
    hdr = next((c for c in root.children if c.name == 'FBXHeaderExtension'), None)
    if hdr is not None and not any(c.name == 'EncryptionType' for c in hdr.children):
        hdr.children.insert(2, Node('EncryptionType', [('I', 0)]))
    extra = [Node('FileId', [('R', _FILE_ID)]), Node('CreationTime', [('S', _TIME_ID)]), Node('Creator', [('S', b'Korvalune ModelForge')])]
    idx = root.children.index(hdr) + 1 if hdr is not None else 0
    root.children[idx:idx] = extra
    out = bytearray(b'Kaydara FBX Binary  \x00\x1a\x00' + struct.pack('<I', VERSION))
    for c in root.children:
        b = _write_node(bytearray(), c, len(out)); out += b
    out += b'\0' * 13
    out += _FOOT_ID + b'\0\0\0\0'
    pad = ((len(out) + 15) & ~15) - len(out)
    if pad == 0: pad = 16
    out += b'\0' * pad
    out += struct.pack('<I', VERSION) + b'\0' * 120 + _FOOT_MAGIC
    return bytes(out)


def read(data):
    """Relecture d'un FBX binaire (contrôle) : renvoie l'arbre de nœuds."""
    assert data[:21] == b'Kaydara FBX Binary  \x00'
    pos = 27
    def node(pos):
        end, nprops, plen, nlen = struct.unpack_from('<IIIB', data, pos)
        if end == 0: return None, pos + 13
        name = data[pos + 13:pos + 13 + nlen].decode(); p = pos + 13 + nlen
        props = []
        for _ in range(nprops):
            t = chr(data[p]); p += 1
            if t in 'CYIFDL':
                fmt = {'C': '<?', 'Y': '<h', 'I': '<i', 'F': '<f', 'D': '<d', 'L': '<q'}[t]
                props.append((t, struct.unpack_from(fmt, data, p)[0])); p += struct.calcsize(fmt)
            elif t in 'SR':
                ln = struct.unpack_from('<I', data, p)[0]; props.append((t, data[p + 4:p + 4 + ln])); p += 4 + ln
            else:
                ln, enc, cl = struct.unpack_from('<III', data, p); raw = data[p + 12:p + 12 + cl]; p += 12 + cl
                raw = zlib.decompress(raw) if enc else raw
                props.append((t, np.frombuffer(raw, {'d': np.float64, 'f': np.float32, 'i': np.int32, 'l': np.int64, 'b': np.int8}[t])))
        nd = Node(name, props)
        while p < end:
            c, p = node(p)
            if c is None: break
            nd.children.append(c)
        return nd, end
    root = Node('')
    while pos < len(data):
        c, pos = node(pos)
        if c is None: break
        root.children.append(c)
    return root
