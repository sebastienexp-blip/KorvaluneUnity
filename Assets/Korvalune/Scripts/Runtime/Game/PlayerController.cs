using System.Collections;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Héros joueur : déplacement relatif à la caméra (joystick tactile ou ZQSD/flèches), attaque de base
    /// (touche Espace / bouton), quatre compétences (touches 1-4 / boutons), niveau et expérience.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 2.4f, runSpeed = 5.2f, turnSpeed = 720f, gravity = -20f;
        public float attackDamage = 18f, attackRange = 2.4f, attackCone = 140f, attackCooldown = 0.75f, critChance = 0.12f;
        public VirtualJoystick joystick;
        public Transform cam;
        public const int MaxLevel = 200;
        public int level = 1; public float xp, xpNext = 100; public int skillPoints = 1;

        /// <summary>Expérience nécessaire pour passer du niveau l au niveau l+1 (courbe longue jusqu'au niveau 200).</summary>
        public static float XpForLevel(int l) => Mathf.Round(80f * Mathf.Pow(l, 1.6f) + 40f * l);
        public float damageMult = 1f, buffUntil;
        public Skill[] skills;
        public System.Action<string> Notify;
        public System.Collections.Generic.List<ItemSave> inventory = new System.Collections.Generic.List<ItemSave>();

        public Health Health { get; private set; }
        public Animator Anim { get; private set; }
        CharacterController cc;
        float vy, nextAttack, lockUntil;
        public float Speed { get; private set; }
        public bool Busy => Time.time < lockUntil;

        void Awake() { Init(); }
        void OnEnable() { LootDrop.Picked += OnPicked; }
        void OnDisable() { LootDrop.Picked -= OnPicked; }
        void OnPicked(ItemSave it)
        {
            inventory.Add(it);
            Notify?.Invoke($"<color=#{ColorUtility.ToHtmlStringRGB(Loot.ColorOf(it.rarity))}>{it.name}</color> ({Loot.LabelOf(it.rarity)})");
        }

        /// <summary>Références et compétences (appelé par Awake ; appelable hors jeu pour les captures).</summary>
        public void Init()
        {
            cc = GetComponent<CharacterController>(); Health = GetComponent<Health>(); Anim = GetComponentInChildren<Animator>();
            if (skills == null || skills.Length == 0) skills = Skill.Defaults();
            xpNext = XpForLevel(level);
        }

        void Update()
        {
            if (Health == null || Health.Dead) { Anim?.SetFloat(CharacterRig.SpeedId, 0); return; }
            if (Time.time > buffUntil) damageMult = 1f;
            Vector2 inp = joystick ? joystick.Value : Vector2.zero;
            var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (kb.sqrMagnitude > inp.sqrMagnitude) inp = Vector2.ClampMagnitude(kb, 1);
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = fwd * inp.y + right * inp.x;
            float mag = Mathf.Clamp01(dir.magnitude);
            if (Busy) mag *= 0.15f;
            float spd = mag < 0.6f ? walkSpeed * mag / 0.6f : Mathf.Lerp(walkSpeed, runSpeed, (mag - 0.6f) / 0.4f);
            if (mag > 0.05f && !Health.Stunned)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
            else spd = 0;
            vy = cc.isGrounded ? -2f : vy + gravity * Time.deltaTime;
            cc.Move((dir.normalized * spd + Vector3.up * vy) * Time.deltaTime);
            Speed = spd;
            Anim?.SetFloat(CharacterRig.SpeedId, spd, 0.1f, Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) Attack();
            var runner = GetComponent<SkillRunner>();
            if (runner) { for (int i = 0; i < runner.loadout.Length; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) runner.Use(i); }
            else for (int i = 0; i < skills.Length && i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) UseSkill(i);
        }

        /// <summary>Tourne vers l'ennemi le plus proche à portée (assistance de visée mobile).</summary>
        public void FaceNearest(float range)
        {
            Health best = null; float bd = range;
            foreach (var h in Health.Around(transform.position, range, Faction.Player)) { float d = Vector3.Distance(h.transform.position, transform.position); if (d < bd) { bd = d; best = h; } }
            if (best) { var d = best.transform.position - transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d); }
        }

        public void Attack()
        {
            if (Health.Dead || Health.Stunned || Time.time < nextAttack || Busy) return;
            nextAttack = Time.time + attackCooldown;
            FaceNearest(attackRange + 2.5f);
            Anim?.SetTrigger(CharacterRig.AttackId);
            lockUntil = Time.time + 0.45f;
            StartCoroutine(Hit(0.38f));
        }

        IEnumerator Hit(float delay)
        {
            yield return new WaitForSeconds(delay);
            bool any = false;
            foreach (var h in Health.Around(transform.position, attackRange, Faction.Player, transform.forward, attackCone))
            {
                bool crit = Random.value < critChance;
                h.TakeDamage(attackDamage * damageMult * (crit ? 2f : 1f) * Random.Range(0.9f, 1.1f), Health, crit);
                FX.Burst(h.transform.position + Vector3.up * 1.1f, crit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.25f), crit ? 30 : 16);
                any = true;
            }
            if (any) FindFirstObjectByType<CameraRig>()?.Shake(0.06f);
        }

        public bool UseSkill(int i)
        {
            if (i < 0 || i >= skills.Length || Health.Dead || Health.Stunned) return false;
            var s = skills[i];
            if (Time.time < s.readyAt) return false;
            if (!Health.Spend(s.mana)) { Notify?.Invoke("Pas assez de mana"); return false; }
            s.readyAt = Time.time + s.cooldown;
            StartCoroutine(s.Run(this));
            return true;
        }

        public void Lock(float seconds) { lockUntil = Mathf.Max(lockUntil, Time.time + seconds); }
        public void Move(Vector3 delta) { if (cc.enabled) cc.Move(delta); }

        public void GainXp(float amount)
        {
            xp += amount;
            if (level >= MaxLevel) { xp = 0; return; }
            while (xp >= xpNext && level < MaxLevel)
            {
                xp -= xpNext; level++; xpNext = XpForLevel(level); skillPoints++;
                Health.maxMana += 8;
                var eq = GetComponent<Equipment>();
                if (eq) eq.AddBase(3, 15); else { Health.maxHp += 15; attackDamage += 3; }
                Health.Heal(Health.maxHp);
                Health.mana = Health.maxMana;
                FX.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.4f), 60, 4f, 0.25f, 1.0f, -0.3f);
                Notify?.Invoke(level >= MaxLevel ? "Niveau maximum (200) atteint !" : $"Niveau {level} !");
            }
            if (level >= MaxLevel) xp = 0;
        }
    }
}
