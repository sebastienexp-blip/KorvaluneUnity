from world import materials as WM
RECIPES = {
    'W_Stone':   (lambda S: WM.stone_blocks(S), 2048, 512, dict(metallic=0.0, smoothness=1.0)),
    'W_Roof':    (lambda S: WM.slate_roof(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
    'W_Wood':    (lambda S: WM.bark(S, tone=(0.28, 0.18, 0.10)), 512, 256, dict(metallic=0.0, smoothness=1.0)),
    'W_Dark':    (lambda S: WM.bark(S, tone=(0.02, 0.02, 0.025)), 64, 64, dict(metallic=0.0, smoothness=1.0)),
    'W_Gold':    (lambda S: __import__('knight.materials', fromlist=['x']).brass(S), 256, 128, dict(metallic=1.0, smoothness=1.0)),
    'W_Bark':    (lambda S: WM.bark(S), 1024, 256, dict(metallic=0.0, smoothness=1.0)),
    'W_Leaves':  (lambda S: WM.leaves(S), 1024, 512, dict(metallic=0.0, smoothness=1.0, cutoff=0.5)),
    'W_Needles': (lambda S: WM.leaves(S, kind='needle', c1=(0.07, 0.17, 0.07), c2=(0.15, 0.27, 0.11)), 1024, 512, dict(metallic=0.0, smoothness=1.0, cutoff=0.5)),
    'W_Rock':    (lambda S: WM.rock(S), 1024, 512, dict(metallic=0.0, smoothness=1.0)),
}
