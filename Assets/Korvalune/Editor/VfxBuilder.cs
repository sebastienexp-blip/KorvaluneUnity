using System.IO;
using Korvalune.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Korvalune.EditorTools
{
    /// <summary>Bibliothèque d'effets des compétences : réglages d'import des textures Art/VFX (générées par ModelForge/vfx)
    /// et matériaux URP Particles/Unlit (additifs pour le feu, la foudre, les âmes ; alpha pour la fumée et les débris).</summary>
    public static class VfxBuilder
    {
        const string Dir = ModelPipeline.Root + "/Art/VFX";

        public static VfxLibrary Build()
        {
            var matDir = Dir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(Dir, "Materials");
            var path = Dir + "/VFX_Bibliotheque.asset";
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);
            if (!lib) { lib = ScriptableObject.CreateInstance<VfxLibrary>(); AssetDatabase.CreateAsset(lib, path); }
            lib.fire = Mat("Feu", true); lib.smoke = Mat("Fumee", false); lib.glow = Mat("Lueur", true); lib.spark = Mat("Etincelle", true);
            lib.lightning = Mat("Eclair", true, clamp: false); lib.ice = Mat("Glace", true); lib.runes = Mat("Runes", true); lib.shock = Mat("Onde", true);
            lib.slash = Mat("Entaille", true); lib.debris = Mat("Debris", false); lib.beam = Mat("Rayon", true, clamp: false); lib.soul = Mat("Ame", true);
            lib.glowAlpha = Mat("Lueur", false, "Lueur_Alpha");
            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets();
            return lib;
        }

        static Material Mat(string tex, bool additive, string name = null, bool clamp = true)
        {
            var tp = $"{Dir}/VFX_{tex}.png";
            var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
            if (ti != null)
            {
                bool dirty = !ti.alphaIsTransparency || ti.textureCompression != TextureImporterCompression.CompressedHQ;
                ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.sRGBTexture = true; ti.mipmapEnabled = true;
                ti.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.maxTextureSize = 1024;
                if (dirty) ti.SaveAndReimport();
            }
            else Debug.LogError($"[VFX] Texture absente : {tp}");
            var mp = $"{Dir}/Materials/VFX_{name ?? tex}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, mp); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
