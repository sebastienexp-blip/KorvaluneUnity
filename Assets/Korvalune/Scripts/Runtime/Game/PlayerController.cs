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
        /// <summary>Chaque niveau coûte nettement plus que le précédent (courbe en puissance 2,15), avec des paliers à 50, 100 et 150 où la
        /// progression ralentit encore : niveau 2 ≈ 285 XP, niveau 10 ≈ 5 600, niveau 50 ≈ 210 000, niveau 100 ≈ 1,2 million.</summary>
        public static float XpForLevel(int l)
        {
            float tier = l >= 150 ? 2.2f : l >= 100 ? 1.7f : l >= 50 ? 1.3f : 1f;
            return Mathf.Round((35f * Mathf.Pow(l, 2.15f) + 65f * l) * tier);
        }
        public float damageMult = 1f, buffUntil;
        public Skill[] skills;
        public System.Action<string> Notify;
        public System.Collections.Generic.List<ItemSave> inventory = new System.Collections.Generic.List<ItemSave>();
        public Wallet wallet = new Wallet();
        public float ElixirXp => wallet.elixir == "savoir" && Time.time < wallet.elixirUntil ? 1.2f : 1f;
        float potionReady;
        public float PotionCooldown => Mathf.Max(0, potionReady - Time.time);
        /// <summary>Potion de soin : rend 45 % de la vie maximale, 15 s de recharge.</summary>
        public bool DrinkPotion()
        {
            if (wallet.potions <= 0 || Time.time < potionReady || Health.Dead) return false;
            wallet.potions--; potionReady = Time.time + 15f; Health.Heal(Health.maxHp * 0.45f); Sound.UI("sfx_potion", 0.9f);
            FX.Burst(transform.position + Vector3.up, new Color(1f, 0.25f, 0.2f), 30, 2.5f, 0.15f, 0.8f, -0.4f);
            return true;
        }
        public void AddGold(long g) { wallet.gold += g; GoldGained?.Invoke(g); if (g > 0) Sound.Play("sfx_or", transform.position, 0.55f, 0.06f, 0.25f); }
        public System.Action<long> GoldGained;

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
            inventory.Add(it); Sound.UI("sfx_ramasser", 0.7f);
            Notify?.Invoke($"<color=#{ColorUtility.ToHtmlStringRGB(Loot.DisplayColor(it))}>{it.name}</color> ({(string.IsNullOrEmpty(it.set) ? Loot.LabelOf(it.rarity) : "ensemble")})");
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
            UpdateSwim();
            if (Swimming)
            {   // nage : le héros flotte, la tête hors de l'eau, et avance plus lentement
                spd *= 0.55f;
                float surfaceY = GameBootstrap.WaterLevel - SwimDepth;
                vy = Mathf.Clamp((surfaceY - transform.position.y) * 4f, -3f, 3f);
            }
            else vy = cc.isGrounded ? -2f : Mathf.Max(vy + gravity * Time.deltaTime, -30f);    // vitesse de chute limitée
            cc.Move((dir.normalized * spd + Vector3.up * vy) * Time.deltaTime);
            KeepInsideWorld();
            FallGuard();
            Speed = spd;
            if (Swimming) SwimStrokes(spd); else Footsteps(spd);
            Anim?.SetFloat(CharacterRig.SpeedId, spd, 0.1f, Time.deltaTime);
            UpdateTarget(inp);
            if (approach && Target && !Target.Dead && mag < 0.05f && !Health.Stunned)
            {
                // ciblage automatique : le héros court vers sa cible puis frappe
                var to = Target.transform.position - transform.position; to.y = 0;
                if (to.magnitude > attackRange * 0.85f && Time.time < approachUntil)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
                    cc.Move(to.normalized * runSpeed * Time.deltaTime); Anim?.SetFloat(CharacterRig.SpeedId, runSpeed, 0.05f, Time.deltaTime); Speed = runSpeed;
                }
                else { approach = false; Attack(); }
            }
            else if (mag >= 0.05f) approach = false;
            // marche vers un point touché (entrée de donjon…) ; le joystick reprend la main
            if (walkTo.HasValue)
            {
                var to = walkTo.Value - transform.position; to.y = 0;
                if (mag >= 0.05f || Health.Stunned) { walkTo = null; arrived = null; }
                else if (to.magnitude > 0.9f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
                    cc.Move(to.normalized * runSpeed * Time.deltaTime); Anim?.SetFloat(CharacterRig.SpeedId, runSpeed, 0.05f, Time.deltaTime); Speed = runSpeed;
                }
                else { walkTo = null; var a = arrived; arrived = null; a?.Invoke(); }
            }
            if (Input.GetKeyDown(KeyCode.Space)) Attack();
            var runner = GetComponent<SkillRunner>();
            if (runner) { for (int i = 0; i < runner.loadout.Length; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) runner.Use(i); }
            else for (int i = 0; i < skills.Length && i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) UseSkill(i);
        }

        // ------------------------------------------------------------------ ciblage automatique
        /// <summary>Cible automatique : l'ennemi le plus pertinent dans un rayon de 12 m — le plus proche, en privilégiant celui
        /// qui se trouve dans la direction du joystick (ou devant le héros) ; la cible est conservée tant qu'elle reste valable.</summary>
        public Health Target { get; private set; }
        public float autoAimRange = 14f;
        bool approach; float approachUntil;
        /// <summary>Destination touchée (le héros y court seul) et action à l'arrivée.</summary>
        public Vector3? walkTo; public System.Action arrived;
        public void WalkTo(Vector3 p, System.Action onArrive) { walkTo = p; arrived = onArrive; approach = false; }
        Transform marker;

        void UpdateTarget(Vector2 stick)
        {
            Vector3 want = transform.forward;
            if (stick.sqrMagnitude > 0.04f && cam)
            {
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f);
                want = (f * stick.y + r * stick.x).normalized;
            }
            Health best = null; float bs = float.MaxValue;
            foreach (var h in Health.Around(transform.position, autoAimRange, Faction.Player))
            {
                var d = h.transform.position - transform.position; d.y = 0; float dist = d.magnitude;
                float ang = dist > 0.01f ? Vector3.Angle(want, d) : 0;
                float score = dist * (1f + ang / 90f) * (h == Target ? 0.7f : 1f);     // proche, devant, et un peu de constance
                if (score < bs) { bs = score; best = h; }
            }
            Target = best;
            // marqueur au sol sous la cible
            if (!marker) marker = TargetMarker();
            marker.gameObject.SetActive(Target);
            if (Target) { marker.position = Target.transform.position + Vector3.up * 0.06f; marker.Rotate(0, 90 * Time.deltaTime, 0); }
        }

        static Transform TargetMarker()
        {
            var go = new GameObject("Cible"); var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 40; lr.widthMultiplier = 0.07f;
            for (int i = 0; i < 40; i++) { float a = i / 40f * Mathf.PI * 2; lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.9f); }
            lr.sharedMaterial = FX.additive ? FX.additive : new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 0.35f, 0.15f, 0.9f);
            return go.transform;
        }

        /// <summary>Tourne vers la cible automatique (ou l'ennemi le plus proche à portée).</summary>
        public void FaceNearest(float range)
        {
            Health best = Target && !Target.Dead && Vector3.Distance(Target.transform.position, transform.position) <= range ? Target : null;
            if (!best)
            {
                float bd = range;
                foreach (var h in Health.Around(transform.position, range, Faction.Player)) { float d = Vector3.Distance(h.transform.position, transform.position); if (d < bd) { bd = d; best = h; } }
            }
            if (!best && Target && !Target.Dead) best = Target;           // hors de portée : on se tourne quand même vers la cible
            if (best) { var d = best.transform.position - transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d); }
        }

        /// <summary>Cible des compétences : la cible automatique, sinon l'ennemi vivant le plus proche dans le rayon donné.</summary>
        public Health AcquireTarget(float range)
        {
            if (Target && !Target.Dead && Vector3.Distance(Target.transform.position, transform.position) <= range) return Target;
            Health best = null; float bd = range;
            foreach (var h in Health.Around(transform.position, range, Faction.Player)) { float d = Vector3.Distance(h.transform.position, transform.position); if (d < bd) { bd = d; best = h; } }
            return best;
        }

        // ------------------------------------------------------------------ eau et bords du monde
        public bool Swimming { get; private set; }
        const float SwimDepth = 1.25f;       // pieds sous la surface : épaules à fleur d'eau
        float strokeTimer;
        /// <summary>Le héros nage dès que le fond est trop profond pour avoir pied (eau de la vallée).</summary>
        void UpdateSwim()
        {
            float w = GameBootstrap.WaterLevel;
            bool was = Swimming;
            if (w < -9000f) { Swimming = false; return; }
            var t = Terrain.activeTerrain;
            float floor = t ? t.SampleHeight(transform.position) + t.transform.position.y : transform.position.y;
            float depth = w - floor;
            Swimming = Swimming ? depth > SwimDepth - 0.15f : depth > SwimDepth + 0.05f && transform.position.y < w - SwimDepth + 0.35f;
            if (Swimming && !was) { Sound.Play("sfx_plouf", transform.position, 0.8f, 0.08f, 0.5f); FX.Burst(new Vector3(transform.position.x, w + 0.05f, transform.position.z), new Color(0.75f, 0.85f, 0.95f), 26, 3f, 0.12f, 0.6f, 0.5f); }
            if (Swimming) { fallingSince = -1; hasSafe = false; }
        }

        void SwimStrokes(float spd)
        {
            if (spd < 0.3f) { strokeTimer = 0.2f; return; }
            strokeTimer -= Time.deltaTime;
            if (strokeTimer > 0) return;
            strokeTimer = 0.95f;
            Sound.Play("sfx_nage", transform.position, 0.5f, 0.1f, 0.3f);
            FX.Burst(new Vector3(transform.position.x, GameBootstrap.WaterLevel + 0.05f, transform.position.z) + transform.forward * 0.5f, new Color(0.8f, 0.88f, 0.95f), 10, 2f, 0.1f, 0.4f, 0.4f);
        }

        /// <summary>Bords du monde : le héros ne peut pas quitter le terrain (plus de chute dans le vide aux extrémités).</summary>
        void KeepInsideWorld()
        {
            var t = Terrain.activeTerrain; if (!t || !t.terrainData) return;
            var o = t.transform.position; var s = t.terrainData.size; const float m = 6f;
            var p = transform.position;
            float x = Mathf.Clamp(p.x, o.x + m, o.x + s.x - m), z = Mathf.Clamp(p.z, o.z + m, o.z + s.z - m);
            if (!Mathf.Approximately(x, p.x) || !Mathf.Approximately(z, p.z)) { bool was = cc.enabled; cc.enabled = false; transform.position = new Vector3(x, p.y, z); cc.enabled = was; }
        }

        public void Attack()
        {
            if (Health.Dead || Health.Stunned || Time.time < nextAttack || Busy) return;
            // cible hors de portée : le héros va la chercher automatiquement (1,5 s au plus)
            if (Target && !Target.Dead && Vector3.Distance(Target.transform.position, transform.position) > attackRange + 0.4f)
            { approach = true; approachUntil = Time.time + 1.5f; return; }
            nextAttack = Time.time + attackCooldown;
            FaceNearest(attackRange + 2.5f);
            Anim?.SetTrigger(CharacterRig.AttackId);
            Sound.Play(Sound.WeaponOf(Loot.PlayerClass).swing, transform.position, 0.75f);
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
                if (!any) Sound.Play(Sound.WeaponOf(Loot.PlayerClass).hit, h.transform.position, 0.85f);
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

        // ------------------------------------------------------------------ filet de sécurité contre les chutes
        Vector3 lastSafe; bool hasSafe; float fallingSince = -1;
        /// <summary>Si le héros passe sous le relief (ou tombe longtemps), il est replacé au sol à son dernier appui sûr.
        /// Protège contre toute faille de collision (terrain, bord de carte, décor mal fermé).</summary>
        float nextStrip;
        void FallGuard()
        {
            // une pièce rapportée plus tard (tenue, arme) ne doit jamais apporter sa propre collision
            if (Time.time > nextStrip) { nextStrip = Time.time + 1f; foreach (var c in GetComponentsInChildren<Collider>()) if (c != cc && c.enabled) { c.enabled = false; Destroy(c); } }
            var pos = transform.position;
            if (Swimming) { fallingSince = -1; return; }
            if (cc.isGrounded) { lastSafe = pos; hasSafe = true; fallingSince = -1; return; }
            if (fallingSince < 0) fallingSince = Time.time;
            var t = Terrain.activeTerrain;
            bool underTerrain = t && t.terrainData && pos.y < t.SampleHeight(pos) + t.transform.position.y - 1.5f && InsideTerrain(t, pos);
            bool tooLong = Time.time - fallingSince > 4f || (hasSafe && pos.y < lastSafe.y - 40f) || pos.y < -500f;
            if (!underTerrain && !tooLong) return;
            var back = underTerrain ? GameBootstrap.Ground(pos) : hasSafe ? lastSafe : GameBootstrap.Ground(pos);
            Debug.LogWarning($"[Korvalune] chute rattrapée à {pos} → {back} (sous le relief : {underTerrain})");
            Teleport(back + Vector3.up * 0.2f);
        }

        static bool InsideTerrain(Terrain t, Vector3 p)
        {
            var o = t.transform.position; var s = t.terrainData.size;
            return p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z;
        }

        /// <summary>Déplace le héros instantanément (sans balayage de collision).</summary>
        public void Teleport(Vector3 to)
        {
            bool was = cc.enabled; cc.enabled = false; transform.position = to; cc.enabled = was;
            vy = -2f; fallingSince = -1;
            FindFirstObjectByType<CameraRig>()?.Snap();
        }

        // ------------------------------------------------------------------ pas
        float stepTimer;
        /// <summary>Bruit de pas au rythme de la course : herbe sur le terrain, pierre sur les pavés, dalles et planchers.</summary>
        void Footsteps(float spd)
        {
            if (spd < 0.4f || !cc.isGrounded) { stepTimer = 0.1f; return; }
            stepTimer -= Time.deltaTime * Mathf.Lerp(1.6f, 3.1f, spd / runSpeed);
            if (stepTimer > 0) return;
            stepTimer = 1f;
            bool grass = false;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                grass = hit.collider is TerrainCollider;
            Sound.Play(grass ? "sfx_pas_herbe" : "sfx_pas_pierre", transform.position, grass ? 0.35f : 0.45f, 0.12f, 0.12f);
        }

        public void Lock(float seconds) { lockUntil = Mathf.Max(lockUntil, Time.time + seconds); }
        public void Move(Vector3 delta) { if (cc.enabled) cc.Move(delta); }

        public void GainXp(float amount)
        {
            xp += amount * ElixirXp;
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
                Sound.UI("sfx_niveau", 1f);
                Notify?.Invoke(level >= MaxLevel ? "Niveau maximum (200) atteint !" : $"Niveau {level} !");
            }
            if (level >= MaxLevel) xp = 0;
        }
    }
}
