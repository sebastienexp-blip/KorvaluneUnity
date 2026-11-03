using UnityEditor;

namespace Korvalune.EditorTools
{
    /// <summary>Tailles des textures sur téléphone (Android) selon leur rôle : couleur en 1024, relief en 512, occlusion et
    /// métal/brillance en 256 (peu de détail visible à l'écran). Divise environ par deux le poids de l'APK et donc des mises
    /// à jour, sans différence notable en jeu. Les textures d'interface et d'effets gardent leurs propres réglages.</summary>
    public class MobileTextures : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/Korvalune/Art/") || p.Contains("/VFX/")) return;
            var ti = (TextureImporter)assetImporter;
            // textures multicouches : relief de détail en carte normale, masques du terrain en linéaire
            if (p.EndsWith("Detail_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (p.EndsWith("_Mask.png")) ti.sRGBTexture = false;
            if (p.Contains("/Detail/")) { ti.wrapMode = UnityEngine.TextureWrapMode.Repeat; ti.mipmapEnabled = true; }
            int max; TextureImporterFormat fmt;
            if (p.EndsWith("_Normal.png")) { max = 512; fmt = TextureImporterFormat.ASTC_6x6; }
            else if (p.EndsWith("_Occlusion.png") || p.EndsWith("_MetallicSmoothness.png")) { max = 256; fmt = TextureImporterFormat.ASTC_8x8; }
            else if (p.EndsWith("_Emission.png")) { max = 512; fmt = TextureImporterFormat.ASTC_6x6; }
            else if (p.Contains("/Catalog/") && !p.Contains("/Skins/")) { max = 512; fmt = TextureImporterFormat.ASTC_6x6; }   // coiffures, yeux, sourcils : petits à l'écran
            else { max = 1024; fmt = TextureImporterFormat.ASTC_6x6; }
            var a = ti.GetPlatformTextureSettings("Android");
            if (a.overridden && a.maxTextureSize == max && a.format == fmt) return;
            a.overridden = true; a.maxTextureSize = max; a.format = fmt; a.compressionQuality = 50;
            ti.SetPlatformTextureSettings(a);
        }
    }
}
