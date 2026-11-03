"""Génère les modèles 3D des nouvelles créatures avec l'API Meshy (image → 3D texturé PBR), puis pose le squelette
des créatures bipèdes (API Rigging de Meshy, humanoïdes seulement). Les créatures non humanoïdes sont riggées ensuite
dans ModelForge (squelettes et animations du jeu).

  MESHY_API_KEY=... python3 Tools/Meshy/generate.py abbe wendigo ...

Chaque créature : concepts/<nom>.png → out/<nom>/ (modele.glb, modele.fbx, textures PBR, tache.json ; pour les
bipèdes : rig.fbx, rig.glb, marche.fbx, course.fbx). Une créature déjà générée (tache.json « SUCCEEDED ») n'est pas
relancée : on ne paie pas deux fois. Les identifiants de tâche sont gardés pour reprendre une génération en cours."""
import base64, json, os, sys, time
import urllib.request, urllib.error

API = 'https://api.meshy.ai/openapi/v1'
HERE = os.path.dirname(os.path.abspath(__file__))

CREATURES = {
    'abbe': dict(biped=True, height=2.1, poly=60000, prompt=(
        "Drowned undead abbot, tall emaciated, rotten waterlogged brown-green priest vestments in tatters, tall pointed mitre with tarnished bronze trim, "
        "skull face with glowing green eyes, beard of purple octopus tentacles with suckers, barnacle-crusted shoulders, left arm is a huge red-brown crab claw, "
        "driftwood crozier with hanging cage lantern glowing yellow-green, seaweed strands, dark fantasy, realistic PBR")),
    'wendigo': dict(biped=True, height=2.9, poly=60000, prompt=(
        "Frost wendigo, very tall gaunt humanoid beast, cracked white deer skull head with glowing pale blue eyes and huge branching frosted antlers with icicles, "
        "exposed ribcage with hanging icicles, ash-grey skin stretched over bones, tattered shaggy pale grey fur on shoulders arms and legs, long black claws, "
        "dark fantasy, realistic PBR")),
    'tisseuse': dict(biped=False, poly=60000, prompt=(
        "Mist weaver swamp horror, hunched skeletal torso on four long thin stilt legs, two long bony scythe forelimbs, eyeless skull face veiled by hanging green moss, "
        "rusty iron bird cages with glowing green will-o-wisps hanging from wooden poles on its back, rotten wood and bone, moss, dark fantasy, realistic PBR")),
    'salamandre': dict(biped=False, poly=60000, prompt=(
        "Huge low armored lava salamander, body covered in riveted hammered black obsidian armor plates like a forge, row of iron chimneys on its back, "
        "glowing orange molten lava cracks between plates and on the belly, wide flat head with glowing mouth, short thick clawed legs, thick tail, dark fantasy, realistic PBR")),
    'basilic': dict(biped=False, poly=60000, prompt=(
        "Scarlet basilisk, six-legged red sandstone lizard, petrified rocky red scales, large spiked fan crest behind the head, glowing amber eyes, "
        "long segmented whip tail curving up and ending in a spiked stone club, sharp claws, dark fantasy, realistic PBR")),
    'voyant': dict(biped=False, poly=50000, prompt=(
        "Void seer, floating abomination: one giant glowing violet eye in a dark fleshy socket, broken halo of black crystal shards around it, "
        "torn pale priestly cloth with a golden chain and cross hanging below, many long shadowy black tentacles trailing down with purple glow, no legs, dark fantasy, realistic PBR")),
}


def req(method, path, body=None):
    key = os.environ['MESHY_API_KEY']
    data = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(API + path, data=data, method=method, headers={'Authorization': f'Bearer {key}', 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(r, timeout=120) as f: return json.loads(f.read().decode())
    except urllib.error.HTTPError as e:
        print(f'  ❌ HTTP {e.code} {path} : {e.read().decode()[:600]}', flush=True); raise


def wait(kind, tid, label):
    t0 = time.time()
    while True:
        t = req('GET', f'/{kind}/{tid}')
        st = t.get('status'); print(f'  {label} : {st} {t.get("progress", 0)} %  ({time.time() - t0:.0f} s)', flush=True)
        if st in ('SUCCEEDED', 'FAILED', 'CANCELED'): return t
        time.sleep(15)


def fetch(url, path):
    if not url: return False
    for k in range(3):
        try:
            urllib.request.urlretrieve(url, path); print(f'    ↓ {os.path.basename(path)} ({os.path.getsize(path) // 1024} Ko)', flush=True); return True
        except Exception as e:
            print(f'    réessai {os.path.basename(path)} : {e}', flush=True); time.sleep(5)
    return False


def run(name):
    cfg = CREATURES[name]; out = os.path.join(HERE, 'out', name); os.makedirs(out, exist_ok=True)
    state_path = os.path.join(out, 'tache.json')
    state = json.load(open(state_path)) if os.path.exists(state_path) else {}
    print(f'== {name}', flush=True)
    # 1. image → 3D
    if state.get('model', {}).get('status') != 'SUCCEEDED':
        tid = state.get('model_id')
        if not tid:
            img = open(os.path.join(HERE, 'concepts', f'{name}.png'), 'rb').read()
            body = dict(image_url='data:image/png;base64,' + base64.b64encode(img).decode(), ai_model='latest', should_texture=True, enable_pbr=True,
                        texture_resolution='2k', should_remesh=True, topology='triangle', target_polycount=cfg['poly'], texture_prompt=cfg['prompt'][:800],
                        pose_mode='t-pose' if cfg['biped'] else '', image_enhancement=True, target_formats=['glb', 'fbx'])
            tid = req('POST', '/image-to-3d', body)['result']; state['model_id'] = tid
            json.dump(state, open(state_path, 'w'), indent=1)
        t = wait('image-to-3d', tid, 'modèle')
        state['model'] = {k: t.get(k) for k in ('status', 'task_error', 'consumed_credits', 'model_urls', 'texture_urls', 'thumbnail_url')}
        json.dump(state, open(state_path, 'w'), indent=1)
        if t['status'] != 'SUCCEEDED': print(f'  ❌ échec : {t.get("task_error")}', flush=True); return
        mu = t.get('model_urls') or {}
        fetch(mu.get('glb'), os.path.join(out, 'modele.glb')); fetch(mu.get('fbx'), os.path.join(out, 'modele.fbx'))
        fetch(t.get('thumbnail_url'), os.path.join(out, 'apercu.png'))
        for tex in (t.get('texture_urls') or [])[:1]:
            for k, v in tex.items(): fetch(v, os.path.join(out, f'tex_{k}.png'))
    # 2. squelette (bipèdes)
    if cfg['biped'] and state.get('rig', {}).get('status') != 'SUCCEEDED':
        rid = state.get('rig_id')
        if not rid:
            rid = req('POST', '/rigging', dict(input_task_id=state['model_id'], height_meters=cfg['height']))['result']; state['rig_id'] = rid
            json.dump(state, open(state_path, 'w'), indent=1)
        t = wait('rigging', rid, 'squelette')
        state['rig'] = {k: t.get(k) for k in ('status', 'task_error', 'consumed_credits', 'result')}
        json.dump(state, open(state_path, 'w'), indent=1)
        if t['status'] == 'SUCCEEDED':
            r = t.get('result') or {}
            fetch(r.get('rigged_character_fbx_url'), os.path.join(out, 'rig.fbx')); fetch(r.get('rigged_character_glb_url'), os.path.join(out, 'rig.glb'))
            ba = r.get('basic_animations') or {}
            fetch(ba.get('walking_fbx_url'), os.path.join(out, 'marche.fbx')); fetch(ba.get('running_fbx_url'), os.path.join(out, 'course.fbx'))
        else:
            print(f'  ❌ squelette : {t.get("task_error")}', flush=True)
    print(f'  ✅ {name} terminé', flush=True)


if __name__ == '__main__':
    names = [n for n in sys.argv[1:] if n in CREATURES] or list(CREATURES)
    for n in names:
        try: run(n)
        except Exception as e: print(f'  ❌ {n} : {e}', flush=True)
