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
    /// Régions extérieures construites en SEGMENTS séparés (une scène chacune) : seule la région où se trouve le héros est
    /// en mémoire. On y va par les portails de la ville (un par région) ou la pierre de voyage ; les trois premières sont
    /// aussi reliées aux bouts des routes de la vallée. Chaque région couvre une tranche de niveaux, jusqu'au niveau 200 :
    ///   Forêt de l'Astral 8 → Désert des Étoiles 25 → Pics Sélénites 45 → Côte des Naufrages 65 → Marais de Brumenoire 85
    ///   → Toundra de Givrecœur 105 → Terres de Cendrefer 125 → Canyon Écarlate 150 → Abîme du Néant 175 (jusqu'à 200).
    /// Repère : X est, Z nord, terrain de 360 m centré sur l'origine, entrée au sud (ou à l'est pour les Pics, au nord
    /// pour le Désert), le niveau des monstres monte en s'éloignant de l'entrée.
    /// </summary>
    public static class SegmentBuilder
    {
        const string Root = ModelPipeline.Root;
        const float Size = 360f, MaxH = 110f, Floor = -6f, MetersPerLevel = 13f;

        public enum Kind { Foret, Desert, Pics, Cote, Marais, Toundra, Volcan, Canyon, Abime }

        public class Def
        {
            public Kind kind; public string scene, name, tag, quest; public int level;
            public Vector2 entry;                       // arrivée dans le segment
            public Vector2? valleyGate;                 // portail au bout d'une route de la vallée (sinon : portails de la ville seulement)
            public string[] enemies;
            public string ScenePath => $"{Root}/Scenes/{scene}.unity";
            public int levelOffset => level - 1;
        }

        static readonly Vector2 South = new Vector2(0, -150);
        public static readonly Def[] All =
        {
            new Def { kind = Kind.Foret,   scene = "Korvalune_Foret",   name = "Forêt de l'Astral",     tag = "Foret",   quest = "Les bois de l'Astral",       level = 8,
                      entry = South, valleyGate = new Vector2(20, 262), enemies = new[] { "loup", "araignee", "squelette" } },
            new Def { kind = Kind.Desert,  scene = "Korvalune_Desert",  name = "Désert des Étoiles",    tag = "Desert",  quest = "Sous les étoiles mortes",    level = 25,
                      entry = new Vector2(0, 150), valleyGate = new Vector2(-60, -262), enemies = new[] { "demon", "squelette", "sorcier" } },
            new Def { kind = Kind.Pics,    scene = "Korvalune_Pics",    name = "Pics Sélénites",        tag = "Pics",    quest = "Les cols sélénites",         level = 45,
                      entry = new Vector2(150, 0), valleyGate = new Vector2(-212, 10), enemies = new[] { "chevalier", "sorcier", "loup" } },
            new Def { kind = Kind.Cote,    scene = "Korvalune_Cote",    name = "Côte des Naufrages",    tag = "Cote",    quest = "Les épaves de la Mer des Tempêtes", level = 65,
                      entry = South, enemies = new[] { "squelette", "abbe", "araignee" } },
            new Def { kind = Kind.Marais,  scene = "Korvalune_Marais",  name = "Marais de Brumenoire",  tag = "Marais",  quest = "Ce qui dort sous la vase",   level = 85,
                      entry = South, enemies = new[] { "araignee", "sorcier", "squelette" } },
            new Def { kind = Kind.Toundra, scene = "Korvalune_Toundra", name = "Toundra de Givrecœur",  tag = "Toundra", quest = "Le cœur de givre",           level = 105,
                      entry = South, enemies = new[] { "loup", "wendigo", "sorcier" } },
            new Def { kind = Kind.Volcan,  scene = "Korvalune_Volcan",  name = "Terres de Cendrefer",   tag = "Volcan",  quest = "Les forges de Cendrefer",    level = 125,
                      entry = South, enemies = new[] { "demon", "chevalier", "squelette" } },
            new Def { kind = Kind.Canyon,  scene = "Korvalune_Canyon",  name = "Canyon Écarlate",       tag = "Canyon",  quest = "Le sang de la pierre",       level = 150,
                      entry = South, enemies = new[] { "basilic", "demon", "sorcier" } },
            new Def { kind = Kind.Abime,   scene = "Korvalune_Abime",   name = "Abîme du Néant",        tag = "Abime",   quest = "Au bord du Néant",           level = 175,
                      entry = South, enemies = new[] { "demon", "sorcier", "chevalier" } },
        };

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float N(float x, float z, float f, float o) => Mathf.PerlinNoise(x * f + o + 1000f, z * f + o * 0.7f + 1000f);

        /// <summary>Niveau de l'eau (mer, marais), de la glace, de la lave ou du vide ; NaN si la région n'en a pas.</summary>
        static float Liquid(Def d) => d.kind switch
        {
            Kind.Cote => 0f, Kind.Marais => 0.4f, Kind.Toundra => 4.5f, Kind.Volcan => 2.5f, Kind.Abime => -0.9f, _ => float.NaN,
        };

        /// <summary>Tracé du chemin principal (de l'entrée vers le fond de la région).</summary>
        static Vector2[] Path(Def d) => d.kind switch
        {
            Kind.Desert => new[] { d.entry, new Vector2(25, 90), new Vector2(-15, 20), new Vector2(30, -60), new Vector2(0, -140) },
            Kind.Pics => new[] { d.entry, new Vector2(90, 15), new Vector2(20, -10), new Vector2(-50, 20), new Vector2(-140, 0) },
            Kind.Cote => new[] { d.entry, new Vector2(-30, -80), new Vector2(25, -20), new Vector2(-25, 30), new Vector2(50, 60) },
            Kind.Canyon => new[] { d.entry, new Vector2(30, -90), new Vector2(-20, -30), new Vector2(25, 40), new Vector2(-10, 140) },
            Kind.Volcan => new[] { d.entry, new Vector2(-35, -85), new Vector2(20, -25), new Vector2(-15, 35), new Vector2(0, 85) },
            _ => new[] { d.entry, new Vector2(-20, -90), new Vector2(15, -20), new Vector2(-10, 60), new Vector2(20, 140) },
        };

        /// <summary>Chef de la bande du hameau : le monstre le plus lourd de la région.</summary>
        static bool Has(string prefab) => File.Exists($"{Root}/Resources/Prefabs/Enemies/{prefab}.prefab");
        static string Chief(Def d) => d.kind switch
        {
            Kind.Foret => "chevalier",
            Kind.Cote when Has("AbbeNoye") => "abbe",
            Kind.Toundra when Has("WendigoGivre") => "wendigo",
            Kind.Canyon when Has("BasilicEcarlate") => "basilic",
            _ => "demon",
        };
        /// <summary>Monstre de la liste dont le modèle n'existe pas encore → remplacé par un monstre existant.</summary>
        static string Avail(string id) => id switch { "abbe" when !Has("AbbeNoye") => "chevalier", "wendigo" when !Has("WendigoGivre") => "chevalier", "basilic" when !Has("BasilicEcarlate") => "loup", _ => id };

        /// <summary>Hameau en ruine au bout du chemin principal (un peu en retrait de son extrémité).</summary>
        static Vector2 HamletAt(Def d)
        {
            var l = Path(d); var e = l[l.Length - 1] + (l[l.Length - 1] - l[l.Length - 2]).normalized * 6f;
            return new Vector2(Mathf.Clamp(e.x, -122f, 122f), Mathf.Clamp(e.y, -122f, 122f));      // loin des bords relevés
        }

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

        // ------------------------------------------------------------------------------------------ relief
        /// <summary>Relief propre à chaque région ; le chemin principal et la clairière d'arrivée restent praticables.</summary>
        public static float Height(Def d, float x, float z)
        {
            var p = new Vector2(x, z); float h, target;
            switch (d.kind)
            {
                case Kind.Foret:   // collines boisées, quelques combes
                    h = 8f + 9f * (N(x, z, 0.012f, 3.1f) - 0.5f) * 2f + 3f * (N(x, z, 0.04f, 7.7f) - 0.5f); target = 8f; break;
                case Kind.Desert:  // dunes allongées et mesas
                {
                    h = 6f + 3.5f * Mathf.Sin(x * 0.045f + 2.2f * N(x, z, 0.01f, 5f)) * (0.6f + 0.6f * N(x, z, 0.02f, 9f)) + 2f * (N(x, z, 0.05f, 1.3f) - 0.5f);
                    float mesa = N(x, z, 0.008f, 11f); h = Mathf.Lerp(h, 26f + 4f * N(x, z, 0.05f, 2f), Smooth(0.66f, 0.72f, mesa)); target = 6f; break;
                }
                case Kind.Pics:    // cols entre des arêtes, crêtes enneigées
                {
                    float ridge = 1f - Mathf.Abs(N(x, z, 0.01f, 4.4f) - 0.5f) * 2f;
                    h = 10f + 62f * Mathf.Pow(ridge, 3f) + 18f * N(x, z, 0.025f, 8.8f); target = 12f; break;
                }
                case Kind.Cote:    // landes et falaises au sud, la mer au nord ; plages dans les criques
                {
                    float coast = 85f + 40f * (N(x, z, 0.012f, 6.6f) - 0.5f);
                    float land = 7f + 7f * (N(x, z, 0.015f, 2.2f) - 0.5f) * 2f + 2f * N(x, z, 0.06f, 9.1f);
                    float cliff = N(x, z, 0.02f, 4.4f) > 0.55f ? 1f : 0.35f;                     // falaises ou plages
                    h = Mathf.Lerp(land, -5f, Smooth(coast - 18f * (1.2f - cliff), coast + 6f, z)); target = 6f; break;
                }
                case Kind.Marais:  // terre basse et spongieuse, mares et chenaux
                {
                    float m = N(x, z, 0.02f, 1.7f) * 0.7f + N(x, z, 0.06f, 3.3f) * 0.3f;
                    h = 1.2f + 2.5f * (m - 0.45f) * 2f; if (m < 0.42f) h = Mathf.Lerp(h, -1.2f, Smooth(0.42f, 0.34f, m)); target = 1.6f; break;
                }
                case Kind.Toundra: // plaines vallonnées, lacs gelés, collines
                {
                    h = 11f + 12f * (N(x, z, 0.01f, 5.5f) - 0.4f) + 4f * (N(x, z, 0.035f, 1.9f) - 0.5f);
                    float lake = N(x, z, 0.014f, 8.2f); h = Mathf.Lerp(h, 3.2f, Smooth(0.62f, 0.7f, lake)); target = 10f; break;
                }
                case Kind.Volcan:  // terre déchirée, coulées de lave, le volcan au fond
                {
                    h = 9f + 8f * (N(x, z, 0.018f, 2.8f) - 0.5f) * 2f + 3f * N(x, z, 0.07f, 6.1f);
                    float flow = Mathf.Abs(N(x, z, 0.012f, 7.3f) - 0.5f); h = Mathf.Lerp(1.8f, h, Smooth(0.015f, 0.045f, flow));      // rivières de lave
                    float cone = (p - new Vector2(10, 130)).magnitude; h = Mathf.Max(h, 75f * Smooth(95f, 20f, cone) - (cone < 14f ? 20f * Smooth(14f, 4f, cone) : 0f));
                    target = 9f; break;
                }
                case Kind.Canyon:  // fonds de canyon sinueux entre des mesas de grès rouge
                {
                    float ch = Mathf.Abs(N(x, z, 0.009f, 3.7f) - 0.5f) * 2f;
                    h = 5f + 38f * Smooth(0.18f, 0.32f, ch) + 6f * Smooth(0.6f, 0.7f, ch) + 2f * N(x, z, 0.08f, 1.1f); target = 5f; break;
                }
                default:           // Abîme : plateaux noirs fissurés au-dessus de gouffres de néant
                {
                    h = 8f + 5f * (N(x, z, 0.02f, 9.9f) - 0.5f) * 2f;
                    float chasm = N(x, z, 0.016f, 4.8f); h = Mathf.Lerp(h, -1.6f, Smooth(0.63f, 0.68f, chasm));
                    target = 8f; break;
                }
            }
            // chemin principal et clairière d'arrivée aplanis (désert : piste à peine marquée, pas de tranchée)
            float pd = PathDist(d, p), flat = d.kind == Kind.Pics || d.kind == Kind.Canyon ? 14f : 9f;
            float keep = d.kind == Kind.Desert ? 0.55f : 1f;
            h = Mathf.Lerp(h, target, keep * Smooth(flat + 14f, flat, pd));
            h = Mathf.Lerp(h, target, Smooth(26f, 16f, (p - d.entry).magnitude));
            h = Mathf.Lerp(h, target + 0.5f, Smooth(34f, 22f, (p - HamletAt(d)).magnitude));      // terre-plein du hameau au bout du chemin
            // bords de la carte relevés (on ne sort pas du segment), sauf côté mer
            float edge = d.kind == Kind.Cote ? Mathf.Max(Mathf.Abs(x), -z) : Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h = Mathf.Lerp(h, Mathf.Max(h, 0) + 32f, Smooth(Size / 2 - 22f, Size / 2 - 4f, edge));
            return Mathf.Clamp(h, Floor + 0.5f, MaxH - 2);
        }

        static Vector3 Ground(Def d, Vector2 p) => new Vector3(p.x, Height(d, p.x, p.y), p.y);

        // ------------------------------------------------------------------------------------------ couches de sol
        static readonly Dictionary<string, float> Tile = new Dictionary<string, float>
        {
            { "Herbe", 6 }, { "Chemin", 5 }, { "Pave", 3.5f }, { "Roche", 14 }, { "SousBois", 8 }, { "Sable", 10 },
            { "Neige", 9 }, { "Cendre", 8 }, { "Boue", 7 }, { "GresRouge", 16 }, { "Neant", 12 },
        };

        /// <summary>Couches de sol de chaque région (4 au plus : une seule passe de rendu du terrain).</summary>
        static string[] LayerNames(Def d) => d.kind switch
        {
            Kind.Foret => new[] { "Herbe", "Chemin", "Roche", "SousBois" },
            Kind.Desert => new[] { "Sable", "Chemin", "Roche", "GresRouge" },
            Kind.Pics => new[] { "Herbe", "Chemin", "Roche", "Neige" },
            Kind.Cote => new[] { "Herbe", "Chemin", "Roche", "Sable" },
            Kind.Marais => new[] { "Boue", "Chemin", "SousBois", "Herbe" },
            Kind.Toundra => new[] { "Neige", "Chemin", "Roche", "Herbe" },
            Kind.Volcan => new[] { "Cendre", "Chemin", "Roche", "GresRouge" },
            Kind.Canyon => new[] { "GresRouge", "Chemin", "Roche", "Sable" },
            _ => new[] { "Neant", "Chemin", "Roche", "Cendre" },
        };

        static TerrainLayer[] Layers(Def d)
        {
            var list = new List<TerrainLayer>();
            foreach (var n in LayerNames(d))
            {
                var path = $"{Root}/Data/Couche_{n}.terrainlayer";
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
                l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/{n}_BaseMap.png");
                l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/{n}_Normal.png");
                l.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/{n}_Mask.png");
                float t = Tile.TryGetValue(n, out var tt) ? tt : 8f;
                l.tileSize = new Vector2(t, t); l.normalScale = 1.35f; l.smoothness = 0.1f; l.metallic = 0;
                l.maskMapRemapMin = Vector4.zero; l.maskMapRemapMax = new Vector4(1, 1, 1, 0.4f);   // lissé plafonné : sol sec, pas « après la pluie »
                EditorUtility.SetDirty(l); list.Add(l);
            }
            return list.ToArray();
        }

        /// <summary>Poids des 4 couches (dans l'ordre de LayerNames) en un point.</summary>
        static void Weights(Def d, float x, float z, float slope, float path, float[] w)
        {
            float h = Height(d, x, z), n1 = N(x, z, 0.03f, 1f), n2 = N(x, z, 0.02f, 6f), rock = Smooth(0.2f, 0.36f, slope);
            float liq = Liquid(d);
            switch (d.kind)
            {
                case Kind.Foret: w[0] = 0.5f + 0.6f * n2; w[3] = 0.6f + 0.8f * n1; w[2] = rock * 2; break;
                case Kind.Desert: w[0] = 1.5f; w[2] = Smooth(0.2f, 0.32f, slope) * 2f; w[3] = Smooth(20f, 26f, h) * 2f + Smooth(0.25f, 0.4f, slope) * 1.5f; w[1] = 0.25f * n1; break;
                case Kind.Pics: w[0] = 0.8f * Smooth(30f, 18f, h); w[2] = Smooth(0.15f, 0.3f, slope) * 2.5f + Smooth(22f, 40f, h) * 1.2f; w[3] = Smooth(42f, 54f, h) * (2.5f - rock) ; w[1] = 0.2f; break;
                case Kind.Cote: w[0] = 0.8f + 0.5f * n2; w[3] = Smooth(3.5f, 1.5f, h) * 3f; w[2] = rock * 2.5f; w[1] = 0.15f; break;
                case Kind.Marais: w[0] = 0.6f + 0.8f * Smooth(1.5f, 0.2f, h); w[2] = 0.5f * n1; w[3] = 0.4f * n2 * Smooth(1f, 2.5f, h); break;
                case Kind.Toundra: w[0] = 1.4f; w[2] = rock * 2.2f; w[3] = 0.5f * Smooth(0.6f, 0.75f, N(x, z, 0.05f, 2.4f)); break;
                case Kind.Volcan: w[0] = 1.2f + 0.4f * n1; w[2] = rock * 2f + Smooth(30f, 50f, h); w[3] = 1.5f * Smooth(liq + 2.5f, liq + 0.8f, h); break;   // terre rougie au bord de la lave
                case Kind.Canyon: w[0] = 0.6f + rock * 2f + Smooth(10f, 18f, h); w[3] = 1.2f * Smooth(10f, 6f, h) * (0.6f + 0.6f * n2); w[2] = rock * 0.5f; break;
                default: w[0] = 1.2f; w[2] = rock * 1.5f; w[3] = 0.6f * n2; break;
            }
            w[1] += path * (d.kind == Kind.Desert ? 1.2f : 3f);
        }

        // ------------------------------------------------------------------------------------------ construction
        [MenuItem("Korvalune/Construire les régions (segments)")]
        public static void BuildMenu() { foreach (var d in All) Build(d); }

        public static GameBootstrap Build(Def d)
        {
            PropKit.Ensure();
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
            td.terrainLayers = Layers(d);
            Paint(d, td);
            EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain"; tgo.transform.position = new Vector3(-Size / 2, Floor, -Size / 2);
            var terrain = tgo.GetComponent<Terrain>(); terrain.heightmapPixelError = 5; terrain.basemapDistance = 160; terrain.drawInstanced = true;
            terrain.treeDistance = 400; terrain.treeBillboardDistance = 120;
            var tmat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Rendering/Terrain.mat"); if (tmat) terrain.materialTemplate = tmat;
            Trees(d, td);
            Grass(d, td);
            terrain.Flush(); terrain.detailObjectDistance = 80; terrain.detailObjectDensity = 1f;

            CI.Step($"{d.tag} : décor");
            var env = new GameObject(d.name).transform; var rng = new System.Random(d.scene.GetHashCode() & 0xffff);
            var look = new GameObject("Ambiance").AddComponent<ZoneLook>();
            Liquids(d, look);
            Decor(d, env, rng, look);
            Lighting(d, look);
            Borders(d);

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
            // pas de ville ici : aucune zone sûre (sinon villageois et chiens y apparaissent et on ne peut pas y combattre) ;
            // l'arrivée reste libre de camps (noCamps + centre de génération)
            game.safeCenter = game.playerSpawn; game.safeRadius = 0f;
            float liq = Liquid(d);
            // eau (mer, marais) : on y nage ; lave et néant : on ne nage pas, ils brûlent (ZoneLook) ; la glace se marche
            bool swim = d.kind == Kind.Cote || d.kind == Kind.Marais;
            game.waterLevel = swim ? liq : Floor - 2f;
            game.hazardLevel = d.kind == Kind.Volcan || d.kind == Kind.Abime ? liq : -10000f;
            look.hazardLevel = game.hazardLevel; look.hazardName = d.kind == Kind.Volcan ? "La lave vous brûle !" : "Le Néant vous dévore !";
            game.populate = true; game.metersPerLevel = MetersPerLevel; game.levelOffset = d.levelOffset;
            game.noCamps.Add(game.playerSpawn);
            // camps fixes le long du chemin, plus forts au fond de la région
            var path = Path(d);
            for (int i = 1; i < path.Length; i++)
            {
                var c = path[i] + new Vector2((float)rng.NextDouble() * 30 - 15, (float)rng.NextDouble() * 30 - 15);
                game.camps.Add(new GameBootstrap.Camp { enemyId = Avail(d.enemies[(i - 1) % d.enemies.Length]), center = Ground(d, c), count = 4 + i / 2, radius = 6 });
                var c2 = path[i] + new Vector2((float)rng.NextDouble() * 50 - 25, (float)rng.NextDouble() * 50 - 25);
                game.camps.Add(new GameBootstrap.Camp { enemyId = Avail(d.enemies[i % d.enemies.Length]), center = Ground(d, c2), count = 3 + i / 2, radius = 5 });
            }
            // le hameau en ruine du bout du chemin est tenu par une bande et son chef
            var hc = HamletAt(d);
            game.camps.Add(new GameBootstrap.Camp { enemyId = Avail(d.enemies[0]), center = Ground(d, hc + new Vector2(-6, 4)), count = 6, radius = 8 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = Avail(d.enemies[1]), center = Ground(d, hc + new Vector2(7, -3)), count = 4, radius = 6 });
            game.camps.Add(new GameBootstrap.Camp { enemyId = Chief(d), center = Ground(d, hc), count = 1, radius = 2 });
            game.questSteps.Add(new Quest.Objective { text = $"Explorer : {d.name}", place = Ground(d, path[2]), radius = 14f });
            game.questSteps.Add(new Quest.Objective { text = "Vaincre les créatures de la région", enemyId = Avail(d.enemies[0]), count = 8 });
            game.questSteps.Add(new Quest.Objective { text = "Libérer le hameau en ruine au bout du chemin", place = Ground(d, hc), radius = 14f });

            // ---- portail du retour, à l'entrée : vers le bout de la route de la vallée, ou la ville
            var back = new GameObject("Retour").AddComponent<Portal>();
            back.transform.position = Ground(d, d.entry);
            back.targetScene = "Korvalune_Vallee"; back.label = "Partir"; back.glowColor = new Color(0.65f, 0.85f, 1f);
            if (d.valleyGate.HasValue)
            {
                var g = d.valleyGate.Value; back.displayName = "Vallée de Korvalune";
                back.arrival = WorldBuilder.Ground(g + (Vector2.zero - g).normalized * 10f) + Vector3.up * 0.1f;
            }
            else { back.useSceneSpawn = true; back.gate = true; back.displayName = "Retour en ville"; }
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

        // ------------------------------------------------------------------------------------------ bords
        /// <summary>Bords du segment : murs invisibles (on ne sort pas, on ne tombe pas, même à la nage), et au-delà du
        /// terrain une ceinture de roche haute qui prolonge le relief relevé des bords (on ne voit jamais le vide).</summary>
        static void Borders(Def d)
        {
            var m = BeltMaterial(d.tag, LayerNames(d)[2], RockTint(d) ?? Color.white);
            Enclose(Size / 2, 8f, Floor, 40f, new[] { d.kind == Kind.Cote ? null : m, m, m, m });
        }

        /// <summary>Murs invisibles autour d'une carte carrée centrée (demi-côté « half », « inset » m à l'intérieur du bord) et
        /// ceintures de roche au-delà du terrain (une par côté, null = pas de ceinture, ex. côté mer).</summary>
        public static void Enclose(float half, float inset, float floorY, float beltTop, Material[] belts)
        {
            var root = new GameObject("Bordures").transform;
            for (int k = 0; k < 4; k++)
            {
                var dir = k == 0 ? Vector3.forward : k == 1 ? Vector3.back : k == 2 ? Vector3.right : Vector3.left;
                var side = new Vector3(Mathf.Abs(dir.z), 0, Mathf.Abs(dir.x)); var thick = new Vector3(Mathf.Abs(dir.x), 0, Mathf.Abs(dir.z));
                var wall = new GameObject("Mur invisible").AddComponent<BoxCollider>(); wall.transform.SetParent(root);
                wall.transform.position = dir * (half - inset) + Vector3.up * 30f; wall.size = side * (half * 2 + 20f) + Vector3.up * 160f + thick * 2f;
                var m = belts != null && k < belts.Length ? belts[k] : null; if (!m) continue;
                var belt = GameObject.CreatePrimitive(PrimitiveType.Cube); belt.name = "Ceinture"; belt.transform.SetParent(root);
                Object.DestroyImmediate(belt.GetComponent<Collider>());
                float hgt = beltTop - floorY;
                belt.transform.position = dir * (half + 40f) + Vector3.up * (floorY + hgt / 2);
                belt.transform.localScale = side * (half * 2 + 200f) + Vector3.up * hgt + thick * 80f;
                belt.GetComponent<Renderer>().sharedMaterial = m; belt.isStatic = true;
            }
        }

        public static Material BeltMaterial(string name, string layer, Color tint)
        {
            var m = WorldBuilder.GateMaterial("Bordure_" + name, "Terrain/" + layer, 1f); if (!m) return null;
            m.SetTextureScale("_BaseMap", new Vector2(24, 4)); m.SetColor("_BaseColor", tint); EditorUtility.SetDirty(m); return m;
        }

        // ------------------------------------------------------------------------------------------ lumière
        static void Lighting(Def d, ZoneLook look)
        {
            // plein jour partout (pas de nuit dans le jeu), teinté selon la région
            Color sunC = new Color(1f, 0.94f, 0.82f), skyTint = new Color(0.52f, 0.62f, 0.82f), amSky = new Color(0.62f, 0.70f, 0.86f), amEq = new Color(0.50f, 0.52f, 0.46f), amGr = new Color(0.24f, 0.22f, 0.17f);
            float sunI = 1.6f, thick = 1f, pitch = 44;
            switch (d.kind)
            {
                case Kind.Desert: sunI = 1.45f; sunC = new Color(1f, 0.92f, 0.76f); thick = 0.8f; amEq = new Color(0.58f, 0.52f, 0.42f); amGr = new Color(0.34f, 0.28f, 0.20f); break;
                case Kind.Pics: pitch = 36; look.hasTop = true; look.top = new Color(0.86f, 0.9f, 0.95f, 0.2f); look.topStrength = 0.45f; break;
                case Kind.Cote: skyTint = new Color(0.5f, 0.64f, 0.85f); amEq = new Color(0.52f, 0.58f, 0.6f); break;
                case Kind.Marais: sunI = 1.4f; sunC = new Color(0.95f, 0.95f, 0.8f); skyTint = new Color(0.5f, 0.58f, 0.55f); amSky = new Color(0.55f, 0.62f, 0.6f); amEq = new Color(0.42f, 0.46f, 0.36f); amGr = new Color(0.2f, 0.2f, 0.14f);
                    look.hasTop = true; look.top = new Color(0.2f, 0.26f, 0.1f, 0.08f); look.topStrength = 0.7f; break;
                case Kind.Toundra: sunI = 1.5f; sunC = new Color(0.95f, 0.97f, 1f); skyTint = new Color(0.55f, 0.66f, 0.85f); amSky = new Color(0.7f, 0.78f, 0.92f); amEq = new Color(0.62f, 0.66f, 0.72f); amGr = new Color(0.45f, 0.47f, 0.5f); pitch = 30;
                    look.hasTop = true; look.top = new Color(0.9f, 0.93f, 0.97f, 0.35f); look.topStrength = 0.8f; break;
                case Kind.Volcan: sunI = 1.45f; sunC = new Color(1f, 0.82f, 0.62f); skyTint = new Color(0.62f, 0.5f, 0.45f); thick = 1.6f; amSky = new Color(0.62f, 0.55f, 0.52f); amEq = new Color(0.5f, 0.38f, 0.3f); amGr = new Color(0.3f, 0.16f, 0.1f);
                    look.hasTop = true; look.top = new Color(0.16f, 0.14f, 0.13f, 0.04f); look.topStrength = 0.6f; break;
                case Kind.Canyon: sunC = new Color(1f, 0.9f, 0.76f); amEq = new Color(0.6f, 0.46f, 0.38f); amGr = new Color(0.36f, 0.2f, 0.14f); break;
                case Kind.Abime: sunI = 1.35f; sunC = new Color(0.92f, 0.86f, 1f); skyTint = new Color(0.56f, 0.48f, 0.78f); thick = 1.4f; amSky = new Color(0.6f, 0.55f, 0.78f); amEq = new Color(0.45f, 0.4f, 0.55f); amGr = new Color(0.2f, 0.14f, 0.26f);
                    look.hasTop = true; look.top = new Color(0.36f, 0.3f, 0.45f, 0.2f); look.topStrength = 0.45f; break;
            }
            var sun = new GameObject("Soleil").AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.78f;
            sun.intensity = sunI; sun.color = sunC; sun.transform.rotation = Quaternion.Euler(pitch, -40, 0);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_AtmosphereThickness", thick); sky.SetColor("_SkyTint", skyTint); sky.SetColor("_GroundColor", new Color(0.36f, 0.36f, 0.32f)); sky.SetFloat("_Exposure", 1.15f);
            var skyPath = $"{Root}/Rendering/Ciel_{d.tag}.mat"; AssetDatabase.DeleteAsset(skyPath); AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky; RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = amSky; RenderSettings.ambientEquatorColor = amEq; RenderSettings.ambientGroundColor = amGr;
            RenderSettings.fog = false;
        }

        // ------------------------------------------------------------------------------------------ eau, glace, lave, vide
        static Material SavedMat(string name, Shader sh, System.Action<Material> setup)
        {
            var path = $"{Root}/Rendering/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            setup(m); EditorUtility.SetDirty(m); return m;
        }

        static void Liquids(Def d, ZoneLook look)
        {
            float lv = Liquid(d); if (float.IsNaN(lv)) return;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); plane.name = "Nappe";
            plane.transform.position = new Vector3(0, lv, 0); plane.transform.localScale = new Vector3(Size / 10f * 1.6f, 1, Size / 10f * 1.6f);
            var mr = plane.GetComponent<Renderer>(); mr.shadowCastingMode = ShadowCastingMode.Off;
            var lit = Shader.Find("Universal Render Pipeline/Lit"); var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            bool walkable = d.kind == Kind.Toundra;
            if (!walkable) Object.DestroyImmediate(plane.GetComponent<Collider>());
            switch (d.kind)
            {
                case Kind.Cote: mr.sharedMaterial = WorldBuilder.WaterMaterial(); break;
                case Kind.Marais:
                {
                    var baseW = WorldBuilder.WaterMaterial();
                    mr.sharedMaterial = SavedMat("Eau_Marais", baseW.shader, m =>
                    {
                        m.CopyPropertiesFromMaterial(baseW);
                        if (m.HasProperty("_ShallowColor")) { m.SetColor("_ShallowColor", new Color(0.20f, 0.24f, 0.12f, 0.75f)); m.SetColor("_DeepColor", new Color(0.06f, 0.08f, 0.04f, 0.97f)); m.SetFloat("_Speed", 0.008f); m.SetFloat("_BumpScale", 0.25f); }
                        else m.SetColor("_BaseColor", new Color(0.16f, 0.2f, 0.1f, 0.9f));
                        m.renderQueue = (int)RenderQueue.Transparent;
                    });
                    break;
                }
                case Kind.Toundra:   // lacs gelés : glace lisse, bleutée, avec les fissures de la roche en relief
                    mr.sharedMaterial = SavedMat("Glace", lit, m =>
                    {
                        m.SetColor("_BaseColor", new Color(0.62f, 0.78f, 0.88f)); m.SetFloat("_Smoothness", 0.88f); m.SetFloat("_Metallic", 0f);
                        var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/Neant_Normal.png");
                        if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.35f); m.SetTextureScale("_BaseMap", new Vector2(40, 40)); }
                    });
                    break;
                case Kind.Volcan:    // lave : croûte noire et fissures incandescentes qui s'écoulent
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/Lave_BaseMap.png");
                    mr.sharedMaterial = SavedMat("Lave", unlit, m => { m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(2.2f, 1.6f, 1.3f)); m.SetTextureScale("_BaseMap", new Vector2(36, 36)); });
                    look.flow = mr; look.flowSpeed = new Vector2(0.003f, 0.0015f);
                    break;
                }
                default:             // Abîme : vide violet sombre, à peine lumineux, au fond des gouffres
                    mr.sharedMaterial = SavedMat("Neant_Vide", unlit, m => { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/World/Terrain/Neant_BaseMap.png")); m.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.9f)); m.SetTextureScale("_BaseMap", new Vector2(10, 10)); });
                    look.flow = mr; look.flowSpeed = new Vector2(0.002f, -0.003f);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------ terrain : peinture, arbres, herbe
        static void Paint(Def d, TerrainData td)
        {
            int A = td.alphamapResolution, L = td.terrainLayers.Length; var map = new float[A, A, L]; var w = new float[4];
            for (int j = 0; j < A; j++) for (int i = 0; i < A; i++)
            {
                float x = -Size / 2 + Size * (i + 0.5f) / A, z = -Size / 2 + Size * (j + 0.5f) / A; var p = new Vector2(x, z);
                var n = td.GetInterpolatedNormal((i + 0.5f) / A, (j + 0.5f) / A); float slope = 1 - n.y;
                float path = Smooth(5f, 2.5f, PathDist(d, p) + N(x, z, 0.05f, 3f) * 1.5f);
                w[0] = w[1] = w[2] = w[3] = 0;
                Weights(d, x, z, slope, path, w);
                float s = 1e-4f; for (int k = 0; k < 4; k++) { w[k] = Mathf.Max(0, w[k]); s += w[k]; }
                for (int k = 0; k < L && k < 4; k++) map[j, i, k] = w[k] / s;
            }
            td.SetAlphamaps(0, 0, map);
        }

        static void Trees(Def d, TerrainData td)
        {
            // arbres « vivants » du terrain ; les arbres morts, enneigés ou calcinés sont posés en objets teintés (Decor)
            string[] names; int target;
            switch (d.kind)
            {
                case Kind.Foret: names = new[] { "Chene0", "Chene1", "Chene2", "Pin0", "Pin1" }; target = 2600; break;
                case Kind.Pics: names = new[] { "Pin0", "Pin1", "Pin2" }; target = 800; break;
                case Kind.Cote: names = new[] { "Pin0", "Pin1", "Pin2", "Chene1" }; target = 500; break;
                case Kind.Marais: names = new[] { "Chene0", "Chene2" }; target = 300; break;
                case Kind.Desert: names = new[] { "Pin2" }; target = 40; break;
                default: return;
            }
            var protos = new List<TreePrototype>();
            foreach (var n in names)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Arbres/{n}_Arbre.prefab");
                if (!pf) pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{n}.prefab");
                if (pf) protos.Add(new TreePrototype { prefab = pf });
            }
            if (protos.Count == 0) return;
            td.treePrototypes = protos.ToArray();
            var rng = new System.Random(17); var inst = new List<TreeInstance>(); float liq = Liquid(d);
            for (int t = 0; t < target * 12 && inst.Count < target; t++)
            {
                float x = (float)rng.NextDouble() * (Size - 20) - (Size - 20) / 2, z = (float)rng.NextDouble() * (Size - 20) - (Size - 20) / 2; var p = new Vector2(x, z);
                if (PathDist(d, p) < 8f || (p - d.entry).magnitude < 24f) continue;
                float h = Height(d, x, z);
                if (!float.IsNaN(liq) && h < liq + 0.8f) continue;
                if (d.kind == Kind.Pics && h > 45f) continue;                          // pas d'arbres sur les crêtes
                float dens = d.kind == Kind.Foret ? 0.55f + 0.45f * N(x, z, 0.03f, 2f) : 0.35f + 0.5f * N(x, z, 0.02f, 5f);
                if (d.kind == Kind.Foret && N(x, z, 0.05f, 9f) < 0.32f) dens *= 0.15f;   // clairières
                if (rng.NextDouble() > dens) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                inst.Add(new TreeInstance { prototypeIndex = rng.Next(protos.Count), position = new Vector3((x + Size / 2) / Size, 0, (z + Size / 2) / Size), widthScale = s, heightScale = s, rotation = (float)rng.NextDouble() * 6.28f, color = Color.white, lightmapColor = Color.white });
            }
            td.SetTreeInstances(inst.ToArray(), true);
        }

        /// <summary>Touffes d'herbe 3D (comme dans la vallée) : denses en forêt et au marais, rares ailleurs ; pas dans le
        /// désert, le canyon, la cendre ni le néant.</summary>
        static void Grass(Def d, TerrainData td)
        {
            if (d.kind == Kind.Desert || d.kind == Kind.Volcan || d.kind == Kind.Canyon || d.kind == Kind.Abime) return;
            var clump = WorldBuilder.GrassClumpPrefab(); if (!clump) return;
            Color dry = d.kind == Kind.Toundra ? new Color(0.95f, 0.9f, 0.7f) : d.kind == Kind.Pics ? new Color(0.9f, 0.85f, 0.65f) : new Color(1.0f, 0.92f, 0.75f);
            Color healthy = d.kind == Kind.Marais ? new Color(0.8f, 0.85f, 0.6f) : d.kind == Kind.Toundra ? new Color(1f, 0.95f, 0.75f) : Color.white;
            td.detailPrototypes = new[] { new DetailPrototype
            {
                prototype = clump, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 0.8f, maxWidth = 1.4f, minHeight = 0.6f, maxHeight = d.kind == Kind.Foret || d.kind == Kind.Marais ? 1.4f : 0.9f, noiseSpread = 0.4f,
                healthyColor = healthy, dryColor = dry
            } };
            int R = td.detailResolution; var map = new int[R, R]; float liq = Liquid(d);
            float thr = d.kind == Kind.Foret || d.kind == Kind.Marais ? 0.2f : d.kind == Kind.Toundra ? 0.55f : 0.35f;
            float max = d.kind == Kind.Foret || d.kind == Kind.Marais ? 7f : d.kind == Kind.Toundra ? 3f : 5f;
            for (int j = 0; j < R; j++) for (int i = 0; i < R; i++)
            {
                float x = -Size / 2 + Size * (i + 0.5f) / R, z = -Size / 2 + Size * (j + 0.5f) / R; var p = new Vector2(x, z);
                if (PathDist(d, p) < 4f) continue;
                float h = Height(d, x, z);
                if (!float.IsNaN(liq) && h < liq + 0.3f) continue;
                if ((d.kind == Kind.Pics && h > 32f) || (d.kind == Kind.Cote && h < 2.5f)) continue;
                float dens = N(x, z, 0.035f, 3.1f);
                map[j, i] = Mathf.RoundToInt(Mathf.Clamp01((dens - thr) * 1.6f) * max);
            }
            td.SetDetailLayer(0, 0, 0, map);
        }

        // ------------------------------------------------------------------------------------------ décor
        static GameObject Prefab(string rel) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{rel}.prefab");

        /// <summary>Pose une pièce de décor au sol (enfoncée de « sink » m), tournée, mise à l'échelle, éventuellement teintée.</summary>
        static GameObject Put(Def d, string rel, Vector2 p, float yaw, float scale, Transform env, float sink = 0.1f, Color? tint = null)
        {
            var pf = Prefab(rel); if (!pf) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, env);
            go.transform.position = Ground(d, p) - Vector3.up * sink; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale *= scale;
            if (tint.HasValue) Tint(go, tint.Value);
            bool breakable = rel.Contains("Tonneaux") || rel.Contains("Caisses") || rel.Contains("Charrette") || rel.Contains("Banc") || rel.Contains("Foin") || rel.Contains("Props/");
            if (breakable) Breakable.Make(go, rel.Contains("Charrette") ? 1.6f : 1.0f);
            else foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        static void Tint(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb); }
        }

        static Vector2 RandomSpot(Def d, System.Random rng, float clearPath, List<Vector2> taken, float gap, bool dryOnly = true)
        {
            float liq = Liquid(d);
            for (int k = 0; k < 80; k++)
            {
                var p = new Vector2((float)rng.NextDouble() * (Size - 50) - (Size - 50) / 2, (float)rng.NextDouble() * (Size - 50) - (Size - 50) / 2);
                if (PathDist(d, p) < clearPath || (p - d.entry).magnitude < 28f) continue;
                if (dryOnly && !float.IsNaN(liq) && Height(d, p.x, p.y) < liq + 0.6f) continue;
                bool ok = true; foreach (var t in taken) if ((t - p).sqrMagnitude < gap * gap) { ok = false; break; }
                if (!ok) continue;
                taken.Add(p); return p;
            }
            return new Vector2(9999, 9999);
        }

        static bool Bad(Vector2 p) => p.x > 9000;

        static readonly Color SandTint = new Color(1.0f, 0.86f, 0.66f), PaleStone = new Color(0.95f, 0.88f, 0.76f), RedStone = new Color(1.0f, 0.62f, 0.48f),
                              Obsidian = new Color(0.22f, 0.2f, 0.22f), VoidStone = new Color(0.45f, 0.38f, 0.58f), Burnt = new Color(0.18f, 0.15f, 0.13f),
                              DeadWood = new Color(0.55f, 0.52f, 0.42f), Frost = new Color(0.88f, 0.92f, 1.0f);

        static Color? RockTint(Def d) => d.kind switch
        {
            Kind.Desert => SandTint, Kind.Canyon => RedStone, Kind.Volcan => Obsidian, Kind.Abime => VoidStone, Kind.Toundra => new Color(0.92f, 0.94f, 1f), _ => (Color?)null,
        };

        static void Decor(Def d, Transform env, System.Random rng, ZoneLook look)
        {
            var taken = new List<Vector2>(); float liq = Liquid(d);
            float R() => (float)rng.NextDouble();
            var rt = RockTint(d);
            // ---- rochers épars (nombreux, tailles très variées)
            int rocks = d.kind == Kind.Pics || d.kind == Kind.Canyon ? 220 : d.kind == Kind.Foret ? 110 : 160;
            for (int k = 0; k < rocks; k++)
            {
                var p = new Vector2(R() * (Size - 30) - (Size - 30) / 2, R() * (Size - 30) - (Size - 30) / 2);
                if (PathDist(d, p) < 6f || (p - d.entry).magnitude < 20f) continue;
                if (!float.IsNaN(liq) && d.kind != Kind.Cote && Height(d, p.x, p.y) < liq) continue;
                float sc = R() < 0.15f ? 2.5f + R() * (d.kind == Kind.Pics || d.kind == Kind.Canyon ? 4f : 2.5f) : 0.5f + R() * 1.4f;
                Put(d, $"Rocher{k % 3}", p, R() * 360, sc, env, 0.25f * sc, rt);
            }
            // ---- pierres en bordure du chemin
            var line = Path(d);
            for (int i = 0; i < line.Length - 1; i++)
                for (float t = 0.1f; t < 1f; t += 0.12f)
                {
                    if (R() < 0.5f) continue;
                    var a = Vector2.Lerp(line[i], line[i + 1], t); var dir = (line[i + 1] - line[i]).normalized; var side = new Vector2(-dir.y, dir.x);
                    Put(d, $"Rocher{rng.Next(3)}", a + side * ((R() < 0.5f ? -1 : 1) * (7f + R() * 3f)), R() * 360, 0.4f + R() * 0.6f, env, 0.15f, rt);
                }

            Hamlet(d, env, rng, taken);
            // abords du chemin : objets cassables (tonneaux, caisses, haltes abandonnées, urnes, charrettes…)
            PropKit.AlongPath(Path(d), q => Ground(d, q), q => (q - d.entry).magnitude > 20f && (float.IsNaN(liq) || Height(d, q.x, q.y) > liq + 0.5f) && (q - HamletAt(d)).magnitude > 30f,
                              env, rng, d.kind == Kind.Desert || d.kind == Kind.Canyon ? SandTint : d.kind == Kind.Volcan ? new Color(0.5f, 0.45f, 0.42f) : d.kind == Kind.Toundra ? Frost : (Color?)null, 20f);
            for (int i = 0; i < 10; i++) { var q = RandomSpot(d, rng, 6f, taken, 25f); if (!Bad(q)) PropKit.Vignette(v => Ground(d, v), q, (float)rng.NextDouble() * 360, env, rng, RockTint(d)); }

            switch (d.kind)
            {
                case Kind.Foret:
                    for (int c = 0; c < 4; c++)   // cercles de menhirs astraux dans les clairières
                    {
                        var ctr = RandomSpot(d, rng, 18f, taken, 60f); if (Bad(ctr)) continue;
                        for (int i = 0; i < 7; i++) { float an = i * Mathf.PI * 2 / 7; Put(d, "Crypte/D_Pilier", ctr + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 7f, R() * 360, 0.55f + R() * 0.25f, env, 0.3f + R() * 0.8f); }
                        look.crystals.Add(Ground(d, ctr) + Vector3.up * 0.2f); look.crystalSizes.Add(1.6f);
                    }
                    look.crystalColor = new Color(0.5f, 0.75f, 1f);
                    Camps(d, env, rng, taken, 5, null);
                    Bones(d, env, rng, taken, 40, null);
                    break;

                case Kind.Desert:
                    Ruins(d, env, rng, Path(d)[3], 16f, 12, true);
                    for (int c = 0; c < 6; c++) { var ctr = RandomSpot(d, rng, 14f, taken, 50f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 6f + R() * 5f, 4 + rng.Next(4), R() < 0.5f); }
                    Spires(d, env, rng, taken, 10, SandTint);
                    Bones(d, env, rng, taken, 60, PaleStone);
                    Camps(d, env, rng, taken, 4, SandTint);
                    break;

                case Kind.Pics:
                    for (int c = 0; c < 4; c++)   // tours de guet et remparts en ruine qui gardaient les cols
                    {
                        var ctr = RandomSpot(d, rng, 16f, taken, 70f); if (Bad(ctr)) continue;
                        Put(d, "Ville/M_TourVille", ctr, R() * 360, 0.9f, env, 1.2f);
                        Put(d, "Ville/M_Rempart", ctr + new Vector2(7, 0), R() * 360, 0.9f, env, 1.5f);
                    }
                    for (int c = 0; c < 6; c++) { var ctr = RandomSpot(d, rng, 12f, taken, 40f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 4f, 5, false); }
                    for (int c = 0; c < 8; c++) { var ctr = RandomSpot(d, rng, 10f, taken, 30f); if (Bad(ctr)) continue; look.crystals.Add(Ground(d, ctr)); look.crystalSizes.Add(1.2f + R() * 1.6f); }
                    look.crystalColor = new Color(0.7f, 0.85f, 1f);
                    Bones(d, env, rng, taken, 30, null);
                    break;

                case Kind.Cote:
                    // pitons dans la mer, phare en ruine sur un cap, épaves (caisses, tonneaux) sur les plages
                    for (int c = 0; c < 9; c++)
                    {
                        var p = new Vector2(R() * 300 - 150, 110 + R() * 55); float hgt = 8f + R() * 16f;
                        Stack(env, rng, new Vector3(p.x, -5f, p.y), hgt + 5f, null);
                    }
                    { var cap = RandomSpot(d, rng, 14f, taken, 60f); if (!Bad(cap)) { Put(d, "Ville/M_TourVille", cap, R() * 360, 1.1f, env, 1f); Put(d, "Ville/M_Lanterne", cap + new Vector2(4, 0), 0, 1.4f, env); } }
                    for (int i = 0; i < 24; i++)
                    {
                        var p = new Vector2(R() * 320 - 160, 40 + R() * 80); float h = Height(d, p.x, p.y);
                        if (h < 0.2f || h > 3f || PathDist(d, p) < 5f) continue;
                        Put(d, i % 3 == 0 ? "Ville/M_Tonneaux" : i % 3 == 1 ? "Ville/M_Caisses" : "Ville/M_Charrette", p, R() * 360, 1f, env, 0.3f, new Color(0.75f, 0.72f, 0.66f));
                    }
                    for (int c = 0; c < 4; c++) { var ctr = RandomSpot(d, rng, 12f, taken, 50f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 5f, 6, R() < 0.5f); }
                    Bones(d, env, rng, taken, 30, null);
                    break;

                case Kind.Marais:
                    DeadTrees(d, env, rng, taken, 180, new Color(0.42f, 0.45f, 0.35f), "Chene", false);
                    for (int c = 0; c < 5; c++) { var ctr = RandomSpot(d, rng, 12f, taken, 45f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 5f + R() * 3f, 6, R() < 0.4f); }
                    for (int c = 0; c < 14; c++)   // feux follets : petits cristaux verts lumineux au ras de l'eau
                    {
                        var p = RandomSpot(d, rng, 8f, taken, 22f, false); if (Bad(p)) continue;
                        look.crystals.Add(new Vector3(p.x, Mathf.Max(Height(d, p.x, p.y), liq) + 0.6f, p.y)); look.crystalSizes.Add(0.5f + R() * 0.4f);
                    }
                    look.crystalColor = new Color(0.55f, 1f, 0.6f); look.floatingCrystals = true;
                    Bones(d, env, rng, taken, 40, new Color(0.7f, 0.72f, 0.6f));
                    Camps(d, env, rng, taken, 3, new Color(0.6f, 0.6f, 0.5f));
                    break;

                case Kind.Toundra:
                    DeadTrees(d, env, rng, taken, 320, Frost, "Pin", true);
                    for (int c = 0; c < 18; c++) { var ctr = RandomSpot(d, rng, 10f, taken, 25f); if (Bad(ctr)) continue; look.crystals.Add(Ground(d, ctr)); look.crystalSizes.Add(1.4f + R() * 2.4f); }
                    look.crystalColor = new Color(0.55f, 0.85f, 1f);
                    for (int c = 0; c < 4; c++) { var ctr = RandomSpot(d, rng, 14f, taken, 50f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 5f, 7, true); }
                    Camps(d, env, rng, taken, 3, Frost);
                    Bones(d, env, rng, taken, 30, Frost);
                    break;

                case Kind.Volcan:
                    DeadTrees(d, env, rng, taken, 160, Burnt, "Pin", true);
                    Spires(d, env, rng, taken, 14, Obsidian);
                    for (int c = 0; c < 3; c++)   // forges abandonnées de Cendrefer
                    {
                        var ctr = RandomSpot(d, rng, 14f, taken, 70f); if (Bad(ctr)) continue;
                        Put(d, "Ville/M_Forge", ctr, R() * 360, 1f, env, 0.2f, new Color(0.5f, 0.45f, 0.42f));
                        Put(d, "Crypte/D_Brasero", ctr + new Vector2(4, 2), 0, 1.2f, env);
                        Ruins(d, env, rng, ctr + new Vector2(0, 12), 5f, 5, true);
                    }
                    for (int c = 0; c < 10; c++) { var ctr = RandomSpot(d, rng, 10f, taken, 30f); if (Bad(ctr)) continue; look.crystals.Add(Ground(d, ctr)); look.crystalSizes.Add(1.2f + R() * 1.5f); }
                    look.crystalColor = new Color(1f, 0.45f, 0.15f);
                    Bones(d, env, rng, taken, 40, new Color(0.45f, 0.4f, 0.38f));
                    break;

                case Kind.Canyon:
                    Spires(d, env, rng, taken, 16, RedStone);
                    for (int c = 0; c < 6; c++) { var ctr = RandomSpot(d, rng, 12f, taken, 50f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 6f, 6, true); }
                    DeadTrees(d, env, rng, taken, 60, DeadWood, "Pin", true);
                    Camps(d, env, rng, taken, 4, new Color(0.9f, 0.75f, 0.65f));
                    Bones(d, env, rng, taken, 60, PaleStone);
                    break;

                default: // Abîme
                    Spires(d, env, rng, taken, 18, VoidStone);
                    for (int c = 0; c < 26; c++) { var ctr = RandomSpot(d, rng, 9f, taken, 20f); if (Bad(ctr)) continue; look.crystals.Add(Ground(d, ctr) + Vector3.up * (c % 3 == 0 ? 3f + R() * 4f : 0f)); look.crystalSizes.Add(1.2f + R() * 2.6f); }
                    look.crystalColor = new Color(0.7f, 0.35f, 1f); look.floatingCrystals = true;
                    for (int c = 0; c < 6; c++) { var ctr = RandomSpot(d, rng, 12f, taken, 45f); if (!Bad(ctr)) Ruins(d, env, rng, ctr, 6f, 8, true); }
                    // rochers suspendus au-dessus des gouffres
                    for (int i = 0; i < 20; i++)
                    {
                        var p = new Vector2(R() * 300 - 150, R() * 280 - 120); if (Height(d, p.x, p.y) > 1f) continue;
                        var go = Put(d, $"Rocher{i % 3}", p, R() * 360, 1.5f + R() * 3f, env, 0f, VoidStone); if (!go) continue;
                        go.transform.position = new Vector3(p.x, 6f + R() * 10f, p.y); foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
                        var f = go.AddComponent<Floater>(); f.bob = 0.6f; f.period = 6f + R() * 4f; f.spin = 3f;
                    }
                    Bones(d, env, rng, taken, 30, VoidStone);
                    break;
            }
        }

        /// <summary>Hameau en ruine : maisons affaissées et de travers (enfoncées, inclinées, noircies), pans de rempart
        /// écroulés, puits, brasero, charrettes, caisses et tonneaux (destructibles), ossements ; teinté selon la région.</summary>
        static void Hamlet(Def d, Transform env, System.Random rng, List<Vector2> taken)
        {
            float R() => (float)rng.NextDouble();
            var c = HamletAt(d); taken.Add(c);
            var line = Path(d); var dir = (line[line.Length - 1] - line[line.Length - 2]).normalized;
            Color tint = d.kind switch
            {
                Kind.Desert => new Color(1f, 0.88f, 0.72f), Kind.Canyon => new Color(1f, 0.8f, 0.68f), Kind.Volcan => new Color(0.42f, 0.38f, 0.36f),
                Kind.Toundra => new Color(0.9f, 0.93f, 1f), Kind.Abime => new Color(0.6f, 0.55f, 0.72f), Kind.Marais => new Color(0.62f, 0.64f, 0.55f), _ => new Color(0.72f, 0.68f, 0.62f),
            };
            // maisons en arc de cercle, ouvertes vers le chemin d'arrivée
            int houses = 5;
            for (int i = 0; i < houses; i++)
            {
                float a = Mathf.Atan2(dir.y, dir.x) + Mathf.Lerp(-1.9f, 1.9f, i / (float)(houses - 1));
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (15f + R() * 3f);
                var face = Mathf.Atan2(-(c - p).x, -(c - p).y) * Mathf.Rad2Deg;   // façade vers la place
                var go = Put(d, $"Ville/M_Maison{rng.Next(8)}", p, face + 180f, 0.95f, env, 0.4f + R() * 1.6f, tint);
                if (go) go.transform.rotation *= Quaternion.Euler(R() * 9 - 4.5f, 0, R() * 9 - 4.5f);       // de travers, affaissée
                // éboulis au pied
                for (int k = 0; k < 3; k++) Put(d, $"Rocher{rng.Next(3)}", p + new Vector2(R() * 6 - 3, R() * 6 - 3), R() * 360, 0.3f + R() * 0.4f, env, 0.1f, tint);
            }
            // rempart écroulé derrière le hameau
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.Atan2(dir.y, dir.x) + Mathf.Lerp(-0.9f, 0.9f, i / 3f);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 26f;
                if (R() < 0.25f) continue;
                var w = Put(d, "Ville/M_Rempart", p, a * -Mathf.Rad2Deg + 90f, 0.8f, env, 1f + R() * 2.5f, tint);
                if (w) w.transform.rotation *= Quaternion.Euler(R() * 6 - 3, 0, R() * 10 - 5);
            }
            // place : puits, brasero, charrette renversée, étal abandonné, caisses et tonneaux
            Put(d, "Ville/M_Puits", c, R() * 360, 1f, env, 0.15f, tint);
            Put(d, "Crypte/D_Brasero", c + new Vector2(4, 3), 0, 1.1f, env);
            var cart = Put(d, "Ville/M_Charrette", c + new Vector2(-6, -2), R() * 360, 1f, env, 0.2f, tint);
            if (cart) cart.transform.rotation *= Quaternion.Euler(0, 0, 12f);
            Put(d, "Ville/M_Etal" + rng.Next(4), c + new Vector2(6, -6), R() * 360, 1f, env, 0.1f, tint);
            for (int i = 0; i < 8; i++) Put(d, i % 2 == 0 ? "Ville/M_Caisses" : "Ville/M_Tonneaux", c + new Vector2(R() * 20 - 10, R() * 20 - 10), R() * 360, 0.9f + R() * 0.2f, env, 0.1f, tint);
            Put(d, "Ville/M_Banniere", c + new Vector2(-3, 7), R() * 360, 1f, env, 0.1f, tint);
            for (int i = 0; i < 10; i++) Put(d, $"Crypte/D_Os{i % 2}", c + new Vector2(R() * 24 - 12, R() * 24 - 12), R() * 360, 0.9f, env, 0.05f);
        }

        /// <summary>Arbres morts, enneigés ou calcinés : arbres du jeu posés en objets, teintés et un peu tordus.</summary>
        static void DeadTrees(Def d, Transform env, System.Random rng, List<Vector2> taken, int n, Color tint, string family, bool pines)
        {
            float liq = Liquid(d);
            for (int i = 0; i < n; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * (Size - 30) - (Size - 30) / 2, (float)rng.NextDouble() * (Size - 30) - (Size - 30) / 2);
                if (PathDist(d, p) < 8f || (p - d.entry).magnitude < 24f) continue;
                float h = Height(d, p.x, p.y);
                if (!float.IsNaN(liq) && h < liq + (d.kind == Kind.Marais ? -0.6f : 0.8f)) continue;      // au marais, les arbres poussent dans l'eau
                if (d.kind == Kind.Volcan && h > 40f) continue;
                string rel = $"Arbres/{family}{rng.Next(pines ? 3 : 3)}_Arbre";
                var go = Put(d, rel, p, (float)rng.NextDouble() * 360, 0.7f + (float)rng.NextDouble() * 0.6f, env, 0.2f, tint);
                if (go) go.transform.rotation *= Quaternion.Euler((float)rng.NextDouble() * 10 - 5, 0, (float)rng.NextDouble() * 10 - 5);
            }
        }

        /// <summary>Pitons rocheux (rochers empilés et étirés), tours naturelles qui rompent l'horizon.</summary>
        static void Spires(Def d, Transform env, System.Random rng, List<Vector2> taken, int n, Color tint)
        {
            for (int c = 0; c < n; c++)
            {
                var ctr = RandomSpot(d, rng, 14f, taken, 30f); if (Bad(ctr)) continue;
                Stack(env, rng, new Vector3(ctr.x, Height(d, ctr.x, ctr.y), ctr.y), 8f + (float)rng.NextDouble() * 16f, tint);
            }
        }

        static void Stack(Transform env, System.Random rng, Vector3 b, float hgt, Color? tint)
        {
            for (float y = 0; y < hgt; y += 2.8f)
            {
                float w = Mathf.Lerp(4.2f, 2f, y / hgt);
                var pf = Prefab($"Rocher{rng.Next(3)}"); if (!pf) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, env);
                go.transform.position = new Vector3(b.x + (float)rng.NextDouble() * 1.4f - 0.7f, b.y + y - 0.5f, b.z + (float)rng.NextDouble() * 1.4f - 0.7f);
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0); go.transform.localScale = new Vector3(w, 2.4f, w);
                if (tint.HasValue) Tint(go, tint.Value);
                go.isStatic = true;
            }
        }

        /// <summary>Campements abandonnés (charrette, caisses, tonneaux, foin, lanterne).</summary>
        static void Camps(Def d, Transform env, System.Random rng, List<Vector2> taken, int n, Color? tint)
        {
            string[] kit = { "Ville/M_Charrette", "Ville/M_Caisses", "Ville/M_Tonneaux", "Ville/M_Foin", "Ville/M_Lanterne" };
            for (int c = 0; c < n; c++)
            {
                var ctr = RandomSpot(d, rng, 12f, taken, 40f); if (Bad(ctr)) continue;
                for (int i = 0; i < kit.Length; i++) Put(d, kit[i], ctr + new Vector2((float)rng.NextDouble() * 10 - 5, (float)rng.NextDouble() * 10 - 5), (float)rng.NextDouble() * 360, 1f, env, 0.15f, i == 4 ? null : tint);
            }
        }

        static void Bones(Def d, Transform env, System.Random rng, List<Vector2> taken, int n, Color? tint)
        {
            for (int i = 0; i < n; i++)
            {
                var p = RandomSpot(d, rng, 5f, taken, 7f); if (Bad(p)) continue;
                Put(d, $"Crypte/D_Os{i % 2}", p, (float)rng.NextDouble() * 360, 0.8f + (float)rng.NextDouble() * (i % 7 == 0 ? 2.5f : 0.5f), env, 0.05f, tint);
            }
        }

        /// <summary>Ruine : cercle de piliers brisés (hauteurs variées, inclinés), arche éventuelle, sarcophage et éboulis.</summary>
        static void Ruins(Def d, Transform env, System.Random rng, Vector2 c, float radius, int n, bool arch)
        {
            float R() => (float)rng.NextDouble();
            Color? tint = d.kind == Kind.Desert || d.kind == Kind.Canyon ? PaleStone : d.kind == Kind.Abime ? VoidStone : d.kind == Kind.Volcan ? new Color(0.5f, 0.45f, 0.42f) : (Color?)null;
            for (int i = 0; i < n; i++)
            {
                float an = i * Mathf.PI * 2 / n + R() * 0.3f;
                if (R() < 0.2f) continue;                                             // pilier disparu
                var go = Put(d, "Crypte/D_Pilier", c + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * radius, R() * 360, 0.7f + R() * 0.3f, env, 0.4f + R() * 2.2f, tint);
                if (go) go.transform.rotation *= Quaternion.Euler(R() * 14 - 7, 0, R() * 14 - 7);
            }
            if (arch) Put(d, "Crypte/D_Arche", c + new Vector2(0, -radius), 0, 1f, env, 0.3f, tint);
            Put(d, "Crypte/D_Sarcophage0", c, R() * 360, 1f, env, 0.15f, tint);
            for (int i = 0; i < 4; i++) Put(d, $"Rocher{rng.Next(3)}", c + new Vector2(R() * radius * 2 - radius, R() * radius * 2 - radius), R() * 360, 0.4f + R() * 0.4f, env, 0.1f, tint ?? RockTint(d));
        }

        // ------------------------------------------------------------------------------------------ vallée
        /// <summary>Portails de la vallée vers les régions reliées à une route (nord, sud, ouest), avec bornes.</summary>
        public static void ValleyEntrances()
        {
            foreach (var d in All)
            {
                if (!d.valleyGate.HasValue) continue;
                var gate = d.valleyGate.Value; var p = WorldBuilder.Ground(gate);
                var root = new GameObject("Route : " + d.name).transform; root.position = p;
                var dir = (Vector2.zero - gate).normalized; var side = new Vector3(-dir.y, 0, dir.x);
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
                var deep = Ground(d, Path(d)[3]);
                cam.transform.position = deep + new Vector3(0, 14, -18); cam.transform.LookAt(deep); Shot("3_sol");
                int camps = game.GetComponent<WorldPopulation>() ? game.GetComponent<WorldPopulation>().CampCount : game.camps.Count;
                check($"Région : {d.name}", true, $"ci-out/Region_{d.tag}_*.png, {camps} camps, niveaux {d.level} à {Mathf.Min(200, d.level + Mathf.RoundToInt(320f / MetersPerLevel))}");
            }
            catch (System.Exception ex) { check($"Captures : {d.name}", false, ex.Message); }
        }
    }
}
