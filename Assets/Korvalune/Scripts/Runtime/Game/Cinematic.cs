using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Cinématiques des chapitres, jouées dans le moteur avec les vrais décors et créatures de la région :
    /// plans de caméra enchaînés (plan d'ensemble aérien, survol jusqu'au lieu de l'objectif, orbite autour du chef de
    /// la région, plan rapproché du héros), bandes noires de cinéma, fondus au noir, carton de titre du chapitre et
    /// sous-titres qui s'écrivent. Pendant la cinématique le jeu est figé (ennemis, héros, interface masquée). Un
    /// toucher la passe.</summary>
    public class Cinematic : MonoBehaviour
    {
        public static bool Active { get; private set; }

        struct Pose { public Vector3 pos, look; public float fov; }
        class Shot { public float dur; public System.Func<float, Pose> at; public string line; }

        readonly List<Shot> shots = new List<Shot>();
        string titleTop, titleMain; bool endCard;
        System.Action onDone;
        Camera cam; float camFov;
        RectTransform barTop, barBottom; Image fader; Text title, subtitle, sub, hint;
        bool skip, finished; readonly List<Canvas> hidden = new List<Canvas>();
        Font font;

        // ======================================================================================== construction des plans
        static float Ease(float t) => t * t * (3f - 2f * t);
        static Vector3 Above(Vector3 p, float min)
        {
            var g = GameBootstrap.Ground(p); if (p.y < g.y + min) p.y = g.y + min; return p;
        }

        static Transform Hero() { var p = FindFirstObjectByType<PlayerController>(); return p ? p.transform : null; }

        /// <summary>Ennemi vivant d'un type, le plus proche d'un point (le chef de la région, près du hameau).</summary>
        static Transform EnemyNear(string id, Vector3 at)
        {
            Transform best = null; float bd = 1e9f;
            foreach (var h in Health.All)
            {
                if (h.Dead || h.faction != Faction.Enemy) continue;
                var ai = h.GetComponent<EnemyAI>(); if (!ai || ai.type == null || (id != null && ai.type.id != id)) continue;
                float d = (h.transform.position - at).sqrMagnitude; if (d < bd) { bd = d; best = h.transform; }
            }
            return best;
        }

        static Vector3 FarPlace(Quest q, Vector3 from)
        {
            Vector3 best = from + Vector3.forward * 40f; float bd = -1;
            foreach (var o in q.steps)
            {
                if (o.place == Vector3.zero) continue;
                float d = (o.place - from).sqrMagnitude; if (d > bd) { bd = d; best = o.place; }
            }
            return best;
        }

        static string LastEnemy(Quest q) { string id = null; foreach (var o in q.steps) if (!string.IsNullOrEmpty(o.enemyId)) id = o.enemyId; return id; }

        public static void ChapterIntro(Quest q)
        {
            var ch = q.chapter; var hero = Hero(); if (ch == null || !hero) return;
            var c = Create();
            c.titleTop = $"Chapitre {Story.ChapterNumber(ch)}"; c.titleMain = ch.title;
            Vector3 H = hero.position, T = FarPlace(q, H);
            var dir = T - H; dir.y = 0; if (dir.sqrMagnitude < 1) dir = Vector3.forward; dir.Normalize();
            var side = Vector3.Cross(Vector3.up, dir);
            var chief = EnemyNear(LastEnemy(q), T);
            var gens = new List<System.Func<float, Pose>>
            {
                // 1. plan d'ensemble : très haut derrière le héros, la caméra glisse vers la région
                t => new Pose { pos = Above(Vector3.Lerp(H - dir * 34 + Vector3.up * 52 + side * 10, H + dir * 14 + Vector3.up * 36 - side * 6, t), 8),
                                look = Vector3.Lerp(H + dir * 45, H + dir * 70, t), fov = 42 },
                // 2. survol jusqu'au lieu de l'objectif (hameau en ruine, porte du château…)
                t => new Pose { pos = Above(Vector3.Lerp(T - dir * 52 + Vector3.up * 30 + side * 14, T - dir * 20 + Vector3.up * 13 + side * 5, t), 5),
                                look = T + Vector3.up * 2f, fov = 40 },
                // 3. orbite lente autour du chef de la région (ou du lieu)
                t => { var C = chief ? chief.position : T; float a = Mathf.Lerp(-35f, 55f, t) * Mathf.Deg2Rad; float r = chief ? 7.5f : 14f;
                       var o = (dir * -Mathf.Cos(a) + side * Mathf.Sin(a)) * r;
                       return new Pose { pos = Above(C + o + Vector3.up * (chief ? 2.4f : 6f), 1.5f), look = C + Vector3.up * (chief ? 1.6f : 1f), fov = 34 }; },
                // 4. plan rapproché du héros, contre-plongée qui se resserre
                t => { var f = hero.forward; return new Pose { pos = Above(H + f * Mathf.Lerp(4.2f, 2.8f, t) + Vector3.up * Mathf.Lerp(1.1f, 1.4f, t) + hero.right * 0.9f, 0.6f),
                                                               look = H + Vector3.up * 1.55f, fov = 32 }; },
            };
            c.Build(ch.intro, gens, false);
        }

        public static void ChapterOutro(Quest q, System.Action done)
        {
            var ch = q.chapter; var hero = Hero(); if (ch == null || !hero) { done?.Invoke(); return; }
            var c = Create(); c.onDone = done;
            c.titleTop = $"Chapitre {Story.ChapterNumber(ch)} terminé"; c.titleMain = ch.title; c.endCard = true;
            Vector3 H = hero.position; var f = hero.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            var side = Vector3.Cross(Vector3.up, f);
            var gens = new List<System.Func<float, Pose>>
            {
                // 1. orbite autour du héros victorieux
                t => { float a = Mathf.Lerp(20f, 140f, t) * Mathf.Deg2Rad; var o = (f * Mathf.Cos(a) + side * Mathf.Sin(a)) * Mathf.Lerp(6.5f, 5f, t);
                       return new Pose { pos = Above(H + o + Vector3.up * 2.2f, 1f), look = H + Vector3.up * 1.4f, fov = 36 }; },
                // 2. la grue s'élève au-dessus du héros
                t => new Pose { pos = Above(Vector3.Lerp(H - f * 6 + Vector3.up * 2.5f, H - f * 22 + Vector3.up * 26 + side * 8, Ease(t)), 1.5f), look = H + Vector3.up * Mathf.Lerp(1.4f, 0f, t), fov = 40 },
                // 3. la région entière, très haut, qui pivote lentement
                t => { float a = Mathf.Lerp(0f, 40f, t) * Mathf.Deg2Rad; var o = (-f * Mathf.Cos(a) + side * Mathf.Sin(a)) * 46f;
                       return new Pose { pos = Above(H + o + Vector3.up * 44f, 10f), look = H + f * 20f, fov = 44 }; },
            };
            c.Build(ch.outro, gens, true);
        }

        static Cinematic Create()
        {
            var go = new GameObject("Cinématique", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 400;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            return go.AddComponent<Cinematic>();
        }

        void Build(string[] lines, List<System.Func<float, Pose>> gens, bool outro)
        {
            lines = lines ?? new string[0];
            int n = Mathf.Max(lines.Length, gens.Count);
            for (int i = 0; i < n; i++)
            {
                string line = i < lines.Length ? lines[i] : null;
                float dur = line == null ? 4.5f : Mathf.Clamp(2.4f + line.Length * 0.055f, 4.8f, 9f);
                shots.Add(new Shot { dur = dur, at = gens[Mathf.Min(i, gens.Count - 1)], line = line });
            }
            // plusieurs lignes sur le dernier plan : on le prolonge en continuant le mouvement
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = (RectTransform)transform;
            fader = Box(root, "Fondu", Vector2.zero, Vector2.one, Color.black); fader.raycastTarget = true;
            barTop = Box(root, "BandeHaut", new Vector2(0, 1), new Vector2(1, 1), Color.black).rectTransform; barTop.pivot = new Vector2(0.5f, 1); barTop.sizeDelta = new Vector2(0, 0);
            barBottom = Box(root, "BandeBas", new Vector2(0, 0), new Vector2(1, 0), Color.black).rectTransform; barBottom.pivot = new Vector2(0.5f, 0); barBottom.sizeDelta = new Vector2(0, 0);
            title = Txt(root, "Chapitre", 40, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1600, 60)); title.color = new Color(0.93f, 0.74f, 0.42f, 0);
            subtitle = Txt(root, "Titre", 86, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1800, 120)); subtitle.fontStyle = FontStyle.Bold; subtitle.color = new Color(0.98f, 0.92f, 0.8f, 0);
            sub = Txt(root, "SousTitre", 38, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1560, 110)); sub.color = new Color(0.98f, 0.94f, 0.86f, 1);
            hint = Txt(root, "Passer", 26, new Vector2(1, 1), new Vector2(-170, -40), new Vector2(320, 40)); hint.text = "Toucher pour passer ›"; hint.alignment = TextAnchor.MiddleRight; hint.color = new Color(0.9f, 0.82f, 0.65f, 0);
            var btn = fader.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(() => skip = true);
            StartCoroutine(Run(outro));
        }

        Image Box(RectTransform parent, string name, Vector2 amin, Vector2 amax, Color c)
        {
            var im = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); var rt = im.rectTransform;
            rt.SetParent(parent, false); rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = rt.offsetMax = Vector2.zero; im.color = c; im.raycastTarget = false; return im;
        }

        Text Txt(RectTransform parent, string name, int size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); var rt = t.rectTransform;
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, anchor.y); rt.anchoredPosition = pos; rt.sizeDelta = box;
            t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2, -2);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
            return t;
        }

        // ======================================================================================== déroulé
        void Begin()
        {
            Active = true;
            cam = Camera.main;
            if (cam) camFov = cam.fieldOfView;
            hidden.Clear();
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv.gameObject != gameObject && cv.enabled && cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay) { cv.enabled = false; hidden.Add(cv); }
            if (WorldMap.Current) WorldMap.Current.Close();
        }

        void End()
        {
            if (finished) return; finished = true;
            foreach (var cv in hidden) if (cv) cv.enabled = true;
            if (cam) cam.fieldOfView = camFov;
            Active = false;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy() { if (!finished) { foreach (var cv in hidden) if (cv) cv.enabled = true; Active = false; } }

        void Apply(Pose p)
        {
            if (!cam) return;
            cam.transform.position = p.pos; cam.transform.rotation = Quaternion.LookRotation((p.look - p.pos).normalized, Vector3.up); cam.fieldOfView = p.fov;
        }

        void Bars(float k)
        {
            float h = 1080f * 0.115f * k;
            barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h);
        }

        IEnumerator Fade(float from, float to, float d)
        {
            for (float t = 0; t < d && !skip; t += Time.unscaledDeltaTime) { fader.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Ease(t / d))); yield return null; }
            fader.color = new Color(0, 0, 0, to);
        }

        IEnumerator Run(bool outro)
        {
            Begin();
            fader.color = new Color(0, 0, 0, outro ? 0f : 1f);
            if (outro) { yield return Fade(0, 1, 0.8f); }
            if (shots.Count > 0) Apply(shots[0].at(0));
            Bars(1);
            Sound.UI("sfx_quete", 0.6f);
            // carton de titre sur le premier plan (ouverture)
            float t0 = Time.unscaledTime;
            for (int i = 0; i < shots.Count && !skip; i++)
            {
                var s = shots[i];
                if (i > 0) { yield return Fade(0, 1, 0.45f); }
                Apply(s.at(0));
                StartCoroutine(Fade(1, 0, i == 0 ? 1.4f : 0.5f));
                sub.text = "";
                for (float t = 0; t < s.dur && !skip; t += Time.unscaledDeltaTime)
                {
                    float k = t / s.dur; Apply(s.at(Ease(Mathf.Clamp01(k))));
                    // sous-titre qui s'écrit, après un court temps
                    if (s.line != null) { int nch = Mathf.Clamp(Mathf.FloorToInt((t - 0.5f) * 42f), 0, s.line.Length); sub.text = s.line.Substring(0, nch); }
                    // carton de titre (premier plan de l'ouverture)
                    if (i == 0 && !outro)
                    {
                        float a = Mathf.Clamp01((t - 0.6f) / 0.8f) * Mathf.Clamp01((s.dur - 0.6f - t) / 0.8f);
                        title.text = titleTop; subtitle.text = titleMain;
                        title.color = new Color(title.color.r, title.color.g, title.color.b, a); subtitle.color = new Color(subtitle.color.r, subtitle.color.g, subtitle.color.b, a);
                        sub.color = new Color(sub.color.r, sub.color.g, sub.color.b, 1 - a);
                    }
                    else sub.color = new Color(sub.color.r, sub.color.g, sub.color.b, 1);
                    hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, Mathf.Clamp01((Time.unscaledTime - t0 - 1.5f) * 0.8f) * 0.7f);
                    yield return null;
                }
                title.color = new Color(title.color.r, title.color.g, title.color.b, 0); subtitle.color = new Color(subtitle.color.r, subtitle.color.g, subtitle.color.b, 0);
            }
            sub.text = "";
            // carton de fin de chapitre
            if (endCard && !skip)
            {
                yield return Fade(fader.color.a, 1, 0.6f);
                title.text = titleTop; subtitle.text = titleMain;
                for (float t = 0; t < 3.2f && !skip; t += Time.unscaledDeltaTime)
                {
                    float a = Mathf.Clamp01(t / 0.7f) * Mathf.Clamp01((3.2f - t) / 0.7f);
                    title.color = new Color(title.color.r, title.color.g, title.color.b, a); subtitle.color = new Color(subtitle.color.r, subtitle.color.g, subtitle.color.b, a);
                    yield return null;
                }
            }
            // retour au jeu : fondu au noir, la caméra de jeu reprend, fondu d'ouverture
            skip = false;
            yield return Fade(fader.color.a, 1, 0.35f);
            title.color = subtitle.color = new Color(1, 1, 1, 0); hint.color = new Color(1, 1, 1, 0);
            Bars(0);
            Active = false;                                  // la caméra de jeu se replace pendant le fondu
            foreach (var cv in hidden) if (cv) cv.enabled = true;
            if (cam) cam.fieldOfView = camFov;
            yield return null; yield return null;
            fader.raycastTarget = false;
            yield return Fade(1, 0, 0.6f);
            End();
        }
    }
}
