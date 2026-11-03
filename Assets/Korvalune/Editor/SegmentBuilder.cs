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
    /// Régions de la carte de Korvalune construites en SEGMENTS séparés (une scène chacune) plutôt qu'en agrandissant la
    /// vallée : seule la région où se trouve le héros est en mémoire. Chaque segment est relié à la vallée par un portail
    /// placé au bout de la route correspondante, et possède sa pierre de voyage (menu commun à toutes les régions).
    ///   Forêt de l'Astral (nord), Désert des Étoiles (sud), Pics Sélénites (ouest) ; la Cité de l'Éclipse (est) existe déjà.
    /// Repère de chaque segment : X est, Z nord, terrain de 360 m centré sur l'origine, entrée du côté de la vallée.
    /// </summary>
    public static class SegmentBuilder
    {
        const string Root = ModelPipeline.Root;
        const float Size = 360f, MaxH = 110f, Floor = -4f;

        public enum Kind { Foret, Desert, Pics }

        public class Def
        {
            public Kind kind; public string scene, name, tag, quest; public int levelOffset;
            public Vector2 entry;              // arrivée dans le segment (côté vallée)
            public Vector2 valleyGate;         // portail dans la vallée (bout de la route)
            public string[] enemies;
            public string ScenePath => $"{Root}/Scenes/{scene}.unity";
        }

        public static readonly Def[] All =
        {
            new Def { kind = Kind.Foret, scene = "Korvalune_Foret", name = "Forêt de l'Astral", tag = "Foret", quest = "Les bois de l'Astral", levelOffset = 7,
                      entry = new Vector2(0, -150), valleyGate = new Vector2(20, 262), enemies = new[] { "loup", "araignee", "squelette" } },
            new Def { kind = Kind.Desert, scene = "Korvalune_Desert", name = "Désert des Étoiles", tag = "Desert", quest = "Sous les étoiles mortes", levelOffset = 17,
                      entry = new Vector2(0, 150), valleyGate = new Vector2(-60, -262), enemies = new[] { "demon", "squelette", "sorcier" } },
            new Def { kind = Kind.Pics, scene = "Korvalune_Pics", name = "Pics Sélénites", tag = "Pics", quest = "Les cols sélénites", levelOffset = 25,
                      entry = new Vector2(150, 0), valleyGate = new Vector2(-212, 10), enemies = new[] { "chevalier", "sorcier", "loup" } },
        };

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float N(float x, float z, float f, float o) => Mathf.PerlinNoise(x * f + o, z * f + o * 0.7f);

        /// <summary>Tracé du chemin principal du segment (de l'entrée vers le cœur de la région).</summary>
        static Vector2[] Path(Def d) => d.kind switch
        {
            Kind.Foret => new[] { d.entry, new Vector2(-20, -90), new Vector2(15, -20), new Vector2(-10, 60), new Vector2(20, 140) },
            Kind.Desert => new[] { d.entry, new Vector2(25, 90), new Vector2(-15, 20), new Vector2(30, -60), new Vector2(0, -140) },
            _ => new[] { d.entry, new Vector2(90, 15), new Vector2(20, -10), new Vector2(-50, 20), new Vector2(-140, 0) },
        };

        static float PathDist(Def d, Vector2 p)
        {
            var line = Path(d); float best = 1e9f;
            for (int i = 0; i < line.Length - 1; i++)
            {
                var a = line[i]; var ab = line[i + 1] - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        /// <summary>Relief propre à chaque région ; le chemin principal est aplani.</summary>
        public static float Height(Def d, float x, float z)
        {
            var p = new Vector2(x, z); float h;
            switch (d.kind)
            {
                case Kind.Foret:   // collines boisées, quelques combes
                    h = 8f + 9f * (N(x, z, 0.012f, 3.1f) - 0.5f) * 2f + 3f * (N(x, z, 0.04f, 7.7f) - 0.5f);
                    break;
                case Kind.Desert:  // dunes allongées et mesas (plateaux rocheux)
                    h = 6f + 3.5f * Mathf.Sin(x * 0.045f + 2.2f * N(x, z, 0.01f, 5f)) * (0.6f + 0.6f * N(x, z, 0.02f, 9f)) + 2f * (N(x, z, 0.05f, 1.3f) - 0.5f);
                    float mesa = N(x, z, 0.008f, 11f); h = Mathf.Lerp(h, 26f + 4f * N(x, z, 0.05f, 2f), Smooth(0.66f, 0.72f, mesa));
                    break;
                default:           // cols de montagne : arêtes de part et d'autre, crêtes enneigées au loin
                    float ridge = 1f - Mathf.Abs(N(x, z, 0.01f, 4.4f) - 0.5f) * 2f;
                    h = 10f + 55f * Mathf.Pow(ridge, 3f) + 18f * N(x, z, 0.025f, 8.8f);
                    break;
            }
            // chemin principal et clairière d'arrivée aplanis
            float pd = PathDist(d, p), flat = d.kind == Kind.Pics ? 14f : 9f;
            float target = d.kind == Kind.Pics ? 12f : d.kind == Kind.Desert ? 6f : 8f;
            float keep = d.kind == Kind.Desert ? 0.55f : 1f;           // désert : piste à peine marquée dans les dunes (pas de tranchée)
            h = Mathf.Lerp(h, target, keep * Smooth(flat + 14f, flat, pd));
            h = Mathf.Lerp(h, target, Smooth(26f, 16f, (p - d.entry).magnitude));
            // bords de la carte relevés (on ne sort pas du segment)
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h = Mathf.Lerp(h, h + 30f, Smooth(Size / 2 - 22f, Size / 2 - 4f, edge));
            return Mathf.Clamp(h, Floor + 1, MaxH - 2);
        }

        static Vector3 Ground(Def d, Vector2 p) => new Vector3(p.x, Height(d, p.x, p.y), p.y);

        [MenuItem("Korvalune/Construire les régions (segments)")]
        public static void BuildMenu() { foreach (var d in All) Build(d); }

        public static GameBootstrap Build(Def d)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CI.Step($"{d.tag} : relief");
            var td = new TerrainData { heightmapResolution = 257, alphamapResolution = 256, baseMapResolution = 512 };
            td.SetDetailResolution(128, 16);
            td.size = new Vector3(Size, MaxH, Size);
            int R = td.heightmapResolution; var H = new float[R, R];
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * i / (R - 1), z = -Size / 2 + Size * j / (R - 1);
                H[j, i] = (Height(d, x, z) - Floor) / MaxH;
            }
            td.SetHeights(0, 0, H);
            var tdPath = $"{Root}/Data/{d.tag}_Terrain.asset"; AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);   // avant la peinture
            td.terrainLayers = WorldBuilder.Layers();
            Paint(d, td);
            EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain"; tgo.transform.position = new Vector3(-Size / 2, Floor, -Size / 2);
            var terrain = tgo.GetComponent<Terrain>(); terrain.heightmapPixelError = 5; terrain.basemapDistance = 160; terrain.drawInstanced = true;
            terrain.treeDistance = 400; terrain.treeBillboardDistance = 120;
            var tmat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Rendering/Terrain.mat"); if (tmat) terrain.materialTemplate = tmat;
            Trees(d, td);
            terrain.Flush();

            CI.Step($"{d.tag} : décor");
            var env = new GameObject(d.name).transform; var rng = new System.Random(d.scene.GetHashCode() & 0xffff);
            Decor(d, env, rng);

            // ---- lumière de plein jour (pas de nuit dans le jeu)
            var sun = new GameObject("Soleil").AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.78f;
            sun.intensity = d.kind == Kind.Desert ? 1.8f : 1.6f; sun.color = d.kind == Kind.Desert ? new Color(1f, 0.92f, 0.76f) : new Color(1f, 0.94f, 0.82f);
            sun.transform.rotation = Quaternion.Euler(d.kind == Kind.Pics ? 36 : 44, -40, 0);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_AtmosphereThickness", d.kind == Kind.Desert ? 0.8f : 1.0f); sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.82f)); sky.SetColor("_GroundColor", new Color(0.36f, 0.36f, 0.32f)); sky.SetFloat("_Exposure", 1.15f);
            var skyPath = $"{Root}/Rendering/Ciel_{d.tag}.mat"; AssetDatabase.DeleteAsset(skyPath); AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky; RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.86f); RenderSettings.ambientEquatorColor = d.kind == Kind.Desert ? new Color(0.58f, 0.52f, 0.42f) : new Color(0.50f, 0.52f, 0.46f);
            RenderSettings.ambientGroundColor = d.kind == Kind.Desert ? new Color(0.34f, 0.28f, 0.20f) : new Color(0.24f, 0.22f, 0.17f);
            RenderSettings.fog = false;

            // ---- caméra et jeu
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 700; cam.nearClipPlane = 0.3f; cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            WorldBuilder.PostProcess(d.tag, 0.15f);
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = WorldBuilder.Config();
            game.zoneName = d.name; game.questTitle = d.quest;
            var arrival = d.entry + (Vector2.zero - d.entry).normalized * 12f;
            game.playerSpawn = Ground(d, arrival); game.castleGate = game.playerSpawn;
            game.safeCenter = game.playerSpawn; game.safeRadius = 14f;                     // clairière d'arrivée sûre
            game.waterLevel = -100f; game.populate = true; game.metersPerLevel = 45f; game.levelOffset = d.levelOffset;
            game.noCamps.Add(game.playerSpawn);
            // camps fixes le long du chemin, plus forts au fond de la région
            var path = Path(d);
            for (int i = 1; i < path.Length; i++)
            {
                var c = path[i] + new Vector2((float)rng.NextDouble() * 30 - 15, (float)rng.NextDouble() * 30 - 15);
                var id = d.enemies[(i - 1) % d.enemies.Length];
                game.camps.Add(new GameBootstrap.Camp { enemyId = id, center = Ground(d, c), count = 3 + i / 2, radius = 5 });
            }
            game.questSteps.Add(new Quest.Objective { text = $"Explorer {d.name}", place = Ground(d, path[2]), radius = 14f });
            game.questSteps.Add(new Quest.Objective { text = $"Vaincre les créatures de la région", enemyId = d.enemies[0], count = 6 });
            game.questSteps.Add(new Quest.Objective { text = "Atteindre le fond de la région", place = Ground(d, path[path.Length - 1]), radius = 16f });

            // ---- portail du retour vers la vallée, à l'entrée
            var back = new GameObject("Vers la vallée").AddComponent<Portal>();
            back.transform.position = Ground(d, d.entry);
            back.targetScene = "Korvalune_Vallee"; back.arrival = WorldBuilder.Ground(d.valleyGate + (Vector2.zero - d.valleyGate).normalized * 10f) + Vector3.up * 0.1f;
            back.label = "Partir"; back.displayName = "Vallée de Korvalune"; back.glowColor = new Color(0.65f, 0.85f, 1f);
            cam.transform.position = game.playerSpawn + new Vector3(0, 10, -10); cam.transform.LookAt(game.playerSpawn);

            MapShots.Save(d.scene);
            EditorSceneManager.SaveScene(scene, d.ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != d.ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(d.ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            CI.Step($"{d.tag} : scène enregistrée");
            return game;
        }

        // ------------------------------------------------------------------------------------------ terrain
        static void Paint(Def d, TerrainData td)
        {
            int A = td.alphamapResolution; var map = new float[A, A, td.terrainLayers.Length];
            for (int j = 0; j < A; j++) for (int i = 0; i < A; i++)
            {
                float x = -Size / 2 + Size * (i + 0.5f) / A, z = -Size / 2 + Size * (j + 0.5f) / A; var p = new Vector2(x, z);
                var n = td.GetInterpolatedNormal((i + 0.5f) / A, (j + 0.5f) / A); float slope = 1 - n.y;
                float noise = N(x, z, 0.05f, 3f), path = Smooth(5f, 2.5f, PathDist(d, p) + noise * 1.5f);
                float herbe = 0, chemin = 0, roche = 0, sousbois = 0, sable = 0;
                switch (d.kind)
                {
                    case Kind.Foret: sousbois = 0.6f + 0.8f * N(x, z, 0.03f, 1f); herbe = 0.5f + 0.6f * N(x, z, 0.02f, 6f); roche = Smooth(0.25f, 0.4f, slope) * 2; break;
                    case Kind.Desert: sable = 1.5f; roche = Smooth(0.2f, 0.32f, slope) * 3 + Smooth(22f, 26f, Height(d, x, z)) * 1.5f; chemin = 0.3f * N(x, z, 0.04f, 5f); break;
                    default: herbe = 0.6f * Smooth(30f, 18f, Height(d, x, z)); roche = Smooth(0.15f, 0.3f, slope) * 2.5f + Smooth(20f, 40f, Height(d, x, z)) * 1.5f; chemin = 0.3f; break;
                }
                chemin += path * (d.kind == Kind.Desert ? 1.2f : 3f);
                float s = herbe + chemin + roche + sousbois + sable + 1e-4f;
                map[j, i, 0] = herbe / s; map[j, i, 1] = chemin / s; map[j, i, 3] = roche / s; map[j, i, 4] = sousbois / s; map[j, i, 5] = sable / s;
            }
            td.SetAlphamaps(0, 0, map);
        }

        static void Trees(Def d, TerrainData td)
        {
            var names = d.kind == Kind.Foret ? new[] { "Chene0", "Chene1", "Chene2", "Pin0", "Pin1" } : d.kind == Kind.Pics ? new[] { "Pin0", "Pin1", "Pin2" } : new[] { "Pin2" };
            var protos = new List<TreePrototype>();
            foreach (var n in names)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Arbres/{n}_Arbre.prefab");
                if (!pf) pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{n}.prefab");
                if (pf) protos.Add(new TreePrototype { prefab = pf });
            }
            if (protos.Count == 0) return;
            td.treePrototypes = protos.ToArray();
            int target = d.kind == Kind.Foret ? 2600 : d.kind == Kind.Pics ? 700 : 40;
            var rng = new System.Random(17); var inst = new List<TreeInstance>();
            for (int t = 0; t < target * 12 && inst.Count < target; t++)
            {
                float x = (float)rng.NextDouble() * (Size - 20) - (Size - 20) / 2, z = (float)rng.NextDouble() * (Size - 20) - (Size - 20) / 2; var p = new Vector2(x, z);
                if (PathDist(d, p) < 8f || (p - d.entry).magnitude < 24f) continue;
                float h = Height(d, x, z);
                if (d.kind == Kind.Pics && h > 45f) continue;                         // pas d'arbres sur les crêtes
                float dens = d.kind == Kind.Foret ? 0.55f + 0.45f * N(x, z, 0.03f, 2f) : 0.35f + 0.5f * N(x, z, 0.02f, 5f);
                if (N(x, z, 0.05f, 9f) < 0.32f && d.kind == Kind.Foret) dens *= 0.15f;   // clairières
                if (rng.NextDouble() > dens) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                inst.Add(new TreeInstance { prototypeIndex = rng.Next(protos.Count), position = new Vector3((x + Size / 2) / Size, 0, (z + Size / 2) / Size), widthScale = s, heightScale = s, rotation = (float)rng.NextDouble() * 6.28f, color = Color.white, lightmapColor = Color.white });
            }
            td.SetTreeInstances(inst.ToArray(), true);
        }

        // ------------------------------------------------------------------------------------------ décor
        static void Decor(Def d, Transform env, System.Random rng)
        {
            int rocks = d.kind == Kind.Desert ? 90 : d.kind == Kind.Pics ? 120 : 50;
            for (int k = 0; k < rocks; k++)
            {
                var p = new Vector2((float)rng.NextDouble() * (Size - 30) - (Size - 30) / 2, (float)rng.NextDouble() * (Size - 30) - (Size - 30) / 2);
                if (PathDist(d, p) < 7f || (p - d.entry).magnitude < 20f) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Rocher{k % 3}.prefab"); if (!pf) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, env);
                go.transform.position = Ground(d, p) - Vector3.up * 0.4f; go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                go.transform.localScale *= 0.6f + (float)rng.NextDouble() * (d.kind == Kind.Pics ? 2.2f : 1.4f);
                if (d.kind == Kind.Desert) foreach (var r in go.GetComponentsInChildren<Renderer>()) { var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(1.0f, 0.86f, 0.66f)); r.SetPropertyBlock(mpb); }
                foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }
            if (d.kind == Kind.Desert)
            {
                // ruines d'un ancien temple des étoiles : colonnes brisées en cercle, au fond de la région
                var c = Path(d)[3]; var stone = WorldBuilder.StoneMaterial();
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI * 2 / 10; var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f;
                    var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder); col.name = "Colonne"; col.transform.SetParent(env);
                    float hgt = 1.5f + (float)rng.NextDouble() * 4.5f; col.transform.position = Ground(d, p) + Vector3.up * hgt / 2; col.transform.localScale = new Vector3(1.2f, hgt / 2, 1.2f);
                    col.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 8 - 4, 0, (float)rng.NextDouble() * 8 - 4);
                    col.GetComponent<Renderer>().sharedMaterial = stone; col.isStatic = true;
                }
            }
        }

        // ------------------------------------------------------------------------------------------ vallée
        /// <summary>Portails de la vallée vers chaque segment (au bout des routes du nord, du sud et de l'ouest), avec bornes.</summary>
        public static void ValleyEntrances()
        {
            foreach (var d in All)
            {
                var p = WorldBuilder.Ground(d.valleyGate);
                var root = new GameObject("Route : " + d.name).transform; root.position = p;
                var dir = (Vector2.zero - d.valleyGate).normalized; var side = new Vector3(-dir.y, 0, dir.x);
                foreach (var s in new[] { -1, 1 })
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = "Borne"; b.transform.SetParent(root);
                    b.transform.position = p + side * (3.2f * s) + Vector3.up * 1.6f; b.transform.localScale = new Vector3(0.9f, 3.2f, 0.9f);
                    b.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); b.isStatic = true;
                }
                var door = new GameObject("Vers " + d.name).AddComponent<Portal>();
                door.transform.SetParent(root); door.transform.position = p;
                door.targetScene = d.scene; door.label = "Voyager"; door.displayName = d.name; door.highlightRoot = root;
                door.glowColor = new Color(0.65f, 0.85f, 1f);
                door.arrival = Ground(d, d.entry + (Vector2.zero - d.entry).normalized * 12f) + Vector3.up * 0.1f;
            }
        }

        // ------------------------------------------------------------------------------------------ captures
        public static void Captures(Def d, GameBootstrap game, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || game == null) return;
            Directory.CreateDirectory("ci-out");
            try
            {
                game.Begin();
                var cam = game.Cam;
                void Shot(string name)
                {
                    game.Hud.Refresh(); Canvas.ForceUpdateCanvases();
                    var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    File.WriteAllBytes($"ci-out/Region_{d.tag}_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                }
                cam.GetComponent<CameraRig>().Snap(); Shot("1_arrivee");
                var mid = Ground(d, Path(d)[2]);
                cam.transform.position = mid + new Vector3(-40, 45, -40); cam.transform.LookAt(mid); Shot("2_vue");
                check($"Région : {d.name}", true, $"ci-out/Region_{d.tag}_*.png, {game.camps.Count} camps, niveau +{d.levelOffset}");
            }
            catch (System.Exception ex) { check($"Captures : {d.name}", false, ex.Message); }
        }
    }
}
