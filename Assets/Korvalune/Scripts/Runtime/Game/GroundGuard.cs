using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Filet de sécurité en faille : un monstre retrouvé sous le relief (sol non détecté à sa pose, poussée,
    /// bord de falaise) est remonté à la surface la plus haute située à hauteur de ses pieds. Seules les surfaces au
    /// niveau des pieds ou plus bas comptent : un monstre sous un toit n'est pas projeté dessus.</summary>
    public class GroundGuard : MonoBehaviour
    {
        float next;

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.25f;
            var all = Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (!h || h.faction != Faction.Enemy || h.hp <= 0) continue;
                var t = h.transform; var p = t.position;
                var hits = Physics.RaycastAll(p + Vector3.up * 60f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.NegativeInfinity; bool found = false;
                foreach (var hit in hits)
                {
                    if (hit.point.y > p.y + 1.2f) continue;          // au-dessus de la tête : toit ou plafond
                    if (hit.collider.transform.IsChildOf(t)) continue; // le monstre lui-même
                    if (hit.point.y > best) { best = hit.point.y; found = true; }
                }
                if (found && p.y < best - 0.3f) t.position = new Vector3(p.x, best + 0.05f, p.z);
            }
        }
    }
}
