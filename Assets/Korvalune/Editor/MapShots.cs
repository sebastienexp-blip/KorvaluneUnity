using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Korvalune.EditorTools
{
    /// <summary>Photographie de dessus de toute une zone extérieure, enregistrée dans Resources/UI/Cartes/&lt;scène&gt;.png : la
    /// grande carte du jeu peut ainsi montrer les autres régions (la Cité de l'Éclipse depuis la vallée, et inversement).</summary>
    public static class MapShots
    {
        public static void Save(string scene)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var t = Terrain.activeTerrain; if (!t) return;
            var min = t.transform.position; var size = t.terrainData.size; float side = Mathf.Max(size.x, size.z);
            const int S = 1024;
            var rt = new RenderTexture(S, S, 24);
            var go = new GameObject("Photographe de carte"); var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = side / 2; cam.aspect = 1; cam.nearClipPlane = 1; cam.farClipPlane = 900;
            cam.transform.position = new Vector3(min.x + side / 2, min.y + 500, min.z + side / 2); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.1f, 0.12f); cam.cullingMask = ~(1 << 5);
            float tree = t.treeDistance, bill = t.treeBillboardDistance; t.treeDistance = 2000; t.treeBillboardDistance = 2000;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            t.treeDistance = tree; t.treeBillboardDistance = bill;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(S, S, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, S, S), 0, 0); tex.Apply();
            RenderTexture.active = prev; Object.DestroyImmediate(go); rt.Release();
            var dir = ModelPipeline.Root + "/Resources/UI/Cartes"; ModelPipeline.EnsureFolder(dir);
            var path = dir + "/" + scene + ".png"; File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti) { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
            // emprise de la photo (pour placer les noms de lieux) : x, z min et côté
            File.WriteAllText(dir + "/" + scene + "_zone.txt", $"{min.x:R};{min.z:R};{side:R}");
            AssetDatabase.ImportAsset(dir + "/" + scene + "_zone.txt");
        }
    }
}
