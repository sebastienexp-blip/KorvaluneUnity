using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Bourse du héros : or, matériaux d'artisanat (ferraille, essence arcanique, éclat d'étoile), éclats d'ombre
    /// (monnaie de la marchande de curiosités), potions de soin, gemmes, pouvoirs du Reliquaire Céleste.</summary>
    [Serializable] public class Wallet
    {
        public long gold;
        public int scrap, essence, starShard, shadow;
        public int potions = 3;
        public List<GemStack> gems = new List<GemStack>();
        public List<RelicPower> relics = new List<RelicPower>();         // pouvoirs extraits (collection)
        public List<string> receptacles = new List<string> { "", "", "" }; // pouvoirs placés : arme, armure, bijou (id)
        public string elixir = ""; public float elixirUntil;              // élixir actif (temps de jeu)
    }

    [Serializable] public class GemStack { public string kind; public int tier; public int count; }
    [Serializable] public class RelicPower { public string id, name, source; public int receptacle; public List<AffixSave> affixes = new List<AffixSave>(); }

    /// <summary>Règles du commerce et de l'artisanat de Korvalune : prix, stocks des marchands, démontage, fabrication,
    /// sertissage, réenchantement, pari de la marchande de curiosités, extraction de pouvoirs et transmutation au Reliquaire.</summary>
    public static class Economy
    {
        // ---------------------------------------------------------------------------------------------- prix
        static readonly float[] RarityValue = { 1f, 2.5f, 6f, 18f, 28f, 60f, 150f };
        public static long Value(ItemSave it) => (long)Mathf.Round((8 + it.ilvl * 3f) * RarityValue[(int)it.rarity] + it.affixes.Count * 4);
        public static long SellPrice(ItemSave it) => Value(it);
        public static long BuyPrice(ItemSave it) => Value(it) * 5;

        // ---------------------------------------------------------------------------------------------- stocks
        /// <summary>Stock d'un marchand, renouvelé à chaque montée de niveau du héros (graine : marchand + niveau).</summary>
        public static List<ItemSave> Stock(MerchantKind k, int level)
        {
            var r = new System.Random(level * 7919 + (int)k * 104729);
            var list = new List<ItemSave>();
            int n = k == MerchantKind.Armurier ? 12 : 10;
            for (int i = 0; i < n; i++)
            {
                ItemSave it = null;
                for (int t = 0; t < 30; t++)
                {
                    it = Loot.Roll(r, level, k == MerchantKind.Armurier ? 0.8f : 0.2f);
                    bool armor = it.slot != Slot.Amulette && it.slot != Slot.Anneau;
                    bool ok = k == MerchantKind.Armurier ? armor : true;
                    if (it.rarity > Rarity.Rare) continue;
                    if (ok) break;
                }
                list.Add(it);
            }
            return list;
        }

        // ---------------------------------------------------------------------------------------------- forgeron
        /// <summary>Démontage : matériaux selon la rareté (les légendaires et au-delà donnent des éclats d'étoile).</summary>
        public static (int scrap, int essence, int shard) SalvageYield(ItemSave it) => it.rarity switch
        {
            Rarity.Commun => (2, 0, 0), Rarity.Magique => (3, 1, 0), Rarity.Rare => (4, 3, 0),
            Rarity.Legendaire => (6, 5, 1), Rarity.Unique => (6, 6, 2), Rarity.Mythique => (8, 10, 4), _ => (10, 15, 8)
        };

        public static void Salvage(Wallet w, ItemSave it) { var y = SalvageYield(it); w.scrap += y.scrap; w.essence += y.essence; w.starShard += y.shard; }

        /// <summary>Coût de fabrication d'un objet rare (ou légendaire avec éclats d'étoile) pour un emplacement donné.</summary>
        public static (long gold, int scrap, int essence, int shard) CraftCost(int level, bool legendary) =>
            legendary ? (2000 + level * 120, 40, 30, 5) : (300 + level * 25, 15, 6, 0);

        public static ItemSave Craft(System.Random r, int level, Slot slot, bool legendary)
        {
            for (int t = 0; t < 200; t++)
            {
                var it = Loot.Roll(r, level, legendary ? 30f : 3f);
                if (it.slot != slot) continue;
                if (!legendary && it.rarity != Rarity.Rare) continue;
                if (legendary && it.rarity != Rarity.Legendaire) continue;
                it.ilvl = level + 1; it.power = Mathf.Round(it.power * 1.05f);
                return it;
            }
            return null;
        }

        // ---------------------------------------------------------------------------------------------- joaillier
        public static readonly string[] GemKinds = { "Rubis", "Saphir", "Émeraude", "Topaze", "Améthyste" };
        public static readonly string[] GemStat = { "Dégâts", "Armure", "Chances de coup critique", "Vie", "Régénération de mana" };
        public static readonly string[] GemTier = { "", "ébréché", "taillé", "parfait", "royal", "céleste" };
        public static readonly Color[] GemColor = { new Color(1f, 0.15f, 0.15f), new Color(0.2f, 0.45f, 1f), new Color(0.2f, 1f, 0.4f), new Color(1f, 0.8f, 0.2f), new Color(0.75f, 0.35f, 1f) };
        public static float GemValue(string kind, int tier)
        {
            int k = Array.IndexOf(GemKinds, kind);
            float b = k == 2 ? 0.8f : k == 4 ? 2f : 6f;
            return Mathf.Round(b * Mathf.Pow(2.1f, tier - 1) * 10) / 10;
        }
        public static string GemName(string kind, int tier) => $"{kind} {GemTier[Mathf.Clamp(tier, 1, 5)]}";

        public static int Sockets(ItemSave it) => it.rarity switch { Rarity.Commun => 0, Rarity.Magique => 1, Rarity.Rare => 1, Rarity.Legendaire => 2, _ => 3 };
        public static void AddGem(Wallet w, string kind, int tier, int n = 1)
        {
            var g = w.gems.FirstOrDefault(x => x.kind == kind && x.tier == tier);
            if (g == null) { g = new GemStack { kind = kind, tier = tier }; w.gems.Add(g); }
            g.count += n; w.gems.RemoveAll(x => x.count <= 0);
        }
        /// <summary>Fusion : 3 gemmes identiques + or → 1 gemme de la taille supérieure.</summary>
        public static long FuseCost(int tier) => 100L * tier * tier;
        public static bool Fuse(Wallet w, string kind, int tier)
        {
            var g = w.gems.FirstOrDefault(x => x.kind == kind && x.tier == tier);
            if (g == null || g.count < 3 || tier >= 5 || w.gold < FuseCost(tier)) return false;
            w.gold -= FuseCost(tier); AddGem(w, kind, tier, -3); AddGem(w, kind, tier + 1); return true;
        }
        public static bool Socket(Wallet w, ItemSave it, string kind, int tier)
        {
            var g = w.gems.FirstOrDefault(x => x.kind == kind && x.tier == tier);
            if (g == null || g.count < 1 || it.gems.Count >= Sockets(it)) return false;
            AddGem(w, kind, tier, -1); it.gems.Add($"{kind}:{tier}"); return true;
        }
        public static long UnsocketCost(ItemSave it) => 50 + it.ilvl * 10;
        public static bool Unsocket(Wallet w, ItemSave it)
        {
            if (it.gems.Count == 0 || w.gold < UnsocketCost(it)) return false;
            w.gold -= UnsocketCost(it);
            foreach (var s in it.gems) { var p = s.Split(':'); AddGem(w, p[0], int.Parse(p[1])); }
            it.gems.Clear(); return true;
        }
        /// <summary>Affixes apportés par les gemmes serties d'un objet.</summary>
        public static IEnumerable<AffixSave> GemAffixes(ItemSave it)
        {
            if (it.gems == null) yield break;
            foreach (var s in it.gems)
            {
                var p = s.Split(':'); int k = Array.IndexOf(GemKinds, p[0]); if (k < 0) continue;
                yield return new AffixSave { stat = GemStat[k], value = GemValue(p[0], int.Parse(p[1])) };
            }
        }

        // ---------------------------------------------------------------------------------------------- mystique
        public static (long gold, int essence) RerollCost(ItemSave it, int times) => ((long)(Value(it) * 0.6f * (1 + times * 0.5f)) + 100, 2 + (int)it.rarity * 2);

        /// <summary>Deux nouvelles propositions pour l'affixe i (statistiques absentes de l'objet, valeur à son niveau).</summary>
        public static List<AffixSave> RerollOptions(System.Random r, ItemSave it, int i)
        {
            var have = new HashSet<string>(it.affixes.Select(a => a.stat));
            var pool = Loot.StatNames.Where(s => !have.Contains(s)).ToList();
            var outl = new List<AffixSave>();
            for (int k = 0; k < 2 && pool.Count > 0; k++)
            {
                var s = pool[r.Next(pool.Count)]; pool.Remove(s);
                outl.Add(new AffixSave { stat = s, value = Loot.AffixValue(s, it.ilvl, it.rarity, r) });
            }
            return outl;
        }

        // ---------------------------------------------------------------------------------------------- alchimiste
        public static readonly (string id, string name, string desc, long price, float minutes)[] Elixirs =
        {
            ("force", "Élixir de fureur", "+15 % de dégâts", 400, 10f),
            ("vie", "Élixir de vigueur", "+15 % de vie maximale", 400, 10f),
            ("savoir", "Élixir de clairvoyance", "+20 % d'expérience gagnée", 600, 15f),
        };
        public static long PotionPrice(int level) => 20 + level * 3;
        public const int MaxPotions = 8;

        // ---------------------------------------------------------------------------------------------- curiosités
        public static int GambleCost(Slot s) => s == Slot.Arme ? 75 : s == Slot.Amulette ? 100 : 50;
        public static ItemSave Gamble(System.Random r, int level, Slot slot)
        {
            for (int t = 0; t < 400; t++) { var it = Loot.Roll(r, level, 4f); if (it.slot == slot && it.rarity >= Rarity.Magique) return it; }
            return Loot.Roll(r, level, 4f);
        }

        // ---------------------------------------------------------------------------------------------- reliquaire
        public static readonly string[] ReceptacleNames = { "Réceptacle de l'arme", "Réceptacle de l'armure", "Réceptacle du bijou" };
        public static int ReceptacleOf(Slot s) => s == Slot.Arme || s == Slot.MainGauche ? 0 : s == Slot.Amulette || s == Slot.Anneau ? 2 : 1;
        public static bool CanExtract(ItemSave it) => it.rarity >= Rarity.Legendaire;

        /// <summary>Extraction : l'objet est détruit, ses deux meilleurs affixes deviennent un pouvoir permanent rangé dans la
        /// collection (arme, armure ou bijou selon l'emplacement d'origine).</summary>
        public static RelicPower Extract(Wallet w, ItemSave it)
        {
            if (!CanExtract(it)) return null;
            var best = it.affixes.OrderByDescending(a => a.value).Take(2).Select(a => new AffixSave { stat = a.stat, value = Mathf.Round(a.value * 0.8f * 10) / 10 }).ToList();
            var p = new RelicPower { id = Guid.NewGuid().ToString("N").Substring(0, 10), name = "Écho : " + it.name, source = it.baseName, receptacle = ReceptacleOf(it.slot), affixes = best };
            w.relics.Add(p); return p;
        }
        public static (long gold, int shard) ExtractCost(ItemSave it) => (500 + it.ilvl * 40, 1 + (int)it.rarity - 3);

        /// <summary>Transmutation : trois objets de même rareté (commun à légendaire) → un objet aléatoire de la rareté supérieure.</summary>
        public static ItemSave Transmute(System.Random r, int level, List<ItemSave> three)
        {
            if (three.Count != 3 || three.Any(x => x.rarity != three[0].rarity) || three[0].rarity >= Rarity.Legendaire) return null;
            var want = three[0].rarity + 1;
            for (int t = 0; t < 500; t++) { var it = Loot.Roll(r, level, 10f); if (it.rarity == want) return it; }
            return null;
        }
        public static long TransmuteCost(Rarity r) => 100L * (1 + (int)r) * (1 + (int)r);

        public static IEnumerable<AffixSave> ActiveRelicAffixes(Wallet w)
        {
            if (w == null) yield break;
            foreach (var id in w.receptacles)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var p = w.relics.FirstOrDefault(x => x.id == id); if (p == null) continue;
                foreach (var a in p.affixes) yield return a;
            }
        }

        // ---------------------------------------------------------------------------------------------- butin d'or
        public static long GoldDrop(System.Random r, int level, float elite) => (long)((3 + level * 1.6f) * (0.6f + r.NextDouble() * 0.8f) * (1 + elite * 4));
    }
}
