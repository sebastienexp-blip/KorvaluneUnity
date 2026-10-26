using System.Collections;
using UnityEngine;

namespace Korvalune.Game
{
    public enum Element { Physique, Feu, Foudre, Glace, Neant, Ombre, Sacre, Poison, Sang, Os, Cendre }

    /// <summary>Matériaux d'effets (textures Art/VFX, shader URP Particles/Unlit), créés par l'éditeur (VfxBuilder).</summary>
    [CreateAssetMenu(menuName = "Korvalune/Bibliothèque d'effets")]
    public class VfxLibrary : ScriptableObject
    {
        public Material fire, smoke, glow, spark, lightning, ice, runes, shock, slash, debris, beam, soul, glowAlpha;
    }

    /// <summary>Effets spéciaux des compétences, composés de couches comme dans les ARPG modernes : éclair de lumière
    /// dynamique, cœur (flammes animées, âmes, éclats), étincelles étirées, fumée, onde de choc au sol, débris,
    /// trace de lame, traînées de projectiles, éclairs tracés, rayons, cercles de runes, auras.</summary>
    public static class Vfx
    {
        public static VfxLibrary lib;

        public struct Palette { public Color core, outer, smoke, light; }
        public static Palette Of(Element e)
        {
            switch (e)
            {
                case Element.Feu: return P(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.38f, 0.06f), new Color(0.18f, 0.15f, 0.13f, 0.7f), new Color(1f, 0.55f, 0.2f));
                case Element.Foudre: return P(new Color(0.9f, 0.95f, 1f), new Color(0.45f, 0.65f, 1f), new Color(0.3f, 0.32f, 0.4f, 0.4f), new Color(0.6f, 0.75f, 1f));
                case Element.Glace: return P(new Color(0.85f, 0.97f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.75f, 0.85f, 0.95f, 0.5f), new Color(0.55f, 0.85f, 1f));
                case Element.Neant: return P(new Color(0.95f, 0.75f, 1f), new Color(0.55f, 0.15f, 1f), new Color(0.12f, 0.05f, 0.2f, 0.7f), new Color(0.65f, 0.3f, 1f));
                case Element.Ombre: return P(new Color(0.6f, 0.6f, 0.7f), new Color(0.15f, 0.12f, 0.25f), new Color(0.05f, 0.04f, 0.08f, 0.8f), new Color(0.35f, 0.3f, 0.6f));
                case Element.Sacre: return P(new Color(1f, 0.98f, 0.85f), new Color(1f, 0.8f, 0.35f), new Color(1f, 0.95f, 0.8f, 0.35f), new Color(1f, 0.9f, 0.6f));
                case Element.Poison: return P(new Color(0.8f, 1f, 0.5f), new Color(0.35f, 0.8f, 0.15f), new Color(0.25f, 0.4f, 0.1f, 0.6f), new Color(0.5f, 0.9f, 0.3f));
                case Element.Sang: return P(new Color(1f, 0.45f, 0.4f), new Color(0.65f, 0.02f, 0.05f), new Color(0.3f, 0.02f, 0.03f, 0.7f), new Color(1f, 0.15f, 0.12f));
                case Element.Os: return P(new Color(0.85f, 1f, 0.9f), new Color(0.25f, 1f, 0.6f), new Color(0.15f, 0.25f, 0.2f, 0.6f), new Color(0.35f, 1f, 0.65f));
                case Element.Cendre: return P(new Color(1f, 0.7f, 0.4f), new Color(0.75f, 0.3f, 0.1f), new Color(0.25f, 0.22f, 0.2f, 0.8f), new Color(1f, 0.5f, 0.25f));
                default: return P(new Color(1f, 0.95f, 0.85f), new Color(0.85f, 0.75f, 0.6f), new Color(0.45f, 0.4f, 0.35f, 0.6f), new Color(1f, 0.85f, 0.65f));
            }
        }
        static Palette P(Color c, Color o, Color s, Color l) => new Palette { core = c, outer = o, smoke = s, light = l };

        // ------------------------------------------------------------------------------------------ briques
        static GameObject Root(Vector3 pos, float life, string name = "VFX")
        {
            var g = new GameObject(name); g.transform.position = pos;
            if (Application.isPlaying) Object.Destroy(g, life); return g;
        }

        public static ParticleSystem MakePS(Transform parent, Material mat, float life, int maxP = 200, bool stretched = false)
        {
            var go = new GameObject("PS"); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = Mathf.Max(0.05f, life); m.startLifetime = life; m.maxParticles = maxP;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat;
            if (stretched) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f; }
            return ps;
        }

        static void Fade(ParticleSystem ps, Color a, Color b, float peak = 0.15f)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, peak), new GradientAlphaKey(0, 1) });
            col.color = g;
        }

        static void Sheet(ParticleSystem ps, int tiles = 4)
        {
            var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = tiles; ts.numTilesY = tiles; ts.animation = ParticleSystemAnimationType.WholeSheet;
        }

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, from, 1, to));
        }

        /// <summary>Lumière dynamique brève (éclair d'impact).</summary>
        public static void Flash(Vector3 pos, Color c, float range, float intensity, float life = 0.25f)
        {
            if (!Application.isPlaying) return;
            var g = new GameObject("Éclair de lumière"); g.transform.position = pos;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            g.AddComponent<LightFade>().life = life;
        }

        // ------------------------------------------------------------------------------------------ effets composés
        /// <summary>Explosion : éclair, boule de flammes / énergie animée, gerbe d'étincelles, fumée qui s'élève, onde au sol, débris.</summary>
        public static void Explosion(Vector3 pos, Element e, float radius = 3f)
        {
            if (!lib) { FX.Burst(pos, Of(e).outer, 30); return; }
            var p = Of(e); var root = Root(pos, 4f, "Explosion");
            Flash(pos + Vector3.up, p.light, radius * 3.5f, 6f, 0.35f);
            var core = MakePS(root.transform, e == Element.Feu || e == Element.Cendre ? lib.fire : lib.glow, 0.55f, 60);
            { var m = core.main; m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.6f, radius * 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, radius * 1.2f); m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); m.startColor = p.core;
              var sh = core.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius * 0.25f; Fade(core, p.core, p.outer, 0.05f); if (e == Element.Feu || e == Element.Cendre) Sheet(core); Grow(core, 0.6f, 1.3f); core.Emit(18); }
            var sparks = MakePS(root.transform, lib.spark, 0.7f, 120, true);
            { var m = sparks.main; m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f); m.startSpeed = new ParticleSystem.MinMaxCurve(radius * 2, radius * 5); m.startColor = p.core; m.gravityModifier = 0.8f;
              var sh = sparks.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.2f; sh.rotation = new Vector3(-90, 0, 0); Fade(sparks, p.core, p.outer, 0.02f); sparks.Emit(60); }
            var smoke = MakePS(root.transform, lib.smoke, 2.4f, 40);
            { var m = smoke.main; m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f); m.startColor = p.smoke; m.gravityModifier = -0.05f; m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
              var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius * 0.4f; Fade(smoke, p.smoke, p.smoke, 0.1f); Sheet(smoke); Grow(smoke, 0.6f, 1.6f); smoke.Emit(12); }
            Shock(pos, p.outer, radius);
            if (e == Element.Physique || e == Element.Feu || e == Element.Cendre) Debris(pos, radius);
        }

        /// <summary>Onde de choc au sol : anneau qui s'élargit et s'efface.</summary>
        public static void Shock(Vector3 pos, Color c, float radius, float life = 0.45f)
        {
            if (!lib) return;
            var root = Root(pos + Vector3.up * 0.08f, life + 0.2f, "Onde");
            var ps = MakePS(root.transform, lib.shock, life, 2);
            var m = ps.main; m.startSize = radius * 0.4f; m.startSpeed = 0; m.startColor = c;
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Fade(ps, c, c, 0.05f); Grow(ps, 0.3f, 2.6f); ps.Emit(1);
        }

        public static void Debris(Vector3 pos, float radius)
        {
            if (!lib) return;
            var root = Root(pos, 2.5f, "Débris");
            var ps = MakePS(root.transform, lib.debris, 1.6f, 40);
            var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(radius, radius * 2.5f); m.gravityModifier = 2.2f; m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.3f; sh.rotation = new Vector3(-90, 0, 0);
            var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 2; ts.numTilesY = 2; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.startFrame = new ParticleSystem.MinMaxCurve(0, 3); ts.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            Fade(ps, Color.white, Color.white, 0.01f); ps.Emit(Mathf.RoundToInt(10 + radius * 5));
        }

        /// <summary>Trace de lame en arc devant le personnage (couleur de l'élément).</summary>
        public static void Slash(Transform who, Element e, float size = 2.6f, float angle = 0f, float height = 1.1f)
        {
            if (!lib) return;
            var p = Of(e);
            var root = Root(who.position + Vector3.up * height + who.forward * 0.6f, 0.6f, "Entaille");
            root.transform.rotation = Quaternion.LookRotation(who.forward) * Quaternion.Euler(90, 0, angle);
            var ps = MakePS(root.transform, lib.slash, 0.22f, 2);
            var m = ps.main; m.startSize = size; m.startSpeed = 0; m.startColor = p.core; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startRotation = (angle + who.eulerAngles.y) * Mathf.Deg2Rad;
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; r.alignment = ParticleSystemRenderSpace.Local;
            Fade(ps, p.core, p.outer, 0.02f); Grow(ps, 0.85f, 1.1f); ps.Emit(1);
            var sp = MakePS(root.transform, lib.spark, 0.35f, 30, true);
            var sm = sp.main; sm.startSize = 0.06f; sm.startSpeed = new ParticleSystem.MinMaxCurve(2, 6); sm.startColor = p.core;
            var sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = size * 0.35f; sh.arc = 140; Fade(sp, p.core, p.outer, 0.02f); sp.Emit(14);
        }

        /// <summary>Éclair tracé (ligne brisée texturée) entre deux points, scintillant, avec éclat lumineux aux extrémités.</summary>
        public static void Bolt(Vector3 a, Vector3 b, Element e = Element.Foudre, float width = 0.5f, float life = 0.25f)
        {
            if (!lib || !Application.isPlaying) return;
            var p = Of(e);
            var go = Root(a, life + 0.05f, "Éclair");
            var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = lib.lightning; lr.textureMode = LineTextureMode.Stretch;
            lr.positionCount = 12; lr.widthMultiplier = width; lr.startColor = p.core; lr.endColor = p.outer; lr.numCapVertices = 2;
            go.AddComponent<BoltJitter>().Init(a, b, life);
            Flash(b + Vector3.up * 0.5f, p.light, 8, 5, life);
            Burst(b, e, 12, 4f);
        }

        /// <summary>Petite gerbe d'étincelles de l'élément (impacts, coups).</summary>
        public static void Burst(Vector3 pos, Element e, int n = 16, float speed = 4f)
        {
            if (!lib) { FX.Burst(pos, Of(e).outer, n, speed); return; }
            var p = Of(e); var root = Root(pos, 1.2f, "Impact");
            var ps = MakePS(root.transform, lib.spark, 0.45f, 60, true);
            var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); m.startColor = p.core; m.gravityModifier = 0.6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f; Fade(ps, p.core, p.outer, 0.02f); ps.Emit(n);
            var g = MakePS(root.transform, lib.glow, 0.25f, 2);
            var gm = g.main; gm.startSize = 1.4f; gm.startSpeed = 0; gm.startColor = p.outer; Fade(g, p.core, p.outer, 0.05f); g.Emit(1);
        }

        /// <summary>Cercle de runes au sol qui tourne (zones, invocations, pièges).</summary>
        public static GameObject Runes(Vector3 pos, Element e, float radius, float life)
        {
            if (!lib) return null;
            var p = Of(e); var root = Root(pos + Vector3.up * 0.06f, life + 0.3f, "Runes");
            var ps = MakePS(root.transform, lib.runes, life, 2);
            var m = ps.main; m.startSize = radius * 2.1f; m.startSpeed = 0; m.startColor = p.outer;
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(p.core, 0), new GradientColorKey(p.outer, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
            col.color = g; ps.Emit(1);
            return root;
        }

        /// <summary>Zone persistante : brasier, nuée empoisonnée, tempête de glace, puits du Néant… (particules continues).</summary>
        public static GameObject Zone(Vector3 pos, Element e, float radius, float life)
        {
            if (!lib) return null;
            var p = Of(e); var root = Root(pos, life + 2f, "Zone");
            Runes(pos, e, radius, life);
            var mat = e == Element.Feu || e == Element.Cendre ? lib.fire : e == Element.Glace ? lib.ice : e == Element.Poison || e == Element.Ombre ? lib.smoke : lib.soul;
            var ps = MakePS(root.transform, mat, 1.1f, 300);
            var m = ps.main; m.loop = true; m.duration = life; m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f); m.startColor = p.outer; m.gravityModifier = e == Element.Glace ? 1.5f : -0.1f;
            var em = ps.emission; em.rateOverTime = 30 + radius * 18;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 0.9f; sh.rotation = new Vector3(-90, 0, 0);
            Fade(ps, p.core, p.outer); if (mat == lib.fire || mat == lib.smoke) Sheet(ps);
            ps.Play();
            if (Application.isPlaying) { var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = Vector3.up; l.type = LightType.Point; l.color = p.light; l.range = radius * 2.5f; l.intensity = 2.5f; root.AddComponent<LightFade>().Delay(life); }
            return root;
        }

        /// <summary>Rayon / colonne de lumière (frappe céleste, rayon du Néant) entre deux points.</summary>
        public static void Beam(Vector3 a, Vector3 b, Element e, float width, float life)
        {
            if (!lib || !Application.isPlaying) return;
            var p = Of(e); var go = Root(a, life + 0.1f, "Rayon");
            var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = lib.beam; lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.widthMultiplier = width; lr.startColor = p.core; lr.endColor = p.outer; lr.textureMode = LineTextureMode.Stretch;
            go.AddComponent<LineFade>().life = life;
            Flash(Vector3.Lerp(a, b, 0.5f), p.light, 10, 4, life);
        }

        /// <summary>Aura autour d'un personnage (cri de guerre, bénédictions, malédictions, boucliers) pendant life secondes.</summary>
        public static void Aura(Transform who, Element e, float radius, float life)
        {
            if (!lib) return;
            var p = Of(e); var root = Root(who.position, life + 1f, "Aura"); root.transform.SetParent(who, true);
            var ps = MakePS(root.transform, lib.soul, 1.2f, 120);
            var m = ps.main; m.loop = true; m.duration = life; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); m.startSpeed = 0.2f; m.startColor = p.outer; m.gravityModifier = -0.3f; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 40;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.orbitalY = 1.5f; vel.space = ParticleSystemSimulationSpace.Local;
            Fade(ps, p.core, p.outer); ps.Play();
            Shock(who.position, p.outer, radius * 1.4f, 0.6f);
        }

        /// <summary>Projectile : cœur lumineux (flamme, âme, éclat de glace…), traînée, lumière portée ; appelle onHit à l'impact.</summary>
        public static GameObject Projectile(Vector3 from, Vector3 dir, Element e, float speed, float range, float size, System.Action<Vector3, Health> onHit, Faction owner, bool pierce = false)
        {
            if (!Application.isPlaying) return null;
            var p = Of(e);
            var go = new GameObject("Projectile"); go.transform.position = from; go.transform.rotation = Quaternion.LookRotation(dir);
            var pr = go.AddComponent<ProjectileMover>(); pr.Init(dir, speed, range, size, owner, onHit, pierce);
            if (lib)
            {
                var coreMat = e == Element.Feu || e == Element.Cendre ? lib.fire : e == Element.Glace ? lib.ice : e == Element.Physique ? lib.spark : lib.soul;
                var core = MakePS(go.transform, coreMat, 0.25f, 80);
                var m = core.main; m.loop = true; m.duration = 5; m.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.4f); m.startSpeed = 0.2f; m.startColor = p.core;
                var em = core.emission; em.rateOverTime = 70; var sh = core.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = size * 0.2f;
                Fade(core, p.core, p.outer, 0.05f); if (coreMat == lib.fire) Sheet(core); core.Play();
                var tr = go.AddComponent<TrailRenderer>(); tr.sharedMaterial = lib.glow; tr.time = 0.25f; tr.widthMultiplier = size * 0.9f;
                tr.startColor = p.outer; tr.endColor = new Color(p.outer.r, p.outer.g, p.outer.b, 0);
                var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = p.light; l.range = 5; l.intensity = 2.5f; l.shadows = LightShadows.None;
            }
            return go;
        }
    }

    // ---------------------------------------------------------------------------------------------- composants d'effet
    public class LightFade : MonoBehaviour
    {
        public float life = 0.25f; float t0, i0, delay; Light l;
        public void Delay(float d) { delay = d; }
        void Start() { l = GetComponent<Light>(); if (!l) l = GetComponentInChildren<Light>(); t0 = Time.time; if (l) i0 = l.intensity; }
        void Update()
        {
            if (!l) return;
            float t = Time.time - t0 - delay; if (t < 0) return;
            l.intensity = i0 * Mathf.Clamp01(1 - t / life);
            if (t > life) Destroy(delay > 0 ? l.gameObject : gameObject);
        }
    }

    public class LineFade : MonoBehaviour
    {
        public float life = 0.3f; float t0; LineRenderer lr; float w0;
        void Start() { lr = GetComponent<LineRenderer>(); t0 = Time.time; w0 = lr.widthMultiplier; }
        void Update() { float t = (Time.time - t0) / life; lr.widthMultiplier = w0 * Mathf.Clamp01(1 - t * t); }
    }

    /// <summary>Éclair : points intermédiaires redistribués à chaque image (zigzag vivant), texture décalée.</summary>
    public class BoltJitter : MonoBehaviour
    {
        Vector3 a, b; float life, t0; LineRenderer lr;
        public void Init(Vector3 a, Vector3 b, float life) { this.a = a; this.b = b; this.life = life; t0 = Time.time; lr = GetComponent<LineRenderer>(); Update(); }
        void Update()
        {
            if (!lr) return;
            var d = b - a; var side = Vector3.Cross(d.normalized, Vector3.up).normalized; if (side == Vector3.zero) side = Vector3.right;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float t = i / (float)(lr.positionCount - 1);
                var j = (i == 0 || i == lr.positionCount - 1) ? Vector3.zero : (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-0.6f, 0.6f)) * d.magnitude * 0.06f;
                lr.SetPosition(i, a + d * t + j);
            }
            lr.material.mainTextureOffset = new Vector2(Random.value, 0);
            float k = 1 - (Time.time - t0) / life; lr.widthMultiplier = Mathf.Max(0, k) * lr.widthMultiplier + 0.001f;
        }
    }

    /// <summary>Déplacement d'un projectile, détection des cibles hostiles au passage, explosion à l'impact.</summary>
    public class ProjectileMover : MonoBehaviour
    {
        Vector3 dir; float speed, range, size, travelled; Faction owner; System.Action<Vector3, Health> onHit; bool pierce;
        readonly System.Collections.Generic.HashSet<Health> hit = new System.Collections.Generic.HashSet<Health>();
        public void Init(Vector3 d, float s, float r, float sz, Faction o, System.Action<Vector3, Health> cb, bool p) { dir = d.normalized; speed = s; range = r; size = sz; owner = o; onHit = cb; pierce = p; }
        void Update()
        {
            float step = speed * Time.deltaTime; transform.position += dir * step; travelled += step;
            foreach (var h in Health.Around(transform.position, 0.6f + size * 0.5f, owner))
            {
                if (hit.Contains(h)) continue;
                hit.Add(h); onHit?.Invoke(transform.position, h);
                if (!pierce) { Stop(); return; }
            }
            if (travelled >= range) { onHit?.Invoke(transform.position, null); Stop(); }
        }
        void Stop()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) { ps.transform.SetParent(null, true); var m = ps.main; m.loop = false; ps.Stop(); Destroy(ps.gameObject, 1.5f); }
            Destroy(gameObject);
        }
    }
}
