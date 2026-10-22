#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Menu « Korvalune » : configure le projet (URP, matériaux, scène, réglages Android) et compile l'APK.
    /// Dans le cloud (GitHub Actions) : -executeMethod Korvalune.EditorTools.KorvaluneSetup.CIBuild
    /// </summary>
    public static class KorvaluneSetup
    {
        /// <summary>Test de compilation seul : si les scripts compilent, Unity arrive ici.</summary>
        public static void CompileCheck()
        {
            Debug.Log("CompileCheck OK");
            EditorApplication.Exit(0);
        }

        const string Root = "Assets/Korvalune";
        const string ScenePath = Root + "/Scenes/Korvalune.unity";
        const string Mats = Root + "/Resources/Materials";
        const string RenderDir = Root + "/Rendering";

        [MenuItem("Korvalune/1. Configurer le projet et créer la scène", priority = 1)]
        public static void Setup()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Mats);
            Directory.CreateDirectory(RenderDir);
            AssetDatabase.Refresh();

            // ---- espace colorimétrique linéaire (rendu physique) ----
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            var ssao = SetupUrp();
            MakeMaterials();

            // ---- scène : un seul objet, le jeu se construit au lancement ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Boot");
            var game = go.AddComponent<Boot>();
            game.ssao = ssao;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // ---- réglages joueur / Android ----
            PlayerSettings.companyName = "Korvalune";
            PlayerSettings.productName = "Korvalune";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.korvalune.unity");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Disabled);

            AssetDatabase.SaveAssets();
            Debug.Log("[Korvalune] Projet configuré.");
        }

        static ScriptableRendererFeature SetupUrp()
        {
            var rendererPath = RenderDir + "/Korvalune_Renderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rendererPath);
            }
            if (rd.postProcessData == null)
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(rd);

            var pipePath = RenderDir + "/Korvalune_URP.asset";
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipePath);
            if (pipe == null)
            {
                pipe = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(pipe, pipePath);
            }
            pipe.supportsHDR = true;
            pipe.shadowDistance = 80f;
            pipe.shadowCascadeCount = 3;
            pipe.msaaSampleCount = 1;
            SetPrivate(pipe, "m_SoftShadowsSupported", true);
            SetPrivate(pipe, "m_MainLightShadowmapResolution", 2048);
            SetPrivate(pipe, "m_AdditionalLightsRenderingMode", 1);        // par pixel
            SetPrivate(pipe, "m_AdditionalLightsPerObjectLimit", 4);
            SetPrivate(pipe, "m_ShadowDepthBias", 1.0f);
            SetPrivate(pipe, "m_ShadowNormalBias", 1.0f);
            EditorUtility.SetDirty(pipe);

            // Occlusion ambiante (SSAO), activée seulement en qualité HAUTE
            ScriptableRendererFeature ssao = null;
            try
            {
                var t = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (t != null)
                {
                    ssao = rd.rendererFeatures.Find(x => x != null && x.GetType() == t);
                    if (ssao == null)
                    {
                        ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
                        ssao.name = "SSAO";
                        AssetDatabase.AddObjectToAsset(ssao, rd);
                        rd.rendererFeatures.Add(ssao);
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string g, out long localId))
                        {
                            var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (mapField != null && mapField.GetValue(rd) is System.Collections.Generic.List<long> map) map.Add(localId);
                        }
                        EditorUtility.SetDirty(rd);
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Korvalune] SSAO non ajouté : " + e.Message); ssao = null; }

            bool changed = GraphicsSettings.defaultRenderPipeline != pipe;
            GraphicsSettings.defaultRenderPipeline = pipe;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipe;
            }
            AssetDatabase.SaveAssets();

            return ssao;
        }

        static void AlwaysInclude(params string[] names)
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning("[Korvalune] Shader introuvable : " + n); continue; }
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (has) continue;
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MakeMaterials()
        {
            Shader terrain = null;
            Shader water = null;
            var sky = Shader.Find("Skybox/Procedural");
            var particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (terrain != null) Save("Terrain", new Material(terrain));
            if (water != null) Save("Water", new Material(water));
            if (sky != null) Save("Sky", new Material(sky));
            if (particle != null)
            {
                Save("ParticleAdd", Particle(particle, true));
                Save("ParticleAlpha", Particle(particle, false));
            }
            // matériaux « témoins » : gardent les variantes de shaders utilisées par le jeu dans l'APK
            if (lit != null)
            {
                Save("Lit_Plain", new Material(lit));
                // matériaux des modèles 3D (glTF) : normal + émission, avec variantes opaque / découpé / transparent
                var full = new Material(lit);
                full.EnableKeyword("_NORMALMAP"); full.EnableKeyword("_EMISSION");
                Save("Lit_Full", full);
                var mask = new Material(full);
                mask.SetFloat("_AlphaClip", 1f); mask.EnableKeyword("_ALPHATEST_ON"); mask.renderQueue = 2450;
                Save("Lit_Mask", mask);
                var blend = new Material(full);
                blend.SetFloat("_Surface", 1f); blend.SetFloat("_Blend", 0f);
                blend.SetFloat("_SrcBlend", 5f); blend.SetFloat("_DstBlend", 10f); blend.SetFloat("_ZWrite", 0f);
                blend.SetOverrideTag("RenderType", "Transparent");
                blend.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); blend.renderQueue = 3000;
                Save("Lit_Blend", blend);
            }
        }

        static Material Particle(Shader s, bool additive)
        {
            var m = new Material(s);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        static void Save(string name, Material m)
        {
            string p = Mats + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(p) != null) AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
        }

        static void SetPrivate(object o, string field, object value)
        {
            var f = o.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return;
            try { f.SetValue(o, f.FieldType.IsEnum ? System.Enum.ToObject(f.FieldType, value) : value); }
            catch (System.Exception e) { Debug.LogWarning("[Korvalune] " + field + " : " + e.Message); }
        }

        public static void CIBuild()
        {
            Setup();
            string outPath = "build/Android/Korvalune.apk";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-customBuildPath") outPath = args[i + 1];
            if (!outPath.EndsWith(".apk")) outPath = Path.Combine(outPath, "Korvalune.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("[Korvalune] Build " + (ok ? "réussi : " + outPath : "échoué : " + report.summary.result));
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Korvalune/2. Compiler l'APK Android", priority = 2)]
        public static void BuildApk()
        {
            if (!File.Exists(ScenePath)) Setup();
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Korvalune.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            EditorUtility.DisplayDialog("Korvalune", report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded
                ? "APK créé : Builds/Korvalune.apk" : "Échec du build : " + report.summary.result, "OK");
        }
    }
}
#endif
