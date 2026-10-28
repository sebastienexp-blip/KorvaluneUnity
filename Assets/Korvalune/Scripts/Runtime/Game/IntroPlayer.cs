using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Korvalune.Game
{
    /// <summary>Cinématique d'introduction (StreamingAssets/Intro.mp4) jouée en plein écran par-dessus le lanceur : au premier
    /// lancement, puis à la demande (« Revoir l'introduction »). Un toucher la passe ; en cas d'erreur de lecture, on la saute.</summary>
    public class IntroPlayer : MonoBehaviour
    {
        const string Key = "korvalune.intro.vue";
        public static bool Seen { get => PlayerPrefs.GetInt(Key, 0) == 1; set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static string Url => Path.Combine(Application.streamingAssetsPath, "Intro.mp4");
        public static bool Playing { get; private set; }

        VideoPlayer vp; RenderTexture rt; RawImage screen; Text hint; float shownAt, prepareDeadline; bool done;
        System.Action onDone;

        public static IntroPlayer Show(Font font, System.Action onDone = null)
        {
            var go = new GameObject("Introduction", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 500;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var ip = go.AddComponent<IntroPlayer>(); ip.onDone = onDone; ip.Build(font); return ip;
        }

        void Build(Font font)
        {
            Playing = true; shownAt = Time.unscaledTime; prepareDeadline = Time.unscaledTime + 10f;
            var root = (RectTransform)transform;
            screen = new GameObject("Écran", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var srt = screen.rectTransform; srt.SetParent(root, false); srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            screen.color = Color.black; screen.raycastTarget = true;
            var fit = screen.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9f;
            var b = screen.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(Skip);

            hint = new GameObject("Passer", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var hrt = hint.rectTransform; hrt.SetParent(root, false); hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 0); hrt.anchoredPosition = new Vector2(-50, 40); hrt.sizeDelta = new Vector2(600, 50);
            hint.font = font; hint.fontSize = 30; hint.alignment = TextAnchor.LowerRight; hint.text = "Toucher pour passer ›"; hint.color = new Color(0.95f, 0.85f, 0.65f, 0f); hint.raycastTarget = false;
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            rt = new RenderTexture(1920, 1080, 0) { name = "Introduction" };
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false; vp.isLooping = false; vp.skipOnDrop = true; vp.waitForFirstFrame = true;
            vp.source = VideoSource.Url; vp.url = Url;
            vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.prepareCompleted += p => { screen.texture = rt; screen.color = Color.white; p.Play(); };
            vp.loopPointReached += _ => Finish();
            vp.errorReceived += (_, msg) => { Debug.LogWarning("[Korvalune] introduction illisible : " + msg); Finish(); };
            vp.Prepare();
        }

        void Update()
        {
            if (done) return;
            float t = Time.unscaledTime - shownAt;
            if (hint) hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, Mathf.Clamp01((t - 1.5f) / 1f) * (0.75f + 0.25f * Mathf.Sin(t * 2.5f)));
            if (vp && !vp.isPrepared && Time.unscaledTime > prepareDeadline) Finish();      // fichier absent ou lecteur indisponible
            if (Input.GetKeyDown(KeyCode.Escape)) Skip();
        }

        void Skip() { if (Time.unscaledTime - shownAt > 0.6f) Finish(); }

        void Finish()
        {
            if (done) return; done = true; Playing = false; Seen = true;
            if (vp) vp.Stop();
            if (rt) rt.Release();
            onDone?.Invoke();
            Destroy(gameObject);
        }
    }
}
