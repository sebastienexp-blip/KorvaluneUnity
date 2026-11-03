using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Inventaire (dans l'esprit des grands ARPG) : à gauche, onglet « Équipement » (le héros en 3D au centre, ses
    /// neuf emplacements de part et d'autre) ou onglet « Caractéristiques » (toutes les statistiques par catégorie, en
    /// grands caractères) ; à droite, le sac. Toucher un objet ouvre sa grande fiche — nom à la couleur de rareté, puissance,
    /// statistiques une par ligne — et, pour un objet du sac, la fiche de l'objet porté à côté, chaque statistique comparée
    /// (vert : gain, rouge : perte). Tout est lisible sans loupe : 24 à 34 px.</summary>
    public partial class HUD
    {
        RectTransform invEquip, invStats, cardLayer; Text statsText, bagHeader, heroTitle; RectTransform statsContent;
        Image tabEquip, tabStats; RawImage portrait; Camera portraitCam; RenderTexture portraitRT;
        (RectTransform root, Image band, Text title, Text type, Text power, Text body, RectTransform content) cardA, cardB;
        Image bClose;

        static readonly string[] AllStats = { "Dégâts", "Vie", "Armure", "Force", "Dextérité", "Intelligence", "Chances de coup critique", "Vitesse d'attaque", "Régénération de mana", "Réduction des temps de recharge" };

        void BuildBag(RectTransform root)
        {
            var eq = player.GetComponent<Equipment>();
            var panel = OrnatePanel(root, "Inventaire", new Vector2(1720, 980), "Personnage"); bagPanel = panel.gameObject;
            RoundButton(panel, "Fermer", "close", new Vector2(1, 1), new Vector2(-30, -26), 90, ToggleBag, out _, out _);

            // ---- onglets de gauche
            tabEquip = TextButton(panel, "OngletEquipement", "Équipement", new Vector2(0, 1), new Vector2(50, -96), new Vector2(330, 66), () => { ShowStatsTab(false); }, out _, 30);
            tabStats = TextButton(panel, "OngletStats", "Caractéristiques", new Vector2(0, 1), new Vector2(392, -96), new Vector2(390, 66), () => { ShowStatsTab(true); }, out _, 30);

            // ---- équipement : portrait 3D au centre, emplacements autour
            invEquip = Img(panel, "Equipement", UIStyle.Bar(), new Vector2(0, 1), new Vector2(50, -176), new Vector2(780, 760)).rectTransform;
            invEquip.GetComponent<Image>().type = Image.Type.Sliced; invEquip.GetComponent<Image>().color = new Color(0.62f, 0.50f, 0.38f, 0.75f);
            portrait = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(portrait.rectTransform, invEquip, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(400, 600)); portrait.raycastTarget = false;
            var left = new[] { Slot.Casque, Slot.Torse, Slot.Gants, Slot.Jambes, Slot.Bottes };
            var right = new[] { Slot.Amulette, Slot.Anneau, Slot.Arme, Slot.MainGauche };
            for (int i = 0; i < left.Length; i++) { var sl = left[i]; slotCells[sl] = Cell(invEquip, "Porté_" + sl, new Vector2(0, 1), new Vector2(26, -14 - i * 138), 124, () => Select(eq.In(sl), true)); }
            for (int i = 0; i < right.Length; i++) { var sl = right[i]; slotCells[sl] = Cell(invEquip, "Porté_" + sl, new Vector2(1, 1), new Vector2(-26, -14 - i * 138 - (i >= 2 ? 69 : 0)), 124, () => Select(eq.In(sl), true)); }
            heroTitle = Label(invEquip, "Héros", "", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(740, 120));

            // ---- caractéristiques détaillées (défilantes)
            invStats = Img(panel, "Caracteristiques", UIStyle.Bar(), new Vector2(0, 1), new Vector2(50, -176), new Vector2(780, 760)).rectTransform;
            invStats.GetComponent<Image>().type = Image.Type.Sliced; invStats.GetComponent<Image>().color = new Color(0.36f, 0.28f, 0.20f, 0.9f);
            statsContent = Scroll(invStats, new Vector2(24, -18), new Vector2(732, 724), out statsText, 26);

            // ---- sac
            bagHeader = Label(panel, "EnTeteSac", "", 30, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(880, -96), new Vector2(780, 66));
            for (int i = 0; i < Equipment.BagSize; i++)
            {
                int k = i;
                var c = Cell(panel, "Sac_" + i, new Vector2(0, 1), new Vector2(880 + (i % 8) * 100, -182 - (i / 8) * 100), 94, () =>
                { if (k < player.inventory.Count) Select(player.inventory[k], false); });
                bagCells.Add(c);
            }
            Label(panel, "Aide", "Touchez un objet pour voir sa fiche et le comparer à celui que vous portez.", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(880, -700), new Vector2(790, 70)).color = new Color(0.72f, 0.64f, 0.52f);

            // ---- calque des fiches (au-dessus de tout le panneau)
            cardLayer = Img(panel, "Fiches", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720, 980)).rectTransform;
            var shade = cardLayer.GetComponent<Image>(); shade.color = new Color(0, 0, 0, 0.72f); shade.raycastTarget = true;
            var sb = shade.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(() => Select(null, false));
            cardB = Card(cardLayer, new Vector2(-305, 40));
            cardA = Card(cardLayer, new Vector2(305, 40));
            bEquip = TextButton(cardLayer, "Equiper", "Équiper", new Vector2(0.5f, 0), new Vector2(305 - 200, 30), new Vector2(250, 74), () =>
            {
                if (selected == null) return;
                if (selectedWorn) eq.Unequip(selected.slot); else eq.Equip(selected);
                Sound.UI("sfx_equiper", 0.7f); Select(null, false);
            }, out bEquipText, 30).GetComponent<Button>();
            bDrop = TextButton(cardLayer, "Jeter", "Jeter", new Vector2(0.5f, 0), new Vector2(305 + 70, 30), new Vector2(200, 74), () =>
            { if (selected != null && !selectedWorn) { eq.Discard(selected); Select(null, false); } }, out _, 30).GetComponent<Button>();
            bClose = TextButton(cardLayer, "FermerFiche", "Fermer", new Vector2(0.5f, 0), new Vector2(305 + 300, 30), new Vector2(200, 74), () => Select(null, false), out _, 30);
            cardLayer.gameObject.SetActive(false);
            ShowStatsTab(false);
            bagPanel.SetActive(false);
        }

        /// <summary>Zone défilante contenant un texte (hauteur ajustée au contenu).</summary>
        RectTransform Scroll(RectTransform parent, Vector2 pos, Vector2 size, out Text text, int fontSize)
        {
            var view = new GameObject("Vue", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect)).GetComponent<RectTransform>();
            Place(view, parent, new Vector2(0, 1), pos, size);
            var vi = view.GetComponent<Image>(); vi.color = new Color(1, 1, 1, 0.003f); vi.raycastTarget = true; view.GetComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Contenu", typeof(RectTransform)).GetComponent<RectTransform>();
            Place(content, view, new Vector2(0, 1), Vector2.zero, new Vector2(size.x, size.y));
            text = Label(content, "Texte", "", fontSize, TextAnchor.UpperLeft, new Vector2(0, 1), Vector2.zero, new Vector2(size.x - 12, size.y));
            text.lineSpacing = 1.12f;
            var sr = view.GetComponent<ScrollRect>(); sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40; sr.viewport = view;
            return content;
        }

        static void FitScroll(RectTransform content, Text text, float minH)
        {
            float h = Mathf.Max(minH, text.preferredHeight + 20);
            content.sizeDelta = new Vector2(content.sizeDelta.x, h); text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, h);
        }

        (RectTransform, Image, Text, Text, Text, Text, RectTransform) Card(RectTransform parent, Vector2 pos)
        {
            var bg = Img(parent, "Fiche", UIStyle.Panel(), new Vector2(0.5f, 0.5f), pos, new Vector2(590, 830)); bg.type = Image.Type.Sliced; bg.raycastTarget = true;
            var r = bg.rectTransform;
            var band = Img(r, "Bandeau", UIStyle.Bar(), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(540, 96)); band.type = Image.Type.Sliced;
            var title = Label(band.rectTransform, "Nom", "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 90)); title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            var type = Label(r, "Type", "", 25, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(540, 36));
            var power = Label(r, "Puissance", "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(540, 50)); power.fontStyle = FontStyle.Bold;
            var sep = Img(r, "Filet", UIStyle.Bar(), new Vector2(0.5f, 1), new Vector2(0, -220), new Vector2(500, 4)); sep.color = new Color(0.8f, 0.62f, 0.35f, 0.6f);
            var content = Scroll(r, new Vector2(30, -236), new Vector2(530, 560), out var body, 25);
            return (r, band, title, type, power, body, content);
        }

        void Select(ItemSave it, bool worn)
        {
            selected = it; selectedWorn = worn && it != null;
            if (it != null) Sound.UI("sfx_ui_clic", 0.45f);
            RefreshBag();
        }

        void ShowStatsTab(bool stats)
        {
            if (!invEquip) return;
            invEquip.gameObject.SetActive(!stats); invStats.gameObject.SetActive(stats);
            tabEquip.color = stats ? new Color(0.6f, 0.55f, 0.5f) : Color.white; tabStats.color = stats ? Color.white : new Color(0.6f, 0.55f, 0.5f);
            PortraitActive(!stats && bagPanel && bagPanel.activeSelf);
            if (stats) RefreshBag();
        }

        /// <summary>Caméra de portrait : le héros en 3D au centre de l'équipement (seulement quand l'onglet est visible).</summary>
        void PortraitActive(bool on)
        {
            if (!player || !portrait) return;
            if (on && !portraitCam)
            {
                portraitRT = new RenderTexture(400, 600, 24, RenderTextureFormat.ARGB32) { name = "Portrait du héros" };
                portraitCam = new GameObject("Caméra de portrait").AddComponent<Camera>(); portraitCam.transform.SetParent(player.transform, false);
                portraitCam.transform.localPosition = new Vector3(0, 1.25f, 2.7f); portraitCam.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.2f, -2.7f));
                portraitCam.fieldOfView = 34; portraitCam.nearClipPlane = 0.3f; portraitCam.farClipPlane = 40; portraitCam.targetTexture = portraitRT;
                portraitCam.clearFlags = CameraClearFlags.SolidColor; portraitCam.backgroundColor = new Color(0.30f, 0.25f, 0.20f, 1);
                portraitCam.cullingMask = 1 << PortraitLayer;              // seulement le héros : pas le décor derrière lui
                var key = new GameObject("Éclairage du portrait").AddComponent<Light>(); key.transform.SetParent(portraitCam.transform, false); key.transform.localPosition = new Vector3(0.8f, 0.6f, 0);
                key.type = LightType.Point; key.range = 6; key.intensity = 2.6f; key.color = new Color(1f, 0.92f, 0.8f); key.shadows = LightShadows.None;
                portrait.texture = portraitRT;
            }
            if (on) IsolateHero();
            if (portraitCam) portraitCam.gameObject.SetActive(on);
        }

        const int PortraitLayer = 30;
        /// <summary>Le héros (corps, armes, armure) passe sur un calque à lui, que la caméra principale voit toujours et que la
        /// caméra du portrait est seule à voir : le portrait se détache sur un fond uni, comme une fiche de personnage.</summary>
        void IsolateHero()
        {
            if (!player) return;
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer) && !r.GetComponent<Collider>() && r.gameObject != player.gameObject) r.gameObject.layer = PortraitLayer;   // jamais un objet physique
            var main = Camera.main; if (main) main.cullingMask |= 1 << PortraitLayer;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static string Num(float v, string stat) => (Mathf.Round(v * 10) / 10).ToString("0.#") + (Loot.IsPercent(stat) ? " %" : "");

        /// <summary>Statistiques d'un objet (qualité comprise, gemmes serties incluses).</summary>
        static Dictionary<string, float> StatsOf(ItemSave it)
        {
            var d = new Dictionary<string, float>(); if (it == null) return d;
            float q = it.Quality;
            foreach (var a in it.affixes) { d.TryGetValue(a.stat, out var v); d[a.stat] = v + a.value * q; }
            if (it.gems != null) foreach (var g in Economy.GemAffixes(it)) { d.TryGetValue(g.stat, out var v); d[g.stat] = v + g.value; }
            return d;
        }

        void FillCard((RectTransform root, Image band, Text title, Text type, Text power, Text body, RectTransform content) c, ItemSave it, ItemSave cmp, bool wornLabel)
        {
            var setDef = Powers.Set(it.set);
            var col = setDef != null ? Powers.SetColor : Loot.DisplayColor(it);
            c.band.color = Color.Lerp(new Color(0.12f, 0.08f, 0.05f), col, 0.55f);
            c.title.text = it.DisplayName; c.title.color = Color.Lerp(col, Color.white, 0.25f);
            c.type.text = $"<color=#{Hex(col)}>{(setDef != null ? "Objet d'ensemble" : Loot.LabelOf(it.rarity))}</color> — {it.baseName}{(wornLabel ? "   <color=#9fd7ff>(porté)</color>" : "")}";
            float pw = Mathf.Round(it.power * it.Quality), pc = cmp != null ? Mathf.Round(cmp.power * cmp.Quality) : 0;
            string delta = cmp == null ? "" : pw >= pc ? $"  <color=#7CFC7C>▲ {pw - pc:0}</color>" : $"  <color=#FF7070>▼ {pc - pw:0}</color>";
            c.power.text = $"<color=#f0d090>◆ {pw:0}</color> <size=24>puissance</size>{delta}";
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#c8b89a>Niveau d'objet {it.ilvl}   ·   Qualité </color><color=#e8c070>{Loot.QualityStars(it.upgrade)} +{it.upgrade}/{ItemSave.MaxUpgrade}</color>\n");
            if (!string.IsNullOrEmpty(it.cls)) sb.Append($"<color=#{(string.IsNullOrEmpty(Loot.PlayerClass) || it.cls == Loot.PlayerClass ? "c8b89a" : "ff6060")}>Réservé : {Loot.ClassLabel(it.cls)}</color>\n");
            if (it.reqLevel > 0) sb.Append($"<color=#{(it.reqLevel > Loot.PlayerLevel ? "ff6060" : "c8b89a")}>Niveau requis : {it.reqLevel}</color>\n");
            sb.Append("\n");
            var mine = StatsOf(it); var other = cmp != null ? StatsOf(cmp) : null;
            string main = string.IsNullOrEmpty(Loot.PlayerClass) ? "" : Loot.MainStat(Loot.PlayerClass);
            foreach (var kv in mine)
            {
                string line = $"+{Num(kv.Value, kv.Key)}  {kv.Key}";
                if (Loot.IsMain(kv.Key) && kv.Key != main) line = $"<color=#8a7a66>{line}</color>";
                else if (kv.Key == main) line = $"<b>{line}</b>";
                if (other != null)
                {
                    other.TryGetValue(kv.Key, out var ov); float d = kv.Value - ov;
                    if (Mathf.Abs(d) >= 0.05f) line += d > 0 ? $"   <color=#7CFC7C>▲ +{Num(d, kv.Key)}</color>" : $"   <color=#FF7070>▼ {Num(d, kv.Key)}</color>";
                }
                sb.Append("• ").Append(line).Append('\n');
            }
            if (other != null) foreach (var kv in other) if (!mine.ContainsKey(kv.Key)) sb.Append($"<color=#FF7070>▼ −{Num(kv.Value, kv.Key)}  {kv.Key}  (perdu)</color>\n");
            var leg = Powers.Get(it.legendaryPower);
            if (leg != null) sb.Append($"\n<color=#ff9a3c><b>◆ {leg.name}</b>\n{Powers.Describe(leg, Powers.Strength(it.rarity))}</color>\n");
            if (setDef != null)
            {
                int n = Loot.WornSetCount != null ? Loot.WornSetCount(setDef.id) : 0;
                string On(int need) => n >= need ? "3cdc52" : "7a7468";
                sb.Append($"\n<color=#3cdc52><b>{setDef.name}</b> ({Mathf.Min(n, 6)}/6)</color>\n");
                sb.Append($"<color=#{On(2)}>(2) " + string.Join(", ", setDef.bonus2.ConvertAll(a => $"+{a.value}{(Loot.IsPercent(a.stat) ? " %" : "")} {a.stat}")) + "</color>\n");
                sb.Append($"<color=#{On(4)}>(4) {Powers.Describe(Powers.Get(setDef.power4))}</color>\n");
                sb.Append($"<color=#{On(6)}>(6) {Powers.Describe(Powers.Get(setDef.power6))}</color>\n");
            }
            int sockets = Economy.Sockets(it);
            if (sockets > 0)
            {
                sb.Append('\n');
                for (int k = 0; k < (it.gems?.Count ?? 0); k++) { var p = it.gems[k].Split(':'); sb.Append($"<color=#9fd7ff>◈ {Economy.GemName(p[0], p.Length > 1 ? int.Parse(p[1]) : 1)}</color>\n"); }
                for (int k = it.gems?.Count ?? 0; k < sockets; k++) sb.Append("<color=#8a7a66>○ Châsse vide</color>\n");
            }
            if (it.maxDurability > 0)
                sb.Append(it.Broken ? "\n<color=#ff4040><b>Brisé</b> — à réparer chez le forgeron</color>\n"
                    : $"\n<color=#{(it.durability < it.maxDurability * 0.2f ? "ffb040" : "a09070")}>Durabilité : {Mathf.CeilToInt(it.durability)} / {Mathf.RoundToInt(it.maxDurability)}</color>\n");
            sb.Append($"<color=#e8c27a>Valeur : {Economy.SellPrice(it)} or</color>");
            c.body.text = sb.ToString();
            FitScroll(c.content, c.body, 560);
        }

        string StatsSheet(Equipment eq)
        {
            var h = player.Health; var w = player.wallet;
            string H(string t) => $"\n<size=32><color=#f0c878><b>{t}</b></color></size>\n";
            string L(string k, string v) => $"{k}   <b><color=#ffffff>{v}</color></b>\n";
            var worn = new List<ItemSave>(); foreach (Slot s in System.Enum.GetValues(typeof(Slot))) { var it = eq.In(s); if (it != null) worn.Add(it); }
            float avgPower = worn.Count > 0 ? worn.Average(x => x.power * x.Quality) : 0;
            float dps = player.attackDamage * player.damageMult * (1 + player.critChance) / Mathf.Max(0.05f, player.attackCooldown);
            var sb = new System.Text.StringBuilder();
            sb.Append(H("Général").TrimStart('\n'));
            sb.Append(L("Niveau", $"{player.level}")); sb.Append(L("Expérience", $"{player.xp:0} / {player.xpNext:0}"));
            sb.Append(L("Puissance d'objet moyenne", $"{avgPower:0}")); sb.Append(L("Points de compétence libres", $"{player.skillPoints}"));
            sb.Append(H("Attaque"));
            sb.Append(L("Dégâts par coup", $"{player.attackDamage * player.damageMult:0}")); sb.Append(L("Dégâts par seconde (estimés)", $"{dps:0}"));
            sb.Append(L("Chances de coup critique", $"{player.critChance * 100:0.#} %")); sb.Append(L("Vitesse d'attaque", $"{1f / Mathf.Max(0.05f, player.attackCooldown):0.00} coups/s"));
            sb.Append(L("Réduction des temps de recharge", $"{eq.cooldownReduction * 100:0} %"));
            sb.Append(H("Défense"));
            sb.Append(L("Vie", $"{h.hp:0} / {h.maxHp:0}")); sb.Append(L("Armure", $"{h.armor:0}  (−{h.armor:0} dégâts par coup reçu)"));
            sb.Append(L("Régénération de vie", $"{h.hpRegen:0.#} /s"));
            sb.Append(H("Ressources"));
            sb.Append(L("Mana", $"{h.mana:0} / {h.maxMana:0}")); sb.Append(L("Régénération de mana", $"{h.manaRegen:0.#} /s"));
            sb.Append(L("Potions de soin", $"{w.potions} / {Economy.MaxPotions}")); sb.Append(L("Potions de mana", $"{w.manaPotions} / {Economy.MaxPotions}"));
            sb.Append(H("Déplacement"));
            bool riding = Rider.Current && Rider.Current.Riding;
            sb.Append(L("Vitesse de course", $"{player.runSpeed:0.#} m/s{(riding ? $"  ({Rider.Current.MountName})" : "")}"));
            sb.Append(H("Bonus de l'équipement"));
            foreach (var st in AllStats) { float v = eq.Sum(st); if (Mathf.Abs(v) >= 0.05f) sb.Append(L(st, "+" + Num(v, st))); }
            var sets = new List<string>(); var legs = new List<string>();
            foreach (var it in worn) { var sd = Powers.Set(it.set); if (sd != null && !sets.Contains(it.set)) sets.Add(it.set); var lp = Powers.Get(it.legendaryPower); if (lp != null) legs.Add(lp.name); }
            if (sets.Count > 0 || legs.Count > 0)
            {
                sb.Append(H("Pouvoirs"));
                foreach (var id in sets) sb.Append($"<color=#3cdc52>◆ {Powers.Set(id).name} ({Mathf.Min(6, eq.SetCount(id))}/6)</color>\n");
                foreach (var l in legs) sb.Append($"<color=#ff9a3c>◆ {l}</color>\n");
            }
            sb.Append(H("Trésor"));
            sb.Append(L("Or", $"{w.gold:N0}")); sb.Append(L("Ferraille / Essence", $"{w.scrap} / {w.essence}"));
            sb.Append(L("Éclats d'étoile / d'ombre", $"{w.starShard} / {w.shadow}")); sb.Append(L("Sceaux de brèche", $"{w.riftKeys}  (meilleure Grande Brèche : {w.riftBest})"));
            return sb.ToString();
        }

        void RefreshBag()
        {
            var eq = player ? player.GetComponent<Equipment>() : null; if (!eq || !bagHeader) return;
            foreach (var kv in slotCells) Paint(kv.Value, eq.In(kv.Key), SlotName[kv.Key]);
            for (int i = 0; i < bagCells.Count; i++) Paint(bagCells[i], i < player.inventory.Count ? player.inventory[i] : null, "");
            bagHeader.text = $"Sac  <b>{player.inventory.Count} / {Equipment.BagSize}</b>      <color=#ffd95a>{player.wallet.gold:N0} or</color>";
            var worn = new List<ItemSave>(); foreach (Slot s in System.Enum.GetValues(typeof(Slot))) { var it = eq.In(s); if (it != null) worn.Add(it); }
            float avg = worn.Count > 0 ? worn.Average(x => x.power * x.Quality) : 0;
            heroTitle.text = $"<size=34><b>{player.name.Replace("(Clone)", "")}</b></size>\n{Loot.ClassLabel(Loot.PlayerClass)} · niveau {player.level} · <color=#f0d090>◆ {avg:0}</color>";
            if (portraitCam && portraitCam.gameObject.activeSelf) IsolateHero();   // l'équipement vient peut-être de changer
            if (invStats && invStats.gameObject.activeSelf) { statsText.text = StatsSheet(eq); FitScroll(statsContent, statsText, 724); }
            // fiches
            if (selected == null) { if (cardLayer.gameObject.activeSelf) cardLayer.gameObject.SetActive(false); return; }
            cardLayer.gameObject.SetActive(true);
            var cur = selectedWorn ? null : eq.In(selected.slot);
            FillCard(cardA, selected, cur, selectedWorn);
            cardB.root.gameObject.SetActive(cur != null);
            if (cur != null) FillCard(cardB, cur, null, true);
            cardA.root.anchoredPosition = new Vector2(cur != null ? 305 : 0, 40);
            bEquipText.text = selectedWorn ? "Retirer" : "Équiper";
            bDrop.gameObject.SetActive(!selectedWorn);
            float x = cardA.root.anchoredPosition.x;
            ((RectTransform)bEquip.transform).anchoredPosition = new Vector2(x - 200, 30);
            ((RectTransform)bDrop.transform).anchoredPosition = new Vector2(x + 70, 30);
            bClose.rectTransform.anchoredPosition = new Vector2(x + 300 > 760 ? x - 430 : x + 300, 30);
        }
    }
}
