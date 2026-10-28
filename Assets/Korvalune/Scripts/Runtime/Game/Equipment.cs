using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Équipement du héros (9 emplacements) et calcul des caractéristiques : caractéristiques de base (niveau) +
    /// puissance des objets + affixes. Le sac contient 40 objets au plus.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class Equipment : MonoBehaviour
    {
        public const int BagSize = 40;
        public readonly Dictionary<Slot, ItemSave> worn = new Dictionary<Slot, ItemSave>();
        PlayerController p; Health h; SkillRunner sr;
        float baseDamage, baseHp, baseArmor, baseCrit, baseCooldown, baseRegen;
        public float cooldownReduction;                 // 0..0.5, appliquée aux recharges des compétences
        public System.Action Changed;

        /// <summary>Mémorise les caractéristiques de base (après le niveau) ; à rappeler si le niveau change.</summary>
        public void Init()
        {
            p = GetComponent<PlayerController>(); h = GetComponent<Health>(); sr = GetComponent<SkillRunner>();
            if (sr) Loot.PlayerClass = sr.classId;
            Loot.PlayerLevel = p.level;
            Loot.WornSetCount = SetCount;
            if (!hooked)
            {
                hooked = true;
                h.Died += _ => WearOnDeath();
                h.Damaged += (amount, src) => { if (++hitsTaken % 8 == 0) WearRandomArmor(); };
                Health.AnyDied += OnAnyDied;
            }
            baseDamage = p.attackDamage; baseHp = h.maxHp; baseArmor = h.armor; baseCrit = p.critChance; baseCooldown = p.attackCooldown; baseRegen = h.manaRegen;
            Apply(true);
        }

        /// <summary>Montée de niveau : la base augmente, l'équipement s'y ajoute.</summary>
        public void AddBase(float damage, float hp) { baseDamage += damage; baseHp += hp; Apply(true); h.hp = h.maxHp; }

        public string ClassId => sr ? sr.classId : Loot.PlayerClass;

        public bool Equip(ItemSave it)
        {
            if (it == null) return false;
            var why = Loot.CannotWear(it, ClassId, p.level);
            if (why != null) { p.Notify?.Invoke(why); return false; }
            p.inventory.Remove(it);
            if (worn.TryGetValue(it.slot, out var old) && old != null) p.inventory.Add(old);
            worn[it.slot] = it;
            Apply(true); Changed?.Invoke(); Sound.UI("sfx_equiper");
            return true;
        }

        public bool Unequip(Slot s)
        {
            if (!worn.TryGetValue(s, out var it) || it == null) return false;
            if (p.inventory.Count >= BagSize) { p.Notify?.Invoke("Sac plein"); return false; }
            worn.Remove(s); p.inventory.Add(it);
            Apply(true); Changed?.Invoke();
            return true;
        }

        public void Discard(ItemSave it) { p.inventory.Remove(it); Changed?.Invoke(); }

        public ItemSave In(Slot s) => worn.TryGetValue(s, out var it) ? it : null;

        /// <summary>Somme d'un affixe sur tout l'équipement porté.</summary>
        public float Sum(string stat)
        {
            float v = 0;
            foreach (var it in Active())
            {
                foreach (var a in it.affixes) if (a.stat == stat) v += a.value;
                foreach (var g in Economy.GemAffixes(it)) if (g.stat == stat) v += g.value;          // gemmes serties
            }
            if (p) foreach (var a in Economy.ActiveRelicAffixes(p.wallet)) if (a.stat == stat) v += a.value;   // pouvoirs du Reliquaire
            foreach (var set in ActiveSets()) if (set.count >= 2) foreach (var a in set.def.bonus2) if (a.stat == stat) v += a.value;   // ensembles (2 pièces)
            return v;
        }

        /// <summary>Objets portés et utilisables (les objets brisés ne donnent plus rien).</summary>
        IEnumerable<ItemSave> Active() { foreach (var it in worn.Values) if (it != null && !it.Broken && Loot.CannotWear(it, ClassId, 999) == null) yield return it; }   // un objet d'une autre classe ne donne rien

        // ------------------------------------------------------------------ ensembles et pouvoirs légendaires
        bool HasConvergence() { foreach (var it in Active()) { var pw = Powers.Get(it.legendaryPower); if (pw != null && pw.setRing) return true; } return false; }

        /// <summary>Pièces portées d'un ensemble (+1 avec l'Anneau de la Convergence dès 2 pièces).</summary>
        public int SetCount(string id)
        {
            int n = 0; foreach (var it in Active()) if (it.set == id) n++;
            if (n >= 2 && HasConvergence()) n++;
            return n;
        }

        public List<(SetDef def, int count)> ActiveSets()
        {
            var ids = new HashSet<string>(); var l = new List<(SetDef, int)>();
            foreach (var it in Active()) if (!string.IsNullOrEmpty(it.set) && ids.Add(it.set)) { var d = Powers.Set(it.set); if (d != null) l.Add((d, SetCount(it.set))); }
            return l;
        }

        /// <summary>Tous les pouvoirs actifs : objets légendaires portés + bonus d'ensembles (4 et 6 pièces), avec leur force.</summary>
        public List<(PowerDef power, float strength)> ActivePowers()
        {
            var l = new List<(PowerDef, float)>();
            foreach (var it in Active())
            {
                var pw = Powers.Get(it.legendaryPower);
                if (pw != null && !pw.setRing) l.Add((pw, Powers.Strength(it.rarity)));
            }
            foreach (var (def, n) in ActiveSets())
            {
                if (n >= 4) { var a = Powers.Get(def.power4); if (a != null) l.Add((a, 1f)); }
                if (n >= 6) { var b = Powers.Get(def.power6); if (b != null) l.Add((b, 1f)); }
            }
            return l;
        }

        /// <summary>Compétence transformée par les pouvoirs portés (copie modifiée) ; echo = force de la relance (0 = aucune).
        /// Les pouvoirs se cumulent : dégâts et rayons se multiplient, projectiles s'additionnent.</summary>
        public SkillDef Modify(SkillDef d, out float echo)
        {
            echo = 0;
            var powers = ActivePowers(); SkillDef m = null;
            float cd = 1f;
            foreach (var (pw, s) in powers)
            {
                if (!pw.Matches(d)) continue;
                if (m == null) m = d.Clone();
                bool protective = d.kind == SkillKind.Shield || d.kind == SkillKind.Heal;
                if (pw.dmg > 0 && !protective) m.mult *= 1f + pw.dmg * s;
                if (pw.radius > 0) m.radius *= 1f + pw.radius * s;
                if (pw.count > 0) m.count += Powers.Count(pw, s);
                if (pw.duration > 0) m.duration *= 1f + pw.duration * s;
                if (pw.cooldown > 0) cd *= 1f - Mathf.Min(0.6f, pw.cooldown * s);
                if (pw.toElement.HasValue) m.element = pw.toElement.Value;
                if (pw.pierce) m.pierce = true;
                if (pw.stun) { m.stun = true; if (m.duration <= 0) m.duration = 1.2f; }
                if (pw.echo > 0) echo = Mathf.Max(echo, Mathf.Min(1f, pw.echo * s));
                m.empowered = true;
            }
            if (m == null) return d;
            m.cooldown *= Mathf.Max(0.25f, cd);
            if (m.kind == SkillKind.Buff) m.mult = Mathf.Min(m.mult, 3f);
            return m;
        }

        // ------------------------------------------------------------------ usure
        int hitsTaken, kills; bool hooked;
        void OnDestroy() { Health.AnyDied -= OnAnyDied; }
        void OnAnyDied(Health dead)
        {
            if (!dead || dead.faction == Faction.Player || !p || h.Dead) return;
            if (++kills % 6 == 0) { Wear(In(Slot.Arme), 1); Wear(In(Slot.MainGauche), 1); }
        }
        void WearRandomArmor()
        {
            var armor = new List<ItemSave>();
            foreach (var it in worn.Values) if (it != null && it.maxDurability > 0 && it.slot != Slot.Arme && it.slot != Slot.MainGauche) armor.Add(it);
            if (armor.Count > 0) Wear(armor[Random.Range(0, armor.Count)], 1);
        }
        /// <summary>Mort du héros : chaque pièce portée perd 10 % de sa durabilité maximale.</summary>
        void WearOnDeath()
        {
            foreach (var it in new List<ItemSave>(worn.Values)) if (it != null) Wear(it, it.maxDurability * 0.1f);
            p.Notify?.Invoke("Votre équipement s'use (−10 % de durabilité)");
        }
        void Wear(ItemSave it, float amount)
        {
            if (it == null || it.maxDurability <= 0 || it.Broken) return;
            it.durability = Mathf.Max(0, it.durability - amount);
            if (it.Broken) { p.Notify?.Invoke($"« {it.name} » est brisé : faites-le réparer chez le forgeron"); Apply(true); Changed?.Invoke(); }
        }
        /// <summary>Réparation chez le forgeron ou l'armurier.</summary>
        public void Repaired() { Apply(true); Changed?.Invoke(); }
        /// <summary>Recalcule les caractéristiques (après sertissage, réenchantement, réceptacles, élixir).</summary>
        public void Recompute() { Apply(true); Changed?.Invoke(); }

        void Apply(bool withItems)
        {
            if (!p) return;
            float dmg = baseDamage, hp = baseHp, armor = baseArmor, crit = baseCrit, cd = baseCooldown, regen = baseRegen, cdr = 0;
            if (withItems)
            {
                Loot.PlayerLevel = p.level;
                foreach (var it in Active())
                {
                    if (it.slot == Slot.Arme) dmg += it.power * 0.6f;
                    else if (it.slot == Slot.Amulette || it.slot == Slot.Anneau) dmg += it.power * 0.15f;
                    else armor += it.power * 0.12f;
                }
                // caractéristique principale de la classe : +0,8 dégât par point ; les deux autres comptent à peine
                string main = Loot.MainStat(ClassId);
                float mainV = Sum(main), others = Sum("Force") + Sum("Dextérité") + Sum("Intelligence") - mainV;
                dmg += Sum("Dégâts") + 0.8f * mainV + 0.1f * others;
                armor += 0.3f * Sum("Armure");
                hp += 3f * Sum("Vie");
                crit += Sum("Chances de coup critique") / 100f;
                cd *= 1f - Mathf.Clamp(Sum("Vitesse d'attaque") / 100f, 0, 0.5f);
                regen += 0.1f * Sum("Régénération de mana");
                cdr = Mathf.Clamp(Sum("Réduction des temps de recharge") / 100f, 0, 0.5f);
            }
            if (p.wallet != null && Time.time < p.wallet.elixirUntil)
            {
                if (p.wallet.elixir == "force") dmg *= 1.15f;
                if (p.wallet.elixir == "vie") hp *= 1.15f;
            }
            float ratio = h.maxHp > 0 ? h.hp / h.maxHp : 1;
            p.attackDamage = dmg; h.maxHp = hp; h.hp = Mathf.Clamp(hp * ratio, 1, hp); h.armor = armor;
            p.critChance = Mathf.Clamp01(crit); p.attackCooldown = cd; h.manaRegen = regen; cooldownReduction = cdr;
        }

        /// <summary>Résumé des caractéristiques pour le panneau d'inventaire.</summary>
        public string Summary() =>
            $"Dégâts <b>{p.attackDamage:0}</b>   Vie <b>{h.maxHp:0}</b>   Armure <b>{h.armor:0}</b>\n" +
            $"Critique <b>{p.critChance * 100:0.#} %</b>   Attaque <b>{1f / p.attackCooldown:0.00}/s</b>   Recharge <b>−{cooldownReduction * 100:0} %</b>\n" +
            $"{Loot.MainStat(ClassId)} <b>{Sum(Loot.MainStat(ClassId)):0}</b>" + SetsSummary();

        string SetsSummary()
        {
            var s = "";
            foreach (var (def, n) in ActiveSets()) s += $"   <color=#3cdc52>{def.name} {Mathf.Min(n, 6)}/6</color>";
            int legend = 0; foreach (var it in Active()) if (!string.IsNullOrEmpty(it.legendaryPower)) legend++;
            if (legend > 0) s += $"   <color=#ff9a3c>{legend} pouvoir(s) légendaire(s)</color>";
            return s;
        }

        public List<ItemSave> ToSave() => new List<ItemSave>(worn.Values);
        public void FromSave(List<ItemSave> items) { worn.Clear(); if (items != null) foreach (var it in items) worn[it.slot] = it; Apply(true); }
    }
}
