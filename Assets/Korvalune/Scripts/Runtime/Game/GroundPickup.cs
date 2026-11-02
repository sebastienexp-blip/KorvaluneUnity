using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Petit butin modélisé au sol, ramassé en marchant dessus : gemme taillée (couronne + pavillon à facettes,
    /// brillante, qui tourne doucement), rune (galet de pierre sombre, glyphe lumineux gravé sur le dessus) ou fiole
    /// (panse de verre, liquide rouge ou bleu, col et bouchon de liège). Chacun a son bruit de chute.</summary>
    public class GroundPickup : MonoBehaviour
    {
        public enum Kind { Gem, Potion, Mana }
        public Kind kind; public string gem; public int tier = 1;
        Transform spin; Light glow; float born; Vector3 basePos;
        public static readonly List<GroundPickup> All = new List<GroundPickup>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static GroundPickup SpawnGem(string kind, int tier, Vector3 pos)
        {
            var d = Make("Gemme " + kind, pos); d.kind = Kind.Gem; d.gem = kind; d.tier = tier;
            var col = Economy.GemColor[Mathf.Max(0, System.Array.IndexOf(Economy.GemKinds, kind))];
            if (Economy.IsRune(kind)) d.BuildRune(col); else d.BuildGem(col, tier);
            d.AddGlow(col, Economy.IsRune(kind) ? 0.9f : 1.2f);
            Sound.Play(Economy.IsRune(kind) ? "sfx_rune" : "sfx_gemme", pos, 0.8f, 0.06f, 0.08f);
            return d;
        }

        public static GroundPickup SpawnPotion(bool mana, Vector3 pos)
        {
            var d = Make(mana ? "Potion de mana" : "Potion de soin", pos); d.kind = mana ? Kind.Mana : Kind.Potion;
            var col = mana ? new Color(0.25f, 0.45f, 1f) : new Color(0.95f, 0.15f, 0.12f);
            d.BuildFlask(col); d.AddGlow(col, 0.8f);
            Sound.Play("sfx_fiole", pos, 0.75f, 0.08f, 0.1f);
            return d;
        }

        static GroundPickup Make(string name, Vector3 pos)
        {
            var go = new GameObject(name); go.transform.position = pos + Vector3.up * 0.02f;
            var d = go.AddComponent<GroundPickup>(); d.born = Time.time; d.basePos = go.transform.position;
            d.spin = new GameObject("Modèle").transform; d.spin.SetParent(go.transform, false); d.spin.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            return d;
        }

        // ---------- matériaux ----------
        static Material Mat(Color c, float metal, float smooth, Color emission = default)
        {
            if (!LootDrop.litBase) return null;
            var m = new Material(LootDrop.litBase); m.SetColor("_BaseColor", c); m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.01f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            return m;
        }

        static GameObject Mesh(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (m) r.sharedMaterial = m;
            return g;
        }

        static GameObject Prim(Transform parent, PrimitiveType t, Material m, Vector3 pos, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(t); var c = g.GetComponent<Collider>(); if (c) Destroy(c);
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (m) r.sharedMaterial = m;
            return g;
        }

        // ---------- maillages procéduraux ----------
        static Mesh gemMesh, stoneMesh;

        /// <summary>Taille « brillant » simplifiée : table octogonale, couronne, rondiste, pavillon jusqu'à la colette.
        /// Facettes à plat (sommets dupliqués) pour que chaque face accroche la lumière.</summary>
        static Mesh GemMesh()
        {
            if (gemMesh) return gemMesh;
            const int N = 8; var v = new List<Vector3>(); var tri = new List<int>();
            Vector3 Ring(float r, float y, int i, float off = 0) { float a = (i + off) * Mathf.PI * 2 / N; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
            void Tri(Vector3 a, Vector3 b, Vector3 c) { if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c - new Vector3(0, -0.2f, 0) * 3) < 0) { var x = b; b = c; c = x; } int k = v.Count; v.Add(a); v.Add(b); v.Add(c); tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); }
            var top = new Vector3(0, 0.62f, 0); var culet = new Vector3(0, -1f, 0);
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                Vector3 t0 = Ring(0.55f, 0.62f, i), t1 = Ring(0.55f, 0.62f, j), g0 = Ring(1f, 0.18f, i, 0.5f), g1 = Ring(1f, 0.18f, j, 0.5f), gm = Ring(1f, 0.18f, i, 0.5f);
                Vector3 b0 = Ring(1f, 0f, i, 0.5f), b1 = Ring(1f, 0f, j, 0.5f);
                Tri(top, t1, t0);                                 // table
                Tri(t0, t1, g0);                                  // couronne : facettes étoile et bezel
                Tri(t1, g1, g0);
                Tri(g0, g1, b0); Tri(g1, b1, b0);                 // rondiste
                Tri(b0, b1, culet);                               // pavillon
            }
            gemMesh = new Mesh { name = "Gemme" }; gemMesh.SetVertices(v); gemMesh.SetTriangles(tri, 0); gemMesh.RecalculateNormals(); gemMesh.RecalculateBounds();
            return gemMesh;
        }

        /// <summary>Galet irrégulier aplati (sphère grossière déformée, facettes plates).</summary>
        static Mesh StoneMesh()
        {
            if (stoneMesh) return stoneMesh;
            var rnd = new System.Random(7); const int R = 6, S = 9; var grid = new Vector3[R + 1, S];
            for (int a = 0; a <= R; a++) for (int b = 0; b < S; b++)
            {
                float th = Mathf.PI * a / R, ph = Mathf.PI * 2 * b / S; float k = 0.85f + 0.3f * (float)rnd.NextDouble();
                grid[a, b] = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * k, Mathf.Cos(th) * k, Mathf.Sin(th) * Mathf.Sin(ph) * k);
                if (a == 0) grid[a, b] = new Vector3(0, 1, 0);
                if (a == R) grid[a, b] = new Vector3(0, -1, 0);
            }
            var v = new List<Vector3>(); var tri = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { if (Vector3.Dot(Vector3.Cross(q - p, r - p), p + q + r) < 0) { var x = q; q = r; r = x; } int k = v.Count; v.Add(p); v.Add(q); v.Add(r); tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); }
            for (int a = 0; a < R; a++) for (int b = 0; b < S; b++)
            {
                int c = (b + 1) % S;
                Tri(grid[a, b], grid[a, c], grid[a + 1, b]); Tri(grid[a, c], grid[a + 1, c], grid[a + 1, b]);
            }
            stoneMesh = new Mesh { name = "Galet" }; stoneMesh.SetVertices(v); stoneMesh.SetTriangles(tri, 0); stoneMesh.RecalculateNormals(); stoneMesh.RecalculateBounds();
            return stoneMesh;
        }

        // ---------- modèles ----------
        void BuildGem(Color c, int t)
        {
            float s = 0.13f + 0.02f * Mathf.Clamp(t - 1, 0, 4);
            var m = Mat(new Color(c.r, c.g, c.b), 0.35f, 0.96f, c * 0.55f);
            var g = Mesh(spin, "Gemme", GemMesh(), m, new Vector3(0, s * 1.2f + 0.12f, 0), Vector3.one * s, Quaternion.Euler(18, 0, 10));
            spin.localPosition = Vector3.zero; spinGem = g.transform; gemY = g.transform.localPosition.y;
        }

        void BuildRune(Color c)
        {
            var stone = Mat(new Color(0.16f, 0.15f, 0.15f), 0f, 0.25f);
            Mesh(spin, "Pierre", StoneMesh(), stone, new Vector3(0, 0.07f, 0), new Vector3(0.24f, 0.08f, 0.2f), Quaternion.Euler(0, 0, 4));
            // glyphe gravé : l'icône de la rune en additif sur le dessus (le fond sombre disparaît, seul le symbole luit)
            var tex = Resources.Load<Texture2D>("Icons/" + ItemIcons.Slug(gem));
            if (FX.additive)
            {
                var gm = new Material(FX.additive); if (tex) gm.SetTexture("_BaseMap", tex); gm.mainTexture = tex; gm.SetColor("_BaseColor", new Color(1, 1, 1, 1));
                var q = Prim(spin, PrimitiveType.Quad, gm, new Vector3(0, 0.158f, 0), new Vector3(0.36f, 0.36f, 1)); q.transform.localRotation = Quaternion.Euler(90, 0, 0); q.name = "Glyphe";
            }
            else Prim(spin, PrimitiveType.Cylinder, Mat(c, 0, 0.5f, c * 2f), new Vector3(0, 0.152f, 0), new Vector3(0.12f, 0.004f, 0.12f));
        }

        void BuildFlask(Color c)
        {
            var root = new GameObject("Fiole").transform; root.SetParent(spin, false); root.localRotation = Quaternion.Euler(0, 0, 12);
            var liquid = Mat(c, 0.1f, 0.9f, c * 0.9f);
            var glass = Mat(new Color(0.75f, 0.85f, 0.9f), 0.6f, 0.98f);
            var brass = Mat(new Color(0.55f, 0.42f, 0.22f), 1f, 0.6f);
            var cork = Mat(new Color(0.45f, 0.3f, 0.17f), 0f, 0.2f);
            Prim(root, PrimitiveType.Sphere, liquid, new Vector3(0, 0.15f, 0), Vector3.one * 0.26f);                       // panse
            Prim(root, PrimitiveType.Cylinder, glass, new Vector3(0, 0.3f, 0), new Vector3(0.08f, 0.05f, 0.08f));           // col
            Prim(root, PrimitiveType.Cylinder, brass, new Vector3(0, 0.27f, 0), new Vector3(0.105f, 0.012f, 0.105f));       // bague de laiton
            Prim(root, PrimitiveType.Cylinder, cork, new Vector3(0, 0.375f, 0), new Vector3(0.07f, 0.035f, 0.07f));         // bouchon
            Prim(root, PrimitiveType.Sphere, Mat(Color.white, 0f, 1f, new Color(0.8f, 0.8f, 0.8f)), new Vector3(-0.06f, 0.2f, -0.08f), Vector3.one * 0.04f);   // reflet
        }

        Transform spinGem; float gemY;

        void AddGlow(Color c, float intensity)
        {
            glow = new GameObject("Lueur").AddComponent<Light>(); glow.transform.SetParent(transform, false); glow.transform.localPosition = Vector3.up * 0.5f;
            glow.type = LightType.Point; glow.color = c; glow.range = 2.2f; glow.intensity = intensity; glow.shadows = LightShadows.None;
            if (FX.additive)
            {
                var halo = Prim(transform, PrimitiveType.Quad, null, new Vector3(0, 0.02f, 0), Vector3.one * 0.9f); halo.name = "Halo";
                halo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var hm = new Material(FX.additive); hm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.6f)); halo.GetComponent<Renderer>().sharedMaterial = hm;
            }
        }

        void Update()
        {
            float t = Time.time - born;
            // petite chute avec rebond à l'apparition, puis la gemme tourne et flotte doucement
            float drop = t < 0.45f ? Mathf.Abs(Mathf.Sin(t / 0.45f * Mathf.PI * 1.5f)) * (1 - t / 0.45f) * 0.5f : 0;
            transform.position = basePos + Vector3.up * drop;
            if (spinGem) { spinGem.localRotation = Quaternion.Euler(18, t * 70, 10); spinGem.localPosition = new Vector3(0, gemY + Mathf.Sin(t * 2.2f) * 0.03f, 0); }
            var hero = FindFirstObjectByType<PlayerController>();
            if (!hero || hero.Health.Dead || t < 0.45f) return;
            if ((hero.transform.position - transform.position).sqrMagnitude > 1.5f * 1.5f) return;
            var w = hero.wallet;
            switch (kind)
            {
                case Kind.Gem: Economy.AddGem(w, gem, tier); hero.Notify?.Invoke($"<color=#{ColorUtility.ToHtmlStringRGB(Economy.GemColor[Mathf.Max(0, System.Array.IndexOf(Economy.GemKinds, gem))])}>{Economy.GemName(gem, tier)}</color>"); break;
                case Kind.Potion: if (w.potions >= Economy.MaxPotions) return; w.potions++; hero.Notify?.Invoke("<color=#ff6a5a>+1 potion de soin</color>"); break;
                case Kind.Mana: if (w.manaPotions >= Economy.MaxPotions) return; w.manaPotions++; hero.Notify?.Invoke("<color=#7aa8ff>+1 potion de mana</color>"); break;
            }
            Sound.Play("sfx_ramasser", transform.position, 0.6f, 0.05f, 0.05f);
            FX.Burst(transform.position + Vector3.up * 0.3f, glow ? glow.color : Color.white, 14, 2.5f, 0.1f, 0.5f, 0.6f);
            Destroy(gameObject);
        }
    }
}
