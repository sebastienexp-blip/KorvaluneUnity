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
    /// <summary>
    /// Les Terriers de l'Astral : donjon-parcours naturel (grottes sinueuses, goulets, cavernes, terriers secondaires à
    /// trésor) généré par ModelForge (world/caves.py) et assemblé ici : tronçons de 48 m, lumières des champignons et des
    /// cristaux, torches des camps, araignées, loups et squelettes le long du parcours, gardienne au fond, coffres,
    /// quête, portail de retour. L'entrée se trouve dans la vallée, au pied des collines de la Forêt de l'Astral.
    /// </summary>
    public static class CaveBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Terriers.unity";
        const string Kit = ModelPipeline.Root + "/Resources/Prefabs/World/Terriers";
        const string Layout = ModelPipeline.Root + "/Art/World/Terriers/layout.json";
        /// <summary>Entrée des terriers dans la vallée (bouche rocheuse au pied des collines du nord-est).</summary>
        public static Vector2 ValleyMouth => new Vector2(62, 96);

        [System.Serializable] public class V { public float x, y, z; public Vector3 P => new Vector3(-x, y, z); }   // FBX (repère direct) → Unity : X inversé à l'import des tronçons, donc aussi pour le plan
        [System.Serializable] public class LightDef { public V p; public Color color; public float intensity, range; }
        [System.Serializable] public class CaveDef { public string kind; public V p; public float ra, rb; }
        [System.Serializable] public class ChestDef { public V p, dir; }
        [System.Serializable] public class LayoutDef { public List<LightDef> lights; public List<CaveDef> caves; public List<string> chunks; public V entrance, entranceDir, boss; public List<V> path; public List<ChestDef> treasure; }

        public static bool Available => File.Exists(Layout);

        [MenuItem("Korvalune/Construire les Terriers de l'Astral")]
        public static void BuildMenu() { Build(); }

        public static GameBootstrap Build()
        {
            if (!Available) { Debug.LogWarning("[Terriers] layout.json absent"); return null; }
            var L = JsonUtility.FromJson<LayoutDef>(File.ReadAllText(Layout));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Terriers").transform;
            foreach (var n in L.chunks)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/{n}.prefab");
                if (!pf) { Debug.LogWarning("[Terriers] tronçon absent : " + n); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, env); go.isStatic = true;
                foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }
            CI.Step("terriers : tronçons");

            // ---- lumières : champignons luminescents et cristaux (sans ombres), torches des camps
            int nl = 0;
            foreach (var l in L.lights)
            {
                var li = new GameObject("Lueur").AddComponent<Light>(); li.transform.SetParent(env); li.transform.position = l.p.P;
                li.type = LightType.Point; li.color = l.color; li.intensity = l.intensity; li.range = l.range; li.shadows = LightShadows.None; nl++;
            }
            var rng = new System.Random(9);
            foreach (var c in L.caves.Where(c => c.kind == "camp" || c.kind == "gardien" || c.kind == "entree"))
                for (int k = 0; k < (c.kind == "gardien" ? 4 : 2); k++)
                {
                    float a = k * Mathf.PI * 2 / (c.kind == "gardien" ? 4 : 2) + 0.5f;
                    var p = Floor(c.p.P + new Vector3(Mathf.Cos(a) * c.ra * 0.55f, 0, Mathf.Sin(a) * c.rb * 0.55f));
                    var br = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPipeline.Root + "/Resources/Prefabs/World/Crypte/D_Brasero.prefab");
                    if (br) { var b = (GameObject)PrefabUtility.InstantiatePrefab(br, env); b.transform.position = p; }
                    DungeonFire(p + Vector3.up * 1.3f, 3.0f, 10f, env);
                }
            CI.Step($"terriers : {nl} lumières");

            // ---- ambiance : très sombre, lueur froide zénithale faible pour garder la lisibilité
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // lisible sur téléphone : sol et ennemis clairement visibles, parois plus sombres, flaques de lumière chaude et bleutée
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.58f); RenderSettings.ambientEquatorColor = new Color(0.34f, 0.31f, 0.30f); RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.13f);
            var moon = new GameObject("Lueur zénithale").AddComponent<Light>(); moon.type = LightType.Directional; moon.intensity = 0.95f; moon.color = new Color(0.62f, 0.70f, 0.88f);
            moon.transform.rotation = Quaternion.Euler(70, -20, 0); moon.shadows = LightShadows.Soft;

            // ---- caméra et jeu
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f); cam.farClipPlane = 260; cam.nearClipPlane = 0.3f;
            cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            WorldBuilder.PostProcess("Terriers", 0.75f);
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = WorldBuilder.Config();
            game.heroLight = 2.2f; game.zoneLevel = 6;
            game.zoneName = "Terriers de l'Astral"; game.questTitle = "Les Terriers de l'Astral";
            var ent = L.entrance.P; var dir = L.entranceDir.P.normalized;
            game.playerSpawn = ent + dir * 3f + Vector3.up * 0.1f;
            game.castleGate = ent;
            int camps = 0;
            foreach (var c in L.caves)
            {
                var p = c.p.P;
                switch (c.kind)
                {
                    case "camp": game.camps.Add(new GameBootstrap.Camp { enemyId = camps++ % 2 == 0 ? "araignee" : "loup", center = p, count = 3, radius = Mathf.Min(c.ra, c.rb) * 0.5f }); break;
                    case "champignons": game.camps.Add(new GameBootstrap.Camp { enemyId = "araignee", center = p, count = 2, radius = 3 }); break;
                    case "ossuaire": game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = p, count = 5, radius = 4 }); break;
                    case "cristaux": game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = p, count = 3, radius = 3 }); break;
                    case "gardien":
                        game.camps.Add(new GameBootstrap.Camp { enemyId = "araignee", center = p, count = 1, radius = 1 });
                        game.camps.Add(new GameBootstrap.Camp { enemyId = "demon", center = p, count = 1, radius = 2 });
                        break;
                }
            }
            // patrouilles le long du parcours (entre les cavernes)
            for (int i = 4; i < L.path.Count - 3; i += 5) game.camps.Add(new GameBootstrap.Camp { enemyId = i % 2 == 0 ? "araignee" : "squelette", center = L.path[i].P, count = 2, radius = 2.5f });
            game.questSteps.Add(new Quest.Objective { text = "Chasser les araignées des terriers", enemyId = "araignee", count = 6 });
            game.questSteps.Add(new Quest.Objective { text = "Fouiller les terriers secondaires (coffres)", place = L.treasure.Count > 0 ? L.treasure[0].p.P : L.boss.P, radius = 3f });
            game.questSteps.Add(new Quest.Objective { text = "Abattre le gardien du repaire", enemyId = "demon", count = 1 });
            game.questSteps.Add(new Quest.Objective { text = "Revenir à l'entrée des terriers", place = ent, radius = 4f });

            // ---- coffres au fond des terriers secondaires
            var chestPf = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPipeline.Root + "/Resources/Prefabs/World/Ville/S_Coffre.prefab");
            foreach (var t in L.treasure)
            {
                var p = Floor(t.p.P);
                GameObject ch = chestPf ? (GameObject)PrefabUtility.InstantiatePrefab(chestPf, env) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                ch.name = "Coffre au trésor"; ch.transform.position = p;
                var d = t.dir.P; d.y = 0; if (d.sqrMagnitude > 0.01f) ch.transform.rotation = Quaternion.LookRotation(d);   // dos au fond du terrier
                ch.AddComponent<TreasureChest>();
            }

            // ---- portail de sortie, à l'entrée
            var exit = new GameObject("Sortie des terriers").AddComponent<Portal>();
            exit.transform.position = ent - dir * 1.5f;
            exit.targetScene = "Korvalune_Vallee"; exit.arrival = WorldBuilder.Ground(ValleyMouth + new Vector2(0, -6)); exit.label = "Sortir"; exit.displayName = "Remonter à la surface";
            cam.transform.position = game.playerSpawn + new Vector3(0, 14, -9); cam.transform.LookAt(game.playerSpawn);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            CI.Step("terriers : scène enregistrée");
            return game;
        }

        static Vector3 Floor(Vector3 p)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var h, 20f)) p.y = h.point.y;
            return p;
        }

        static void DungeonFire(Vector3 pos, float intensity, float range, Transform parent)
        {
            var l = new GameObject("Flamme").AddComponent<Light>(); l.transform.SetParent(parent); l.transform.position = pos;
            l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.3f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<Flicker>();
            var ps = l.gameObject.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 0.6f; m.startSpeed = 0.6f; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); m.maxParticles = 60;
            m.startColor = new Color(1f, 0.55f, 0.18f, 0.9f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.15f;
            var em = ps.emission; em.rateOverTime = 40; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
            var cfg = WorldBuilder.Config(); ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = cfg.vfx && cfg.vfx.fire ? cfg.vfx.fire : cfg.fxAdditive;
        }

        /// <summary>Entrée dans la vallée : bouche rocheuse illuminée (on la touche pour descendre).</summary>
        public static void ValleyEntrance()
        {
            var mouth = WorldBuilder.Ground(ValleyMouth);
            var root = new GameObject("Entrée des Terriers de l'Astral").transform; root.position = mouth;
            var rng = new System.Random(4);
            for (int k = 0; k < 7; k++)
            {
                float a = Mathf.Lerp(-150, -30, k / 6f) * Mathf.Deg2Rad;
                var p = mouth + new Vector3(Mathf.Cos(a) * 3.6f, 0, Mathf.Sin(a) * 3.6f + 1.5f);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Resources/Prefabs/World/Rocher{k % 3}.prefab");
                if (!pf) continue;
                var r = (GameObject)PrefabUtility.InstantiatePrefab(pf, root); r.transform.position = WorldBuilder.Ground(p) - Vector3.up * 0.4f;
                r.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0); r.transform.localScale = Vector3.one * (1.0f + (float)rng.NextDouble() * 0.8f);
            }
            var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hole.name = "Gueule du terrier"; Object.DestroyImmediate(hole.GetComponent<Collider>());
            hole.transform.SetParent(root); hole.transform.position = mouth + new Vector3(0, 0.02f, 1.5f); hole.transform.localScale = new Vector3(4.2f, 0.02f, 3.0f);
            var hm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); hm.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f)); hole.GetComponent<Renderer>().sharedMaterial = hm;
            var door = new GameObject("Descendre dans les terriers").AddComponent<Portal>();
            door.transform.SetParent(root); door.transform.position = mouth + new Vector3(0, 0, 1.5f);
            door.targetScene = "Korvalune_Terriers"; door.label = "Descendre"; door.displayName = "Terriers de l'Astral"; door.highlightRoot = root;
            door.glowColor = new Color(0.45f, 0.95f, 0.75f);
            if (Available) { var L = JsonUtility.FromJson<LayoutDef>(File.ReadAllText(Layout)); door.arrival = L.entrance.P + L.entranceDir.P.normalized * 3f + Vector3.up * 0.1f; }
        }

        public static void Captures(GameBootstrap game, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || game == null) return;
            Directory.CreateDirectory("ci-out");
            try
            {
                game.Begin();
                var cam = game.Cam;
                foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) CharacterRig.EditorIdlePose(h.gameObject);
                var L = JsonUtility.FromJson<LayoutDef>(File.ReadAllText(Layout));
                void Shot(string name)
                {
                    game.Hud.Refresh(); Canvas.ForceUpdateCanvases();
                    var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    File.WriteAllBytes($"ci-out/Terriers_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                    CI.Step("terriers : capture " + name);
                }
                var p = game.Player.transform; var cc = p.GetComponent<CharacterController>();
                cam.GetComponent<CameraRig>().Snap(); Shot("1_entree");
                int k = 2;
                foreach (var c in L.caves.Where(c => c.kind != "entree"))
                {
                    cc.enabled = false; p.position = Floor(c.p.P + new Vector3(0, 0, -c.rb * 0.5f)); cc.enabled = true;
                    cam.GetComponent<CameraRig>().Snap(); Shot($"{k++}_{c.kind}");
                    if (k > 5) break;
                }
                cc.enabled = false; p.position = Floor(L.boss.P + new Vector3(0, 0, -6)); cc.enabled = true;
                cam.GetComponent<CameraRig>().Snap(); Shot("6_repaire_du_gardien");
                // vue d'ensemble plongeante d'un tronçon du parcours
                var mid = L.path[L.path.Count / 2].P;
                cam.transform.position = mid + new Vector3(0, 45, -30); cam.transform.LookAt(mid); Shot("7_vue_du_parcours");
                // praticabilité : le héros peut-il aller de l'entrée au gardien ? (lancer de rayons le long du tracé)
                int blocked = 0; string where = "";
                for (int i = 0; i < L.path.Count - 1; i++)
                {
                    var a = L.path[i].P + Vector3.up * 1.4f; var b = L.path[i + 1].P + Vector3.up * 1.4f;
                    if (Physics.Raycast(a, (b - a).normalized, out var h, (b - a).magnitude) && !h.collider.isTrigger && h.collider.GetComponentInParent<Health>() == null) { blocked++; if (where == "") where = $"tronçon {i} ({h.collider.name})"; }
                }
                check("Terriers : parcours praticable de l'entrée au gardien", blocked == 0, blocked == 0 ? $"{L.path.Count} points du tracé dégagés" : $"{blocked} obstacles, premier : {where}");
                check("Captures des terriers", true, "ci-out/Terriers_*.png");
            }
            catch (System.Exception ex) { check("Captures des terriers", false, ex.Message); }
        }
    }
}
