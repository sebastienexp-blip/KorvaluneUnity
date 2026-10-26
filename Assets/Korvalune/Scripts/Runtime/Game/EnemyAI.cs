using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Ennemi : garde son camp, repère le joueur (rayon d'aggro), le poursuit, attaque au contact avec un
    /// temps de préparation (fenêtre d'esquive), recule vers son camp s'il est trop loin ; meurt, rapporte de l'XP.</summary>
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        public GameConfig.EnemyType type;
        public Transform target;
        public Vector3 home;
        public float leash = 28f;
        /// <summary>Camp pour lequel la créature se bat : Enemy (monstres) ou Player (invocations du héros).</summary>
        public Faction side = Faction.Enemy;
        public Transform owner;               // invocations : le héros à suivre
        public float lifetime;                // invocations : durée de vie (s), 0 = permanente
        public float damageOverride;          // invocations : dégâts calqués sur le héros
        float bornAt;
        Health hp; Animator anim; CharacterController cc;
        float nextAttack, vy, wanderAt; Vector3 wanderTo;
        enum S { Idle, Chase, Return }
        S state;

        void Start()
        {
            hp = GetComponent<Health>(); anim = GetComponentInChildren<Animator>(); cc = GetComponent<CharacterController>();
            home = transform.position; wanderTo = home; bornAt = Time.time;
            hp.Died += _ =>
            {
                if (side == Faction.Player) { FX.Burst(transform.position + Vector3.up, new Color(0.5f, 0.8f, 1f), 20, 3f); Destroy(gameObject, 3f); return; }
                var p = FindFirstObjectByType<PlayerController>();
                if (p) p.GainXp(type.xp * (1 + 0.04f * (p.level - 1)));
                // butin : les ennemis forts lâchent plus d'objets, de meilleure rareté
                var rng = new System.Random();
                int lvl = p ? p.level : 1; float elite = Mathf.Clamp01(type.xp / 300f);
                int n = (rng.NextDouble() < 0.45 + 0.5 * elite ? 1 : 0) + (elite > 0.9f ? 2 : 0);
                for (int i = 0; i < n; i++)
                {
                    var it = Loot.Roll(rng, lvl, elite * 3f);
                    var off = Random.insideUnitCircle * 1.4f;
                    LootDrop.Spawn(it, GameBootstrap.Ground(transform.position + new Vector3(off.x, 0, off.y)));
                }
                Destroy(gameObject, 8f);
            };
        }

        void Update()
        {
            if (hp.Dead) return;
            if (side == Faction.Player) { AllyUpdate(); return; }
            if (!target || Time.frameCount % 20 == 0) PickTarget();
            float speed = 0;
            Vector3 move = Vector3.zero;
            if (hp.Stunned) { Anim(0); Gravity(Vector3.zero); return; }
            var th = target ? target.GetComponent<Health>() : null;
            float dist = target ? Flat(target.position - transform.position).magnitude : 999f;
            float fromHome = Flat(transform.position - home).magnitude;
            switch (state)
            {
                case S.Idle:
                    if (target && th && !th.Dead && dist < type.aggro) state = S.Chase;
                    else
                    {
                        if (Time.time > wanderAt) { wanderAt = Time.time + Random.Range(3f, 7f); var r = Random.insideUnitCircle * 4f; wanderTo = home + new Vector3(r.x, 0, r.y); }
                        var d = Flat(wanderTo - transform.position);
                        if (d.magnitude > 0.6f) { move = d.normalized; speed = type.speed * 0.35f; }
                    }
                    break;
                case S.Chase:
                    if (!target || th == null || th.Dead || fromHome > leash) { state = S.Return; break; }
                    var dir = Flat(target.position - transform.position);
                    if (dist > type.attackRange * 0.9f) { move = dir.normalized; speed = type.speed; }
                    else
                    {
                        Face(dir);
                        if (Time.time > nextAttack) { nextAttack = Time.time + type.attackCooldown; StartCoroutine(Strike(th)); }
                    }
                    break;
                case S.Return:
                    var back = Flat(home - transform.position);
                    if (back.magnitude < 1.5f) { state = S.Idle; hp.Heal(hp.maxHp); }
                    else { move = back.normalized; speed = type.speed * 1.2f; }
                    if (target && th && !th.Dead && dist < type.aggro * 0.6f && fromHome < leash * 0.7f) state = S.Chase;
                    break;
            }
            // séparation entre ennemis
            foreach (var o in Health.All)
            {
                if (o == hp || o.faction != side || o.Dead) continue;
                var d = Flat(transform.position - o.transform.position); float m = d.magnitude;
                if (m < 1.2f * type.scale && m > 0.001f) move += d / m * (1.2f * type.scale - m) * 1.5f;
            }
            if (move.sqrMagnitude > 0.0001f && speed > 0.01f) Face(move);
            Gravity(move.normalized * speed);
            Anim(speed);
        }

        /// <summary>Cible la plus proche du camp adverse (héros ou ses invocations) ; le héros par défaut.</summary>
        void PickTarget()
        {
            Transform best = null; float bd = type.aggro;
            foreach (var h in Health.Around(transform.position, type.aggro, side))
            { float d = Flat(h.transform.position - transform.position).magnitude; if (d < bd) { bd = d; best = h.transform; } }
            if (!best) { var p = FindFirstObjectByType<PlayerController>(); if (p) best = p.transform; }
            if (state != S.Chase || !target) target = best;
            else if (best && best != target && bd < Flat(target.position - transform.position).magnitude * 0.6f) target = best;
        }

        /// <summary>Invocation : attaque l'ennemi le plus proche du héros, sinon le suit ; disparaît à la fin de sa durée.</summary>
        void AllyUpdate()
        {
            if (lifetime > 0 && Time.time - bornAt > lifetime) { hp.TakeDamage(hp.hp + 999, null); return; }
            if (hp.Stunned) { Anim(0); Gravity(Vector3.zero); return; }
            var center = owner ? owner.position : transform.position;
            Health foe = null; float bd = 14f;
            foreach (var h in Health.Around(center, 14f, Faction.Player))
            { float d = Flat(h.transform.position - transform.position).magnitude; if (d < bd) { bd = d; foe = h; } }
            Vector3 move = Vector3.zero; float speed = 0;
            if (foe)
            {
                var dir = Flat(foe.transform.position - transform.position);
                if (bd > type.attackRange * 0.9f) { move = dir.normalized; speed = type.speed * 1.1f; }
                else { Face(dir); if (Time.time > nextAttack) { nextAttack = Time.time + type.attackCooldown; StartCoroutine(Strike(foe)); } }
            }
            else if (owner)
            {
                var d = Flat(owner.position - transform.position);
                if (d.magnitude > 18f) { cc.enabled = false; transform.position = GameBootstrap.Ground(owner.position - owner.forward * 2f); cc.enabled = true; }
                else if (d.magnitude > 3.5f) { move = d.normalized; speed = type.speed * (d.magnitude > 8 ? 1.4f : 0.9f); }
            }
            foreach (var o in Health.All)
            {
                if (o == hp || o.faction != Faction.Player || o.Dead) continue;
                var d = Flat(transform.position - o.transform.position); float m = d.magnitude;
                if (m < 1.2f * type.scale && m > 0.001f) move += d / m * (1.2f * type.scale - m) * 1.5f;
            }
            if (move.sqrMagnitude > 0.0001f && speed > 0.01f) Face(move);
            Gravity(move.normalized * speed);
            Anim(speed);
        }

        System.Collections.IEnumerator Strike(Health th)
        {
            if (anim) anim.SetTrigger(CharacterRig.AttackId);
            yield return new WaitForSeconds(type.hitDelay);
            if (hp.Dead || hp.Stunned || th == null || th.Dead) yield break;
            var d = Flat(th.transform.position - transform.position);
            if (d.magnitude <= type.attackRange + 0.6f && Vector3.Angle(transform.forward, d) < 75f)
            {
                th.TakeDamage((damageOverride > 0 ? damageOverride : type.damage) * Random.Range(0.85f, 1.15f), hp);
                FX.Burst(th.transform.position + Vector3.up * 1.1f, new Color(0.9f, 0.1f, 0.08f), 14, 4f, 0.15f, 0.4f);
            }
        }

        void Gravity(Vector3 v)
        {
            if (cc && cc.enabled)
            {
                vy = cc.isGrounded ? -2f : vy - 20f * Time.deltaTime;
                cc.Move((v + Vector3.up * vy) * Time.deltaTime);
            }
            else transform.position += v * Time.deltaTime;
        }

        void Face(Vector3 d) { d.y = 0; if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 540f * Time.deltaTime); }
        void Anim(float s) { if (anim) anim.SetFloat(CharacterRig.SpeedId, s, 0.1f, Time.deltaTime); }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
