using System.IO;
using System.Linq;
using Korvalune.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Korvalune.EditorTools
{
    /// <summary>Scène des brèches : vide à l'enregistrement (caméra, jeu, générateur RiftLevel) ; le niveau est construit au
    /// chargement, différent à chaque entrée. Les captures de contrôle génèrent chacun des cinq décors.</summary>
    public static class BrecheBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Breche.unity";

        [MenuItem("Korvalune/Construire la scène des brèches")]
        public static void BuildMenu() { Build(); }

        public static GameBootstrap Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.5f, 0.6f); RenderSettings.ambientEquatorColor = new Color(0.38f, 0.36f, 0.4f); RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.16f);
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.02f, 0.05f); cam.farClipPlane = 300; cam.nearClipPlane = 0.3f;
            cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            WorldBuilder.PostProcess("Breche", 0.7f);
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = WorldBuilder.Config();
            game.zoneName = "Brèche"; game.questTitle = "Brèche";
            game.gameObject.AddComponent<RiftLevel>();
            // retour de secours vers la vallée (la brèche ouvre son propre portail à la victoire)
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            return game;
        }

        /// <summary>Génère chacun des décors et photographie le départ et le repaire du gardien.</summary>
        public static void Captures(GameBootstrap game, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("ci-out");
            var cfg = game.config;
            if (cfg) { FX.additive = cfg.fxAdditive; FX.alpha = cfg.fxAlpha; if (cfg.litMat) LootDrop.litBase = cfg.litMat; }
            var lvl = game.GetComponent<RiftLevel>();
            var cam = Camera.main;
            string[] names = { "crypte_engloutie", "fournaise", "necropole_astrale", "bois_petrifie", "glacier" };
            for (int t = 0; t < names.Length; t++)
            {
                try
                {
                    lvl.Generate(1000 + t * 17, t);
                    int pieces = lvl.GetComponentsInChildren<Renderer>().Length;
                    void Shot(string tag, Vector3 at)
                    {
                        cam.transform.position = at + new Vector3(0, 17, -12); cam.transform.LookAt(at + Vector3.up);
                        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                        RenderTexture.active = null; cam.targetTexture = null;
                        File.WriteAllBytes($"ci-out/Breche_{t + 1}_{names[t]}_{tag}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                    }
                    Shot("depart", game.playerSpawn);
                    var boss = game.camps.Count > 0 ? game.camps[game.camps.Count - 1].center : game.playerSpawn;
                    Shot("gardien", boss);
                    cam.transform.position = (game.playerSpawn + boss) / 2 + new Vector3(0, 95, -50); cam.transform.LookAt((game.playerSpawn + boss) / 2);
                    {
                        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                        RenderTexture.active = null; cam.targetTexture = null;
                        File.WriteAllBytes($"ci-out/Breche_{t + 1}_{names[t]}_plan.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                    }
                    check($"Brèche : {RiftLevel.ThemeName}", pieces > 80 && game.camps.Count >= 6, $"{pieces} éléments de décor, {game.camps.Count} groupes d'ennemis");
                    CI.Step("brèche : " + names[t]);
                }
                catch (System.Exception ex) { check($"Brèche : {names[t]}", false, ex.Message); }
            }
        }
    }
}
