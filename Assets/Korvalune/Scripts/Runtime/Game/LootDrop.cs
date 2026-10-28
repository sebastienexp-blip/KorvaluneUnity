using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Objet au sol, mis en valeur comme dans Diablo 4 : faisceau de lumière vertical à la couleur de la rareté
    /// (de plus en plus haut et intense : magique → ancestral), halo au sol, lumière ponctuelle, étincelles qui montent
    /// (légendaire et au-delà), tourbillon (mythique, ancestral), nom affiché. Ramassé automatiquement au contact.</summary>
    public class LootDrop : MonoBehaviour
    {
        public ItemSave item;
        public float pickupRadius = 1.6f;
        Light glow; Transform beam; float born;
        public static System.Action<ItemSave> Picked;
        public static Material beamMat;
        public static readonly System.Collections.Generic.List<LootDrop> All = new System.Collections.Generic.List<LootDrop>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        static void Kill(Object o) { if (!o) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public static LootDrop Spawn(ItemSave it, Vector3 pos)
        {
            var go = new GameObject("Butin " + it.name);
            go.transform.position = pos + Vector3.up * 0.05f;
            var d = go.AddComponent<LootDrop>(); d.item = it; d.Build(); if (!All.Contains(d)) All.Add(d);
            Sound.Play(Sound.DropOf(it.rarity), pos, it.rarity >= Rarity.Legendaire ? 1f : 0.6f, 0.04f, 0.1f);
            return d;
        }

        void Build()
        {
            born = Time.time;
            var c = Loot.DisplayColor(item); int r = (int)item.rarity;
            // objet : coffret de bois cerclé de métal, ferrures et serrure à la couleur de la rareté
            var box = new GameObject("Coffret").transform; box.SetParent(transform, false); box.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var wood = new Color(0.36f, 0.22f, 0.12f); var woodDark = new Color(0.22f, 0.13f, 0.07f);
            var trim = Color.Lerp(new Color(0.42f, 0.42f, 0.45f), c, r == 0 ? 0f : 0.75f);
            Part(box, "Caisse", new Vector3(0, 0.17f, 0), new Vector3(0.56f, 0.30f, 0.38f), wood, 0f, 0.25f);
            Part(box, "Couvercle", new Vector3(0, 0.355f, 0), new Vector3(0.60f, 0.08f, 0.42f), woodDark, 0f, 0.3f);
            Part(box, "Bombé", new Vector3(0, 0.41f, 0), new Vector3(0.54f, 0.05f, 0.34f), wood, 0f, 0.3f);
            foreach (var x in new[] { -0.19f, 0.19f })
            {
                Part(box, "Cerclage", new Vector3(x, 0.22f, 0), new Vector3(0.06f, 0.42f, 0.43f), trim, 0.9f, 0.55f);
            }
            foreach (var x in new[] { -0.27f, 0.27f }) foreach (var z in new[] { -0.18f, 0.18f })
                Part(box, "Coin", new Vector3(x, 0.17f, z), new Vector3(0.06f, 0.32f, 0.06f), trim, 0.9f, 0.55f);
            Part(box, "Serrure", new Vector3(0, 0.3f, 0.2f), new Vector3(0.1f, 0.12f, 0.03f), Color.Lerp(trim, Color.white, 0.25f), 1f, 0.7f, r >= 2 ? c * 1.6f : Color.black);
            // halo au sol
            if (FX.additive)
            {
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad); halo.name = "Halo"; Kill(halo.GetComponent<Collider>());
                halo.transform.SetParent(transform, false); halo.transform.localPosition = new Vector3(0, 0.03f, 0); halo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                halo.transform.localScale = Vector3.one * (1.2f + 0.35f * r);
                var hm = new Material(FX.additive); hm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.85f)); halo.GetComponent<Renderer>().sharedMaterial = hm;
            }
            // faisceau vertical (dès magique)
            if (r >= 1 && beamMat)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.name = "Faisceau"; Kill(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false);
                float h = r switch { 1 => 1.6f, 2 => 2.8f, 3 => 6f, 4 => 6.5f, 5 => 9f, _ => 11f };
                b.transform.localPosition = new Vector3(0, h / 2, 0); b.transform.localScale = new Vector3(0.18f + 0.05f * r, h / 2, 0.18f + 0.05f * r);
                var bm = new Material(beamMat); bm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.55f + 0.06f * r)); b.GetComponent<Renderer>().sharedMaterial = bm;
                b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam = b.transform;
            }
            // lumière
            glow = new GameObject("Lueur").AddComponent<Light>(); glow.transform.SetParent(transform, false); glow.transform.localPosition = new Vector3(0, 0.8f, 0);
            glow.type = LightType.Point; glow.color = c; glow.range = 3 + r; glow.intensity = 0.8f + 0.5f * r; glow.shadows = LightShadows.None;
            // étincelles montantes et tourbillon
            if (r >= 3 && FX.additive)
            {
                var ps = new GameObject("Étincelles").AddComponent<ParticleSystem>(); ps.transform.SetParent(transform, false);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var m = ps.main; m.loop = true; m.startLifetime = 1.6f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f); m.startColor = c; m.gravityModifier = -0.15f;
                var em = ps.emission; em.rateOverTime = 10 + 8 * (r - 3);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f; sh.rotation = new Vector3(-90, 0, 0);
                if (r >= 5) { var vel = ps.velocityOverLifetime; vel.enabled = true; vel.orbitalY = 2.5f; }
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = FX.additive;
                ps.Play();
            }
        }

        public static Material litBase;   // matériau URP Lit fourni par la configuration (le matériau par défaut des primitives est rose sur Android)
        static void Part(Transform parent, string name, Vector3 pos, Vector3 size, Color col, float metal, float smooth, Color emission = default)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; Kill(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = size;
            var mr = g.GetComponent<Renderer>(); if (!litBase) litBase = mr.sharedMaterial;
            var m = new Material(litBase); m.color = col; m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.01f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            mr.sharedMaterial = m;
        }

        void Update()
        {
            float t = Time.time - born;
            if (glow) glow.intensity = (0.8f + 0.5f * (int)item.rarity) * (0.85f + 0.15f * Mathf.Sin(t * 3));
            if (beam) beam.localRotation = Quaternion.Euler(0, t * 40, 0);
            var p = FindFirstObjectByType<PlayerController>();
            if (p && !p.Health.Dead && t > 0.6f && p.inventory.Count < Equipment.BagSize && (p.transform.position - transform.position).sqrMagnitude < pickupRadius * pickupRadius)
            {
                Picked?.Invoke(item);
                FX.Burst(transform.position + Vector3.up * 0.4f, Loot.DisplayColor(item), 20, 3f, 0.12f, 0.5f, -0.3f);
                Destroy(gameObject);
            }
        }
    }
}
