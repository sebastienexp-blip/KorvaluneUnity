using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>
    /// Écran de lancement : illustration du jeu en plein écran (léger mouvement de caméra), titre, braises qui montent,
    /// notes de version défilantes, état du contenu avec barre de téléchargement, bouton « Jouer ».
    /// Contenu : si une adresse de catalogue est configurée (contentUrl), le lanceur télécharge les paquets manquants ou
    /// mis à jour dans le stockage de l'appareil, avec reprise ; sinon tout le contenu est inclus dans l'APK.
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        public string nextScene = "Korvalune_Selection";
        [Tooltip("Adresse du catalogue de contenu (JSON). Vide : tout le contenu est inclus dans l'APK.")]
        public string contentUrl = "";

        RawImage bg; RectTransform bgRt; Image bar, barBack; Text status, notes, version; Button play; Text playText;
        readonly List<(RectTransform rt, Image im, float speed, float phase)> embers = new List<(RectTransform, Image, float, float)>();
        float t0; Font font; RectTransform root, notesContent;

        [System.Serializable] public class Pack { public string name, url; public long size; public string hash; }
        [System.Serializable] public class Catalog { public string version; public List<Pack> packs = new List<Pack>(); }

        void Start()
        {
            t0 = Time.time;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            StartCoroutine(UpdateContent());
        }

        // ------------------------------------------------------------------------------------------ interface
        void Build()
        {
            var go = new GameObject("Lanceur", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)go.transform;
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()) new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // illustration (rendu du jeu) plein écran, légèrement agrandie pour le mouvement lent
            bg = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bgRt = bg.rectTransform; bgRt.SetParent(root, false); bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            bg.texture = Resources.Load<Texture2D>("UI/Lanceur_Fond"); if (!bg.texture) bg.color = new Color(0.06f, 0.05f, 0.08f);
            bgRt.localScale = Vector3.one * 1.08f;
            // voiles : dégradé sombre en bas et sur la gauche (lisibilité)
            Shade(new Vector2(0, 0), new Vector2(1, 0.45f), new Color(0, 0, 0, 0.85f), true);
            Shade(new Vector2(0, 0), new Vector2(0.42f, 1), new Color(0, 0, 0, 0.55f), false);

            // braises
            for (int i = 0; i < 46; i++)
            {
                var e = Img(root, "Braise", UIStyle.Glow(), new Vector2(Random.value, 0), Vector2.zero, Vector2.one * Random.Range(6f, 16f));
                e.color = new Color(1f, Random.Range(0.45f, 0.7f), 0.2f, 0.8f);
                embers.Add((e.rectTransform, e, Random.Range(40f, 120f), Random.value * 50f));
            }

            // titre
            var title = Label(root, "Titre", "KORVALUNE", 150, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(90, -60), new Vector2(1200, 190));
            title.color = new Color(0.95f, 0.80f, 0.48f); title.fontStyle = FontStyle.Bold;
            var o = title.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.15f, 0.06f, 0.02f, 0.95f); o.effectDistance = new Vector2(4, -4);
            var sh = title.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(8, -10);
            var sub = Label(root, "Sous-titre", "Les ombres du Cœur Céleste", 40, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(100, -240), new Vector2(1000, 60));
            sub.color = new Color(0.86f, 0.80f, 0.70f); sub.fontStyle = FontStyle.Italic; sub.gameObject.AddComponent<Outline>().effectColor = Color.black;

            // notes de version (panneau défilant à droite)
            var np = Img(root, "Notes", UIStyle.Panel(), new Vector2(1, 1), new Vector2(-60, -60), new Vector2(720, 700)); np.type = Image.Type.Sliced; np.color = new Color(1, 1, 1, 0.9f);
            var nt = Label(np.rectTransform, "Titre", "Notes de version", 38, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(40, -26), new Vector2(640, 52)); nt.color = UIStyle.Gold;
            var view = new GameObject("Vue", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            view.SetParent(np.rectTransform, false); view.anchorMin = view.anchorMax = new Vector2(0, 1); view.pivot = new Vector2(0, 1); view.anchoredPosition = new Vector2(36, -90); view.sizeDelta = new Vector2(650, 580);
            view.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            notesContent = new GameObject("Contenu", typeof(RectTransform)).GetComponent<RectTransform>(); notesContent.SetParent(view, false);
            notesContent.anchorMin = notesContent.anchorMax = new Vector2(0, 1); notesContent.pivot = new Vector2(0, 1); notesContent.sizeDelta = new Vector2(630, 2400);
            notes = Label(notesContent, "Texte", NotesText(), 25, TextAnchor.UpperLeft, new Vector2(0, 1), Vector2.zero, new Vector2(630, 2400));
            notes.lineSpacing = 1.1f;
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = notesContent; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped;

            // contenu et téléchargement (bas)
            status = Label(root, "État", "Vérification du contenu…", 30, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(100, 150), new Vector2(1100, 44));
            status.gameObject.AddComponent<Outline>().effectColor = Color.black;
            barBack = Img(root, "Barre", UIStyle.Bar(), new Vector2(0, 0), new Vector2(100, 100), new Vector2(1100, 30)); barBack.type = Image.Type.Sliced; barBack.color = new Color(0.08f, 0.05f, 0.04f, 0.95f);
            bar = Img(barBack.rectTransform, "Remplissage", UIStyle.Bar(), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(1092, 22)); bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.color = new Color(0.95f, 0.70f, 0.30f); bar.fillAmount = 0;
            version = Label(root, "Version", "Version " + Application.version, 24, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(100, 40), new Vector2(600, 34)); version.color = new Color(0.7f, 0.65f, 0.58f);

            var pb = Img(root, "Jouer", UIStyle.Button(), new Vector2(1, 0), new Vector2(-90, 70), new Vector2(420, 130)); pb.raycastTarget = true; pb.type = Image.Type.Simple;
            playText = Label(pb.rectTransform, "T", "JOUER", 54, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 130)); playText.color = UIStyle.Gold; playText.fontStyle = FontStyle.Bold;
            play = pb.gameObject.AddComponent<Button>(); play.targetGraphic = pb; play.onClick.AddListener(() => SceneManager.LoadScene(nextScene)); play.interactable = false;
            pb.color = new Color(0.6f, 0.55f, 0.5f);
        }

        static string NotesText()
        {
            var ta = Resources.Load<TextAsset>("UI/NotesDeVersion"); if (!ta) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var l in ta.text.Split('\n'))
            {
                var s = l.TrimEnd('\r');
                if (s.StartsWith("## ")) sb.Append($"\n<color=#f0c878><b><size=30>{s.Substring(3)}</size></b></color>\n");
                else if (s.StartsWith("- ")) sb.Append($"<color=#c9a46a>•</color> {s.Substring(2)}\n");
                else sb.Append(s + "\n");
            }
            return sb.ToString().TrimStart('\n');
        }

        void Shade(Vector2 a, Vector2 b, Color c, bool vertical)
        {
            var t = new Texture2D(vertical ? 1 : 64, vertical ? 64 : 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) { float f = 1 - i / 63f; var col = new Color(c.r, c.g, c.b, c.a * f * f); if (vertical) t.SetPixel(0, i, col); else t.SetPixel(i, 0, col); }
            t.Apply();
            var im = new GameObject("Voile", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); var rt = im.rectTransform;
            rt.SetParent(root, false); rt.anchorMin = a; rt.anchorMax = b; rt.offsetMin = rt.offsetMax = Vector2.zero; im.texture = t; im.raycastTarget = false;
        }

        Image Img(RectTransform parent, string name, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); var rt = im.rectTransform;
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; im.sprite = s; im.raycastTarget = false; return im;
        }

        Text Label(RectTransform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); var rt = t.rectTransform;
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = box;
            t.font = font; t.fontSize = size; t.alignment = align; t.text = text; t.color = UIStyle.Parch; t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }

        void Update()
        {
            float t = Time.time - t0;
            if (bgRt) bgRt.anchoredPosition = new Vector2(Mathf.Sin(t * 0.05f) * 30f, Mathf.Sin(t * 0.037f) * 14f);   // mouvement de caméra très lent
            var h = root ? root.rect.height : 1080; var w = root ? root.rect.width : 1920;
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i]; float y = ((t + e.phase) * e.speed) % (h + 100f);
                e.rt.anchoredPosition = new Vector2(Mathf.Sin((t + e.phase) * 0.9f) * 30f, y - 50f);
                var c = e.im.color; c.a = 0.85f * Mathf.Clamp01(1 - y / h) * (0.6f + 0.4f * Mathf.Sin(t * 5 + i)); e.im.color = c;
            }
        }

        // ------------------------------------------------------------------------------------------ contenu
        IEnumerator UpdateContent()
        {
            yield return null;
            if (string.IsNullOrEmpty(contentUrl))
            {
                bar.fillAmount = 1; status.text = "Contenu à jour — tout est inclus dans cette version"; Ready(); yield break;
            }
            string dir = Path.Combine(Application.persistentDataPath, "contenu"); Directory.CreateDirectory(dir);
            Catalog cat = null;
            using (var req = UnityWebRequest.Get(contentUrl))
            {
                status.text = "Recherche des mises à jour…";
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    status.text = File.Exists(Path.Combine(dir, "catalogue.json")) ? "Hors ligne — contenu déjà téléchargé utilisé" : "Connexion impossible : le contenu sera téléchargé au prochain lancement";
                    bar.fillAmount = File.Exists(Path.Combine(dir, "catalogue.json")) ? 1 : 0; Ready(); yield break;
                }
                cat = JsonUtility.FromJson<Catalog>(req.downloadHandler.text);
                File.WriteAllText(Path.Combine(dir, "catalogue.json"), req.downloadHandler.text);
            }
            long total = 0, done = 0; var todo = new List<Pack>();
            foreach (var p in cat.packs)
            {
                var f = Path.Combine(dir, p.name);
                if (File.Exists(f) && new FileInfo(f).Length == p.size && File.Exists(f + ".hash") && File.ReadAllText(f + ".hash") == p.hash) continue;
                todo.Add(p); total += p.size;
            }
            for (int i = 0; i < todo.Count; i++)
            {
                var p = todo[i]; var f = Path.Combine(dir, p.name); var part = f + ".part";
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                using (var req = new UnityWebRequest(p.url, "GET"))
                {
                    req.downloadHandler = new DownloadHandlerFile(part, have > 0);
                    if (have > 0) req.SetRequestHeader("Range", $"bytes={have}-");          // reprise
                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        long now = done + have + (long)req.downloadedBytes;
                        bar.fillAmount = total > 0 ? (float)now / total : 1;
                        status.text = $"Téléchargement du jeu : {Mo(now)} / {Mo(total)}  ({i + 1}/{todo.Count} — {p.name})";
                        yield return null;
                    }
                    if (req.result != UnityWebRequest.Result.Success) { status.text = "Téléchargement interrompu — il reprendra au prochain lancement"; Ready(); yield break; }
                }
                if (File.Exists(f)) File.Delete(f);
                File.Move(part, f); File.WriteAllText(f + ".hash", p.hash); done += p.size;
            }
            bar.fillAmount = 1; status.text = $"Contenu à jour (version {cat.version})"; Ready();
        }

        static string Mo(long b) => $"{b / 1048576.0:0.0} Mo";

        void Ready()
        {
            play.interactable = true; play.targetGraphic.color = Color.white; playText.color = UIStyle.Gold;
            if (AutoTest.Enabled) { AutoTest.Log("lanceur prêt : on joue"); Invoke(nameof(AutoPlay), 6f); }
        }
        void AutoPlay() => SceneManager.LoadScene(nextScene);
    }
}
