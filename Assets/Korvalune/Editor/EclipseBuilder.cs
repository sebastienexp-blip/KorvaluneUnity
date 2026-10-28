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
    /// Cité de l'Éclipse (Refuge Lunaire), à l'est de la carte : une cité perchée sur un plateau de falaises au bord du Golfe
    /// de l'Aurore, des pitons rocheux qui sortent de la mer, un pont de pierre jusqu'au sanctuaire du Cristal de l'Éclipse qui
    /// flotte au-dessus du plus haut piton. Lumière de crépuscule d'éclipse (ciel violet, lune bleue). La cité est une zone sûre
    /// (échoppes du mystique, du joaillier, de l'alchimiste, coffre) ; les terres alentour abritent des monstres de niveau 14+.
    /// On y vient par la route de l'est de la vallée. Repère : X est, Z nord, place de la cité en (20, 0).
    /// </summary>
    public static class EclipseBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Eclipse.unity";
        const string Root = ModelPipeline.Root, Kit = Root + "/Resources/Prefabs/World/Ville";
        const float Size = 400f, MaxH = 120f, Sea = -6f;
        public const float WaterLevel = 4f;
        public static readonly Vector2 City = new Vector2(20, 0);
        public const float CityR = 56f, CityH = 24f;
        public static readonly Vector2 Spire = new Vector2(122, 4);
        public static readonly Vector2 WestGate = new Vector2(City.x - CityR, 0);
        public static readonly Vector2 Arrival = new Vector2(-172, 0);
        /// <summary>Portail vers la Cité, sur la route de l'est de la vallée.</summary>
        public static readonly Vector2 ValleyGate = new Vector2(216, 31);

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        /// <summary>Relief : terres vallonnées à l'ouest, plateau de la cité en surplomb, falaises puis golfe à l'est, pitons.</summary>
        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = 9f + 3f * (Mathf.PerlinNoise(x * 0.018f + 4.2f, z * 0.018f + 1.3f) - 0.5f) * 2f;
            // golfe de l'Aurore à l'est (la côte ondule)
            float coast = 72f + 14f * Mathf.Sin(z * 0.025f);
            h = Mathf.Lerp(h, Sea, Smooth(coast, coast + 22f, x));
            // plateau de la cité (falaise côté mer), rampe vers la porte ouest
            float dc = (p - City).magnitude;
            h = Mathf.Lerp(h, CityH, Smooth(CityR + 8f, CityR - 2f, dc));
            float ramp = Mathf.Abs(z) < 7f && x < City.x - CityR + 4f && x > City.x - CityR - 46f ? Smooth(City.x - CityR - 46f, City.x - CityR + 4f, x) : 0f;
            h = Mathf.Max(h, Mathf.Lerp(9f, CityH, ramp) * (ramp > 0 ? 1 : 0));
            // route ouest (aplanie) jusqu'à l'arrivée
            if (Mathf.Abs(z) < 5f && x < City.x - CityR - 40f) h = Mathf.Lerp(h, 9f, 0.8f);
            return Mathf.Clamp(h, Sea - 2, MaxH - 1);
        }

        public static Vector3 Ground(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        [MenuItem("Korvalune/Construire la Cité de l'Éclipse")]
        public static void BuildMenu() { Build(); }

        public static GameBootstrap Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CI.Step("éclipse : relief");
            // ---- terrain
            var td = new TerrainData { heightmapResolution = 257, alphamapResolution = 256, baseMapResolution = 512 };
            td.size = new Vector3(Size, MaxH, Size);
            int R = td.heightmapResolution; var H = new float[R, R];
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * i / (R - 1), z = -Size / 2 + Size * j / (R - 1);
                H[j, i] = (Height(x, z) - Sea + 2f) / MaxH;
            }
            td.SetHeights(0, 0, H);
            td.terrainLayers = WorldBuilder.Layers();
            Paint(td);
            var tdPath = Root + "/Data/Eclipse_Terrain.asset"; AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-Size / 2, Sea - 2f, -Size / 2);
            var terrain = tgo.GetComponent<Terrain>(); terrain.heightmapPixelError = 5; terrain.basemapDistance = 160; terrain.drawInstanced = true;
            terrain.treeDistance = 400; terrain.treeBillboardDistance = 120;
            var tmat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Rendering/Terrain.mat"); if (tmat) terrain.materialTemplate = tmat;
            Trees(td);
            terrain.Flush();

            CI.Step("éclipse : eau, pitons, cité");
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "Eau"; Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = new Vector3(0, WaterLevel, 0); water.transform.localScale = new Vector3(Size / 10f * 1.8f, 1, Size / 10f * 1.8f);
            water.GetComponent<Renderer>().sharedMaterial = WorldBuilder.WaterMaterial(); water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var rng = new System.Random(21);
            var env = new GameObject("Cité de l'Éclipse").transform;
            Spires(env, rng);
            Bridge(env);
            CityPieces(env, rng);
            var shrine = Shrine(env);

            // ---- lumière d'éclipse
            var sun = new GameObject("Lune").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(0.72f, 0.78f, 1f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(34, -120, 0);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.09f); sky.SetFloat("_AtmosphereThickness", 0.55f); sky.SetColor("_SkyTint", new Color(0.42f, 0.30f, 0.62f)); sky.SetColor("_GroundColor", new Color(0.16f, 0.14f, 0.22f)); sky.SetFloat("_Exposure", 0.75f);
            var skyPath = Root + "/Rendering/Ciel_Eclipse.mat"; AssetDatabase.DeleteAsset(skyPath); AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky; RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.42f, 0.66f); RenderSettings.ambientEquatorColor = new Color(0.32f, 0.30f, 0.40f); RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.14f);
            RenderSettings.fog = false;

            // ---- caméra et jeu
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 700; cam.nearClipPlane = 0.3f; cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); if (!camData) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            WorldBuilder.PostProcess("Eclipse", 0.35f);
            var game = new GameObject("Jeu").AddComponent<GameBootstrap>();
            game.config = WorldBuilder.Config();
            game.zoneName = "Cité de l'Éclipse"; game.questTitle = "Le Refuge Lunaire";
            game.playerSpawn = Ground(City + new Vector2(-14, -4));
            game.castleGate = Ground(City);
            game.safeCenter = Ground(City); game.safeRadius = CityR + 4f;
            game.waterLevel = WaterLevel; game.populate = true; game.metersPerLevel = 12f; game.levelOffset = 13;
            game.noCamps.Add(Ground(Arrival)); game.noCamps.Add(Ground(new Vector2(-110, 70))); game.noCamps.Add(Ground(new Vector2(-100, -80)));
            game.camps.Add(new GameBootstrap.Camp { enemyId = "chevalier", center = Ground(new Vector2(-110, 70)), count = 4, radius = 6 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "sorcier", center = Ground(new Vector2(-100, -80)), count = 2, radius = 4 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = "demon", center = Ground(new Vector2(-150, 120)), count = 1, radius = 2 });
            game.questSteps.Add(new Quest.Objective { text = "Entrer dans la Cité de l'Éclipse", place = Ground(City), radius = 14f });
            game.questSteps.Add(new Quest.Objective { text = "Traverser le pont jusqu'au Cristal de l'Éclipse", place = shrine, radius = 7f });
            game.questSteps.Add(new Quest.Objective { text = "Vaincre les chevaliers maudits des Hautes-Terres", enemyId = "chevalier", count = 4 });
            game.questSteps.Add(new Quest.Objective { text = "Abattre le démon du nord", enemyId = "demon", count = 1 });

            // ---- portail vers la vallée (route de l'ouest)
            var back = new GameObject("Route de la vallée").AddComponent<Portal>();
            back.transform.position = Ground(Arrival + new Vector2(-12, 0));
            back.targetScene = "Korvalune_Vallee"; back.arrival = WorldBuilder.Ground(ValleyGate + new Vector2(-9, -4)); back.label = "Partir"; back.displayName = "Vallée de Korvalune";
            back.glowColor = new Color(0.65f, 0.75f, 1f);
            cam.transform.position = game.playerSpawn + new Vector3(0, 10, -10); cam.transform.LookAt(game.playerSpawn);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            CI.Step("éclipse : scène enregistrée");
            return game;
        }

        /// <summary>Portail de la vallée vers la Cité (bout de la route de l'est), avec deux bornes de pierre et des lanternes.</summary>
        public static void ValleyEntrance()
        {
            var p = WorldBuilder.Ground(ValleyGate);
            var root = new GameObject("Route de la Cité de l'Éclipse").transform; root.position = p;
            foreach (var s in new[] { -1, 1 })
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = "Borne"; b.transform.SetParent(root);
                b.transform.position = p + new Vector3(0, 1.6f, s * 3.2f); b.transform.localScale = new Vector3(0.9f, 3.2f, 0.9f);
                b.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); b.isStatic = true;
                var lan = Put("M_Lanterne", p + new Vector3(0.8f, 0, s * 3.2f), 0, root);
            }
            var door = new GameObject("Vers la Cité de l'Éclipse").AddComponent<Portal>();
            door.transform.SetParent(root); door.transform.position = p;
            door.targetScene = "Korvalune_Eclipse"; door.label = "Voyager"; door.displayName = "Cité de l'Éclipse"; door.highlightRoot = root;
            door.glowColor = new Color(0.65f, 0.75f, 1f);
            door.arrival = Ground(Arrival) + Vector3.up * 0.1f;
        }

        // ------------------------------------------------------------------------------------------ décor
        static GameObject Put(string name, Vector3 pos, float yaw, Transform parent)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/{name}.prefab");
            if (!pf) { Debug.LogWarning("[Éclipse] pièce absente : " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }
        static float Facing(Vector2 from, Vector2 to) { var d = to - from; return Mathf.Atan2(-d.x, -d.y) * Mathf.Rad2Deg; }   // façade (−Z) vers « to »

        static GameObject Rock(Vector3 pos, Vector3 scale, float yaw, Transform parent, int k)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Rocher{k % 3}.prefab");
            if (!pf) return null;
            var r = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            r.transform.position = pos; r.transform.rotation = Quaternion.Euler(0, yaw, 0); r.transform.localScale = scale; r.isStatic = true;
            return r;
        }

        /// <summary>Pitons rocheux qui sortent du golfe (blocs de roche empilés et étirés), le plus haut porte le sanctuaire.</summary>
        static void Spires(Transform env, System.Random rng)
        {
            var spires = new (Vector2 p, float h)[] { (Spire, CityH), (new Vector2(150, 46), 30), (new Vector2(162, -40), 38), (new Vector2(104, -62), 20), (new Vector2(186, 10), 44), (new Vector2(130, 92), 26) };
            int k = 0;
            foreach (var s in spires)
            {
                for (float y = Sea; y < s.h; y += 6f)
                {
                    float w = Mathf.Lerp(9f, 6f, (y - Sea) / (s.h - Sea));
                    Rock(new Vector3(s.p.x + (float)rng.NextDouble() * 2 - 1, y, s.p.y + (float)rng.NextDouble() * 2 - 1), new Vector3(w, 4.5f, w), rng.Next(360), env, k++);
                }
            }
            // plateforme du sanctuaire, au sommet du piton principal
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder); top.name = "Parvis du sanctuaire"; top.transform.SetParent(env);
            top.transform.position = new Vector3(Spire.x, CityH - 0.5f, Spire.y); top.transform.localScale = new Vector3(18, 0.5f, 18);
            top.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); top.isStatic = true;
            Object.DestroyImmediate(top.GetComponent<Collider>()); var mc = top.AddComponent<MeshCollider>(); mc.convex = true;
        }

        /// <summary>Pont de pierre à arches, du bord est du plateau jusqu'au parvis du sanctuaire.</summary>
        static void Bridge(Transform env)
        {
            var a = new Vector3(City.x + CityR - 6f, CityH, City.y); var b = new Vector3(Spire.x - 8f, CityH, Spire.y);
            var mid = (a + b) / 2; var dir = (b - a); float len = dir.magnitude; dir.Normalize();
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); deck.name = "Pont de l'Aurore"; deck.transform.SetParent(env);
            deck.transform.position = mid - Vector3.up * 0.4f; deck.transform.rotation = Quaternion.LookRotation(dir); deck.transform.localScale = new Vector3(6f, 0.8f, len + 2f);
            deck.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); deck.isStatic = true;
            foreach (var s in new[] { -1, 1 })
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube); rail.name = "Parapet"; rail.transform.SetParent(env);
                rail.transform.rotation = deck.transform.rotation; rail.transform.position = mid + Vector3.Cross(Vector3.up, dir) * s * 2.8f + Vector3.up * 0.55f;
                rail.transform.localScale = new Vector3(0.4f, 1.1f, len + 2f); rail.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); rail.isStatic = true;
            }
            int piers = Mathf.Max(2, Mathf.RoundToInt(len / 14f));
            for (int i = 1; i < piers; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)piers);
                var pier = GameObject.CreatePrimitive(PrimitiveType.Cube); pier.name = "Pile"; pier.transform.SetParent(env);
                pier.transform.rotation = deck.transform.rotation; pier.transform.position = new Vector3(p.x, (CityH + Sea) / 2f - 0.6f, p.z);
                pier.transform.localScale = new Vector3(4.5f, CityH - Sea - 0.4f, 3f); pier.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); pier.isStatic = true;
            }
            for (int i = 0; i <= piers; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)piers) + Vector3.Cross(Vector3.up, dir) * 2.6f * (i % 2 == 0 ? 1 : -1);
                var l = Put("M_Lanterne", p, 0, env);
                if (l) Glow(l.transform, new Vector3(0, 2.2f, 0), new Color(0.6f, 0.75f, 1f), 1.6f, 7f);
            }
        }

        /// <summary>Cristal de l'Éclipse : gemme géante qui flotte et tourne au-dessus du sanctuaire, halo bleu.</summary>
        static Vector3 Shrine(Transform env)
        {
            var c = new Vector3(Spire.x, CityH, Spire.y);
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube); crystal.name = "Cristal de l'Éclipse"; crystal.transform.SetParent(env);
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.transform.position = c + Vector3.up * 11f; crystal.transform.rotation = Quaternion.Euler(45, 0, 45); crystal.transform.localScale = new Vector3(3.2f, 7.5f, 3.2f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 1f)); m.SetFloat("_Smoothness", 0.95f); m.SetFloat("_Metallic", 0.2f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.35f, 0.75f, 1.4f) * 2.2f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var mp = Root + "/Rendering/Cristal_Eclipse.mat"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(m, mp);
            crystal.GetComponent<Renderer>().sharedMaterial = m;
            crystal.AddComponent<Floater>().spin = 14f;
            Glow(crystal.transform, Vector3.zero, new Color(0.55f, 0.85f, 1f), 6f, 30f, true);
            // autel et quatre obélisques autour du parvis
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI / 4;
                var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = "Obélisque"; o.transform.SetParent(env);
                o.transform.position = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7.2f + Vector3.up * 2.2f; o.transform.localScale = new Vector3(0.9f, 4.4f, 0.9f);
                o.GetComponent<Renderer>().sharedMaterial = WorldBuilder.StoneMaterial(); o.isStatic = true;
            }
            return c;
        }

        /// <summary>La cité : rempart et tours sur le pourtour (porte à l'ouest, pont à l'est), maisons en anneaux, place centrale
        /// avec fontaine, échoppes (mystique, joaillier, alchimiste) et coffre, lanternes bleues.</summary>
        static void CityPieces(Transform env, System.Random rng)
        {
            var g = new Vector3(City.x, CityH, City.y);
            Put("M_Fontaine", g - Vector3.up * 0.05f, 0, env);
            // tours et courtines sur le bord du plateau (sauf porte ouest et départ du pont à l'est)
            int towers = 14;
            for (int i = 0; i < towers; i++)
            {
                float a = i * Mathf.PI * 2 / towers;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 180)) < 14 || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0)) < 14) continue;
                var p = City + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (CityR - 3f);
                Put("M_TourVille", new Vector3(p.x, CityH - 0.3f, p.y), Facing(p, City), env);
                float a2 = (i + 0.5f) * Mathf.PI * 2 / towers;
                if (Mathf.Abs(Mathf.DeltaAngle(a2 * Mathf.Rad2Deg, 180)) < 14 || Mathf.Abs(Mathf.DeltaAngle(a2 * Mathf.Rad2Deg, 0)) < 14) continue;
                var q = City + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * (CityR - 3f);
                var wall = Put("M_Rempart", new Vector3(q.x, CityH - 0.3f, q.y), Facing(q, City) + 90f, env);
            }
            var gate = Put("M_PorteVille", new Vector3(WestGate.x + 3f, CityH - 0.2f, WestGate.y), 90f, env);
            // maisons : deux anneaux, en laissant libres les rues (est-ouest et nord-sud)
            foreach (var ring in new[] { 24f, 38f })
            {
                int n = ring < 30 ? 9 : 14;
                for (int i = 0; i < n; i++)
                {
                    float deg = i * 360f / n + (ring < 30 ? 10 : 0) + (float)rng.NextDouble() * 6 - 3;
                    bool street = false; foreach (var s in new[] { 0f, 90f, 180f, 270f }) if (Mathf.Abs(Mathf.DeltaAngle(deg, s)) < 11) street = true;
                    if (street) continue;
                    float a = deg * Mathf.Deg2Rad; var p = City + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                    Put($"M_Maison{rng.Next(8)}", new Vector3(p.x, CityH - 0.25f, p.y), Facing(p, City), env);
                }
            }
            // échoppes autour de la place
            var shops = new (string piece, MerchantKind kind, Vector3 vendor, string roof, float deg)[]
            {
                ("S_Mystique", MerchantKind.Mystique, new Vector3(0, 0.12f, 1.3f), "S_Mystique_Toit", 45),
                ("S_Joaillier", MerchantKind.Joaillier, new Vector3(0, 0.12f, 0.6f), "S_Joaillier_Toit", 135),
                ("S_Alchimiste", MerchantKind.Alchimiste, new Vector3(0, 0.12f, 0.4f), "S_Alchimiste_Toit", 225),
            };
            foreach (var s in shops)
            {
                float a = s.deg * Mathf.Deg2Rad; var p = City + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13.5f;
                var go = Put(s.piece, new Vector3(p.x, CityH, p.y), Facing(p, City), env); if (!go) continue;
                var roof = Put(s.roof, go.transform.position, go.transform.eulerAngles.y, go.transform); if (roof) { roof.isStatic = false; roof.AddComponent<RoofFade>().radius = 7f; }
                var v = new GameObject(Merchant.DefaultTitle(s.kind)); v.transform.SetParent(go.transform, false); v.transform.localPosition = s.vendor; v.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var m = v.AddComponent<Merchant>(); m.kind = s.kind; m.title = Merchant.DefaultTitle(s.kind);
            }
            var stash = Put("S_Coffre", new Vector3(City.x + 9f, CityH, City.y - 9f), Facing(City + new Vector2(9, -9), City), env);
            if (stash) { var v = new GameObject("Coffre"); v.transform.SetParent(stash.transform, false); v.transform.localPosition = new Vector3(0, 0, 0.9f); var m = v.AddComponent<Merchant>(); m.kind = MerchantKind.Coffre; m.title = Merchant.DefaultTitle(MerchantKind.Coffre); m.talkRadius = 3.6f; }
            // lanternes bleues le long des rues
            for (int i = 0; i < 16; i++)
            {
                float deg = (i % 4) * 90f + (i < 8 ? 6 : -6); float r = 10f + (i / 4) * 11f;
                float a = deg * Mathf.Deg2Rad; var p = City + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var l = Put("M_Lanterne", new Vector3(p.x, CityH, p.y), 0, env);
                if (l && i % 2 == 0) Glow(l.transform, new Vector3(0, 2.2f, 0), new Color(0.55f, 0.7f, 1f), 1.5f, 8f);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2; var p = City + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (16f + (float)rng.NextDouble() * 4f);
                Put(new[] { "M_Tonneaux", "M_Caisses", "M_Banc", "M_Etal0", "M_Etal2" }[rng.Next(5)], new Vector3(p.x, CityH, p.y), Facing(p, City), env);
            }
        }

        static void Glow(Transform parent, Vector3 local, Color c, float intensity, float range, bool sparks = false)
        {
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(parent, false); l.transform.localPosition = local;
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (!sparks) return;
            var ps = l.gameObject.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 3f; m.startSpeed = 0.4f; m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); m.maxParticles = 150;
            m.startColor = c; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.04f;
            var em = ps.emission; em.rateOverTime = 30; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 3.5f;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldBuilder.Config().fxAdditive;
        }

        // ------------------------------------------------------------------------------------------ terrain
        /// <summary>Couches : herbe sur les terres, pavés sur le plateau et la route, roche sur les falaises, sable sur la côte.</summary>
        static void Paint(TerrainData td)
        {
            int R = td.alphamapResolution, L = td.terrainLayers.Length; var a = new float[R, R, L];
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * i / (R - 1), z = -Size / 2 + Size * j / (R - 1); var p = new Vector2(x, z);
                float h = Height(x, z), hx = Height(x + 2, z), hz = Height(x, z + 2);
                float slope = Mathf.Max(Mathf.Abs(hx - h), Mathf.Abs(hz - h)) / 2f;
                var w = new float[L];
                w[0] = 1f;                                                             // herbe
                if (L > 4) w[4] = 0.4f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f);     // sous-bois
                float city = Smooth(CityR - 1, CityR - 6, (p - City).magnitude);
                bool road = Mathf.Abs(z) < 4.5f && x < City.x;
                if (L > 2) w[2] = Mathf.Max(city * 3f, road ? 2.5f : 0);             // pavés
                if (L > 3) w[3] = Smooth(0.45f, 1.1f, slope) * 6f;                    // roche
                if (L > 5) w[5] = Smooth(WaterLevel + 2.5f, WaterLevel + 0.5f, h) * 4f; // sable
                float sum = 0; foreach (var v in w) sum += v;
                for (int k = 0; k < L; k++) a[j, i, k] = w[k] / sum;
            }
            td.SetAlphamaps(0, 0, a);
        }

        /// <summary>Pins et chênes sur les terres de l'ouest (prototypes de la vallée).</summary>
        static void Trees(TerrainData td)
        {
            var protos = new List<TreePrototype>();
            foreach (var n in new[] { "Pin0", "Pin1", "Pin2", "Chene0", "Chene1" })
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Arbres/{n}_Arbre.prefab");
                if (pf) protos.Add(new TreePrototype { prefab = pf });
            }
            if (protos.Count == 0) return;
            td.treePrototypes = protos.ToArray();
            var rng = new System.Random(8); var inst = new List<TreeInstance>();
            for (int t = 0; t < 9000 && inst.Count < 900; t++)
            {
                float x = (float)rng.NextDouble() * Size - Size / 2, z = (float)rng.NextDouble() * Size - Size / 2; var p = new Vector2(x, z);
                if ((p - City).magnitude < CityR + 14 || x > 60 || Mathf.Abs(z) < 9f || (p - Arrival).magnitude < 16) continue;
                float h = Height(x, z); if (h < WaterLevel + 1.5f) continue;
                if (rng.NextDouble() > 0.25 + 0.6 * Mathf.PerlinNoise(x * 0.02f + 7, z * 0.02f)) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                inst.Add(new TreeInstance { prototypeIndex = rng.Next(protos.Count), position = new Vector3((x + Size / 2) / Size, 0, (z + Size / 2) / Size), widthScale = s, heightScale = s, rotation = (float)rng.NextDouble() * 6.28f, color = Color.white, lightmapColor = Color.white });
            }
            td.SetTreeInstances(inst.ToArray(), true);
        }

        // ------------------------------------------------------------------------------------------ captures
        public static void Captures(GameBootstrap game, System.Action<string, bool, string> check)
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
                    File.WriteAllBytes($"ci-out/Eclipse_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
                    CI.Step("éclipse : capture " + name);
                }
                cam.GetComponent<CameraRig>().Snap(); Shot("1_place");
                cam.transform.position = Ground(City) + new Vector3(-70, 45, -60); cam.transform.LookAt(new Vector3(Spire.x - 30, CityH, Spire.y)); Shot("2_vue_cite_et_cristal");
                cam.transform.position = new Vector3(City.x + CityR, CityH + 4, City.y - 6); cam.transform.LookAt(new Vector3(Spire.x, CityH + 10, Spire.y)); Shot("3_pont_et_sanctuaire");
                cam.transform.position = Ground(Arrival) + new Vector3(-10, 12, -14); cam.transform.LookAt(Ground(City)); Shot("4_arrivee_route");
                // praticabilité : chemin de l'arrivée à la place puis au sanctuaire
                Physics.SyncTransforms();
                int blocked = 0; string where = "";
                var path = new List<Vector3> { Ground(Arrival), Ground(WestGate + new Vector2(-30, 0)), Ground(WestGate + new Vector2(6, 0)), Ground(City + new Vector2(-6, 0)), new Vector3(City.x + CityR - 4, CityH, 0), new Vector3(Spire.x - 6, CityH, Spire.y) };
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var a = path[i] + Vector3.up * 1.4f; var b = path[i + 1] + Vector3.up * 1.4f;
                    if (Physics.Raycast(a, (b - a).normalized, out var h, (b - a).magnitude) && !h.collider.isTrigger && h.collider.GetComponentInParent<Health>() == null) { blocked++; if (where == "") where = $"tronçon {i} ({h.collider.name})"; }
                }
                check("Cité : chemin libre jusqu'au sanctuaire", blocked == 0, blocked == 0 ? "arrivée → porte ouest → place → pont → cristal" : where);
            }
            catch (System.Exception ex) { check("Captures de la Cité", false, ex.Message); }
        }
    }
}
