using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Écran de sélection des personnages, à la manière de Diablo 3 : le héros choisi se tient au centre d'une
    /// crypte éclairée de braseros ; à gauche les 5 emplacements du compte (nom, niveau, classe) ; « Jouer » et
    /// « Supprimer » en bas. Création : bandeau des 5 classes, homme / femme, apparence détaillée (créateur), nom.</summary>
    public class CharacterSelect : MonoBehaviour
    {
        public CharacterCatalog catalog;
        public Camera viewCamera;
        public Transform stage;
        public string gameScene = "Korvalune_Vallee", creatorScene = "CharacterCreator";

        /// <summary>Brouillon de création partagé avec le créateur d'apparence (aller-retour entre les scènes).</summary>
        public class Draft { public int classIndex; public bool female; public string name = ""; public AppearanceSave look = new AppearanceSave(); }
        public static Draft draft;
        public static bool returningFromCreator;

        Font font; RectTransform root, slotsPanel, createPanel, mainButtons;
        readonly List<Image> slotFrames = new List<Image>();
        Text infoText, errorText; InputField nameField; Text sexLabel;
        readonly List<Image> classCards = new List<Image>();
        GameObject shown; int selected; bool creating; float yaw = 180;

        void Start() { Init(); }

        public void Init()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SaveSystem.Load();
            BuildUI();
            selected = Mathf.Clamp(SaveSystem.Account.lastSelected, 0, Mathf.Max(0, SaveSystem.Account.characters.Count - 1));
            if (returningFromCreator && draft != null) { returningFromCreator = false; StartCreate(false); }
            else if (SaveSystem.Account.characters.Count == 0) StartCreate(true);
            else ShowList();
            if (Application.isPlaying && AutoTest.Enabled) { AutoTest.Log("écran de sélection ouvert"); Invoke(nameof(AutoPlay), 4f); }
        }

        /// <summary>Test automatique : crée l'héroïne de test comme un joueur (classe, sexe, apparence aléatoire) puis lance la partie.</summary>
        void AutoPlay()
        {
            if (SaveSystem.Account.characters.Count == 0)
            {
                int ci = catalog != null ? Mathf.Max(0, catalog.classes.FindIndex(c => c.id == "ElyraNoctis")) : 0;
                var look = new AppearanceSave(); look.skin = 1; look.hair = 3;
                var id = catalog != null && catalog.classes.Count > 0 ? catalog.classes[ci].id : "ElyraNoctis";
                SaveSystem.Create("Testeuse", id, true, look);
            }
            selected = 0; AutoTest.Log("sélection : partie lancée avec " + SaveSystem.Account.characters[0].name);
            Play();
        }

        void Update()
        {
            if (shown)
            {
                if (Input.GetMouseButton(0) && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject())) yaw -= Input.GetAxis("Mouse X") * 6f;
                shown.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        // ------------------------------------------------------------------------------------------ héros affiché
        GameObject PrefabFor(int classIndex, bool female)
        {
            if (catalog == null || catalog.classes.Count == 0) return null;
            var c = catalog.classes[Mathf.Clamp(classIndex, 0, catalog.classes.Count - 1)];
            return female ? (c.HasFemale ? c.female : c.male) : (c.HasMale ? c.male : c.female);
        }
        int ClassIndexOf(string id) { if (catalog == null) return 0; int i = catalog.classes.FindIndex(c => c.id == id); return Mathf.Max(0, i); }

        void Show(int classIndex, bool female, AppearanceSave look)
        {
            if (shown) Kill(shown);
            var p = PrefabFor(classIndex, female); if (!p) return;
            shown = Instantiate(p, stage); shown.transform.localPosition = Vector3.zero; yaw = 180; shown.transform.rotation = Quaternion.Euler(0, yaw, 0); CharacterRig.EditorIdlePose(shown);
            SaveSystem.ApplyLook(shown, catalog, look, female);
        }

        static void Kill(Object o) { if (!o) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        // ------------------------------------------------------------------------------------------ états
        public void ShowList()
        {
            creating = false;
            createPanel.gameObject.SetActive(false); slotsPanel.gameObject.SetActive(true); mainButtons.gameObject.SetActive(true);
            RefreshSlots();
            var chars = SaveSystem.Account.characters;
            if (chars.Count > 0) Select(Mathf.Clamp(selected, 0, chars.Count - 1));
        }

        public void Select(int i)
        {
            var chars = SaveSystem.Account.characters;
            if (i >= chars.Count) { StartCreate(true); return; }
            selected = i; SaveSystem.Account.lastSelected = i;
            var c = chars[i];
            Show(ClassIndexOf(c.classId), c.female, c.look);
            var cl = catalog != null && catalog.classes.Count > 0 ? catalog.classes[ClassIndexOf(c.classId)] : null;
            infoText.text = $"<b>{c.name}</b>\n{(cl != null ? cl.label : c.classId)} · {(c.female ? "femme" : "homme")}\nNiveau {c.level}";
            for (int k = 0; k < slotFrames.Count; k++) slotFrames[k].color = k == i ? new Color(1f, 0.85f, 0.55f) : Color.white;
        }

        public void StartCreate(bool fresh)
        {
            if (SaveSystem.Account.characters.Count >= AccountSave.Slots) { infoText.text = "Les 5 emplacements de ce compte sont occupés."; return; }
            creating = true;
            if (fresh || draft == null) draft = new Draft();
            createPanel.gameObject.SetActive(true); slotsPanel.gameObject.SetActive(SaveSystem.Account.characters.Count > 0); mainButtons.gameObject.SetActive(false);
            nameField.text = draft.name; errorText.text = "";
            PickClass(draft.classIndex);
        }

        public void PickClass(int i)
        {
            draft.classIndex = i;
            for (int k = 0; k < classCards.Count; k++) classCards[k].color = k == i ? new Color(1f, 0.85f, 0.55f) : Color.white;
            sexLabel.text = draft.female ? "Femme" : "Homme";
            Show(i, draft.female, draft.look);
            var cl = catalog != null && i < catalog.classes.Count ? catalog.classes[i] : null;
            infoText.text = cl != null ? $"<b>{cl.label}</b>\n{cl.description}" : "";
        }

        public void ToggleSex() { draft.female = !draft.female; draft.look = new AppearanceSave(); PickClass(draft.classIndex); }

        public void OpenCreator()
        {
            draft.name = nameField.text;
            returningFromCreator = true;
            if (Application.isPlaying) SceneManager.LoadScene(creatorScene);
        }

        public bool Create()
        {
            var err = SaveSystem.ValidateName(nameField.text);
            if (err != null) { errorText.text = err; return false; }
            var cl = catalog != null && draft.classIndex < catalog.classes.Count ? catalog.classes[draft.classIndex].id : "KaelDraven";
            var c = SaveSystem.Create(nameField.text, cl, draft.female, draft.look);
            if (c == null) { errorText.text = "Plus d'emplacement libre sur ce compte."; return false; }
            draft = null; selected = SaveSystem.Account.characters.Count - 1;
            ShowList();
            return true;
        }

        public void Play()
        {
            var chars = SaveSystem.Account.characters;
            if (selected >= chars.Count) return;
            var c = chars[selected];
            c.lastPlayed = System.DateTime.UtcNow.ToString("o"); SaveSystem.Save();
            SaveSystem.Current = c;
            GameBootstrap.chosenHero = PrefabFor(ClassIndexOf(c.classId), c.female);
            if (Application.isPlaying) SceneManager.LoadScene(gameScene);
        }

        bool confirmDelete;
        public void DeleteSelected()
        {
            var chars = SaveSystem.Account.characters;
            if (selected >= chars.Count) return;
            if (!confirmDelete) { confirmDelete = true; infoText.text = $"Supprimer définitivement <b>{chars[selected].name}</b> ? Touchez encore « Supprimer »."; return; }
            confirmDelete = false;
            SaveSystem.Delete(chars[selected]); selected = 0;
            if (chars.Count == 0) { if (shown) Kill(shown); StartCreate(true); } else ShowList();
        }

        // ------------------------------------------------------------------------------------------ interface
        void BuildUI()
        {
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var go = new GameObject("Sélection", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            if (viewCamera) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewCamera; canvas.planeDistance = 1; }
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            root = (RectTransform)go.transform;

            var logo = Label(root, "Logo", "KORVALUNE", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(900, 90));
            logo.color = UIStyle.Gold; logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);

            // emplacements (gauche)
            slotsPanel = Panel(root, "Héros", new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(470, 760));
            Label(slotsPanel, "Titre", "Vos héros", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(420, 50)).color = UIStyle.Gold;
            for (int i = 0; i < AccountSave.Slots; i++)
            {
                int k = i;
                var b = Btn(slotsPanel, "", new Vector2(0.5f, 1), new Vector2(0, -95 - i * 128), new Vector2(420, 116), () => Select(k), 24);
                slotFrames.Add(b);
            }
            // informations et boutons (bas)
            var info = Panel(root, "Infos", new Vector2(1, 0.5f), new Vector2(-40, 120), new Vector2(460, 260));
            infoText = Label(info, "Texte", "", 28, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(26, -22), new Vector2(410, 220));
            mainButtons = new GameObject("Boutons", typeof(RectTransform)).GetComponent<RectTransform>(); Place(mainButtons, root, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1200, 120));
            Btn(mainButtons, "JOUER", new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(360, 100), Play, 44);
            Btn(mainButtons, "Supprimer", new Vector2(0.5f, 0), new Vector2(-370, 10), new Vector2(240, 76), DeleteSelected, 28);
            Btn(mainButtons, "Nouveau héros", new Vector2(0.5f, 0), new Vector2(370, 10), new Vector2(280, 76), () => StartCreate(true), 28);

            // création (bas) : classes, sexe, nom, apparence, créer
            createPanel = Panel(root, "Création", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1500, 330));
            int n = catalog != null ? catalog.classes.Count : 0;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                var b = Btn(createPanel, catalog.classes[i].label.Split('—')[0].Trim(), new Vector2(0, 1), new Vector2(30 + i * 292, -24), new Vector2(276, 92), () => PickClass(k), 28);
                classCards.Add(b);
            }
            var sx = Btn(createPanel, "", new Vector2(0, 0), new Vector2(30, 120), new Vector2(276, 76), ToggleSex, 28);
            sexLabel = sx.GetComponentInChildren<Text>();
            Btn(createPanel, "Apparence…", new Vector2(0, 0), new Vector2(322, 120), new Vector2(276, 76), OpenCreator, 28);
            var nf = Panel(createPanel, "Nom", new Vector2(0, 0), new Vector2(614, 120), new Vector2(560, 76));
            nameField = MakeInput(nf, "Nom du héros");
            Btn(createPanel, "CRÉER", new Vector2(1, 0), new Vector2(-30, 112), new Vector2(270, 92), () => Create(), 36);
            Btn(createPanel, "Annuler", new Vector2(1, 0), new Vector2(-30, 22), new Vector2(270, 70), () => { if (SaveSystem.Account.characters.Count > 0) ShowList(); }, 26);
            errorText = Label(createPanel, "Erreur", "", 24, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(30, 30), new Vector2(1100, 60));
            errorText.color = new Color(1f, 0.45f, 0.35f);
        }

        void RefreshSlots()
        {
            var chars = SaveSystem.Account.characters;
            for (int i = 0; i < slotFrames.Count; i++)
            {
                var t = slotFrames[i].GetComponentInChildren<Text>();
                if (i < chars.Count)
                {
                    var c = chars[i]; var cl = catalog != null && catalog.classes.Count > 0 ? catalog.classes[ClassIndexOf(c.classId)].label.Split('—')[0].Trim() : c.classId;
                    t.text = $"<b>{c.name}</b>\n<size=22>Niveau {c.level} · {cl} · {(c.female ? "F" : "H")}</size>";
                }
                else t.text = "<color=#a08d6a>+ Emplacement libre</color>";
            }
        }

        // fabrique (même habillage que l'interface de jeu)
        static void Place(RectTransform rt, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        { rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; }

        RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); Place(im.rectTransform, parent, anchor, pos, size);
            im.sprite = UIStyle.Panel(); im.type = Image.Type.Sliced; im.color = new Color(1, 1, 1, 0.93f); return im.rectTransform;
        }

        Text Label(RectTransform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); Place(t.rectTransform, parent, anchor, pos, box);
            t.font = font; t.fontSize = size; t.alignment = align; t.text = text; t.color = UIStyle.Parch; t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }

        Image Btn(RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick, int fs)
        {
            var im = new GameObject("Bouton " + label, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Image>(); Place(im.rectTransform, parent, anchor, pos, size);
            im.sprite = UIStyle.Panel(); im.type = Image.Type.Sliced;
            var b = im.GetComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => onClick());
            var t = Label(im.rectTransform, "Texte", label, fs, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20, 10));
            return im;
        }

        InputField MakeInput(RectTransform parent, string placeholder)
        {
            var go = new GameObject("Saisie", typeof(RectTransform), typeof(Image), typeof(InputField)); var rt = (RectTransform)go.transform;
            Place(rt, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 60));
            go.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.04f, 0.85f);
            var txt = Label(rt, "Texte", "", 30, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(490, 56)); txt.supportRichText = false;
            var ph = Label(rt, "Indication", placeholder, 30, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(490, 56)); ph.color = new Color(0.7f, 0.62f, 0.5f, 0.7f); ph.fontStyle = FontStyle.Italic;
            var f = go.GetComponent<InputField>(); f.textComponent = txt; f.placeholder = ph; f.characterLimit = 16; f.lineType = InputField.LineType.SingleLine;
            return f;
        }
    }
}
