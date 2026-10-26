using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Quête à objectifs successifs : éliminer des ennemis d'un type, atteindre un lieu.</summary>
    public class Quest : MonoBehaviour
    {
        [System.Serializable] public class Objective
        {
            public string text; public string enemyId; public int count; public int done;
            public Vector3 place; public float radius;
            public bool Complete => enemyId != null && enemyId.Length > 0 ? done >= count : done > 0;
        }
        public string title = "Purifier la vallée";
        public List<Objective> steps = new List<Objective>();
        public int current;
        public System.Action<string> Notify;
        public Transform player;
        public bool Finished => current >= steps.Count;
        public Objective Current => Finished ? null : steps[current];

        void OnEnable() { Health.AnyDied += OnDied; }
        void OnDisable() { Health.AnyDied -= OnDied; }

        void OnDied(Health h)
        {
            if (Finished || h.faction != Faction.Enemy) return;
            var ai = h.GetComponent<EnemyAI>();
            var o = Current;
            if (ai && ai.type != null && o.enemyId == ai.type.id) { o.done++; Advance(); }
        }

        void Update()
        {
            if (Finished || !player) return;
            var o = Current;
            if (string.IsNullOrEmpty(o.enemyId) && o.radius > 0)
            {
                var d = player.position - o.place; d.y = 0;
                if (d.magnitude < o.radius) { o.done = 1; Advance(); }
            }
        }

        void Advance()
        {
            if (!Current.Complete) return;
            Notify?.Invoke("Objectif accompli : " + Current.text);
            current++;
            if (Finished) Notify?.Invoke($"Quête terminée : {title} !");
            else Notify?.Invoke("Nouvel objectif : " + Current.text);
        }

        public string Describe()
        {
            if (Finished) return $"<b>{title}</b>\nTerminée !";
            var o = Current;
            return $"<b>{title}</b>\n{o.text}" + (string.IsNullOrEmpty(o.enemyId) ? "" : $" ({o.done}/{o.count})");
        }

        /// <summary>Position vers laquelle pointe la boussole du HUD (lieu à atteindre ou ennemi vivant le plus proche du type demandé).</summary>
        public Vector3? Waypoint()
        {
            if (Finished || !player) return null;
            var o = Current;
            if (string.IsNullOrEmpty(o.enemyId)) return o.place;
            float best = 1e9f; Vector3? p = null;
            foreach (var h in Health.All)
            {
                var ai = h.GetComponent<EnemyAI>();
                if (h.Dead || !ai || ai.type == null || ai.type.id != o.enemyId) continue;
                float d = (h.transform.position - player.position).sqrMagnitude; if (d < best) { best = d; p = h.transform.position; }
            }
            return p;
        }
    }
}
