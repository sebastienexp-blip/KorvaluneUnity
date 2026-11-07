using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>GPS au sol : un tracé doré posé sur le terrain mène du héros à un point. Le point vient d'un clic sur la grande
    /// carte (manuel) ou, à défaut, de l'étape en cours de la quête principale. Activable ou désactivable dans les options
    /// (Settings.Gps) ; désactivé, rien n'est dessiné, mais les quêtes restent indiquées sur la mini-carte et le journal.</summary>
    public class GroundGps : MonoBehaviour
    {
        /// <summary>Point posé à la main sur la grande carte (dans la scène courante).</summary>
        public static Vector3? Manual;

        GameBootstrap game; LineRenderer trail, beam;

        void Start()
        {
            game = GetComponent<GameBootstrap>();
            Manual = null;   // un point posé dans une zone n'a pas de sens dans une autre
            trail = MakeLine(new Color(1f, 0.82f, 0.25f, 0.95f), 0.14f);
            beam = MakeLine(new Color(1f, 0.85f, 0.35f, 0.7f), 0.3f);
        }

        public static void SetTarget(Vector3 world) { Manual = world; }

        /// <summary>Points intermédiaires du tracé entre deux lieux (sans le départ ni l'arrivée), posés à plat : pour les cartes.</summary>
        public static System.Collections.Generic.List<Vector3> Samples(Vector3 from, Vector3 to, int segments)
        {
            var l = new System.Collections.Generic.List<Vector3>();
            var a = new Vector3(from.x, 0, from.z); var b = new Vector3(to.x, 0, to.z);
            for (int i = 1; i < segments; i++) l.Add(Vector3.Lerp(a, b, i / (float)segments));
            return l;
        }

        /// <summary>Le point à indiquer : celui posé à la main, sinon le lieu de l'étape de quête en cours.</summary>
        public static Vector3? Pick(Quest q)
        {
            if (Manual != null) return Manual;
            if (q == null || q.current < 0 || q.current >= q.steps.Count) return null;
            var place = q.steps[q.current].place;
            return place == Vector3.zero ? (Vector3?)null : place;
        }

        LineRenderer MakeLine(Color c, float width)
        {
            var go = new GameObject("GPS au sol"); go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.material = new Material(Shader.Find("Sprites/Default"));
            l.startColor = l.endColor = c; l.startWidth = l.endWidth = width;
            l.useWorldSpace = true; l.enabled = false; l.numCapVertices = 2;
            return l;
        }

        void Update()
        {
            if (!game || !game.Player || Rift.Active) { Hide(); return; }
            var target = Pick(game.Hud ? game.Hud.quest : null);
            if (Settings.Gps != 0 || target == null) { Hide(); return; }

            var from = game.Player.transform.position; var to = target.Value;
            var flat = to - from; flat.y = 0;
            if (flat.magnitude < 3f) { if (Manual != null) Manual = null; Hide(); return; }   // arrivé : le point s'efface

            // tracé : un point tous les deux mètres, posé sur le sol (et non dans la roche ni sous la carte)
            int n = Mathf.Max(2, Mathf.CeilToInt(flat.magnitude / 2f));
            trail.positionCount = n + 1;
            for (int i = 0; i <= n; i++)
            {
                var p = GameBootstrap.Ground(from + flat * (i / (float)n));
                trail.SetPosition(i, p + Vector3.up * 0.12f);
            }
            trail.enabled = true;

            // colonne de lumière au point d'arrivée, visible de loin
            var g = GameBootstrap.Ground(to);
            beam.positionCount = 2; beam.SetPosition(0, g); beam.SetPosition(1, g + Vector3.up * 6f);
            beam.enabled = true;
        }

        void Hide() { if (trail) trail.enabled = false; if (beam) beam.enabled = false; }
    }
}
