using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Objet destructible (tonneaux, caisses, charrette, banc, table, tabouret, urne, botte de foin…) : un coup suffit.
    /// Il vole en éclats de façon crédible selon sa matière : douelles et cercles de fer pour un tonneau, planches et clous
    /// pour une caisse, plateau fendu et pieds pour une table, tessons pour une urne, brins pour le foin. Les morceaux
    /// partent à l'opposé du coup, rebondissent, roulent, puis s'enfoncent doucement dans le sol. Poussière, échardes et
    /// son de bris ; lâche parfois de l'or, plus rarement un objet, une potion ou une gemme. N'attire pas la visée
    /// automatique (ce n'est pas un monstre).</summary>
    public class Breakable : MonoBehaviour
    {
        public enum Kind { Auto, Barrel, Crate, Wood, Pottery, Hay }
        public static readonly List<Breakable> All = new List<Breakable>();
        public float radius = 0.9f;
        public Kind kind = Kind.Auto;
        bool broken;
        static int liveShards;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static void Make(GameObject go, float radius = 0.9f, Kind kind = Kind.Auto)
        {
            if (!go || go.GetComponent<Breakable>()) return;
            var b = go.AddComponent<Breakable>(); b.radius = radius; b.kind = kind;
        }

        /// <summary>Brise tout ce qui se trouve dans la zone (frappe, explosion, projectile).</summary>
        public static void HitArea(Vector3 c, float r, Vector3? fwd = null, float cone = 360)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i]; if (!b || b.broken || !b.gameObject.activeInHierarchy) continue;
                var d = b.transform.position - c; d.y = 0;
                if (d.magnitude > r + b.radius) continue;
                if (fwd.HasValue && cone < 360 && d.sqrMagnitude > 0.01f && Vector3.Angle(fwd.Value, d) > cone * 0.5f) continue;
                b.Break(c);
            }
        }

        public void Break() => Break(transform.position - transform.forward);

        Kind Resolve()
        {
            if (kind != Kind.Auto) return kind;
            var n = name;
            if (n.Contains("Tonneau")) return Kind.Barrel;
            if (n.Contains("Caisse")) return Kind.Crate;
            if (n.Contains("Urne") || n.Contains("Jarre")) return Kind.Pottery;
            if (n.Contains("Foin")) return Kind.Hay;
            return Kind.Wood;
        }

        public void Break(Vector3 from)
        {
            if (broken) return; broken = true;
            var k = Resolve();
            // volume réel de l'objet (rendus), matériau de base pour les éclats
            var rs = GetComponentsInChildren<Renderer>();
            Bounds bb = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in rs) if (!(r is ParticleSystemRenderer)) bb.Encapsulate(r.bounds);
            Material mat = null; foreach (var r in rs) if (r.sharedMaterial) { mat = r.sharedMaterial; break; }
            var p = bb.center; var push = p - from; push.y = 0; push = push.sqrMagnitude > 0.01f ? push.normalized : transform.forward;

            // poussière, échardes, son
            Vfx.Debris(p, 1.2f);
            var dust = k == Kind.Pottery ? new Color(0.7f, 0.55f, 0.42f) : k == Kind.Hay ? new Color(0.85f, 0.75f, 0.4f) : new Color(0.6f, 0.47f, 0.32f);
            FX.Burst(p, dust, 22, 4f, 0.12f, 0.7f, -1.2f);
            FX.Burst(bb.min + Vector3.up * 0.1f + new Vector3(bb.extents.x, 0, bb.extents.z), new Color(0.55f, 0.5f, 0.45f, 0.5f), 14, 1.6f, 0.35f, 1.2f, 0.2f);   // nuage au sol
            Sound.Play(k == Kind.Pottery ? "sfx_poterie_brise" : "sfx_bois_brise", p, 0.75f, 0.12f, 0.05f);
            Sound.Play("sfx_marteau_impact", p, 0.25f, 0.25f, 0.05f);

            Shatter(k, bb, mat, push);
            Loot(p);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------ éclats
        void Shatter(Kind k, Bounds bb, Material mat, Vector3 push)
        {
            if (liveShards > 160) return;                                        // garde-fou (gros combats)
            var iron = IronMat(); var c = bb.center; var e = bb.extents;
            switch (k)
            {
                case Kind.Barrel:
                {
                    // douelles (planches courbes en cercle), deux cercles de fer, le fond
                    int staves = 9; float rad = Mathf.Min(e.x, e.z) * 0.85f, h = e.y * 1.7f;
                    for (int i = 0; i < staves; i++)
                    {
                        float a = i * Mathf.PI * 2 / staves; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var len = h * Random.Range(0.45f, 1f);                                     // douelles cassées à des longueurs variées
                        Shard(c + dir * rad, Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-8f, 8f), 0, 0), new Vector3(rad * 0.65f, len, 0.05f), mat, (dir + push) * 3f + Vector3.up * 2.5f);
                    }
                    for (int i = 0; i < 2; i++) Shard(c + Vector3.up * (i == 0 ? -e.y * 0.5f : e.y * 0.5f), Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0, 360f), 0), new Vector3(rad * 2.1f, 0.04f, rad * 2.1f) * 0.5f, iron, push * 2f + Vector3.up * 3f, ring: true);
                    Shard(c, Quaternion.Euler(80, 0, 0), new Vector3(rad * 1.7f, rad * 1.7f, 0.05f), mat, push * 2.5f + Vector3.up * 2f);
                    break;
                }
                case Kind.Crate:
                {
                    // planches des faces (certaines brisées en deux), montants d'angle, clous
                    float s = Mathf.Max(e.x, e.z) * 1.6f;
                    for (int i = 0; i < 10; i++)
                    {
                        var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.6f;
                        float len = s * (Random.value < 0.4f ? Random.Range(0.35f, 0.6f) : Random.Range(0.8f, 1f));
                        Shard(c + Vector3.Scale(dir, e) * 0.7f, Random.rotation, new Vector3(len, 0.04f, Random.Range(0.12f, 0.2f)), mat, (dir + push) * 3.2f + Vector3.up * 2.4f);
                    }
                    for (int i = 0; i < 4; i++) { var dir = new Vector3(i < 2 ? -1 : 1, 0, i % 2 == 0 ? -1 : 1); Shard(c + Vector3.Scale(dir, e) * 0.8f, Random.rotation, new Vector3(0.07f, e.y * 1.6f, 0.07f), mat, (dir.normalized + push) * 2.5f + Vector3.up * 2f); }
                    for (int i = 0; i < 4; i++) Shard(c, Random.rotation, new Vector3(0.015f, 0.06f, 0.015f), iron, Random.insideUnitSphere * 2f + Vector3.up * 3f, small: true);
                    break;
                }
                case Kind.Pottery:
                {
                    // tessons courbes de tailles variées, col de l'urne parfois intact
                    for (int i = 0; i < 12; i++)
                    {
                        var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.5f;
                        float sz = Random.Range(0.06f, 0.18f) * Mathf.Max(1f, e.y);
                        Shard(c + Vector3.Scale(dir, e) * 0.6f, Random.rotation, new Vector3(sz, 0.025f, sz * Random.Range(0.6f, 1.3f)), mat, (dir + push) * 3.5f + Vector3.up * 2f);
                    }
                    if (Random.value < 0.5f) Shard(c + Vector3.up * e.y * 0.6f, Quaternion.identity, new Vector3(e.x * 0.7f, e.y * 0.35f, e.z * 0.7f), mat, push * 1.5f + Vector3.up * 2f);
                    break;
                }
                case Kind.Hay:
                    for (int i = 0; i < 16; i++)
                    {
                        var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y);
                        Shard(c + Vector3.Scale(dir, e) * 0.5f, Random.rotation, new Vector3(Random.Range(0.15f, 0.4f), Random.Range(0.08f, 0.2f), Random.Range(0.15f, 0.35f)), mat, (dir + push) * 2.2f + Vector3.up * 1.5f, light: true);
                    }
                    break;
                default:
                {
                    // bois (table, banc, tabouret, charrette, étal) : plateau fendu en morceaux, pieds, traverses, échardes
                    float span = Mathf.Max(e.x, e.z) * 2f;
                    int pieces = Mathf.Clamp(Mathf.RoundToInt(span * 4f), 6, 14);
                    for (int i = 0; i < pieces; i++)
                    {
                        var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.5f;
                        bool leg = i % 3 == 0;
                        var size = leg ? new Vector3(0.08f, Random.Range(0.3f, e.y * 1.4f + 0.1f), 0.08f) : new Vector3(Random.Range(0.25f, span * 0.45f), 0.05f, Random.Range(0.1f, 0.3f));
                        Shard(c + Vector3.Scale(dir, e) * 0.6f, Random.rotation, size, mat, (dir + push) * 3f + Vector3.up * 2.4f);
                    }
                    for (int i = 0; i < 6; i++) Shard(c, Random.rotation, new Vector3(0.12f, 0.02f, 0.02f), mat, Random.insideUnitSphere * 3f + Vector3.up * 3f, small: true);   // échardes
                    break;
                }
            }
        }

        static Material ironMat;
        static Material IronMat()
        {
            if (ironMat) return ironMat;
            var b = LootDrop.litBase; ironMat = b ? new Material(b) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ironMat.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.14f)); if (ironMat.HasProperty("_Metallic")) ironMat.SetFloat("_Metallic", 0.8f); if (ironMat.HasProperty("_Smoothness")) ironMat.SetFloat("_Smoothness", 0.35f);
            return ironMat;
        }

        static Mesh ringMesh;
        static Mesh Ring()
        {
            if (ringMesh) return ringMesh;
            int n = 20; var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * 1f + Vector3.up * 0.5f); v.Add(d * 1f - Vector3.up * 0.5f); v.Add(d * 0.9f + Vector3.up * 0.5f); v.Add(d * 0.9f - Vector3.up * 0.5f);
                if (i < n) { int b = i * 4; t.AddRange(new[] { b, b + 4, b + 1, b + 1, b + 4, b + 5, b + 2, b + 3, b + 6, b + 3, b + 7, b + 6, b, b + 2, b + 4, b + 2, b + 6, b + 4 }); }
            }
            ringMesh = new Mesh { name = "Cercle" }; ringMesh.SetVertices(v); ringMesh.SetTriangles(t, 0); ringMesh.RecalculateNormals(); return ringMesh;
        }

        void Shard(Vector3 pos, Quaternion rot, Vector3 size, Material mat, Vector3 vel, bool ring = false, bool small = false, bool light = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Éclat";
            if (ring) { g.GetComponent<MeshFilter>().sharedMesh = Ring(); Object.Destroy(g.GetComponent<Collider>()); g.AddComponent<BoxCollider>().size = new Vector3(2f, 1f, 2f); }
            g.transform.position = pos; g.transform.rotation = rot; g.transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
            var mr = g.GetComponent<MeshRenderer>(); if (mat) mr.sharedMaterial = mat; mr.shadowCastingMode = small ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            g.layer = 2;                                                                   // « Ignore Raycast » : la visée et les clics les ignorent
            var rb = g.AddComponent<Rigidbody>(); rb.mass = light ? 0.1f : small ? 0.05f : 0.6f; rb.linearDamping = light ? 2.5f : 0.2f; rb.angularDamping = 0.4f;
            rb.linearVelocity = vel * Random.Range(0.75f, 1.25f); rb.angularVelocity = Random.insideUnitSphere * (light ? 3f : 12f);
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete; rb.interpolation = RigidbodyInterpolation.Interpolate;
            g.AddComponent<Fragment>().life = Random.Range(5f, 8f);
        }

        void Loot(Vector3 p)
        {
            var hero = FindFirstObjectByType<PlayerController>(); int lvl = hero ? hero.level : 1; var rng = new System.Random();
            float roll = Random.value;
            if (roll < 0.35f) LootDrop.SpawnGold((long)(Economy.GoldDrop(rng, lvl, 0) * 0.6f) + 2, LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.40f) LootDrop.Spawn(Korvalune.Game.Loot.Roll(rng, lvl, 0.5f), LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.44f) GroundPickup.SpawnPotion(false, LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.48f) GroundPickup.SpawnPotion(true, LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.50f) GroundPickup.SpawnGem(Economy.GemKinds[Random.Range(0, Economy.GemKinds.Length)], 1, LootDrop.Near(p, Random.insideUnitSphere));
        }

        /// <summary>Morceau d'objet brisé : la physique le fait rebondir et rouler, puis il se fige, s'enfonce dans le sol et disparaît.</summary>
        class Fragment : MonoBehaviour
        {
            public float life = 6f; float t; bool frozen; Vector3 scale0;
            void Start() { liveShards++; scale0 = transform.localScale; }
            void OnDestroy() { liveShards--; }
            void Update()
            {
                t += Time.deltaTime;
                if (!frozen && t > 2.5f)
                {
                    frozen = true;
                    var rb = GetComponent<Rigidbody>(); if (rb) Destroy(rb);
                    var c = GetComponent<Collider>(); if (c) Destroy(c);
                }
                if (t > life - 1.5f)
                {
                    float k = Mathf.Clamp01((t - (life - 1.5f)) / 1.5f);
                    transform.position += Vector3.down * Time.deltaTime * 0.25f;            // s'enfonce doucement
                    transform.localScale = scale0 * (1f - 0.6f * k);
                }
                if (t > life) Destroy(gameObject);
            }
        }
    }
}
