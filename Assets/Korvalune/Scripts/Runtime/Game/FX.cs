using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Effets procéduraux légers : éclats, anneaux de choc, poussière, lueurs (systèmes de particules créés à la volée).</summary>
    public static class FX
    {
        public static Material additive, alpha;

        static ParticleSystem Make(Vector3 pos, Material mat, float life)
        {
            var go = new GameObject("FX");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = life; m.startLifetime = life; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            Object.Destroy(go, life + 1.5f);
            return ps;
        }

        /// <summary>Gerbe d'étincelles / de braises.</summary>
        public static void Burst(Vector3 pos, Color c, int count = 24, float speed = 5f, float size = 0.18f, float life = 0.5f, float gravity = 0.8f)
        {
            if (!additive) return;
            var ps = Make(pos, additive, life);
            var m = ps.main; m.startColor = c; m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size); m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            m.gravityModifier = gravity;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Emit(count);
        }

        /// <summary>Onde de choc au sol (anneau de particules qui s'étend).</summary>
        public static void Ring(Vector3 pos, Color c, float radius, float life = 0.45f)
        {
            if (!additive) return;
            var ps = Make(pos + Vector3.up * 0.15f, additive, life);
            var m = ps.main; m.startColor = c; m.startSize = 0.6f; m.startSpeed = radius / life; m.gravityModifier = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.3f; sh.arc = 360; sh.rotation = new Vector3(90, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Emit(Mathf.RoundToInt(40 + radius * 12));
            Dust(pos, radius);
        }

        public static void Dust(Vector3 pos, float radius)
        {
            if (!alpha) return;
            var ps = Make(pos, alpha, 1.2f);
            var m = ps.main; m.startColor = new Color(0.55f, 0.48f, 0.38f, 0.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 0.6f; sh.rotation = new Vector3(90, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.5f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
            ps.Emit(Mathf.RoundToInt(10 + radius * 4));
        }

        /// <summary>Spirale de lames (tourbillon) autour d'un personnage pendant duration.</summary>
        public static void Whirl(Transform t, Color c, float radius, float duration)
        {
            if (!additive) return;
            var ps = Make(t.position + Vector3.up, additive, duration);
            ps.transform.SetParent(t, true);
            var m = ps.main; m.startColor = c; m.startSize = 0.35f; m.startSpeed = 0; m.startLifetime = 0.35f; m.duration = duration; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.rotation = new Vector3(90, 0, 0); sh.arcMode = ParticleSystemShapeMultiModeValue.Loop; sh.arcSpeed = 3f;
            var em = ps.emission; em.rateOverTime = 120;
            ps.Play();
        }
    }
}
