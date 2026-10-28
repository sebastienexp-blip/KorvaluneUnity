using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Ligne de vue : murs, rochers, arbres, maisons et reliefs arrêtent attaques, compétences et projectiles.
    /// Les créatures (monstres, héros, invocations) et les zones de déclenchement ne bloquent pas.</summary>
    public static class Sight
    {
        static readonly RaycastHit[] hits = new RaycastHit[16];
        public const float EyeHeight = 1.1f;

        /// <summary>Vrai si rien de solide ne sépare les deux points (pris à hauteur de buste).</summary>
        public static bool Clear(Vector3 from, Vector3 to, float height = EyeHeight)
        {
            var a = from + Vector3.up * height; var b = to + Vector3.up * height;
            var d = b - a; float len = d.magnitude; if (len < 0.3f) return true;
            int n = Physics.RaycastNonAlloc(a, d / len, hits, len - 0.25f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Blocks(hits[i].collider)) return false;
            return true;
        }

        public static bool Clear(Vector3 from, Health h) => h && Clear(from, h.transform.position);

        /// <summary>Premier obstacle solide sur le trajet (pour arrêter un projectile contre un mur).</summary>
        public static bool Obstacle(Vector3 a, Vector3 b, out Vector3 point)
        {
            point = b; var d = b - a; float len = d.magnitude; if (len < 0.001f) return false;
            int n = Physics.RaycastNonAlloc(a, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool any = false;
            for (int i = 0; i < n; i++) if (Blocks(hits[i].collider) && hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; any = true; }
            return any;
        }

        static bool Blocks(Collider c)
        {
            if (!c || c.isTrigger) return false;
            if (c.GetComponentInParent<Health>()) return false;
            if (c is CharacterController) return false;
            return true;
        }
    }
}
