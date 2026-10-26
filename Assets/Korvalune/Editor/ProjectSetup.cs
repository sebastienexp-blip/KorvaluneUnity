using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Korvalune.EditorTools
{
    /// <summary>Réglages du projet : URP (actif sur tous les niveaux de qualité), espace linéaire, ombres douces, SSAO.</summary>
    public static class ProjectSetup
    {
        const string RenderDir = ModelPipeline.Root + "/Rendering";

        [MenuItem("Korvalune/Projet/Configurer URP")]
        public static UniversalRenderPipelineAsset EnsureUrp()
        {
            ModelPipeline.EnsureFolder(RenderDir);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var rendererPath = RenderDir + "/Korvalune_Renderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rd == null) { rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, rendererPath); }
            if (rd.postProcessData == null)
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AddSsao(rd);
            EditorUtility.SetDirty(rd);

            var pipePath = RenderDir + "/Korvalune_URP.asset";
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipePath);
            if (pipe == null) { pipe = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(pipe, pipePath); }
            pipe.supportsHDR = true;
            pipe.shadowDistance = 60f;
            pipe.shadowCascadeCount = 3;
            pipe.msaaSampleCount = 4;
            SetPrivate(pipe, "m_SoftShadowsSupported", true);
            SetPrivate(pipe, "m_MainLightShadowmapResolution", 2048);
            SetPrivate(pipe, "m_AdditionalLightsRenderingMode", 1);
            SetPrivate(pipe, "m_AdditionalLightsPerObjectLimit", 4);
            EditorUtility.SetDirty(pipe);

            EnsureMobile();
            GraphicsSettings.defaultRenderPipeline = pipe;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipe; }
            QualitySettings.SetQualityLevel(cur, false);
            AssetDatabase.SaveAssets();
            return pipe;
        }

        /// <summary>Pipeline « Mobile » pour les appareils Android modestes : pas de SSAO ni HDR ni MSAA, rendu à 80 %,
        /// une cascade d'ombres dures en 1024 sur 35 m. Choisi à l'exécution par GraphicsQuality.</summary>
        public static UniversalRenderPipelineAsset EnsureMobile()
        {
            var rendererPath = RenderDir + "/Korvalune_Renderer_Mobile.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rd == null) { rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, rendererPath); }
            if (rd.postProcessData == null)
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(rd);
            var path = RenderDir + "/Korvalune_URP_Mobile.asset";
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipe == null) { pipe = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(pipe, path); }
            pipe.supportsHDR = false; pipe.msaaSampleCount = 1; pipe.renderScale = 0.8f;
            pipe.shadowDistance = 35f; pipe.shadowCascadeCount = 1;
            SetPrivate(pipe, "m_SoftShadowsSupported", false);
            SetPrivate(pipe, "m_MainLightShadowmapResolution", 1024);
            SetPrivate(pipe, "m_AdditionalLightsRenderingMode", 0);
            EditorUtility.SetDirty(pipe);
            return pipe;
        }

        static void AddSsao(UniversalRendererData rd)
        {
            try
            {
                var t = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (t == null || rd.rendererFeatures.Exists(x => x != null && x.GetType() == t)) return;
                var f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
                f.name = "SSAO";
                AssetDatabase.AddObjectToAsset(f, rd);
                rd.rendererFeatures.Add(f);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId))
                {
                    var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (mapField != null && mapField.GetValue(rd) is System.Collections.Generic.List<long> map) map.Add(localId);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Korvalune] SSAO non ajouté : " + e.Message); }
        }

        static void SetPrivate(object o, string field, object value)
        {
            var f = o.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(o, value); else Debug.LogWarning("[Korvalune] Champ URP absent : " + field);
        }
    }
}
