using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Compétences du héros : rangs (1 à 5, un point par niveau), barre de 5 compétences équipées, recharges,
    /// exécution générique par type avec effets spéciaux de l'élément (Vfx).</summary>
    [RequireComponent(typeof(PlayerController))]
    public class SkillRunner : MonoBehaviour
    {
        public string classId = "KaelDraven";
        public List<SkillDef> book;
        public int[] ranks;            // rang de chaque compétence du livre (0 = non apprise)
        public int[] loadout = { 0, 1, 2, 3, 4 };
        float[] readyAt;
        PlayerController p;
        public float shieldUntil, shieldPower, curseUntil;
        public GameConfig config;

        public void Init(string cls, GameConfig cfg)
        {
            classId = cls; config = cfg; p = GetComponent<PlayerController>();
            book = SkillBook.For(cls);
            if (ranks == null || ranks.Length != book.Count) { ranks = new int[book.Count]; ranks[0] = 1; }
            readyAt = new float[book.Count];
        }

        public bool Unlocked(int i) => p && p.level >= book[i].unlockLevel;
        public float Remaining(int i) => readyAt == null ? 0 : Mathf.Max(0, readyAt[i] - Time.time);

        /// <summary>Compétence parente dans l'arbre (-1 = racine).</summary>
        public int ParentOf(int i) => SkillBook.TreeOf(classId).parent[i];
        /// <summary>La branche est ouverte : la compétence parente a au moins un rang.</summary>
        public bool BranchOpen(int i) { int pa = ParentOf(i); return pa < 0 || ranks[pa] > 0 || ranks[i] > 0; }

        public bool Learn(int i)
        {
            if (p.skillPoints <= 0 || !Unlocked(i) || !BranchOpen(i) || ranks[i] >= book[i].MaxRank) return false;
            ranks[i]++; p.skillPoints--; return true;
        }

        public void Equip(int slot, int skill) { if (slot >= 0 && slot < loadout.Length && skill >= 0 && skill < book.Count) loadout[slot] = skill; }

        public bool Use(int slot)
        {
            if (slot < 0 || slot >= loadout.Length) return false;
            int i = loadout[slot]; var eqp = GetComponent<Equipment>();
            float echo = 0; var d = eqp ? eqp.Modify(book[i], out echo) : book[i];   // pouvoirs légendaires et ensembles
            if (p.Health.Dead || p.Health.Stunned) return false;
            if (ranks[i] <= 0) { p.Notify?.Invoke($"« {d.name} » n'est pas encore apprise"); return false; }
            if (Time.time < readyAt[i]) return false;
            if (!p.Health.Spend(d.mana)) { p.Notify?.Invoke("Pas assez de mana"); return false; }
            readyAt[i] = Time.time + d.cooldown * (1 - 0.04f * (ranks[i] - 1)) * (1 - (eqp ? eqp.cooldownReduction : 0));
            if (d.empowered) Vfx.Burst(Pos + Vector3.up * 1.2f, d.element, 14, 3f);         // éclat de l'élément : la compétence est transformée
            Sound.Play(Sound.CastOf(d), Pos, 0.9f);
            StartCoroutine(Run(d, ranks[i]));
            if (echo > 0 && Echoable(d.kind)) StartCoroutine(Echo(d, ranks[i], echo));
            return true;
        }

        /// <summary>Types de compétences qu'un écho peut relancer (pas les déplacements ni les effets sur soi).</summary>
        static bool Echoable(SkillKind k) => !(k == SkillKind.Leap || k == SkillKind.Dash || k == SkillKind.Teleport || k == SkillKind.Evade
            || k == SkillKind.Buff || k == SkillKind.Shield || k == SkillKind.Heal || k == SkillKind.Whirl || k == SkillKind.Beam || k == SkillKind.Summon);

        /// <summary>Écho légendaire : une copie spectrale de la compétence se déclenche juste après, à puissance réduite.</summary>
        IEnumerator Echo(SkillDef d, int rank, float power)
        {
            yield return new WaitForSeconds(0.55f);
            if (p.Health.Dead) yield break;
            var e = d.Clone(); e.mult *= power; e.anim = ""; e.ghost = true;    // pas de seconde animation du héros : l'écho est spectral
            var c = Vfx.Of(e.element);
            Vfx.Shock(Pos, c.outer, 2.2f); Vfx.Burst(Pos + Vector3.up * 1.4f, e.element, 24, 4f);
            Sound.Play("sfx_echo", Pos, 0.8f); Sound.Play(Sound.CastOf(e), Pos, 0.6f, 0.12f, 0f);
            yield return Run(e, rank);
        }

        float Dmg(SkillDef d, int rank) => p.attackDamage * p.damageMult * d.mult * (1 + 0.15f * (rank - 1));
        Vector3 Fwd => p.transform.forward;
        Vector3 Pos => p.transform.position;

        Vector3 TargetPoint(float range)
        {
            // ciblage automatique : la cible du héros si elle est à portée, sinon l'ennemi le plus proche
            Health best = p.Target && !p.Target.Dead && Vector3.Distance(p.Target.transform.position, Pos) <= range ? p.Target : null; float bd = range;
            if (!best) foreach (var h in Health.Around(Pos, range, Faction.Player)) { float dd = Vector3.Distance(h.transform.position, Pos); if (dd < bd) { bd = dd; best = h; } }
            if (best) { var dir = best.transform.position - Pos; dir.y = 0; if (dir.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.LookRotation(dir); return best.transform.position; }
            return GameBootstrap.Ground(Pos + Fwd * Mathf.Min(range, 6f));
        }

        void Hit(Health h, float dmg, SkillDef d)
        {
            if (!h || h.Dead) return;
            bool crit = Random.value < p.critChance;
            h.TakeDamage(dmg * (crit ? 2f : 1f) * (Time.time < curseUntil ? 1.25f : 1f), p.Health, crit);
            if (d.stun && d.duration > 0) h.Stun(d.duration);
            Vfx.Burst(h.transform.position + Vector3.up * 1.1f, d.element, 10, 4f);
        }

        void HitArea(Vector3 c, float r, float dmg, SkillDef d, Vector3? fwd = null, float cone = 360)
        { foreach (var h in Health.Around(c, r, Faction.Player, fwd, cone)) Hit(h, dmg, d); }

        void Shake(float a) { var c = FindFirstObjectByType<CameraRig>(); if (c) c.Shake(a); }

        bool HasState(string s) => p.Anim && !string.IsNullOrEmpty(s) && p.Anim.HasState(0, Animator.StringToHash(s));

        /// <summary>Joue l'animation propre au héros pour cette compétence (sinon l'attaque de base) ; renvoie l'instant d'impact.</summary>
        float Play(SkillDef d, float fallback)
        {
            if (d.ghost) return fallback;
            if (HasState(d.anim))
            {
                p.Anim.CrossFadeInFixedTime(d.anim, 0.08f, 0, 0f);
                return SkillAnims.Impact.TryGetValue(d.anim, out var t) ? t : fallback;
            }
            p.Anim?.SetTrigger(CharacterRig.AttackId);
            return fallback;
        }

        float[] HitTimes(SkillDef d, int n, float step)
        {
            if (HasState(d.anim) && SkillAnims.Hits.TryGetValue(d.anim, out var h)) return h;
            var a = new float[n]; for (int i = 0; i < n; i++) a[i] = 0.25f + i * step; return a;
        }

        IEnumerator Run(SkillDef d, int rank)
        {
            float dmg = Dmg(d, rank);
            var anim = p.Anim;
            switch (d.kind)
            {
                case SkillKind.Strike:
                case SkillKind.Cleave:
                {
                    p.FaceNearest(d.radius + 2); float w = Play(d, 0.3f); p.Lock(w + 0.25f);
                    yield return new WaitForSeconds(w);
                    Vfx.Slash(p.transform, d.element, d.radius * 1.3f, d.kind == SkillKind.Cleave ? 0 : Random.Range(-30f, 30f));
                    if (d.kind == SkillKind.Cleave) { Vfx.Slash(p.transform, d.element, d.radius * 1.3f, 180); Vfx.Shock(Pos, Vfx.Of(d.element).outer, d.radius); }
                    HitArea(Pos, d.radius, dmg, d, Fwd, d.kind == SkillKind.Cleave ? 360 : 150);
                    Shake(0.08f);
                    break;
                }
                case SkillKind.Combo:
                {
                    p.FaceNearest(d.radius + 3); float w = Play(d, 0.25f);
                    var times = HitTimes(d, Mathf.Max(2, d.count), 0.16f); p.Lock(times[times.Length - 1] + 0.3f);
                    float prev = 0;
                    for (int k = 0; k < times.Length; k++)
                    {
                        yield return new WaitForSeconds(times[k] - prev); prev = times[k];
                        bool last = k == times.Length - 1;
                        Vfx.Slash(p.transform, d.element, d.radius * 1.3f, last ? 0 : (k % 2 == 0 ? -35f : 35f));
                        if (last) Vfx.Shock(Pos + Fwd, Vfx.Of(d.element).outer, d.radius);
                        HitArea(Pos, d.radius, dmg * (last ? 1.6f : 1f), d, Fwd, last ? 200 : 140);
                        Shake(last ? 0.18f : 0.06f);
                    }
                    break;
                }
                case SkillKind.Evade:
                {
                    var start = Pos; Play(d, 0f); p.Lock(0.6f);
                    Vfx.Explosion(start + Vector3.up * 0.3f, d.element, d.radius); HitArea(start, d.radius, dmg, d); Shake(0.12f);
                    float t = 0, dur = 0.45f; var back = -Fwd;
                    while (t < dur) { float dt = Time.deltaTime; t += dt; p.Move(back * d.range / dur * dt); yield return null; }
                    break;
                }
                case SkillKind.Whirl:
                {
                    p.Lock(d.duration); float t = 0, next = 0, replay = 0, yaw = p.transform.eulerAngles.y;
                    bool own = HasState(d.anim);
                    Vfx.Aura(p.transform, d.element, d.radius * 0.8f, d.duration);
                    while (t < d.duration)
                    {
                        if (own && t >= replay) { replay += 0.7f; p.Anim.CrossFadeInFixedTime(d.anim, 0.05f, 0, 0.1f); }
                        t += Time.deltaTime; if (!own) p.transform.rotation = Quaternion.Euler(0, yaw + t * 900, 0);
                        if (t >= next) { next += 0.25f; Vfx.Slash(p.transform, d.element, d.radius * 1.2f, Random.Range(0, 360)); HitArea(Pos, d.radius, dmg * 0.35f, d); }
                        yield return null;
                    }
                    break;
                }
                case SkillKind.Line:
                {
                    p.FaceNearest(d.range); float w = Play(d, 0.25f); p.Lock(w + 0.2f);
                    yield return new WaitForSeconds(w);
                    var a = Pos + Vector3.up; var b = a + Fwd * d.range;
                    if (d.element == Element.Foudre) Vfx.Bolt(a, b, d.element, 1.0f, 0.35f); else Vfx.Beam(a, b, d.element, d.radius, 0.35f);
                    for (float s = 1; s < d.range; s += 1.5f) Vfx.Burst(GameBootstrap.Ground(Pos + Fwd * s) + Vector3.up * 0.2f, d.element, 6, 3f);
                    foreach (var h in Health.Around(Pos + Fwd * d.range * 0.5f, d.range * 0.5f + 0.5f, Faction.Player))
                    {
                        var rel = h.transform.position - Pos; float along = Vector3.Dot(rel, Fwd), side = Vector3.Cross(Fwd, rel).magnitude;
                        if (along > 0 && along < d.range + 0.5f && side < d.radius + 0.6f) Hit(h, dmg, d);
                    }
                    Shake(0.1f);
                    break;
                }
                case SkillKind.Projectile:
                case SkillKind.Barrage:
                {
                    var tp = TargetPoint(d.range); float w = Play(d, 0.2f); p.Lock(w + 0.15f);
                    yield return new WaitForSeconds(w);
                    int n = Mathf.Max(1, d.count + (d.kind == SkillKind.Barrage ? rank - 1 : 0)); float spread = d.kind == SkillKind.Barrage ? 70f : 0f;
                    for (int k = 0; k < n; k++)
                    {
                        float ang = n == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, k / (float)(n - 1));
                        var dir = Quaternion.Euler(0, ang, 0) * Fwd;
                        var start = Pos + Vector3.up * 1.2f + Fwd * 0.6f;
                        bool explode = d.radius > 1.4f && !d.pierce;
                        Vfx.Projectile(start, dir, d.element, 18f, d.range, Mathf.Clamp(d.radius * 0.35f, 0.25f, 0.8f), (pt, h) =>
                        {
                            if (explode) { Vfx.Explosion(pt, d.element, d.radius); HitArea(pt, d.radius, dmg, d); Shake(0.06f); }
                            else if (h) Hit(h, dmg, d);
                        }, Faction.Player, d.pierce);
                        if (d.kind == SkillKind.Barrage && n > 4) yield return new WaitForSeconds(0.04f);
                    }
                    break;
                }
                case SkillKind.Nova:
                {
                    float w = Play(d, 0.25f); p.Lock(w + 0.25f);
                    yield return new WaitForSeconds(w);
                    Vfx.Explosion(Pos + Vector3.up * 0.3f, d.element, d.radius * 0.6f); Vfx.Shock(Pos, Vfx.Of(d.element).outer, d.radius); Vfx.Shock(Pos, Vfx.Of(d.element).core, d.radius * 0.6f, 0.6f);
                    int hits = 0;
                    foreach (var h in Health.Around(Pos, d.radius, Faction.Player)) { Hit(h, dmg, d); hits++; }
                    if (d.heal) p.Health.Heal(p.Health.maxHp * 0.03f * hits + p.Health.maxHp * 0.05f);
                    Shake(0.2f + d.radius * 0.02f);
                    break;
                }
                case SkillKind.GroundAoE:
                {
                    var tp = TargetPoint(d.range); float w = Mathf.Max(0.45f, Play(d, 0.55f)); p.Lock(w + 0.2f);
                    Vfx.Runes(tp, d.element, d.radius, w + 0.15f);
                    yield return new WaitForSeconds(w);
                    Vfx.Beam(tp + Vector3.up * 30, tp, d.element, d.radius * 0.7f, 0.25f);
                    Vfx.Explosion(tp, d.element, d.radius);
                    HitArea(tp, d.radius, dmg, d); Shake(0.25f);
                    break;
                }
                case SkillKind.Rain:
                {
                    var tp = TargetPoint(d.range); float w = Play(d, 0.2f); p.Lock(w + 0.2f);
                    Vfx.Runes(tp, d.element, d.radius, d.duration + w + 0.3f);
                    yield return new WaitForSeconds(w);
                    int n = d.count + 2 * (rank - 1); float dt = d.duration / Mathf.Max(1, n);
                    for (int k = 0; k < n; k++)
                    {
                        var o = Random.insideUnitCircle * d.radius; var pt = GameBootstrap.Ground(tp + new Vector3(o.x, 0, o.y));
                        Vfx.Projectile(pt + new Vector3(-2, 14, -2), (pt - (pt + new Vector3(-2, 14, -2))).normalized, d.element, 40f, 14.2f, 0.35f, (x, h) => { Vfx.Explosion(pt, d.element, 1.6f); HitArea(pt, 1.8f, dmg, d); }, Faction.Player, true);
                        yield return new WaitForSeconds(dt);
                    }
                    break;
                }
                case SkillKind.Zone:
                case SkillKind.Totem:
                {
                    var tp = d.range > 0 ? TargetPoint(d.range) : Pos; float w = Play(d, 0.2f); p.Lock(w + 0.2f);
                    yield return new WaitForSeconds(w);
                    Vfx.Zone(tp, d.element, d.radius, d.duration);
                    if (d.kind == SkillKind.Totem) Vfx.Beam(tp, tp + Vector3.up * 4, d.element, 0.6f, d.duration);
                    float t = 0;
                    while (t < d.duration)
                    {
                        foreach (var h in Health.Around(tp, d.radius, Faction.Player)) { Hit(h, dmg * 0.5f, d); }
                        if (d.heal && (p.transform.position - tp).sqrMagnitude < d.radius * d.radius) p.Health.Heal(p.Health.maxHp * 0.04f);
                        if (d.kind == SkillKind.Totem) { var tgt = Health.Around(tp, d.radius * 2, Faction.Player); if (tgt.Count > 0) Vfx.Bolt(tp + Vector3.up * 3, tgt[0].transform.position + Vector3.up, d.element, 0.4f, 0.2f); }
                        t += 0.5f; yield return new WaitForSeconds(0.5f);
                    }
                    break;
                }
                case SkillKind.Chain:
                {
                    p.FaceNearest(d.range); float w = Play(d, 0.2f); p.Lock(w + 0.15f);
                    yield return new WaitForSeconds(w);
                    var from = Pos + Vector3.up * 1.4f; var done = new HashSet<Health>();
                    int n = d.count + (rank - 1);
                    for (int k = 0; k < n; k++)
                    {
                        Health best = null; float bd = k == 0 ? d.range : d.radius;
                        foreach (var h in Health.Around(from, bd, Faction.Player)) { if (done.Contains(h)) continue; float dd = Vector3.Distance(h.transform.position, from); if (dd < bd) { bd = dd; best = h; } }
                        if (!best) break;
                        var to = best.transform.position + Vector3.up * 1.1f;
                        Vfx.Bolt(from, to, d.element, 0.7f, 0.3f); Hit(best, dmg * Mathf.Pow(0.9f, k), d); done.Add(best); from = to;
                        yield return new WaitForSeconds(0.06f);
                    }
                    break;
                }
                case SkillKind.Beam:
                {
                    p.FaceNearest(d.range); float w = Play(d, 0.2f); p.Lock(d.duration + w);
                    yield return new WaitForSeconds(w);
                    float t = 0, tick = 0;
                    while (t < d.duration)
                    {
                        var a = Pos + Vector3.up * 1.3f + Fwd * 0.6f; var b = a + Fwd * d.range;
                        if (tick <= 0)
                        {
                            tick = 0.25f;
                            Vfx.Beam(a, b, d.element, d.radius, 0.28f);
                            foreach (var h in Health.Around(Pos + Fwd * d.range * 0.5f, d.range * 0.5f + 0.5f, Faction.Player))
                            {
                                var rel = h.transform.position - Pos; float along = Vector3.Dot(rel, Fwd), side = Vector3.Cross(Fwd, rel).magnitude;
                                if (along > 0 && side < d.radius + 0.7f) { Hit(h, dmg * 0.3f, d); if (d.heal) p.Health.Heal(dmg * 0.08f); }
                            }
                        }
                        tick -= Time.deltaTime; t += Time.deltaTime; yield return null;
                    }
                    if (HasState(d.anim)) p.Anim.CrossFadeInFixedTime("Locomotion", 0.2f, 0);
                    break;
                }
                case SkillKind.Leap:
                case SkillKind.Dash:
                case SkillKind.Teleport:
                {
                    var tp = TargetPoint(d.range); var start = Pos; var dir = tp - start; dir.y = 0; float dist = Mathf.Min(dir.magnitude, d.range); dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Fwd;
                    if (d.kind == SkillKind.Teleport)
                    {
                        float w0 = Play(d, 0f); p.Lock(w0 + 0.3f); yield return new WaitForSeconds(w0);
                        Vfx.Explosion(start + Vector3.up, d.element, 1.2f);
                        var cc = GetComponent<CharacterController>(); cc.enabled = false; p.transform.position = GameBootstrap.Ground(start + dir * dist); cc.enabled = true;
                        Vfx.Explosion(Pos + Vector3.up * 0.5f, d.element, d.radius); HitArea(Pos, d.radius, dmg, d);
                        break;
                    }
                    bool ownLeap = HasState(d.anim);
                    if (ownLeap) { float w0 = Play(d, 0f); if (d.kind == SkillKind.Leap) yield return new WaitForSeconds(Mathf.Min(0.15f, w0)); }
                    else anim?.SetTrigger(CharacterRig.DodgeId);
                    p.Lock(0.6f);
                    float t = 0, dur = d.kind == SkillKind.Leap ? (ownLeap ? 0.42f : 0.5f) : 0.25f; var hitOnce = new HashSet<Health>();
                    while (t < dur)
                    {
                        float dt = Time.deltaTime; t += dt;
                        p.Move(dir * dist / dur * dt + (d.kind == SkillKind.Leap && !ownLeap ? Vector3.up * Mathf.Cos(Mathf.PI * t / dur) * 6f * dt : Vector3.zero));
                        if (d.kind == SkillKind.Dash && d.mult > 0)
                            foreach (var h in Health.Around(Pos, d.radius, Faction.Player)) if (hitOnce.Add(h)) Hit(h, dmg, d);
                        Vfx.Burst(Pos + Vector3.up, d.element, 3, 1.5f);
                        yield return null;
                    }
                    if (d.kind == SkillKind.Leap) { Vfx.Explosion(Pos, d.element, d.radius); HitArea(Pos, d.radius, dmg, d); Shake(0.3f); }
                    break;
                }
                case SkillKind.Buff:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    p.damageMult = 1f + d.mult * (1 + 0.1f * (rank - 1)); p.buffUntil = Time.time + d.duration;
                    if (d.mana <= 0) p.Health.TakeDamage(p.Health.maxHp * 0.1f, p.Health);
                    Vfx.Aura(p.transform, d.element, 1.2f, d.duration); Vfx.Explosion(Pos + Vector3.up, d.element, 1.5f);
                    p.Notify?.Invoke($"{d.name} : dégâts ×{p.damageMult:0.0}");
                    break;
                }
                case SkillKind.Shield:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    shieldUntil = Time.time + d.duration; shieldPower = Mathf.Clamp01(d.mult + 0.05f * (rank - 1));
                    Vfx.Aura(p.transform, d.element, 1.0f, d.duration); Vfx.Shock(Pos, Vfx.Of(d.element).outer, 2.5f);
                    p.Notify?.Invoke($"{d.name} : dégâts subis −{Mathf.RoundToInt(shieldPower * 100)} %");
                    break;
                }
                case SkillKind.Heal:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    p.Health.Heal(p.Health.maxHp * (d.mult + 0.05f * (rank - 1)));
                    Vfx.Aura(p.transform, d.element, 1.0f, 1.5f); Vfx.Explosion(Pos + Vector3.up, d.element, 1.2f);
                    break;
                }
                case SkillKind.Curse:
                {
                    var tp = d.range > 0 ? TargetPoint(d.range) : Pos; float w = Play(d, 0.2f); p.Lock(w + 0.2f);
                    Vfx.Runes(tp, d.element, d.radius, w + 1.2f);
                    yield return new WaitForSeconds(w); Vfx.Zone(tp, d.element, d.radius, 1.0f);
                    curseUntil = Time.time + d.duration;
                    foreach (var h in Health.Around(tp, d.radius, Faction.Player)) { Hit(h, dmg, d); if (d.stun) h.Stun(d.duration); }
                    break;
                }
                case SkillKind.Pull:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    var tp = d.range > 0 ? TargetPoint(d.range) : Pos; Vfx.Zone(tp, d.element, d.radius * 0.5f, 1.2f);
                    float t = 0;
                    var targets = Health.Around(tp, d.radius, Faction.Player);
                    while (t < 0.6f)
                    {
                        foreach (var h in targets) if (h)
                        {
                            var dd = tp - h.transform.position; dd.y = 0;
                            var cc = h.GetComponent<CharacterController>(); var mv = dd * Mathf.Min(1, Time.deltaTime * 5);
                            if (cc && cc.enabled) cc.Move(mv); else h.transform.position += mv;
                        }
                        t += Time.deltaTime; yield return null;
                    }
                    Vfx.Explosion(tp, d.element, 2f); HitArea(tp, 2.5f, dmg, d);
                    break;
                }
                case SkillKind.Trap:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    int n = Mathf.Max(1, d.count);
                    for (int k = 0; k < n; k++)
                    {
                        var o = n == 1 ? (Vector2)Vector2.zero : new Vector2(Mathf.Cos(k * 6.283f / n), Mathf.Sin(k * 6.283f / n)) * 3f;
                        var pt = GameBootstrap.Ground(Pos + Fwd * (n == 1 ? 2.5f : 0) + new Vector3(o.x, 0, o.y));
                        StartCoroutine(TrapRoutine(d, pt, dmg));
                    }
                    break;
                }
                case SkillKind.Summon:
                {
                    { float w = Play(d, 0.1f); p.Lock(w + 0.2f); yield return new WaitForSeconds(w); }
                    var t = config ? config.enemies.Find(x => x.id == SummonType(d)) : null;
                    if (t == null || !t.prefab) { p.Notify?.Invoke("Invocation indisponible"); break; }
                    var boot = FindFirstObjectByType<GameBootstrap>();
                    for (int k = 0; k < d.count; k++)
                    {
                        var o = Random.insideUnitCircle * 2.5f; var pt = GameBootstrap.Ground(Pos + new Vector3(o.x, 0, o.y));
                        Vfx.Runes(pt, d.element, 1.5f, 1.2f); Vfx.Explosion(pt + Vector3.up * 0.5f, d.element, 1.2f);
                        var m = boot ? boot.Spawn(t, pt) : null; if (!m) continue;
                        var ai = m.GetComponent<EnemyAI>(); ai.side = Faction.Player; ai.owner = p.transform; ai.lifetime = d.duration;
                        var h = m.GetComponent<Health>(); h.faction = Faction.Player; h.maxHp = h.hp = p.Health.maxHp * 0.5f * d.mult * (1 + 0.2f * (rank - 1));
                        ai.damageOverride = p.attackDamage * d.mult;
                        m.name = d.name;
                    }
                    break;
                }
            }
        }

        static string SummonType(SkillDef d) => d.classId switch
        {
            "Morvanna" => "squelette", "KaelDraven" => "chevalier", "Varek" => "loup", "ElyraNoctis" => "sorcier", _ => "chevalier"
        };

        IEnumerator TrapRoutine(SkillDef d, Vector3 pt, float dmg)
        {
            Vfx.Runes(pt, d.element, 1.2f, d.duration);
            float t = 0;
            while (t < d.duration)
            {
                var hs = Health.Around(pt, 1.4f, Faction.Player);
                if (hs.Count > 0) { Vfx.Explosion(pt, d.element, d.radius); HitArea(pt, d.radius, dmg, d); Shake(0.15f); yield break; }
                t += 0.15f; yield return new WaitForSeconds(0.15f);
            }
        }
    }
}
