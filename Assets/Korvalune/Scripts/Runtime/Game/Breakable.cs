using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Objet destructible (tonneau, caisse, charrette, urne) : un coup suffit. Il éclate en débris et lâche parfois de
    /// l'or, plus rarement un objet ou une potion. Les attaques et compétences du héros le brisent au passage ; il n'attire pas la
    /// visée automatique (ce n'est pas un monstre).</summary>
    public class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();
        public float radius = 0.9f;
        bool broken;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static void Make(GameObject go, float radius = 0.9f) { if (go && !go.GetComponent<Breakable>()) go.AddComponent<Breakable>().radius = radius; }

        /// <summary>Brise tout ce qui se trouve dans la zone (frappe, explosion, projectile).</summary>
        public static void HitArea(Vector3 c, float r, Vector3? fwd = null, float cone = 360)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i]; if (!b || b.broken || !b.gameObject.activeInHierarchy) continue;
                var d = b.transform.position - c; d.y = 0;
                if (d.magnitude > r + b.radius) continue;
                if (fwd.HasValue && cone < 360 && d.sqrMagnitude > 0.01f && Vector3.Angle(fwd.Value, d) > cone * 0.5f) continue;
                b.Break();
            }
        }

        public void Break()
        {
            if (broken) return; broken = true;
            var p = transform.position;
            Vfx.Debris(p + Vector3.up * 0.5f, 1.2f);
            FX.Burst(p + Vector3.up * 0.6f, new Color(0.55f, 0.4f, 0.25f), 18, 4f, 0.12f, 0.6f, -1.2f);
            Sound.Play("sfx_coffre", p, 0.55f, 0.2f, 0.05f);
            var hero = FindFirstObjectByType<PlayerController>(); int lvl = hero ? hero.level : 1; var rng = new System.Random();
            float roll = Random.value;
            if (roll < 0.35f) LootDrop.SpawnGold((long)(Economy.GoldDrop(rng, lvl, 0) * 0.6f) + 2, LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.40f) LootDrop.Spawn(Loot.Roll(rng, lvl, 0.5f), LootDrop.Near(p, Random.insideUnitSphere));
            else if (roll < 0.44f && hero && hero.wallet.potions < Economy.MaxPotions) { hero.wallet.potions++; hero.Notify?.Invoke("+1 potion"); }
            Destroy(gameObject);
        }
    }
}
