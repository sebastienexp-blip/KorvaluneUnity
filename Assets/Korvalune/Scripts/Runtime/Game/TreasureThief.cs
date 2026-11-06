using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Chapardeur aux trésors : petite créature dorée chargée d'un sac de butin. Elle n'attaque jamais : dès que le
    /// héros approche, elle détale en semant quelques pièces quand on la frappe ; au bout de 25 s de fuite, elle ouvre un
    /// portail et disparaît avec son trésor. Abattue à temps, elle fait pleuvoir l'or et lâche plusieurs objets, souvent rares.</summary>
    public class TreasureThief : MonoBehaviour
    {
        public const float EscapeAfter = 25f;
        Health hp; CharacterController cc; Animator anim; Transform hero;
        float alarmedAt = -1, vy, nextCoins, lastHp; Vector3 vel; bool gone;
        Light glow; ParticleSystem sparkle;

        public static GameObject Make(GameBootstrap game, Vector3 pos, int level)
        {
            var cfg = game.config; var t = cfg ? cfg.Enemy("squelette") : null; if (t == null) return null;
            var e = game.Spawn(t, pos, level); if (!e) return null;
            var ai = e.GetComponent<EnemyAI>(); if (ai) ai.enabled = false;              // pas de combat : on prend la main
            var h = e.GetComponent<Health>();
            h.displayName = "Chapardeur aux trésors"; h.maxHp = Mathf.Round(h.maxHp * 3.2f); h.hp = h.maxHp; h.armor *= 0.5f;
            e.transform.localScale *= 0.72f;
            foreach (var r in e.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * new Color(1.25f, 1.0f, 0.45f)); }
            var th = e.AddComponent<TreasureThief>();
            // sac d'or sur le dos
            var bag = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(bag.GetComponent<Collider>()); bag.name = "Sac";
            bag.transform.SetParent(e.transform, false); bag.transform.localPosition = new Vector3(0, 1.25f, -0.38f); bag.transform.localScale = new Vector3(0.75f, 0.85f, 0.6f);
            if (LootDrop.litBase) { var m = new Material(LootDrop.litBase); m.SetColor("_BaseColor", new Color(0.42f, 0.28f, 0.14f)); bag.GetComponent<Renderer>().sharedMaterial = m; }
            var l = new GameObject("Éclat").AddComponent<Light>(); l.transform.SetParent(e.transform, false); l.transform.localPosition = Vector3.up * 1.4f;
            l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.3f); l.intensity = 2.6f; l.range = 5f; l.shadows = LightShadows.None; th.glow = l;
            Sound.Play("sfx_or", pos, 0.8f);
            // petit cri de gobelin à l'apparition : on l'entend avant de le voir
            Sound.Play("sfx_loup_grogne", pos, 0.35f, 0.25f);
            game.Hud?.Notify("<color=#ffd24a>Un gobelin chapardeur rôde : il emporte un sac d'or !</color>");
            return e;
        }

        void Start()
        {
            hp = GetComponent<Health>(); cc = GetComponent<CharacterController>(); anim = GetComponentInChildren<Animator>();
            lastHp = hp.hp;
            hp.Died += _ => Burst();
        }

        void Update()
        {
            if (gone || hp.Dead) return;
            if (!hero) { var p = FindFirstObjectByType<PlayerController>(); if (p) hero = p.transform; else return; }
            var away = transform.position - hero.position; away.y = 0; float d = away.magnitude;
            if (alarmedAt < 0 && (d < 13f || hp.hp < hp.maxHp)) { alarmedAt = Time.time; Sound.Play("sfx_or", transform.position, 1f, 0.2f); }
            if (glow) glow.intensity = 2.2f + 0.8f * Mathf.Sin(Time.time * 6f);
            Vector3 want = Vector3.zero; float speed = 0;
            if (alarmedAt >= 0)
            {
                // fuite : à l'opposé du héros, en zigzag, en contournant les obstacles
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                dir = Quaternion.Euler(0, 35f * Mathf.Sin(Time.time * 1.7f), 0) * dir;
                if (Sight.Obstacle(transform.position + Vector3.up * 0.8f, transform.position + Vector3.up * 0.8f + dir * 2.5f, out _))
                    dir = Quaternion.Euler(0, Random.value < 0.5f ? 90 : -90, 0) * dir;
                want = dir; speed = d < 25f ? 5.6f : 2.5f;
                // frappé : il sème quelques pièces
                if (hp.hp < lastHp - 0.5f && Time.time > nextCoins) { nextCoins = Time.time + 0.6f; LootDrop.SpawnGold(GoldBit(), LootDrop.Near(transform.position, Random.insideUnitSphere * 1.2f)); }
                if (Time.time - alarmedAt > EscapeAfter) { Escape(); return; }
            }
            lastHp = hp.hp;
            vel = Vector3.MoveTowards(vel, want * speed, 20f * Time.deltaTime);
            if (vel.sqrMagnitude > 0.05f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(vel), 720f * Time.deltaTime);
            vy = cc && cc.isGrounded ? -2f : vy - 20f * Time.deltaTime;
            if (cc && cc.enabled) cc.Move((vel + Vector3.up * vy) * Time.deltaTime); else transform.position += vel * Time.deltaTime;
            if (anim) anim.SetFloat(CharacterRig.SpeedId, vel.magnitude, 0.1f, Time.deltaTime);
        }

        int Level => hp.level > 0 ? hp.level : 1;
        long GoldBit() => (long)(Economy.GoldDrop(new System.Random(), Level, 0.3f) * 0.4f * Difficulty.Gold) + 3;

        /// <summary>Abattu : pluie d'or et objets (au moins un rare, souvent un légendaire).</summary>
        void Burst()
        {
            var rng = new System.Random(); var pos = transform.position;
            FX.Burst(pos + Vector3.up, new Color(1f, 0.82f, 0.25f), 60, 7f, 0.15f, 0.9f, 1.2f);
            Sound.Play("sfx_butin_legendaire", pos, 1f);
            for (int i = 0; i < 9; i++) { var o = Random.insideUnitCircle * 2.4f; LootDrop.SpawnGold(GoldBit() * 3, LootDrop.Near(pos, new Vector3(o.x, 0, o.y))); }
            int items = 3 + rng.Next(3);
            for (int i = 0; i < items; i++)
            {
                var it = Loot.Roll(rng, Level, 4f + Difficulty.Luck);
                if (i == 0 && it.rarity < Rarity.Rare) it = Loot.Roll(rng, Level, 30f);
                var o = Random.insideUnitCircle * 1.8f; LootDrop.Spawn(it, LootDrop.Near(pos, new Vector3(o.x, 0, o.y)));
            }
            var p = hero ? hero.GetComponent<PlayerController>() : null; p?.Notify?.Invoke("<color=#ffd24a>Le chapardeur lâche son trésor !</color>");
            if (p) p.GainXp(Mathf.Round(40f * Mathf.Pow(Level, 1.25f) * Difficulty.Xp));
        }

        /// <summary>Trop tard : il s'enfuit par un portail doré.</summary>
        void Escape()
        {
            gone = true;
            FX.Burst(transform.position + Vector3.up, new Color(1f, 0.8f, 0.3f), 40, 4f, 0.2f, 0.8f, 0.8f);
            Vfx.Shock(transform.position, new Color(1f, 0.8f, 0.3f), 2.5f);
            Sound.Play("sfx_portail", transform.position, 0.9f);
            var p = hero ? hero.GetComponent<PlayerController>() : null; p?.Notify?.Invoke("Le chapardeur s'est échappé avec son trésor…");
            Destroy(gameObject, 0.15f);
        }
    }
}
