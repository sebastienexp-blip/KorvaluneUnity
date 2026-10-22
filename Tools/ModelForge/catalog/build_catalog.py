"""Catalogue de personnalisation (créateur de personnage) à partir des packs MakeHuman :
peaux, yeux, coiffures (+ variantes de couleur/matière), sourcils, cils. Les textures sont copiées une seule fois dans
Assets/Korvalune/Art/Catalog (peaux en JPG 2048, le reste en PNG), avec catalog.json (métadonnées, licences, sexe,
étiquettes) et CREDITS.md (auteurs, obligatoire pour les ressources CC-BY).

  python3 catalog/build_catalog.py /chemin/des/packs/décompressés [...]
"""
import os, sys, json, glob, re
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Korvalune', 'Art', 'Catalog')
EXCLUDE = {'joepal_annotated_skin'}          # texture de contrôle des UV, pas une peau


def mhmat(fn):
    d = {}
    for l in open(fn, encoding='utf-8', errors='replace'):
        l = l.strip()
        if not l or l[0] in '#/': continue
        k, _, v = l.partition(' '); d[k] = v.strip()
    return d


def find_tex(folder, rel):
    if not rel: return None
    p = os.path.join(folder, os.path.basename(rel))
    return p if os.path.exists(p) else None


def save(src, dst, size, mode, fmt):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if os.path.exists(dst): return
    im = Image.open(src).convert(mode)
    if max(im.size) > size: im = im.resize((size, size), Image.LANCZOS)
    if fmt == 'JPEG': im.save(dst, 'JPEG', quality=90, optimize=True)
    else: im.save(dst, 'PNG', optimize=True)


def sex_of(name):
    n = name.lower()
    if 'female' in n or 'woman' in n or 'alana' in n or 'zoey' in n: return 'F'
    if re.search(r'(^|[_\-])male', n) or 'aksel' in n or 'viking' in n: return 'M'
    return 'MF'


def main(roots):
    meta = {}
    for r in roots:
        for f in glob.glob(os.path.join(r, '*', 'packs', '*.json')) + glob.glob(os.path.join(r, 'packs', '*.json')):
            pack = os.path.basename(f)[:-5]
            for k, v in json.load(open(f)).items(): meta[k] = dict(v, pack=pack)
    def m(k): return meta.get(k, {})
    def lic(k): return m(k).get('license', 'CC0')
    cat = dict(skins=[], eyes=[], hair=[], eyebrows=[], eyelashes=[])
    seen = set()
    dirs = []
    for r in roots: dirs += glob.glob(os.path.join(r, '*'))
    # ---- peaux
    for f in sorted(glob.glob(os.path.join('{' , '')) or []): pass
    for r in roots:
        for mf in sorted(glob.glob(os.path.join(r, '*', 'skins', '*', '*.mhmat'))):
            sid = os.path.basename(os.path.dirname(mf))
            if sid in seen or sid in EXCLUDE: continue
            d = mhmat(mf); tex = find_tex(os.path.dirname(mf), d.get('diffuseTexture'))
            if not tex: continue
            seen.add(sid)
            save(tex, os.path.join(OUT, 'Skins', sid + '.jpg'), 2048, 'RGB', 'JPEG')
            nrm = find_tex(os.path.dirname(mf), d.get('normalmapTexture'))
            if nrm: save(nrm, os.path.join(OUT, 'Skins', sid + '_Normal.png'), 2048, 'RGB', 'PNG')
            tags = []
            n = sid.lower()
            if any(t in n for t in ('zombie', 'alien', 'elf', 'orc', 'ork', 'tigra', 'leopard', 'fur', 'walker', 'baboon', 'manhattan', 'grey_skinned', 'bio_armor')): tags.append('fantasy')
            if 'genitals' in n: tags.append('adult')
            if 'suit' in n: tags.append('costume')
            if 'makeup' in n or 'eyeliner' in n or 'emo' in n or 'goth' in n: tags.append('makeup')
            if 'tattoo' in n: tags.append('tattoo')
            cat['skins'].append(dict(id=sid, file=f'Skins/{sid}.jpg', normal=f'Skins/{sid}_Normal.png' if nrm else None, sex=sex_of(sid), tags=tags,
                                     license=lic(sid), author=m(sid).get('author', 'MakeHuman'), source=m(sid).get('source', '')))
    # ---- yeux (matériaux d'iris, même globe)
    for r in roots:
        for mf in sorted(glob.glob(os.path.join(r, '*', 'eyes', 'materials', '*.mhmat')) + glob.glob(os.path.join(r, '*', 'eyes', 'materials', '*', '*.mhmat'))):
            eid = os.path.splitext(os.path.basename(mf))[0]
            if ('eye:' + eid) in seen: continue
            d = mhmat(mf); tex = find_tex(os.path.dirname(mf), d.get('diffuseTexture'))
            if not tex: continue
            seen.add('eye:' + eid)
            save(tex, os.path.join(OUT, 'Eyes', eid + '.png'), 1024, 'RGB', 'PNG')
            cat['eyes'].append(dict(id=eid, file=f'Eyes/{eid}.png', license=lic(eid), author=m(eid).get('author', 'MakeHuman'), source=m(eid).get('source', '')))
    # ---- coiffures (géométrie : proxys .mhclo) et variantes de matière
    styles = {}
    for r in roots:
        for cf in sorted(glob.glob(os.path.join(r, '*', 'hair', '*', '*.mhclo'))):
            hid = os.path.basename(os.path.dirname(cf))
            if hid in styles: continue
            folder = os.path.dirname(cf)
            mats = [x for x in glob.glob(os.path.join(folder, '*.mhmat'))]
            styles[hid] = dict(id=hid, folder=folder, variants=[], license=lic(hid), author=m(hid).get('author', 'MakeHuman'), source=m(hid).get('source', ''))
            for mf in mats:
                d = mhmat(mf); tex = find_tex(folder, d.get('diffuseTexture'))
                if tex: styles[hid]['variants'].append(dict(id='default', tex=tex, nrm=find_tex(folder, d.get('normalmapTexture') or d.get('bumpmapTexture')),
                                                             license=lic(hid), author=styles[hid]['author']))
    for r in roots:
        for mf in sorted(glob.glob(os.path.join(r, '*', 'hair', '*', '*', '*.mhmat'))):
            hid = os.path.basename(os.path.dirname(os.path.dirname(mf))); vid = os.path.basename(os.path.dirname(mf))
            if hid not in styles: continue
            d = mhmat(mf); tex = find_tex(os.path.dirname(mf), d.get('diffuseTexture'))
            if not tex or any(v['id'] == vid for v in styles[hid]['variants']): continue
            styles[hid]['variants'].append(dict(id=vid, tex=tex, nrm=find_tex(os.path.dirname(mf), d.get('normalmapTexture')), license=lic(vid), author=m(vid).get('author', '')))
    for hid, s in sorted(styles.items()):
        vv = []
        for v in s['variants']:
            save(v['tex'], os.path.join(OUT, 'Hair', hid, v['id'] + '_BaseMap.png'), 1024, 'RGBA', 'PNG')
            if v['nrm']: save(v['nrm'], os.path.join(OUT, 'Hair', hid, v['id'] + '_Normal.png'), 1024, 'RGB', 'PNG')
            vv.append(dict(id=v['id'], file=f"Hair/{hid}/{v['id']}_BaseMap.png", normal=f"Hair/{hid}/{v['id']}_Normal.png" if v['nrm'] else None, license=v['license'], author=v['author']))
        cat['hair'].append(dict(id=hid, proxy=os.path.relpath(s['folder'], '/'), variants=vv, sex=('F' if any(t in hid for t in ('bob', 'braid', 'long', 'ponytail', 'bun', 'toigo')) else 'MF'),
                                license=s['license'], author=s['author'], source=s['source']))
    # ---- sourcils / cils (proxys)
    for kind in ('eyebrows', 'eyelashes'):
        for r in roots:
            for cf in sorted(glob.glob(os.path.join(r, '*', kind, '*', '*.mhclo'))):
                bid = os.path.basename(os.path.dirname(cf))
                if any(x['id'] == bid for x in cat[kind]): continue
                folder = os.path.dirname(cf); mats = glob.glob(os.path.join(folder, '*.mhmat'))
                tex = find_tex(folder, mhmat(mats[0]).get('diffuseTexture')) if mats else None
                if tex: save(tex, os.path.join(OUT, kind.capitalize(), bid + '_BaseMap.png'), 1024, 'RGBA', 'PNG')
                nv = sum(1 for l in open(glob.glob(os.path.join(folder, '*.obj'))[0], errors='replace') if l.startswith('v '))
                cat[kind].append(dict(id=bid, proxy=os.path.relpath(folder, '/'), file=f'{kind.capitalize()}/{bid}_BaseMap.png' if tex else None,
                                      hd=nv > 5000, license=lic(bid), author=m(bid).get('author', 'MakeHuman'), source=m(bid).get('source', '')))
    os.makedirs(OUT, exist_ok=True)
    json.dump(cat, open(os.path.join(OUT, 'catalog.json'), 'w'), indent=1, ensure_ascii=False)
    # ---- crédits
    rows = []
    for k, lst in cat.items():
        for x in lst:
            rows.append((x.get('license', 'CC0'), k, x['id'], x.get('author', ''), x.get('source', '')))
            for v in x.get('variants', []):
                if v['id'] != 'default': rows.append((v.get('license', 'CC0'), k + ' (variante)', v['id'], v.get('author', ''), ''))
    with open(os.path.join(OUT, 'CREDITS.md'), 'w', encoding='utf-8') as f:
        f.write('# Crédits des ressources de personnalisation\n\nRessources issues des packs de la communauté MakeHuman (makehumancommunity.org).\n\n')
        for L in ('CC-BY', 'CC0'):
            sel = [r for r in rows if r[0].upper().startswith(L)]
            f.write(f'## Licence {L} ({len(sel)})\n\n' + ('Attribution obligatoire : ces auteurs doivent apparaître dans les crédits du jeu.\n\n' if L == 'CC-BY' else 'Domaine public : attribution facultative, donnée par courtoisie.\n\n'))
            for r in sorted(sel): f.write(f'- {r[1]} « {r[2]} » — {r[3]}' + (f' ({r[4]})' if r[4] else '') + '\n')
            f.write('\n')
    print({k: len(v) for k, v in cat.items()}, 'variantes cheveux', sum(len(h['variants']) for h in cat['hair']))


if __name__ == '__main__':
    main(sys.argv[1:])
