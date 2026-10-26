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
        public Quest quest;
        public Camera worldCam;
        public string zoneName = "Vallée de Korvalune";
        public VirtualJoystick Joystick { get; private set; }
        public Canvas Canvas { get; private set; }

        Font font;
        Image hpFill, manaFill, xpFill; Text hpText, levelText, questText, notifyText, zoneText;
        readonly List<(Image cd, Text t, int idx)> skillButtons = new List<(Image, Text, int)>();
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
            var pos = new[] { new Vector2(-330, 70), new Vector2(-310, 230), new Vector2(-200, 345), new Vector2(-45, 345) };
            string[] ic = { "whirlwind", "leap", "warcry", "stomp" };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                RoundButton(root, "Compétence" + i, ic[i], new Vector2(1, 0), pos[i], 140, () => player?.UseSkill(k), out var cd, out var t);
                skillButtons.Add((cd, t, i));
            }
            // menus (inventaire, compétences) — coins du haut
            RoundButton(root, "Inventaire", "bag", new Vector2(0.5f, 1), new Vector2(-60, -24), 96, () => Notify("Inventaire : bientôt disponible"), out _, out _);
            RoundButton(root, "Arbre", "book", new Vector2(0.5f, 1), new Vector2(60, -24), 96, () => Notify("Arbre de compétences : bientôt disponible"), out _, out _);

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
            foreach (var (cd, t, i) in skillButtons)
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
            UpdatePopups(); UpdateBars();
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
