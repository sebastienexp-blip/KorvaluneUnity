"""Génération GRATUITE des créatures : modèles 3D texturés à partir des concepts, avec les modèles ouverts hébergés sur
Hugging Face (TRELLIS de Microsoft, sinon Hunyuan3D-2 de Tencent), appelés depuis GitHub Actions avec gradio_client.
Aucun compte payant ; un jeton Hugging Face gratuit (secret HF_TOKEN) donne plus de temps de calcul, mais n'est pas
obligatoire. Les squelettes et animations sont posés ensuite dans ModelForge (pas de rig payant).

  python3 Tools/Meshy/generate_free.py abbe wendigo ...

concepts/<nom>.png → out/<nom>/modele.glb (+ journal.txt). Une créature déjà générée n'est pas relancée."""
import os, shutil, sys, time, traceback

LIMIT = 900   # s max par appel : une file d'attente bloquée ne fige plus tout le travail


def call(c, **kw):
    return c.submit(**kw).result(timeout=LIMIT)

HERE = os.path.dirname(os.path.abspath(__file__))
NAMES = ['abbe', 'wendigo', 'tisseuse', 'salamandre', 'basilic', 'voyant']
TOKEN = os.environ.get('HF_TOKEN') or None


def log(out, *a):
    s = ' '.join(str(x) for x in a); print(s, flush=True)
    with open(os.path.join(out, 'journal.txt'), 'a') as f: f.write(s + '\n')


def client(space):
    from gradio_client import Client
    if not TOKEN: return Client(space, verbose=False)
    try: return Client(space, token=TOKEN, verbose=False)            # gradio_client récent
    except TypeError: return Client(space, hf_token=TOKEN, verbose=False)


def first_file(res):
    """Premier chemin de fichier .glb trouvé dans un résultat gradio (tuples, dicts, chaînes)."""
    stack = [res]
    while stack:
        x = stack.pop(0)
        if isinstance(x, str) and x.lower().endswith(('.glb', '.gltf', '.obj')) and os.path.exists(x): return x
        if isinstance(x, dict): stack.extend(x.values())
        if isinstance(x, (list, tuple)): stack.extend(x)
    return None


SPACES = ['trellis-community/TRELLIS', 'tencent/Hunyuan3D-2', 'tencent/Hunyuan3D-2mini-Turbo', 'VAST-AI/TripoSG',
          'stabilityai/stable-fast-3d', 'tencent/Hunyuan3D-2.1', 'JeffreyXiang/TRELLIS']


def dump_api(c, space):
    """Écrit la description complète de l'API d'un espace (pour écrire les appels exacts ensuite)."""
    import json
    try:
        api = c.view_api(return_format='dict', print_info=False)
        d = os.path.join(HERE, 'out', '_api'); os.makedirs(d, exist_ok=True)
        json.dump(api, open(os.path.join(d, space.replace('/', '__') + '.json'), 'w'), indent=1, default=str)
        return api
    except Exception as e:
        print('  view_api', space, e, flush=True); return None


def fill(params, img, out):
    """Arguments d'un point d'entrée : l'image du concept, détourage et texture activés, valeurs par défaut sinon."""
    from gradio_client import handle_file
    kw = {}
    for p in params:
        n = p.get('parameter_name') or p.get('label'); low = (n or '').lower(); typ = str(p.get('python_type', {}).get('type', '')).lower()
        if not n: continue
        if low in ('image', 'image_prompt', 'input_image', 'img') or ('image' in low and 'dict' in typ and 'list' not in typ): kw[n] = handle_file(img)
        elif low in ('images', 'multiimages', 'mv_image_front'): continue
        elif 'rembg' in low or 'remove_bg' in low or 'background' in low: kw[n] = True
        elif 'texture' in low and 'bool' in typ: kw[n] = True
        elif 'seed' == low: kw[n] = 7
    return kw


def generic(space, img, out):
    c = client(space); api = dump_api(c, space) or {}
    eps = api.get('named_endpoints', {})
    log(out, f'  {space} : points d\'entrée', list(eps))
    if '/start_session' in eps:
        try: call(c, api_name='/start_session')
        except Exception as e: log(out, '   (start_session', repr(e)[:200], ')')
    order = [n for k in ('generate_and_extract_glb', 'image_to_3d', 'generation_all', 'generate', 'run', 'shape_generation', 'predict')
             for n in eps if k in n]
    seen = set()
    for ep in order:
        if ep in seen: continue
        seen.add(ep)
        kw = fill(eps[ep]['parameters'], img, out)
        if not any(hasattr(v, 'get') or isinstance(v, dict) for v in kw.values()) and not any('image' in k.lower() for k in kw): continue
        log(out, '   appel', ep, list(kw))
        try:
            res = call(c, api_name=ep, **kw)
        except Exception as e:
            log(out, '   échec', ep, ':', repr(e)[:400]); continue
        f = first_file(res)
        if not f and any('extract_glb' in n for n in eps):
            try: f = first_file(call(c, api_name=next(n for n in eps if 'extract_glb' in n), mesh_simplify=0.85, texture_size=2048))
            except Exception as e: log(out, '   extract_glb', repr(e)[:300])
        if f: return f
        log(out, '   résultat sans fichier :', str(res)[:300])
    return None


def run(name):
    out = os.path.join(HERE, 'out', name); os.makedirs(out, exist_ok=True)
    dst = os.path.join(out, 'modele.glb')
    if os.path.exists(dst) and os.path.getsize(dst) > 10000:
        print(f'== {name} : déjà généré', flush=True); return True
    log(out, f'== {name}  ({time.strftime("%H:%M:%S")})')
    img = os.path.join(HERE, 'concepts', f'{name}.png')
    for space in SPACES:
        try: f = generic(space, img, out)
        except Exception as e: log(out, '  échec', space, ':', repr(e)[:400]); f = None
        if f:
            ext = os.path.splitext(f)[1].lower(); dst = os.path.join(out, 'modele' + ext)
            shutil.copy(f, dst); log(out, f'  ✅ {name} via {space} : {os.path.getsize(dst) // 1024} Ko'); return True
    log(out, f'  ❌ {name} : aucune génération réussie')
    return False


if __name__ == '__main__':
    names = [n for n in sys.argv[1:] if n in NAMES] or NAMES
    ok = 0
    for n in names:
        try: ok += bool(run(n))
        except Exception: traceback.print_exc()
    print(f'Réussies : {ok} / {len(names)}', flush=True)
