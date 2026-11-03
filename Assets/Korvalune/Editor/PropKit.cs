using System.Collections.Generic;
using Korvalune.Game;
using UnityEditor;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Petits objets destructibles du monde et leur disposition le long des routes et chemins (comme dans Diablo : on casse
    /// tout sur son passage). Table, tabouret et urnes sont construits ici (maillages enregistrés, bois et terre cuite
    /// texturés) ; tonneaux, caisses, charrette, banc et foin viennent du décor de la ville. Chaque objet porte un
    /// Breakable qui le fait voler en éclats selon sa matière.
    /// </summary>
    public static class PropKit
    {
        const string Root = ModelPipeline.Root, Dir = Root + "/Resources/Prefabs/World/Props";

        static Material Wood() => Tinted("Prop_Bois", "Ville/M_Maison0/Textures/W_Wood", new Color(0.9f, 0.82f, 0.72f));
        static Material Clay() => Tinted("Prop_TerreCuite", "Ville/M_Maison0/Textures/T_Plaster", new Color(0.78f, 0.47f, 0.32f));
        static Material Tinted(string name, string tex, Color c)
        {
            var m = WorldBuilder.GateMaterial(name, tex, 1f); if (!m) return WorldBuilder.StoneMaterial();
            m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); return m;
        }

        static GameObject Save(string name, Mesh mesh, Material mat, Vector3 colCenter, Vector3 colSize, Breakable.Kind kind, float radius)
        {
            ModelPipeline.EnsureFolder(Dir);
            var mp = $"{Root}/Data/Prop_{name}.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var bc = go.AddComponent<BoxCollider>(); bc.center = colCenter; bc.size = colSize;
            var b = go.AddComponent<Breakable>(); b.kind = kind; b.radius = radius;
            var pf = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab"); Object.DestroyImmediate(go);
            return pf;
        }

        /// <summary>Crée (une fois par construction) la table, le tabouret et les urnes.</summary>
        public static void Ensure()
        {
            EnsureMore();
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/P_Table.prefab") && AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/P_Urne0.prefab")) return;
            var rng = new System.Random(5);
            float R() => (float)rng.NextDouble();
            // table de taverne : plateau de 4 planches, ceinture, 4 pieds, entretoise
            {
                var m = new BridgeBuilder.Mesher();
                for (int i = 0; i < 4; i++) m.Box(new Vector3(-0.42f + i * 0.28f, 0.78f, 0), Quaternion.Euler(0, (R() - 0.5f) * 1.5f, 0), new Vector3(0.27f, 0.05f, 1.7f), 0.9f);
                foreach (var (x, z) in new[] { (-0.48f, -0.7f), (0.48f, -0.7f), (-0.48f, 0.7f), (0.48f, 0.7f) }) m.Box(new Vector3(x, 0.38f, z), Quaternion.identity, new Vector3(0.09f, 0.76f, 0.09f));
                m.Box(new Vector3(0, 0.7f, -0.7f), Quaternion.identity, new Vector3(1.0f, 0.09f, 0.05f)); m.Box(new Vector3(0, 0.7f, 0.7f), Quaternion.identity, new Vector3(1.0f, 0.09f, 0.05f));
                m.Box(new Vector3(-0.48f, 0.7f, 0), Quaternion.identity, new Vector3(0.05f, 0.09f, 1.4f)); m.Box(new Vector3(0.48f, 0.7f, 0), Quaternion.identity, new Vector3(0.05f, 0.09f, 1.4f));
                m.Box(new Vector3(0, 0.18f, 0), Quaternion.identity, new Vector3(0.06f, 0.06f, 1.4f));
                Save("P_Table", m.ToMesh("Table"), Wood(), new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 1.7f), Breakable.Kind.Wood, 1.0f);
            }
            // tabouret : assise ronde (octogone de planchettes), trois pieds évasés
            {
                var m = new BridgeBuilder.Mesher();
                for (int i = 0; i < 3; i++) m.Box(new Vector3(-0.12f + i * 0.12f, 0.46f, 0), Quaternion.identity, new Vector3(0.11f, 0.04f, 0.34f - Mathf.Abs(i - 1) * 0.08f), 0.9f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2 / 3; var top = new Vector3(Mathf.Cos(a) * 0.1f, 0.45f, Mathf.Sin(a) * 0.1f); var bot = new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f);
                    m.Beam(bot, top, 0.045f, 0.045f);
                }
                Save("P_Tabouret", m.ToMesh("Tabouret"), Wood(), new Vector3(0, 0.24f, 0), new Vector3(0.4f, 0.48f, 0.4f), Breakable.Kind.Wood, 0.6f);
            }
            // urnes : tour de potier (profil tourné), col et lèvre, trois galbes différents
            float[][] profiles =
            {
                new[] { 0.10f, 0.22f, 0.30f, 0.32f, 0.28f, 0.18f, 0.11f, 0.12f, 0.14f },
                new[] { 0.12f, 0.18f, 0.22f, 0.24f, 0.24f, 0.22f, 0.16f, 0.10f, 0.13f },
                new[] { 0.14f, 0.28f, 0.36f, 0.34f, 0.24f, 0.14f, 0.12f, 0.15f, 0.16f },
            };
            float[] heights = { 0.75f, 0.95f, 0.6f };
            for (int u = 0; u < 3; u++)
            {
                var mesh = Lathe(profiles[u], heights[u], 18);
                float rmax = 0; foreach (var r in profiles[u]) rmax = Mathf.Max(rmax, r);
                Save($"P_Urne{u}", mesh, Clay(), new Vector3(0, heights[u] / 2, 0), new Vector3(rmax * 2, heights[u], rmax * 2), Breakable.Kind.Pottery, 0.5f);
            }
            AssetDatabase.SaveAssets();
        }

        static Material Iron() => Tinted("Prop_Fer", "Ville/M_Maison0/Textures/T_Iron", new Color(0.55f, 0.52f, 0.5f));
        static Material Cloth() => Tinted("Prop_Toile", "Ville/S_Marchand/Textures/S_Sack", Color.white);
        static Material Wicker() => Tinted("Prop_Osier", "Ville/M_Maison0/Textures/W_Wood", new Color(0.95f, 0.78f, 0.5f));

        /// <summary>Prefab à plusieurs maillages (un par matériau : bois + ferrures…).</summary>
        static void SaveMulti(string name, (Mesh mesh, Material mat)[] parts, Vector3 colCenter, Vector3 colSize, Breakable.Kind kind, float radius)
        {
            ModelPipeline.EnsureFolder(Dir);
            var go = new GameObject(name);
            for (int i = 0; i < parts.Length; i++)
            {
                var mp = $"{Root}/Data/Prop_{name}_{i}.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(parts[i].mesh, mp);
                var child = i == 0 ? go : new GameObject("Partie" + i);
                if (i > 0) child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = parts[i].mesh; child.AddComponent<MeshRenderer>().sharedMaterial = parts[i].mat;
            }
            var bc = go.AddComponent<BoxCollider>(); bc.center = colCenter; bc.size = colSize;
            var b = go.AddComponent<Breakable>(); b.kind = kind; b.radius = radius;
            PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab"); Object.DestroyImmediate(go);
        }

        static Mesh Shift(Mesh m, float y) { var v = m.vertices; for (int i = 0; i < v.Length; i++) v[i].y += y; m.vertices = v; m.RecalculateBounds(); return m; }

        /// <summary>Tonneau seul, caisse seule, sac, panier, clôture, râtelier d'armes, brouette.</summary>
        static void EnsureMore()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/P_Brouette.prefab")) return;
            // tonneau : douelles bombées (profil tourné), trois cercles de fer
            {
                float[] prof = { 0.25f, 0.29f, 0.315f, 0.325f, 0.315f, 0.29f, 0.25f };
                var wood = Lathe(prof, 0.9f, 20);
                var hoops = new CombineInstance[3]; float[] ys = { 0.1f, 0.45f, 0.78f };
                for (int k = 0; k < 3; k++)
                {
                    float y = ys[k], r = Mathf.Lerp(0.25f, 0.325f, 1f - Mathf.Abs(y - 0.45f) / 0.45f) + 0.012f;
                    hoops[k] = new CombineInstance { mesh = Shift(Lathe(new[] { r, r, r }, 0.05f, 20), y), transform = Matrix4x4.identity };
                }
                var hm = new Mesh { name = "Cercles" }; hm.CombineMeshes(hoops, true, true);
                SaveMulti("P_Tonneau", new[] { (wood, Wood()), (hm, Iron()) }, new Vector3(0, 0.45f, 0), new Vector3(0.66f, 0.9f, 0.66f), Breakable.Kind.Barrel, 0.6f);
            }
            // caisse : montants d'angle, lattes sur chaque face, couvercle de planches, écharpe en diagonale
            {
                var m = new BridgeBuilder.Mesher(); float h = 0.7f, w = 0.7f;
                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) m.Box(new Vector3(x * (w / 2 - 0.03f), h / 2, z * (w / 2 - 0.03f)), Quaternion.identity, new Vector3(0.06f, h, 0.06f));
                for (int f = 0; f < 4; f++)
                {
                    var rot = Quaternion.Euler(0, f * 90, 0);
                    for (int k = 0; k < 4; k++) m.Box(rot * new Vector3(0, 0.09f + k * 0.17f, w / 2 - 0.01f), rot, new Vector3(w - 0.08f, 0.14f, 0.025f), 0.8f);
                    if (f % 2 == 0) { var a = rot * new Vector3(-w / 2 + 0.06f, 0.06f, w / 2 + 0.005f); var b = rot * new Vector3(w / 2 - 0.06f, h - 0.06f, w / 2 + 0.005f); m.Beam(a, b, 0.07f, 0.025f); }
                }
                for (int k = 0; k < 4; k++) m.Box(new Vector3(-w / 2 + 0.09f + k * 0.17f, h + 0.012f, 0), Quaternion.identity, new Vector3(0.15f, 0.025f, w), 0.8f);
                Save("P_Caisse", m.ToMesh("Caisse"), Tinted("Prop_BoisCaisse", "Ville/M_Maison0/Textures/W_Wood", new Color(0.82f, 0.7f, 0.55f)), new Vector3(0, h / 2, 0), new Vector3(w, h, w), Breakable.Kind.Crate, 0.6f);
            }
            // sac de grain : toile ventrue, col noué
            Save("P_Sac", Lathe(new[] { 0.2f, 0.27f, 0.29f, 0.27f, 0.21f, 0.09f, 0.07f, 0.11f }, 0.62f, 16), Cloth(), new Vector3(0, 0.3f, 0), new Vector3(0.56f, 0.62f, 0.56f), Breakable.Kind.Hay, 0.5f);
            // panier d'osier évasé, bord renforcé
            Save("P_Panier", Lathe(new[] { 0.17f, 0.2f, 0.23f, 0.255f, 0.27f, 0.285f, 0.29f }, 0.34f, 18), Wicker(), new Vector3(0, 0.17f, 0), new Vector3(0.58f, 0.34f, 0.58f), Breakable.Kind.Wood, 0.45f);
            // clôture : deux poteaux plantés, deux traverses (une fendue, de travers)
            {
                var m = new BridgeBuilder.Mesher();
                m.Box(new Vector3(-1.15f, 0.55f, 0), Quaternion.Euler(0, 0, 2), new Vector3(0.12f, 1.1f, 0.12f)); m.Box(new Vector3(1.15f, 0.5f, 0), Quaternion.Euler(0, 0, -3), new Vector3(0.12f, 1.0f, 0.12f));
                m.Beam(new Vector3(-1.25f, 0.85f, 0.07f), new Vector3(1.25f, 0.82f, 0.07f), 0.07f, 0.13f);
                m.Beam(new Vector3(-1.25f, 0.4f, 0.07f), new Vector3(1.2f, 0.28f, 0.07f), 0.07f, 0.12f);
                Save("P_Cloture", m.ToMesh("Cloture"), Wood(), new Vector3(0, 0.5f, 0), new Vector3(2.5f, 1f, 0.25f), Breakable.Kind.Wood, 1.2f);
            }
            // râtelier d'armes : montants, traverses, lances et bâtons appuyés
            {
                var m = new BridgeBuilder.Mesher();
                m.Box(new Vector3(-0.7f, 0.75f, 0), Quaternion.identity, new Vector3(0.1f, 1.5f, 0.1f)); m.Box(new Vector3(0.7f, 0.75f, 0), Quaternion.identity, new Vector3(0.1f, 1.5f, 0.1f));
                m.Box(new Vector3(0, 1.35f, 0), Quaternion.identity, new Vector3(1.5f, 0.08f, 0.1f)); m.Box(new Vector3(0, 0.25f, 0.1f), Quaternion.identity, new Vector3(1.5f, 0.08f, 0.25f));
                for (int k = 0; k < 5; k++) { float x = -0.5f + k * 0.25f; m.Beam(new Vector3(x, 0.05f, 0.2f), new Vector3(x + 0.03f, 1.9f, -0.04f), 0.045f, 0.045f); }
                Save("P_Ratelier", m.ToMesh("Ratelier"), Wood(), new Vector3(0, 0.8f, 0.05f), new Vector3(1.6f, 1.6f, 0.4f), Breakable.Kind.Wood, 0.9f);
            }
            // brouette : caisse en planches, brancards, roue à rayons
            {
                var m = new BridgeBuilder.Mesher();
                m.Box(new Vector3(0, 0.45f, 0), Quaternion.identity, new Vector3(0.6f, 0.04f, 0.9f));
                m.Box(new Vector3(-0.3f, 0.6f, 0), Quaternion.Euler(0, 0, -12), new Vector3(0.03f, 0.3f, 0.9f)); m.Box(new Vector3(0.3f, 0.6f, 0), Quaternion.Euler(0, 0, 12), new Vector3(0.03f, 0.3f, 0.9f));
                m.Box(new Vector3(0, 0.6f, -0.44f), Quaternion.Euler(12, 0, 0), new Vector3(0.62f, 0.3f, 0.03f)); m.Box(new Vector3(0, 0.6f, 0.44f), Quaternion.Euler(-25, 0, 0), new Vector3(0.66f, 0.32f, 0.03f));
                foreach (var x in new[] { -0.26f, 0.26f }) m.Beam(new Vector3(x, 0.32f, 0.7f), new Vector3(x * 1.3f, 0.55f, -1.0f), 0.05f, 0.06f);
                foreach (var x in new[] { -0.2f, 0.2f }) m.Beam(new Vector3(x, 0.42f, -0.35f), new Vector3(x, 0f, -0.45f), 0.05f, 0.05f);   // pieds
                for (int k = 0; k < 10; k++)
                {
                    float a = k * Mathf.PI * 2 / 10, b2 = (k + 1) * Mathf.PI * 2 / 10;
                    m.Beam(new Vector3(0, 0.25f + Mathf.Sin(a) * 0.24f, 0.75f + Mathf.Cos(a) * 0.24f), new Vector3(0, 0.25f + Mathf.Sin(b2) * 0.24f, 0.75f + Mathf.Cos(b2) * 0.24f), 0.06f, 0.05f);
                    if (k % 2 == 0) m.Beam(new Vector3(0, 0.25f, 0.75f), new Vector3(0, 0.25f + Mathf.Sin(a) * 0.22f, 0.75f + Mathf.Cos(a) * 0.22f), 0.025f, 0.025f);
                }
                Save("P_Brouette", m.ToMesh("Brouette"), Wood(), new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.9f, 2.0f), Breakable.Kind.Wood, 1.0f);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Volume de révolution (rayons du bas vers le haut), fond fermé, UV : tour × hauteur.</summary>
        static Mesh Lathe(float[] radii, float height, int seg)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int rows = radii.Length;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2 / seg, y = height * j / (rows - 1);
                    v.Add(new Vector3(Mathf.Cos(a) * radii[j], y, Mathf.Sin(a) * radii[j])); uv.Add(new Vector2(i / (float)seg * 2f, y * 2f));
                }
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * (seg + 1) + i, b = a + seg + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            int c = v.Count; v.Add(Vector3.zero); uv.Add(new Vector2(0.5f, 0.5f));               // fond
            for (int i = 0; i < seg; i++) t.AddRange(new[] { c, i, i + 1 });
            int top = (rows - 1) * (seg + 1), ct = v.Count; v.Add(new Vector3(0, height * 0.97f, 0)); uv.Add(new Vector2(0.5f, 0.5f));   // bouchon de cire
            for (int i = 0; i < seg; i++) t.AddRange(new[] { ct, top + i + 1, top + i });
            var m = new Mesh { name = "Urne" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------------------------------ disposition
        static GameObject Place(string rel, Vector3 pos, float yaw, Transform parent, Color? tint, bool breakable)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/{rel}.prefab"); if (!pf) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (tint.HasValue) foreach (var r in go.GetComponentsInChildren<Renderer>()) { var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", tint.Value); r.SetPropertyBlock(mpb); }
            if (breakable) Breakable.Make(go, rel.Contains("Charrette") ? 1.6f : rel.Contains("Urne") || rel.Contains("Tabouret") ? 0.55f : 1.0f);
            if (!breakable) foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        static readonly string[] Pots = { "Props/P_Urne0", "Props/P_Urne1", "Props/P_Urne2" };

        /// <summary>Petite scène d'objets cassables autour de « at », tirée parmi une dizaine de variantes (la table de halte
        /// reste rare) : tonneaux, caisses empilées, sacs de grain, paniers, urnes, clôture brisée, râtelier d'armes,
        /// brouette, charrette et foin, banc.</summary>
        public static void Vignette(System.Func<Vector2, Vector3> ground, Vector2 at, float yaw, Transform parent, System.Random rng, Color? tint = null)
        {
            float R() => (float)rng.NextDouble();
            Vector3 G(float dx, float dz) { var q = Quaternion.Euler(0, yaw, 0) * new Vector3(dx, 0, dz); return ground(at + new Vector2(q.x, q.z)); }
            string Pot() => Pots[rng.Next(Pots.Length)];
            int pick = rng.Next(100);
            if (pick < 14)        // tonneaux isolés, un couché
            {
                int n = 2 + rng.Next(3);
                for (int i = 0; i < n; i++) Place("Props/P_Tonneau", G(R() * 2.2f - 1.1f, R() * 2.2f - 1.1f), R() * 360, parent, tint, true);
                var lying = Place("Props/P_Tonneau", G(1.6f, -0.8f), yaw + 90, parent, tint, true);
                if (lying) { lying.transform.rotation *= Quaternion.Euler(90, 0, 0); lying.transform.position += Vector3.up * 0.32f; }
            }
            else if (pick < 28)   // caisses empilées
            {
                Place("Props/P_Caisse", G(0, 0), yaw + R() * 20, parent, tint, true);
                Place("Props/P_Caisse", G(0.75f, 0.1f), yaw + R() * 20, parent, tint, true);
                var top = Place("Props/P_Caisse", G(0.35f, 0.05f), yaw + R() * 30, parent, tint, true); if (top) top.transform.position += Vector3.up * 0.72f;
                if (R() < 0.5f) Place("Props/P_Sac", G(-0.8f, 0.4f), R() * 360, parent, tint, true);
            }
            else if (pick < 40)   // sacs de grain et paniers
            {
                for (int i = 0; i < 3 + rng.Next(3); i++) Place("Props/P_Sac", G(R() * 2f - 1f, R() * 2f - 1f), R() * 360, parent, tint, true);
                for (int i = 0; i < 1 + rng.Next(2); i++) Place("Props/P_Panier", G(R() * 2.6f - 1.3f, R() * 2.6f - 1.3f), R() * 360, parent, tint, true);
            }
            else if (pick < 52)   // urnes et jarres
            {
                for (int i = 0; i < 3 + rng.Next(3); i++) Place(Pot(), G(R() * 2.4f - 1.2f, R() * 2.4f - 1.2f), R() * 360, parent, null, true);
            }
            else if (pick < 62)   // clôture brisée
            {
                for (int i = 0; i < 2 + rng.Next(3); i++)
                {
                    var f = Place("Props/P_Cloture", G(i * 2.45f - 2.4f, 0), yaw + R() * 8 - 4, parent, tint, true);
                    if (f && R() < 0.3f) f.transform.rotation *= Quaternion.Euler(0, 0, 25 + R() * 30);        // à moitié tombée
                }
            }
            else if (pick < 70)   // râtelier d'armes abandonné
            {
                Place("Props/P_Ratelier", G(0, 0), yaw, parent, tint, true);
                Place("Props/P_Tonneau", G(1.2f, 0.4f), R() * 360, parent, tint, true);
            }
            else if (pick < 78)   // brouette
            {
                Place("Props/P_Brouette", G(0, 0), yaw + R() * 60 - 30, parent, tint, true);
                Place("Props/P_Sac", G(0.9f, 0.6f), R() * 360, parent, tint, true);
            }
            else if (pick < 86)   // charrette et foin
            {
                Place("Ville/M_Charrette", G(0, 0), yaw + R() * 30, parent, tint, true);
                Place("Ville/M_Foin", G(2.2f, 0.5f), R() * 360, parent, tint, true);
            }
            else if (pick < 94)   // banc et caisses
            {
                Place("Ville/M_Banc", G(0, 0), yaw, parent, tint, true);
                Place("Ville/M_Caisses", G(1.6f, 0.8f), R() * 360, parent, tint, true);
            }
            else                  // halte abandonnée (rare) : table, tabouret renversé, urne, lanterne
            {
                Place("Props/P_Table", G(0, 0), yaw + R() * 30 - 15, parent, tint, true);
                var st = Place("Props/P_Tabouret", G(-1.1f, -0.3f), R() * 360, parent, tint, true);
                if (st) { st.transform.rotation *= Quaternion.Euler(90, 0, 0); st.transform.position += Vector3.up * 0.22f; }
                Place(Pot(), G(0.2f, 1.3f), R() * 360, parent, null, true);
                Place("Ville/M_Lanterne", G(-0.6f, 1.6f), R() * 360, parent, null, false);
            }
        }

        /// <summary>Abords d'un chemin : une vignette tous les ~22 m, en alternant les côtés, à 6-9 m de l'axe.</summary>
        public static void AlongPath(Vector2[] line, System.Func<Vector2, Vector3> ground, System.Func<Vector2, bool> ok, Transform parent, System.Random rng, Color? tint = null, float every = 22f)
        {
            int side = 1;
            for (int i = 0; i < line.Length - 1; i++)
            {
                var a = line[i]; var b = line[i + 1]; float L = (b - a).magnitude; var dir = (b - a) / L; var nrm = new Vector2(-dir.y, dir.x);
                for (float s = every * 0.5f; s < L; s += every * (0.8f + (float)rng.NextDouble() * 0.5f))
                {
                    side = -side; if (rng.NextDouble() < 0.3f) continue;
                    var p = a + dir * s + nrm * side * (6f + (float)rng.NextDouble() * 3f);
                    if (!ok(p)) continue;
                    Vignette(ground, p, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + (side > 0 ? 90 : -90), parent, rng, tint);
                }
            }
        }
    }
}
