using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Carte plein écran : vue de dessus de toute la zone (photographiée une fois à l'ouverture), qu'on fait glisser
    /// du doigt et qu'on agrandit (pincement, boutons + / −). Repères : héros, villes et zone sûre, portails et entrées de
    /// donjons, marchands, Oracle des Brèches, objectif de quête, camps de monstres en cours. Légende à droite.</summary>
    public class WorldMap : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public static WorldMap Current;
        RawImage view; RectTransform frame, markers; RenderTexture rt;
        RectTransform repere; Text distLbl, tracerLbl;   // repère GPS posé à la main, et ses commandes
        Vector3 worldMin, worldSize; float zoom = 1f; Vector2 center = new Vector2(0.5f, 0.5f);
        readonly List<(Transform t, Vector3 fixedPos, RectTransform icon)> pins = new List<(Transform, Vector3, RectTransform)>();
        RectTransform heroPin; Transform hero; Font font; float lastPinch;

        public static void Toggle(Font font) { if (Current) Current.Close(); else Open(font); }

        static void Open(Font font)
        {
            QuestEvents.Raise("carte");
            var go = new GameObject("Carte", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 200;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            var m = go.AddComponent<WorldMap>(); m.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); m.Build(); Current = m;
            Sound.UI("sfx_ui_ouvrir", 0.7f);
        }

        public void Close() { Sound.UI("sfx_ui_fermer", 0.7f); if (rt) rt.Release(); Current = null; Destroy(gameObject); }

        // ------------------------------------------------------------------------------------------ construction
        void Build()
        {
            var root = (RectTransform)transform;
            var shade = Img(root, null, new Color(0.03f, 0.02f, 0.02f, 0.94f)); Stretch(shade.rectTransform, 0, 0, 0, 0); shade.raycastTarget = true;
            var border = Img(root, UIStyle.Ornate(), Color.white); border.type = Image.Type.Sliced; Stretch(border.rectTransform, 60, 60, 420, 60);
            var mask = new GameObject("Vue", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>(); mask.SetParent(border.rectTransform, false);
            Stretch(mask, 40, 40, 40, 40); mask.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.08f); frame = mask;
            view = new GameObject("Carte", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); view.rectTransform.SetParent(mask, false); Stretch(view.rectTransform, 0, 0, 0, 0); view.raycastTarget = true;
            markers = new GameObject("Repères", typeof(RectTransform)).GetComponent<RectTransform>(); markers.SetParent(mask, false); Stretch(markers, 0, 0, 0, 0);

            Snapshot();
            // légende et commandes (à droite)
            var side = new GameObject("Légende", typeof(RectTransform)).GetComponent<RectTransform>(); side.SetParent(root, false);
            side.anchorMin = new Vector2(1, 0); side.anchorMax = new Vector2(1, 1); side.pivot = new Vector2(1, 0.5f); side.sizeDelta = new Vector2(340, -120); side.anchoredPosition = new Vector2(-50, 0);
            var zone = GameBootstrap.Instance ? GameBootstrap.Instance.zoneName : "";
            Lbl(side, zone, new Vector2(0, -20), 34, UIStyle.Gold).fontStyle = FontStyle.Bold;
            float y = -100;
            foreach (var (c, s) in new[] { (new Color(1f, 0.9f, 0.4f), "Toi"), (new Color(0.55f, 0.85f, 1f), "Ville (zone sûre)"), (new Color(0.75f, 0.45f, 1f), "Portail / donjon"),
                                          (new Color(1f, 0.75f, 0.3f), "Marchand"), (new Color(0.8f, 0.55f, 1f), "Oracle des Brèches"), (new Color(0.45f, 0.78f, 1f), "Pierre de voyage"), (new Color(0.4f, 1f, 0.5f), "Objectif de quête"), (new Color(1f, 0.3f, 0.25f), "Monstres") })
            { var d = Img(side, UIStyle.Disc(), c); d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0, 1); d.rectTransform.anchoredPosition = new Vector2(30, y); d.rectTransform.sizeDelta = new Vector2(26, 26); Lbl(side, s, new Vector2(60, y), 24, UIStyle.Parch, TextAnchor.MiddleLeft); y -= 46; }
            // régions : la zone actuelle (vue en direct) et les autres régions extérieures (photographiées à la construction)
            var here = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // sélecteur de région (‹ nom ›) : une seule ligne, quel que soit le nombre de régions (la légende reste lisible)
            var avail = new List<Waypoint.Region>();
            foreach (var r in Waypoint.Regions) if (r.scene == here || Resources.Load<Texture2D>("UI/Cartes/" + r.scene)) avail.Add(r);
            if (avail.Count > 0)
            {
                int idx = Mathf.Max(0, avail.FindIndex(r => r.scene == here));
                Btn(side, avail[idx].name, new Vector2(130, 330), () => ShowRegion(avail[idx].scene), 240);
                var lab = side.GetChild(side.childCount - 1).GetComponentInChildren<Text>();
                lab.rectTransform.sizeDelta = new Vector2(220, 70); lab.resizeTextForBestFit = true; lab.resizeTextMinSize = 16; lab.resizeTextMaxSize = 30; lab.horizontalOverflow = HorizontalWrapMode.Wrap;
                System.Action<int> step = k => { idx = (idx + k + avail.Count) % avail.Count; lab.text = avail[idx].name; ShowRegion(avail[idx].scene); };
                Btn(side, "‹", new Vector2(70, 250), () => step(-1));
                Btn(side, "›", new Vector2(190, 250), () => step(1));
            }
            Btn(side, "+", new Vector2(70, 160), () => SetZoom(zoom * 1.5f));
            Btn(side, "−", new Vector2(190, 160), () => SetZoom(zoom / 1.5f));
            Btn(side, "Me centrer", new Vector2(130, 70), () => { if (hero) center = UvOf(hero.position); Clamp(); }, 240);
            Btn(side, "Fermer", new Vector2(130, -20), Close, 240);
            BuildPins();
            // repère GPS : pastille dorée à l'endroit posé, distance, effacer, activer/couper le tracé au sol
            repere = Img(markers, UIStyle.Disc(), new Color(1f, 0.82f, 0.2f)).rectTransform; repere.sizeDelta = new Vector2(30, 30); repere.gameObject.SetActive(false);
            distLbl = Lbl(frame, "", Vector2.zero, 26, UIStyle.Gold, TextAnchor.MiddleLeft);
            distLbl.rectTransform.anchorMin = distLbl.rectTransform.anchorMax = Vector2.zero; distLbl.rectTransform.pivot = Vector2.zero;
            distLbl.rectTransform.anchoredPosition = new Vector2(20, 104); distLbl.rectTransform.sizeDelta = new Vector2(900, 40); distLbl.alignment = TextAnchor.MiddleLeft;
            Btn(frame, "Effacer le repère", new Vector2(16, 16), () => GroundGps.Manual = null, 280);
            Btn(frame, "Tracé", new Vector2(310, 16), () => Settings.Gps = 1 - Settings.Gps, 280);
            tracerLbl = frame.GetChild(frame.childCount - 1).GetComponentInChildren<Text>();
            if (hero) center = UvOf(hero.position);
            SetZoom(2f);
        }

        /// <summary>Photographie de dessus de toute la zone (terrain ou donjon), une seule fois.</summary>
        void Snapshot()
        {
            var t = Terrain.activeTerrain;
            if (t) { worldMin = t.transform.position; worldSize = t.terrainData.size; }
            else
            {   // donjon : boîte englobante des sols
                var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                b.Expand(new Vector3(10, 0, 10)); worldMin = b.min; worldSize = b.size;
            }
            float side = Mathf.Max(worldSize.x, worldSize.z); worldSize.x = worldSize.z = side;
            rt = new RenderTexture(2048, 2048, 16) { name = "Carte" };
            var go = new GameObject("Photographe"); var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = side / 2; cam.aspect = 1; cam.nearClipPlane = 1; cam.farClipPlane = 600;
            cam.transform.position = new Vector3(worldMin.x + side / 2, worldMin.y + 400, worldMin.z + side / 2); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.1f, 0.12f); cam.cullingMask = ~(1 << 5);
            bool depthOn = Shader.IsKeywordEnabled("KORV_WATER_DEPTH"); Shader.DisableKeyword("KORV_WATER_DEPTH");   // vue orthographique : l'écume de l'eau y serait fausse
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; Destroy(go);
            if (depthOn) Shader.EnableKeyword("KORV_WATER_DEPTH");
            view.texture = rt; liveTex = rt;
        }

        Texture liveTex; readonly List<GameObject> regionLabels = new List<GameObject>();

        /// <summary>Affiche une région : la zone actuelle avec ses repères, ou la photo d'une autre région (sans repères).</summary>
        void ShowRegion(string scene)
        {
            bool here = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            foreach (var g in regionLabels) if (g) Destroy(g); regionLabels.Clear();
            if (here) { view.texture = liveTex; markers.gameObject.SetActive(true); if (hero) center = UvOf(hero.position); SetZoom(2f); return; }
            var tex = Resources.Load<Texture2D>("UI/Cartes/" + scene); if (!tex) return;
            if (!liveTex) liveTex = view.texture;
            view.texture = tex; markers.gameObject.SetActive(false); center = new Vector2(0.5f, 0.5f); zoom = 1f; SetZoom(1f);
            var name = new GameObject("Région", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); name.rectTransform.SetParent(frame, false);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 1); name.rectTransform.anchoredPosition = new Vector2(0, -40); name.rectTransform.sizeDelta = new Vector2(900, 60);
            name.font = font; name.fontSize = 40; name.alignment = TextAnchor.MiddleCenter; name.color = UIStyle.Gold; name.fontStyle = FontStyle.Bold; name.raycastTarget = false;
            foreach (var r in Waypoint.Regions) if (r.scene == scene) name.text = r.name;
            name.gameObject.AddComponent<Outline>().effectColor = Color.black; regionLabels.Add(name.gameObject);
        }

        Vector2 UvOf(Vector3 w) => new Vector2((w.x - worldMin.x) / worldSize.x, (w.z - worldMin.z) / worldSize.z);

        void BuildPins()
        {
            var p = FindFirstObjectByType<PlayerController>(); hero = p ? p.transform : null;
            var g = GameBootstrap.Instance;
            if (g && g.safeRadius > 0) Pin(null, g.safeCenter + Vector3.up * 0.01f, new Color(0.55f, 0.85f, 1f), 40, "Ville");
            foreach (var x in FindObjectsByType<Portal>(FindObjectsSortMode.None)) Pin(x.transform, Vector3.zero, new Color(0.75f, 0.45f, 1f), 30, x.displayName);
            foreach (var x in FindObjectsByType<Merchant>(FindObjectsSortMode.None)) Pin(x.transform, Vector3.zero, new Color(1f, 0.75f, 0.3f), 18, null);
            foreach (var x in FindObjectsByType<Waypoint>(FindObjectsSortMode.None)) Pin(x.transform, Vector3.zero, new Color(0.45f, 0.78f, 1f), 30, "Pierre de voyage");
            foreach (var x in FindObjectsByType<RiftKeeper>(FindObjectsSortMode.None)) Pin(x.transform, Vector3.zero, new Color(0.8f, 0.55f, 1f), 28, "Oracle des Brèches");
            var q = FindFirstObjectByType<Quest>();
            var place = q ? q.CurrentPlace : null; if (place.HasValue) Pin(null, place.Value, new Color(0.4f, 1f, 0.5f), 30, "Objectif");
            foreach (var h in Health.All) if (h && !h.Dead && h.faction == Faction.Enemy) Pin(h.transform, Vector3.zero, new Color(1f, 0.3f, 0.25f), 12, null);
            var ev = WorldEvents.Current ? WorldEvents.Current.Spot : null; if (ev.HasValue) Pin(null, ev.Value + Vector3.up * 0.01f, new Color(1f, 0.8f, 0.3f), 34, "Événement");
            heroPin = Img(markers, UIStyle.Disc(), new Color(1f, 0.9f, 0.4f)).rectTransform; heroPin.sizeDelta = new Vector2(30, 30);
            var ring = Img(heroPin, UIStyle.Ring(), Color.white).rectTransform; ring.sizeDelta = new Vector2(44, 44);
        }

        void Pin(Transform t, Vector3 at, Color c, float size, string label)
        {
            var im = Img(markers, UIStyle.Disc(), c); im.rectTransform.sizeDelta = new Vector2(size, size);
            if (!string.IsNullOrEmpty(label)) { var l = Lbl(im.rectTransform, label, new Vector2(0, size * 0.5f + 16), 22, Color.white); l.rectTransform.anchorMin = l.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); }
            pins.Add((t, at, im.rectTransform));
        }

        // ------------------------------------------------------------------------------------------ navigation
        void SetZoom(float z) { zoom = Mathf.Clamp(z, 1f, 8f); Clamp(); }
        void Clamp() { float h = 0.5f / zoom; center.x = Mathf.Clamp(center.x, h, 1 - h); center.y = Mathf.Clamp(center.y, h, 1 - h); }

        public void OnDrag(PointerEventData e)
        {
            if (Input.touchCount >= 2) return;
            var size = frame.rect.size; float k = 1f / zoom;
            var scale = ((RectTransform)transform).rect.size.x / Screen.width;
            center -= new Vector2(e.delta.x * scale / size.x * k, e.delta.y * scale / size.y * k); Clamp();
        }
        public void OnScroll(PointerEventData e) { SetZoom(zoom * (e.scrollDelta.y > 0 ? 1.2f : 1 / 1.2f)); }

        /// <summary>Un tap sur la carte pose le point GPS à cet endroit (le tracé au sol s'affiche si le GPS est activé).</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (!view || e.dragging) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view.rectTransform, e.position, e.pressEventCamera, out var lp)) return;
            var r = view.rectTransform.rect; var ur = view.uvRect;
            var uv = new Vector2(ur.xMin + (lp.x / r.width + 0.5f) * ur.width, ur.yMin + (lp.y / r.height + 0.5f) * ur.height);
            GroundGps.SetTarget(worldMin + new Vector3(uv.x * worldSize.x, 0, uv.y * worldSize.z));
            Sound.UI("sfx_ui_clic", 0.5f);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M)) { Close(); return; }
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1); float d = (a.position - b.position).magnitude;
                if (lastPinch > 0) SetZoom(zoom * d / lastPinch); lastPinch = d;
            }
            else lastPinch = 0;
            // fenêtre carrée dans un cadre rectangulaire : on garde les proportions
            var size = frame.rect.size; float aspect = size.x / Mathf.Max(1, size.y);
            // la photo est carrée et le cadre plus large : on zoome au moins assez pour remplir la largeur (sinon les bords
            // de l'image étaient étirés en bandes sur les côtés)
            if (zoom < aspect) { zoom = aspect; }
            float h = 0.5f / zoom, w = h * aspect;
            center.x = Mathf.Clamp(center.x, w, 1 - w); center.y = Mathf.Clamp(center.y, h, 1 - h);
            view.uvRect = new Rect(center.x - w, center.y - h, 2 * w, 2 * h);
            foreach (var (t, fixedPos, icon) in pins)
            {
                if (!icon) continue;
                if (t == null && fixedPos == Vector3.zero) { icon.gameObject.SetActive(false); continue; }
                Place(icon, t ? t.position : fixedPos, size, w, h);
            }
            // repère posé à la main : pastille, distance depuis le héros, état du tracé
            if (GroundGps.Manual.HasValue)
            {
                repere.gameObject.SetActive(true); Place(repere, GroundGps.Manual.Value, size, w, h);
                if (hero && distLbl) { var d = GroundGps.Manual.Value - hero.position; d.y = 0; distLbl.text = $"Repère : {d.magnitude:0} m"; }
            }
            else { repere.gameObject.SetActive(false); if (distLbl) distLbl.text = "Aucun repère : touchez la carte pour en poser un"; }
            if (tracerLbl) tracerLbl.text = Settings.Gps == 0 ? "Tracé : activé" : "Tracé : désactivé";
            if (hero && heroPin) { Place(heroPin, hero.position, size, w, h); heroPin.localRotation = Quaternion.Euler(0, 0, -hero.eulerAngles.y); }
        }

        void Place(RectTransform icon, Vector3 world, Vector2 size, float w, float h)
        {
            var uv = UvOf(world);
            var local = new Vector2((uv.x - center.x) / (2 * w) * size.x, (uv.y - center.y) / (2 * h) * size.y);
            bool inside = Mathf.Abs(local.x) < size.x / 2 && Mathf.Abs(local.y) < size.y / 2;
            icon.gameObject.SetActive(inside); icon.anchoredPosition = local;
        }

        // ------------------------------------------------------------------------------------------ outils
        Image Img(RectTransform parent, Sprite s, Color c)
        {
            var im = new GameObject("I", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); im.rectTransform.SetParent(parent, false);
            im.sprite = s; im.color = c; im.raycastTarget = false; return im;
        }
        static void Stretch(RectTransform r, float l, float b, float rr, float t) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t); }
        Text Lbl(RectTransform parent, string s, Vector2 pos, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(parent, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1); t.rectTransform.pivot = new Vector2(a == TextAnchor.MiddleLeft ? 0 : 0.5f, 0.5f);
            t.rectTransform.anchoredPosition = pos + (a == TextAnchor.MiddleLeft ? Vector2.zero : new Vector2(160, 0)); t.rectTransform.sizeDelta = new Vector2(300, 40);
            t.font = font; t.fontSize = size; t.alignment = a; t.color = c; t.text = s; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<Outline>().effectColor = Color.black; return t;
        }
        void Btn(RectTransform parent, string s, Vector2 pos, System.Action a, float w = 100)
        {
            var im = Img(parent, UIStyle.Slot(), new Color(0.75f, 0.65f, 0.55f)); im.type = Image.Type.Sliced; im.raycastTarget = true;
            im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0, 0); im.rectTransform.anchoredPosition = pos; im.rectTransform.sizeDelta = new Vector2(w, 74);
            var t = Lbl(im.rectTransform, s, Vector2.zero, 32, UIStyle.Gold); t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); t.rectTransform.anchoredPosition = Vector2.zero;
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => { Sound.UI("sfx_ui_clic", 0.5f); a(); });
        }
    }
}
