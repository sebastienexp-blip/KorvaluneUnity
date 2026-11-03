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

        /// <summary>Petite scène d'objets cassables autour de « at » : pile de tonneaux et caisses, halte abandonnée (table,
        /// tabouret renversé, urne, lanterne), urnes, charrette et foin, banc et caisses.</summary>
        public static void Vignette(System.Func<Vector2, Vector3> ground, Vector2 at, float yaw, Transform parent, System.Random rng, Color? tint = null)
        {
            float R() => (float)rng.NextDouble();
            Vector3 G(float dx, float dz) { var q = Quaternion.Euler(0, yaw, 0) * new Vector3(dx, 0, dz); return ground(at + new Vector2(q.x, q.z)); }
            switch (rng.Next(5))
            {
                case 0:
                    Place("Ville/M_Tonneaux", G(0, 0), yaw + R() * 40, parent, tint, true);
                    Place("Ville/M_Caisses", G(1.8f, 0.6f), yaw + R() * 90, parent, tint, true);
                    if (R() < 0.6f) Place($"Props/P_Urne{rng.Next(3)}", G(-1.2f, 0.9f), R() * 360, parent, null, true);
                    break;
                case 1:
                {
                    Place("Props/P_Table", G(0, 0), yaw + R() * 30 - 15, parent, tint, true);
                    var st = Place("Props/P_Tabouret", G(1.0f, 0.4f), R() * 360, parent, tint, true);
                    var st2 = Place("Props/P_Tabouret", G(-1.1f, -0.3f), R() * 360, parent, tint, true);
                    if (st2) st2.transform.rotation *= Quaternion.Euler(90, 0, 0);                    // renversé
                    if (st2) st2.transform.position += Vector3.up * 0.22f;
                    Place($"Props/P_Urne{rng.Next(3)}", G(0.2f, 1.3f), R() * 360, parent, null, true);
                    Place("Ville/M_Lanterne", G(-0.6f, 1.6f), R() * 360, parent, null, false);
                    break;
                }
                case 2:
                    for (int i = 0; i < 3 + rng.Next(3); i++) Place($"Props/P_Urne{rng.Next(3)}", G(R() * 2.4f - 1.2f, R() * 2.4f - 1.2f), R() * 360, parent, null, true);
                    break;
                case 3:
                    Place("Ville/M_Charrette", G(0, 0), yaw + R() * 30, parent, tint, true);
                    Place("Ville/M_Foin", G(2.2f, 0.5f), R() * 360, parent, tint, true);
                    Place("Ville/M_Tonneaux", G(-1.8f, 0.8f), R() * 360, parent, tint, true);
                    break;
                default:
                    Place("Ville/M_Banc", G(0, 0), yaw, parent, tint, true);
                    Place("Ville/M_Caisses", G(1.6f, 0.8f), R() * 360, parent, tint, true);
                    Place($"Props/P_Urne{rng.Next(3)}", G(-1.3f, 0.7f), R() * 360, parent, null, true);
                    break;
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
