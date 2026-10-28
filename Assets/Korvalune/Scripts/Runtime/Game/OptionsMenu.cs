using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Réglages du joueur (mémorisés sur l'appareil) et leur application à la scène en cours.</summary>
    public static class Settings
    {
        public static event System.Action Changed;
        static int Get(string k, int d) => PlayerPrefs.GetInt("korvalune." + k, d);
        static void Set(string k, int v) { PlayerPrefs.SetInt("korvalune." + k, v); PlayerPrefs.Save(); Apply(); Changed?.Invoke(); }

        // graphismes
        public static int Fps { get => Get("fps", GraphicsQuality.Current == GraphicsQuality.Tier.Mobile ? 0 : 1); set => Set("fps", value); }          // 30 / 60
        public static int Shadows { get => Get("ombres", 0); set => Set("ombres", value); }                // activées / désactivées
        public static int PostFx { get => Get("postfx", 0); set => Set("postfx", value); }                 // complets / réduits / désactivés
        public static int Brightness { get => Get("lumi", 1); set => Set("lumi", value); }                 // sombre / normale / claire / très claire
        public static int ViewDistance { get => Get("vue", 1); set => Set("vue", value); }                 // courte / normale / longue
        // son
        public static int Ambience { get => Get("ambiance", 3); set => Set("ambiance", value); }           // 0..4 (×25 %)
        public static int UiSound { get => Get("son_ui", 3); set => Set("son_ui", value); }
        public static int Muted { get => Get("muet", 0); set => Set("muet", value); }
        // jeu
        public static int AutoAim { get => Get("visee", 0); set => Set("visee", value); }                  // activée / désactivée
        public static int AutoLoot { get => Get("ramasse", 0); set => Set("ramasse", value); }             // non / communs / magiques / rares
        public static int EnemyNames { get => Get("noms", 0); set => Set("noms", value); }                 // tous / cible / masqués
        public static int Shake { get => Get("secousses", 0); set => Set("secousses", value); }            // activées / désactivées
        public static int Vibration { get => Get("vibrations", 0); set => Set("vibrations", value); }
        // interface
        public static int Minimap { get => Get("carte", 0); set => Set("carte", value); }                  // affichée / masquée
        public static int FpsCounter { get => Get("compteur", 1); set => Set("compteur", value); }         // affiché / masqué
        public static int NameSize { get => Get("taille_noms", 1); set => Set("taille_noms", value); }     // petite / normale / grande
        public static int QuestPanel { get => Get("quete", 0); set => Set("quete", value); }               // affichée / masquée

        public static float AmbienceVolume => Muted == 1 ? 0 : Ambience / 4f;
        public static float UiVolume => Muted == 1 ? 0 : UiSound / 4f;

        static readonly Dictionary<Light, float> sunBase = new Dictionary<Light, float>();
        static string ambScene; static Color ambSky, ambEq, ambGround; static float ambInt;

        /// <summary>Applique tout ce qui touche la scène : images/s, ombres, post-traitement, luminosité, distance de vue, son.</summary>
        public static void Apply()
        {
            Application.targetFrameRate = Fps == 0 ? 30 : 60;
            AudioListener.volume = Muted == 1 ? 0f : 1f;
            var cam = Camera.main;
            if (cam)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data) { data.renderShadows = Shadows == 0; data.renderPostProcessing = PostFx < 2; }
                cam.farClipPlane = ViewDistance == 0 ? 260 : ViewDistance == 2 ? 900 : 520;
            }
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) v.weight = PostFx == 0 ? (GraphicsQuality.Current == GraphicsQuality.Tier.Mobile ? 0.6f : 1f) : PostFx == 1 ? 0.45f : 0f;
            var t = Terrain.activeTerrain;
            if (t) { t.treeDistance = ViewDistance == 0 ? 160 : ViewDistance == 2 ? 600 : 360; t.detailObjectDistance = ViewDistance == 0 ? 20 : ViewDistance == 2 ? 70 : 45; }
            // luminosité : on part des valeurs d'origine de la scène (mémorisées une fois)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (ambScene != scene) { ambScene = scene; ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor; ambInt = RenderSettings.ambientIntensity; sunBase.Clear(); }
            float k = Brightness == 0 ? 0.8f : Brightness == 2 ? 1.25f : Brightness == 3 ? 1.5f : 1f;
            RenderSettings.ambientSkyColor = ambSky * k; RenderSettings.ambientEquatorColor = ambEq * k; RenderSettings.ambientGroundColor = ambGround * k; RenderSettings.ambientIntensity = ambInt * k;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (!sunBase.ContainsKey(l)) sunBase[l] = l.intensity;
                l.intensity = sunBase[l] * Mathf.Lerp(1f, k, 0.6f);
                l.shadows = Shadows == 0 ? LightShadows.Soft : LightShadows.None;
            }
        }
    }

    /// <summary>Fenêtre d'options à onglets (Graphismes, Son, Jeu, Interface), ouverte depuis le menu principal ou la pause.
    /// Construite à la volée sur son propre canevas, au-dessus de tout le reste.</summary>
    public class OptionsMenu : MonoBehaviour
    {
        public static OptionsMenu Current;
        System.Action onClose; Font font;
        readonly List<System.Action> refreshers = new List<System.Action>();
        readonly List<(GameObject page, Image tab, Text label)> pages = new List<(GameObject, Image, Text)>();
        RectTransform panel;

        public static OptionsMenu Open(Font font, System.Action onClose = null)
        {
            if (Current) { Current.onClose = onClose; return Current; }
            var go = new GameObject("Options", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 300;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()) new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            var m = go.AddComponent<OptionsMenu>(); m.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); m.onClose = onClose; m.Build();
            Current = m; Sound.UI("sfx_ui_ouvrir", 0.7f);
            return m;
        }

        public void Close() { Sound.UI("sfx_ui_fermer", 0.7f); Current = null; var cb = onClose; Destroy(gameObject); cb?.Invoke(); }

        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }

        // ------------------------------------------------------------------------------------------ construction
        void Build()
        {
            var root = (RectTransform)transform;
            var shade = Img(root, "Voile", null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); shade.color = new Color(0, 0, 0, 0.72f);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one; shade.raycastTarget = true;
            var p = Img(root, "Fenêtre", UIStyle.Ornate(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 940)); p.type = Image.Type.Sliced; panel = p.rectTransform;
            var title = Lbl(panel, "Titre", "Options", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 70)); title.color = UIStyle.Gold; title.fontStyle = FontStyle.Bold;
            Btn(panel, "Fermer", "Fermer", new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(340, 80), Close, 32);

            string[] tabs = { "Graphismes", "Son", "Jeu", "Interface" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                var page = new GameObject(tabs[i], typeof(RectTransform)).GetComponent<RectTransform>();
                page.SetParent(panel, false); page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one; page.offsetMin = new Vector2(60, 140); page.offsetMax = new Vector2(-60, -200);
                var tb = Btn(panel, "Onglet" + i, tabs[i], new Vector2(0.5f, 1), new Vector2(-480 + i * 320, -140), new Vector2(300, 72), () => ShowPage(k), 30);
                pages.Add((page.gameObject, tb, tb.GetComponentInChildren<Text>()));
            }
            var cfg = FindFirstObjectByType<GameBootstrap>()?.config;
            string[] pct = { "Muet", "25 %", "50 %", "75 %", "100 %" };

            float y = 0; var g = (RectTransform)pages[0].page.transform;
            Row(g, ref y, "Qualité", new[] { "Haute", "Mobile" }, () => GraphicsQuality.Current == GraphicsQuality.Tier.Haute ? 0 : 1,
                k => { if (cfg) GraphicsQuality.Apply(cfg, k == 0 ? GraphicsQuality.Tier.Haute : GraphicsQuality.Tier.Mobile, true); else PlayerPrefs.SetInt("korvalune.qualite", k == 0 ? 1 : 0); Settings.Apply(); });
            Row(g, ref y, "Images par seconde", new[] { "30", "60" }, () => Settings.Fps, k => Settings.Fps = k);
            Row(g, ref y, "Ombres", new[] { "Activées", "Désactivées" }, () => Settings.Shadows, k => Settings.Shadows = k);
            Row(g, ref y, "Effets de lumière", new[] { "Complets", "Réduits", "Aucun" }, () => Settings.PostFx, k => Settings.PostFx = k);
            Row(g, ref y, "Luminosité", new[] { "Sombre", "Normale", "Claire", "Très claire" }, () => Settings.Brightness, k => Settings.Brightness = k);
            Row(g, ref y, "Distance de vue", new[] { "Courte", "Normale", "Longue" }, () => Settings.ViewDistance, k => Settings.ViewDistance = k);

            y = 0; var s = (RectTransform)pages[1].page.transform;
            Row(s, ref y, "Musique", pct, () => Mathf.RoundToInt(Sound.Music * 4), k => { Sound.Music = k / 4f; Changed(); });
            Row(s, ref y, "Effets sonores", pct, () => Mathf.RoundToInt(Sound.Effects * 4), k => { Sound.Effects = k / 4f; Sound.UI("sfx_epee_coup"); Changed(); });
            Row(s, ref y, "Ambiance", pct, () => Settings.Ambience, k => Settings.Ambience = k);
            Row(s, ref y, "Interface", pct, () => Settings.UiSound, k => { Settings.UiSound = k; Sound.UI("sfx_ui_clic"); });
            Row(s, ref y, "Tout le son", new[] { "Activé", "Coupé" }, () => Settings.Muted, k => Settings.Muted = k);

            y = 0; var j = (RectTransform)pages[2].page.transform;
            Row(j, ref y, "Visée automatique", new[] { "Activée", "Désactivée" }, () => Settings.AutoAim, k => Settings.AutoAim = k);
            Row(j, ref y, "Ramassage auto", new[] { "Aucun", "Communs", "Magiques", "Rares" }, () => Settings.AutoLoot, k => Settings.AutoLoot = k);
            Row(j, ref y, "Distance caméra", new[] { "Proche", "Normale", "Lointaine" }, () => PlayerPrefs.GetInt("korvalune.zoom", 1), k => { PlayerPrefs.SetInt("korvalune.zoom", k); Changed(); });
            Row(j, ref y, "Secousses d'écran", new[] { "Activées", "Désactivées" }, () => Settings.Shake, k => Settings.Shake = k);
            Row(j, ref y, "Vibrations", new[] { "Activées", "Désactivées" }, () => Settings.Vibration, k => { Settings.Vibration = k; if (k == 0) Vibrate(); });

            y = 0; var u = (RectTransform)pages[3].page.transform;
            Row(u, ref y, "Taille de l'interface", new[] { "Petite", "Normale", "Grande" }, () => PlayerPrefs.GetInt("korvalune.ui", 1), k => { PlayerPrefs.SetInt("korvalune.ui", k); Changed(); });
            Row(u, ref y, "Noms des monstres", new[] { "Tous", "Cible seule", "Masqués" }, () => Settings.EnemyNames, k => Settings.EnemyNames = k);
            Row(u, ref y, "Taille des noms", new[] { "Petite", "Normale", "Grande" }, () => Settings.NameSize, k => Settings.NameSize = k);
            Row(u, ref y, "Chiffres de dégâts", new[] { "Affichés", "Masqués" }, () => PlayerPrefs.GetInt("korvalune.degats", 0), k => { PlayerPrefs.SetInt("korvalune.degats", k); HUD.ShowDamageNumbers = k == 0; Changed(); });
            Row(u, ref y, "Mini-carte / quête", new[] { "Affichées", "Carte seule", "Masquées" }, () => Settings.Minimap == 1 ? 2 : Settings.QuestPanel == 1 ? 1 : 0,
                k => { PlayerPrefs.SetInt("korvalune.quete", k >= 1 ? 1 : 0); Settings.Minimap = k == 2 ? 1 : 0; });
            Row(u, ref y, "Compteur d'images/s", new[] { "Affiché", "Masqué" }, () => Settings.FpsCounter, k => Settings.FpsCounter = k);

            ShowPage(PlayerPrefs.GetInt("korvalune.onglet", 0));
        }

        static void Changed() { PlayerPrefs.Save(); Settings.Apply(); var hud = FindFirstObjectByType<HUD>(); if (hud) hud.ApplySettings(); }
        public static void Vibrate() { if (Settings.Vibration == 0 && Application.isMobilePlatform) Handheld.Vibrate(); }

        void ShowPage(int i)
        {
            i = Mathf.Clamp(i, 0, pages.Count - 1); PlayerPrefs.SetInt("korvalune.onglet", i);
            for (int k = 0; k < pages.Count; k++)
            {
                bool on = k == i; pages[k].page.SetActive(on);
                pages[k].tab.color = on ? new Color(1f, 0.85f, 0.5f) : new Color(0.55f, 0.5f, 0.45f);
                pages[k].label.color = on ? Color.white : UIStyle.Gold;
            }
            foreach (var r in refreshers) r();
        }

        void Row(RectTransform parent, ref float y, string title, string[] choices, System.Func<int> current, System.Action<int> pick)
        {
            var l = Lbl(parent, title, title, 30, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(10, y), new Vector2(430, 84)); l.color = UIStyle.Parch;
            float w = Mathf.Min(200, 880f / choices.Length - 12);
            var bs = new List<(Image bg, Text t)>();
            for (int k = 0; k < choices.Length; k++)
            {
                int c = k;
                var b = Btn(parent, title + k, choices[k], new Vector2(0, 1), new Vector2(460 + k * (w + 12) + w / 2, y - 42), new Vector2(w, 70), () => { pick(c); foreach (var r in refreshers) r(); }, 24, true);
                bs.Add((b, b.GetComponentInChildren<Text>()));
            }
            refreshers.Add(() =>
            {
                int cur = current();
                for (int k = 0; k < bs.Count; k++)
                {
                    bool on = k == cur;
                    bs[k].bg.color = on ? new Color(1f, 0.82f, 0.45f) : new Color(0.55f, 0.5f, 0.45f);
                    bs[k].t.color = on ? Color.white : UIStyle.Gold; bs[k].t.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                }
            });
            y -= 104;
        }

        // ------------------------------------------------------------------------------------------ petits outils d'interface
        Image Img(RectTransform parent, string name, Sprite sp, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rt = im.rectTransform; rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size; im.sprite = sp; im.raycastTarget = false; return im;
        }

        Text Lbl(RectTransform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rt = t.rectTransform; rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(align == TextAnchor.MiddleLeft ? 0 : 0.5f, 0.5f); rt.anchoredPosition = pos + (anchor.y >= 1 && align == TextAnchor.MiddleLeft ? new Vector2(0, -box.y / 2) : Vector2.zero); rt.sizeDelta = box;
            t.font = font; t.fontSize = size; t.alignment = align; t.text = text; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            return t;
        }

        Image Btn(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action click, int fontSize, bool small = false)
        {
            var bg = Img(parent, name, UIStyle.Slot(), anchor, pos, size); bg.type = Image.Type.Sliced; bg.raycastTarget = true; bg.color = new Color(0.55f, 0.5f, 0.45f);
            var t = Lbl(bg.rectTransform, "T", label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size); t.color = UIStyle.Gold;
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => { Sound.UI("sfx_ui_clic", 0.5f); click(); });
            return bg;
        }
    }
}
