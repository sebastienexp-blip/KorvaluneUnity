using UnityEditor;

namespace Korvalune.EditorTools
{
    /// <summary>Les icônes d'objets (Resources/Icons) sont importées en sprites d'interface, sans mipmaps, avec transparence.</summary>
    public class IconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Icons/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
