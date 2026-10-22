# Corvalune (Unity)

Action-RPG 3D dark fantasy sous **Unity 6000.0.32f1**, rendu **URP** (Universal Render Pipeline), cibles PC et Android.

## Organisation

| Dossier | Contenu |
|---|---|
| `Assets/Korvalune/Art/` | modèles **FBX**, textures PBR séparées, manifestes `*.forge.json` (réglages d'intégration) |
| `Assets/Korvalune/Editor/` | intégration automatique des modèles (import Humanoid, matériaux URP Lit, contrôleur d'animation, prefab), vérifications CI |
| `Assets/Korvalune/Scripts/Runtime/` | code du jeu (`CharacterRig` : points d'attache et pilotage de l'Animator) |
| `Tools/ModelForge/` | **sources modifiables des modèles** : scripts Python qui construisent géométrie, squelette, skinning, textures et animations, puis exportent le FBX |
| `.github/workflows/unity-ci.yml` | Unity dans le cloud : import, intégration, vérification, renvoi des fichiers générés et du rapport `ci-report.md` |

## Chaîne de production d'un modèle

1. `python3 Tools/ModelForge/build_knight.py` (Python 3 + numpy, scipy, Pillow) écrit `Knight.fbx`, ses textures et `Knight.forge.json`.
2. `python3 Tools/ModelForge/forge/fbxcheck.py <fichier.fbx>` relit le FBX de façon indépendante (polygones, poids, animations).
3. Un `git push` lance le workflow Unity : le FBX est importé par Unity, configuré (Humanoid, clips, boucles), ses matériaux URP sont créés, le prefab est assemblé (épée et écu sur leurs points d'attache, collisions), puis chaque point est vérifié dans `ci-report.md`.

Le FBX est écrit en ASCII FBX 7.4 (unités : centimètres, Y vers le haut, regard +Z) ; Unity le ramène en mètres.
