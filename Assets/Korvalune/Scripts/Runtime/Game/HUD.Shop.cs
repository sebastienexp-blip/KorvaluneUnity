using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Fenêtres des marchands de la ville : une même fenêtre à onglets (grille d'objets à gauche, fiche et actions à
    /// droite, bourse en haut) configurée selon le marchand — armurier et marchand (achat, vente, potions), forgeron
    /// (fabrication, démontage), joaillier (sertissage, fusion de gemmes), mystique (réenchantement), alchimiste (potions,
    /// élixirs), marchande de curiosités (objets mystères), coffre (dépôt partagé), Reliquaire Céleste (extraction de
    /// pouvoirs, réceptacles, transmutation).</summary>
    public partial class HUD
    {
        class Entry { public ItemSave item; public string label, sub; public Color color = UIStyle.Gold; public Sprite icon; public object tag; public bool worn; }
        class Act { public string label; public Func<bool> can; public Action run; }
        class Tab { public string name; public Func<List<Entry>> list; public Func<Entry, string> detail; public Func<Entry, List<Act>> acts; public bool multi; }

        GameObject shopPanel; Text shopTitle, shopPurse, shopDetail; Merchant shopOwner;
        readonly List<(Image frame, Image glow, Text label)> shopCells = new List<(Image, Image, Text)>();
        readonly List<(Image bg, Text t)> shopTabs = new List<(Image, Text)>();
        readonly List<(Image bg, Text t, Button b)> shopActs = new List<(Image, Text, Button)>();
        List<Tab> tabs = new List<Tab>(); int tabIdx; List<Entry> shown = new List<Entry>(); Entry sel; readonly List<Entry> multiSel = new List<Entry>();
        readonly Dictionary<MerchantKind, List<ItemSave>> stocks = new Dictionary<MerchantKind, List<ItemSave>>(); int stockLevel = -1;
        List<AffixSave> rerollOffer; int rerollIdx = -1; ItemSave rerollItem;
        readonly System.Random shopRng = new System.Random();

        Wallet W => player.wallet;
        Equipment Eq => player.GetComponent<Equipment>();

        public bool ShopOpen => shopPanel && shopPanel.activeSelf;

        void BuildShop(RectTransform root)
        {
            var panel = Panel(root, "Commerce", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 940)); shopPanel = panel.gameObject;
            shopTitle = Label(panel, "Titre", "", 40, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -22), new Vector2(1100, 56)); shopTitle.color = UIStyle.Gold;
            shopPurse = Label(panel, "Bourse", "", 23, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -78), new Vector2(1500, 34));
            RoundButton(panel, "Fermer", "attack", new Vector2(1, 1), new Vector2(-24, -20), 80, CloseShop, out _, out _);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var bg = Img(panel, "Onglet" + i, UIStyle.Button(), new Vector2(0, 1), new Vector2(40 + i * 236, -122), new Vector2(226, 62)); bg.raycastTarget = true; bg.type = Image.Type.Simple;
                var t = Label(bg.rectTransform, "T", "", 25, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(226, 62));
                var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => { tabIdx = k; sel = null; multiSel.Clear(); rerollOffer = null; RefreshShop(); });
                shopTabs.Add((bg, t));
            }
            for (int i = 0; i < 40; i++)
            {
                int k = i;
                var c = Cell(panel, "Case_" + i, new Vector2(0, 1), new Vector2(40 + (i % 8) * 116, -200 - (i / 8) * 116), 106, () => PickShop(k));
                shopCells.Add(c);
            }
            var side = Img(panel, "Fiche", UIStyle.Bar(), new Vector2(0, 1), new Vector2(990, -200), new Vector2(630, 700)); side.type = Image.Type.Sliced; side.color = new Color(0.06f, 0.045f, 0.035f, 0.95f);
            shopDetail = Label(side.rectTransform, "Texte", "", 23, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(22, -16), new Vector2(586, 300));
            FitText(shopDetail, 13);
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                var im = Img(side.rectTransform, "Action" + i, UIStyle.Button(), new Vector2(0.5f, 0), new Vector2(0, 20 + (4 - i) * 76), new Vector2(560, 66)); im.raycastTarget = true;
                var t = Label(im.rectTransform, "T", "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 66)); t.color = UIStyle.Gold;
                var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => RunAct(k));
                shopActs.Add((im, t, b));
            }
            shopPanel.SetActive(false);
            Merchant.Opened += OpenShop;
        }

        public void OpenShop(Merchant m)
        {
            if (!shopPanel || !player) return;
            if (bagPanel) bagPanel.SetActive(false); if (treePanel) treePanel.SetActive(false);
            shopOwner = m; tabIdx = 0; sel = null; multiSel.Clear(); rerollOffer = null;
            if (stockLevel != player.level) { stocks.Clear(); stockLevel = player.level; }
            tabs = TabsFor(m.kind);
            shopTitle.text = m.title + (string.IsNullOrEmpty(m.displayName) ? "" : " — " + m.displayName);
            shopPanel.SetActive(true); RefreshShop();
        }

        public void CloseShop() { if (shopPanel) shopPanel.SetActive(false); FindFirstObjectByType<GameBootstrap>()?.Persist(); }

        // ------------------------------------------------------------------------------------------ entrées
        static Entry E(ItemSave it, bool worn = false) => new Entry { item = it, worn = worn };
        List<Entry> Bag() => player.inventory.Select(x => E(x)).ToList();
        List<Entry> BagAndWorn() => Eq.worn.Values.Select(x => E(x, true)).Concat(Bag()).ToList();
        List<ItemSave> StockOf(MerchantKind k) { if (!stocks.TryGetValue(k, out var s)) stocks[k] = s = Economy.Stock(k, player.level); return s; }

        static readonly Slot[] AllSlots = (Slot[])Enum.GetValues(typeof(Slot));
        static readonly Dictionary<Slot, string> SlotIconBase = new Dictionary<Slot, string>
        { {Slot.Arme,"Épée longue"},{Slot.MainGauche,"Écu"},{Slot.Casque,"Heaume"},{Slot.Torse,"Cuirasse"},{Slot.Gants,"Gantelets"},{Slot.Jambes,"Jambières"},{Slot.Bottes,"Bottes"},{Slot.Amulette,"Amulette"},{Slot.Anneau,"Anneau"} };
        List<Entry> SlotEntries(Func<Slot, string> sub) => AllSlots.Select(s => new Entry { label = SlotName[s], sub = sub(s), icon = ItemIcons.Get(SlotIconBase[s]), tag = s }).ToList();

        bool Pay(long gold, int scrap = 0, int essence = 0, int shard = 0, int shadow = 0)
        {
            if (W.gold < gold || W.scrap < scrap || W.essence < essence || W.starShard < shard || W.shadow < shadow) { Notify("Ressources insuffisantes"); return false; }
            W.gold -= gold; W.scrap -= scrap; W.essence -= essence; W.starShard -= shard; W.shadow -= shadow; return true;
        }
        bool Room() { if (player.inventory.Count >= Equipment.BagSize) { Notify("Sac plein"); return false; } return true; }
        string Cost(long g, int scrap = 0, int ess = 0, int shard = 0, int shadow = 0)
        {
            var p = new List<string>();
            if (g > 0) p.Add($"{g} or"); if (scrap > 0) p.Add($"{scrap} ferraille"); if (ess > 0) p.Add($"{ess} essence"); if (shard > 0) p.Add($"{shard} éclat(s) d'étoile"); if (shadow > 0) p.Add($"{shadow} éclats d'ombre");
            return string.Join(", ", p);
        }
        void Remove(Entry e) { if (e.worn) { Eq.worn.Remove(e.item.slot); Eq.Recompute(); } else player.inventory.Remove(e.item); }

        // ------------------------------------------------------------------------------------------ onglets par marchand
        List<Tab> TabsFor(MerchantKind k)
        {
            var list = new List<Tab>();
            Tab Buy() => new Tab
            {
                name = "Acheter", list = () => StockOf(k).Select(x => new Entry { item = x, sub = Economy.BuyPrice(x) + " or" }).ToList(),
                detail = e => Loot.Tooltip(e.item) + $"\n\n<color=#e8c27a>Prix : {Economy.BuyPrice(e.item)} or</color>" + Compare(e.item),
                acts = e => new List<Act> { new Act { label = $"Acheter ({Economy.BuyPrice(e.item)} or)", can = () => W.gold >= Economy.BuyPrice(e.item), run = () => { if (!Room() || !Pay(Economy.BuyPrice(e.item))) return; StockOf(k).Remove(e.item); player.inventory.Add(e.item); Notify("Acheté : " + e.item.name); sel = null; } } }
            };
            Tab Sell() => new Tab
            {
                name = "Vendre", list = () => Bag().Select(x => { x.sub = Economy.SellPrice(x.item) + " or"; return x; }).ToList(),
                detail = e => Loot.Tooltip(e.item),
                acts = e => new List<Act>
                {
                    new Act { label = $"Vendre ({Economy.SellPrice(e.item)} or)", can = () => true, run = () => { W.gold += Economy.SellPrice(e.item); player.inventory.Remove(e.item); StockOf(k).Add(e.item); sel = null; } },
                    new Act { label = "Vendre tous les communs et magiques", can = () => player.inventory.Any(x => x.rarity <= Rarity.Magique), run = () => SellAll(Rarity.Magique) },
                    new Act { label = "Vendre aussi les rares", can = () => player.inventory.Any(x => x.rarity <= Rarity.Rare), run = () => SellAll(Rarity.Rare) },
                }
            };
            long RepairTotal() => BagAndWorn().Where(x => Economy.Damaged(x.item)).Sum(x => Economy.RepairCost(x.item));
            Tab Repair() => new Tab
            {
                name = "Réparer", multi = true,
                list = () => BagAndWorn().Where(x => Economy.Damaged(x.item)).Select(x => { x.sub = Economy.RepairCost(x.item) + " or"; return x; }).ToList(),
                detail = e => (e == null
                        ? "<b>Réparation</b>\nLes objets s'usent au combat et perdent 10 % de durabilité à chaque mort. Un objet brisé ne donne plus aucun bonus."
                        : Loot.Tooltip(e.item) + (e.worn ? "\n<color=#9fd7ff>(porté)</color>" : "") + $"\n\n<color=#e8c27a>Réparation : {Economy.RepairCost(e.item)} or</color>")
                    + $"\n\n<color=#a08c6c>Tout réparer : {RepairTotal()} or</color>",
                acts = e =>
                {
                    var l = new List<Act>();
                    if (e != null && e.item != null)
                        l.Add(new Act { label = $"Réparer ({Economy.RepairCost(e.item)} or)", can = () => W.gold >= Economy.RepairCost(e.item), run = () => { if (!Pay(Economy.RepairCost(e.item))) return; e.item.durability = e.item.maxDurability; Eq.Repaired(); Notify("Réparé : " + e.item.name); sel = null; multiSel.Clear(); } });
                    l.Add(new Act { label = $"Tout réparer ({RepairTotal()} or)", can = () => RepairTotal() > 0, run = () => { RepairAll(); multiSel.Clear(); } });
                    return l;
                }
            };
            switch (k)
            {
                case MerchantKind.Armurier: list.Add(Buy()); list.Add(Sell()); list.Add(Repair()); break;
                case MerchantKind.Marchand:
                    list.Add(Buy()); list.Add(Sell());
                    list.Add(new Tab
                    {
                        name = "Potions", list = () => new List<Entry> { new Entry { label = "Potion de soin", sub = Economy.PotionPrice(player.level) + " or", color = new Color(1f, 0.3f, 0.25f), tag = "potion" } },
                        detail = e => $"<b>Potion de soin</b>\nRend 45 % de la vie maximale. Recharge : 15 s.\nVous en portez {W.potions} / {Economy.MaxPotions}.\n\n<color=#e8c27a>{Economy.PotionPrice(player.level)} or l'unité</color>",
                        acts = e => new List<Act>
                        {
                            new Act { label = "Acheter une potion", can = () => W.potions < Economy.MaxPotions, run = () => { if (Pay(Economy.PotionPrice(player.level))) W.potions++; } },
                            new Act { label = "Remplir la ceinture", can = () => W.potions < Economy.MaxPotions, run = () => { while (W.potions < Economy.MaxPotions && Pay(Economy.PotionPrice(player.level))) W.potions++; } },
                        }
                    });
                    break;
                case MerchantKind.Forgeron:
                    list.Add(new Tab
                    {
                        name = "Fabriquer", list = () => SlotEntries(s => ""),
                        detail = e =>
                        {
                            var r = Economy.CraftCost(player.level, false); var l = Economy.CraftCost(player.level, true);
                            return $"<b>Fabriquer : {e.label}</b>\nUn objet neuf, forgé à votre niveau ({player.level + 1}).\n\n<color=#ffe640>Rare</color> : {Cost(r.gold, r.scrap, r.essence)}\n<color=#ff8014>Légendaire</color> : {Cost(l.gold, l.scrap, l.essence, l.shard)}";
                        },
                        acts = e => new List<Act>
                        {
                            new Act { label = "Forger un objet rare", can = () => true, run = () => Craft((Slot)e.tag, false) },
                            new Act { label = "Forger un objet légendaire", can = () => true, run = () => Craft((Slot)e.tag, true) },
                        }
                    });
                    list.Add(new Tab
                    {
                        name = "Démonter", list = Bag,
                        detail = e => { var y = Economy.SalvageYield(e.item); return Loot.Tooltip(e.item) + $"\n\n<color=#9fd7ff>Démontage : {Cost(0, y.scrap, y.essence, y.shard)}</color>"; },
                        acts = e => new List<Act>
                        {
                            new Act { label = "Démonter cet objet", can = () => true, run = () => { Economy.Salvage(W, e.item); player.inventory.Remove(e.item); sel = null; } },
                            new Act { label = "Démonter tous les communs", can = () => player.inventory.Any(x => x.rarity == Rarity.Commun), run = () => SalvageAll(Rarity.Commun) },
                            new Act { label = "Démonter jusqu'aux magiques", can = () => player.inventory.Any(x => x.rarity <= Rarity.Magique), run = () => SalvageAll(Rarity.Magique) },
                            new Act { label = "Démonter jusqu'aux rares", can = () => player.inventory.Any(x => x.rarity <= Rarity.Rare), run = () => SalvageAll(Rarity.Rare) },
                        }
                    });
                    list.Add(Repair());
                    break;
                case MerchantKind.Joaillier:
                    list.Add(new Tab
                    {
                        name = "Sertir", list = () => BagAndWorn().Where(x => Economy.Sockets(x.item) > 0).ToList(),
                        detail = e => Loot.Tooltip(e.item) + (e.worn ? "\n<color=#9fd7ff>(porté)</color>" : "") + $"\n\nChâsses : {e.item.gems.Count} / {Economy.Sockets(e.item)}" + (W.gems.Count == 0 ? "\n<color=#a08c6c>Vous n'avez aucune gemme.</color>" : ""),
                        acts = e =>
                        {
                            var a = W.gems.OrderByDescending(g => g.tier).Take(4).Select(g => new Act
                            {
                                label = $"Sertir : {Economy.GemName(g.kind, g.tier)} (×{g.count})", can = () => e.item.gems.Count < Economy.Sockets(e.item),
                                run = () => { if (Economy.Socket(W, e.item, g.kind, g.tier)) { Eq.Recompute(); Notify("Gemme sertie"); } }
                            }).ToList();
                            a.Add(new Act { label = $"Retirer les gemmes ({Economy.UnsocketCost(e.item)} or)", can = () => e.item.gems.Count > 0, run = () => { if (Economy.Unsocket(W, e.item)) Eq.Recompute(); else Notify("Pas assez d'or"); } });
                            return a;
                        }
                    });
                    list.Add(new Tab
                    {
                        name = "Gemmes", list = () => W.gems.OrderBy(g => g.kind).ThenBy(g => g.tier).Select(g => new Entry { label = g.kind, sub = $"T{g.tier} ×{g.count}", color = Economy.GemColor[Array.IndexOf(Economy.GemKinds, g.kind)], tag = g }).ToList(),
                        detail = e => { var g = (GemStack)e.tag; int k2 = Array.IndexOf(Economy.GemKinds, g.kind); return $"<b>{Economy.GemName(g.kind, g.tier)}</b> ×{g.count}\nSertie : +{Economy.GemValue(g.kind, g.tier)}{(Loot.IsPercent(Economy.GemStat[k2]) ? " %" : "")} {Economy.GemStat[k2]}\n\nFusion : 3 gemmes → 1 de taille supérieure ({Economy.FuseCost(g.tier)} or)"; },
                        acts = e => { var g = (GemStack)e.tag; return new List<Act> { new Act { label = "Fusionner 3 gemmes", can = () => g.count >= 3 && g.tier < 5, run = () => { if (!Economy.Fuse(W, g.kind, g.tier)) Notify("Impossible : 3 gemmes et de l'or sont nécessaires"); else { Notify("Fusion réussie"); sel = null; } } } }; }
                    });
                    break;
                case MerchantKind.Mystique:
                    list.Add(new Tab
                    {
                        name = "Réenchanter", list = () => BagAndWorn().Where(x => x.item.affixes.Count > 0).ToList(),
                        detail = e =>
                        {
                            var c = Economy.RerollCost(e.item, e.item.rerolls);
                            var s = Loot.Tooltip(e.item) + (e.worn ? "\n<color=#9fd7ff>(porté)</color>" : "");
                            if (rerollOffer != null && rerollItem == e.item) return s + $"\n\n<b>Nouvelle propriété pour « {e.item.affixes[rerollIdx].stat} » :</b>";
                            return s + $"\n\nChoisissez la propriété à réenchanter.\n<color=#e8c27a>Coût : {Cost(c.gold, 0, c.essence)}</color>";
                        },
                        acts = e =>
                        {
                            if (rerollOffer != null && rerollItem == e.item)
                            {
                                var cur = e.item.affixes[rerollIdx];
                                var a = new List<Act> { new Act { label = $"Garder : +{cur.value}{Pct(cur.stat)} {cur.stat}", can = () => true, run = () => { rerollOffer = null; } } };
                                foreach (var o in rerollOffer) { var oo = o; a.Add(new Act { label = $"Prendre : +{oo.value}{Pct(oo.stat)} {oo.stat}", can = () => true, run = () => { e.item.affixes[rerollIdx] = oo; rerollOffer = null; Eq.Recompute(); Notify("Propriété réenchantée"); } }); }
                                return a;
                            }
                            return e.item.affixes.Take(5).Select((af, i) => new Act
                            {
                                label = $"Changer : +{af.value}{Pct(af.stat)} {af.stat}", can = () => true,
                                run = () => { var c = Economy.RerollCost(e.item, e.item.rerolls); if (!Pay(c.gold, 0, c.essence)) return; e.item.rerolls++; rerollItem = e.item; rerollIdx = i; rerollOffer = Economy.RerollOptions(shopRng, e.item, i); }
                            }).ToList();
                        }
                    });
                    break;
                case MerchantKind.Alchimiste:
                    list.Add(new Tab
                    {
                        name = "Breuvages",
                        list = () => new[] { new Entry { label = "Potion de soin", sub = Economy.PotionPrice(player.level) + " or", color = new Color(1f, 0.3f, 0.25f), tag = "potion" } }
                            .Concat(Economy.Elixirs.Select(x => new Entry { label = x.name, sub = x.price + " or", color = x.id == "force" ? new Color(1f, 0.45f, 0.2f) : x.id == "vie" ? new Color(0.4f, 1f, 0.45f) : new Color(0.6f, 0.7f, 1f), tag = x.id })).ToList(),
                        detail = e =>
                        {
                            if ((string)e.tag == "potion") return $"<b>Potion de soin</b>\nRend 45 % de la vie maximale.\nVous en portez {W.potions} / {Economy.MaxPotions}.";
                            var x = Economy.Elixirs.First(y => y.id == (string)e.tag);
                            string active = Time.time < W.elixirUntil ? $"\n\n<color=#9fd7ff>Actif : {Economy.Elixirs.FirstOrDefault(y => y.id == W.elixir).name} ({(W.elixirUntil - Time.time) / 60f:0} min)</color>" : "";
                            return $"<b>{x.name}</b>\n{x.desc} pendant {x.minutes:0} minutes.\nUn seul élixir à la fois." + active;
                        },
                        acts = e =>
                        {
                            if ((string)e.tag == "potion") return new List<Act> { new Act { label = $"Acheter ({Economy.PotionPrice(player.level)} or)", can = () => W.potions < Economy.MaxPotions, run = () => { if (Pay(Economy.PotionPrice(player.level))) W.potions++; } } };
                            var x = Economy.Elixirs.First(y => y.id == (string)e.tag);
                            return new List<Act> { new Act { label = $"Boire ({x.price} or)", can = () => W.gold >= x.price, run = () => { if (!Pay(x.price)) return; W.elixir = x.id; W.elixirUntil = Time.time + x.minutes * 60f; Eq.Recompute(); Notify(x.name + " : " + x.desc); } } };
                        }
                    });
                    break;
                case MerchantKind.Curiosites:
                    list.Add(new Tab
                    {
                        name = "Objets mystères", list = () => SlotEntries(s => Economy.GambleCost(s) + " ombre"),
                        detail = e => $"<b>Objet mystère : {e.label}</b>\nEnveloppé dans un linge noir… Sa rareté n'est révélée qu'à l'achat (magique au minimum, légendaire possible).\n\n<color=#c7a0ff>{Economy.GambleCost((Slot)e.tag)} éclats d'ombre</color> (vous en avez {W.shadow})\nLes éclats d'ombre tombent des élites et des gardiens.",
                        acts = e => new List<Act> { new Act { label = "Acheter l'objet mystère", can = () => W.shadow >= Economy.GambleCost((Slot)e.tag), run = () => { if (!Room() || !Pay(0, 0, 0, 0, Economy.GambleCost((Slot)e.tag))) return; var it = Economy.Gamble(shopRng, player.level, (Slot)e.tag); player.inventory.Add(it); Notify($"<color=#{ColorUtility.ToHtmlStringRGB(Loot.ColorOf(it.rarity))}>{it.name}</color> ({Loot.LabelOf(it.rarity)})"); } } }
                    });
                    break;
                case MerchantKind.Coffre:
                    var acc = SaveSystem.Account ?? SaveSystem.Load();
                    list.Add(new Tab
                    {
                        name = "Coffre", list = () => acc.stash.Select(x => E(x)).ToList(), detail = e => Loot.Tooltip(e.item),
                        acts = e => new List<Act> { new Act { label = "Reprendre dans le sac", can = () => true, run = () => { if (!Room()) return; acc.stash.Remove(e.item); player.inventory.Add(e.item); sel = null; SaveSystem.Save(); } } }
                    });
                    list.Add(new Tab
                    {
                        name = "Déposer", list = Bag, detail = e => Loot.Tooltip(e.item) + $"\n\nCoffre : {acc.stash.Count} / 40 (partagé entre vos personnages)",
                        acts = e => new List<Act> { new Act { label = "Déposer dans le coffre", can = () => acc.stash.Count < 40, run = () => { player.inventory.Remove(e.item); acc.stash.Add(e.item); sel = null; SaveSystem.Save(); } } }
                    });
                    break;
                case MerchantKind.Reliquaire:
                    list.Add(new Tab
                    {
                        name = "Extraire", list = () => Bag().Where(x => Economy.CanExtract(x.item)).ToList(),
                        detail = e => { var c = Economy.ExtractCost(e.item); var best = e.item.affixes.OrderByDescending(a => a.value).Take(2); return Loot.Tooltip(e.item) + $"\n\n<b>L'éclat d'étoile absorbe l'âme de l'objet.</b>\nL'objet est détruit ; ses deux meilleures propriétés (à 80 %) deviennent un écho pour le {Economy.ReceptacleNames[Economy.ReceptacleOf(e.item.slot)].ToLower()} :\n" + string.Join("\n", best.Select(a => $"  +{Mathf.Round(a.value * 8) / 10}{Pct(a.stat)} {a.stat}")) + $"\n<color=#e8c27a>Coût : {Cost(c.gold, 0, 0, c.shard)}</color>"; },
                        acts = e => new List<Act> { new Act { label = "Extraire le pouvoir", can = () => true, run = () => { var c = Economy.ExtractCost(e.item); if (!Pay(c.gold, 0, 0, c.shard)) return; var p = Economy.Extract(W, e.item); player.inventory.Remove(e.item); sel = null; Notify("Écho recueilli : " + p.name); } } }
                    });
                    list.Add(new Tab
                    {
                        name = "Réceptacles", list = () => W.relics.Select(p => new Entry { label = p.source, sub = Economy.ReceptacleNames[p.receptacle].Replace("Réceptacle ", ""), color = W.receptacles.Contains(p.id) ? new Color(0.55f, 0.85f, 1f) : UIStyle.Gold, icon = ItemIcons.Get(p.source), tag = p }).ToList(),
                        detail = e =>
                        {
                            var p = (RelicPower)e.tag;
                            var s = $"<b>{p.name}</b>\n" + string.Join("\n", p.affixes.Select(a => $"+{a.value}{Pct(a.stat)} {a.stat}")) + "\n\n<b>Réceptacles :</b>";
                            for (int i = 0; i < 3; i++) { var cur = W.relics.FirstOrDefault(x => x.id == W.receptacles[i]); s += $"\n{Economy.ReceptacleNames[i]} : {(cur != null ? cur.name : "<color=#8a7a66>vide</color>")}"; }
                            return s;
                        },
                        acts = e =>
                        {
                            var p = (RelicPower)e.tag; bool on = W.receptacles[p.receptacle] == p.id;
                            return new List<Act>
                            {
                                new Act { label = on ? "Retirer du réceptacle" : $"Placer dans le {Economy.ReceptacleNames[p.receptacle].ToLower()}", can = () => true, run = () => { W.receptacles[p.receptacle] = on ? "" : p.id; Eq.Recompute(); } },
                            };
                        }
                    });
                    list.Add(new Tab
                    {
                        name = "Transmuter", multi = true, list = Bag,
                        detail = e => $"<b>Transmutation</b>\nTrois objets de même rareté (commun à rare) → un objet aléatoire de la rareté supérieure.\nChoisis : {multiSel.Count} / 3" + (multiSel.Count > 0 ? $"\n<color=#e8c27a>Coût : {Economy.TransmuteCost(multiSel[0].item.rarity)} or</color>" : ""),
                        acts = e => new List<Act>
                        {
                            new Act { label = "Transmuter", can = () => multiSel.Count == 3, run = () =>
                            {
                                var three = multiSel.Select(x => x.item).ToList();
                                if (three.Any(x => x.rarity != three[0].rarity) || three[0].rarity >= Rarity.Legendaire) { Notify("Il faut trois objets de même rareté (jusqu'à rare)"); return; }
                                if (!Pay(Economy.TransmuteCost(three[0].rarity))) return;
                                var it = Economy.Transmute(shopRng, player.level, three); if (it == null) return;
                                foreach (var x in three) player.inventory.Remove(x);
                                player.inventory.Add(it); multiSel.Clear(); sel = null; Notify($"<color=#{ColorUtility.ToHtmlStringRGB(Loot.ColorOf(it.rarity))}>{it.name}</color>");
                            } },
                            new Act { label = "Vider la sélection", can = () => multiSel.Count > 0, run = () => multiSel.Clear() },
                        }
                    });
                    break;
            }
            return list;
        }

        static string Pct(string stat) => Loot.IsPercent(stat) ? " %" : "";
        string Compare(ItemSave it)
        {
            var cur = Eq ? Eq.In(it.slot) : null; if (cur == null) return "";
            float d = it.power - cur.power;
            return $"\n<color=#{(d >= 0 ? "7CFC7C" : "FF7070")}>{(d >= 0 ? "▲ +" : "▼ ")}{d:0} puissance par rapport à « {cur.name} »</color>";
        }
        void SellAll(Rarity max)
        {
            var l = player.inventory.Where(x => x.rarity <= max).ToList(); long g = 0;
            foreach (var x in l) { g += Economy.SellPrice(x); player.inventory.Remove(x); }
            W.gold += g; Notify($"{l.Count} objets vendus : +{g} or"); sel = null;
        }
        void SalvageAll(Rarity max)
        {
            var l = player.inventory.Where(x => x.rarity <= max).ToList();
            foreach (var x in l) { Economy.Salvage(W, x); player.inventory.Remove(x); }
            Notify($"{l.Count} objets démontés"); sel = null;
        }
        /// <summary>Répare tout l'équipement porté puis le sac, tant que l'or suffit.</summary>
        void RepairAll()
        {
            long spent = 0; int n = 0;
            foreach (var e in BagAndWorn().Where(x => Economy.Damaged(x.item)))
            {
                long c = Economy.RepairCost(e.item); if (W.gold < c) continue;
                W.gold -= c; spent += c; n++; e.item.durability = e.item.maxDurability;
            }
            Eq.Repaired(); sel = null;
            Notify(n > 0 ? $"{n} objet(s) réparé(s) pour {spent} or" : "Pas assez d'or");
        }

        void Craft(Slot s, bool legendary)
        {
            if (!Room()) return;
            var c = Economy.CraftCost(player.level, legendary);
            if (!Pay(c.gold, c.scrap, c.essence, c.shard)) return;
            var it = Economy.Craft(shopRng, player.level, s, legendary);
            if (it == null) { W.gold += c.gold; W.scrap += c.scrap; W.essence += c.essence; W.starShard += c.shard; Notify("La forge a échoué, rien n'est perdu"); return; }
            player.inventory.Add(it); Notify($"Forgé : <color=#{ColorUtility.ToHtmlStringRGB(Loot.ColorOf(it.rarity))}>{it.name}</color>");
        }

        // ------------------------------------------------------------------------------------------ interaction
        void PickShop(int k)
        {
            if (k >= shown.Count) return;
            var e = shown[k]; var tab = tabs[tabIdx];
            if (tab.multi)
            {
                var hit = multiSel.FirstOrDefault(x => x.item == e.item);
                if (hit != null) multiSel.Remove(hit); else if (multiSel.Count < 3) multiSel.Add(e);
            }
            if (rerollOffer != null && (sel == null || sel.item != e.item)) rerollOffer = null;
            sel = e; RefreshShop();
        }

        List<Act> currentActs = new List<Act>();
        void RunAct(int k) { if (k < currentActs.Count && currentActs[k].can()) { currentActs[k].run(); RefreshShop(); RefreshBag(); } }

        void RefreshShop()
        {
            if (!shopPanel || !shopPanel.activeSelf || tabs.Count == 0) return;
            shopPurse.text = $"<color=#ffd95a>{W.gold:N0} or</color>    Ferraille {W.scrap}    Essence {W.essence}    Éclats d'étoile {W.starShard}    <color=#c7a0ff>Éclats d'ombre {W.shadow}</color>    <color=#ff7a6a>Potions {W.potions}</color>    Sac {player.inventory.Count}/{Equipment.BagSize}";
            for (int i = 0; i < shopTabs.Count; i++)
            {
                bool on = i < tabs.Count; shopTabs[i].bg.gameObject.SetActive(on); if (!on) continue;
                shopTabs[i].t.text = tabs[i].name; shopTabs[i].t.color = i == tabIdx ? Color.white : UIStyle.Parch;
                shopTabs[i].bg.color = i == tabIdx ? new Color(1f, 0.85f, 0.6f) : new Color(0.7f, 0.6f, 0.5f);
            }
            var tab = tabs[tabIdx];
            shown = tab.list();
            if (sel != null && !shown.Any(x => (x.item != null && x.item == sel.item) || (x.item == null && Equals(x.tag, sel.tag)))) sel = null;
            for (int i = 0; i < shopCells.Count; i++)
            {
                var c = shopCells[i];
                var e = i < shown.Count ? shown[i] : null;
                if (e == null || e.item != null) Paint(c, e?.item, "");
                else
                {
                    var icon = c.frame.transform.Find("Icone")?.GetComponent<Image>();
                    c.frame.color = Color.Lerp(new Color(0.10f, 0.07f, 0.05f), e.color, 0.25f); c.glow.color = new Color(0, 0, 0, 0);
                    if (icon) { icon.sprite = e.icon; icon.enabled = e.icon; }
                    c.label.alignment = e.icon ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
                    c.label.text = e.icon ? $"<size=15>{e.sub}</size>" : $"<color=#{ColorUtility.ToHtmlStringRGB(e.color)}><b>{e.label}</b></color>\n<size=15>{e.sub}</size>";
                }
                if (e != null && e.item != null && !string.IsNullOrEmpty(e.sub)) c.label.text += $"\n<size=14><color=#ffd95a>{e.sub}</color></size>";
                if (e != null && e.worn) c.label.text += "\n<size=13><color=#9fd7ff>porté</color></size>";
                bool chosen = e != null && (sel == e || (sel != null && e.item != null && sel.item == e.item) || multiSel.Any(x => x.item != null && x.item == e?.item));
                if (chosen) c.glow.color = new Color(1f, 0.9f, 0.55f, 0.75f);
            }
            if (sel == null)
            {
                shopDetail.text = shown.Count == 0 ? "<color=#a08c6c>Rien à afficher ici pour l'instant.</color>" : "<color=#a08c6c>Touchez un objet pour voir sa fiche.</color>";
                if (tab.multi) shopDetail.text = tab.detail(null);
                currentActs = tab.multi ? tab.acts(null) : new List<Act>();
            }
            else { shopDetail.text = tab.detail(sel); currentActs = tab.acts(sel); }
            for (int i = 0; i < shopActs.Count; i++)
            {
                bool on = i < currentActs.Count; shopActs[i].bg.gameObject.SetActive(on); if (!on) continue;
                bool can = currentActs[i].can(); shopActs[i].t.text = currentActs[i].label; shopActs[i].t.color = can ? UIStyle.Gold : new Color(0.5f, 0.45f, 0.4f);
                shopActs[i].bg.color = can ? Color.white : new Color(0.55f, 0.5f, 0.45f);
            }
        }
    }
}
