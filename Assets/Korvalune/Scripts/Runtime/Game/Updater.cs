using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Mise à jour depuis le jeu : au lancement, compare la version installée à la dernière version publiée ; si une
    /// version plus récente existe, un bandeau propose de la télécharger. Sur Android, le téléchargement passe par le
    /// gestionnaire de téléchargements du système (barre de progression ici, notification là-bas), puis l'installateur
    /// s'ouvre : la nouvelle version s'installe par-dessus l'ancienne (même signature), personnages et réglages conservés.</summary>
    public class Updater : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN
        // version Windows : publiée dans « windows-derniere » ; la mise à jour passe par le lanceur Korvalune
        const string Base = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/windows-derniere/";
        const string Page = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/tag/windows-derniere";
#else
        const string Base = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/apk-derniere/";
        const string Page = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/tag/apk-derniere";
#endif
        /// <summary>Lanceur Windows installé (il télécharge et installe les nouvelles versions du jeu).</summary>
        static string LauncherExe => System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Korvalune", "KorvaluneLauncher.exe");
        public static int InstalledBuild
        {
            get { var v = Application.version.Split('.'); return v.Length > 0 && int.TryParse(v[v.Length - 1], out var n) ? n : 0; }
        }

        Text label; Image bg; int remote; long downloadId = -1; bool busy;
        AndroidJavaObject dm;

        public static void Attach(RectTransform root, Font font)
        {
            var go = new GameObject("Mise à jour", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(96, 192); rt.sizeDelta = new Vector2(1000, 54);   // en bas à gauche, au-dessus de l'état du contenu
            var u = go.AddComponent<Updater>(); u.bg = go.GetComponent<Image>(); u.bg.sprite = UIStyle.Slot(); u.bg.type = Image.Type.Sliced; u.bg.color = new Color(0.9f, 0.7f, 0.35f);
            var t = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(rt, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = 26; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            u.label = t;
            var b = go.AddComponent<Button>(); b.targetGraphic = u.bg; b.onClick.AddListener(u.Click);
            // (pas de SetActive(false) ici : une coroutine ne démarre pas sur un objet inactif — la vérification ne se
            // faisait jamais et le bandeau de mise à jour n'apparaissait pas)
            u.StartCoroutine(u.Check());
        }

        IEnumerator Check()
        {
            gameObject.SetActive(true); label.text = ""; bg.enabled = false;
            using (var r = UnityWebRequest.Get(Base + "version.txt"))
            {
                r.timeout = 8;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success || !int.TryParse(r.downloadHandler.text.Trim(), out remote))
                { label.text = $"<size=20><color=#a08c6c>Version 0.1.{InstalledBuild} — vérification des mises à jour impossible (hors ligne)</color></size>"; yield break; }
            }
            if (remote <= InstalledBuild) { label.text = $"<size=20><color=#8fd08f>Version 0.1.{InstalledBuild} — à jour</color></size>"; yield break; }
            bg.enabled = true;
            label.text = Application.platform == RuntimePlatform.WindowsPlayer
                ? $"Mise à jour disponible (version 0.1.{remote}) — cliquer pour l'installer avec le lanceur"
                : $"Mise à jour disponible (version 0.1.{remote}) — toucher pour télécharger";
        }

        void Click()
        {
            if (busy || remote <= InstalledBuild) return;              // déjà à jour : rien à télécharger
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                // Windows : le lanceur télécharge la nouvelle version, remplace l'ancienne (personnages conservés) et relance le jeu
                try
                {
                    if (System.IO.File.Exists(LauncherExe))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LauncherExe, "--mise-a-jour") { UseShellExecute = true });
                        Application.Quit(); return;
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("[Korvalune] lanceur : " + e.Message); }
                Application.OpenURL(Page); return;
            }
            if (Application.platform != RuntimePlatform.Android) { Application.OpenURL(Page); return; }
            if (installAct != null) { Install(); return; }                // déjà téléchargée : on relance l'installation
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                dm = act.Call<AndroidJavaObject>("getSystemService", "download");
                using var uriC = new AndroidJavaClass("android.net.Uri");
                var uri = uriC.CallStatic<AndroidJavaObject>("parse", Base + "Korvalune.apk");
                var req = new AndroidJavaObject("android.app.DownloadManager$Request", uri);
                req.Call<AndroidJavaObject>("setTitle", $"Korvalune 0.1.{remote}");
                req.Call<AndroidJavaObject>("setDescription", "Mise à jour du jeu");
                req.Call<AndroidJavaObject>("setMimeType", "application/vnd.android.package-archive");
                req.Call<AndroidJavaObject>("setNotificationVisibility", 1);          // visible, puis « terminé »
                req.Call<AndroidJavaObject>("setDestinationInExternalFilesDir", act, "Download", $"Korvalune-0.1.{remote}.apk");
                downloadId = dm.Call<long>("enqueue", req);
                busy = true; StartCoroutine(Follow(act));
            }
            catch (System.Exception e) { Debug.LogWarning("[Korvalune] mise à jour : " + e.Message); Application.OpenURL(Page); }
        }

        IEnumerator Follow(AndroidJavaObject act)
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                int status = 0; long done = 0, total = 0;
                try
                {
                    var q = new AndroidJavaObject("android.app.DownloadManager$Query");
                    q.Call<AndroidJavaObject>("setFilterById", new long[] { downloadId });
                    var c = dm.Call<AndroidJavaObject>("query", q);
                    if (c.Call<bool>("moveToFirst"))
                    {
                        status = c.Call<int>("getInt", c.Call<int>("getColumnIndex", "status"));
                        done = c.Call<long>("getLong", c.Call<int>("getColumnIndex", "bytes_so_far"));
                        total = c.Call<long>("getLong", c.Call<int>("getColumnIndex", "total_size"));
                    }
                    c.Call("close");
                }
                catch (System.Exception e) { Debug.LogWarning("[Korvalune] suivi : " + e.Message); }
                if (status == 8) break;                                    // STATUS_SUCCESSFUL
                if (status == 16) { label.text = "Échec du téléchargement — toucher pour réessayer"; busy = false; yield break; }
                label.text = total > 0 ? $"Téléchargement… {100 * done / total} %  ({done / 1048576} / {total / 1048576} Mo)" : "Téléchargement…";
            }
            label.text = "Téléchargé — installation…";
            installAct = act;
            Install();
            busy = false;
        }

        AndroidJavaObject installAct; bool waitingPermission;

        /// <summary>Ouvre l'installateur d'Android sur l'APK téléchargé : il remplace la version installée (même signature,
        /// personnages et réglages conservés) après un seul toucher sur « Mettre à jour ». La première fois, Android demande
        /// d'autoriser Korvalune à installer des applications : on ouvre ce réglage puis on reprend au retour dans le jeu.</summary>
        void Install()
        {
            var act = installAct; if (act == null) return;
            try
            {
                using var ver = new AndroidJavaClass("android.os.Build$VERSION");
                if (ver.GetStatic<int>("SDK_INT") >= 26)
                {
                    var pm = act.Call<AndroidJavaObject>("getPackageManager");
                    if (!pm.Call<bool>("canRequestPackageInstalls"))
                    {
                        using var uriC0 = new AndroidJavaClass("android.net.Uri");
                        var pkg = uriC0.CallStatic<AndroidJavaObject>("parse", "package:" + act.Call<string>("getPackageName"));
                        var st = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_UNKNOWN_APP_SOURCES", pkg);
                        st.Call<AndroidJavaObject>("addFlags", 0x10000000);
                        act.Call("startActivity", st);
                        waitingPermission = true;
                        label.text = "Autorise « Korvalune » à installer la mise à jour, puis reviens dans le jeu";
                        return;
                    }
                }
                var uri = dm.Call<AndroidJavaObject>("getUriForDownloadedFile", downloadId);
                var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.INSTALL_PACKAGE");
                intent.Call<AndroidJavaObject>("setDataAndType", uri, "application/vnd.android.package-archive");
                intent.Call<AndroidJavaObject>("addFlags", 0x10000000 | 0x00000001);   // NEW_TASK | GRANT_READ_URI_PERMISSION
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.NOT_UNKNOWN_SOURCE", true);
                act.Call("startActivity", intent);
                label.text = "Touchez « Mettre à jour » : le jeu se met à jour (personnages conservés)";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Korvalune] installation : " + e.Message);
                label.text = "Téléchargé : touchez la notification « Korvalune » pour installer";
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && waitingPermission) { waitingPermission = false; Install(); }       // retour du réglage : on installe
        }
    }
}
