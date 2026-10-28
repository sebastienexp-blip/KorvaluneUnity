using System;
using UnityEngine;

namespace Korvalune.Game
{
    public enum Faction { Player, Enemy }

    /// <summary>Points de vie, mana, mort, étourdissement. Pilote les paramètres Hit / Dead de l'Animator.</summary>
    public class Health : MonoBehaviour
    {
        public Faction faction = Faction.Enemy;
        public float maxHp = 100, hp = 100, maxMana = 0, mana = 0, manaRegen = 4f, hpRegen = 0f;
        public float armor = 0f;                         // réduction plate des dégâts
        public string displayName = "";
        /// <summary>Niveau affiché au-dessus de la créature (0 = non affiché).</summary>
        public int level;
        public float StunnedUntil { get; private set; }
        public bool Dead => hp <= 0;
        public bool Stunned => Time.time < StunnedUntil;
        public event Action<float, Health> Damaged;      // (montant, attaquant)
        public event Action<Health> Died;
        public static event Action<Health, float, bool> AnyDamaged;   // (cible, montant, critique) — chiffres flottants
        public static event Action<Health> AnyDied;
        Animator anim;
        float lastHitAnim, lastGrunt;

        void Awake() { anim = GetComponentInChildren<Animator>(); hp = maxHp; mana = maxMana; }

        void Update()
        {
            if (Dead) return;
            if (maxMana > 0) mana = Mathf.Min(maxMana, mana + manaRegen * Time.deltaTime);
            if (hpRegen > 0) hp = Mathf.Min(maxHp, hp + hpRegen * Time.deltaTime);
        }

        public bool Spend(float m) { if (mana < m) return false; mana -= m; return true; }

        public void Stun(float seconds) { if (!Dead) StunnedUntil = Mathf.Max(StunnedUntil, Time.time + seconds); }

        /// <summary>Modificateur des dégâts reçus (boucliers, réductions) : appliqué avant l'armure.</summary>
        public System.Func<float, float> modifyIncoming;

        public void TakeDamage(float amount, Health source, bool crit = false)
        {
            if (Dead || amount <= 0) return;
            if (modifyIncoming != null) amount = modifyIncoming(amount);
            amount = Mathf.Max(1f, amount - armor);
            hp = Mathf.Max(0, hp - amount);
            Damaged?.Invoke(amount, source);
            if (faction == Faction.Player && hp > 0 && GetComponent<PlayerController>() && Time.time - lastGrunt > 0.9f)
            { lastGrunt = Time.time; Sound.Play(SaveSystem.Current != null && SaveSystem.Current.female ? "sfx_heros_touche_f" : "sfx_heros_touche_h", transform.position, 0.8f); }
            else if (faction == Faction.Enemy && source && source.faction == Faction.Player) Sound.Play(crit ? "sfx_critique" : "sfx_ennemi_touche", transform.position, crit ? 0.9f : 0.45f, 0.12f, 0.06f);
            AnyDamaged?.Invoke(this, amount, crit);
            if (anim && hp > 0 && Time.time - lastHitAnim > 0.6f) { anim.SetTrigger(CharacterRig.HitId); lastHitAnim = Time.time; }
            if (hp <= 0)
            {
                if (anim) anim.SetBool(CharacterRig.DeadId, true);
                foreach (var c in GetComponents<Collider>()) c.enabled = false;
                var cc = GetComponent<CharacterController>(); if (cc) cc.enabled = false;
                if (faction == Faction.Player && GetComponent<PlayerController>()) Sound.Play("sfx_heros_mort", transform.position);
                Died?.Invoke(this); AnyDied?.Invoke(this);
            }
        }

        public void Heal(float amount) { if (!Dead) hp = Mathf.Min(maxHp, hp + amount); }

        public void Revive()
        {
            hp = maxHp; mana = maxMana;
            if (anim) anim.SetBool(CharacterRig.DeadId, false);
            foreach (var c in GetComponents<Collider>()) c.enabled = true;
            var cc = GetComponent<CharacterController>(); if (cc) cc.enabled = true;
        }

        /// <summary>Toutes les cibles vivantes de la faction opposée dans un rayon (et éventuellement un cône).</summary>
        public static System.Collections.Generic.List<Health> Around(Vector3 center, float radius, Faction hostileTo, Vector3? fwd = null, float coneDeg = 360)
        {
            var list = new System.Collections.Generic.List<Health>();
            foreach (var h in All)
            {
                if (h == null || h.Dead || h.faction == hostileTo) continue;
                var d = h.transform.position - center; d.y = 0;
                if (d.magnitude > radius) continue;
                if (fwd.HasValue && coneDeg < 360 && d.sqrMagnitude > 0.01f && Vector3.Angle(fwd.Value, d) > coneDeg * 0.5f) continue;
                list.Add(h);
            }
            return list;
        }

        public static readonly System.Collections.Generic.List<Health> All = new System.Collections.Generic.List<Health>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }
}
