using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Korvalune
{
    /// <summary>
    /// Morphologie sans blendshapes : la table « KMT1 » (ModelForge) ne contient les déplacements que des sommets du corps
    /// de base, une seule fois par curseur ; chaque pièce suit le corps par interpolation (MorphMap). Les 123 curseurs sont
    /// conservés pour un coût minime (quelques Mo au lieu de centaines par héros). Après réglage, le maillage final est
    /// calculé une fois : en jeu, le héros ne coûte pas plus qu'un modèle normal.
    /// </summary>
    [ExecuteAlways]
    public class MorphRig : MonoBehaviour
    {
        public TextAsset table;
        public MorphMap map;

        // table décodée
        string[] names; Dictionary<string, int> index; int nb; int dataOffset; byte[] raw;
        readonly Dictionary<int, float[]> decoded = new Dictionary<int, float[]>();
        readonly Dictionary<int, float> weights = new Dictionary<int, float>();
        float[] body;                            // déplacement cumulé de chaque sommet du corps (x, y, z)
        // pièces
        class Live { public SkinnedMeshRenderer smr; public Mesh src, mesh; public Vector3[] baseV, outV; public MorphMap.Part part; }
        List<Live> live; bool dirty;

        public bool Ready => table && map && Load();

        bool Load()
        {
            if (raw != null) return true;
            raw = table.bytes;
            if (raw.Length < 12 || Encoding.ASCII.GetString(raw, 0, 4) != "KMT1") { raw = null; return false; }
            nb = BitConverter.ToInt32(raw, 4); int ns = BitConverter.ToInt32(raw, 8); int o = 12;
            names = new string[ns]; index = new Dictionary<string, int>();
            for (int i = 0; i < ns; i++) { int l = BitConverter.ToInt32(raw, o); o += 4; names[i] = Encoding.UTF8.GetString(raw, o, l); o += l; index[names[i]] = i; }
            o += nb * 12;                         // positions (utiles à l'import seulement)
            dataOffset = o;
            body = new float[nb * 3];
            return true;
        }

        float[] Shape(int s)
        {
            if (decoded.TryGetValue(s, out var d)) return d;
            d = new float[nb * 3]; int o = dataOffset + s * nb * 6;
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.HalfToFloat(BitConverter.ToUInt16(raw, o + i * 2));
            decoded[s] = d; return d;
        }

        public bool Has(string shape) => Ready && index.ContainsKey(shape);

        /// <summary>Règle le poids (0..1) d'une forme (« id_plus » / « id_moins ») ; appliquer avec Flush().</summary>
        public void SetWeight(string shape, float w)
        {
            if (!Ready || !index.TryGetValue(shape, out int s)) return;
            weights.TryGetValue(s, out float old);
            float dw = w - old; if (Mathf.Abs(dw) < 1e-5f) return;
            var d = Shape(s);
            for (int i = 0; i < body.Length; i++) body[i] += dw * d[i];
            if (Mathf.Abs(w) < 1e-6f) { weights.Remove(s); decoded.Remove(s); } else weights[s] = w;
            dirty = true;
        }

        /// <summary>Recalcule les sommets de toutes les pièces à partir du déplacement du corps.</summary>
        public void Flush()
        {
            if (!dirty || !Ready) return;
            dirty = false;
            if (live == null) Bind();
            var ax = map.axis * map.scale;
            foreach (var l in live)
            {
                if (!l.smr) continue;
                var p = l.part; int n = Mathf.Min(p.vertexCount, l.baseV.Length);
                for (int v = 0; v < n; v++)
                {
                    float x = 0, y = 0, z = 0; int b = v * MorphMap.K;
                    for (int k = 0; k < MorphMap.K; k++)
                    {
                        float w = p.w[b + k] * (1f / 255f); if (w <= 0) continue;
                        int j = p.idx[b + k] * 3; x += w * body[j]; y += w * body[j + 1]; z += w * body[j + 2];
                    }
                    x *= ax.x; y *= ax.y; z *= ax.z;
                    l.outV[v] = l.baseV[v] + p.c0 * x + p.c1 * y + p.c2 * z;
                }
                l.mesh.SetVertices(l.outV); l.mesh.RecalculateBounds();
            }
            live.RemoveAll(l => { if (l.smr) return false; if (l.mesh) { if (Application.isPlaying) Destroy(l.mesh); else DestroyImmediate(l.mesh); } return true; });
        }

        // ------------------------------------------------------------------ pièces ajoutées en cours de route (créateur)
        Vector3[] bodyT; Dictionary<long, List<int>> grid; const float Cell = 0.04f;
        static long Key(int x, int y, int z) => ((long)(x + 100000) * 200003L + (y + 100000)) * 200003L + (z + 100000);

        /// <summary>Fait suivre la morphologie à une pièce rapportée après l'import (cheveux, sourcils… du catalogue) :
        /// la correspondance avec le corps est calculée sur place, comme MorphBaker le fait à l'import.</summary>
        public void Track(SkinnedMeshRenderer smr)
        {
            if (!smr || !smr.sharedMesh || !Ready) return;
            if (live == null) Bind();
            foreach (var x in live) if (x.smr == smr) return;
            if (bodyT == null)
            {
                Vector3[] P; MorphRig.ReadTable(raw, out P, out _);
                bodyT = new Vector3[P.Length]; grid = new Dictionary<long, List<int>>();
                for (int i = 0; i < P.Length; i++)
                {
                    var t = Vector3.Scale(P[i], map.axis) * map.scale; bodyT[i] = t;
                    long k = Key(Mathf.FloorToInt(t.x / Cell), Mathf.FloorToInt(t.y / Cell), Mathf.FloorToInt(t.z / Cell));
                    if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(); l.Add(i);
                }
            }
            var src = smr.sharedMesh; var v = src.vertices; int K = MorphMap.K;
            var m = transform.worldToLocalMatrix * smr.transform.localToWorldMatrix; var inv = m.inverse;
            var part = new MorphMap.Part
            {
                path = smr.name, vertexCount = v.Length, idx = new ushort[v.Length * K], w = new byte[v.Length * K],
                c0 = inv.MultiplyVector(Vector3.right), c1 = inv.MultiplyVector(Vector3.up), c2 = inv.MultiplyVector(Vector3.forward)
            };
            var bi = new int[K]; var bd = new float[K];
            for (int i = 0; i < v.Length; i++)
            {
                var q = m.MultiplyPoint3x4(v[i]);
                int cx = Mathf.FloorToInt(q.x / Cell), cy = Mathf.FloorToInt(q.y / Cell), cz = Mathf.FloorToInt(q.z / Cell);
                for (int k = 0; k < K; k++) { bi[k] = -1; bd[k] = float.MaxValue; }
                for (int r = 1; r <= 8; r++)
                {
                    for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != r && r > 1) continue;   // coquille seulement
                        if (!grid.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) continue;
                        foreach (int j in l)
                        {
                            float d = (bodyT[j] - q).sqrMagnitude; if (d >= bd[K - 1]) continue;
                            int s = K - 1; while (s > 0 && bd[s - 1] > d) { bd[s] = bd[s - 1]; bi[s] = bi[s - 1]; s--; }
                            bd[s] = d; bi[s] = j;
                        }
                    }
                    if (bi[K - 1] >= 0 && Mathf.Sqrt(bd[K - 1]) <= (r - 1) * Cell) break;   // plus aucun voisin plus proche possible
                }
                if (bi[0] < 0) continue;
                float sum = 0; var ws = new float[K];
                for (int k = 0; k < K; k++) if (bi[k] >= 0) { ws[k] = 1f / Mathf.Pow(Mathf.Sqrt(bd[k]) + 2e-4f, 2); sum += ws[k]; }
                int left = 255, last = K - 1; while (last > 0 && bi[last] < 0) last--;
                for (int k = 0; k <= last; k++)
                {
                    int b = k == last ? left : Mathf.Clamp(Mathf.RoundToInt(ws[k] / sum * 255f), 0, left);
                    part.idx[i * K + k] = (ushort)Mathf.Max(0, bi[k]); part.w[i * K + k] = (byte)b; left -= b;
                }
            }
            var lv = new Live { smr = smr, src = src, part = part, mesh = Instantiate(src) };
            lv.mesh.name = src.name + " (morphologie)"; lv.baseV = v; lv.outV = (Vector3[])v.Clone();
            smr.sharedMesh = lv.mesh; live.Add(lv); dirty = true;
        }

        void Bind()
        {
            live = new List<Live>();
            foreach (var p in map.parts)
            {
                var t = transform.Find(p.path); var smr = t ? t.GetComponent<SkinnedMeshRenderer>() : null;
                if (!smr || !smr.sharedMesh) continue;
                var src = smr.sharedMesh;
                var l = new Live { smr = smr, src = src, part = p, mesh = Instantiate(src) };
                l.mesh.name = src.name + " (morphologie)";
                l.baseV = src.vertices; l.outV = (Vector3[])l.baseV.Clone();
                smr.sharedMesh = l.mesh;
                live.Add(l);
            }
        }

        /// <summary>Remet les maillages d'origine (aucune morphologie).</summary>
        public void ResetAll()
        {
            if (live != null) foreach (var l in live) if (l.smr) l.smr.sharedMesh = l.src;
            live = null; weights.Clear(); decoded.Clear(); if (body != null) Array.Clear(body, 0, body.Length); dirty = false;
        }

        /// <summary>Libère les formes décodées (le maillage final reste) : à appeler une fois la morphologie fixée en jeu.</summary>
        public void Freeze() { Flush(); decoded.Clear(); }

        void LateUpdate() { if (dirty) Flush(); }

        void OnDestroy() { if (live != null) foreach (var l in live) if (l.mesh) { if (Application.isPlaying) Destroy(l.mesh); else DestroyImmediate(l.mesh); } }

        // ---------------------------------------------------------------------------------------------- lecture pour l'import
        public static bool ReadTable(byte[] bytes, out Vector3[] positions, out string[] shapeNames)
        {
            positions = null; shapeNames = null;
            if (bytes == null || bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "KMT1") return false;
            int n = BitConverter.ToInt32(bytes, 4), ns = BitConverter.ToInt32(bytes, 8), o = 12;
            shapeNames = new string[ns];
            for (int i = 0; i < ns; i++) { int l = BitConverter.ToInt32(bytes, o); o += 4; shapeNames[i] = Encoding.UTF8.GetString(bytes, o, l); o += l; }
            positions = new Vector3[n];
            for (int i = 0; i < n; i++) positions[i] = new Vector3(BitConverter.ToSingle(bytes, o + i * 12), BitConverter.ToSingle(bytes, o + i * 12 + 4), BitConverter.ToSingle(bytes, o + i * 12 + 8));
            return true;
        }
    }
}
