using System.Collections;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Compétences de la Voie du Guerrier (arbre de compétences) : Tourbillon, Bond fracassant, Cri de guerre, Piétinement.</summary>
    public class Skill
    {
        public string id, label, description; public float cooldown, mana, readyAt; public Color color;
        public System.Func<PlayerController, IEnumerator> body;
        public IEnumerator Run(PlayerController p) => body(p);
        public float Remaining => Mathf.Max(0, readyAt - Time.time);

        public static Skill[] Defaults() => new[]
        {
            new Skill { id = "whirlwind", label = "Tourbillon", description = "Tournoie en frappant tous les ennemis proches.", cooldown = 6f, mana = 25, color = new Color(1f, 0.6f, 0.2f), body = Whirlwind },
            new Skill { id = "leap", label = "Bond fracassant", description = "Bondit en avant et écrase le sol.", cooldown = 8f, mana = 30, color = new Color(1f, 0.45f, 0.15f), body = LeapSlam },
            new Skill { id = "warcry", label = "Cri de guerre", description = "Dégâts +50 % pendant 8 s.", cooldown = 15f, mana = 20, color = new Color(1f, 0.25f, 0.2f), body = WarCry },
            new Skill { id = "stomp", label = "Piétinement", description = "Étourdit les ennemis autour de vous.", cooldown = 10f, mana = 30, color = new Color(0.6f, 0.75f, 1f), body = Stomp },
        };

        static IEnumerator Whirlwind(PlayerController p)
        {
            p.Lock(1.2f);
            FX.Whirl(p.transform, new Color(1f, 0.6f, 0.25f), 2.2f, 1.1f);
            float t = 0; int ticks = 0; float startYaw = p.transform.eulerAngles.y;
            while (t < 1.1f)
            {
                t += Time.deltaTime;
                p.transform.rotation = Quaternion.Euler(0, startYaw + t * 1080f, 0);
                if (t > ticks * 0.27f && ticks < 4)
                {
                    ticks++;
                    foreach (var h in Health.Around(p.transform.position, 3.0f, Faction.Player)) { h.TakeDamage(p.attackDamage * 0.8f * p.damageMult, p.Health); FX.Burst(h.transform.position + Vector3.up, new Color(1, 0.6f, 0.2f), 10); }
                }
                yield return null;
            }
        }

        static IEnumerator LeapSlam(PlayerController p)
        {
            p.FaceNearest(9f);
            p.Lock(0.9f);
            p.Anim?.SetTrigger(CharacterRig.DodgeId);
            Vector3 start = p.transform.position, fwd = p.transform.forward;
            float t = 0, dur = 0.45f, dist = 6f;
            while (t < dur)
            {
                float dt = Time.deltaTime; t += dt;
                float y = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)) * 1.6f;
                var want = start + fwd * dist * Mathf.Clamp01(t / dur);
                var d = want - p.transform.position; d.y = 0;
                p.Move(d + Vector3.up * (y - (p.transform.position.y - start.y)) * 0.5f);
                yield return null;
            }
            FX.Ring(p.transform.position, new Color(1f, 0.5f, 0.15f), 3.5f);
            Object.FindFirstObjectByType<CameraRig>()?.Shake(0.25f);
            foreach (var h in Health.Around(p.transform.position, 3.5f, Faction.Player)) { h.TakeDamage(p.attackDamage * 2.2f * p.damageMult, p.Health); h.Stun(0.6f); }
        }

        static IEnumerator WarCry(PlayerController p)
        {
            p.damageMult = 1.5f; p.buffUntil = Time.time + 8f;
            FX.Ring(p.transform.position, new Color(1f, 0.2f, 0.15f), 5f, 0.6f);
            FX.Burst(p.transform.position + Vector3.up * 1.6f, new Color(1f, 0.3f, 0.2f), 50, 3f, 0.3f, 1f, -0.2f);
            p.Notify?.Invoke("Cri de guerre : dégâts +50 %");
            yield break;
        }

        static IEnumerator Stomp(PlayerController p)
        {
            p.Lock(0.5f);
            p.Anim?.SetTrigger(CharacterRig.AttackId);
            yield return new WaitForSeconds(0.3f);
            FX.Ring(p.transform.position, new Color(0.55f, 0.75f, 1f), 4.5f);
            Object.FindFirstObjectByType<CameraRig>()?.Shake(0.3f);
            foreach (var h in Health.Around(p.transform.position, 4.5f, Faction.Player)) { h.TakeDamage(p.attackDamage * 1.2f * p.damageMult, p.Health); h.Stun(2.5f); }
        }
    }
}
