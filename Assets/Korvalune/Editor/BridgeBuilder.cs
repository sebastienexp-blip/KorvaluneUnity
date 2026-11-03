using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Ponts de bois détaillés, construits pièce par pièce puis fusionnés en quelques maillages (un par matériau) :
    /// tablier de planches posées une à une (légers décalages, jours entre les planches, planches abîmées ou manquantes
    /// au bord), trois longerons qui suivent la légère cambrure, piles doublées plantées dans le lit avec chapeaux et
    /// croix de Saint-André, garde-corps (poteaux, lisses haute et basse, écharpes), ferrures aux assemblages, lanternes
    /// aux deux bouts. UV « au mètre » : le grain du bois garde la même taille sur chaque pièce.
    /// </summary>
    public static class BridgeBuilder
    {
        const string Root = ModelPipeline.Root;

        public class Mesher
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
            /// <summary>Pavé de dimensions « size » (m) centré en « c », orienté par « rot » ; UV au mètre (« tex » m par répétition),
            /// le fil du bois suit la plus grande dimension.</summary>
            public void Box(Vector3 c, Quaternion rot, Vector3 size, float tex = 1.2f)
            {
                var h = size * 0.5f;
                void Face(Vector3 nrm, Vector3 u, Vector3 w, float du, float dw)
                {
                    int b = v.Count; var center = nrm;
                    var corners = new[] { center - u - w, center + u - w, center + u + w, center - u + w };
                    var uvs = new[] { new Vector2(0, 0), new Vector2(du, 0), new Vector2(du, dw), new Vector2(0, dw) };
                    bool swap = dw > du;                                                   // fil du bois le long du plus grand côté
                    for (int k = 0; k < 4; k++)
                    {
                        v.Add(c + rot * Vector3.Scale(corners[k], h)); n.Add(rot * nrm);
                        var q = uvs[k] / tex; uv.Add(swap ? new Vector2(q.y, q.x) : q);
                    }
                    t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
                }
                Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
                Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
                Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
                Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
                Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
                Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            }
            /// <summary>Poutre entre deux points (section w × h).</summary>
            public void Beam(Vector3 a, Vector3 b, float w, float h, float roll = 0f)
            {
                var d = b - a; if (d.sqrMagnitude < 1e-4f) return;
                var dn = d.normalized; Box((a + b) / 2, Quaternion.LookRotation(dn, Mathf.Abs(dn.y) > 0.98f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0, 0, roll), new Vector3(w, h, d.magnitude));
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
            public bool Empty => v.Count == 0;
        }

        static Material Mat(string name, string tex, Color tint)
        {
            var m = WorldBuilder.GateMaterial(name, tex, 1f);
            if (m) { m.SetColor("_BaseColor", tint); m.SetTextureScale("_BaseMap", Vector2.one); EditorUtility.SetDirty(m); }
            return m ? m : WorldBuilder.StoneMaterial();
        }

        /// <summary>Pont de « a » à « b » (bouts posés sur les berges), « width » m de large, piles jusqu'au lit « bedY ».</summary>
        public static GameObject Build(string name, Vector3 a, Vector3 b, float width, float bedY, Transform parent, int seed)
        {
            var rng = new System.Random(seed); float R() => (float)rng.NextDouble();
            var root = new GameObject(name); if (parent) root.transform.SetParent(parent);
            var flat = b - a; flat.y = 0; float L = flat.magnitude; var fwd = flat / L; var right = Vector3.Cross(Vector3.up, fwd);
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            float y0 = Mathf.Max(a.y, b.y) - 0.03f, arch = Mathf.Min(1.2f, L * 0.03f);
            Vector3 P(float s, float x, float y) { float k = Mathf.Clamp01(s / L); return a + fwd * s + right * x + Vector3.up * (y0 - a.y + y + arch * Mathf.Sin(Mathf.PI * k)); }
            float hw = width / 2;
            var planks = new Mesher(); var timber = new Mesher(); var iron = new Mesher();

            // ---- tablier : planches transversales
            for (float s = 0.2f; s < L - 0.1f; s += 0.32f + R() * 0.03f)
            {
                bool edge = s < 1.2f || s > L - 1.2f;
                if (!edge && R() < 0.025f) continue;                                         // planche manquante
                float len = width + 0.25f - (R() < 0.12f ? R() * 0.9f : 0f);                     // planche plus courte, cassée
                float off = (R() - 0.5f) * 0.12f;
                var c = P(s, off, 0.04f + (R() - 0.5f) * 0.02f);
                planks.Box(c, rot * Quaternion.Euler((R() - 0.5f) * 1.2f, (R() - 0.5f) * 2.2f, (R() - 0.5f) * 1.2f), new Vector3(len, 0.07f, 0.28f + R() * 0.03f), 1.0f);
                // clous (deux par longeron extérieur)
                foreach (var x in new[] { -hw + 0.35f, hw - 0.35f }) iron.Box(P(s, x, 0.085f), rot, new Vector3(0.035f, 0.01f, 0.035f));
            }
            // ---- longerons (3), par tronçons de 2 m qui suivent la cambrure
            foreach (var x in new[] { -hw + 0.35f, 0f, hw - 0.35f })
                for (float s = 0; s < L; s += 2f) timber.Beam(P(s, x, -0.18f), P(Mathf.Min(L, s + 2f), x, -0.18f), 0.26f, 0.32f);
            // ---- piles doublées dans le lit, chapeau, croix de Saint-André, ferrures
            int bents = Mathf.Max(2, Mathf.RoundToInt(L / 5f));
            for (int i = 1; i < bents; i++)
            {
                float s = L * i / bents;
                var top = P(s, 0, -0.36f);
                foreach (var x in new[] { -hw - 0.05f, hw + 0.05f })
                {
                    var tp = P(s, x, -0.36f); var bt = new Vector3(tp.x, bedY - 1.2f, tp.z);
                    timber.Beam(bt, tp, 0.32f, 0.32f, R() * 20f);
                    iron.Beam(tp + Vector3.down * 0.35f, tp + Vector3.down * 0.5f, 0.36f, 0.36f);   // frette de fer
                }
                timber.Beam(P(s, -hw - 0.35f, -0.3f), P(s, hw + 0.35f, -0.3f), 0.3f, 0.28f);              // chapeau
                float lowY = Mathf.Max(bedY + 0.4f, top.y - 3.2f);
                var l1 = P(s, -hw - 0.05f, 0); var r1 = P(s, hw + 0.05f, 0);
                timber.Beam(new Vector3(l1.x, top.y - 0.4f, l1.z), new Vector3(r1.x, lowY, r1.z), 0.16f, 0.16f);
                timber.Beam(new Vector3(r1.x, top.y - 0.4f, r1.z), new Vector3(l1.x, lowY, l1.z), 0.16f, 0.16f);
            }
            // ---- garde-corps : poteaux, lisses, écharpes
            float postGap = 2.4f;
            foreach (var side in new[] { -1f, 1f })
            {
                float x = side * (hw - 0.08f);
                for (float s = 0.15f; s <= L - 0.1f; s += postGap)
                {
                    var bottom = P(s, x, 0.05f);
                    timber.Box(bottom + Vector3.up * 0.55f, rot * Quaternion.Euler(0, R() * 4 - 2, (R() - 0.5f) * 2f), new Vector3(0.16f, 1.12f, 0.16f));
                    iron.Box(bottom + Vector3.up * 0.25f + right * side * 0.09f, rot, new Vector3(0.02f, 0.18f, 0.2f));   // équerre
                    float s2 = Mathf.Min(L - 0.1f, s + postGap);
                    if (s2 - s < 0.5f) continue;
                    timber.Beam(P(s, x, 1.08f), P(s2, x, 1.08f), 0.13f, 0.11f);                           // main courante
                    timber.Beam(P(s, x, 0.55f), P(s2, x, 0.55f), 0.09f, 0.09f);                           // lisse basse
                    if (((int)(s / postGap)) % 2 == 0) timber.Beam(P(s, x, 0.12f), P(s2, x, 1.0f), 0.07f, 0.07f);   // écharpe
                }
            }
            // ---- culées : gros blocs de pierre sous les extrémités
            var stoneParts = new List<GameObject>();
            foreach (var s in new[] { 0.6f, L - 0.6f })
            {
                var c = P(s, 0, -0.9f);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Rocher{rng.Next(3)}.prefab");
                if (!pf) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                go.transform.position = c; go.transform.rotation = rot; go.transform.localScale = new Vector3(width + 1.2f, 1.6f, 2.2f); go.isStatic = true;
            }

            void Emit(Mesher m, string part, Material mat, bool collide)
            {
                if (m.Empty) return;
                var mesh = m.ToMesh($"{name}_{part}");
                var path = $"{Root}/Data/Pont_{name.Replace(' ', '_').Replace("'", "")}_{part}.asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(part); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.isStatic = true;
            }
            Emit(planks, "Planches", Mat("Pont_Planches", "Ville/M_Maison0/Textures/W_Wood", new Color(0.92f, 0.86f, 0.78f)), true);
            Emit(timber, "Charpente", Mat("Pont_Charpente", "Ville/M_Maison0/Textures/T_Timber", new Color(0.85f, 0.8f, 0.74f)), true);
            Emit(iron, "Ferrures", Mat("Pont_Fer", "Ville/M_Maison0/Textures/T_Iron", Color.white), false);

            // lanternes aux deux bouts
            var lan = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Prefabs/World/Ville/M_Lanterne.prefab");
            if (lan)
                foreach (var (s, side) in new[] { (0.2f, 1f), (L - 0.2f, -1f) })
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(lan, root.transform);
                    go.transform.position = P(s, side * (hw + 0.6f), 0) + Vector3.down * 0.2f; go.isStatic = true;
                }
            return root;
        }

        /// <summary>Croisements route / rivière de la vallée : un pont de bois à chacun (point, direction de la route).</summary>
        public static List<(Vector2 p, Vector2 dir)> Crossings(Vector2[][] roads, Vector2[] river)
        {
            var res = new List<(Vector2, Vector2)>();
            foreach (var road in roads)
                for (int i = 0; i < road.Length - 1; i++)
                    for (int j = 0; j < river.Length - 1; j++)
                        if (Intersect(road[i], road[i + 1], river[j], river[j + 1], out var hit))
                        {
                            bool dup = false; foreach (var (q, _) in res) if ((q - hit).sqrMagnitude < 25f * 25f) dup = true;
                            if (!dup) res.Add((hit, (road[i + 1] - road[i]).normalized));
                        }
            return res;
        }

        static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = Vector2.zero; var r = b - a; var s = d - c; float den = r.x * s.y - r.y * s.x; if (Mathf.Abs(den) < 1e-6f) return false;
            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den, u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
            if (t < 0 || t > 1 || u < 0 || u > 1) return false;
            hit = a + r * t; return true;
        }
    }
}
