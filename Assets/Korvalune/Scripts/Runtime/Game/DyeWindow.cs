using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Fenêtre du teinturier : on choisit l'objet à teindre (arme, bouclier ou armure), puis une couleur unie,
    /// un mélange de deux couleurs ou l'arc-en-ciel, et on paie la teinture en or. Retirer une teinture coûte aussi.</summary>
    public class DyeWindow : MonoBehaviour
    {
        public const long Price = 300;
        public static DyeWindow Current;

        PlayerController player; Font font;
        int target = 0;          // 0 arme, 1 bouclier, 2 armure
        int mode = 0;            // 0 unie, 1 mélange, 2 arc-en-ciel, 3 retirer
        int colA = 0, colB = 1;  // indices dans Dye.Palette
        bool pickB;              // en mélange : les pastilles choisissent la couleur 2 au lieu de la couleur 1
        Text preview, price, message, toggleText;
        readonly List<Image> targetBg = new List<Image>(), modeBg = new List<Image>();
        readonly List<Outline> swatchOutline = new List<Outline>();
        Button toggleBtn;

        static readonly string[] TargetNames = { "Arme", "Bouclier", "Armure" };
        static readonly string[] ModeNames = { "Unie", "Mélange", "Arc-en-ciel", "Retirer" };

        public static DyeWindow Open(PlayerController p)
        {
            if (Current) Current.Close();
            var go = new GameObject("Teinturier", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 310;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var w = go.AddComponent<DyeWindow>(); w.player = p; w.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            w.Build(); Current = w; w.Refresh();
            Sound.UI("sfx_ui_ouvrir", 0.7f);
            return w;
        }

        public void Close()
        {
            Sound.UI("sfx_ui_fermer", 0.7f);
            if (Current == this) Current = null;
            Destroy(gameObject);
            FindFirstObjectByType<GameBootstrap>()?.Persist();
        }

        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }

        // ---------------------------------------------------------------- logique
        static string Hex(int i) => Dye.Palette[Mathf.Clamp(i, 0, Dye.Palette.Length - 1)];

        string CurrentDye()
        {
            switch (mode)
            {
                case 0: return Hex(colA);
                case 1: return Hex(colA) + "|" + Hex(colB);
                case 2: return Dye.Rainbow;
                default: return "";
            }
        }

        string DescribeChoice()
        {
            switch (mode)
            {
                case 0: return "Couleur unie " + Hex(colA);
                case 1: return "Mélange " + Hex(colA) + " + " + Hex(colB);
                case 2: return "Arc-en-ciel";
                default: return "Retirer la teinture";
            }
        }

        void Apply()
        {
            if (!player) return;
            string dye = CurrentDye();
            if (target == 2)
            {
                var outfit = player.GetComponent<HeroOutfit>();
                if (!outfit) { Say("Armure introuvable."); return; }
                if (player.wallet.gold < Price) { Say("Il te faut " + Price + " or."); return; }
                player.AddGold(-Price);
                outfit.ArmorDye = dye;
                outfit.DyeArmor();
            }
            else
            {
                var eq = player.GetComponent<Equipment>();
                var it = eq ? eq.In(target == 0 ? Slot.Arme : Slot.MainGauche) : null;
                if (it == null) { Say("Rien à teindre ici : équipe " + (target == 0 ? "une arme." : "un bouclier.")); return; }
                if (player.wallet.gold < Price) { Say("Il te faut " + Price + " or."); return; }
                player.AddGold(-Price);
                it.dye = dye;
                eq.Recompute();                                   // relance l'affichage de l'arme et du bouclier
            }
            FindFirstObjectByType<GameBootstrap>()?.Persist();
            Say("Teinture appliquée : " + TargetNames[target].ToLower() + ", " + DescribeChoice().ToLower() + ".");
            Refresh();
        }

        void Say(string s) { if (message) message.text = s; }

        void Refresh()
        {
            for (int i = 0; i < targetBg.Count; i++) targetBg[i].color = i == target ? new Color(1f, 0.82f, 0.45f) : new Color(0.35f, 0.3f, 0.25f);
            for (int i = 0; i < modeBg.Count; i++) modeBg[i].color = i == mode ? new Color(1f, 0.82f, 0.45f) : new Color(0.35f, 0.3f, 0.25f);
            for (int i = 0; i < swatchOutline.Count; i++)                 // couleur 1 entourée ; en mélange, la couleur 2 aussi
                swatchOutline[i].enabled = i == colA || (mode == 1 && i == colB);
            if (toggleBtn) toggleBtn.gameObject.SetActive(mode == 1);
            if (toggleText) toggleText.text = "Choisir : couleur " + (pickB ? "2" : "1");
            if (preview) preview.text = "Aperçu : " + TargetNames[target] + " — " + DescribeChoice();
            if (price) price.text = "Coût : " + Price + " or   (or : " + (player ? player.wallet.gold.ToString() : "0") + ")";
        }

        void PickSwatch(int i)
        {
            if (mode == 1) { if (pickB) colB = i; else colA = i; }
            else colA = i;
            Refresh();
        }

        // ---------------------------------------------------------------- interface
        void Build()
        {
            var root = (RectTransform)transform;
            var shade = Img(root, "Voile", null); shade.color = new Color(0, 0, 0, 0.6f);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one; shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero; shade.raycastTarget = true;

            var panel = Img(root, "Fenêtre", UIStyle.Ornate()); panel.type = Image.Type.Sliced;
            var p = panel.rectTransform; p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f); p.sizeDelta = new Vector2(1100, 760); p.anchoredPosition = Vector2.zero;

            var title = Lbl(p, "Titre", "Teinturier", 46, TextAnchor.MiddleCenter, new Vector2(550, -60), new Vector2(600, 70));
            title.color = UIStyle.Gold; title.fontStyle = FontStyle.Bold;

            // colonne gauche : objet à teindre
            Lbl(p, "ObjetT", "Objet à teindre", 30, TextAnchor.MiddleCenter, new Vector2(190, -140), new Vector2(320, 40)).color = UIStyle.Parch;
            for (int i = 0; i < TargetNames.Length; i++)
            {
                int k = i;
                var b = Btn(p, TargetNames[i], new Vector2(190, -200 - i * 70), new Vector2(300, 60), () => { target = k; Refresh(); }, 28, out var bg);
                targetBg.Add(bg);
            }

            // colonne droite : couleurs, mode, aperçu, achat
            Lbl(p, "CouleurT", "Couleur", 30, TextAnchor.MiddleCenter, new Vector2(700, -140), new Vector2(400, 40)).color = UIStyle.Parch;
            for (int i = 0; i < Dye.Palette.Length; i++)
            {
                int k = i; int row = i / 8, col = i % 8;
                var go = new GameObject("Pastille" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform; rt.SetParent(p, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(700 + (col - 3.5f) * 82, -200 - row * 82); rt.sizeDelta = new Vector2(70, 70);
                var img = go.GetComponent<Image>(); ColorUtility.TryParseHtmlString(Dye.Palette[i], out var c); img.color = c;
                var ol = go.AddComponent<Outline>(); ol.effectColor = new Color(1f, 0.85f, 0.4f); ol.effectDistance = new Vector2(4, -4); ol.enabled = false;
                go.GetComponent<Button>().onClick.AddListener(() => PickSwatch(k));
                swatchOutline.Add(ol);
            }

            Lbl(p, "ModeT", "Mode", 30, TextAnchor.MiddleCenter, new Vector2(700, -340), new Vector2(400, 40)).color = UIStyle.Parch;
            for (int i = 0; i < ModeNames.Length; i++)
            {
                int k = i;
                var b = Btn(p, ModeNames[i], new Vector2(400 + i * 200, -390), new Vector2(180, 60), () => { mode = k; Refresh(); }, 24, out var bg);
                modeBg.Add(bg);
            }

            toggleBtn = Btn(p, "Bascule", new Vector2(700, -450), new Vector2(360, 56), () => { pickB = !pickB; Refresh(); }, 26, out _);
            toggleText = toggleBtn.GetComponentInChildren<Text>();

            preview = Lbl(p, "Aperçu", "", 28, TextAnchor.MiddleCenter, new Vector2(700, -520), new Vector2(760, 50)); preview.color = UIStyle.Parch;
            price = Lbl(p, "Prix", "", 28, TextAnchor.MiddleCenter, new Vector2(700, -570), new Vector2(760, 50)); price.color = UIStyle.Gold;
            message = Lbl(p, "Message", "", 26, TextAnchor.MiddleCenter, new Vector2(550, -620), new Vector2(960, 50)); message.color = Color.white;

            Btn(p, "Appliquer", new Vector2(700, -680), new Vector2(320, 64), Apply, 30, out _);
            Btn(p, "Fermer", new Vector2(190, -680), new Vector2(300, 64), Close, 30, out _);
        }

        Image Img(RectTransform parent, string name, Sprite sp)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rt = im.rectTransform; rt.SetParent(parent, false); im.sprite = sp; im.raycastTarget = false;
            return im;
        }

        Text Lbl(RectTransform parent, string name, string text, int size, TextAnchor align, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rt = t.rectTransform; rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            t.font = font; t.fontSize = size; t.alignment = align; t.text = text; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            return t;
        }

        Button Btn(RectTransform parent, string text, Vector2 pos, Vector2 size, System.Action onClick, int fontSize, out Image bg)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            bg = go.GetComponent<Image>(); bg.color = new Color(0.35f, 0.3f, 0.25f);
            var b = go.GetComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => onClick());
            var lbl = Lbl(rt, "Texte", text, fontSize, TextAnchor.MiddleCenter, Vector2.zero, size);
            lbl.rectTransform.anchorMin = Vector2.zero; lbl.rectTransform.anchorMax = Vector2.one; lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;
            lbl.color = Color.white;
            return b;
        }
    }
}
