using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Menus du jeu : arbre de compétences (nœuds reliés par paliers de niveau, fiche détaillée, barre des 5
    /// compétences équipées), menu pause (reprendre, options, changer de personnage, quitter) et options (qualité graphique,
    /// volume, taille de l'interface, chiffres de dégâts, distance de la caméra), habillés comme la maquette.</summary>
    public partial class HUD
    {
        // ========================================================================================== habillage commun
        RectTransform OrnatePanel(RectTransform parent, string name, Vector2 size, string title, string subtitle = null)
        {
            var im = Img(parent, name, UIStyle.Ornate(), new Vector2(0.5f, 0.5f), Vector2.zero, size); im.type = Image.Type.Sliced; im.raycastTarget = true;
            var p = im.rectTransform;
            var ban = Img(p, "Bandeau", UIStyle.Banner(), new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(Mathf.Min(760, size.x - 200), 78)); ban.type = Image.Type.Sliced;
            var t = Label(ban.rectTransform, "Titre", title, 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(ban.rectTransform.sizeDelta.x - 80, 70));
            t.color = UIStyle.Gold; t.fontStyle = FontStyle.Bold; t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            if (!string.IsNullOrEmpty(subtitle))
                Label(p, "Sous-titre", subtitle, 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(size.x - 200, 34)).color = UIStyle.Parch;
            return p;
        }

        Image TextButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick, out Text text, int fontSize = 30)
        {
            var bg = Img(parent, name, UIStyle.Slot(), anchor, pos, size); bg.type = Image.Type.Sliced; bg.raycastTarget = true;
            text = Label(bg.rectTransform, "Texte", label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16, 6));
            text.color = UIStyle.Gold;
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => { Sound.UI("sfx_ui_clic", 0.5f); onClick(); });
            var cb = b.colors; cb.highlightedColor = new Color(1f, 0.92f, 0.75f); cb.pressedColor = new Color(1f, 0.8f, 0.5f); b.colors = cb;
            return bg;
        }

        Image Line(RectTransform parent, Vector2 a, Vector2 b, float width)
        {
            var im = Img(parent, "Branche", UIStyle.Bar(), new Vector2(0, 1), Vector2.zero, Vector2.one);
            var rt = im.rectTransform; rt.pivot = new Vector2(0.5f, 0.5f);
            var d = b - a; rt.anchoredPosition = (a + b) / 2f; rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return im;
        }

        void CloseAllPanels()
        {
            if (bagPanel) bagPanel.SetActive(false);
            if (treePanel) treePanel.SetActive(false);
            if (shopPanel) shopPanel.SetActive(false);
        }

        /// <summary>Ouvre un panneau pour les captures du test automatique (« arbre », « sac », « menu », « options », "" = tout fermer).</summary>
        public void TestShow(string which)
        {
            CloseAllPanels(); SetPause(false);
            switch (which)
            {
                case "arbre": ToggleTree(); if (runner) { treeSel = runner.loadout[0]; RefreshTree(); } break;
                case "sac": ToggleBag(); break;
                case "menu": SetPause(true); break;
                case "options": SetPause(true); ShowOptions(true); break;
            }
        }

        // ========================================================================================== arbre de compétences
        GameObject treePanel; Text treePoints, treeDetail, treeLearnText; Image treeLearn; int treeSel = -1;
        readonly Dictionary<int, (Image node, Image glow, Image icon, Image lockIcon, Text rank, Text name)> treeNodes = new Dictionary<int, (Image, Image, Image, Image, Text, Text)>();
        readonly List<(int child, Image line)> treeLines = new List<(int, Image)>();
        readonly List<(Image bg, Image icon, Text num)> treeSlots = new List<(Image, Image, Text)>();

        const float ColW = 214, NodeSize = 96, TreeH = 600;

        void BuildTree(RectTransform root)
        {
            var tree = SkillBook.TreeOf(runner.classId);
            var panel = OrnatePanel(root, "Compétences", new Vector2(1780, 1000), "Arbre de compétences", $"{player.name.Replace("(Clone)", "")} — {Loot.ClassLabel(runner.classId)}");
            treePanel = panel.gameObject;
            var close = RoundButton(panel, "Fermer", "close", new Vector2(1, 1), new Vector2(-30, -26), 84, ToggleTree, out _, out _);
            treePoints = Label(panel, "Points", "", 28, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(60, -60), new Vector2(560, 40));

            // ---- vue défilante de l'arbre
            var view = Img(panel, "Vue", UIStyle.Slot(), new Vector2(0, 1), new Vector2(46, -108), new Vector2(1180, TreeH + 40)); view.type = Image.Type.Sliced; view.raycastTarget = true;
            view.color = new Color(0.75f, 0.7f, 0.65f, 1f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Contenu", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.rectTransform, false); content.anchorMin = content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(30 + tree.columns.Count * ColW, TreeH + 40); content.anchoredPosition = Vector2.zero;

            Vector2 NodePos(int i)
            {
                int c = tree.column[i], n = tree.columns[c].Count; float sp = n <= 1 ? 0 : Mathf.Min(110f, (TreeH - 160) / (float)(n - 1));
                float y = 60 + (TreeH - 80) / 2f + (tree.row[i] - (n - 1) / 2f) * sp;
                return new Vector2(30 + c * ColW + ColW / 2f, -y);
            }
            // en-têtes de paliers
            for (int c = 0; c < tree.columns.Count; c++)
            {
                var h = Label(content, "Palier" + c, $"Niveau {SkillBook.TierLevels[c]}", 22, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(30 + c * ColW + 12, -14), new Vector2(ColW - 24, 30));
                h.color = UIStyle.Gold;
                if (c > 0) { var sep = Img(content, "Séparation", UIStyle.Bar(), new Vector2(0, 1), new Vector2(30 + c * ColW - 1, -50), new Vector2(2, TreeH - 30)); sep.color = new Color(0.55f, 0.4f, 0.22f, 0.35f); }
            }
            // branches (sous les nœuds)
            for (int i = 0; i < runner.book.Count; i++)
            {
                int pa = tree.parent[i]; if (pa < 0) continue;
                var a = NodePos(pa) + new Vector2(NodeSize / 2f, 0); var b = NodePos(i) - new Vector2(NodeSize / 2f, 0);
                var mid = new Vector2((a.x + b.x) / 2f, a.y);
                // tracé en coude : horizontal, diagonal, horizontal
                var l1 = Line(content, a, mid, 7); var l2 = Line(content, mid, new Vector2(mid.x + 1, b.y), 7); var l3 = Line(content, new Vector2(mid.x, b.y), b, 7);
                treeLines.Add((i, l1)); treeLines.Add((i, l2)); treeLines.Add((i, l3));
            }
            // nœuds
            for (int i = 0; i < runner.book.Count; i++)
            {
                int k = i; var d = runner.book[i]; var p = NodePos(i);
                var glow = Img(content, "Halo_" + i, UIStyle.Glow(), new Vector2(0, 1), p, new Vector2(NodeSize * 1.7f, NodeSize * 1.7f)); glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var node = Img(content, d.name, UIStyle.Button(), new Vector2(0, 1), p, new Vector2(NodeSize, NodeSize)); node.rectTransform.pivot = new Vector2(0.5f, 0.5f); node.raycastTarget = true;
                var nodeMask = Img(node.rectTransform, "Masque", UIStyle.Disc(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize * 0.84f, NodeSize * 0.84f));
                nodeMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var icon = Img(nodeMask.rectTransform, "Icône", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize * 0.6f, NodeSize * 0.6f));
                SetSkillIcon(icon, d, 0.7f, NodeSize * 0.6f, NodeSize * 0.84f);
                var lk = Img(node.rectTransform, "Verrou", UIStyle.Icon("lock", new Color(0.75f, 0.68f, 0.6f)), new Vector2(1, 0), new Vector2(6, -6), new Vector2(34, 34));
                var rank = Label(node.rectTransform, "Rang", "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(70, 26));
                rank.gameObject.AddComponent<Outline>().effectColor = Color.black;
                var name = Label(content, "Nom_" + i, d.name, 18, TextAnchor.UpperCenter, new Vector2(0, 1), p + new Vector2(-ColW / 2f + 8, -NodeSize / 2f - 6), new Vector2(ColW - 16, 44));
                name.rectTransform.pivot = new Vector2(0, 1);
                var b = node.gameObject.AddComponent<Button>(); b.targetGraphic = node;
                b.onClick.AddListener(() => { treeSel = k; Sound.UI("sfx_ui_clic", 0.45f); RefreshTree(); });
                treeNodes[i] = (node, glow, icon, lk, rank, name);
            }
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = view.rectTransform; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40; scroll.inertia = true;

            // ---- fiche de la compétence choisie
            var side = Img(panel, "Fiche", UIStyle.Slot(), new Vector2(1, 1), new Vector2(-46, -108), new Vector2(460, TreeH + 40)); side.type = Image.Type.Sliced;
            treeDetail = Label(side.rectTransform, "Texte", "", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -20), new Vector2(412, TreeH - 110));
            FitText(treeDetail, 14);
            treeLearn = TextButton(side.rectTransform, "Apprendre", "Apprendre", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(400, 76), LearnSelected, out treeLearnText, 30);

            // ---- compétences équipées (barre du bas)
            Label(panel, "Équipées", "Compétences équipées", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(-230, 168), new Vector2(560, 34)).color = UIStyle.Gold;
            Label(panel, "Aide", "Choisissez une compétence apprise, puis touchez un emplacement pour l'y placer.", 21, TextAnchor.MiddleLeft, new Vector2(0.5f, 0), new Vector2(330, 82), new Vector2(520, 70)).color = new Color(0.85f, 0.78f, 0.66f);
            for (int s = 0; s < runner.loadout.Length; s++)
            {
                int slot = s;
                var bg = Img(panel, "Emplacement" + s, UIStyle.Slot(), new Vector2(0.5f, 0), new Vector2(-470 + s * 120, 40), new Vector2(110, 110)); bg.type = Image.Type.Sliced; bg.raycastTarget = true;
                var ic = Img(bg.rectTransform, "Icône", null, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(76, 76));
                var num = Label(bg.rectTransform, "N", (s + 1).ToString(), 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(100, 104));
                num.color = UIStyle.Gold; num.gameObject.AddComponent<Outline>().effectColor = Color.black;
                var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => PickTreeSlot(slot));
                treeSlots.Add((bg, ic, num));
            }
            treeSel = runner.loadout[0];
            treePanel.SetActive(false);
        }

        void ToggleTree()
        {
            if (!treePanel) { Notify("Arbre de compétences indisponible"); return; }
            if (bagPanel) bagPanel.SetActive(false);
            treePanel.SetActive(!treePanel.activeSelf); if (treePanel.activeSelf) RefreshTree();
            Sound.UI(treePanel.activeSelf ? "sfx_ui_ouvrir" : "sfx_ui_fermer", 0.7f);
        }

        void LearnSelected()
        {
            if (treeSel < 0) return; var d = runner.book[treeSel];
            if (runner.Learn(treeSel)) { Notify($"{d.name} : rang {runner.ranks[treeSel]}"); Sound.UI("sfx_soin", 0.6f); }
            else if (!runner.Unlocked(treeSel)) Notify($"Niveau {d.unlockLevel} requis");
            else if (!runner.BranchOpen(treeSel)) Notify($"Apprenez d'abord « {runner.book[runner.ParentOf(treeSel)].name} »");
            else if (runner.ranks[treeSel] >= d.MaxRank) Notify("Rang maximum");
            else Notify("Aucun point de compétence");
            RefreshTree();
        }

        void PickTreeSlot(int slot)
        {
            if (treeSel >= 0 && runner.ranks[treeSel] > 0 && runner.loadout[slot] != treeSel)
            {
                int was = System.Array.IndexOf(runner.loadout, treeSel);
                if (was >= 0) runner.loadout[was] = runner.loadout[slot];     // échange si elle était déjà sur la barre
                runner.Equip(slot, treeSel); Notify($"{runner.book[treeSel].name} → emplacement {slot + 1}");
            }
            else treeSel = runner.loadout[slot];
            RefreshTree();
        }

        void RefreshTree()
        {
            if (!runner || !treePanel) return;
            var tree = SkillBook.TreeOf(runner.classId);
            treePoints.text = $"Niveau {player.level}   ·   Points à dépenser : <color=#FFD27A><b>{player.skillPoints}</b></color>";
            foreach (var kv in treeNodes)
            {
                int i = kv.Key; var (node, glow, icon, lk, rank, name) = kv.Value; var d = runner.book[i];
                bool open = runner.Unlocked(i) && runner.BranchOpen(i); int r = runner.ranks[i];
                var col = Vfx.Of(d.element).outer; col.a = 1;
                node.color = r > 0 ? Color.white : open ? new Color(0.78f, 0.72f, 0.66f) : new Color(0.38f, 0.34f, 0.32f);
                icon.color = r > 0 ? Color.white : open ? new Color(0.8f, 0.8f, 0.8f, 0.9f) : new Color(0.35f, 0.35f, 0.35f, 0.8f);
                lk.enabled = !open && r == 0;
                bool sel = i == treeSel, equipped = System.Array.IndexOf(runner.loadout, i) >= 0;
                glow.color = sel ? new Color(1f, 0.85f, 0.45f, 0.9f) : r > 0 ? new Color(col.r, col.g, col.b, 0.45f) : new Color(0, 0, 0, 0);
                rank.text = r > 0 ? $"{r}/{d.MaxRank}" : open ? "0/5" : $"Nv {d.unlockLevel}";
                rank.color = r > 0 ? UIStyle.Gold : new Color(0.75f, 0.7f, 0.62f);
                name.text = equipped ? $"<color=#FFD27A>{d.name} ★</color>" : d.name;
                name.color = r > 0 ? UIStyle.Parch : new Color(0.7f, 0.64f, 0.56f);
            }
            foreach (var (child, line) in treeLines)
            {
                bool lit = runner.ranks[child] > 0;
                line.color = lit ? new Color(1f, 0.78f, 0.38f, 1f) : runner.BranchOpen(child) ? new Color(0.62f, 0.45f, 0.25f, 0.9f) : new Color(0.25f, 0.18f, 0.12f, 0.9f);
            }
            for (int s = 0; s < treeSlots.Count; s++)
            {
                var (bg, ic, num) = treeSlots[s]; var d = runner.book[runner.loadout[s]];
                SetSkillIcon(ic, d, 0.7f, 76, 90); ic.color = runner.ranks[runner.loadout[s]] > 0 ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                bg.color = runner.loadout[s] == treeSel ? new Color(1f, 0.85f, 0.55f) : Color.white;
            }
            // fiche
            if (treeSel < 0) { treeDetail.text = "<color=#a08c6c>Touchez une compétence de l'arbre.</color>"; treeLearn.gameObject.SetActive(false); return; }
            var sd = runner.book[treeSel]; int rk = runner.ranks[treeSel];
            var eqp = player.GetComponent<Equipment>(); float echo = 0; var md = eqp ? eqp.Modify(sd, out echo) : sd;
            var ec = Vfx.Of(sd.element).outer;
            string txt = $"<size=34><b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(ec, Color.white, 0.4f))}>{sd.name}</color></b></size>\n";
            txt += $"<color=#c8b89a>{ElementText.Label(md.element)} · niveau {sd.unlockLevel} · rang {rk}/{sd.MaxRank}</color>\n\n";
            txt += sd.description + "\n\n";
            float mult = md.mult * (1 + 0.15f * (Mathf.Max(1, rk) - 1));
            if (sd.kind != SkillKind.Shield && sd.kind != SkillKind.Heal && sd.kind != SkillKind.Buff) txt += $"Dégâts : <b>{mult * 100:0} %</b> de l'arme\n";
            txt += $"Recharge : <b>{md.cooldown * (1 - 0.04f * (Mathf.Max(1, rk) - 1)):0.#} s</b>   Mana : <b>{sd.mana:0}</b>\n";
            if (rk < sd.MaxRank) txt += $"<color=#9fd7ff>Rang suivant : dégâts +15 %, recharge −4 %</color>\n";
            int pa = runner.ParentOf(treeSel);
            if (pa >= 0 && runner.ranks[pa] == 0 && rk == 0) txt += $"\n<color=#ff8a70>Branche : apprenez d'abord « {runner.book[pa].name} ».</color>\n";
            if (!runner.Unlocked(treeSel)) txt += $"\n<color=#ff8a70>Débloquée au niveau {sd.unlockLevel}.</color>\n";
            if (md.empowered)
            {
                txt += "\n<color=#ff9a3c><b>Transformée par votre équipement :</b></color>";
                if (eqp) foreach (var (pw, s) in eqp.ActivePowers()) if (pw.Matches(sd)) txt += $"\n<color=#ff9a3c>◆ {pw.name}</color>";
                if (echo > 0) txt += $"\n<color=#ffd27a>Écho spectral : {echo * 100:0} %</color>";
            }
            treeDetail.text = txt;
            bool canLearn = runner.Unlocked(treeSel) && runner.BranchOpen(treeSel) && rk < sd.MaxRank;
            treeLearn.gameObject.SetActive(true);
            treeLearnText.text = rk >= sd.MaxRank ? "Rang maximum" : rk == 0 ? "Apprendre (1 point)" : "Améliorer (1 point)";
            treeLearn.color = canLearn && player.skillPoints > 0 ? Color.white : new Color(0.5f, 0.46f, 0.42f);
        }

        // ========================================================================================== menu pause et options
        GameObject pausePanel, optionsPanel, pauseShade;
        readonly List<System.Action> optionRefreshers = new List<System.Action>();
        public static bool ShowDamageNumbers = true;
        public bool Paused => pausePanel && pausePanel.activeSelf;

        Text difficultyText;

        /// <summary>Passe à la difficulté suivante débloquée ; les camps se repeuplent aussitôt avec elle.</summary>
        void CycleDifficulty()
        {
            int lvl = player ? player.level : 1, next = Difficulty.Next(lvl);
            if (next == Difficulty.Current)
            {   // rien d'autre de débloqué : on indique le prochain palier
                int d = Mathf.Min(Difficulty.Count - 1, Difficulty.Current + 1);
                Notify(d > Difficulty.Current ? $"{Difficulty.Names[d]} : niveau {Difficulty.UnlockLevel[d]} requis" : "Difficulté maximale");
                return;
            }
            Difficulty.Current = next; RefreshDifficulty();
            FindFirstObjectByType<WorldPopulation>()?.Refresh();
            Notify($"Difficulté : {Difficulty.Name}");
        }

        void RefreshDifficulty()
        {
            if (difficultyText) difficultyText.text = $"Difficulté : <color=#{ColorUtility.ToHtmlStringRGB(Difficulty.ColorOf(Difficulty.Current))}>{Difficulty.Name}</color>";
            if (zoneText) zoneText.text = Difficulty.Current > 0 ? $"{zoneName}\n<size=20><color=#{ColorUtility.ToHtmlStringRGB(Difficulty.ColorOf(Difficulty.Current))}>{Difficulty.Name}</color></size>" : zoneName;
        }

        void BuildMenu(RectTransform root)
        {
            RoundButton(root, "Menu", "menu", new Vector2(0.5f, 1), new Vector2(180, -24), 96, () => SetPause(!Paused), out _, out _);
            var shade = Img(root, "Voile", UIStyle.Disc(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000)); shade.sprite = null; shade.color = new Color(0, 0, 0, 0.62f); shade.raycastTarget = true;
            pauseShade = shade.gameObject;

            var p = OrnatePanel(root, "Pause", new Vector2(760, 880), "Pause", zoneName); pausePanel = p.gameObject;
            float y = -150;
            TextButton(p, "Reprendre", "Reprendre", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 92), () => SetPause(false), out _, 34); y -= 118;
            TextButton(p, "Difficulte", "", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 92), CycleDifficulty, out difficultyText, 30); y -= 118;
            RefreshDifficulty();
            TextButton(p, "Options", "Options", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 92), () => ShowOptions(true), out _, 34); y -= 118;
            TextButton(p, "Personnages", "Changer de personnage", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 92), BackToSelection, out _, 34); y -= 118;
            TextButton(p, "Quitter", "Quitter le jeu", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 92), QuitGame, out _, 34);
            Label(p, "Sauvegarde", "Votre progression est sauvegardée automatiquement.", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(620, 30)).color = new Color(0.8f, 0.72f, 0.6f);

            var o = OrnatePanel(root, "Options", new Vector2(1060, 980), "Options"); optionsPanel = o.gameObject;
            float oy = -120;
            var cfg = FindFirstObjectByType<GameBootstrap>()?.config;
            OptionRow(o, ref oy, "Qualité graphique", new[] { "Haute", "Mobile" },
                () => GraphicsQuality.Current == GraphicsQuality.Tier.Haute ? 0 : 1,
                k => GraphicsQuality.Apply(cfg, k == 0 ? GraphicsQuality.Tier.Haute : GraphicsQuality.Tier.Mobile, true));
            OptionRow(o, ref oy, "Musique", new[] { "Muet", "25 %", "50 %", "75 %", "100 %" },
                () => Mathf.RoundToInt(Sound.Music * 4),
                k => { Sound.Music = k / 4f; });
            OptionRow(o, ref oy, "Effets sonores", new[] { "Muet", "25 %", "50 %", "75 %", "100 %" },
                () => Mathf.RoundToInt(Sound.Effects * 4),
                k => { Sound.Effects = k / 4f; Sound.UI("sfx_epee_coup"); });
            OptionRow(o, ref oy, "Taille de l'interface", new[] { "Petite", "Normale", "Grande" },
                () => PlayerPrefs.GetInt("korvalune.ui", 1),
                k => { PlayerPrefs.SetInt("korvalune.ui", k); ApplyUiScale(); });
            OptionRow(o, ref oy, "Distance de la caméra", new[] { "Proche", "Normale", "Lointaine" },
                () => PlayerPrefs.GetInt("korvalune.zoom", 1),
                k => { PlayerPrefs.SetInt("korvalune.zoom", k); ApplyCameraZoom(); });
            OptionRow(o, ref oy, "Chiffres de dégâts", new[] { "Affichés", "Masqués" },
                () => ShowDamageNumbers ? 0 : 1,
                k => { ShowDamageNumbers = k == 0; PlayerPrefs.SetInt("korvalune.degats", k); });
            TextButton(o, "Retour", "Retour", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 84), () => ShowOptions(false), out _, 32);

            pausePanel.SetActive(false); optionsPanel.SetActive(false); pauseShade.SetActive(false);
            // réglages mémorisés
            AudioListener.volume = 1f;
            ShowDamageNumbers = PlayerPrefs.GetInt("korvalune.degats", 0) == 0;
            ApplySettings();
        }

        void Update()
        {
            // bouton « retour » d'Android / Échap : ferme le panneau ouvert, sinon ouvre ou ferme la pause
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (optionsPanel && optionsPanel.activeSelf) ShowOptions(false);
            else if (Paused) SetPause(false);
            else if ((treePanel && treePanel.activeSelf) || (bagPanel && bagPanel.activeSelf) || (shopPanel && shopPanel.activeSelf)) { CloseAllPanels(); FindFirstObjectByType<GameBootstrap>()?.Persist(); }
            else SetPause(true);
        }

        void OptionRow(RectTransform parent, ref float y, string title, string[] choices, System.Func<int> current, System.Action<int> pick)
        {
            Label(parent, title, title, 30, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(70, y), new Vector2(340, 80)).color = UIStyle.Parch;
            float w = Mathf.Min(170, 600f / choices.Length - 10);
            var buttons = new List<(Image bg, Text t)>();
            for (int k = 0; k < choices.Length; k++)
            {
                int c = k;
                var bg = TextButton(parent, title + k, choices[k], new Vector2(0, 1), new Vector2(400 + k * (w + 10), y - 6), new Vector2(w, 70), () => { pick(c); RefreshOptions(); }, out var t, 24);
                buttons.Add((bg, t));
            }
            optionRefreshers.Add(() =>
            {
                int cur = current();
                for (int k = 0; k < buttons.Count; k++)
                {
                    bool on = k == cur;
                    buttons[k].bg.color = on ? new Color(1f, 0.82f, 0.45f) : new Color(0.62f, 0.56f, 0.5f);
                    buttons[k].t.color = on ? Color.white : UIStyle.Gold; buttons[k].t.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                }
            });
            y -= 112;
        }

        void RefreshOptions() { foreach (var r in optionRefreshers) r(); }

        void SetPause(bool on)
        {
            if (!pausePanel) return;
            if (on) CloseAllPanels();
            if (on != pausePanel.activeSelf) Sound.UI(on ? "sfx_ui_ouvrir" : "sfx_ui_fermer", 0.7f);
            pausePanel.SetActive(on); pauseShade.SetActive(on);
            if (!on && optionsPanel) optionsPanel.SetActive(false);
            Time.timeScale = on ? 0f : 1f;
            if (on) FindFirstObjectByType<GameBootstrap>()?.Persist();
        }

        void ShowOptions(bool on)
        {
            // fenêtre d'options complète (onglets Graphismes, Son, Jeu, Interface) ; la pause réapparaît à la fermeture
            optionsPanel.SetActive(false);
            if (!on) { pausePanel.SetActive(Paused); return; }
            pausePanel.SetActive(false);
            OptionsMenu.Open(font, () => { if (pausePanel) pausePanel.SetActive(Paused); ApplySettings(); });
        }

        void BackToSelection()
        {
            FindFirstObjectByType<GameBootstrap>()?.Persist();
            Time.timeScale = 1f;
            SceneManager.LoadScene("Korvalune_Selection");
        }

        void QuitGame()
        {
            FindFirstObjectByType<GameBootstrap>()?.Persist();
            Time.timeScale = 1f;
            Application.Quit();
        }

        void ApplyUiScale()
        {
            var sc = Canvas ? Canvas.GetComponent<CanvasScaler>() : null; if (!sc) return;
            int k = PlayerPrefs.GetInt("korvalune.ui", 1);
            sc.referenceResolution = k == 0 ? new Vector2(2200, 1238) : k == 2 ? new Vector2(1680, 945) : new Vector2(1920, 1080);
        }

        void ApplyCameraZoom()
        {
            var rig = worldCam ? worldCam.GetComponent<CameraRig>() : null; if (!rig) return;
            int k = PlayerPrefs.GetInt("korvalune.zoom", 1);
            rig.zoom = k == 0 ? 0.75f : k == 2 ? 1.12f : 0.95f;
        }
    }
}
