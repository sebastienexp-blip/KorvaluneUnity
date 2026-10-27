# Rapport absent : Unity s'est arrêté avant la fin

```
```

## Erreurs de compilation (Library/Bee, Logs)
```
```

## Dernières étapes (ci-out/progress.log)
```
01:50:49 - ✅ **Clip Sk_BackFlip** 1.00 s, boucle=False, humanoïde=True, déplacement max 0.584 m, événements 0
01:50:49 - ✅ **Clip Sk_Throw** 0.80 s, boucle=False, humanoïde=True, déplacement max 0.932 m, événements 0
01:50:49 - ✅ **Clip Sk_Trap** 0.93 s, boucle=False, humanoïde=True, déplacement max 1.040 m, événements 0
01:50:49 - ✅ **Clip Sk_Kick** 0.73 s, boucle=False, humanoïde=True, déplacement max 1.393 m, événements 0
01:50:49 - ✅ **Clip Sk_Sweep** 0.90 s, boucle=False, humanoïde=True, déplacement max 0.712 m, événements 0
01:50:49 - ✅ **Clip Sk_Whistle** 1.10 s, boucle=False, humanoïde=True, déplacement max 0.615 m, événements 0
01:50:49 - ✅ **Clip Sk_Dash** 0.67 s, boucle=False, humanoïde=True, déplacement max 0.626 m, événements 0
01:50:49 validation des pièces
01:50:49 
## Pièces du créateur de personnage (140 modèles : cheveux, sourcils, cils)
01:51:14 - ✅ **Pièces importées (modèle + prefab + peau)** 140/140, jusqu'à 158 blendshapes
01:51:14 BuildShowcase
01:51:28 - ✅ **Scène vitrine** Assets/Korvalune/Scenes/Showcase.unity
01:51:28 Captures
01:51:31 Error: BlitFinalToBackBuffer: Attachment 0 was created with 4 samples but 2 samples were requested.
01:51:31 Error: EndRenderPass: Not inside a Renderpass
01:51:31 captures AshCrossbow : 1 (0.0 min)
01:51:35 - ❌ **Exécution CI** UnityEngine.MissingReferenceException: The object of type 'UnityEngine.Texture2D' has been destroyed but you are still trying to access it.
Your script should either check if it is null or you should not destroy the object.
  at UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) [0x00064] in /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/UnityEngineObject.bindings.cs:818 
  at UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) [0x00010] in /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/BindingsHelpers.cs:61 
  at UnityEngine.Texture2D.get_isReadable () [0x00006] in <32a1eb638d034453a949378d062eb6d8>:0 
  at UnityEngine.Texture2D.ReadPixels (UnityEngine.Rect source, System.Int32 destX, System.Int32 destY, System.Boolean recalculateMipMaps) [0x00001] in /home/bokken/build/output/unity/unity/Runtime/Export/Graphics/Texture.cs:1115 
  at UnityEngine.Texture2D.ReadPixels (UnityEngine.Rect source, System.Int32 destX, System.Int32 destY) [0x00001] in /home/bokken/build/output/unity/unity/Runtime/Export/Graphics/Texture.cs:1119 
  at Korvalune.EditorTools.CI.Captures (System.Collections.Generic.List`1[T] entries) [0x002ec] in /github/workspace/Assets/Korvalune/Editor/CI.cs:401 
  at Korvalune.EditorTools.CI.Run () [0x00357] in /github/workspace/Assets/Korvalune/Editor/CI.cs:71 
01:51:35 
## Journal Unity (erreurs et avertissements)
01:51:35 - ❌ Error: BlitFinalToBackBuffer: Attachment 0 was created with 4 samples but 2 samples were requested.
01:51:35 - ❌ Error: EndRenderPass: Not inside a Renderpass
01:51:35 
**RÉSULTAT : 1 vérification(s) en échec.**
01:52:26 # Rapport d'intégration Unity — 2026-10-27 01:52 UTC
01:52:26 - Unity 6000.0.32f1, plateforme active StandaloneLinux64, rendu OpenGLCore
01:52:28 - ✅ **Render Pipeline** URP actif (Korvalune_URP)
01:52:28 - 187 modèle(s) ModelForge trouvé(s)
01:52:28 == éditeur 3 : scènes
01:52:33 - 0 modèle(s) intégré(s), 187 déjà à jour (inchangés)
01:52:33 traitement terminé — mémoire 131 Mo
01:52:33 CharacterCreatorCheck
```
