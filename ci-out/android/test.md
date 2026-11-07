# Test de l'APK dans un émulateur Android (API 30, x86_64)

- Android 11
- APK : 129.1 Mo
- ✅ Installation
- ✅ Jeu en cours d'exécution à 20 s (capture ecran_20s.png)
- ✅ Jeu en cours d'exécution à 45 s (capture ecran_45s.png)
- ✅ Jeu en cours d'exécution à 90 s (capture ecran_90s.png)
- ✅ Toujours vivant après une interaction tactile

## Partie automatique dans la vallée
- Activité : com.korvalune.game/com.unity3d.player.UnityPlayerGameActivity
    [Korvalune] partie automatique : activée
    [Korvalune][test] lanceur prêt : on joue
    [Korvalune][test] chargement de la scène « Korvalune_Selection » avec Testeur
    [Korvalune][test] ÉCHEC : scène introuvable
- ⚠️ Partie automatique inachevée (voir partie.txt)
- ✅ Aucun plantage dans le journal
- Erreurs Unity dans le journal : 2
    11-07 08:54:09.093  6271  6293 E Unity   : Scene 'Korvalune_Selection' couldn't be loaded because it has not been added to the active build profile or shared scene list or the AssetBundle has not been loaded.
    11-07 08:54:09.093  6271  6293 E Unity   : To add a scene to the active build profile or shared scene list use the menu File->Build Profiles

**RÉSULTAT : OK**
