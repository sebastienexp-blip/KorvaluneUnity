using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Korvalune.Game
{
    /// <summary>Garde le héros visible : les arbres, rochers et murs placés entre la caméra et lui deviennent invisibles
    /// (ils gardent leur ombre et leur collision) puis réapparaissent dès qu'ils ne gênent plus.
    /// Arbres du terrain : retirés du terrain et remplacés par une copie « ombre seule » tant qu'ils masquent la vue.</summary>
    [RequireComponent(typeof(Camera))]
    public class Occluders : MonoBehaviour
    {
        public Transform target;
        public float lookHeight = 1.0f, radius = 1.1f, treeMargin = 1.6f, period = 0.2f;

        float next;
        readonly Dictionary<Renderer, ShadowCastingMode> hidden = new Dictionary<Renderer, ShadowCastingMode>();
        readonly HashSet<Renderer> seen = new HashSet<Renderer>();
        readonly RaycastHit[] hits = new RaycastHit[24];

        // arbres du terrain
        Terrain terrain; TreeInstance[] original; Vector3[] treePos; float[] treeTop, treeR;
        readonly Dictionary<int, GameObject> treeProxies = new Dictionary<int, GameObject>();
        readonly HashSet<int> wantTrees = new HashSet<int>();
        bool treesDirty;

        void Start() { CacheTrees(); }

        void CacheTrees()
        {
            terrain = Terrain.activeTerrain;
            if (!terrain || !terrain.terrainData || terrain.terrainData.treeInstanceCount == 0) { terrain = null; return; }
            var td = terrain.terrainData; original = td.treeInstances;
            var protos = td.treePrototypes; var protoH = new float[protos.Length]; var protoR = new float[protos.Length];
            for (int p = 0; p < protos.Length; p++)
            {
                protoH[p] = 12f; protoR[p] = 3f;
                if (!protos[p].prefab) continue;
                var tmp = Instantiate(protos[p].prefab); var rs = tmp.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    protoH[p] = b.max.y - tmp.transform.position.y; protoR[p] = Mathf.Max(b.extents.x, b.extents.z);
                }
                Destroy(tmp);
            }
            var o = terrain.transform.position; var s = td.size;
            treePos = new Vector3[original.Length]; treeTop = new float[original.Length]; treeR = new float[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                var t = original[i];
                treePos[i] = o + Vector3.Scale(t.position, s);
                treeTop[i] = treePos[i].y + protoH[t.prototypeIndex] * t.heightScale;
                treeR[i] = protoR[t.prototypeIndex] * t.widthScale;
            }
        }

        void LateUpdate()
        {
            if (!target || Time.unscaledTime < next) return;
            next = Time.unscaledTime + period;
            var cam = transform.position; var focus = target.position + Vector3.up * lookHeight;
            var dir = focus - cam; float dist = dir.magnitude; if (dist < 0.5f) return;
            dir /= dist;

            // objets (rochers, murs, maisons) : tout ce que traverse la ligne de vue, sauf le sol, les créatures et le héros
            seen.Clear();
            int n = Physics.SphereCastNonAlloc(cam, radius, dir, hits, dist - 1.2f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c is TerrainCollider || c.GetComponentInParent<Health>() || c.transform.IsChildOf(target)) continue;
                // le collider est souvent posé à part du maillage visible (pont, arche, porte) : on remonte jusqu'à deux parents
                // et on ne masque que les morceaux réellement traversés par la ligne de vue
                var from = c.transform; for (int up = 0; up < 2 && from.parent && from.GetComponentsInChildren<Renderer>().Length == 0; up++) from = from.parent;
                if (from.GetComponentsInChildren<Renderer>().Length == 0 && from.parent) from = from.parent;
                var ray = new Ray(cam, dir);
                foreach (var r in from.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                    var bb = r.bounds; bb.Expand(radius * 2f);
                    if (!bb.IntersectRay(ray, out var at) || at > dist) continue;
                    seen.Add(r);
                    if (!hidden.ContainsKey(r)) { hidden[r] = r.shadowCastingMode; r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
                }
            }
            if (hidden.Count > seen.Count)
            {
                var back = new List<Renderer>();
                foreach (var kv in hidden) if (!seen.Contains(kv.Key)) back.Add(kv.Key);
                foreach (var r in back) { if (r) r.shadowCastingMode = hidden[r]; hidden.Remove(r); }
            }

            if (terrain) UpdateTrees(cam, focus);
        }

        void UpdateTrees(Vector3 cam, Vector3 focus)
        {
            wantTrees.Clear();
            var a = new Vector2(focus.x, focus.z); var b = new Vector2(cam.x, cam.z); var ab = b - a; float len2 = Mathf.Max(0.01f, ab.sqrMagnitude);
            float reach = Mathf.Sqrt(len2) + 6f;
            for (int i = 0; i < treePos.Length; i++)
            {
                var p = new Vector2(treePos[i].x, treePos[i].z);
                if ((p - a).sqrMagnitude > reach * reach) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                if (t <= 0f) { if ((p - a).magnitude > treeR[i] * 0.6f) continue; }   // derrière le héros : ne gêne pas
                float d = (p - (a + ab * t)).magnitude;
                if (d > treeR[i] + treeMargin) continue;
                float lineY = Mathf.Lerp(focus.y, cam.y, t);
                if (treeTop[i] < lineY - 0.5f) continue;                             // la ligne de vue passe au-dessus
                wantTrees.Add(i);
            }
            bool changed = wantTrees.Count != treeProxies.Count;
            if (!changed) foreach (var i in wantTrees) if (!treeProxies.ContainsKey(i)) { changed = true; break; }
            if (!changed) return;

            var drop = new List<int>();
            foreach (var kv in treeProxies) if (!wantTrees.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var i in drop) { Destroy(treeProxies[i]); treeProxies.Remove(i); }
            var protos = terrain.terrainData.treePrototypes;
            foreach (var i in wantTrees)
            {
                if (treeProxies.ContainsKey(i)) continue;
                var t = original[i]; var pf = protos[t.prototypeIndex].prefab; if (!pf) continue;
                var g = Instantiate(pf, treePos[i], Quaternion.Euler(0, t.rotation * Mathf.Rad2Deg, 0));
                g.transform.localScale = new Vector3(t.widthScale, t.heightScale, t.widthScale);
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                treeProxies[i] = g;
            }
            var keep = new List<TreeInstance>(original.Length);
            for (int i = 0; i < original.Length; i++) if (!treeProxies.ContainsKey(i)) keep.Add(original[i]);
            terrain.terrainData.SetTreeInstances(keep.ToArray(), false);
            treesDirty = true;
        }

        void Restore()
        {
            foreach (var kv in hidden) if (kv.Key) kv.Key.shadowCastingMode = kv.Value;
            hidden.Clear();
            if (terrain && treesDirty && original != null) { terrain.terrainData.SetTreeInstances(original, false); treesDirty = false; }
            foreach (var g in treeProxies.Values) if (g) Destroy(g);
            treeProxies.Clear();
        }

        void OnDisable() => Restore();
        void OnApplicationQuit() => Restore();
    }
}
