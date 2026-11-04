using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Vie de la ville : villageois qui flânent d'un point à l'autre, s'arrêtent, discutent ; chiens qui trottent
    /// derrière eux ; volée d'oiseaux qui tourne au-dessus des toits. Tous restent dans la zone sûre et ne gênent pas le
    /// héros (pas de collision avec lui). Les villageois reprennent les silhouettes des marchands, en tenues variées.</summary>
    public class TownLife : MonoBehaviour
    {
        static readonly string[] Folk = { "PNJ_Forgeron", "PNJ_Armurier", "PNJ_Marchand", "PNJ_Joaillier", "PNJ_Mystique", "PNJ_Alchimiste", "PNJ_Curiosites" };
        static readonly string[] Names = { "Villageois", "Villageoise", "Paysan", "Lavandière", "Porteur d'eau", "Garde", "Marchande ambulante", "Enfant du bourg" };

        public static void Populate(GameBootstrap game, int villagers = 12, int dogs = 4)
        {
            if (game.safeRadius <= 0) return;
            var root = new GameObject("Vie de la ville").AddComponent<TownLife>();
            var center = game.safeCenter; float r = game.safeRadius * 0.82f;
            var rng = new System.Random(7);
            var people = new List<Walker>();
            for (int i = 0; i < villagers; i++)
            {
                var pf = Res.Load<GameObject>("Prefabs/Characters/" + Folk[i % Folk.Length]); if (!pf) continue;
                var at = Spot(center, r, rng); if (!at.HasValue) continue;
                var go = Instantiate(pf, at.Value, Quaternion.Euler(0, rng.Next(360), 0), root.transform); go.name = Names[i % Names.Length];
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                // tenue variée : teinte légère des vêtements
                var tint = Color.HSVToRGB((float)rng.NextDouble(), 0.25f, 1f);
                foreach (var rr in go.GetComponentsInChildren<Renderer>()) foreach (var m in rr.materials) if (m.HasProperty("_BaseColor") && !m.name.Contains("Skin") && !m.name.Contains("Hair")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * Color.Lerp(Color.white, tint, 0.45f));
                go.transform.localScale *= 0.92f + 0.12f * (float)rng.NextDouble();
                var w = go.AddComponent<Walker>(); w.Setup(center, r, 1.25f + 0.35f * (float)rng.NextDouble(), rng.Next());
                people.Add(w);
            }
            var cfg = game.config; var wolf = cfg ? cfg.Enemy("loup") : null;
            for (int i = 0; wolf != null && wolf.prefab && i < dogs; i++)
            {
                var at = Spot(center, r, rng); if (!at.HasValue) continue;
                var go = Instantiate(wolf.prefab, at.Value, Quaternion.identity, root.transform); go.name = "Chien";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                go.transform.localScale *= 0.55f + 0.1f * (float)rng.NextDouble();
                var coat = new[] { new Color(0.75f, 0.55f, 0.35f), new Color(0.95f, 0.9f, 0.8f), new Color(0.35f, 0.25f, 0.18f), new Color(0.85f, 0.7f, 0.45f) }[i % 4];
                foreach (var rr in go.GetComponentsInChildren<Renderer>()) foreach (var m in rr.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", coat * 1.6f); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black); }
                var w = go.AddComponent<Walker>(); w.dog = true; w.Setup(center, r, 2.2f, rng.Next());
                if (people.Count > 0) w.follow = people[i % people.Count].transform;
            }
            Birds(root.transform, center + Vector3.up * 22f, 9, rng);
        }

        static Vector3? Spot(Vector3 c, float r, System.Random rng)
        {
            for (int k = 0; k < 30; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = Mathf.Sqrt((float)rng.NextDouble()) * r;
                var p = GameBootstrap.Ground(c + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d));
                if (Physics.CheckSphere(p + Vector3.up * 1.0f, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;   // pas dans une maison
                if (p.y < GameBootstrap.WaterLevel + 0.2f) continue;
                return p;
            }
            return null;
        }

        /// <summary>Oiseaux : petits « V » sombres qui planent en cercle au-dessus de la ville.</summary>
        static void Birds(Transform parent, Vector3 c, int n, System.Random rng)
        {
            var mat = LootDrop.litBase ? new Material(LootDrop.litBase) : null; if (mat) mat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.1f));
            for (int i = 0; i < n; i++)
            {
                var b = new GameObject("Oiseau").transform; b.SetParent(parent, false);
                foreach (var s in new[] { -1, 1 })
                {
                    var wing = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(wing.GetComponent<Collider>()); wing.name = "Aile";
                    wing.transform.SetParent(b, false); wing.transform.localPosition = new Vector3(s * 0.28f, 0, 0); wing.transform.localScale = new Vector3(0.55f, 0.03f, 0.18f);
                    if (mat) wing.GetComponent<Renderer>().sharedMaterial = mat; wing.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var f = b.gameObject.AddComponent<Bird>(); f.center = c + new Vector3((float)rng.NextDouble() * 10 - 5, (float)rng.NextDouble() * 6, (float)rng.NextDouble() * 10 - 5);
                f.radius = 14f + (float)rng.NextDouble() * 12f; f.speed = 0.25f + 0.15f * (float)rng.NextDouble(); f.phase = (float)rng.NextDouble() * 6.28f;
            }
        }

        // ---------------------------------------------------------------------------------------------- comportements
        public class Walker : MonoBehaviour
        {
            public Transform follow; public bool dog;
            Vector3 center, target; float radius, speed, waitUntil, stuckSince; Animator anim; System.Random rng; Vector3 lastPos;
            public void Setup(Vector3 c, float r, float s, int seed)
            {
                center = c; radius = r; speed = s; rng = new System.Random(seed); anim = GetComponentInChildren<Animator>(); Pick(); waitUntil = Time.time + (float)rng.NextDouble() * 4f;
                if (!follow) Usable.Add(gameObject, "Parler au villageois", 2.6f, Talk, () => follow == null && !dog);
            }
            static readonly string[] Lines = {
                "« Les morts ne restent plus dans le vieux cimetière… méfiez-vous, voyageur. »",
                "« La pierre de voyage vous mènera loin d'ici. Plus loin que je n'oserais aller. »",
                "« On dit qu'un abbé noyé marche encore sur la Côte des Naufrages. »",
                "« Le forgeron a reçu du bon acier. Passez le voir avant de partir. »",
                "« Les brèches s'ouvrent toujours plus profond. La statue en sait quelque chose. »",
                "« Ma grand-mère parlait d'un abîme au bord du monde… je n'y croyais pas. »",
                "« Merci de veiller sur la vallée. »",
            };
            void Talk()
            {
                var hero = FindFirstObjectByType<PlayerController>();
                if (hero) { var to = hero.transform.position - transform.position; to.y = 0; if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to); }
                waitUntil = Time.time + 4f;
                FindFirstObjectByType<GameBootstrap>()?.Hud?.Notify(Lines[rng.Next(Lines.Length)]);
            }
            void Pick()
            {
                var s = Spot(center, radius, rng); target = s ?? center;
            }
            void Update()
            {
                var goal = follow ? follow.position - follow.forward * 1.6f : target;
                var to = goal - transform.position; to.y = 0; float d = to.magnitude;
                float v = 0;
                if (Time.time >= waitUntil && d > (follow ? 1.2f : 0.6f))
                {
                    v = follow ? Mathf.Clamp(d - 1f, 0, speed * 1.6f) : speed;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 240f * Time.deltaTime);
                    var next = transform.position + transform.forward * v * Time.deltaTime;
                    // obstacle devant (maison, étal) : on choisit une autre destination
                    if (Physics.Raycast(transform.position + Vector3.up * 0.8f, transform.forward, 1.0f, ~0, QueryTriggerInteraction.Ignore)) { if (!follow) { Pick(); waitUntil = Time.time + 1f; } v = 0; }
                    else transform.position = GameBootstrap.Ground(next);
                }
                else if (!follow && d <= 0.6f) { waitUntil = Time.time + 2f + (float)rng.NextDouble() * 6f; Pick(); }
                // coincé : nouvelle destination
                if ((transform.position - lastPos).sqrMagnitude > 0.01f) { lastPos = transform.position; stuckSince = Time.time; }
                else if (v > 0 && Time.time - stuckSince > 3f && !follow) { Pick(); stuckSince = Time.time; }
                if (anim) anim.SetFloat(CharacterRig.SpeedId, v, 0.15f, Time.deltaTime);
            }
        }

        public class Bird : MonoBehaviour
        {
            public Vector3 center; public float radius, speed, phase;
            Transform l, r;
            void Start() { l = transform.GetChild(0); r = transform.GetChild(1); }
            void Update()
            {
                float a = phase + Time.time * speed;
                var p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2.3f) * 1.5f, Mathf.Sin(a) * radius);
                var dir = p - transform.position; if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
                transform.position = p;
                float flap = Mathf.Sin(Time.time * 9f + phase) * (Mathf.Sin(Time.time * 0.7f + phase) > 0.3f ? 0 : 35f);   // battements, puis vol plané
                l.localRotation = Quaternion.Euler(0, 0, flap); r.localRotation = Quaternion.Euler(0, 0, -flap);
            }
        }
    }
}
