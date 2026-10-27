using System.Collections.Generic;
using System.Linq;
using Korvalune.Game;
using UnityEditor;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Ville de Korvalune, bâtie aux abords du château : enceinte de ville (courtines, tours, portes voûtées sur chaque route),
    /// rue principale pavée de la porte sud au châtelet, rue circulaire au pied des remparts du château, place du marché avec
    /// fontaine, échoppes des marchands (forgeron, armurier, marchand, joaillier, mystique, alchimiste, curiosités), coffre,
    /// Reliquaire Céleste, taverne, puits ; maisons à colombages alignées le long des rues, lanternes, accessoires.
    /// Kit ModelForge : Resources/Prefabs/World/Ville. Repère : château en (0, 0), porte du château au sud.
    /// </summary>
    public static class TownBuilder
    {
        const string Kit = ModelPipeline.Root + "/Resources/Prefabs/World/Ville";
        public const float WallR = 84f, RingR = 44f, PlazaR = 12f;
        public static readonly Vector2 Plaza = new Vector2(0, -60);
        static readonly Vector2[] Main = { new Vector2(0, -36), new Vector2(0, -70) };

        // ------------------------------------------------------------------------------------------ rues
        static float SegDist(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return (a + ab * t - p).magnitude; }

        /// <summary>Distance (m) à la rue pavée la plus proche à l'intérieur de la ville (rue principale, rue circulaire, routes).</summary>
        public static float StreetDist(Vector2 p)
        {
            float d = SegDist(p, Main[0], Main[1]);
            d = Mathf.Min(d, Mathf.Abs(p.magnitude - RingR));
            if (p.magnitude < WallR + 6) d = Mathf.Min(d, WorldBuilder.RoadDist(p));
            return d;
        }

        /// <summary>Poids de la couche « pavés » du terrain (rues et place, à l'intérieur de l'enceinte).</summary>
        public static float Cobble(Vector2 p)
        {
            if (p.magnitude > WallR + 4) return 0;
            float s = Smooth(3.6f, 2.4f, StreetDist(p));
            s = Mathf.Max(s, Smooth(PlazaR + 1.5f, PlazaR - 0.5f, (p - Plaza).magnitude));
            return s;
        }
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        // ------------------------------------------------------------------------------------------ placement
        struct Spot { public Vector2 p; public float r; }
        static readonly List<Spot> taken = new List<Spot>();
        static Transform root;

        static GameObject Put(string name, Vector3 pos, float yaw, Transform parent = null)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/{name}.prefab");
            if (!pf) { Debug.LogWarning("[Ville] pièce absente : " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent ? parent : root);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.isStatic = true; foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        /// <summary>Lacet (°) qui tourne la façade (−Z local) vers la direction dir.</summary>
        static float FaceYaw(Vector2 dir) => Quaternion.LookRotation(new Vector3(-dir.x, 0, -dir.y)).eulerAngles.y;

        static bool Free(Vector2 p, float r, float street = 0)
        {
            if (p.magnitude < 40f + r || p.magnitude > WallR - 3.5f - r) return false;
            if (WorldBuilder.RiverDist(p) < 14 + r) return false;
            if (street > 0 && StreetDist(p) < street) return false;
            if ((p - Plaza).magnitude < PlazaR + r - 1) return false;
            foreach (var s in taken) if ((s.p - p).magnitude < s.r + r) return false;
            return true;
        }

        static Vector3 G(Vector2 p) => WorldBuilder.Ground(p);

        /// <summary>Pose une pièce sur la première place libre parmi les candidates (façade vers face).</summary>
        static GameObject PlaceAt(string name, IEnumerable<Vector2> cands, float r, System.Func<Vector2, Vector2> face, float street)
        {
            foreach (var c in cands)
            {
                if (!Free(c, r, street)) continue;
                taken.Add(new Spot { p = c, r = r });
                return Put(name, G(c) - Vector3.up * 0.05f, FaceYaw(face(c)));
            }
            Debug.LogWarning("[Ville] aucune place pour " + name);
            return null;
        }

        static IEnumerable<Vector2> AroundPlaza(float rad)
        {
            for (int k = 0; k < 30; k++) { float a = (k * 137.5f) % 360f * Mathf.Deg2Rad; yield return Plaza + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * rad; }
        }

        // ------------------------------------------------------------------------------------------ construction
        public static void Build()
        {
            taken.Clear();
            root = new GameObject("Ville de Korvalune").transform;
            var rng = new System.Random(21);
            Ramparts(rng);
            CI.Step("ville : enceinte");

            // place du marché : étals des petits marchands en cercle autour de la fontaine, caisses et tonneaux
            for (int k = 0; k < 6; k++)
            {
                float a = (30 + 60 * k) * Mathf.Deg2Rad; var q = Plaza + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 8.6f;
                if (Mathf.Abs(Mathf.Sin(a)) < 0.3f) continue;                                      // laisse passer la rue principale (nord-sud)
                Put("M_Etal" + (k % 4), G(q) - Vector3.up * 0.05f, FaceYaw(Plaza - q));
                var side = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                Put(k % 2 == 0 ? "M_Caisses" : "M_Tonneaux", G(q + side * 3.0f + (q - Plaza).normalized * 1.0f), (float)rng.NextDouble() * 360);
            }
            // place du marché : fontaine, bancs, lanternes, bannières
            Put("M_Fontaine", G(Plaza) - Vector3.up * 0.05f, 0); taken.Add(new Spot { p = Plaza, r = 4.5f });
            for (int k = 0; k < 4; k++)
            {
                float a = (45 + 90 * k) * Mathf.Deg2Rad; var q = Plaza + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (PlazaR - 1.2f);
                Lamp(q); var b = Plaza + new Vector2(Mathf.Sin(a + 0.35f), Mathf.Cos(a + 0.35f)) * 5.3f; Put("M_Banc", G(b), FaceYaw(Plaza - b) + 180);
            }

            // échoppes autour de la place, façade vers la fontaine ; marchand derrière le comptoir
            var shops = new (string piece, MerchantKind kind, float r, Vector3 vendor, string roof)[]
            {
                ("S_Forgeron", MerchantKind.Forgeron, 6.3f, new Vector3(0.6f, 0.12f, 0.6f), "S_Forgeron_Toit"),
                ("S_Armurier", MerchantKind.Armurier, 5.6f, new Vector3(0, 0.12f, 0.2f), "S_Armurier_Toit"),
                ("S_Marchand", MerchantKind.Marchand, 4.4f, new Vector3(0, 0.12f, 0.4f), "S_Marchand_Toit"),
                ("S_Joaillier", MerchantKind.Joaillier, 3.9f, new Vector3(0, 0.12f, 0.6f), "S_Joaillier_Toit"),
                ("S_Alchimiste", MerchantKind.Alchimiste, 4.4f, new Vector3(0, 0.12f, 0.4f), "S_Alchimiste_Toit"),
                ("S_Mystique", MerchantKind.Mystique, 4.6f, new Vector3(0, 0.12f, 1.3f), "S_Mystique_Toit"),
                ("S_Curiosites", MerchantKind.Curiosites, 4.4f, new Vector3(-2.4f, 0f, -1.6f), null),
            };
            foreach (var s in shops)
            {
                var cands = AroundPlaza(PlazaR + s.r + 3.5f).Concat(AroundPlaza(PlazaR + s.r + 8f)).Concat(AroundPlaza(PlazaR + s.r + 13f));
                var go = PlaceAt(s.piece, cands, s.r, c => Plaza - c, 3.2f + s.r * 0.7f);
                if (!go) continue;
                if (s.roof != null) { var roof = Put(s.roof, go.transform.position, go.transform.eulerAngles.y, go.transform); if (roof) { roof.isStatic = false; roof.AddComponent<RoofFade>().radius = s.r + 2.5f; } }
                Vendor(go.transform, s.kind, s.vendor);
                Decorate(go.transform, s.piece);
            }
            // coffre (près du marchand général) et Reliquaire Céleste (place secondaire)
            var stash = PlaceAt("S_Coffre", AroundPlaza(PlazaR + 2.2f), 1.2f, c => Plaza - c, 0);
            if (stash) Vendor(stash.transform, MerchantKind.Coffre, new Vector3(0, 0, 0.9f), standing: false);
            var rel = PlaceAt("S_Reliquaire", AroundPlaza(PlazaR + 10f).Concat(AroundPlaza(PlazaR + 16f)).Concat(RingSpots(RingR + 10f, 72)).Concat(RingSpots(RingR + 18f, 72)), 5.4f, c => Plaza - c, 6f);
            if (rel)
            {
                Vendor(rel.transform, MerchantKind.Reliquaire, new Vector3(0, 0.75f, -1.2f), standing: false);
                Glow(rel.transform, new Vector3(0, 2.9f, 0), new Color(0.55f, 0.85f, 1f), 3.2f, 9f, sparks: true);
            }
            CI.Step("ville : marché et échoppes");

            // taverne et puits sur la rue principale, face à la rue
            var tav = PlaceAt("M_Taverne", RingSpots(RingR + 11f, 90).Concat(RingSpots(RingR + 20f, 90)), 7.2f, c => -c.normalized, 4f);
            if (tav) { Glow(tav.transform, new Vector3(-1.6f, 2.55f, -4.35f), new Color(1f, 0.7f, 0.35f), 1.0f, 5f); Glow(tav.transform, new Vector3(1.6f, 2.55f, -4.35f), new Color(1f, 0.7f, 0.35f), 1.0f, 5f); }
            PlaceAt("M_Puits", RingSpots(RingR + 5f, 40), 2.0f, c => -c, 3.2f);

            // maisons le long des rues (des deux côtés), façade vers la rue
            int houses = 0;
            var streetCands = new List<(Vector2 p, Vector2 face)>();
            void Line(Vector2 a, Vector2 b, float step)
            {
                var d = (b - a); float L = d.magnitude; var t = d / L; var n = new Vector2(-t.y, t.x);
                for (float s = step * 0.5f; s < L; s += step)
                    foreach (var side in new[] { -1f, 1f }) streetCands.Add((a + t * s + n * side * 8.2f, -n * side));
            }
            Line(Main[0], Main[1], 9.5f);
            foreach (var road in WorldBuilder.Roads)
                for (int i = 0; i < road.Length - 1; i++)
                    if (road[i].magnitude < WallR + 10 || road[i + 1].magnitude < WallR + 10) Line(road[i], road[i + 1], 9.5f);
            for (int k = 0; k < 64; k++) { float a = k / 64f * Mathf.PI * 2; var p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); streetCands.Add((p * (RingR + 8.4f), -p)); }
            // 2e rang : maisons plus à l'écart, tournées vers le château
            for (int k = 0; k < 52; k++) { float a = k / 52f * Mathf.PI * 2 + 0.03f; var p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); streetCands.Add((p * (RingR + 21f), -p)); }
            for (int k = 0; k < 60; k++) { float a = k / 60f * Mathf.PI * 2 + 0.05f; var p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); streetCands.Add((p * (WallR - 10f), -p)); }
            foreach (var (p, face) in streetCands)
            {
                if (!Free(p, 5.2f, 6.2f)) continue;
                int v = rng.Next(8);
                taken.Add(new Spot { p = p, r = 5.2f });
                var h = Put("M_Maison" + v, G(p) - Vector3.up * 0.1f, FaceYaw(face) + (float)(rng.NextDouble() * 6 - 3));
                if (!h) continue;
                houses++;
                // accessoires devant la maison (sur le côté, hors de la rue)
                if (rng.NextDouble() < 0.55)
                {
                    string[] props = { "M_Tonneaux", "M_Caisses", "M_Foin", "M_Banc", "M_Charrette" };
                    var side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var q3 = h.transform.TransformPoint(new Vector3(side * 4.6f, 0, -4.6f));
                    var q = new Vector2(q3.x, q3.z);
                    if (StreetDist(q) > 2.8f && Free(q, 0.6f)) { Put(props[rng.Next(props.Length)], G(q), h.transform.eulerAngles.y + (float)rng.NextDouble() * 40 - 20); taken.Add(new Spot { p = q, r = 1.2f }); }
                }
                if (rng.NextDouble() < 0.35) Smoke(h.transform);
            }
            CI.Step($"ville : {houses} maisons");

            // lanternes le long des rues
            void Lamps(Vector2 a, Vector2 b, float step)
            {
                var d = b - a; float L = d.magnitude; var t = d / L; var n = new Vector2(-t.y, t.x); int k = 0;
                for (float s = 4; s < L; s += step, k++) { var q = a + t * s + n * (k % 2 == 0 ? 3.9f : -3.9f); if (q.magnitude < WallR - 2 && q.magnitude > 39 && Free(q, 0.5f)) Lamp(q); }
            }
            Lamps(Main[0], Main[1], 14);
            for (int k = 0; k < 18; k++) { float a = k / 18f * Mathf.PI * 2; var q = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (RingR + 3.9f); if (Free(q, 0.5f)) Lamp(q); }
            foreach (var road in WorldBuilder.Roads) for (int i = 0; i < road.Length - 1; i++) Lamps(road[i], road[i + 1], 16);

            // bannières à l'entrée de la rue principale
            foreach (var x in new[] { -3.6f, 3.6f }) Put("M_Banniere", G(new Vector2(x, -40)), x < 0 ? 0 : 180);
            CI.Step("ville : lanternes et décor");
        }

        static IEnumerable<Vector2> AlongStreet(Vector2 a, Vector2 b, float off, float step)
        {
            var d = b - a; float L = d.magnitude; var t = d / L; var n = new Vector2(-t.y, t.x);
            for (float s = 6; s < L; s += step) { yield return a + t * s + n * (off + 2f); yield return a + t * s - n * (off + 2f); }
        }

        static IEnumerable<Vector2> RingSpots(float r, int n) { for (int k = 0; k < n; k++) { float a = (k * 137.5f) % 360 * Mathf.Deg2Rad; yield return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r; } }

        static void Lamp(Vector2 q)
        {
            var go = Put("M_Lanterne", G(q), (Mathf.Atan2(q.x, q.y) * Mathf.Rad2Deg) + 90);
            if (go) taken.Add(new Spot { p = q, r = 0.6f });
        }

        /// <summary>Marchand : repère derrière le comptoir avec le composant de commerce ; la silhouette du marchand y sera posée.</summary>
        static void Vendor(Transform shop, MerchantKind kind, Vector3 local, bool standing = true)
        {
            var v = new GameObject(Merchant.DefaultTitle(kind)); v.transform.SetParent(shop, false);
            v.transform.localPosition = local; v.transform.localRotation = Quaternion.identity;   // regarde vers la façade (−Z)
            v.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var m = v.AddComponent<Merchant>(); m.kind = kind; m.title = Merchant.DefaultTitle(kind);
            if (!standing) m.talkRadius = 3.6f;
        }

        static void Glow(Transform parent, Vector3 local, Color c, float intensity, float range, bool sparks = false)
        {
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(parent, false); l.transform.localPosition = local;
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            var f = l.gameObject.AddComponent<Flicker>(); f.amount = 0.08f;
            if (!sparks) return;
            var ps = l.gameObject.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 2.2f; m.startSpeed = 0.3f; m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f); m.maxParticles = 80;
            m.startColor = c; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 18; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.9f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.orbitalY = 0.8f;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldBuilder.Config().fxAdditive;
        }

        static void Fire(Transform parent, Vector3 local)
        {
            var l = new GameObject("Foyer").AddComponent<Light>(); l.transform.SetParent(parent, false); l.transform.localPosition = local;
            l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.22f); l.intensity = 2.4f; l.range = 7; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<Flicker>();
            var ps = l.gameObject.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 0.7f; m.startSpeed = 0.8f; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); m.maxParticles = 80;
            m.startColor = new Color(1f, 0.55f, 0.18f, 0.9f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.2f;
            var em = ps.emission; em.rateOverTime = 45; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.6f, 0.1f, 1.0f);
            var cfg = WorldBuilder.Config();
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = cfg.vfx && cfg.vfx.fire ? cfg.vfx.fire : cfg.fxAdditive;
        }

        static void Smoke(Transform house)
        {
            // fumée de cheminée : particules grises lentes (matériau d'effets alpha si disponible)
            var cfg = WorldBuilder.Config(); var mat = cfg.vfx && cfg.vfx.smoke ? cfg.vfx.smoke : null; if (!mat) return;
            var mr = house.GetComponentInChildren<MeshRenderer>(); if (!mr) return;
            var b = mr.bounds;
            var go = new GameObject("Fumée"); go.transform.SetParent(house, false);
            go.transform.position = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main; m.startLifetime = 5f; m.startSpeed = 0.7f; m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f); m.maxParticles = 40;
            m.startColor = new Color(0.55f, 0.55f, 0.55f, 0.35f); m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 4; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.5f));
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 0), new GradientColorKey(new Color(0.75f, 0.75f, 0.78f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.35f, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        }

        static void Decorate(Transform shop, string piece)
        {
            switch (piece)
            {
                case "S_Forgeron": Fire(shop, new Vector3(2.6f, 1.25f, 2.0f)); break;
                case "S_Mystique": Glow(shop, new Vector3(0, 0.85f, 0.5f), new Color(0.7f, 0.5f, 1f), 1.6f, 6f, sparks: true); break;
                case "S_Alchimiste": Glow(shop, new Vector3(2.2f, 0.9f, -1.4f), new Color(0.4f, 1f, 0.45f), 1.2f, 4f, sparks: true); break;
                case "S_Joaillier": Glow(shop, new Vector3(0, 1.4f, -0.6f), new Color(1f, 0.85f, 0.6f), 0.8f, 3.5f); break;
            }
        }

        // ------------------------------------------------------------------------------------------ enceinte
        static void Ramparts(System.Random rng)
        {
            // portes : intersection de chaque route avec le cercle de l'enceinte
            var gates = new List<(Vector2 p, Vector2 outward)>();
            foreach (var road in WorldBuilder.Roads)
                for (int i = 0; i < road.Length - 1; i++)
                {
                    Vector2 a = road[i], b = road[i + 1];
                    if ((a.magnitude - WallR) * (b.magnitude - WallR) > 0) continue;
                    float lo = 0, hi = 1;
                    for (int it = 0; it < 30; it++) { float m = (lo + hi) / 2; if (((a + (b - a) * m).magnitude - WallR) * (a.magnitude - WallR) > 0) lo = m; else hi = m; }
                    var p = a + (b - a) * lo;
                    if (gates.Any(g => (g.p - p).magnitude < 20)) continue;
                    var dir = (b - a).normalized; if (Vector2.Dot(dir, p) < 0) dir = -dir;
                    gates.Add((p, dir));
                }
            foreach (var g in gates)
            {
                if (WorldBuilder.RiverDist(g.p) < 12) continue;
                Put("M_PorteVille", G(g.p) - Vector3.up * 0.2f, FaceYaw(g.outward));
                taken.Add(new Spot { p = g.p, r = 7f });
                Lamp(g.p - g.outward * 7f + new Vector2(g.outward.y, -g.outward.x) * 4.2f);
            }
            // courtines de 12 m et tours tous les 4 tronçons
            int n = Mathf.RoundToInt(2 * Mathf.PI * WallR / 12f);
            float step = 2 * Mathf.PI / n; float segL = 2 * WallR * Mathf.Sin(step / 2);
            for (int k = 0; k < n; k++)
            {
                float a = k * step + step / 2;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); var p = dir * WallR * Mathf.Cos(step / 2);
                if (gates.Any(g => (g.p - p).magnitude < 6.3f + segL / 2 + 0.5f)) continue;
                if (WorldBuilder.RiverDist(p) < 13) continue;
                var e0 = new Vector2(Mathf.Sin(a - step / 2), Mathf.Cos(a - step / 2)) * WallR; var e1 = new Vector2(Mathf.Sin(a + step / 2), Mathf.Cos(a + step / 2)) * WallR;
                float y = Mathf.Min(G(p).y, Mathf.Min(G(e0).y, G(e1).y)) - 0.5f;
                var w = Put("M_Rempart", new Vector3(p.x, y, p.y), FaceYaw(dir));
                if (w) w.transform.localScale = new Vector3(segL / 12f + 0.04f, 1, 1);
                if (k % 4 == 0 && WorldBuilder.RiverDist(e0) > 13 && !gates.Any(g => (g.p - e0).magnitude < 10))
                    Put("M_TourVille", new Vector3(e0.x, Mathf.Min(G(e0).y, y + 0.5f) - 0.6f, e0.y), (float)rng.NextDouble() * 360);
            }
        }

        // ------------------------------------------------------------------------------------------ contrôle
        /// <summary>Contrôles : nombre de maisons, échoppes présentes, rue principale dégagée jusqu'au château.</summary>
        public static void Checks(System.Action<string, bool, string> check)
        {
            var merchants = Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None);
            check("Ville : échoppes des marchands", merchants.Length >= 8, string.Join(", ", merchants.Select(m => m.title)));
            int houses = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("M_Maison"));
            check("Ville : maisons", houses >= 25, houses + " maisons");
            // rue principale : aucun obstacle entre la porte sud et le châtelet
            bool clear = true; string hit = "";
            for (float z = -78; z < -38; z += 2)
            {
                float x = Mathf.Abs(z - Plaza.y) < 7 ? 6f : 0f;                      // contourne la fontaine par l'est
                var o = WorldBuilder.Ground(new Vector2(x, z)) + Vector3.up * 1.0f;
                if (Physics.Raycast(o, Vector3.forward, out var h, 2f) && !h.collider.isTrigger) { clear = false; hit = $"{h.collider.name} en z={z:0}"; break; }
            }
            check("Ville : rue principale praticable jusqu'au château", clear, clear ? "dégagée" : hit);
        }
    }
}
