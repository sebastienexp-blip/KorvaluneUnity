using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Interface de jeu mobile (d'après la maquette « Mobile ARPG Concept ») : portrait et barres de vie / mana /
    /// expérience en haut à gauche, mini-carte ronde et quête actuelle en haut à droite, joystick en bas à gauche,
    /// grand bouton d'attaque et quatre compétences en arc en bas à droite (recharge circulaire), notifications,
    /// chiffres de dégâts, barres de vie des ennemis, écran de mort.</summary>
    public class HUD : MonoBehaviour
    {
        public PlayerController player;
        public SkillRunner runner;
        public Quest quest;
        public Camera worldCam;
        public string zoneName = "Vallée de Korvalune";
        public VirtualJoystick Joystick { get; private set; }
        public Canvas Canvas { get; private set; }

        Font font;
        Image hpFill, manaFill, xpFill; Text hpText, levelText, questText, notifyText, zoneText;
        readonly List<(Image cd, Text t, int idx)> skillButtons = new List<(Image, Text, int)>();
        readonly List<Image> skillIcons = new List<Image>(); readonly List<string> shownIcon = new List<string>();
        GameObject treePanel; Text treePoints; int pickSlot = -1;
        readonly List<(SkillDef d, int i, Text title, Text rank, Image row, Button learn)> treeRows = new List<(SkillDef, int, Text, Text, Image, Button)>();
        RectTransform arrow; Camera mapCam; RenderTexture mapRT; GameObject deathPanel;
        float notifyUntil;
        readonly List<(RectTransform rt, Text t, Vector3 world, float born)> popups = new List<(RectTransform, Text, Vector3, float)>();
        readonly Dictionary<Health, (RectTransform root, Image fill)> bars = new Dictionary<Health, (RectTransform, Image)>();
        RectTransform root;

        public void Build(Camera uiCam)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            Canvas = go.GetComponent<Canvas>();
            if (uiCam) { Canvas.renderMode = RenderMode.ScreenSpaceCamera; Canvas.worldCamera = uiCam; Canvas.planeDistance = 1f; }
            else Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            root = (RectTransform)go.transform;
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ---- portrait et barres (haut gauche)
            var pf = Panel(root, "Portrait", new Vector2(0, 1), new Vector2(30, -30), new Vector2(560, 170));
            var por = Img(pf, "Médaillon", UIStyle.Button(), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(136, 136));
            Img((RectTransform)por.transform, "Emblème", UIStyle.Icon("attack", UIStyle.Gold), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
            levelText = Label(pf, "Niveau", "Nv 1", 26, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(64, 14), new Vector2(70, 34));
            Label(pf, "Nom", player ? player.name.Replace("(Clone)", "").Replace("_M", "").Replace("_F", "") : "Héros", 28, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(170, -16), new Vector2(370, 36));
            hpFill = BarRow(pf, "Vie", new Color(0.78f, 0.12f, 0.10f), new Vector2(170, -60), out hpText);
            manaFill = BarRow(pf, "Mana", new Color(0.18f, 0.38f, 0.90f), new Vector2(170, -98), out _);
            xpFill = BarRow(pf, "XP", new Color(0.95f, 0.75f, 0.25f), new Vector2(170, -132), out _, 12);

            // ---- mini-carte et quête (haut droite)
            var mm = new GameObject("MiniCarte", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            Place(mm, root, new Vector2(1, 1), new Vector2(-40, -36), new Vector2(300, 300)); mm.GetComponent<Image>().sprite = UIStyle.Disc(); mm.GetComponent<Mask>().showMaskGraphic = false;
            mapRT = new RenderTexture(512, 512, 16) { name = "MiniCarte" };
            var raw = new GameObject("Carte", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); Place((RectTransform)raw.transform, mm, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300)); raw.texture = mapRT;
            var ringImg = Img(root, "Cadre", UIStyle.Ring(), new Vector2(1, 1), new Vector2(-22, -18), new Vector2(336, 336));
            arrow = Img(mm, "Joueur", UIStyle.Icon("leap", Color.white), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)).rectTransform;
            arrow.GetComponent<Image>().sprite = Arrow();
            zoneText = Label(root, "Zone", zoneName, 24, TextAnchor.MiddleCenter, new Vector2(1, 1), new Vector2(-40, -352), new Vector2(300, 32));
            var qp = Panel(root, "Quête", new Vector2(1, 1), new Vector2(-30, -400), new Vector2(380, 150));
            Label(qp, "Titre", "Quête actuelle :", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -16), new Vector2(340, 30)).color = UIStyle.Gold;
            questText = Label(qp, "Texte", "", 22, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -48), new Vector2(340, 90));

            // ---- joystick (bas gauche)
            var jb = Img(root, "Joystick", UIStyle.Ring(), new Vector2(0, 0), new Vector2(70, 60), new Vector2(300, 300));
            Img((RectTransform)jb.transform, "Fond", UIStyle.Disc(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260)).color = new Color(0.1f, 0.06f, 0.04f, 0.45f);
            var knob = Img((RectTransform)jb.transform, "Pouce", UIStyle.Button(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130));
            Joystick = jb.gameObject.AddComponent<VirtualJoystick>(); Joystick.knob = knob.rectTransform; Joystick.radius = 105;

            // ---- attaque et compétences (bas droite)
            var atk = RoundButton(root, "Attaque", "attack", new Vector2(1, 0), new Vector2(-70, 60), 230, () => player?.Attack(), out _, out _);
            var pos = new[] { new Vector2(-330, 70), new Vector2(-310, 230), new Vector2(-200, 345), new Vector2(-45, 345), new Vector2(-490, 60) };
            int nb = runner ? runner.loadout.Length : 4;
            for (int i = 0; i < nb; i++)
            {
                int k = i;
                var bg = RoundButton(root, "Compétence" + i, runner ? IconOf(runner.book[runner.loadout[i]]) : "stomp", new Vector2(1, 0), pos[i], 140,
                    () => { if (runner) runner.Use(k); else player?.UseSkill(k); }, out var cd, out var t);
                skillIcons.Add(bg.transform.Find("Icône").GetComponent<Image>()); shownIcon.Add("");
                skillButtons.Add((cd, t, i));
            }
            // menus (inventaire, compétences) — coins du haut
            RoundButton(root, "Inventaire", "bag", new Vector2(0.5f, 1), new Vector2(-60, -24), 96, ToggleBag, out _, out _);
            if (player && player.GetComponent<Equipment>()) BuildBag(root);
            RoundButton(root, "Arbre", "book", new Vector2(0.5f, 1), new Vector2(60, -24), 96, ToggleTree, out _, out _);
            if (runner) BuildTree(root);

            notifyText = Label(root, "Notification", "", 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1200, 60));
            notifyText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // ---- écran de mort
            var dp = Panel(root, "Mort", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 260)); deathPanel = dp.gameObject;
            Label(dp, "Titre", "Vous êtes tombé", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(560, 60)).color = UIStyle.Gold;
            RoundButton(dp, "Revivre", "warcry", new Vector2(0.5f, 0), new Vector2(0, 30), 110, Revive, out _, out _);
            deathPanel.SetActive(false);

            // ---- caméra de la mini-carte
            var mc = new GameObject("Caméra mini-carte"); mapCam = mc.AddComponent<Camera>();
            mapCam.orthographic = true; mapCam.orthographicSize = 38; mapCam.targetTexture = mapRT; mapCam.clearFlags = CameraClearFlags.SolidColor; mapCam.backgroundColor = new Color(0.12f, 0.16f, 0.10f);
            mapCam.transform.rotation = Quaternion.Euler(90, 0, 0); mapCam.cullingMask = ~(1 << 5); mapCam.depth = -5;

            if (player) player.Notify = Notify;
            if (quest) quest.Notify = Notify;
            Health.AnyDamaged += OnDamaged;
        }

        void OnDestroy() { Health.AnyDamaged -= OnDamaged; }

        Sprite arrowSprite;
        Sprite Arrow()
        {
            if (arrowSprite) return arrowSprite;
            int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, false); var px = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x - 32f) / 32f, v = (y - 32f) / 32f;
                bool inTri = v < 0.9f && v > -0.8f && Mathf.Abs(u) < (0.9f - v) * 0.45f && !(v < -0.3f && Mathf.Abs(u) < (-0.3f - v) * 0.9f);
                px[y * S + x] = inTri ? new Color(1f, 0.85f, 0.3f, 1) : new Color(0, 0, 0, 0);
            }
            t.SetPixels(px); t.Apply();
            return arrowSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
        }

        // ------------------------------------------------------------------------------------------ mise à jour
        void LateUpdate() { Refresh(); }

        /// <summary>Met à jour barres, recharges, quête, mini-carte (appelé chaque image ; appelable hors jeu pour les captures).</summary>
        public void Refresh()
        {
            if (!player || !player.Health) return;
            var h = player.Health;
            hpFill.fillAmount = h.hp / h.maxHp; hpText.text = $"{Mathf.CeilToInt(h.hp)} / {Mathf.CeilToInt(h.maxHp)}";
            manaFill.fillAmount = h.maxMana > 0 ? h.mana / h.maxMana : 0;
            xpFill.fillAmount = player.xp / player.xpNext; levelText.text = "Nv " + player.level;
            if (runner) RefreshSkills(h);
            else foreach (var (cd, t, i) in skillButtons)
            {
                var s = player.skills[i];
                float rem = s.Remaining;
                cd.fillAmount = rem > 0 ? rem / s.cooldown : 0;
                t.text = rem > 0 ? Mathf.CeilToInt(rem).ToString() : "";
                cd.color = h.mana < s.mana && rem <= 0 ? new Color(0.1f, 0.15f, 0.5f, 0.55f) : new Color(0, 0, 0, 0.6f);
                if (h.mana < s.mana && rem <= 0) cd.fillAmount = 1;
            }
            if (quest) questText.text = quest.Describe();
            if (mapCam) { var p = player.transform.position; mapCam.transform.position = new Vector3(p.x, p.y + 120, p.z); }
            if (arrow) arrow.localRotation = Quaternion.Euler(0, 0, -player.transform.eulerAngles.y);
            if (notifyText && Time.time > notifyUntil) notifyText.color = Color.Lerp(notifyText.color, new Color(1, 1, 1, 0), 4 * Time.deltaTime);
            deathPanel.SetActive(h.Dead);
            UpdatePopups(); UpdateBars(); UpdateLootLabels();
        }

        // ------------------------------------------------------------------------------------------ compétences
        public static string IconOf(SkillDef d)
        {
            switch (d.kind)
            {
                case SkillKind.Whirl: case SkillKind.Cleave: return "whirlwind";
                case SkillKind.Leap: case SkillKind.Dash: case SkillKind.Teleport: case SkillKind.Evade: return "leap";
                case SkillKind.Buff: case SkillKind.Shield: case SkillKind.Heal: case SkillKind.Summon: case SkillKind.Curse: return "warcry";
                case SkillKind.Nova: case SkillKind.GroundAoE: case SkillKind.Zone: case SkillKind.Rain: case SkillKind.Totem: case SkillKind.Trap: return "stomp";
                default: return "attack";
            }
        }

        void RefreshSkills(Health h)
        {
            for (int k = 0; k < skillButtons.Count; k++)
            {
                var (cd, t, i) = skillButtons[k];
                int si = runner.loadout[i]; var d = runner.book[si];
                string key = d.id + runner.ranks[si];
                if (shownIcon[k] != key)
                {
                    shownIcon[k] = key;
                    var col = Vfx.Of(d.element).outer; col.a = 1;
                    skillIcons[k].sprite = UIStyle.Icon(IconOf(d), Color.Lerp(UIStyle.Gold, col, 0.65f));
                    skillIcons[k].color = runner.ranks[si] > 0 ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1);
                }
                float rem = runner.Remaining(si), full = d.cooldown;
                cd.fillAmount = rem > 0 ? Mathf.Clamp01(rem / full) : 0;
                t.text = rem > 0 ? Mathf.CeilToInt(rem).ToString() : "";
                bool noMana = h.mana < d.mana && rem <= 0;
                cd.color = noMana ? new Color(0.1f, 0.15f, 0.5f, 0.55f) : new Color(0, 0, 0, 0.6f);
                if (noMana || runner.ranks[si] <= 0) cd.fillAmount = 1;
            }
            if (treePanel && treePanel.activeSelf) RefreshTree();
            if (bagPanel && bagPanel.activeSelf && Time.frameCount % 15 == 0) RefreshBag();
        }

        // ------------------------------------------------------------------------------------------ inventaire et équipement
        GameObject bagPanel; Text bagStats, bagTip; ItemSave selected; bool selectedWorn;
        Button bEquip, bDrop; Text bEquipText;
        readonly List<(Image frame, Image glow, Text label)> bagCells = new List<(Image, Image, Text)>();
        readonly Dictionary<Slot, (Image frame, Image glow, Text label)> slotCells = new Dictionary<Slot, (Image, Image, Text)>();
        static readonly Dictionary<Slot, string> SlotName = new Dictionary<Slot, string>
        { {Slot.Arme,"Arme"},{Slot.MainGauche,"Main gauche"},{Slot.Casque,"Casque"},{Slot.Torse,"Torse"},{Slot.Gants,"Gants"},{Slot.Jambes,"Jambes"},{Slot.Bottes,"Bottes"},{Slot.Amulette,"Amulette"},{Slot.Anneau,"Anneau"} };

        public void ToggleBag()
        {
            if (!bagPanel) { Notify("Inventaire indisponible"); return; }
            if (treePanel) treePanel.SetActive(false);
            bagPanel.SetActive(!bagPanel.activeSelf); selected = null; if (bagPanel.activeSelf) RefreshBag();
        }

        (Image, Image, Text) Cell(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, System.Action onClick)
        {
            var glow = Img(parent, name + "_aura", UIStyle.Glow(), anchor, pos, new Vector2(size * 1.5f, size * 1.5f)); glow.color = new Color(0, 0, 0, 0);
            var frame = Img(parent, name, UIStyle.Bar(), anchor, pos, new Vector2(size, size)); frame.type = Image.Type.Sliced; frame.raycastTarget = true;
            var icon = Img(frame.rectTransform, "Icone", null, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(size * 0.84f, size * 0.84f)); icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
            var label = Label(frame.rectTransform, "Texte", "", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 8, size - 8));
            var b = frame.gameObject.AddComponent<Button>(); b.targetGraphic = frame; b.onClick.AddListener(() => onClick());
            return (frame, glow, label);
        }

        static void Paint((Image frame, Image glow, Text label) c, ItemSave it, string empty)
        {
            var icon = c.frame.transform.Find("Icone")?.GetComponent<Image>();
            if (it == null)
            {
                c.frame.color = new Color(0.12f, 0.09f, 0.07f, 0.9f); c.glow.color = new Color(0, 0, 0, 0); c.label.alignment = TextAnchor.MiddleCenter; c.label.text = $"<color=#6b5a48>{empty}</color>";
                if (icon) icon.enabled = false; return;
            }
            var spr = ItemIcons.Get(it.baseName);
            if (icon) { icon.sprite = spr; icon.enabled = spr; }
            var col = Loot.ColorOf(it.rarity);
            c.frame.color = Color.Lerp(new Color(0.10f, 0.07f, 0.05f), col, 0.35f);
            c.glow.color = it.rarity >= Rarity.Legendaire ? new Color(col.r, col.g, col.b, 0.55f) : new Color(0, 0, 0, 0);   // aura comme au sol
            if (spr) { c.label.alignment = TextAnchor.LowerRight; c.label.text = $"<size=17><color=#{ColorUtility.ToHtmlStringRGB(col)}><b>{it.power}</b></color></size>"; }
            else { c.label.alignment = TextAnchor.MiddleCenter; c.label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(col)}><b>{it.baseName}</b></color>\n<size=15>{SlotName[it.slot]} · {it.power}</size>"; }
        }

        /// <summary>Panneau d'inventaire : équipement porté (9 emplacements) à gauche, sac de 40 objets à droite, caractéristiques et
        /// fiche de l'objet choisi (couleur de rareté comme au sol, comparaison avec l'objet porté) ; équiper, retirer, jeter.</summary>
        void BuildBag(RectTransform root)
        {
            var eq = player.GetComponent<Equipment>();
            var panel = Panel(root, "Inventaire", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 900)); bagPanel = panel.gameObject;
            Label(panel, "Titre", "Équipement et inventaire", 40, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -24), new Vector2(900, 56)).color = UIStyle.Gold;
            RoundButton(panel, "Fermer", "attack", new Vector2(1, 1), new Vector2(-24, -20), 80, ToggleBag, out _, out _);
            // équipement : disposition autour d'une silhouette
            var doll = Img(panel, "Silhouette", UIStyle.Bar(), new Vector2(0, 1), new Vector2(40, -100), new Vector2(560, 620)); doll.type = Image.Type.Sliced; doll.color = new Color(0, 0, 0, 0.35f);
            var pos = new Dictionary<Slot, Vector2>
            {
                { Slot.Casque, new Vector2(220, -20) }, { Slot.Amulette, new Vector2(370, -40) }, { Slot.Torse, new Vector2(220, -170) },
                { Slot.Gants, new Vector2(60, -190) }, { Slot.Anneau, new Vector2(380, -190) }, { Slot.Arme, new Vector2(60, -340) },
                { Slot.MainGauche, new Vector2(380, -340) }, { Slot.Jambes, new Vector2(220, -320) }, { Slot.Bottes, new Vector2(220, -470) },
            };
            foreach (var kv in pos)
            {
                var slot = kv.Key;
                slotCells[slot] = Cell(doll.rectTransform, "Porté_" + slot, new Vector2(0, 1), kv.Value, 120, () => { selected = eq.In(slot); selectedWorn = true; RefreshBag(); });
            }
            bagStats = Label(panel, "Caractéristiques", "", 24, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(40, 40), new Vector2(560, 120));
            // sac : 8 × 5
            for (int i = 0; i < Equipment.BagSize; i++)
            {
                int k = i;
                var c = Cell(panel, "Sac_" + i, new Vector2(0, 1), new Vector2(640 + (i % 8) * 116, -100 - (i / 8) * 116), 106, () =>
                { selected = k < player.inventory.Count ? player.inventory[k] : null; selectedWorn = false; RefreshBag(); });
                bagCells.Add(c);
            }
            // fiche de l'objet
            var tip = Img(panel, "Fiche", UIStyle.Bar(), new Vector2(0, 0), new Vector2(640, 30), new Vector2(940, 200)); tip.type = Image.Type.Sliced; tip.color = new Color(0.06f, 0.045f, 0.035f, 0.95f);
            bagTip = Label(tip.rectTransform, "Texte", "", 22, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(18, -10), new Vector2(640, 185));
            Button Btn(string name, Vector2 p, System.Action a, out Text t)
            {
                var im = Img(tip.rectTransform, name, UIStyle.Button(), new Vector2(1, 1), p, new Vector2(220, 70)); im.raycastTarget = true;
                t = Label(im.rectTransform, "T", name, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 70)); t.color = UIStyle.Gold;
                var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => a()); return b;
            }
            bEquip = Btn("Équiper", new Vector2(-20, -20), () =>
            {
                if (selected == null) return;
                if (selectedWorn) eq.Unequip(selected.slot); else eq.Equip(selected);
                selected = null; RefreshBag();
            }, out bEquipText);
            bDrop = Btn("Jeter", new Vector2(-20, -105), () => { if (selected != null && !selectedWorn) { eq.Discard(selected); selected = null; RefreshBag(); } }, out _);
            bagPanel.SetActive(false);
        }

        void RefreshBag()
        {
            var eq = player ? player.GetComponent<Equipment>() : null; if (!eq) return;
            foreach (var kv in slotCells) Paint(kv.Value, eq.In(kv.Key), SlotName[kv.Key]);
            for (int i = 0; i < bagCells.Count; i++) Paint(bagCells[i], i < player.inventory.Count ? player.inventory[i] : null, "");
            bagStats.text = eq.Summary() + $"\n<size=20>Sac : {player.inventory.Count} / {Equipment.BagSize}</size>";
            if (selected == null) { bagTip.text = "<color=#a08c6c>Touchez un objet pour voir sa fiche.</color>"; bEquip.gameObject.SetActive(false); bDrop.gameObject.SetActive(false); return; }
            var txt = Loot.Tooltip(selected);
            var cur = eq.In(selected.slot);
            if (!selectedWorn && cur != null)
            {
                float d = selected.power - cur.power;
                txt += $"\n<color=#{(d >= 0 ? "7CFC7C" : "FF7070")}>{(d >= 0 ? "▲ +" : "▼ ")}{d:0} puissance par rapport à « {cur.name} »</color>";
            }
            bagTip.text = txt;
            bEquip.gameObject.SetActive(true); bEquipText.text = selectedWorn ? "Retirer" : "Équiper";
            bDrop.gameObject.SetActive(!selectedWorn);
        }

        void ToggleTree()
        {
            if (!treePanel) { Notify("Arbre de compétences indisponible"); return; }
            if (bagPanel) bagPanel.SetActive(false);
            treePanel.SetActive(!treePanel.activeSelf); pickSlot = -1; if (treePanel.activeSelf) RefreshTree();
        }

        /// <summary>Panneau des compétences : les 32 compétences de la classe (niveau requis, rang 0 à 5), apprendre avec les
        /// points gagnés à chaque niveau, et placer une compétence sur l'un des 5 emplacements de la barre.</summary>
        void BuildTree(RectTransform root)
        {
            var panel = Panel(root, "Compétences", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 860)); treePanel = panel.gameObject;
            Label(panel, "Titre", "Compétences — " + runner.classId, 40, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -24), new Vector2(800, 56)).color = UIStyle.Gold;
            treePoints = Label(panel, "Points", "", 30, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(-130, -28), new Vector2(420, 50));
            RoundButton(panel, "Fermer", "attack", new Vector2(1, 1), new Vector2(-24, -20), 80, ToggleTree, out _, out _);
            Label(panel, "Aide", "Touchez un emplacement (1 à 5) puis une compétence pour l'équiper. « + » dépense un point.", 22, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -78), new Vector2(1100, 30));
            // défilement
            var view = Img(panel, "Vue", UIStyle.Bar(), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1120, 720)); view.color = new Color(0, 0, 0, 0.25f); view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Contenu", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.rectTransform, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            var order = new List<int>(); for (int i = 0; i < runner.book.Count; i++) order.Add(i);
            order.Sort((a, b) => runner.book[a].unlockLevel.CompareTo(runner.book[b].unlockLevel));
            float rowH = 92; content.sizeDelta = new Vector2(0, rowH * order.Count + 10); content.anchoredPosition = Vector2.zero;
            for (int r = 0; r < order.Count; r++)
            {
                int i = order[r]; var d = runner.book[i];
                var row = Img(content, d.name, UIStyle.Bar(), new Vector2(0.5f, 1), new Vector2(0, -6 - r * rowH), new Vector2(1090, rowH - 8)); row.type = Image.Type.Sliced; row.raycastTarget = true;
                var col = Vfx.Of(d.element).outer; col.a = 1;
                Img(row.rectTransform, "Icône", UIStyle.Icon(IconOf(d), Color.Lerp(UIStyle.Gold, col, 0.65f)), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(70, 70));
                var title = Label(row.rectTransform, "Nom", "", 28, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(100, -6), new Vector2(760, 36));
                Label(row.rectTransform, "Description", d.description, 20, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(100, -44), new Vector2(760, 40)).color = new Color(0.85f, 0.8f, 0.7f);
                var rank = Label(row.rectTransform, "Rang", "", 28, TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(110, 60));
                var lb = Img(row.rectTransform, "Apprendre", UIStyle.Button(), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(76, 76)); lb.raycastTarget = true;
                Label(lb.rectTransform, "+", "+", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76)).color = UIStyle.Gold;
                var learn = lb.gameObject.AddComponent<Button>(); learn.targetGraphic = lb;
                learn.onClick.AddListener(() => { if (runner.Learn(i)) Notify($"{d.name} : rang {runner.ranks[i]}"); else if (!runner.Unlocked(i)) Notify($"Niveau {d.unlockLevel} requis"); else if (runner.ranks[i] >= d.MaxRank) Notify("Rang maximum"); else Notify("Aucun point de compétence"); RefreshTree(); });
                var pick = row.gameObject.AddComponent<Button>(); pick.targetGraphic = row;
                pick.onClick.AddListener(() =>
                {
                    if (pickSlot < 0) { Notify("Choisissez d'abord un emplacement (1 à 5)"); return; }
                    if (runner.ranks[i] <= 0) { Notify("Apprenez d'abord cette compétence"); return; }
                    runner.Equip(pickSlot, i); Notify($"{d.name} → emplacement {pickSlot + 1}"); pickSlot = -1; RefreshTree();
                });
                treeRows.Add((d, i, title, rank, row, learn));
            }
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = view.rectTransform; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            // emplacements de la barre
            for (int k = 0; k < runner.loadout.Length; k++)
            {
                int slot = k;
                var sb = Img(panel, "Emplacement" + k, UIStyle.Button(), new Vector2(0, 1), new Vector2(560 + k * 104, -14), new Vector2(64, 64)); sb.raycastTarget = true;
                Label(sb.rectTransform, "N", (k + 1).ToString(), 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)).color = UIStyle.Gold;
                var b = sb.gameObject.AddComponent<Button>(); b.targetGraphic = sb;
                b.onClick.AddListener(() => { pickSlot = slot; Notify($"Emplacement {slot + 1} : touchez une compétence apprise"); });
            }
            treePanel.SetActive(false);
        }

        void RefreshTree()
        {
            if (!runner) return;
            treePoints.text = $"Niveau {player.level} — points : <color=#FFD27A>{player.skillPoints}</color>";
            foreach (var (d, i, title, rank, row, learn) in treeRows)
            {
                bool open = runner.Unlocked(i); int r = runner.ranks[i];
                int slot = System.Array.IndexOf(runner.loadout, i);
                var col = Vfx.Of(d.element).outer;
                title.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(col, Color.white, 0.35f))}>{d.name}</color>  <size=20>Nv {d.unlockLevel} · {d.element} · {d.cooldown:0.#} s · {d.mana:0} mana</size>" + (slot >= 0 ? $"  <color=#FFD27A>[{slot + 1}]</color>" : "");
                rank.text = open ? $"{r}/{d.MaxRank}" : "—";
                row.color = !open ? new Color(0.25f, 0.22f, 0.2f, 0.9f) : (pickSlot >= 0 && r > 0 ? new Color(0.45f, 0.35f, 0.15f, 0.95f) : new Color(0.16f, 0.10f, 0.07f, 0.95f));
                learn.interactable = open && r < d.MaxRank && player.skillPoints > 0;
            }
        }

        public void Notify(string msg) { if (!notifyText) return; notifyText.text = msg; notifyText.color = UIStyle.Parch; notifyUntil = Time.time + 2.5f; }

        void Revive()
        {
            var b = FindFirstObjectByType<GameBootstrap>();
            if (b) b.RespawnPlayer();
        }

        void OnDamaged(Health h, float amount, bool crit)
        {
            if (!worldCam) return;
            var t = Label(root, "Dégâts", Mathf.RoundToInt(amount).ToString() + (crit ? " !" : ""), crit ? 44 : 32, TextAnchor.MiddleCenter, new Vector2(0, 0), Vector2.zero, new Vector2(200, 50));
            t.color = h.faction == Faction.Player ? new Color(1f, 0.3f, 0.25f) : crit ? new Color(1f, 0.85f, 0.3f) : Color.white;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            popups.Add((t.rectTransform, t, h.transform.position + Vector3.up * (2.0f + Random.Range(0f, 0.4f)) + Random.insideUnitSphere * 0.3f, Time.time));
        }

        void UpdatePopups()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (rt, t, w, born) = popups[i];
                float age = Time.time - born;
                if (age > 1.0f || !rt) { if (rt) Destroy(rt.gameObject); popups.RemoveAt(i); continue; }
                Screen2Canvas(w + Vector3.up * age * 1.2f, rt);
                var c = t.color; c.a = 1 - age; t.color = c;
            }
        }

        void UpdateBars()
        {
            foreach (var h in Health.All)
            {
                if (h.faction != Faction.Enemy) continue;
                bool show = !h.Dead && h.hp < h.maxHp && player && (h.transform.position - player.transform.position).sqrMagnitude < 40 * 40;
                if (!bars.TryGetValue(h, out var b))
                {
                    if (!show) continue;
                    var bg = Img(root, "BarreEnnemi", UIStyle.Bar(), new Vector2(0, 0), Vector2.zero, new Vector2(110, 14)); bg.color = new Color(0.1f, 0.05f, 0.04f, 0.85f); bg.type = Image.Type.Sliced;
                    var f = Img((RectTransform)bg.transform, "Remplissage", UIStyle.Bar(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 9)); f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.color = new Color(0.85f, 0.15f, 0.1f);
                    b = (bg.rectTransform, f); bars[h] = b;
                }
                b.root.gameObject.SetActive(show);
                if (show) { b.fill.fillAmount = h.hp / h.maxHp; Screen2Canvas(h.transform.position + Vector3.up * (h.GetComponent<CharacterController>() ? h.GetComponent<CharacterController>().height + 0.4f : 2.2f), b.root); }
            }
            var dead = new List<Health>();
            foreach (var kv in bars) if (kv.Key == null) { if (kv.Value.root) Destroy(kv.Value.root.gameObject); dead.Add(kv.Key); }
            foreach (var d in dead) bars.Remove(d);
        }

        readonly Dictionary<LootDrop, Text> lootLabels = new Dictionary<LootDrop, Text>();
        /// <summary>Noms des objets au sol, à la couleur de leur rareté (comme dans Diablo 4).</summary>
        void UpdateLootLabels()
        {
            foreach (var d in LootDrop.All)
            {
                if (!d) continue;
                if (!lootLabels.TryGetValue(d, out var t))
                {
                    t = Label(root, "NomObjet", d.item.name, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(360, 30));
                    t.color = Loot.ColorOf(d.item.rarity); t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
                    lootLabels[d] = t;
                }
                Screen2Canvas(d.transform.position + Vector3.up * 0.9f, t.rectTransform);
            }
            var gone = new List<LootDrop>();
            foreach (var kv in lootLabels) if (!kv.Key) { if (kv.Value) Destroy(kv.Value.gameObject); gone.Add(kv.Key); }
            foreach (var g in gone) lootLabels.Remove(g);
        }

        void Screen2Canvas(Vector3 world, RectTransform rt)
        {
            var sp = worldCam.WorldToScreenPoint(world);
            if (sp.z < 0) { rt.anchoredPosition = new Vector2(-9999, -9999); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera, out var lp);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = lp;
        }

        // ------------------------------------------------------------------------------------------ fabrique
        static void Place(RectTransform rt, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        Image Img(RectTransform parent, string name, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(im.rectTransform, parent, anchor, pos, size); im.sprite = s; im.raycastTarget = false; return im;
        }

        RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = Img(parent, name, UIStyle.Panel(), anchor, pos, size); im.type = Image.Type.Sliced; im.color = new Color(1, 1, 1, 0.94f); return im.rectTransform;
        }

        Text Label(RectTransform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Place(t.rectTransform, parent, anchor, pos, box);
            t.font = font; t.fontSize = size; t.alignment = align; t.text = text; t.color = UIStyle.Parch; t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Image BarRow(RectTransform parent, string name, Color c, Vector2 pos, out Text label, float h = 26)
        {
            var bg = Img(parent, name, UIStyle.Bar(), new Vector2(0, 1), pos, new Vector2(360, h)); bg.type = Image.Type.Sliced; bg.color = new Color(0.10f, 0.05f, 0.04f, 0.9f);
            var f = Img(bg.rectTransform, "Remplissage", UIStyle.Bar(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(352, h - 6)); f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.color = c;
            label = Label(bg.rectTransform, "Valeur", "", Mathf.RoundToInt(h * 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(352, h));
            return f;
        }

        Image RoundButton(RectTransform parent, string name, string icon, Vector2 anchor, Vector2 pos, float size, System.Action onClick, out Image cooldown, out Text cdText)
        {
            var bg = Img(parent, name, UIStyle.Button(), anchor, pos, new Vector2(size, size)); bg.raycastTarget = true;
            Img(bg.rectTransform, "Icône", UIStyle.Icon(icon, UIStyle.Gold), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            cooldown = Img(bg.rectTransform, "Recharge", UIStyle.Disc(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
            cooldown.type = Image.Type.Filled; cooldown.fillMethod = Image.FillMethod.Radial360; cooldown.fillOrigin = 2; cooldown.fillClockwise = false; cooldown.fillAmount = 0; cooldown.color = new Color(0, 0, 0, 0.6f);
            cdText = Label(bg.rectTransform, "Temps", "", Mathf.RoundToInt(size * 0.3f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => onClick());
            var cb = b.colors; cb.pressedColor = new Color(1f, 0.85f, 0.6f); b.colors = cb;
            return bg;
        }
    }
}
