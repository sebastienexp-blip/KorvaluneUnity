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
    /// Construit la scène jouable « Vallée de Korvalune » d'après la carte : relief (vallée centrale, Pics Sélénites à
    /// l'ouest, collines boisées de la Forêt de l'Astral au nord, Désert des Étoiles au sud, côte à l'est), Fleuve de la
    /// Comète qui contourne le château, routes de terre, Château de Korvalune au centre, forêts et rochers, ciel et
    /// brume ; puis la configuration de jeu (héros, ennemis, camps, quête) et les captures de contrôle.
    /// Repère : X est, Z nord, château en (0, 0), terrain de 640 m de côté.
    /// </summary>
    public static class WorldBuilder
    {
        public const string Root = ModelPipeline.Root;
        public const string ScenePath = Root + "/Scenes/Korvalune_Vallee.unity";
        const string DataDir = Root + "/Data", WorldArt = Root + "/Art/World";
        const float Size = 640f, MaxH = 140f, Base = 12f;
        static readonly Vector2 Gate = new Vector2(0, -36);
        /// <summary>Entrée de la crypte, au bord du vieux cimetière.</summary>
        public static Vector3 CryptEntrance => Ground(Grave + new Vector2(-11, -1));
        /// <summary>Vieux cimetière (camp des squelettes), à l'écart, au nord-est de la ville, au bout d'un chemin de terre qui part de la route de l'est.</summary>
        public static readonly Vector2 Grave = new Vector2(122, 74);

        // ------------------------------------------------------------------------------------------ relief
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Fbm(float x, float z, float f, int oct, float seed)
        {
            float s = 0, a = 1, n = 0;
            for (int i = 0; i < oct; i++) { s += a * (Mathf.PerlinNoise(x * f + seed + i * 17.3f, z * f - seed + i * 9.1f) - 0.5f); n += a; a *= 0.5f; f *= 2.03f; }
            return s / n * 2f;
        }

        /// <summary>Tracé du Fleuve de la Comète (polyligne nord → contourne le château par l'ouest → sud-est).</summary>
        public static readonly Vector2[] River =
        {
            new Vector2(-40, 330), new Vector2(-55, 230), new Vector2(-60, 150), new Vector2(-85, 80), new Vector2(-95, 10), new Vector2(-80, -60),
            new Vector2(-45, -105), new Vector2(10, -125), new Vector2(70, -150), new Vector2(115, -205), new Vector2(150, -330)
        };
        public static readonly Vector2[][] Roads =
        {
            new[] { Gate, new Vector2(0, -70), new Vector2(-5, -110), new Vector2(0, -150), new Vector2(-20, -200), new Vector2(-60, -260) },        // route du sud (désert)
            new[] { new Vector2(0, -150), new Vector2(60, -175), new Vector2(120, -190), new Vector2(200, -230) },                                    // vers le sud-est
            new[] { new Vector2(-5, -70), new Vector2(-50, -45), new Vector2(-95, -30), new Vector2(-150, -20), new Vector2(-210, 10) },              // pont de l'ouest → pics
            new[] { new Vector2(30, -40), new Vector2(80, -20), new Vector2(140, 10), new Vector2(210, 30), new Vector2(270, 40) },                    // route de l'est (côte)
            new[] { new Vector2(20, 35), new Vector2(30, 80), new Vector2(40, 120), new Vector2(35, 180), new Vector2(20, 260) },                     // vers la forêt
            new[] { new Vector2(140, 10), new Vector2(136, 36), new Vector2(124, 58), new Vector2(118, 66) },                                          // chemin du vieux cimetière
        };
        public static readonly Vector2 Bridge = new Vector2(-95, -28);

        static float DistPolyline(Vector2 p, Vector2[] line, out float along)
        {
            float best = 1e9f; along = 0; float acc = 0;
            for (int i = 0; i < line.Length - 1; i++)
            {
                var a = line[i]; var b = line[i + 1]; var ab = b - a; float L = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (L * L));
                float d = (a + ab * t - p).magnitude;
                if (d < best) { best = d; along = acc + t * L; }
                acc += L;
            }
            return best;
        }

        public static float RoadDist(Vector2 p) { float best = 1e9f; foreach (var r in Roads) best = Mathf.Min(best, DistPolyline(p, r, out _)); return best; }
        public static float RiverDist(Vector2 p) => DistPolyline(p, River, out _);

        /// <summary>Hauteur (m) du terrain au point (x, z).</summary>
        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = Base + 2.5f * Fbm(x, z, 0.012f, 4, 3.1f);                                  // vallée doucement ondulée
            float west = Smooth(-120, -230, x);                                                    // Pics Sélénites
            float ridges = Mathf.Abs(Fbm(x, z, 0.010f, 5, 11.7f));
            h += west * (60 + 75 * (1 - ridges) + 25 * Fbm(x, z, 0.03f, 3, 5.5f));
            float north = Smooth(140, 260, z) * (1 - west);                                        // collines boisées
            h += north * (14 + 10 * Fbm(x, z, 0.02f, 4, 7.7f));
            float south = Smooth(-170, -260, z) * (1 - west);                                      // dunes du désert
            h += south * (3 + 5 * Mathf.Abs(Mathf.Sin(x * 0.045f + 2 * Fbm(x, z, 0.01f, 3, 2.2f))));
            float east = Smooth(230, 310, x);                                                      // côte : falaises puis mer
            h = Mathf.Lerp(h, -6, east);
            h += 6 * Smooth(80, 0, (p - Vector2.zero).magnitude) * 0;                              // (butte du château : aplanie plus bas)
            // plateau du château et de la ville (jusqu'à l'enceinte) légèrement surélevé
            float dc = p.magnitude;
            h = Mathf.Lerp(h, Base + 1.5f, Smooth(TownBuilder.WallR + 12, TownBuilder.WallR + 2, dc));   // château et ville
            // routes : aplanies
            float rd = RoadDist(p);
            h = Mathf.Lerp(h, Mathf.Min(h, Base + 1.0f + 0.5f * west * 40), Smooth(9, 3, rd) * (1 - west * 0.6f));
            // lit du fleuve
            float rv = RiverDist(p);
            float bank = Smooth(16, 6, rv);
            h = Mathf.Lerp(h, Base - 3.5f, bank);
            return Mathf.Clamp(h, -8, MaxH - 1);
        }
        public const float WaterLevel = Base - 1.6f;

        // ------------------------------------------------------------------------------------------ construction
        [MenuItem("Korvalune/Construire la Vallée de Korvalune")]
        public static void BuildMenu() { Build(); }

        public static GameBootstrap Build()
        {
            ModelPipeline.EnsureFolder(DataDir); ModelPipeline.EnsureFolder(Root + "/Scenes"); ModelPipeline.EnsureFolder(Root + "/Rendering");
            ConfigureTerrainTextures();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- terrain
            CI.Step("vallée : relief");
            var td = new TerrainData { heightmapResolution = 513, alphamapResolution = 512, baseMapResolution = 1024 };
            td.SetDetailResolution(256, 16);
            td.size = new Vector3(Size, MaxH, Size);
            int R = td.heightmapResolution; var H = new float[R, R];
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * i / (R - 1), z = -Size / 2 + Size * j / (R - 1);
                H[j, i] = (Height(x, z) + 8f) / MaxH;
            }
            td.SetHeights(0, 0, H);
            CI.Step("vallée : peinture du terrain");
            // l'asset est créé AVANT la peinture : les textures de mélange (alphamaps) sont des sous-assets ; peintes avant,
            // elles n'étaient pas enregistrées et, au rechargement, tout le terrain retombait sur la première couche (herbe
            // partout : plus de chemins, de pavés en ville, de sable ni de roche)
            var tdPath = DataDir + "/Vallee_Terrain.asset";
            AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);
            td.terrainLayers = Layers();
            Paint(td);
            EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-Size / 2, -8f, -Size / 2);
            var terrain = tgo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4; terrain.basemapDistance = 220; terrain.treeBillboardDistance = 140; terrain.treeDistance = 600; terrain.detailObjectDistance = 60;
            terrain.drawInstanced = true;
            var tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); var tmp = Root + "/Rendering/Terrain.mat";
            tmat.EnableKeyword("_MASKMAP");                                                       // cartes de masque des couches (occlusion, relief, lissé)
            AssetDatabase.DeleteAsset(tmp); AssetDatabase.CreateAsset(tmat, tmp); terrain.materialTemplate = tmat;
            Trees(td, terrain);
            Grass(td);
            CI.Step("vallée : arbres");
            terrain.Flush();

            CI.Step("vallée : eau, château, décor");
            // ---- eau (fleuve et mer)
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "Eau";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = new Vector3(0, WaterLevel, 0); water.transform.localScale = new Vector3(Size / 10f * 1.6f, 1, Size / 10f * 1.6f);
            water.GetComponent<Renderer>().sharedMaterial = WaterMaterial();
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---- château, pont, rochers
            var castle = Place("World/ChateauKorvalune", new Vector3(0, Base + 1.5f - 8f + 8f, 0), 0);
            TownBuilder.Build();
            Hamlets();
            Roadside();
            EclipseBuilder.ValleyEntrance();
            SegmentBuilder.ValleyEntrances();               // portails vers la Forêt (nord), le Désert (sud) et les Pics (ouest)
            CaveBuilder.ValleyEntrance();
            // abords des routes : objets cassables (hors de la ville, du fleuve et de la côte)
            PropKit.Ensure();
            var clutter = new GameObject("Abords des routes").transform; var crng = new System.Random(4242);
            foreach (var road in Roads)
                PropKit.AlongPath(road, Ground, q => q.magnitude > TownBuilder.WallR + 14f && RiverDist(q) > 16f && q.x < 225f && Mathf.Abs(q.x) < 300 && Mathf.Abs(q.y) < 300, clutter, crng, null, 26f);
            // ponts de bois à chaque croisement d'une route et du fleuve
            int nb = 0;
            foreach (var (cp, cd) in BridgeBuilder.Crossings(Roads, River))
                BridgeBuilder.Build(nb == 0 ? "Pont" : $"Pont {nb}", Ground(cp - cd * 19f), Ground(cp + cd * 19f), 5.6f, Base - 3.5f, null, 11 + nb++);
            var rng = new System.Random(7);
            // cimetière (camp des squelettes) : pierres tombales et rochers
            var grave = Grave;                                    // à l'écart, au bout du chemin du nord-est
            var tombs = new List<Vector2>();
            for (int k = 0; k < 18; k++)
            {
                // trois rangées de six tombes, alignées comme dans un vrai cimetière (avec le désordre des ans)
                int row = k / 6, col = k % 6;
                var o = grave + new Vector2(-4f + col * 3.3f + (float)rng.NextDouble() * 0.6f - 0.3f, -3.4f + row * 3.4f + (float)rng.NextDouble() * 0.5f - 0.25f);
                tombs.Add(o);
                // pierres tombales modélisées : stèle cintrée, croix, stèle brisée, stèle à fronton (penchées, enfoncées)
                var g = new GameObject("Tombe"); var mesh = TombMesh(k % 4);
                g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = StoneMaterial();
                var bc = g.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size;
                float sc = 0.85f + (float)rng.NextDouble() * 0.4f;
                g.transform.position = Ground(o) - Vector3.up * 0.08f; g.transform.localScale = Vector3.one * sc;
                g.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 14 - 7, (float)rng.NextDouble() * 40 - 20, (float)rng.NextDouble() * 14 - 7);
                g.isStatic = true;
            }
            for (int k = 0; k < 70; k++)
            {
                Vector2 o;
                if (k < 10)
                {
                    // rochers autour du cimetière, jamais sur une tombe (2,5 m d'écart au moins)
                    int tries = 0;
                    do { o = grave + new Vector2((float)rng.NextDouble() * 34 - 17, (float)rng.NextDouble() * 14 - 7); }
                    while ((tombs.Exists(t => (t - o).sqrMagnitude < 3f * 3f) || (o - new Vector2(CryptEntrance.x, CryptEntrance.z)).sqrMagnitude < 36f) && ++tries < 40);
                    if (tries >= 40) continue;
                }
                else
                {
                    do { o = new Vector2((float)rng.NextDouble() * Size - Size / 2, (float)rng.NextDouble() * Size - Size / 2); }
                    while (o.magnitude < TownBuilder.WallR + 12 || RoadDist(o) < 10 || RiverDist(o) < 14 || o.x > 240 || NearHamlet(o, 20));
                }
                if (k < 10) { Place("World/Rocher0", Ground(o) - Vector3.up * 0.3f, (float)rng.NextDouble() * 360, 0.5f + (float)rng.NextDouble() * 0.4f); continue; }   // petits rochers au cimetière
                Place($"World/Rocher{k % 3}", Ground(o) - Vector3.up * 0.3f, (float)rng.NextDouble() * 360, 0.6f + (float)rng.NextDouble() * (o.x < -150 ? 2.5f : 1.2f));
            }

            // ---- lumière, ciel, brume
            var sun = new GameObject("Soleil").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.color = new Color(1f, 0.93f, 0.80f); sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;                                           // ombres franches mais lisibles (pas de zones noires)
            sun.transform.rotation = Quaternion.Euler(38, -40, 0);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_AtmosphereThickness", 1.0f); sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.80f)); sky.SetColor("_GroundColor", new Color(0.35f, 0.38f, 0.32f)); sky.SetFloat("_Exposure", 1.15f);
            var skyPath = Root + "/Rendering/Ciel.mat"; AssetDatabase.DeleteAsset(skyPath); AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.84f); RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.46f); RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.17f);   // lumière du ciel plus généreuse dans les ombres
            RenderSettings.fog = false; RenderSettings.fogMode = FogMode.Linear;   // pas de brouillard (demande de Sébastien)
            RenderSettings.fogStartDistance = 170; RenderSettings.fogEndDistance = 820; RenderSettings.fogColor = new Color(0.70f, 0.76f, 0.84f);

            CI.Step("vallée : caméra, post-traitement, configuration");
            // ---- caméra et jeu
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 900; cam.nearClipPlane = 0.3f;
            cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            PostProcess("Vallee", 0.45f);
            Ambience(cam.transform);
            CI.Step("vallée : configuration du jeu");
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = Config();
            game.playerSpawn = Ground(new Vector2(0, -68));            // en ville, rue principale au sud de la place du marché
            game.safeCenter = Vector3.zero; game.safeRadius = TownBuilder.WallR + 6f;   // la ville : aucun monstre
            game.waterLevel = WaterLevel; game.populate = true; game.metersPerLevel = 13f;
            game.noCamps.Clear(); foreach (var h in HamletSites) game.noCamps.Add(Ground(h));   // pas de camp de monstres dans les hameaux
            game.castleGate = Ground(Gate);
            game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = Ground(grave), count = 6, radius = 10 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "loup", center = Ground(new Vector2(45, 120)), count = 4, radius = 8 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "sorcier", center = Ground(Bridge + new Vector2(-14, 4)), count = 1, radius = 2 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "squelette", center = Ground(Bridge + new Vector2(-16, -6)), count = 2, radius = 4 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "araignee", center = Ground(new Vector2(-150, 60)), count = 2, radius = 8 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "chevalier", center = Ground(new Vector2(150, 10)), count = 2, radius = 5 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "demon", center = Ground(new Vector2(-30, -240)), count = 1, radius = 2 });
            // entrée de la crypte (escalier qui descend, portail) au bord du cimetière
            // mausolée de pierre posé sur le sol (plus d'escalier enfoui qui traversait le relief) : porte sombre et lueur violette
            CI.Step("vallée : entrée de la crypte");
            var st = DungeonGate("Entrée de la crypte", CryptEntrance, 180f, new Color(0.7f, 0.45f, 1f));
            var door = new GameObject("Descendre dans la crypte").AddComponent<Portal>();
            door.displayName = "Crypte de Korvalune"; door.highlightRoot = st.transform; door.glowColor = new Color(0.7f, 0.45f, 1f);
            door.transform.position = st.transform.TransformPoint(new Vector3(0, 0.1f, -2.9f));
            door.targetScene = "Korvalune_Crypte"; door.label = "Descendre";
            door.arrival = new Vector3(-4f + 3 * 4f, 0.05f, (5 - 9) * 4f);     // pied de l'escalier de la crypte (case E, un pas à côté)
            cam.transform.position = game.playerSpawn + new Vector3(0, 8, -9); cam.transform.LookAt(game.playerSpawn);

            // bords : murs invisibles 22 m avant le bord (on ne voit pas le bout du terrain) ; roche à l'ouest, sable au sud,
            // herbe au nord, la mer à l'est
            CI.Step("vallée : bords de la carte");
            SegmentBuilder.Enclose(Size / 2, 22f, -8f, 30f, new[] { SegmentBuilder.BeltMaterial("Vallee_N", "Herbe", new Color(0.8f, 0.85f, 0.7f)), SegmentBuilder.BeltMaterial("Vallee_S", "Sable", Color.white), null, SegmentBuilder.BeltMaterial("Vallee_O", "Roche", Color.white) });
            CI.Step("vallée : photo pour la grande carte");
            MapShots.Save("Korvalune_Vallee");                                   // photo de la région pour la grande carte
            EditorSceneManager.SaveScene(scene, ScenePath);
            CI.Step("vallée : sauvegarde de la scène");
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            return game;
        }

        public static Vector3 Ground(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        // ------------------------------------------------------------------------------------------ hameaux et herbe
        /// <summary>Hameaux le long des routes, hors les murs : fermes et maisons autour d'un puits, foin, charrette, tonneaux.</summary>
        public static readonly Vector2[] HamletSites = { new Vector2(112, 24), new Vector2(-30, -158), new Vector2(140, -150), new Vector2(60, 92), new Vector2(-42, -208) };
        static bool NearHamlet(Vector2 p, float r) { foreach (var h in HamletSites) if ((p - h).magnitude < r) return true; return false; }

        static GameObject Ville(string name, Vector3 pos, float yaw)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Ville/{name}.prefab");
            if (!p) { Debug.LogWarning("[Monde] pièce de hameau absente : " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); return go;
        }

        static void Hamlets()
        {
            var rng = new System.Random(11);
            var root = new GameObject("Hameaux").transform;
            foreach (var c in HamletSites)
            {
                var well = Ville("M_Puits", Ground(c) - Vector3.up * 0.15f, (float)rng.NextDouble() * 360);
                if (well) well.transform.SetParent(root, true);
                int houses = 3 + rng.Next(2);
                float a0 = (float)rng.NextDouble() * 360;
                for (int k = 0; k < houses; k++)
                {
                    float a = (a0 + k * 360f / houses + (float)rng.NextDouble() * 20 - 10) * Mathf.Deg2Rad;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12f + (float)rng.NextDouble() * 3f);
                    if (RoadDist(p) < 6.5f || RiverDist(p) < 16f) continue;
                    var face = c - p; float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;        // façade (−Z local) vers le puits
                    var h = Ville($"M_Maison{rng.Next(8)}", Ground(p) - Vector3.up * 0.35f, yaw);
                    if (h) h.transform.SetParent(root, true);
                }
                string[] small = { "M_Foin", "M_Charrette", "M_Tonneaux", "M_Caisses", "M_Banc", "M_Lanterne" };
                for (int k = 0; k < 4; k++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (5f + (float)rng.NextDouble() * 3f);
                    if (RoadDist(p) < 4f) continue;
                    var g = Ville(small[rng.Next(small.Length)], Ground(p) - Vector3.up * 0.05f, (float)rng.NextDouble() * 360);
                    if (g) g.transform.SetParent(root, true);
                }
            }
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        /// <summary>Bords de route vivants : bornes, panneaux indicateurs aux carrefours, charrettes, tonneaux et caisses
        /// destructibles, lanternes près de la ville ; rien dans le lit des routes (le passage reste dégagé).</summary>
        static readonly (Vector2 at, string[] labels)[] Signposts =
        {
            (new Vector2(6, -72), new[] { "Château ↑", "Désert du Sud ↓", "Pont de l'Ouest ←" }),
            (new Vector2(8, -152), new[] { "Désert du Sud ↓", "Marais du Sud-Est →", "Château ↑" }),
            (new Vector2(38, -36), new[] { "Côte de l'Est →", "Château ←" }),
            (new Vector2(146, 4), new[] { "Vieux cimetière ↑", "Château ←", "Côte de l'Est →" }),
            (new Vector2(26, 40), new[] { "Forêt du Nord ↑", "Château ↓" }),
        };
        static void Roadside()
        {
            var rng = new System.Random(17);
            var root = new GameObject("Bords de route").transform;
            string[] props = { "M_Tonneaux", "M_Caisses", "M_Tonneaux", "M_Caisses", "M_Charrette", "M_Foin" };
            foreach (var road in Roads)
            {
                float acc = 0, next = 14f;
                for (int i = 0; i < road.Length - 1; i++)
                {
                    var a = road[i]; var b = road[i + 1]; var ab = b - a; float L = ab.magnitude; var dir = ab / L; var side = new Vector2(-dir.y, dir.x);
                    while (next <= acc + L)
                    {
                        var c = a + dir * (next - acc); next += 18f + (float)rng.NextDouble() * 16f;
                        if (c.magnitude < TownBuilder.WallR + 6 || NearHamlet(c, 18) || RiverDist(c) < 12) continue;
                        float sd = rng.Next(2) == 0 ? 1 : -1; var p = c + side * sd * (5.2f + (float)rng.NextDouble() * 1.5f);
                        if (RoadDist(p) < 4.2f) continue;
                        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + (float)rng.NextDouble() * 40 - 20;
                        GameObject g;
                        if (c.magnitude < 140 && rng.Next(3) == 0) g = Ville("M_Lanterne", Ground(p) - Vector3.up * 0.05f, yaw);
                        else if (rng.Next(4) == 0) g = Milestone(Ground(p), yaw);
                        else g = Ville(props[rng.Next(props.Length)], Ground(p) - Vector3.up * 0.05f, yaw);
                        if (!g) continue;
                        g.transform.SetParent(root, true);
                        if (g.name.Contains("Tonneaux") || g.name.Contains("Caisses")) Breakable.Make(g, 1.0f);
                    }
                    acc += L;
                }
            }
            foreach (var (at, labels) in Signposts) Signpost(Ground(at), labels).transform.SetParent(root, true);
            foreach (var t in root.GetComponentsInChildren<Transform>()) if (!t.GetComponent<Breakable>()) t.gameObject.isStatic = true;
        }

        static Material RoadMat(Color c)
        {
            var m = new Material(Config().litMat ? Config().litMat : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat"));
            m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            var path = $"{DataDir}/Route_{ColorUtility.ToHtmlStringRGB(c)}.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path); if (old) return old;
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static GameObject Milestone(Vector3 p, float yaw)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Borne"; g.transform.position = p + Vector3.up * 0.35f;
            g.transform.rotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale = new Vector3(0.35f, 0.9f, 0.25f);
            g.GetComponent<Renderer>().sharedMaterial = RoadMat(new Color(0.55f, 0.53f, 0.48f));
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(cap.GetComponent<Collider>()); cap.name = "Sommet"; cap.transform.SetParent(g.transform, false);
            cap.transform.localPosition = new Vector3(0, 0.5f, 0); cap.transform.localRotation = Quaternion.Euler(90, 0, 0); cap.transform.localScale = new Vector3(1f, 0.5f, 1f);
            cap.GetComponent<Renderer>().sharedMaterial = g.GetComponent<Renderer>().sharedMaterial;
            return g;
        }

        static GameObject Signpost(Vector3 p, string[] labels)
        {
            var root = new GameObject("Panneau indicateur"); root.transform.position = p;
            var wood = RoadMat(new Color(0.40f, 0.27f, 0.15f));
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Poteau"; post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0, 1.4f, 0); post.transform.localScale = new Vector3(0.16f, 2.8f, 0.16f); post.GetComponent<Renderer>().sharedMaterial = wood;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < labels.Length; i++)
            {
                float yaw = labels[i].Contains("→") ? 90 : labels[i].Contains("←") ? -90 : labels[i].Contains("↓") ? 180 : 0;
                var arm = new GameObject("Planche").transform; arm.SetParent(root.transform, false); arm.localPosition = new Vector3(0, 2.45f - i * 0.38f, 0); arm.localRotation = Quaternion.Euler(0, yaw + 20 * i, 0);
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(board.GetComponent<Collider>()); board.transform.SetParent(arm, false);
                board.transform.localPosition = new Vector3(0, 0, 0.75f); board.transform.localScale = new Vector3(0.06f, 0.30f, 1.4f); board.GetComponent<Renderer>().sharedMaterial = wood;
                foreach (var sx in new[] { 1, -1 })
                {
                    var t = new GameObject("Texte").AddComponent<TextMesh>(); t.transform.SetParent(arm, false);
                    t.transform.localPosition = new Vector3(0.035f * sx, 0, 0.75f); t.transform.localRotation = Quaternion.Euler(0, sx > 0 ? -90 : 90, 0);
                    t.text = labels[i].Replace(" ↑", "").Replace(" ↓", "").Replace(" ←", "").Replace(" →", ""); t.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    t.fontSize = 48; t.characterSize = 0.035f; t.anchor = TextAnchor.MiddleCenter; t.color = new Color(0.95f, 0.88f, 0.70f); t.fontStyle = FontStyle.Bold;
                }
            }
            return root;
        }

        /// <summary>Herbes hautes peintes sur le terrain (prairies, lisières), absentes des routes, de la ville, du désert et des crêtes.</summary>
        static void Grass(TerrainData td)
        {
            var tex = GrassTexture(); if (!tex) return;
            // touffes en 3D (brins courbés) plutôt que des panneaux face caméra : vus de dessus, les panneaux devenaient des
            // paquets de points brillants qui scintillaient
            var clump = GrassClumpPrefab();
            var proto = clump ? new DetailPrototype
            {
                prototype = clump, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.6f, maxHeight = 1.1f, noiseSpread = 0.4f,
                healthyColor = Color.white, dryColor = new Color(1.0f, 0.92f, 0.75f)
            } : new DetailPrototype
            {
                prototypeTexture = tex, renderMode = DetailRenderMode.GrassBillboard, usePrototypeMesh = false,
                minWidth = 0.7f, maxWidth = 1.3f, minHeight = 0.45f, maxHeight = 0.95f, noiseSpread = 0.4f,
                healthyColor = new Color(0.95f, 1.0f, 0.88f), dryColor = new Color(1.0f, 0.9f, 0.7f)
            };
            td.detailPrototypes = new[] { proto };
            int R = td.detailResolution; var map = new int[R, R];
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * (i + 0.5f) / R, z = -Size / 2 + Size * (j + 0.5f) / R; var p = new Vector2(x, z);
                if (p.magnitude < TownBuilder.WallR + 6 || RoadDist(p) < 4.5f || RiverDist(p) < 9f || x > 232 || NearHamlet(p, 9)) continue;
                float h = Height(x, z);
                if (h < WaterLevel + 0.6f || h > 48f) continue;
                float dens = Mathf.PerlinNoise(x * 0.035f + 3.1f, z * 0.035f - 1.7f);
                dens *= 1f - 0.85f * Smooth(-170, -215, z);                         // le désert du sud est nu
                dens *= 1f - 0.6f * Smooth(-130, -175, x);                          // la montagne est rase
                map[j, i] = Mathf.RoundToInt(Mathf.Clamp01((dens - 0.25f) * 1.6f) * 7f);
            }
            td.SetDetailLayer(0, 0, 0, map);
        }

        /// <summary>Touffe d'herbe en 3D : une douzaine de brins effilés et courbés qui rayonnent du centre, teinte du pied
        /// (sombre) à la pointe (claire) par un dégradé, normales tournées vers le ciel (éclairage doux, sans face noire).</summary>
        public static GameObject GrassClumpPrefab()
        {
            string dir = WorldArt + "/Terrain", path = dir + "/Touffe.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            // dégradé pied → pointe
            var gpath = dir + "/Touffe_Degrade.png";
            if (!File.Exists(gpath))
            {
                var g = new Texture2D(4, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) { float f = y / 63f; var c = Color.Lerp(new Color(0.07f, 0.12f, 0.03f), new Color(0.42f, 0.52f, 0.20f), Mathf.Pow(f, 0.8f)); if (f > 0.85f) c = Color.Lerp(c, new Color(0.62f, 0.60f, 0.34f), (f - 0.85f) / 0.15f); for (int x = 0; x < 4; x++) g.SetPixel(x, y, c); }
                g.Apply(); File.WriteAllBytes(gpath, g.EncodeToPNG()); Object.DestroyImmediate(g); AssetDatabase.ImportAsset(gpath);
                var gi = (TextureImporter)AssetImporter.GetAtPath(gpath); gi.wrapMode = TextureWrapMode.Clamp; gi.mipmapEnabled = false; gi.textureCompression = TextureImporterCompression.Uncompressed; gi.SaveAndReimport();
            }
            var grad = AssetDatabase.LoadAssetAtPath<Texture2D>(gpath);
            var rng = new System.Random(21); var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int b = 0; b < 12; b++)
            {
                float ang = (float)(b * 2 * System.Math.PI / 12 + rng.NextDouble() * 0.5);
                float h = 0.35f + 0.3f * (float)rng.NextDouble(), lean = 0.12f + 0.25f * (float)rng.NextDouble(), w = 0.022f + 0.012f * (float)rng.NextDouble();
                var dir2 = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); var side = new Vector3(-dir2.z, 0, dir2.x);
                var root = dir2 * 0.04f * (float)rng.NextDouble();
                int baseIdx = v.Count; const int segs = 4;
                for (int k = 0; k <= segs; k++)
                {
                    float f = k / (float)segs; var c = root + dir2 * (lean * f * f) + Vector3.up * (h * f);
                    float ww = w * (1 - f * 0.92f);
                    v.Add(c - side * ww); v.Add(c + side * ww);
                    var nn = Vector3.Lerp(Vector3.up, -dir2, 0.25f).normalized; n.Add(nn); n.Add(nn);
                    uv.Add(new Vector2(0, f)); uv.Add(new Vector2(1, f));
                }
                for (int k = 0; k < segs; k++) { int a = baseIdx + k * 2; tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            }
            var mesh = new Mesh { name = "Touffe" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, dir + "/Touffe_Mesh.asset");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Touffe" };
            mat.SetTexture("_BaseMap", grad); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", 0.12f); mat.SetFloat("_Cull", 0f); mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, dir + "/Touffe.mat");
            var go = new GameObject("Touffe"); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            var pf = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go);
            return pf;
        }

        /// <summary>Texture de touffe d'herbe (brins effilés, transparence), créée une fois dans Art/World/Terrain.</summary>
        static Texture2D GrassTexture()
        {
            var path = WorldArt + "/Terrain/Herbe_Touffe.png";
            if (!File.Exists(path))
            {
                ModelPipeline.EnsureFolder(WorldArt + "/Terrain");
                const int W = 128, H = 128; var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var px = new Color[W * H]; var rng = new System.Random(5);
                for (int b = 0; b < 26; b++)
                {
                    float x0 = 10 + (float)rng.NextDouble() * (W - 20), lean = ((float)rng.NextDouble() - 0.5f) * 30f, hgt = H * (0.55f + 0.42f * (float)rng.NextDouble());
                    float wid = 2.5f + (float)rng.NextDouble() * 2.5f; float shade = 0.75f + 0.25f * (float)rng.NextDouble();
                    for (int y = 0; y < hgt; y++)
                    {
                        float f = y / hgt; float cx = x0 + lean * f * f; float w = wid * (1 - f);
                        for (int x = Mathf.FloorToInt(cx - w - 1); x <= Mathf.CeilToInt(cx + w + 1); x++)
                        {
                            if (x < 0 || x >= W) continue;
                            float a = Mathf.Clamp01(w + 0.6f - Mathf.Abs(x - cx));
                            if (a <= 0) continue;
                            var c = Color.Lerp(new Color(0.30f, 0.42f, 0.16f), new Color(0.78f, 0.86f, 0.52f), f) * shade; c.a = a;
                            int k = y * W + x; if (c.a > px[k].a) px[k] = c;
                        }
                    }
                }
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public static Vector3 Ground(Vector3 p) => new Vector3(p.x, Height(p.x, p.z), p.z);

        static GameObject Place(string rel, Vector3 pos, float yaw, float scale = 1f)
        {
            var path = $"{Root}/Resources/Prefabs/World/{Path.GetFileName(rel)}.prefab";
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!p) { Debug.LogWarning("[Monde] prefab absent : " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ------------------------------------------------------------------------------------------ textures et couches
        // les 4 premières couches sont rendues dans la passe principale du terrain (URP) : herbe, chemins, pavés, roche
        static readonly string[] LayerNames = { "Herbe", "Chemin", "Pave", "Roche", "SousBois", "Sable" };
        static readonly float[] LayerTile = { 6, 5, 3.5f, 14, 8, 10 };

        static void ConfigureTerrainTextures()
        {
            var dir = WorldArt + "/Terrain";
            if (!AssetDatabase.IsValidFolder(dir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (!ti) continue;
                bool changed = false;
                if (p.EndsWith("_Normal.png") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
        }

        public static TerrainLayer[] Layers()
        {
            var list = new List<TerrainLayer>();
            for (int i = 0; i < LayerNames.Length; i++)
            {
                var n = LayerNames[i];
                var path = $"{DataDir}/Couche_{n}.terrainlayer";
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
                l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WorldArt}/Terrain/{n}_BaseMap.png");
                l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WorldArt}/Terrain/{n}_Normal.png");
                l.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WorldArt}/Terrain/{n}_Mask.png");   // occlusion, relief, lissé par couche
                l.tileSize = new Vector2(LayerTile[i], LayerTile[i]); l.normalScale = 1.35f; l.smoothness = 0.1f; l.metallic = 0;
                l.maskMapRemapMin = Vector4.zero; l.maskMapRemapMax = new Vector4(1, 1, 1, 0.4f);   // lissé plafonné : sol sec, pas « après la pluie »
                EditorUtility.SetDirty(l); list.Add(l);
            }
            return list.ToArray();
        }

        static void Paint(TerrainData td)
        {
            int A = td.alphamapResolution; var map = new float[A, A, LayerNames.Length];
            for (int j = 0; j < A; j++) for (int i = 0; i < A; i++)
            {
                float x = -Size / 2 + Size * (i + 0.5f) / A, z = -Size / 2 + Size * (j + 0.5f) / A;
                var p = new Vector2(x, z);
                var n = td.GetInterpolatedNormal((i + 0.5f) / A, (j + 0.5f) / A);
                float slope = 1 - n.y;
                float h = Height(x, z);
                float w0 = 1, w1 = 0, w2 = 0, w3 = 0, w4 = 0;
                float noise = Mathf.PerlinNoise(x * 0.05f + 3, z * 0.05f + 7);
                w1 = Smooth(110, 170, z) * (0.6f + 0.6f * noise) + Smooth(0.6f, 0.9f, noise) * 0.4f;     // sous-bois (forêt nord, taches)
                w4 = Smooth(-150, -210, z) + Smooth(6, 2, RiverDist(p)) * 0.6f;                        // sable : désert et berges
                w3 = Smooth(0.18f, 0.35f, slope) * 2 + Smooth(60, 90, h);                                // roche : pentes et sommets
                float rd = RoadDist(p);
                float town = Smooth(TownBuilder.WallR + 6, TownBuilder.WallR - 2, p.magnitude);
                w2 = Smooth(4.5f, 2.0f, rd + noise * 1.5f) * 3 * (1 - town) + Smooth(40, 34, p.magnitude) * 2 + town * 0.35f;   // chemins, cour, terre battue en ville
                w1 *= 1 - town; w3 *= 1 - town * 0.8f; w4 *= 1 - town;
                float w5 = TownBuilder.Cobble(p) * 8;                                                   // rues pavées et place du marché
                // ville : sol de terre battue et de pavés (plus de pré dans les rues), herbe seulement en petites touffes
                float tufts = Smooth(0.62f, 0.78f, Mathf.PerlinNoise(x * 0.11f + 11, z * 0.11f + 5));
                w0 *= 1 - town * (1 - 0.35f * tufts);
                w2 += town * (0.9f + 0.6f * noise);
                w5 += Smooth(24f, 18f, (p - TownBuilder.Plaza).magnitude) * 6;                          // grande place autour de la fontaine
                // hors les murs : prairie variée (taches de terre et d'herbe sombre)
                w1 += (1 - town) * Smooth(0.58f, 0.72f, Mathf.PerlinNoise(x * 0.025f + 50, z * 0.025f + 20)) * 0.7f;
                w2 += (1 - town) * Smooth(0.70f, 0.82f, Mathf.PerlinNoise(x * 0.06f + 90, z * 0.06f + 40)) * 0.6f;
                if (x > 230) w4 += Smooth(230, 260, x) * 2;
                float s = w0 + w1 + w2 + w3 + w4 + w5;
                map[j, i, 0] = w0 / s; map[j, i, 4] = w1 / s; map[j, i, 1] = w2 / s; map[j, i, 3] = w3 / s; map[j, i, 5] = w4 / s; map[j, i, 2] = w5 / s;
            }
            td.SetAlphamaps(0, 0, map);
        }

        static void Trees(TerrainData td, Terrain terrain)
        {
            var protos = new List<TreePrototype>();
            string[] names = { "Chene0", "Chene1", "Chene2", "Pin0", "Pin1", "Pin2" };
            ModelPipeline.EnsureFolder(Root + "/Resources/Prefabs/World/Arbres");
            foreach (var n in names)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{n}.prefab");
                if (!p) continue;
                // arbre du terrain : LODGroup à la racine — LOD0 détaillé, LOD1 allégé, disparition au loin
                var p1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{n}_LOD1.prefab");
                var go = new GameObject(n);
                Renderer Lod(GameObject src, string nm)
                {
                    if (!src) return null;
                    var mf = src.GetComponentInChildren<MeshFilter>(); var mr = src.GetComponentInChildren<MeshRenderer>();
                    if (!mf || !mr) return null;
                    var c = new GameObject(nm); c.transform.SetParent(go.transform, false);
                    c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; var r = c.AddComponent<MeshRenderer>(); r.sharedMaterials = mr.sharedMaterials; return r;
                }
                var r0 = Lod(p, n + "_LOD0"); var r1 = Lod(p1, n + "_LOD1");
                if (!r0) { Object.DestroyImmediate(go); continue; }
                var lg = go.AddComponent<LODGroup>();
                lg.SetLODs(r1 ? new[] { new LOD(0.22f, new[] { r0 }), new LOD(0.035f, new[] { r1 }) } : new[] { new LOD(0.035f, new[] { r0 }) });
                lg.RecalculateBounds();
                var cap = go.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, 2, 0); cap.height = 4; cap.radius = 0.45f;
                var tp = $"{Root}/Resources/Prefabs/World/Arbres/{n}_Arbre.prefab";
                var pf = PrefabUtility.SaveAsPrefabAsset(go, tp); Object.DestroyImmediate(go);
                protos.Add(new TreePrototype { prefab = pf });
            }
            if (protos.Count == 0) return;
            td.treePrototypes = protos.ToArray();
            var inst = new List<TreeInstance>();
            var rng = new System.Random(3);
            int tries = 0;
            while (inst.Count < 3300 && tries++ < 60000)
            {
                float x = (float)rng.NextDouble() * Size - Size / 2, z = (float)rng.NextDouble() * Size - Size / 2;
                var p = new Vector2(x, z);
                if (p.magnitude < TownBuilder.WallR + 10 || RoadDist(p) < 7 || RiverDist(p) < 15 || x > 235 || NearHamlet(p, 22) || (p - Grave).magnitude < 28f) continue;
                float h = Height(x, z);
                if (h > 85 || h < WaterLevel + 0.8f) continue;
                float dens = 0.03f + 0.95f * Smooth(95, 160, z) + 0.4f * Smooth(-130, -190, x) * Smooth(70, 40, h) - 0.9f * Smooth(-160, -200, z);
                dens *= 0.5f + Mathf.PerlinNoise(x * 0.02f, z * 0.02f);
                dens *= Smooth(0.38f, 0.52f, Mathf.PerlinNoise(x * 0.011f + 40f, z * 0.011f + 40f));          // clairières : de grands espaces dégagés dans les bois
                if (RoadDist(p) < 16) dens *= 0.35f;                                                               // lisières dégagées le long des routes
                if (rng.NextDouble() > dens) continue;
                bool pine = h > 30 || z > 170 || rng.NextDouble() < 0.3;
                int idx = pine ? 3 + rng.Next(3) : rng.Next(3);
                if (idx >= protos.Count) idx = rng.Next(protos.Count);
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                inst.Add(new TreeInstance
                {
                    prototypeIndex = idx, position = new Vector3((x + Size / 2) / Size, 0, (z + Size / 2) / Size),
                    widthScale = s, heightScale = s * (0.9f + (float)rng.NextDouble() * 0.25f), rotation = (float)rng.NextDouble() * Mathf.PI * 2,
                    color = Color.Lerp(Color.white, new Color(0.8f, 0.85f, 0.7f), (float)rng.NextDouble()), lightmapColor = Color.white
                });
            }
            td.SetTreeInstances(inst.ToArray(), true);
        }

        // ------------------------------------------------------------------------------------------ matériaux simples
        public static Material WaterMaterial()
        {
            var path = Root + "/Rendering/Eau.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/Terrain/Eau_Normal.png");
            // shader d'eau maison (vagues animées, Fresnel, reflet du soleil, profondeur et écume) ; repli URP Lit
            var eau = Shader.Find("Korvalune/Eau");
            if (eau != null && !ShaderUtil.ShaderHasError(eau))
            {
                if (!m) { m = new Material(eau); AssetDatabase.CreateAsset(m, path); } else if (m.shader != eau) m.shader = eau;
                if (nrm) m.SetTexture("_BumpMap", nrm);
                // eau claire au bord (on voit le fond), vert-bleu profond au large ; écume : fin liseré cassé
                m.SetColor("_ShallowColor", new Color(0.20f, 0.34f, 0.32f, 0.35f)); m.SetColor("_DeepColor", new Color(0.03f, 0.10f, 0.14f, 0.95f));
                m.SetColor("_FoamColor", new Color(0.78f, 0.84f, 0.82f, 1f));
                m.SetFloat("_Tiling", 0.13f); m.SetFloat("_Speed", 0.03f); m.SetFloat("_BumpScale", 0.75f); m.SetFloat("_Smoothness", 0.93f);
                m.SetFloat("_DepthDistance", 3.2f); m.SetFloat("_FoamDistance", 0.18f);
                m.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(m);
                return m;
            }
            if (!m || m.shader.name != "Universal Render Pipeline/Lit") { if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); } else m.shader = Shader.Find("Universal Render Pipeline/Lit"); }
            m.SetColor("_BaseColor", new Color(0.12f, 0.28f, 0.34f, 0.78f)); m.SetFloat("_Smoothness", 0.93f); m.SetFloat("_Metallic", 0.1f);
            if (nrm) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetTextureScale("_BumpMap", new Vector2(40, 40)); }
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent; m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Pierre tombale : profil 2D extrudé (stèle cintrée, croix, stèle brisée, fronton), enregistré une fois.</summary>
        static Mesh TombMesh(int variant)
        {
            var path = WorldArt + "/Tombes/Tombe_" + variant + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            ModelPipeline.EnsureFolder(WorldArt + "/Tombes");
            var pr = new List<Vector2>(); float w = 0.36f, h = 1.15f;
            switch (variant)
            {
                case 0: pr.Add(new Vector2(-w, 0)); pr.Add(new Vector2(w, 0)); pr.Add(new Vector2(w, h - w)); for (int i = 1; i < 12; i++) { float a = Mathf.PI * i / 12f; pr.Add(new Vector2(Mathf.Cos(a) * w, h - w + Mathf.Sin(a) * w)); } pr.Add(new Vector2(-w, h - w)); break;
                case 1: { float t = 0.11f, ah = 0.95f, aw = 0.36f; h = 1.45f; pr.AddRange(new[] { new Vector2(-t, 0), new Vector2(t, 0), new Vector2(t, ah - t), new Vector2(aw, ah - t), new Vector2(aw, ah + t), new Vector2(t, ah + t), new Vector2(t, h), new Vector2(-t, h), new Vector2(-t, ah + t), new Vector2(-aw, ah + t), new Vector2(-aw, ah - t), new Vector2(-t, ah - t) }); break; }
                case 2: { h = 0.8f; pr.Add(new Vector2(-w, 0)); pr.Add(new Vector2(w, 0)); pr.Add(new Vector2(w, h)); var jr = new System.Random(3); for (int i = 1; i < 7; i++) pr.Add(new Vector2(w - 2 * w * i / 7f, h + (float)jr.NextDouble() * 0.22f - 0.05f + (i == 3 ? 0.18f : 0))); pr.Add(new Vector2(-w, h - 0.12f)); break; }
                default: { w = 0.42f; h = 1.0f; pr.AddRange(new[] { new Vector2(-w, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(w * 0.55f, h), new Vector2(0, h + 0.28f), new Vector2(-w * 0.55f, h), new Vector2(-w, h) }); break; }
            }
            float d = variant == 1 ? 0.11f : 0.13f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var c = Vector2.zero; foreach (var p in pr) c += p; c /= pr.Count;
            foreach (var z in new[] { -d, d })      // faces avant et arrière (éventail depuis le centre)
            {
                int b = v.Count; var nz = new Vector3(0, 0, Mathf.Sign(z));
                v.Add(new Vector3(c.x, c.y, z)); n.Add(nz); uv.Add(c * 0.9f);
                foreach (var p in pr) { v.Add(new Vector3(p.x, p.y, z)); n.Add(nz); uv.Add(p * 0.9f); }
                for (int i = 0; i < pr.Count; i++) { int a = b + 1 + i, e = b + 1 + (i + 1) % pr.Count; if (z > 0) { tri.Add(b); tri.Add(a); tri.Add(e); } else { tri.Add(b); tri.Add(e); tri.Add(a); } }
            }
            float run = 0;
            for (int i = 0; i < pr.Count; i++)      // tranche
            {
                var p0 = pr[i]; var p1 = pr[(i + 1) % pr.Count]; var e2 = (p1 - p0); float len = e2.magnitude; var nn = new Vector3(e2.y, -e2.x, 0).normalized;
                int b = v.Count;
                v.Add(new Vector3(p0.x, p0.y, -d)); v.Add(new Vector3(p1.x, p1.y, -d)); v.Add(new Vector3(p1.x, p1.y, d)); v.Add(new Vector3(p0.x, p0.y, d));
                for (int k = 0; k < 4; k++) n.Add(nn);
                uv.Add(new Vector2(run, 0)); uv.Add(new Vector2(run + len, 0)); uv.Add(new Vector2(run + len, 2 * d)); uv.Add(new Vector2(run, 2 * d)); run += len;
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);
            }
            var m = new Mesh { name = "Tombe_" + variant }; m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            // orientation des triangles de tranche : on vérifie avec la normale et on retourne si besoin
            var tris = m.triangles; var verts = m.vertices; var norms = m.normals;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var fn = Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]);
                if (Vector3.Dot(fn, norms[tris[i]]) < 0) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            m.triangles = tris; m.RecalculateTangents(); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Matériau de bâtiment (shader du monde) d'après un jeu de textures du château : pierre de taille, ardoises.</summary>
        public static Material GateMaterial(string name, string texBase, float tiling)
        {
            var path = Root + "/Rendering/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Korvalune/PBR Monde"); if (sh == null || ShaderUtil.ShaderHasError(sh)) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else if (m.shader != sh) m.shader = sh;
            var b = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/" + texBase + "_BaseMap.png"); if (!b) return null;
            m.SetTexture("_BaseMap", b); m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); m.SetColor("_BaseColor", Color.white);
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/" + texBase + "_Normal.png"); if (nm) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
            var oc = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/" + texBase + "_Occlusion.png"); if (oc) { m.SetTexture("_OcclusionMap", oc); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.SetFloat("_Smoothness", 0.2f); EditorUtility.SetDirty(m);
            return m;
        }

        static Material stone;
        public static Material StoneMaterial()
        {
            if (stone) return stone;
            var path = Root + "/Rendering/PierreTombale.mat";
            stone = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!stone) { stone = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(stone, path); }
            var monde = Shader.Find("Korvalune/PBR Monde");
            if (monde != null && !ShaderUtil.ShaderHasError(monde) && stone.shader != monde) stone.shader = monde;   // mousse sur le dessus, variation
            // granit des rochers (grain fin, lichens) : la texture de dalles du terrain faisait de grandes plaques sur les murs
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/Rocher0/Textures/W_Rock_BaseMap.png");
            if (tex) { stone.SetTexture("_BaseMap", tex); stone.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); }
            var rn = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/Rocher0/Textures/W_Rock_Normal.png");
            if (rn) { stone.SetTexture("_BumpMap", rn); stone.EnableKeyword("_NORMALMAP"); }
            var ro = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldArt + "/Rocher0/Textures/W_Rock_Occlusion.png");
            if (ro) { stone.SetTexture("_OcclusionMap", ro); stone.EnableKeyword("_OCCLUSIONMAP"); }
            EditorUtility.SetDirty(stone);
            stone.SetColor("_BaseColor", new Color(0.75f, 0.74f, 0.72f)); stone.SetFloat("_Smoothness", 0.15f);
            return stone;
        }

        static void BridgeObject()
        {
            var b = new GameObject("Pont de l'ouest");
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); deck.name = "Tablier"; deck.transform.SetParent(b.transform);
            var a = Ground(Bridge + new Vector2(18, 4)); var c = Ground(Bridge + new Vector2(-18, -4));
            var mid = (a + c) / 2; mid.y = Base + 0.6f;
            deck.transform.position = mid; deck.transform.rotation = Quaternion.LookRotation(c - a, Vector3.up) * Quaternion.Euler(0, 90, 0);
            deck.transform.localScale = new Vector3((c - a).magnitude + 4, 0.8f, 6f);
            deck.GetComponent<Renderer>().sharedMaterial = StoneMaterial();
            for (int s = -1; s <= 1; s += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube); rail.name = "Parapet"; rail.transform.SetParent(b.transform);
                rail.transform.rotation = deck.transform.rotation; rail.transform.position = mid + deck.transform.forward * s * 2.8f + Vector3.up * 0.8f;
                rail.transform.localScale = new Vector3((c - a).magnitude + 4, 0.9f, 0.4f);
                rail.GetComponent<Renderer>().sharedMaterial = StoneMaterial();
            }
            foreach (var t in b.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        /// <summary>Entrée de donjon : petit mausolée (socle, murs, fronton, toit à deux pans), porte sombre et voûtée, deux
        /// braseros ; tout est posé au sol (pas de trou dans le relief). La façade regarde vers « yaw ».</summary>
        public static GameObject DungeonGate(string name, Vector3 at, float yaw, Color glow)
        {
            var root = new GameObject(name); root.transform.position = at; root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var stone = GateMaterial("Porte_Pierre", "ChateauKorvalune/Textures/W_Stone", 2.5f); var roofMat = GateMaterial("Porte_Toit", "ChateauKorvalune/Textures/W_Roof", 2.5f);
            if (!stone) stone = StoneMaterial(); if (!roofMat) roofMat = stone;
            var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")); dark.SetColor("_BaseColor", new Color(0.015f, 0.01f, 0.02f));
            var dp = Root + "/Rendering/Porte_Sombre.mat"; if (!AssetDatabase.LoadAssetAtPath<Material>(dp)) AssetDatabase.CreateAsset(dark, dp); dark = AssetDatabase.LoadAssetAtPath<Material>(dp);
            GameObject B(string n, Vector3 p, Vector3 sc, Vector3 rot, Material m, bool col = true)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.transform.SetParent(root.transform, false);
                g.transform.localPosition = p; g.transform.localScale = sc; g.transform.localRotation = Quaternion.Euler(rot); g.GetComponent<Renderer>().sharedMaterial = m; g.isStatic = true;
                if (!col) Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
            }
            B("Socle", new Vector3(0, -0.4f, 0.6f), new Vector3(7.2f, 1.2f, 6.6f), Vector3.zero, stone);
            B("Marche", new Vector3(0, -0.15f, -3.1f), new Vector3(3.6f, 0.5f, 1.2f), Vector3.zero, stone);
            B("Mur arrière", new Vector3(0, 2.1f, 3.4f), new Vector3(6.4f, 4.2f, 0.7f), Vector3.zero, stone);
            B("Mur gauche", new Vector3(-3.0f, 2.1f, 0.6f), new Vector3(0.7f, 4.2f, 6.2f), Vector3.zero, stone);
            B("Mur droit", new Vector3(3.0f, 2.1f, 0.6f), new Vector3(0.7f, 4.2f, 6.2f), Vector3.zero, stone);
            B("Façade gauche", new Vector3(-2.05f, 2.1f, -2.2f), new Vector3(2.0f, 4.2f, 0.8f), Vector3.zero, stone);
            B("Façade droite", new Vector3(2.05f, 2.1f, -2.2f), new Vector3(2.0f, 4.2f, 0.8f), Vector3.zero, stone);
            B("Linteau", new Vector3(0, 3.7f, -2.2f), new Vector3(2.4f, 1.0f, 0.8f), Vector3.zero, stone);
            B("Fronton", new Vector3(0, 4.55f, -2.25f), new Vector3(4.6f, 0.7f, 0.9f), Vector3.zero, stone);
            B("Toit gauche", new Vector3(-1.75f, 5.0f, 0.6f), new Vector3(4.2f, 0.5f, 6.8f), new Vector3(0, 0, 28), roofMat);
            B("Toit droit", new Vector3(1.75f, 5.0f, 0.6f), new Vector3(4.2f, 0.5f, 6.8f), new Vector3(0, 0, -28), roofMat);
            B("Porte", new Vector3(0, 1.6f, -2.0f), new Vector3(2.1f, 3.2f, 0.1f), Vector3.zero, dark, false);
            B("Seuil sombre", new Vector3(0, 0.22f, 0.4f), new Vector3(5.2f, 0.02f, 4.6f), Vector3.zero, dark, false);
            foreach (var x in new[] { -2.9f, 2.9f })
            {
                B("Brasero", new Vector3(x, 0.7f, -3.4f), new Vector3(0.5f, 1.4f, 0.5f), Vector3.zero, stone);
                var l = new GameObject("Flamme").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(x, 1.8f, -3.4f);
                l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.intensity = 2.2f; l.range = 7f; l.shadows = LightShadows.None; l.gameObject.AddComponent<Flicker>();
            }
            var g2 = new GameObject("Lueur de la porte").AddComponent<Light>(); g2.transform.SetParent(root.transform, false); g2.transform.localPosition = new Vector3(0, 1.4f, -1.4f);
            g2.type = LightType.Point; g2.color = glow; g2.intensity = 2.5f; g2.range = 5f; g2.shadows = LightShadows.None;
            root.transform.position = Ground(at) + Vector3.up * 0.15f;
            return root;
        }

        // ------------------------------------------------------------------------------------------ configuration de jeu
        public static GameConfig Config()
        {
            var path = DataDir + "/GameConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (!cfg) { cfg = ScriptableObject.CreateInstance<GameConfig>(); AssetDatabase.CreateAsset(cfg, path); }
            cfg.catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(DataDir + "/CharacterCatalog.asset");
            GameObject P(string p) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/{p}.prefab");
            string R(string p) => File.Exists($"{Root}/Resources/Prefabs/{p}.prefab") ? "Prefabs/" + p : null;
            cfg.heroRes.Clear();
            foreach (var n in new[] { "KaelDraven", "ElyraNoctis", "Varek", "Morvanna", "Tharion" })
                foreach (var s in new[] { "_M", "_F" }) { var h = R("Characters/" + n + s); if (h != null) cfg.heroRes.Add(h); }
            cfg.defaultHeroRes = R("Characters/KaelDraven_M") ?? cfg.heroRes.FirstOrDefault() ?? R("Characters/BaseMale");
            cfg.enemies.Clear();
            // niveau naturel (dernier nombre) : les caractéristiques valent pour ce niveau puis suivent le niveau réel du monstre
            void E(string id, string label, string prefab, float hp, float dmg, float spd, float range, float cd, float aggro, float xp, float scale = 1, float armor = 0, float hitDelay = 0.45f, int level = 1)
                => cfg.enemies.Add(new GameConfig.EnemyType { id = id, label = label, prefab = P("Enemies/" + prefab), hp = hp, damage = dmg, speed = spd, attackRange = range, attackCooldown = cd, aggro = aggro, xp = xp, scale = scale, armor = armor, hitDelay = hitDelay, level = level });
            E("squelette", "Squelette guerrier", "SqueletteGuerrier", 70, 9, 3.0f, 1.9f, 1.7f, 13, 20, level: 1);
            E("loup", "Loup des Ténèbres", "LoupTenebres", 90, 12, 6.2f, 2.0f, 1.3f, 16, 30, hitDelay: 0.5f, level: 2);
            E("sorcier", "Sorcier corrompu", "SorcierCorrompu", 160, 16, 2.8f, 2.4f, 2.0f, 15, 80, armor: 1, level: 5);
            E("araignee", "Araignée géante", "AraigneeGeante", 220, 18, 4.5f, 2.6f, 1.8f, 14, 90, armor: 2, level: 4);
            E("chevalier", "Chevalier maudit", "ChevalierMaudit", 260, 20, 3.2f, 2.1f, 1.6f, 14, 110, armor: 5, level: 7);
            E("demon", "Démon brute", "DemonBrute", 650, 34, 3.4f, 3.2f, 2.4f, 18, 300, armor: 6, hitDelay: 0.6f, level: 12);
            // créatures des régions lointaines (modèles générés d'après les concepts puis retouchés)
            if (R("Enemies/AbbeNoye") != null) E("abbe", "Abbé noyé", "AbbeNoye", 340, 24, 2.7f, 2.7f, 2.1f, 16, 150, armor: 3, hitDelay: 0.55f, level: 10);
            if (R("Enemies/WendigoGivre") != null) E("wendigo", "Wendigo des glaces", "WendigoGivre", 520, 30, 4.6f, 2.9f, 1.9f, 18, 240, armor: 4, hitDelay: 0.5f, level: 14);
            if (R("Enemies/BasilicEcarlate") != null) E("basilic", "Basilic écarlate", "BasilicEcarlate", 600, 32, 4.2f, 3.0f, 2.0f, 17, 260, armor: 7, hitDelay: 0.55f, level: 15);
            if (R("Enemies/TisseuseBrumes") != null) E("tisseuse", "Tisseuse des brumes", "TisseuseBrumes", 420, 26, 3.6f, 3.2f, 2.2f, 17, 190, armor: 3, hitDelay: 0.55f, level: 12);
            cfg.highPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Root + "/Rendering/Korvalune_URP.asset");
            cfg.mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Root + "/Rendering/Korvalune_URP_Mobile.asset");
            cfg.ultraPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Root + "/Rendering/Korvalune_URP_Ultra.asset");
            cfg.fxAdditive = FxMaterial("FX_Additif", true); cfg.fxAlpha = FxMaterial("FX_Poussiere", false);
            cfg.vfx = VfxBuilder.Build();
            var litPath = Root + "/Rendering/Lit_Objets.mat"; var lit = AssetDatabase.LoadAssetAtPath<Material>(litPath);
            if (!lit) { lit = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(lit, litPath); }
            cfg.litMat = lit;
            // base du décor généré en jeu (brèches) : shader PBR maison du monde + couche de détail (grain et micro-relief)
            var wPath = Root + "/Rendering/Monde_Objets.mat"; var wm = AssetDatabase.LoadAssetAtPath<Material>(wPath);
            var wsh = Shader.Find("Korvalune/PBR Monde");
            if (wsh != null && !ShaderUtil.ShaderHasError(wsh))
            {
                if (!wm) { wm = new Material(wsh); AssetDatabase.CreateAsset(wm, wPath); } else if (wm.shader != wsh) wm.shader = wsh;
                var dAlb = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/World/Detail/Detail_Albedo.png");
                var dNrm = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/World/Detail/Detail_Normal.png");
                if (dAlb && dNrm)
                {
                    wm.SetTexture("_DetailAlbedoMap", dAlb); wm.SetTexture("_DetailNormalMap", dNrm);
                    wm.SetTextureScale("_DetailAlbedoMap", new Vector2(3f, 3f)); wm.SetFloat("_DetailAlbedoMapScale", 1f); wm.SetFloat("_DetailNormalMapScale", 0.5f);
                    wm.EnableKeyword("_DETAIL_MULX2");
                }
                EditorUtility.SetDirty(wm);
                cfg.worldMat = wm;
            }
            else cfg.worldMat = null;
            // matériau translucide (obstacles entre la caméra et le héros) : URP Lit en mode transparent
            var ghPath = Root + "/Rendering/Lit_Translucide.mat"; var gh = AssetDatabase.LoadAssetAtPath<Material>(ghPath);
            if (!gh) { gh = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(gh, ghPath); }
            gh.SetFloat("_Surface", 1); gh.SetFloat("_Blend", 0); gh.SetFloat("_AlphaClip", 0); gh.SetFloat("_ZWrite", 0);
            gh.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); gh.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (gh.HasProperty("_SrcBlendAlpha")) { gh.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); gh.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); }
            gh.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); gh.DisableKeyword("_ALPHATEST_ON"); gh.SetOverrideTag("RenderType", "Transparent");
            gh.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; gh.SetShaderPassEnabled("ShadowCaster", false);
            gh.SetColor("_BaseColor", new Color(1, 1, 1, 0.28f)); EditorUtility.SetDirty(gh);
            cfg.ghostMat = gh;
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        /// <summary>Post-traitement global : bloom doux, tonemapping ACES, étalonnage chaud, vignette, lift/gamma/gain légers.</summary>
        /// <summary>Profil de post-traitement propre à chaque scène (un profil partagé, supprimé puis recréé par une autre scène,
        /// laisserait des références cassées dans les scènes déjà enregistrées).</summary>
        public static void PostProcess(string tag = "Vallee", float exposure = 0.15f)
        {
            var path = DataDir + $"/{tag}_PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, path);
            var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(0.55f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.65f);
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(exposure); ca.contrast.Override(12f); ca.saturation.Override(8f); ca.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.28f); vg.smoothness.Override(0.45f);
            var smh = prof.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(0.95f, 0.97f, 1.05f, 0f)); smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
            EditorUtility.SetDirty(prof);
            var vol = new GameObject("Post-traitement").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof; vol.priority = 1;
        }

        /// <summary>Particules d'ambiance autour de la caméra : pollen et poussières dorées en suspension dans la lumière.</summary>
        static void Ambience(Transform cam)
        {
            var go = new GameObject("Ambiance"); go.transform.SetParent(cam, false); go.transform.localPosition = new Vector3(0, 0, 12);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main; m.loop = true; m.startLifetime = 9f; m.startSpeed = 0.15f; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f); m.maxParticles = 400;
            m.startColor = new Color(1f, 0.92f, 0.7f, 0.55f); m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 18;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30, 12, 26);
            var no = ps.noise; no.enabled = true; no.strength = 0.25f; no.frequency = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = FxMaterial("FX_Additif", true);
        }

        static Material FxMaterial(string name, bool additive)
        {
            var path = $"{Root}/Rendering/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            var tp = $"{Root}/Rendering/FX_Disque.png";
            if (!File.Exists(tp))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f; t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1.6f))); }
                File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
                var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.SaveAndReimport();
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------ captures de contrôle
        /// <summary>Lance la partie hors jeu (héros, ennemis, interface) et rend : vue aérienne, vue de jeu avec interface,
        /// château depuis la route, combat au cimetière.</summary>
        public static void Captures(GameBootstrap game, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("ci-out");
            game.Begin();
            var cam = game.Cam;
            void Pose(GameObject go, string clip, float t)
            {
                var anim = go.GetComponentInChildren<Animator>();
                if (!anim || !anim.runtimeAnimatorController) return;
                var c = anim.runtimeAnimatorController.animationClips.FirstOrDefault(x => x.name == clip);
                if (c) c.SampleAnimation(anim.gameObject, t);
            }
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) Pose(h.gameObject, "Idle", 0.3f);
            // rendu logiciel (CI sans GPU) : arbres et détails à courte distance, définition réduite
            var ter = Terrain.activeTerrain;
            if (ter) { ter.treeDistance = 160; ter.treeBillboardDistance = 50; ter.heightmapPixelError = 16; ter.basemapDistance = 80; ter.detailObjectDistance = 0; }
            void Shot(string name, int w = 1280, int hgt = 720)
            {
                game.Hud.Refresh();
                Canvas.ForceUpdateCanvases();
                CI.Step("vallée : rendu " + name);
                var rt = new RenderTexture(w, hgt, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;
                // l'interface (canevas en espace caméra) se recale sur la taille de la capture avant le rendu
                Canvas.ForceUpdateCanvases(); game.Hud.Refresh(); Canvas.ForceUpdateCanvases();
                cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(w, hgt, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, hgt), 0, 0); tex.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"ci-out/Jeu_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                CI.Step("vallée : capture " + name);
            }
            try
            {
                var hud = game.Hud.Canvas;
                var p = game.Player.transform;
                // 1. vue de jeu (caméra du jeu, interface visible)
                cam.GetComponent<CameraRig>().Snap(); Shot("1_vue_de_jeu");
                // 2. château depuis la route
                hud.enabled = false;
                cam.transform.position = Ground(new Vector2(-25, -110)) + new Vector3(0, 6, 0); cam.transform.LookAt(new Vector3(0, 22, 0)); Shot("2_chateau_depuis_la_route");
                // 3. vue aérienne de la vallée
                cam.transform.position = new Vector3(60, 230, -300); cam.transform.LookAt(new Vector3(-20, 0, 20)); Shot("3_vue_aerienne");
                // 4. combat au cimetière : héros au milieu des squelettes, interface visible
                hud.enabled = true;
                var cc = p.GetComponent<CharacterController>(); cc.enabled = false; p.position = Ground(Grave + new Vector2(0, 2)); cc.enabled = true;
                p.rotation = Quaternion.LookRotation(new Vector3(0, 0, -1));
                Pose(game.Player.gameObject, "Attack", 0.35f);
                cam.GetComponent<CameraRig>().Snap(); Shot("4_combat_cimetiere");
                // 4b. la ville : porte sud, rue principale, place du marché, échoppes, château depuis la place
                hud.enabled = false;
                cam.transform.position = Ground(new Vector2(-6, -112)) + new Vector3(0, 9, 0); cam.transform.LookAt(Ground(new Vector2(0, -70)) + Vector3.up * 6); Shot("8_ville_porte_sud");
                cam.transform.position = Ground(TownBuilder.Plaza + new Vector2(-16, -22)) + new Vector3(0, 22, 0); cam.transform.LookAt(Ground(TownBuilder.Plaza) + Vector3.up * 1.5f); Shot("9_place_du_marche");
                cam.transform.position = Ground(TownBuilder.Plaza + new Vector2(2, -6)) + new Vector3(0, 3.2f, 0); cam.transform.LookAt(new Vector3(0, 14, 0)); Shot("10_chateau_depuis_la_place");
                cam.transform.position = new Vector3(70, 120, -170); cam.transform.LookAt(new Vector3(0, 0, -30)); Shot("11_ville_vue_aerienne");
                foreach (var m in Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None).Take(9))
                {
                    var t = m.transform; var front = t.parent ? t.parent.TransformPoint(new Vector3(0, 0, -9f)) : t.position - t.forward * 9f;
                    cam.transform.position = front + Vector3.up * 7.5f; cam.transform.LookAt(t.position + Vector3.up * 1.2f);
                    foreach (var rf in Object.FindObjectsByType<RoofFade>(FindObjectsSortMode.None)) foreach (var r in rf.GetComponentsInChildren<Renderer>()) r.enabled = (rf.transform.position - t.position).sqrMagnitude > 64;
                    Shot("12_echoppe_" + m.kind);
                }
                foreach (var rf in Object.FindObjectsByType<RoofFade>(FindObjectsSortMode.None)) foreach (var r in rf.GetComponentsInChildren<Renderer>()) r.enabled = true;
                cam.transform.position = Ground(new Vector2(0, -24)) + new Vector3(0, 7, 0); cam.transform.LookAt(Ground(new Vector2(0, -2)) + Vector3.up * 3); Shot("13_cour_du_chateau");
                TownBuilder.Checks(check);
                // illustrations du jeu : fond de l'écran de lancement et icône de l'application
                try { LauncherBuilder.RenderKeyArt(cam); check("Illustration du lanceur et icône", File.Exists(LauncherBuilder.ArtDir + "/Lanceur_Fond.png"), "ci-out/Lanceur_Fond.png, ci-out/Icone.png"); }
                catch (System.Exception ex) { check("Illustration du lanceur et icône", false, ex.Message); }
                // 5. galerie des ennemis vus en jeu
                hud.enabled = false;
                cam.transform.position = Ground(new Vector2(45, 104)) + new Vector3(0, 5, 0); cam.transform.LookAt(Ground(new Vector2(45, 120)) + Vector3.up); Shot("5_loups_lisiere");
                // 6. butin : un objet de chaque rareté au sol (faisceaux et auras)
                var rng = new System.Random(7); var p0 = game.Player.transform.position;
                for (int k = 0; k < 7; k++)
                {
                    var it = Loot.Roll(rng, 40); it.rarity = (Rarity)k; it.name = it.baseName + " (" + Loot.LabelOf(it.rarity) + ")";
                    var at = Ground(p0 + game.Player.transform.right * ((k - 3) * 1.6f) + game.Player.transform.forward * 3f);
                    LootDrop.Spawn(it, at);
                }
                cam.transform.position = p0 - game.Player.transform.forward * 4f + Vector3.up * 5.5f; cam.transform.LookAt(p0 + game.Player.transform.forward * 3f);
                Shot("6_butin_raretes");
                // 7. inventaire : sac rempli, quelques objets portés, fiche d'un légendaire
                var pc = game.Player; var eq = pc.GetComponent<Equipment>();
                pc.level = 60; pc.skillPoints = 12; Loot.PlayerLevel = 60;          // héros avancé : objets portables, arbre bien rempli
                pc.inventory.Clear();
                for (int k = 0; k < 26; k++) { var it = Loot.Roll(rng, 45, k % 5 == 0 ? 6 : 1); pc.inventory.Add(it); }
                foreach (var it in pc.inventory.Where(x => x.rarity >= Rarity.Rare).GroupBy(x => x.slot).Select(g => g.First()).ToList()) eq.Equip(it);
                game.Hud.ToggleBag();
                Shot("7_inventaire");
                game.Hud.ToggleBag();
                // 14-16. arbre de compétences, menu pause et options
                var rn = game.Player.GetComponent<SkillRunner>();
                if (rn)
                {
                    var tr = SkillBook.TreeOf(rn.classId);
                    for (int i = 0; i < rn.book.Count; i++) if (rn.book[i].unlockLevel <= 60 && (tr.parent[i] < 0 || rn.ranks[tr.parent[i]] > 0) && i % 3 != 2) rn.ranks[i] = 1 + i % 4;
                }
                // 17. le héros en selle : chaque monture, pour vérifier la posture et l'assise
                try
                {
                    var rider = p.GetComponent<Rider>(); if (!rider) rider = p.gameObject.AddComponent<Rider>();
                    cc.enabled = false; p.position = Ground(new Vector2(-10, -90)); cc.enabled = true; p.rotation = Quaternion.Euler(0, 60, 0);
                    var heroAnim = game.Player.Anim;
                    bool hasRide = heroAnim && heroAnim.runtimeAnimatorController && heroAnim.runtimeAnimatorController.animationClips.Any(x => x.name == "Sk_Monte");
                    string where = heroAnim ? (heroAnim.transform == p ? "Animator sur la racine" : "Animator sur " + heroAnim.name) : "pas d'Animator";
                    foreach (var md in Mounts.All)
                    {
                        rider.Mount(md);
                        if (heroAnim && hasRide) heroAnim.runtimeAnimatorController.animationClips.First(x => x.name == "Sk_Monte").SampleAnimation(heroAnim.gameObject, 0.2f);
                        var ma = p.Find("Monture") ? p.Find("Monture").GetComponentInChildren<Animator>() : null;
                        if (ma && ma.runtimeAnimatorController) { var ic = ma.runtimeAnimatorController.animationClips.FirstOrDefault(x => x.name == "Idle"); if (ic) ic.SampleAnimation(ma.gameObject, 0.2f); }
                        rider.Align();
                        cam.transform.position = p.position + Quaternion.Euler(0, 60, 0) * new Vector3(4.2f, 2.6f, 1.0f); cam.transform.LookAt(p.position + Vector3.up * 1.3f);
                        Shot("17_monture_" + md.id);
                        rider.Dismount(true);
                    }
                    check("Montures", hasRide, "ci-out/Jeu_17_monture_*.png — " + where + (hasRide ? ", posture Sk_Monte" : ", posture Sk_Monte ABSENTE du contrôleur"));
                }
                catch (System.Exception ex) { check("Montures", false, ex.Message); }
                game.Hud.TestShow("arbre"); Shot("14_arbre_competences");
                game.Hud.TestShow("menu"); Shot("15_menu_pause");
                game.Hud.TestShow("options"); Shot("16_options");
                game.Hud.TestShow("");
                check("Butin et inventaire", eq.worn.Count > 0, $"{eq.worn.Count} objets portés, {pc.inventory.Count} dans le sac — {eq.Summary().Replace("<b>", "").Replace("</b>", "").Replace("\n", " ")}");
                check("Captures de la vallée", true, "ci-out/Jeu_*.png");
            }
            catch (System.Exception ex) { check("Captures de la vallée", false, ex.Message); }
        }
    }
}
