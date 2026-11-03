using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Korvalune.Game
{
    /// <summary>Trois niveaux de qualité : Ultra (PC : ombres fines à 120 m, 4 cascades, distance d'affichage maximale),
    /// Haute (téléphones puissants) et Mobile (Android modestes). Choix automatique au lancement d'après l'appareil,
    /// modifiable en jeu ; règle pipeline URP, textures, LOD, terrain et post-traitement.</summary>
    public static class GraphicsQuality
    {
        public enum Tier { Mobile, Haute, Ultra }
        public static Tier Current { get; private set; } = Tier.Haute;
        const string Key = "korvalune.qualite";

        public static Tier Detect()
        {
            if (PlayerPrefs.HasKey(Key)) return (Tier)PlayerPrefs.GetInt(Key);
            if (!Application.isMobilePlatform) return Tier.Ultra;
            bool strong = SystemInfo.systemMemorySize >= 7000 && SystemInfo.graphicsMemorySize >= 3000 && SystemInfo.processorCount >= 8;
            return strong ? Tier.Haute : Tier.Mobile;
        }

        public static void Apply(GameConfig cfg, Tier t, bool remember = false)
        {
            Current = t;
            if (remember) PlayerPrefs.SetInt(Key, (int)t);
            bool low = t == Tier.Mobile, ultra = t == Tier.Ultra;
            var pipe = cfg ? (low ? cfg.mobilePipeline : ultra && cfg.ultraPipeline ? cfg.ultraPipeline : cfg.highPipeline) : null;
            if (pipe) QualitySettings.renderPipeline = pipe;
            QualitySettings.globalTextureMipmapLimit = low ? 1 : 0;           // textures à demi-résolution
            QualitySettings.lodBias = low ? 0.6f : ultra ? 2.5f : 1.5f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.skinWeights = low ? SkinWeights.TwoBones : SkinWeights.FourBones;
            QualitySettings.anisotropicFiltering = low ? AnisotropicFiltering.Disable : ultra ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            Application.targetFrameRate = low ? 30 : 60;
            var terrain = Terrain.activeTerrain;
            if (terrain)
            {
                terrain.heightmapPixelError = low ? 10 : ultra ? 2 : 4;
                terrain.basemapDistance = low ? 90 : ultra ? 400 : 220;
                terrain.treeDistance = low ? 220 : ultra ? 900 : 600;
                terrain.treeBillboardDistance = low ? 50 : ultra ? 260 : 140;
                terrain.treeMaximumFullLODCount = low ? 40 : ultra ? 500 : 200;
                terrain.detailObjectDistance = low ? 25 : ultra ? 110 : 60;
                if (ultra) terrain.detailObjectDensity = 1f;
                terrain.shadowCastingMode = low ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
            }
            RenderSettings.fogEndDistance = low ? 300 : ultra ? 700 : 520;
            // eau : profondeur et écume des berges (texture de profondeur de la caméra) en qualité haute seulement
            if (low) Shader.DisableKeyword("KORV_WATER_DEPTH"); else Shader.EnableKeyword("KORV_WATER_DEPTH");
            var cam = Camera.main;
            if (cam)
            {
                var cd = cam.GetComponent<UniversalAdditionalCameraData>(); if (cd) cd.requiresDepthOption = low ? CameraOverrideOption.UsePipelineSettings : CameraOverrideOption.On;
                cam.farClipPlane = low ? 360 : ultra ? 1300 : 900;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = low ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.renderShadows = true;
                }
            }
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) v.weight = low ? 0.6f : 1f;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.name == "Ambiance") { var em = ps.emission; em.rateOverTimeMultiplier = low ? 6 : 18; }
        }
    }
}
