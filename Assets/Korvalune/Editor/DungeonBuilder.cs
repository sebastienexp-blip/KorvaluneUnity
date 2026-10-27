using System.Collections.Generic;
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
    /// <summary>La Crypte de Korvalune, premier donjon (sous le vieux cimetière de la vallée) : salles et couloirs posés sur une grille
    /// de 4 m avec le kit ModelForge (dalles, murs, arches, piliers, sarcophages, braseros, torches, ossements), éclairage de
    /// flammes vacillantes, squelettes, chevalier maudit et démon gardien, quête « Purger la crypte », portail de sortie.</summary>
    public static class DungeonBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Crypte.unity";
        const string Kit = ModelPipeline.Root + "/Resources/Prefabs/World/Crypte";
        const float T = 4f;

        // # mur plein · sol : . libre, P pilier, C sarcophage, B brasero, o ossements, E entrée (escalier), X centre de la salle du gardien
        static readonly string[] Map =
        {
            "#############",
            "#.C...#..o..#",
            "#.....#.....#",
            "#.P.P.....X.#",
            "#.....#.....#",
            "#.C...#..o..#",
            "###.#########",
            "#.........###",
            "#.P..B..P.###",
            "#o.......E###",
            "#############",
        };

        static Vector3 Cell(int col, int row) => new Vector3((col - 6) * T, 0, (5 - row) * T);
        static bool Solid(int c, int r) => r < 0 || r >= Map.Length || c < 0 || c >= Map[r].Length || Map[r][c] == '#';

        static GameObject Put(string name, Vector3 pos, float yaw, Transform parent)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/{name}.prefab");
            if (!p) { Debug.LogWarning("[Crypte] pièce absente : " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.isStatic = true;
            return go;
        }

        static Light Fire(Vector3 pos, float intensity, float range, Transform parent)
        {
            var l = new GameObject("Flamme").AddComponent<Light>(); l.transform.SetParent(parent); l.transform.position = pos;
            l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.3f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<Flicker>();
            // flammes : particules additives (matériau d'effets de la configuration)
            var ps = l.gameObject.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 0.6f; m.startSpeed = 0.6f; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); m.maxParticles = 60;
            m.startColor = new Color(1f, 0.55f, 0.18f, 0.9f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.15f;
            var em = ps.emission; em.rateOverTime = 40; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var cfg = WorldBuilder.Config(); var mat = cfg.vfx && cfg.vfx.fire ? cfg.vfx.fire : cfg.fxAdditive;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return l;
        }

        [MenuItem("Korvalune/Construire la Crypte")]
        public static void BuildMenu() { Build(); }

        public static GameBootstrap Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Crypte").transform;
            var rng = new System.Random(5);
            Vector3 entrance = Vector3.zero, boss = Vector3.zero;
            var halls = new List<Vector3>();

            for (int r = 0; r < Map.Length; r++)
                for (int c = 0; c < Map[r].Length; c++)
                {
                    char ch = Map[r][c]; if (ch == '#') continue;
                    var p = Cell(c, r);
                    Put("D_Sol" + rng.Next(3), p, 90 * rng.Next(4), env);
                    // murs sur les bords qui touchent la roche
                    var sides = new (int dc, int dr, Vector3 off, float yaw)[] { (0, -1, new Vector3(0, 0, T / 2), 0), (0, 1, new Vector3(0, 0, -T / 2), 180), (1, 0, new Vector3(T / 2, 0, 0), 90), (-1, 0, new Vector3(-T / 2, 0, 0), 270) };
                    foreach (var (dc, dr, off, yaw) in sides)
                    {
                        if (!Solid(c + dc, r + dr)) continue;
                        Put("D_Mur" + rng.Next(3), p + off, yaw, env);
                        // torche murale une fois sur trois, tournée vers la salle
                        if (rng.Next(3) == 0)
                        {
                            var inward = -off.normalized;
                            var t = Put("D_Torche", p + off + inward * 0.42f, yaw + 180, env);
                            if (t) Fire(p + off + inward * 0.7f + Vector3.up * 2.1f, 2.2f, 7f, env);
                        }
                    }
                    switch (ch)
                    {
                        case 'P': Put("D_Pilier", p, 0, env); break;
                        case 'C': Put("D_Sarcophage0", p, 90, env); break;
                        case 'B': Put("D_Brasero", p, 0, env); Fire(p + Vector3.up * 1.3f, 3.5f, 11f, env); break;
                        case 'o': Put("D_Os" + rng.Next(2), p, rng.Next(360), env); break;
                        case 'E': entrance = p; Put("D_Escalier", p + new Vector3(0, 0, 0), 180, env); break;
                        case 'X': boss = p; break;
                    }
                    if (ch == '.' && rng.Next(9) == 0) Put("D_Os" + rng.Next(2), p + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f), rng.Next(360), env);
                    halls.Add(p);
                }
            // braseros de la salle du gardien
            foreach (var d in new[] { new Vector3(-6, 0, -6), new Vector3(6, 0, -6), new Vector3(-6, 0, 6), new Vector3(6, 0, 6) })
            { Put("D_Brasero", boss + d, 0, env); Fire(boss + d + Vector3.up * 1.3f, 3.2f, 10f, env); }

            // ---- lumière d'ensemble : sombre mais lisible (on doit voir les ennemis)
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.28f, 0.34f); RenderSettings.ambientEquatorColor = new Color(0.22f, 0.19f, 0.18f); RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.07f);
            var moon = new GameObject("Lumière froide").AddComponent<Light>(); moon.type = LightType.Directional; moon.intensity = 0.55f; moon.color = new Color(0.62f, 0.68f, 0.9f);
            moon.transform.rotation = Quaternion.Euler(62, -30, 0); moon.shadows = LightShadows.Soft;

            // ---- caméra et jeu
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.015f, 0.02f); cam.farClipPlane = 300; cam.nearClipPlane = 0.3f;
            cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            WorldBuilder.PostProcess("Crypte", 0.25f);
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = WorldBuilder.Config();
            game.zoneName = "Crypte de Korvalune"; game.questTitle = "Purger la crypte";
            game.playerSpawn = entrance + new Vector3(-T, 0.05f, 0);
            game.castleGate = entrance;
            game.questSteps.Add(new Quest.Objective { text = "Détruire les morts-vivants de la crypte", enemyId = "squelette", count = 8 });
            game.questSteps.Add(new Quest.Objective { text = "Abattre le chevalier maudit", enemyId = "chevalier", count = 1 });
            game.questSteps.Add(new Quest.Objective { text = "Vaincre le gardien de la crypte", enemyId = "demon", count = 1 });
            game.questSteps.Add(new Quest.Objective { text = "Remonter au vieux cimetière", place = entrance, radius = 3f });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = Cell(4, 8), count = 4, radius = 5 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = Cell(3, 3), count = 5, radius = 6 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "chevalier", center = Cell(3, 1), count = 1, radius = 1 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "araignee", center = Cell(8, 5), count = 2, radius = 3 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "demon", center = boss, count = 1, radius = 1 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = boss, count = 2, radius = 5 });
            // portail de sortie (au pied de l'escalier) vers le cimetière de la vallée
            var exit = new GameObject("Sortie vers le cimetière").AddComponent<Portal>();
            exit.transform.position = entrance + new Vector3(0, 0.05f, -1.2f);
            exit.targetScene = "Korvalune_Vallee"; exit.arrival = WorldBuilder.CryptEntrance + new Vector3(0, 0, 3f); exit.label = "Remonter"; exit.displayName = "Remonter au cimetière";
            cam.transform.position = game.playerSpawn + new Vector3(0, 14, -9); cam.transform.LookAt(game.playerSpawn);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            return game;
        }

        /// <summary>Captures de contrôle : vue de jeu à l'entrée, salle aux piliers, salle du gardien.</summary>
        public static void Captures(GameBootstrap game, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("ci-out");
            try
            {
                game.Begin();
                var cam = game.Cam;
                foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) CharacterRig.EditorIdlePose(h.gameObject);
                void Shot(string name)
                {
                    game.Hud.Refresh(); Canvas.ForceUpdateCanvases();
                    var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    File.WriteAllBytes($"ci-out/Crypte_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                    CI.Step("crypte : capture " + name);
                }
                cam.GetComponent<CameraRig>().Snap(); Shot("1_entree");
                var p = game.Player.transform; var cc = p.GetComponent<CharacterController>();
                cc.enabled = false; p.position = Cell(3, 3) + Vector3.up * 0.05f; cc.enabled = true;
                cam.GetComponent<CameraRig>().Snap(); Shot("2_salle_aux_piliers");
                cc.enabled = false; p.position = Cell(8, 3) + Vector3.up * 0.05f; cc.enabled = true;
                cam.GetComponent<CameraRig>().Snap(); Shot("3_salle_du_gardien");
                check("Captures de la crypte", true, "ci-out/Crypte_*.png");
            }
            catch (System.Exception ex) { check("Captures de la crypte", false, ex.Message); }
        }
    }
}
