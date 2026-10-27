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
            baseDamage = p.attackDamage; baseHp = h.maxHp; baseArmor = h.armor; baseCrit = p.critChance; baseCooldown = p.attackCooldown; baseRegen = h.manaRegen;
            Apply(true);
        }

        /// <summary>Montée de niveau : la base augmente, l'équipement s'y ajoute.</summary>
        public void AddBase(float damage, float hp) { baseDamage += damage; baseHp += hp; Apply(true); h.hp = h.maxHp; }

        public bool Equip(ItemSave it)
        {
            if (it == null) return false;
            p.inventory.Remove(it);
            if (worn.TryGetValue(it.slot, out var old) && old != null) p.inventory.Add(old);
            worn[it.slot] = it;
            Apply(true); Changed?.Invoke();
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
        public float Sum(string stat) { float v = 0; foreach (var it in worn.Values) foreach (var a in it.affixes) if (a.stat == stat) v += a.value; return v; }

        void Apply(bool withItems)
        {
            if (!p) return;
            float dmg = baseDamage, hp = baseHp, armor = baseArmor, crit = baseCrit, cd = baseCooldown, regen = baseRegen, cdr = 0;
            if (withItems)
            {
                foreach (var it in worn.Values)
                {
                    if (it.slot == Slot.Arme) dmg += it.power * 0.6f;
                    else if (it.slot == Slot.Amulette || it.slot == Slot.Anneau) dmg += it.power * 0.15f;
                    else armor += it.power * 0.12f;
                }
                dmg += Sum("Dégâts") + 0.4f * (Sum("Force") + Sum("Dextérité") + Sum("Intelligence"));
                armor += 0.3f * Sum("Armure");
                hp += 3f * Sum("Vie");
                crit += Sum("Chances de coup critique") / 100f;
                cd *= 1f - Mathf.Clamp(Sum("Vitesse d'attaque") / 100f, 0, 0.5f);
                regen += 0.1f * Sum("Régénération de mana");
                cdr = Mathf.Clamp(Sum("Réduction des temps de recharge") / 100f, 0, 0.5f);
            }
            float ratio = h.maxHp > 0 ? h.hp / h.maxHp : 1;
            p.attackDamage = dmg; h.maxHp = hp; h.hp = Mathf.Clamp(hp * ratio, 1, hp); h.armor = armor;
            p.critChance = Mathf.Clamp01(crit); p.attackCooldown = cd; h.manaRegen = regen; cooldownReduction = cdr;
        }

        /// <summary>Résumé des caractéristiques pour le panneau d'inventaire.</summary>
        public string Summary() =>
            $"Dégâts <b>{p.attackDamage:0}</b>   Vie <b>{h.maxHp:0}</b>   Armure <b>{h.armor:0}</b>\n" +
            $"Critique <b>{p.critChance * 100:0.#} %</b>   Attaque <b>{1f / p.attackCooldown:0.00}/s</b>   Recharge <b>−{cooldownReduction * 100:0} %</b>";

        public List<ItemSave> ToSave() => new List<ItemSave>(worn.Values);
        public void FromSave(List<ItemSave> items) { worn.Clear(); if (items != null) foreach (var it in items) worn[it.slot] = it; Apply(true); }
    }
}
