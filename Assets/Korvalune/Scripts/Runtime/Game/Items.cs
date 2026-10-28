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
        public int reqLevel;                                 // niveau requis pour le porter
        public float durability, maxDurability;              // usure (0 = indestructible, ex. bijoux et anciennes sauvegardes)
        public string cls = "";                              // classe à laquelle l'objet est réservé (vide = toutes)
        public string legendaryPower = "";                   // pouvoir légendaire (Powers)
        public string set = "";                              // ensemble auquel appartient la pièce
        public bool Broken => maxDurability > 0 && durability <= 0;
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
        /// <summary>Couleur d'affichage : vert pour les pièces d'ensemble (comme dans Diablo 3), sinon celle de la rareté.</summary>
        public static Color DisplayColor(ItemSave it) => !string.IsNullOrEmpty(it.set) ? Powers.SetColor : ColorOf(it.rarity);

        // ------------------------------------------------------------------ classes
        /// <summary>Classe et niveau du héros en jeu (butin intelligent : 85 % des objets lui sont destinés).</summary>
        public static string PlayerClass = "";
        public static int PlayerLevel = 1;
        /// <summary>Nombre de pièces portées d'un ensemble (fourni par l'équipement du héros, pour les fiches).</summary>
        public static Func<string, int> WornSetCount;
        public static readonly string[] Classes = { Powers.K, Powers.E, Powers.V, Powers.M, Powers.T };
        public static string ClassLabel(string c) => c switch
        {
            Powers.K => "Chevalier déchu", Powers.E => "Archimage du Néant", Powers.V => "Chasseur des cendres",
            Powers.M => "Nécromancienne", Powers.T => "Gardien solaire", _ => "toutes les classes"
        };
        /// <summary>Caractéristique principale de la classe (comme dans Diablo 3) : elle augmente fortement les dégâts.</summary>
        public static string MainStat(string c) => c switch { Powers.V => "Dextérité", Powers.E => "Intelligence", Powers.M => "Intelligence", _ => "Force" };
        static readonly Dictionary<string, string[]> WeaponBases = new Dictionary<string, string[]>
        {
            { Powers.K, new[] { "Épée longue", "Hache de guerre", "Épée bâtarde" } }, { Powers.T, new[] { "Masse d'armes", "Fléau d'armes" } },
            { Powers.V, new[] { "Arbalète", "Arc long", "Arc de chasse" } }, { Powers.E, new[] { "Bâton", "Sceptre" } }, { Powers.M, new[] { "Faux", "Dague rituelle" } },
        };
        static readonly Dictionary<string, string[]> OffBases = new Dictionary<string, string[]>
        {
            // main gauche propre à chaque classe (comme dans Diablo) : le chasseur porte un carquois, pas un bouclier
            { Powers.K, new[] { "Écu" } }, { Powers.T, new[] { "Pavois" } }, { Powers.V, new[] { "Carquois" } }, { Powers.E, new[] { "Orbe", "Grimoire" } }, { Powers.M, new[] { "Phylactère" } },
        };
        static string PickClass(System.Random r)
        {
            bool known = Array.IndexOf(Classes, PlayerClass) >= 0;
            return known && r.NextDouble() < 0.85 ? PlayerClass : Classes[r.Next(Classes.Length)];
        }
        static bool IsMain(string s) => s == "Force" || s == "Dextérité" || s == "Intelligence";
        public static float MaxDurabilityOf(Slot s) => s == Slot.Amulette || s == Slot.Anneau ? 0 : s == Slot.Arme || s == Slot.MainGauche ? 80 : 60;
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
        static readonly HashSet<string> Fem = new HashSet<string> { "Épée longue", "Hache de guerre", "Masse d'armes", "Arbalète", "Faux", "Épée bâtarde", "Dague rituelle", "Capuche", "Couronne", "Cuirasse", "Brigandine", "Robe", "Amulette", "Chevalière", "Mitaines", "Jambières", "Chausses", "Bottes", "Sandales" };
        static readonly HashSet<string> Plur = new HashSet<string> { "Gantelets", "Gants de cuir", "Mitaines", "Jambières", "Cuissots", "Chausses", "Solerets", "Bottes", "Sandales" };
        /// <summary>Accorde l'adjectif (en minuscules) avec le nom de base : « Brigandine ancienne », « Bottes brisées », « Solerets infernaux ».</summary>
        public static string Accord(string adj, string noun)
        {
            adj = adj.ToLower(); bool f = Fem.Contains(noun), pl = Plur.Contains(noun);
            if (f)
            {
                if (adj.EndsWith("ien")) adj += "ne";
                else if (!adj.EndsWith("e")) adj += "e";
            }
            else if (pl && adj.EndsWith("al")) return adj.Substring(0, adj.Length - 2) + "aux";
            if (pl && !adj.EndsWith("s") && !adj.EndsWith("x")) adj += "s";
            return adj;
        }
        static readonly string[] Suffix = { "de la Comète", "des Pics Sélénites", "du Néant", "de l'Éclipse", "des Cendres", "du Roi sans aube", "de Korvalune", "de l'Astral" };
        static readonly string[] Uniques = { "Larme d'Auralith", "Croc de Ghorvax", "Couronne du Roi sans aube", "Cœur de Maldrakor", "Aile de Seralyth" };
        public static IReadOnlyList<string> StatNames => Stats;
        static readonly string[] Stats = { "Dégâts", "Armure", "Vie", "Force", "Dextérité", "Intelligence", "Chances de coup critique", "Vitesse d'attaque", "Régénération de mana", "Réduction des temps de recharge" };

        /// <summary>Tire la rareté : les élites et boss améliorent les chances ; mythique et ancestral restent très rares.</summary>
        public static Rarity RollRarity(System.Random r, int level, float luck)
        {
            // raretés façon Diablo : un légendaire reste un événement (~1 objet sur 90 au départ, davantage avec la chance)
            double x = r.NextDouble() / (1 + 0.5 * luck);
            if (level >= 30 && x < 0.0004) return Rarity.Ancestral;
            if (level >= 20 && x < 0.0012) return Rarity.Mythique;
            if (x < 0.003) return Rarity.Unique;
            if (x < (level >= 5 ? 0.011 : 0.006)) return Rarity.Legendaire;
            if (x < 0.075) return Rarity.Rare;
            if (x < 0.36) return Rarity.Magique;
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
            string cls = PickClass(r);
            var bases = slot == Slot.Arme ? WeaponBases[cls] : slot == Slot.MainGauche ? OffBases[cls] : Bases[slot];
            var b = bases[r.Next(bases.Length)];
            int naff = rar switch { Rarity.Commun => 0, Rarity.Magique => 1, Rarity.Rare => 3, Rarity.Legendaire => 4, Rarity.Unique => 4, Rarity.Mythique => 5, _ => 5 };
            float mult = Mult(rar);
            var it = new ItemSave { id = Guid.NewGuid().ToString("N").Substring(0, 12), baseName = b, slot = slot, rarity = rar, ilvl = Mathf.Max(1, level + r.Next(-2, 3)) };
            it.power = Mathf.Round((10 + it.ilvl * 4.2f) * mult);
            it.reqLevel = Mathf.Clamp(Mathf.Min(it.ilvl, level), 1, PlayerController.MaxLevel);
            it.maxDurability = it.durability = MaxDurabilityOf(slot);
            if (slot == Slot.Arme || slot == Slot.MainGauche) it.cls = cls;      // armes et mains gauches : propres à une classe
            // caractéristique principale de la classe en premier (plus forte), puis les autres propriétés
            var pool = new List<string>(Stats); pool.RemoveAll(IsMain);
            for (int i = 0; i < naff; i++)
            {
                string s;
                if (i == 0) s = MainStat(cls);
                else { if (pool.Count == 0) break; s = pool[r.Next(pool.Count)]; pool.Remove(s); }
                float v = AffixValue(s, it.ilvl, rar, r); if (i == 0) v = Mathf.Round(v * 1.5f);
                it.affixes.Add(new AffixSave { stat = s, value = v });
            }
            // légendaires et au-delà : un pouvoir légendaire, ou (30 % des légendaires) une pièce d'ensemble
            PowerDef pw = null;
            if (rar >= Rarity.Legendaire)
            {
                if (rar == Rarity.Legendaire && Array.IndexOf(Powers.SetSlots, slot) >= 0 && r.NextDouble() < 0.3)
                {
                    var sets = Powers.SetsOf(cls); var set = sets[r.Next(sets.Count)];
                    it.set = set.id; it.cls = cls;
                }
                else
                {
                    var pool2 = r.NextDouble() < 0.12 ? Powers.Legendary("") : Powers.Legendary(cls);
                    pw = pool2[r.Next(pool2.Count)];
                    it.legendaryPower = pw.id; if (!string.IsNullOrEmpty(pw.cls)) it.cls = pw.cls;
                }
            }
            var setDef = Powers.Set(it.set);
            it.name = setDef != null ? $"{b} {setDef.of}" : rar switch
            {
                Rarity.Commun => b,
                Rarity.Magique => $"{b} {Suffix[r.Next(Suffix.Length)]}",
                Rarity.Rare => $"{b} {Accord(Prefix[r.Next(Prefix.Length)], b)}",
                Rarity.Legendaire => pw != null ? pw.name : $"{b} {Accord(Prefix[r.Next(Prefix.Length)], b)} {Suffix[r.Next(Suffix.Length)]}",
                Rarity.Unique => Uniques[r.Next(Uniques.Length)],
                Rarity.Mythique => Uniques[r.Next(Uniques.Length)] + " (mythique)",
                Rarity.Ancestral => pw != null ? $"{pw.name} ancestral" : $"{b} {Accord("ancestral", b)} {Suffix[r.Next(Suffix.Length)]}",
                _ => $"{b} {Accord(Prefix[r.Next(Prefix.Length)], b)} {Suffix[r.Next(Suffix.Length)]}",
            };
            return it;
        }

        /// <summary>Peut-on porter cet objet ? (classe et niveau requis) ; raison du refus sinon.</summary>
        public static string CannotWear(ItemSave it, string cls, int level)
        {
            if (!string.IsNullOrEmpty(it.cls) && !string.IsNullOrEmpty(cls) && it.cls != cls) return $"Réservé : {ClassLabel(it.cls)}";
            // anciens objets : une arme ou une main gauche étrangère à la classe (ex. un écu pour le chasseur) n'est plus portable
            if (!string.IsNullOrEmpty(cls) && (it.slot == Slot.Arme || it.slot == Slot.MainGauche))
            {
                var allowed = it.slot == Slot.Arme ? WeaponBases : OffBases;
                if (allowed.TryGetValue(cls, out var list) && Array.IndexOf(list, it.baseName) < 0) return $"{ClassLabel(cls)} : ne peut pas porter « {it.baseName} »";
            }
            if (it.reqLevel > level) return $"Niveau {it.reqLevel} requis";
            return null;
        }

        public static string Tooltip(ItemSave it)
        {
            var setDef = Powers.Set(it.set);
            var c = ColorUtility.ToHtmlStringRGB(setDef != null ? Powers.SetColor : ColorOf(it.rarity));
            var s = $"<color=#{c}><b>{it.name}</b></color>\n<color=#{c}>{(setDef != null ? "Objet d'ensemble" : LabelOf(it.rarity))} — {it.baseName}</color>\nPuissance {it.power} · niveau d'objet {it.ilvl}";
            if (!string.IsNullOrEmpty(it.cls)) s += $"\n<color=#{(string.IsNullOrEmpty(PlayerClass) || it.cls == PlayerClass ? "c8b89a" : "ff6060")}>Réservé : {ClassLabel(it.cls)}</color>";
            if (it.reqLevel > 0) s += $"\n<color=#{(it.reqLevel > PlayerLevel ? "ff6060" : "c8b89a")}>Niveau requis : {it.reqLevel}</color>";
            string main = string.IsNullOrEmpty(PlayerClass) ? "" : MainStat(PlayerClass);
            foreach (var a in it.affixes)
            {
                var line = $"+{a.value}{(IsPercent(a.stat) ? " %" : "")} {a.stat}";
                s += IsMain(a.stat) ? (a.stat == main ? $"\n<b>{line}</b>" : $"\n<color=#8a7a66>{line}</color>") : "\n" + line;
            }
            var pw = Powers.Get(it.legendaryPower);
            if (pw != null) s += $"\n<color=#ff9a3c>◆ {pw.name}\n{Powers.Describe(pw, Powers.Strength(it.rarity))}</color>";
            if (setDef != null)
            {
                int n = WornSetCount != null ? WornSetCount(setDef.id) : 0;
                string On(int need) => n >= need ? "3cdc52" : "7a7468";
                s += $"\n<color=#3cdc52><b>{setDef.name}</b> ({Mathf.Min(n, 6)}/6)</color>";
                s += $"\n<color=#{On(2)}>(2) " + string.Join(", ", setDef.bonus2.ConvertAll(a => $"+{a.value}{(IsPercent(a.stat) ? " %" : "")} {a.stat}")) + "</color>";
                s += $"\n<color=#{On(4)}>(4) {Powers.Describe(Powers.Get(setDef.power4))}</color>";
                s += $"\n<color=#{On(6)}>(6) {Powers.Describe(Powers.Get(setDef.power6))}</color>";
            }
            int sockets = Economy.Sockets(it);
            if (it.gems != null) foreach (var g in Economy.GemAffixes(it)) s += $"\n<color=#9fd7ff>• +{g.value}{(IsPercent(g.stat) ? " %" : "")} {g.stat}</color>";
            for (int k = it.gems?.Count ?? 0; k < sockets; k++) s += "\n<color=#8a7a66>○ Châsse vide</color>";
            if (it.maxDurability > 0)
                s += it.Broken ? $"\n<color=#ff4040><b>Brisé</b> — à réparer chez le forgeron</color>"
                    : $"\n<color=#{(it.durability < it.maxDurability * 0.2f ? "ffb040" : "a09070")}>Durabilité : {Mathf.CeilToInt(it.durability)} / {Mathf.RoundToInt(it.maxDurability)}</color>";
            s += $"\n<color=#e8c27a>Valeur : {Economy.SellPrice(it)} or</color>";
            return s;
        }
    }
}
