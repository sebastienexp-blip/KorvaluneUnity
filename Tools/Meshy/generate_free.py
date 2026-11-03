"""Génération GRATUITE des créatures : modèles 3D texturés à partir des concepts, avec les modèles ouverts hébergés sur
Hugging Face (TRELLIS de Microsoft, sinon Hunyuan3D-2 de Tencent), appelés depuis GitHub Actions avec gradio_client.
Aucun compte payant ; un jeton Hugging Face gratuit (secret HF_TOKEN) donne plus de temps de calcul, mais n'est pas
obligatoire. Les squelettes et animations sont posés ensuite dans ModelForge (pas de rig payant).

  python3 Tools/Meshy/generate_free.py abbe wendigo ...

concepts/<nom>.png → out/<nom>/modele.glb (+ journal.txt). Une créature déjà générée n'est pas relancée."""
import os, shutil, sys, time, traceback

HERE = os.path.dirname(os.path.abspath(__file__))
NAMES = ['abbe', 'wendigo', 'tisseuse', 'salamandre', 'basilic', 'voyant']
TOKEN = os.environ.get('HF_TOKEN') or None


def log(out, *a):
    s = ' '.join(str(x) for x in a); print(s, flush=True)
    with open(os.path.join(out, 'journal.txt'), 'a') as f: f.write(s + '\n')


def first_file(res):
    """Premier chemin de fichier .glb trouvé dans un résultat gradio (tuples, dicts, chaînes)."""
    stack = [res]
    while stack:
        x = stack.pop(0)
        if isinstance(x, str) and x.lower().endswith(('.glb', '.gltf', '.obj')) and os.path.exists(x): return x
        if isinstance(x, dict): stack.extend(x.values())
        if isinstance(x, (list, tuple)): stack.extend(x)
    return None


def trellis(img, out):
    from gradio_client import Client, handle_file
    for space in ('trellis-community/TRELLIS', 'JeffreyXiang/TRELLIS'):
        try:
            log(out, f'  TRELLIS : {space}')
            c = Client(space, hf_token=TOKEN, verbose=False)
            try: log(out, '  API :', str(c.view_api(return_format='str', print_info=False))[:1500])
            except Exception: pass
            try: c.predict(api_name='/start_session')
            except Exception as e: log(out, '  (start_session :', e, ')')
            im = handle_file(img)
            try: im = c.predict(image=im, api_name='/preprocess_image') or im
            except Exception as e: log(out, '  (preprocess :', e, ')')
            c.predict(image=im if not isinstance(im, str) else handle_file(im), multiimages=[], seed=7, ss_guidance_strength=7.5, ss_sampling_steps=12,
                      slat_guidance_strength=3.0, slat_sampling_steps=12, multiimage_algo='stochastic', api_name='/image_to_3d')
            res = c.predict(mesh_simplify=0.85, texture_size=2048, api_name='/extract_glb')
            f = first_file(res)
            if f: return f
            log(out, '  résultat sans fichier :', res)
        except Exception as e:
            log(out, '  échec', space, ':', repr(e)[:500])
    return None


def hunyuan(img, out):
    from gradio_client import Client, handle_file
    for space in ('tencent/Hunyuan3D-2.1', 'tencent/Hunyuan3D-2'):
        try:
            log(out, f'  Hunyuan3D : {space}')
            c = Client(space, hf_token=TOKEN, verbose=False)
            api = c.view_api(return_format='dict', print_info=False)
            names = list((api or {}).get('named_endpoints', {}).keys()); log(out, '  points d\'entrée :', names)
            ep = next((n for n in names if 'generation_all' in n), None) or next((n for n in names if 'gen' in n), None)
            if not ep: continue
            params = api['named_endpoints'][ep]['parameters']
            kw = {}
            for p in params:
                n = p.get('parameter_name') or p.get('label')
                if n in ('image', 'Image'): kw[n] = handle_file(img)
                elif 'rembg' in (n or ''): kw[n] = True
                elif 'texture' in (n or '').lower(): kw[n] = True
            log(out, '  appel', ep, list(kw))
            res = c.predict(api_name=ep, **kw)
            f = first_file(res)
            if f: return f
            log(out, '  résultat sans fichier :', str(res)[:500])
        except Exception as e:
            log(out, '  échec', space, ':', repr(e)[:500])
    return None


def run(name):
    out = os.path.join(HERE, 'out', name); os.makedirs(out, exist_ok=True)
    dst = os.path.join(out, 'modele.glb')
    if os.path.exists(dst) and os.path.getsize(dst) > 10000:
        print(f'== {name} : déjà généré', flush=True); return True
    log(out, f'== {name}  ({time.strftime("%H:%M:%S")})')
    img = os.path.join(HERE, 'concepts', f'{name}.png')
    for fn in (trellis, hunyuan):
        for attempt in range(2):
            f = fn(img, out)
            if f:
                shutil.copy(f, dst); log(out, f'  ✅ {name} : {os.path.getsize(dst) // 1024} Ko'); return True
            time.sleep(30)                                   # quota de calcul : on laisse souffler avant de réessayer
    log(out, f'  ❌ {name} : aucune génération réussie')
    return False


if __name__ == '__main__':
    names = [n for n in sys.argv[1:] if n in NAMES] or NAMES
    ok = 0
    for n in names:
        try: ok += bool(run(n))
        except Exception: traceback.print_exc()
    print(f'Réussies : {ok} / {len(names)}', flush=True)
