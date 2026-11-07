using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Boss d'évènement : dans une région extérieure, un boss mondial apparaît au hasard à quelques dizaines de mètres
    /// du héros, annoncé dans le journal et repéré sur la carte. S'il n'est pas abattu dans les dix minutes, il se retire dans
    /// la brume. Pas de boss mondial dans les failles ni dans la ville.</summary>
    public class WorldBossEvents : MonoBehaviour
    {
        const float GapMin = 720f, GapMax = 1200f, Lifetime = 600f;
        GameBootstrap game; Health alive; float nextAt, spawnedAt;

        void Start()
        {
            game = GetComponent<GameBootstrap>();
            nextAt = Time.time + Random.Range(GapMin, GapMax);
        }

        void Update()
        {
            if (!game || !game.Player || Rift.Active || Cinematic.Active || Waypoint.Here == null) return;

            if (alive != null)
            {
                if (alive.Dead) { alive = null; nextAt = Time.time + Random.Range(GapMin, GapMax); return; }
                if (Time.time > spawnedAt + Lifetime)
                {
                    game.Hud?.Notify("Le boss s'est retiré dans la brume…");
                    Destroy(alive.gameObject); alive = null; nextAt = Time.time + Random.Range(GapMin, GapMax);
                }
                return;
            }
            if (Time.time < nextAt) return;

            var d = BossCatalog.All[Random.Range(0, BossCatalog.All.Count)];
            var hero = game.Player.transform;
            var dir = Random.insideUnitCircle.normalized;
            var p = GameBootstrap.Ground(hero.position + new Vector3(dir.x, 0, dir.y) * Random.Range(30f, 45f));
            var e = BossCatalog.Spawn(game, d, p);
            if (!e) { nextAt = Time.time + 60f; return; }
            alive = e.GetComponent<Health>(); spawnedAt = Time.time;
            game.Hud?.Notify($"<color=#ff7a5a>Un boss rôde sur la carte : {d.title}</color>");
            Sound.Play("sfx_demon_rugit", p, 1f);
            Vfx.Shock(p, new Color(1f, 0.5f, 0.3f), 5f);
        }
    }
}
