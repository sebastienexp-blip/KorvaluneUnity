using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Korvalune
{
    /// <summary>
    /// Écran de création de personnage : classe, sexe, peau, yeux, coiffure (variante + teinte), sourcils, cils, curseurs
    /// de corps et de visage, aperçu de l'équipement pièce par pièce. Interface uGUI construite au démarrage depuis le
    /// catalogue (aucun prefab d'interface à maintenir). Le personnage est reconstruit à chaque changement de classe ou de sexe,
    /// son apparence est réappliquée.
    /// </summary>
    public class CharacterCreator : MonoBehaviour
    {
        static void Kill(UnityEngine.Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public CharacterCatalog catalog;
        public Camera viewCamera;
        public Transform stage;
        [Header("État")] public int classIndex, skinIndex = -1, eyeIndex = -1, hairIndex = -1, hairVariant, browIndex = -1, lashIndex = -1;
        public bool female;
        public Color hairTint = Color.white;
        public string tab = "Classe";

        GameObject current;
        CharacterAppearance look;
        readonly Dictionary<string, float> morphs = new Dictionary<string, float>();
        RectTransform listContent;
        Text title;
        Font font;
        float yaw = 180;

        static readonly string[] Tabs = { "Classe", "Sexe", "Peau", "Yeux", "Cheveux", "Teinte", "Sourcils", "Cils", "Corps", "Visage", "Équipement" };
        static readonly Color[] HairColors =
        {
            new Color(1, 1, 1), new Color(0.12f, 0.10f, 0.09f), new Color(0.35f, 0.22f, 0.12f), new Color(0.65f, 0.45f, 0.25f), new Color(0.95f, 0.82f, 0.55f),
            new Color(0.75f, 0.30f, 0.12f), new Color(0.85f, 0.85f, 0.88f), new Color(0.55f, 0.10f, 0.12f), new Color(0.35f, 0.20f, 0.55f), new Color(0.15f, 0.30f, 0.55f), new Color(0.20f, 0.45f, 0.25f)
        };

        void Start() { Init(); }

        /// <summary>Construit l'interface et le personnage (appelé au démarrage ; aussi en mode éditeur pour les captures).</summary>
        public void Init()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!stage) stage = new GameObject("Scène").transform;
            BuildUI();
            Rebuild();
            ShowTab(tab);
        }

        void Update()
        {
            if (current) current.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        // ------------------------------------------------------------------ personnage
        public void Rebuild()
        {
            if (current) Kill(current);
            if (catalog == null || catalog.classes.Count == 0) return;
            classIndex = Mathf.Clamp(classIndex, 0, catalog.classes.Count - 1);
            var c = catalog.classes[classIndex];
            var prefab = female ? (c.female ? c.female : c.male) : (c.male ? c.male : c.female);
            if (!prefab) return;
            current = Instantiate(prefab, stage);
            current.transform.localPosition = Vector3.zero;
            look = current.GetComponent<CharacterAppearance>() ?? current.AddComponent<CharacterAppearance>();
            look.female = female;
            Reapply();
        }

        void Reapply()
        {
            if (!look) return;
            if (skinIndex >= 0 && skinIndex < catalog.skins.Count) look.SetSkin(catalog.skins[skinIndex]);
            if (eyeIndex >= 0 && eyeIndex < catalog.eyes.Count) look.SetEyes(catalog.eyes[eyeIndex]);
            if (hairIndex >= 0 && hairIndex < catalog.hair.Count) look.Attach("Cheveux", catalog.hair[hairIndex], hairVariant, hairTint);
            if (browIndex >= 0 && browIndex < catalog.eyebrows.Count) look.Attach("Sourcils", catalog.eyebrows[browIndex], 0, Color.white);
            if (lashIndex >= 0 && lashIndex < catalog.eyelashes.Count) look.Attach("Cils", catalog.eyelashes[lashIndex], 0, Color.white);
            foreach (var kv in morphs) look.SetMorph(kv.Key, kv.Value);
        }

        public void Randomize()
        {
            var r = new System.Random();
            var sk = Enumerable.Range(0, catalog.skins.Count).Where(i => CharacterCatalog.Fits(catalog.skins[i].sex, female) && !catalog.skins[i].tags.Contains("adult")).ToList();
            if (sk.Count > 0) skinIndex = sk[r.Next(sk.Count)];
            if (catalog.eyes.Count > 0) eyeIndex = r.Next(catalog.eyes.Count);
            var hs = Enumerable.Range(0, catalog.hair.Count).Where(i => CharacterCatalog.Fits(catalog.hair[i].sex, female)).ToList();
            if (hs.Count > 0) { hairIndex = hs[r.Next(hs.Count)]; hairVariant = r.Next(Math.Max(1, catalog.hair[hairIndex].variants.Count)); }
            hairTint = HairColors[r.Next(HairColors.Length)];
            morphs.Clear();
            foreach (var m in catalog.Morphs(female)) if (r.NextDouble() < 0.25) morphs[m.id] = (float)(r.NextDouble() * (m.bipolar ? 1.2 : 0.8) - (m.bipolar ? 0.6 : 0));
            Rebuild(); ShowTab(tab);
        }

        // ------------------------------------------------------------------ interface
        void BuildUI()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var cgo = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>();
            if (viewCamera) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewCamera; canvas.planeDistance = 1f; }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            var root = cgo.GetComponent<RectTransform>();

            // colonne des onglets (gauche)
            var tabs = Panel(root, "Onglets", new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 20), new Vector2(260, -20), new Color(0.05f, 0.05f, 0.07f, 0.85f));
            var tl = tabs.gameObject.AddComponent<VerticalLayoutGroup>(); tl.padding = new RectOffset(12, 12, 90, 12); tl.spacing = 6; tl.childForceExpandHeight = false;
            var logo = Label(tabs, "CORVALUNE", 34, TextAnchor.MiddleCenter, new Color(0.85f, 0.78f, 0.6f)); logo.rectTransform.anchorMin = new Vector2(0, 1); logo.rectTransform.anchorMax = new Vector2(1, 1);
            logo.rectTransform.offsetMin = new Vector2(0, -80); logo.rectTransform.offsetMax = new Vector2(0, -16); logo.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            foreach (var t in Tabs) { var tt = t; Button(tabs, t, () => ShowTab(tt), 44); }
            Button(tabs, "Aléatoire", Randomize, 44);

            // liste des choix (droite)
            var right = Panel(root, "Choix", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-560, 20), new Vector2(-20, -20), new Color(0.05f, 0.05f, 0.07f, 0.85f));
            title = Label(right, "", 30, TextAnchor.MiddleLeft, new Color(0.85f, 0.78f, 0.6f));
            title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.offsetMin = new Vector2(20, -70); title.rectTransform.offsetMax = new Vector2(-20, -10);
            var scroll = new GameObject("Défilement", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            var srt = scroll.GetComponent<RectTransform>(); srt.SetParent(right, false);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = new Vector2(10, 10); srt.offsetMax = new Vector2(-10, -80);
            scroll.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); scroll.GetComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Contenu", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listContent = content.GetComponent<RectTransform>(); listContent.SetParent(srt, false);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(0.5f, 1);
            var vl = content.GetComponent<VerticalLayoutGroup>(); vl.spacing = 4; vl.childForceExpandHeight = false; vl.padding = new RectOffset(6, 6, 6, 6);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.GetComponent<ScrollRect>(); sr.content = listContent; sr.horizontal = false; sr.scrollSensitivity = 30;

            // rotation (bas)
            var bottom = Panel(root, "Rotation", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 20), new Vector2(300, 70), new Color(0.05f, 0.05f, 0.07f, 0.7f));
            var s = Slider(bottom, 0, 360, yaw, v => yaw = v); var r = s.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.offsetMin = new Vector2(20, -10); r.offsetMax = new Vector2(-20, 10);
        }

        public void ShowTab(string t)
        {
            tab = t;
            if (!listContent || catalog == null) return;
            foreach (var c in listContent.Cast<Transform>().ToList()) Kill(c.gameObject);
            title.text = t;
            switch (t)
            {
                case "Classe":
                    for (int i = 0; i < catalog.classes.Count; i++) { int k = i; var c = catalog.classes[i]; Button(listContent, $"{c.label}", () => { classIndex = k; Rebuild(); }, 56, k == classIndex); }
                    break;
                case "Sexe":
                    Button(listContent, "Homme", () => { female = false; morphs.Clear(); Rebuild(); ShowTab("Sexe"); }, 56, !female);
                    Button(listContent, "Femme", () => { female = true; morphs.Clear(); Rebuild(); ShowTab("Sexe"); }, 56, female);
                    break;
                case "Peau":
                    for (int i = 0; i < catalog.skins.Count; i++)
                    {
                        var s = catalog.skins[i]; if (!CharacterCatalog.Fits(s.sex, female)) continue;
                        int k = i; Button(listContent, Pretty(s.id) + (s.tags.Length > 0 ? $"  [{string.Join(", ", s.tags)}]" : ""), () => { skinIndex = k; look?.SetSkin(catalog.skins[k]); ShowTab("Peau"); }, 40, k == skinIndex);
                    }
                    break;
                case "Yeux":
                    for (int i = 0; i < catalog.eyes.Count; i++) { int k = i; Button(listContent, Pretty(catalog.eyes[i].id), () => { eyeIndex = k; look?.SetEyes(catalog.eyes[k]); ShowTab("Yeux"); }, 40, k == eyeIndex); }
                    break;
                case "Cheveux":
                    Button(listContent, "Coiffure d'origine", () => { hairIndex = -1; Rebuild(); ShowTab("Cheveux"); }, 40, hairIndex < 0);
                    for (int i = 0; i < catalog.hair.Count; i++)
                    {
                        var h = catalog.hair[i]; if (!CharacterCatalog.Fits(h.sex, female) && !h.For(female)) continue;
                        int k = i; Button(listContent, Pretty(h.id), () => { hairIndex = k; hairVariant = 0; look?.Attach("Cheveux", catalog.hair[k], 0, hairTint); ShowTab("Cheveux"); }, 40, k == hairIndex);
                        if (k == hairIndex)
                            for (int v = 0; v < h.variants.Count; v++) { int vv = v; Button(listContent, "    • " + Pretty(h.variants[v].id), () => { hairVariant = vv; look?.Attach("Cheveux", catalog.hair[k], vv, hairTint); ShowTab("Cheveux"); }, 34, vv == hairVariant); }
                    }
                    break;
                case "Teinte":
                    foreach (var col in HairColors) { var cc = col; var b = Button(listContent, "", () => { hairTint = cc; look?.SetHairTint(cc); }, 40); b.GetComponent<Image>().color = cc; }
                    break;
                case "Sourcils":
                    for (int i = 0; i < catalog.eyebrows.Count; i++) { int k = i; Button(listContent, Pretty(catalog.eyebrows[i].id), () => { browIndex = k; look?.Attach("Sourcils", catalog.eyebrows[k], 0, Color.white); ShowTab("Sourcils"); }, 40, k == browIndex); }
                    break;
                case "Cils":
                    for (int i = 0; i < catalog.eyelashes.Count; i++) { int k = i; Button(listContent, Pretty(catalog.eyelashes[i].id), () => { lashIndex = k; look?.Attach("Cils", catalog.eyelashes[k], 0, Color.white); ShowTab("Cils"); }, 40, k == lashIndex); }
                    break;
                case "Corps":
                case "Visage":
                    bool face = t == "Visage";
                    string group = null;
                    foreach (var m in catalog.Morphs(female).Where(x => x.face == face))
                    {
                        if (m.group != group) { group = m.group; var g = Label(listContent, "— " + m.label.Split(':')[0].Trim() + " —", 22, TextAnchor.MiddleLeft, new Color(0.85f, 0.78f, 0.6f)); g.gameObject.AddComponent<LayoutElement>().preferredHeight = 34; }
                        var row = new GameObject(m.id, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                        row.transform.SetParent(listContent, false); row.AddComponent<LayoutElement>().preferredHeight = 34;
                        var hl = row.GetComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childForceExpandWidth = false; hl.childControlWidth = true;
                        var lab = Label(row.transform, m.label.Contains(":") ? m.label.Split(':')[1].Trim() : m.label, 18, TextAnchor.MiddleLeft, Color.white);
                        lab.gameObject.AddComponent<LayoutElement>().preferredWidth = 230;
                        var id = m.id;
                        var sl = Slider(row.transform, m.bipolar ? -1f : 0f, 1f, morphs.TryGetValue(id, out var cur) ? cur : 0f, v => { morphs[id] = v; look?.SetMorph(id, v); });
                        sl.gameObject.AddComponent<LayoutElement>().preferredWidth = 240;
                    }
                    break;
                case "Équipement":
                    var eq = current ? current.GetComponent<CharacterEquipment>() : null;
                    if (eq == null) { Label(listContent, "Aucune pièce détachable.", 20, TextAnchor.MiddleLeft, Color.white); break; }
                    foreach (var s in eq.Slots.Where(x => x != null && !CharacterEquipment.IsBody(x.name) && x.name != CharacterEquipment.Anatomy))
                    {
                        var n = s.name; Button(listContent, (s.equipped ? "✔ " : "✘ ") + Pretty(n), () => { eq.Toggle(n); ShowTab("Équipement"); }, 40, s.equipped);
                    }
                    break;
            }
        }

        static string Pretty(string id)
        {
            var s = id.Replace('_', ' ').Replace('-', ' ');
            return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
        }

        // ------------------------------------------------------------------ petites fabriques uGUI
        RectTransform Panel(RectTransform parent, string name, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = omin; rt.offsetMax = omax;
            go.GetComponent<Image>().color = col;
            return rt;
        }

        Text Label(Transform parent, string text, int size, TextAnchor anchor, Color col)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        Button Button(Transform parent, string text, Action onClick, float height, bool selected = false)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.GetComponent<Image>().color = selected ? new Color(0.45f, 0.32f, 0.12f, 0.95f) : new Color(0.16f, 0.16f, 0.2f, 0.95f);
            var b = go.GetComponent<Button>(); b.onClick.AddListener(() => onClick());
            var l = Label(go.transform, text, 20, TextAnchor.MiddleLeft, Color.white);
            l.rectTransform.anchorMin = Vector2.zero; l.rectTransform.anchorMax = Vector2.one; l.rectTransform.offsetMin = new Vector2(14, 0); l.rectTransform.offsetMax = new Vector2(-8, 0);
            return b;
        }

        Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var go = new GameObject("Curseur", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var bg = new GameObject("Fond", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(go.transform, false);
            var bgr = bg.GetComponent<RectTransform>(); bgr.anchorMin = new Vector2(0, 0.35f); bgr.anchorMax = new Vector2(1, 0.65f); bgr.offsetMin = bgr.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            var area = new GameObject("Zone", typeof(RectTransform)); area.transform.SetParent(go.transform, false);
            var ar = area.GetComponent<RectTransform>(); ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = new Vector2(8, 0); ar.offsetMax = new Vector2(-8, 0);
            var handle = new GameObject("Poignée", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(area.transform, false);
            var hr = handle.GetComponent<RectTransform>(); hr.sizeDelta = new Vector2(16, 0); hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(0, 1);
            handle.GetComponent<Image>().color = new Color(0.85f, 0.7f, 0.4f);
            var s = go.GetComponent<Slider>(); s.handleRect = hr; s.targetGraphic = handle.GetComponent<Image>();
            s.minValue = min; s.maxValue = max; s.value = value; s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }
    }
}
