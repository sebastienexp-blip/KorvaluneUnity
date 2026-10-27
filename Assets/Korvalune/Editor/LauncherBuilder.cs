using System.IO;
using System.Linq;
using Korvalune.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Écran de lancement (première scène du jeu) et illustrations tirées du jeu lui-même : fond du lanceur et icône
    /// de l'application, rendus depuis la vallée au crépuscule (château, ville, fleuve).</summary>
    public static class LauncherBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Lanceur.unity";
        public const string ArtDir = ModelPipeline.Root + "/Resources/UI";

        [MenuItem("Korvalune/Construire l'écran de lancement")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.gameObject.AddComponent<AudioListener>();
            new GameObject("Lanceur").AddComponent<Launcher>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Rend l'illustration du lanceur (1920×1080) et l'icône (512×512) depuis la vallée construite, au crépuscule.</summary>
        public static void RenderKeyArt(Camera cam)
        {
            Directory.CreateDirectory(ArtDir); Directory.CreateDirectory("ci-out");
            var sun = RenderSettings.sun; var oldRot = sun ? sun.transform.rotation : Quaternion.identity; var oldCol = sun ? sun.color : Color.white; var oldI = sun ? sun.intensity : 1;
            var oldAmb = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
            var oldPos = cam.transform.position; var oldR = cam.transform.rotation; var oldFov = cam.fieldOfView;
            try
            {
                // crépuscule : soleil rasant orangé, ciel plus froid, fenêtres et lanternes qui ressortent
                if (sun) { sun.transform.rotation = Quaternion.Euler(9, -25, 0); sun.color = new Color(1f, 0.62f, 0.38f); sun.intensity = 1.25f; }
                RenderSettings.ambientSkyColor = new Color(0.36f, 0.38f, 0.55f); RenderSettings.ambientEquatorColor = new Color(0.40f, 0.30f, 0.30f); RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.10f);
                var town = WorldBuilder.Ground(TownBuilder.Plaza);
                cam.fieldOfView = 38;
                cam.transform.position = WorldBuilder.Ground(new Vector2(-70, -150)) + Vector3.up * 34; cam.transform.LookAt(new Vector3(6, 16, -18));
                Save(cam, 1920, 1080, ArtDir + "/Lanceur_Fond.png");
                cam.fieldOfView = 30;
                cam.transform.position = WorldBuilder.Ground(new Vector2(-10, -78)) + Vector3.up * 6; cam.transform.LookAt(new Vector3(0, 22, 0));
                var icon = Grab(cam, 512, 512);
                Frame(icon);
                File.WriteAllBytes(ArtDir + "/Icone.png", icon.EncodeToPNG()); File.WriteAllBytes("ci-out/Icone.png", icon.EncodeToPNG()); Object.DestroyImmediate(icon);
                File.Copy(ArtDir + "/Lanceur_Fond.png", "ci-out/Lanceur_Fond.png", true);
                AssetDatabase.ImportAsset(ArtDir + "/Lanceur_Fond.png"); AssetDatabase.ImportAsset(ArtDir + "/Icone.png");
                foreach (var p in new[] { ArtDir + "/Lanceur_Fond.png", ArtDir + "/Icone.png" })
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(p); if (!ti) continue;
                    ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = false; ti.maxTextureSize = 2048; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport();
                }
            }
            finally
            {
                if (sun) { sun.transform.rotation = oldRot; sun.color = oldCol; sun.intensity = oldI; }
                (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor) = oldAmb;
                cam.transform.position = oldPos; cam.transform.rotation = oldR; cam.fieldOfView = oldFov;
            }
        }

        static Texture2D Grab(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            return tex;
        }

        static void Save(Camera cam, int w, int h, string path)
        {
            var t = Grab(cam, w, h);
            // léger vignettage et teinte chaude (illustration)
            var px = t.GetPixels();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = (x / (float)w - 0.5f) * 1.6f, v = (y / (float)h - 0.5f) * 1.6f; float vig = Mathf.Clamp01(1.05f - 0.55f * (u * u + v * v));
                var c = px[y * w + x]; c = new Color(c.r * vig * 1.03f, c.g * vig, c.b * vig * 0.95f, 1); px[y * w + x] = c;
            }
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        }

        /// <summary>Icône : carré arrondi, cadre doré biseauté, vignette sombre.</summary>
        static void Frame(Texture2D t)
        {
            int S = t.width; var px = t.GetPixels(); float R = S * 0.19f, B = S * 0.045f;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - S / 2f + 0.5f) - (S / 2f - R)), dy = Mathf.Max(0, Mathf.Abs(y - S / 2f + 0.5f) - (S / 2f - R));
                float d = R - Mathf.Sqrt(dx * dx + dy * dy);            // distance au bord (positif à l'intérieur)
                var c = px[y * S + x];
                float u = (x / (float)S - 0.5f) * 2, v = (y / (float)S - 0.5f) * 2; float vig = Mathf.Clamp01(1.1f - 0.45f * (u * u + v * v)); c *= vig;
                if (d < B) { float k = d / B; var gold = Color.Lerp(new Color(0.45f, 0.30f, 0.10f), new Color(1f, 0.85f, 0.45f), Mathf.Sin(k * Mathf.PI)); c = Color.Lerp(gold, c, k > 0.85f ? (k - 0.85f) / 0.15f : 0); }
                c.a = Mathf.Clamp01(d + 0.5f);
                px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply();
        }
    }
}
