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
