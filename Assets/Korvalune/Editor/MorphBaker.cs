using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Construit la MorphMap d'un personnage à l'import : pour chaque sommet de chaque pièce, les 4 sommets du corps
    /// de base les plus proches (table « KMT1 » de ModelForge) et leurs poids. Détecte la convention d'axes et l'échelle
    /// entre ModelForge et Unity en comparant la peau importée aux positions de la table.</summary>
    public static class MorphBaker
    {
        public static string TablePath(string dir, string prefabName) => $"{dir}/{prefabName}_Morphs.bytes";

        public static MorphRig Attach(GameObject go, string dir, string prefabName)
        {
            var tpath = TablePath(dir, prefabName);
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(tpath);
            if (!ta) return null;
            if (!MorphRig.ReadTable(ta.bytes, out var P, out var names)) { Debug.LogError("[Morphologie] table illisible : " + tpath); return null; }
            var root = go.transform; var toRoot = root.worldToLocalMatrix;
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh).ToList();
            // ---- convention d'axes et échelle : la peau importée doit coïncider avec les positions de la table
            var skin = smrs.Where(s => s.name.ToLower().Contains("peau")).ToList(); if (skin.Count == 0) skin = smrs;
            var probe = new List<Vector3>();
            foreach (var s in skin) { var m = toRoot * s.transform.localToWorldMatrix; var v = s.sharedMesh.vertices; for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 60)) probe.Add(m.MultiplyPoint3x4(v[i])); }
            Vector3 bestAx = Vector3.one; float bestS = 1, bestErr = float.MaxValue;
            foreach (var s in new[] { 1f, 0.01f, 100f })
                foreach (var ax in new[] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1) })
                {
                    var g = new Grid(P.Select(p => Vector3.Scale(p, ax) * s).ToArray(), 0.05f);
                    float err = probe.Take(400).Select(q => g.Nearest(q, 1)[0].d).OrderBy(x => x).ElementAt(Mathf.Min(probe.Count, 400) / 2);
                    if (err < bestErr) { bestErr = err; bestAx = ax; bestS = s; }
                }
            var T = P.Select(p => Vector3.Scale(p, bestAx) * bestS).ToArray();
            var grid = new Grid(T, 0.04f);
            var map = ScriptableObject.CreateInstance<MorphMap>();
            map.axis = bestAx; map.scale = bestS; map.bodyVertices = P.Length;
            foreach (var s in smrs)
            {
                var m = toRoot * s.transform.localToWorldMatrix; var inv = m.inverse;
                var v = s.sharedMesh.vertices; int K = MorphMap.K;
                var part = new MorphMap.Part
                {
                    path = AnimationUtility.CalculateTransformPath(s.transform, root), vertexCount = v.Length,
                    idx = new ushort[v.Length * K], w = new byte[v.Length * K],
                    c0 = inv.MultiplyVector(Vector3.right), c1 = inv.MultiplyVector(Vector3.up), c2 = inv.MultiplyVector(Vector3.forward)
                };
                for (int i = 0; i < v.Length; i++)
                {
                    var q = m.MultiplyPoint3x4(v[i]);
                    var nn = grid.Nearest(q, K);
                    float sum = 0; var ws = new float[nn.Count];
                    for (int k = 0; k < nn.Count; k++) { ws[k] = 1f / Mathf.Pow(nn[k].d + 2e-4f, 2); sum += ws[k]; }
                    int left = 255;
                    for (int k = 0; k < nn.Count && k < K; k++)
                    {
                        int b = k == nn.Count - 1 ? left : Mathf.Clamp(Mathf.RoundToInt(ws[k] / sum * 255f), 0, left);
                        part.idx[i * K + k] = (ushort)nn[k].i; part.w[i * K + k] = (byte)b; left -= b;
                    }
                }
                map.parts.Add(part);
            }
            var mpath = $"{dir}/{prefabName}_MorphMap.asset";
            AssetDatabase.DeleteAsset(mpath); AssetDatabase.CreateAsset(map, mpath);
            var rig = go.AddComponent<MorphRig>(); rig.table = ta; rig.map = map;
            Debug.Log($"[Morphologie] {prefabName} : {names.Length} formes, {P.Length} sommets du corps, {smrs.Count} pièces, axes {bestAx} × {bestS}, écart peau {bestErr * 1000:0.00} mm");
            return rig;
        }

        /// <summary>Grille de hachage pour les plus proches voisins (positions du corps).</summary>
        class Grid
        {
            readonly Vector3[] P; readonly float h; readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            static long Key(int x, int y, int z) => ((long)(x + 100000) * 200003L + (y + 100000)) * 200003L + (z + 100000);
            public Grid(Vector3[] p, float cell)
            {
                P = p; h = cell;
                for (int i = 0; i < p.Length; i++)
                {
                    long k = Key(Mathf.FloorToInt(p[i].x / h), Mathf.FloorToInt(p[i].y / h), Mathf.FloorToInt(p[i].z / h));
                    if (!cells.TryGetValue(k, out var l)) cells[k] = l = new List<int>(); l.Add(i);
                }
            }
            public List<(int i, float d)> Nearest(Vector3 q, int k)
            {
                int cx = Mathf.FloorToInt(q.x / h), cy = Mathf.FloorToInt(q.y / h), cz = Mathf.FloorToInt(q.z / h);
                var found = new List<(int i, float d)>();
                for (int r = 1; r <= 8; r++)
                {
                    found.Clear();
                    for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++)
                        if (cells.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) foreach (var i in l) found.Add((i, (P[i] - q).magnitude));
                    if (found.Count >= k && found.OrderBy(f => f.d).ElementAt(k - 1).d <= r * h) break;
                }
                if (found.Count < k) found = Enumerable.Range(0, P.Length).Select(i => (i, (P[i] - q).magnitude)).ToList();
                return found.OrderBy(f => f.d).Take(k).ToList();
            }
        }
    }
}
