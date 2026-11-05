using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Korvalune.Boot
{
    /// <summary>
    /// Démarrage du jeu (seul code « figé » dans l'APK, avec HybridCLR). Sur Android, il :
    /// 1. regarde la dernière version publiée (release « apk-derniere », version.txt) ;
    /// 2. si elle est plus récente que le code déjà présent, télécharge son code (contenu-N/code.json : bibliothèque du jeu
    ///    et métadonnées), vérifie chaque fichier (SHA-256) et le garde dans le stockage du jeu ;
    /// 3. charge ce code (interpréteur HybridCLR) puis ouvre l'écran de lancement, qui télécharge le contenu de la même version.
    /// Ainsi le jeu se met à jour entièrement depuis le jeu, sans réinstaller d'APK. Hors ligne : la dernière version
    /// téléchargée, sinon celle livrée dans l'APK. Sur ordinateur et dans l'éditeur, le code est déjà là : on lance directement.
    /// ⚠ Ce fichier et GameVersion.cs sont compilés dans l'APK : s'ils changent (ou le paquet HybridCLR, ou la version
    /// d'Unity), mettre le numéro de la construction suivante dans .github/apk-min.txt (le nouvel APK devient obligatoire).
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        const string GameAssembly = "Korvalune.Runtime", LauncherType = "Korvalune.Game.Launcher";
        static Assembly game;

        Text status; Image bar; GameObject ui;

        void Start()
        {
            if (game != null) { Spawn(); return; }            // retour à l'écran de lancement : le code est déjà chargé
            game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == GameAssembly);
            if (game != null) { Spawn(); return; }            // ordinateur, éditeur : code compris dans le programme
            StartCoroutine(LoadHotCode());
        }

        void Spawn()
        {
            var t = game.GetType(LauncherType);
            if (t == null) { Fail("Lanceur introuvable dans le code du jeu"); return; }
            if (ui) Destroy(ui);
            new GameObject("Lanceur").AddComponent(t);
        }

        static string CacheDir => Path.Combine(Application.persistentDataPath, "code");
        static string Url(string path) => path.Contains("://") ? path : "file://" + path;

        IEnumerator LoadHotCode()
        {
            BuildUi();
            Status("Recherche des mises à jour…", 0.05f);
            int apk = GameVersion.ApkBuild;
            // code livré avec l'APK (StreamingAssets/code) et code déjà téléchargé
            CodeManifest embedded = null, cached = null;
            yield return ReadJson(Path.Combine(Application.streamingAssetsPath, "code", "code.json"), m => embedded = m);
            yield return ReadJson(Path.Combine(CacheDir, "code.json"), m => cached = m);
            if (cached != null && (!Complete(cached, CacheDir) || cached.apkMin > apk)) cached = null;
            // dernière version publiée
            int remote = 0;
            using (var r = UnityWebRequest.Get(GameVersion.Releases + "apk-derniere/version.txt?t=" + DateTime.UtcNow.Ticks))
            {
                r.timeout = 8; yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success) int.TryParse(r.downloadHandler.text.Trim(), out remote);
            }
            int local = Math.Max(cached?.version ?? 0, embedded?.version ?? 0);
            if (remote > local)
            {
                // téléchargement du code de la nouvelle version (quelques Mo), fichier par fichier, vérifié
                CodeManifest next = null;
                yield return ReadJson(GameVersion.ContentBase(remote) + "code.json", m => next = m);
                if (next != null && next.apkMin <= apk)
                {
                    var tmp = CacheDir + ".nouveau";
                    try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); Directory.CreateDirectory(tmp); } catch { }
                    var files = next.hot.Concat(next.aot).ToArray(); long total = files.Sum(f => f.size), done = 0; bool ok = true;
                    foreach (var f in files)
                    {
                        // fichier identique déjà téléchargé (métadonnées le plus souvent inchangées) : simple copie
                        var old = Path.Combine(CacheDir, f.name);
                        if (cached != null && File.Exists(old) && Sha(File.ReadAllBytes(old)) == f.hash) { File.Copy(old, Path.Combine(tmp, f.name), true); done += f.size; continue; }
                        using (var r = UnityWebRequest.Get(GameVersion.ContentBase(remote) + f.name))
                        {
                            r.timeout = 120; var op = r.SendWebRequest();
                            while (!op.isDone) { Status($"Mise à jour du jeu vers la version 0.1.{remote}…", 0.1f + 0.8f * (done + (long)(r.downloadProgress * f.size)) / Mathf.Max(1, total)); yield return null; }
                            if (r.result != UnityWebRequest.Result.Success || Sha(r.downloadHandler.data) != f.hash) { ok = false; break; }
                            File.WriteAllBytes(Path.Combine(tmp, f.name), r.downloadHandler.data); done += f.size;
                        }
                    }
                    if (ok)
                    {
                        File.WriteAllText(Path.Combine(tmp, "code.json"), JsonUtility.ToJson(next));
                        try { if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true); Directory.Move(tmp, CacheDir); cached = next; }
                        catch (Exception e) { Debug.LogWarning("[Korvalune] mise à jour du code : " + e.Message); }
                    }
                    else Debug.LogWarning("[Korvalune] téléchargement du code interrompu : on garde la version présente");
                }
                else if (next != null) Debug.Log($"[Korvalune] la version {remote} demande un APK plus récent (≥ {next.apkMin})");
            }
            // version la plus récente disponible sur l'appareil
            bool fromCache = cached != null && (embedded == null || cached.version > embedded.version);
            var use = fromCache ? cached : embedded;
            string dir = fromCache ? CacheDir : Path.Combine(Application.streamingAssetsPath, "code");
            if (use == null) { Fail("Le code du jeu est introuvable : connectez-vous à Internet puis relancez le jeu."); yield break; }

            Status($"Chargement de Korvalune 0.1.{use.version}…", 0.92f);
            // métadonnées complémentaires (génériques des bibliothèques du moteur), puis la bibliothèque du jeu
            foreach (var f in use.aot)
            {
                byte[] b = null; yield return ReadBytes(Path.Combine(dir, f.name), x => b = x);
                if (b == null) continue;
                var err = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(b, HybridCLR.HomologousImageMode.SuperSet);
                if (err != HybridCLR.LoadImageErrorCode.OK) Debug.LogWarning($"[Korvalune] métadonnées {f.name} : {err}");
            }
            foreach (var f in use.hot)
            {
                byte[] b = null; yield return ReadBytes(Path.Combine(dir, f.name), x => b = x);
                if (b == null) { Fail("Le code du jeu est abîmé : relancez le jeu (il sera retéléchargé)."); TryDeleteCache(); yield break; }
                try { var a = Assembly.Load(b); if (a.GetName().Name == GameAssembly) game = a; }
                catch (Exception e) { Fail("Le code du jeu n'a pas pu être chargé : " + e.Message); TryDeleteCache(); yield break; }
            }
            if (game == null) { Fail("Le code du jeu n'a pas pu être chargé."); yield break; }
            GameVersion.Build = use.version; GameVersion.HotLoaded = true;
            RunInitializers(game);
            Status("", 1f);
            Spawn();
        }

        /// <summary>Le code chargé ainsi n'a pas ses méthodes [RuntimeInitializeOnLoadMethod] appelées par Unity : on le fait.</summary>
        static void RunInitializers(Assembly a)
        {
            Type[] types; try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    if (m.GetParameters().Length == 0 && m.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                        try { m.Invoke(null, null); } catch (Exception e) { Debug.LogWarning($"[Korvalune] init {t.Name}.{m.Name} : {e.InnerException?.Message ?? e.Message}"); }
        }

        static bool Complete(CodeManifest m, string dir) => m.hot.Concat(m.aot).All(f => File.Exists(Path.Combine(dir, f.name)) && new FileInfo(Path.Combine(dir, f.name)).Length == f.size);
        static void TryDeleteCache() { try { if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true); } catch { } }

        static IEnumerator ReadBytes(string path, Action<byte[]> done)
        {
            using (var r = UnityWebRequest.Get(Url(path)))
            {
                r.timeout = 20; yield return r.SendWebRequest();
                done(r.result == UnityWebRequest.Result.Success ? r.downloadHandler.data : null);
            }
        }
        static IEnumerator ReadJson(string path, Action<CodeManifest> done)
        {
            byte[] b = null; yield return ReadBytes(path, x => b = x);
            CodeManifest m = null;
            if (b != null) try { m = JsonUtility.FromJson<CodeManifest>(System.Text.Encoding.UTF8.GetString(b)); } catch { m = null; }
            done(m != null && m.version > 0 ? m : null);
        }
        static string Sha(byte[] b) { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }

        // ------------------------------------------------------------------------------------------ écran d'attente
        void BuildUi()
        {
            ui = new GameObject("Démarrage", typeof(Canvas), typeof(CanvasScaler));
            var cv = ui.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 100;
            var sc = ui.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)ui.transform;
            var bg = New<RawImage>(root, "Fond", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.texture = Resources.Load<Texture2D>("UI/Lanceur_Fond"); bg.color = bg.texture ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.05f, 0.04f, 0.07f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = New<Text>(root, "Titre", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1400, 180));
            title.font = font; title.fontSize = 130; title.fontStyle = FontStyle.Bold; title.alignment = TextAnchor.MiddleCenter; title.text = "KORVALUNE"; title.color = new Color(0.95f, 0.8f, 0.48f);
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0.15f, 0.06f, 0.02f, 0.95f);
            var back = New<Image>(root, "Barre", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 16)); back.color = new Color(0, 0, 0, 0.7f);
            bar = New<Image>((RectTransform)back.transform, "Remplissage", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero); bar.color = new Color(0.95f, 0.72f, 0.35f);
            ((RectTransform)bar.transform).pivot = new Vector2(0, 0.5f);
            status = New<Text>(root, "État", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(1600, 60));
            status.font = font; status.fontSize = 34; status.alignment = TextAnchor.MiddleCenter; status.color = new Color(0.9f, 0.85f, 0.75f);
            status.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }
        static T New<T>(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size; return go.GetComponent<T>();
        }
        void Status(string s, float p)
        {
            if (status) status.text = s;
            if (bar) { var rt = (RectTransform)bar.transform; rt.anchorMax = new Vector2(Mathf.Clamp01(p), 1); rt.offsetMax = Vector2.zero; rt.offsetMin = Vector2.zero; }
        }
        void Fail(string s)
        {
            Debug.LogError("[Korvalune] " + s);
            if (!ui) BuildUi();
            Status(s, 0);
            // toucher l'écran : nouvel essai
            var btn = ui.AddComponent<Button>(); var img = ui.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Destroy(ui); ui = null; StopAllCoroutines(); Start(); });
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()) new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }
}
