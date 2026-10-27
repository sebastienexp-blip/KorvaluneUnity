using System;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Raretés (comme dans Diablo 4) : chacune a sa couleur, son aura au sol et son faisceau.</summary>
    public enum Rarity { Commun, Magique, Rare, Legendaire, Unique, Mythique, Ancestral }
    public enum Slot { Arme, MainGauche, Casque, Torse, Gants, Jambes, Bottes, Amulette, Anneau }

    [Serializable] public class AffixSave { public string stat; public float value; }

    [Serializable] public class ItemSave
    {
        public string id, name, baseName; public Slot slot; public Rarity rarity; public int ilvl; public float power;
        public List<AffixSave> affixes = new List<AffixSave>();
        public List<string> gems = new List<string>();      // gemmes serties « Rubis:3 »
        public int rerolls;                                  // réenchantements déjà faits chez la mystique
    }

    public static class Loot
    {
        public static readonly Color[] Colors =
        {
            new Color(0.85f, 0.85f, 0.85f), new Color(0.35f, 0.55f, 1.0f), new Color(1.0f, 0.90f, 0.25f), new Color(1.0f, 0.50f, 0.08f),
            new Color(0.88f, 0.72f, 0.40f), new Color(0.78f, 0.35f, 1.0f), new Color(1.0f, 0.12f, 0.10f)
        };
        public static readonly string[] Labels = { "Commun", "Magique", "Rare", "Légendaire", "Unique", "Mythique", "Ancestral" };
        public static Color ColorOf(Rarity r) => Colors[(int)r];
        public static string LabelOf(Rarity r) => Labels[(int)r];

        static readonly Dictionary<Slot, string[]> Bases = new Dictionary<Slot, string[]>
        {
            { Slot.Arme, new[] { "Épée longue", "Hache de guerre", "Masse d'armes", "Bâton", "Arbalète", "Faux" } },
            { Slot.MainGauche, new[] { "Écu", "Pavois", "Grimoire", "Orbe" } },
            { Slot.Casque, new[] { "Heaume", "Capuche", "Couronne", "Bassinet" } },
            { Slot.Torse, new[] { "Cuirasse", "Brigandine", "Robe", "Haubert" } },
            { Slot.Gants, new[] { "Gantelets", "Gants de cuir", "Mitaines" } },
            { Slot.Jambes, new[] { "Jambières", "Cuissots", "Chausses" } },
            { Slot.Bottes, new[] { "Solerets", "Bottes", "Sandales" } },
            { Slot.Amulette, new[] { "Amulette", "Talisman" } },
            { Slot.Anneau, new[] { "Anneau", "Chevalière" } },
        };
        static readonly string[] Prefix = { "Brisé", "Sinistre", "Ardent", "Glacial", "Funeste", "Céleste", "Sanglant", "Lunaire", "Infernal", "Ancien" };
        static readonly string[] Suffix = { "de la Comète", "des Pics Sélénites", "du Néant", "de l'Éclipse", "des Cendres", "du Roi sans aube", "de Korvalune", "de l'Astral" };
        static readonly string[] Uniques = { "Larme d'Auralith", "Croc de Ghorvax", "Couronne du Roi sans aube", "Cœur de Maldrakor", "Aile de Seralyth" };
        public static IReadOnlyList<string> StatNames => Stats;
        static readonly string[] Stats = { "Dégâts", "Armure", "Vie", "Force", "Dextérité", "Intelligence", "Chances de coup critique", "Vitesse d'attaque", "Régénération de mana", "Réduction des temps de recharge" };

        /// <summary>Tire la rareté : les élites et boss améliorent les chances ; mythique et ancestral restent très rares.</summary>
        public static Rarity RollRarity(System.Random r, int level, float luck)
        {
            double x = r.NextDouble() / (1 + luck);
            if (level >= 30 && x < 0.0015) return Rarity.Ancestral;
            if (level >= 20 && x < 0.004) return Rarity.Mythique;
            if (x < 0.015) return Rarity.Unique;
            if (x < 0.06) return Rarity.Legendaire;
            if (x < 0.22) return Rarity.Rare;
            if (x < 0.55) return Rarity.Magique;
            return Rarity.Commun;
        }

        public static float Mult(Rarity rar) => rar switch { Rarity.Commun => 1f, Rarity.Magique => 1.15f, Rarity.Rare => 1.35f, Rarity.Legendaire => 1.6f, Rarity.Unique => 1.75f, Rarity.Mythique => 2.0f, _ => 2.3f };
        public static bool IsPercent(string s) => s.StartsWith("Chances") || s.StartsWith("Vitesse") || s.StartsWith("Réduction");
        /// <summary>Valeur d'un affixe selon le niveau d'objet et la rareté (pourcentages pour critique, vitesse, recharge).</summary>
        public static float AffixValue(string s, int ilvl, Rarity rar, System.Random r)
        {
            float mult = Mult(rar) * (0.85f + 0.3f * (float)r.NextDouble());
            return IsPercent(s) ? Mathf.Round((2 + ilvl * 0.06f) * mult * 10) / 10 : Mathf.Round((3 + ilvl * 1.4f) * mult);
        }

        public static ItemSave Roll(System.Random r, int level, float luck = 0)
        {
            var rar = RollRarity(r, level, luck);
            var slot = (Slot)r.Next(Enum.GetValues(typeof(Slot)).Length);
            var bases = Bases[slot]; var b = bases[r.Next(bases.Length)];
            int naff = rar switch { Rarity.Commun => 0, Rarity.Magique => 1, Rarity.Rare => 3, Rarity.Legendaire => 4, Rarity.Unique => 4, Rarity.Mythique => 5, _ => 5 };
            float mult = rar switch { Rarity.Commun => 1f, Rarity.Magique => 1.15f, Rarity.Rare => 1.35f, Rarity.Legendaire => 1.6f, Rarity.Unique => 1.75f, Rarity.Mythique => 2.0f, _ => 2.3f };
            var it = new ItemSave { id = Guid.NewGuid().ToString("N").Substring(0, 12), baseName = b, slot = slot, rarity = rar, ilvl = Mathf.Max(1, level + r.Next(-2, 3)) };
            it.power = Mathf.Round((10 + it.ilvl * 4.2f) * mult);
            var pool = new List<string>(Stats);
            for (int i = 0; i < naff && pool.Count > 0; i++)
            {
                var s = pool[r.Next(pool.Count)]; pool.Remove(s);
                it.affixes.Add(new AffixSave { stat = s, value = AffixValue(s, it.ilvl, rar, r) });
            }
            it.name = rar switch
            {
                Rarity.Commun => b,
                Rarity.Magique => $"{b} {Suffix[r.Next(Suffix.Length)]}",
                Rarity.Rare => $"{b} {Prefix[r.Next(Prefix.Length)].ToLower()}",
                Rarity.Unique => Uniques[r.Next(Uniques.Length)],
                Rarity.Mythique => Uniques[r.Next(Uniques.Length)] + " (mythique)",
                Rarity.Ancestral => $"{b} ancestral {Suffix[r.Next(Suffix.Length)]}",
                _ => $"{b} {Prefix[r.Next(Prefix.Length)].ToLower()} {Suffix[r.Next(Suffix.Length)]}",
            };
            return it;
        }

        public static string Tooltip(ItemSave it)
        {
            var c = ColorUtility.ToHtmlStringRGB(ColorOf(it.rarity));
            var s = $"<color=#{c}><b>{it.name}</b></color>\n<color=#{c}>{LabelOf(it.rarity)} — {it.baseName}</color>\nPuissance {it.power} · niveau d'objet {it.ilvl}";
            foreach (var a in it.affixes) s += $"\n+{a.value}{(IsPercent(a.stat) ? " %" : "")} {a.stat}";
            int sockets = Economy.Sockets(it);
            if (it.gems != null) foreach (var g in Economy.GemAffixes(it)) s += $"\n<color=#9fd7ff>• +{g.value}{(IsPercent(g.stat) ? " %" : "")} {g.stat}</color>";
            for (int k = it.gems?.Count ?? 0; k < sockets; k++) s += "\n<color=#8a7a66>○ Châsse vide</color>";
            s += $"\n<color=#e8c27a>Valeur : {Economy.SellPrice(it)} or</color>";
            return s;
        }
    }
}
