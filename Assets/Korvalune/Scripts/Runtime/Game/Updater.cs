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
        const string Base = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/apk-derniere/";
        const string Page = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/tag/apk-derniere";
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
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -30); rt.sizeDelta = new Vector2(900, 80);
            var u = go.AddComponent<Updater>(); u.bg = go.GetComponent<Image>(); u.bg.sprite = UIStyle.Slot(); u.bg.type = Image.Type.Sliced; u.bg.color = new Color(0.9f, 0.7f, 0.35f);
            var t = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(rt, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = 30; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            u.label = t;
            var b = go.AddComponent<Button>(); b.targetGraphic = u.bg; b.onClick.AddListener(u.Click);
            go.SetActive(false);
            u.StartCoroutine(u.Check());
        }

        IEnumerator Check()
        {
            gameObject.SetActive(true); label.text = ""; bg.enabled = false;
            using (var r = UnityWebRequest.Get(Base + "version.txt"))
            {
                r.timeout = 8;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success || !int.TryParse(r.downloadHandler.text.Trim(), out remote)) { gameObject.SetActive(false); yield break; }
            }
            if (remote <= InstalledBuild) { gameObject.SetActive(false); yield break; }
            bg.enabled = true;
            label.text = $"Mise à jour disponible (version 0.1.{remote}) — toucher pour télécharger";
        }

        void Click()
        {
            if (busy) return;
            if (Application.platform != RuntimePlatform.Android) { Application.OpenURL(Page); return; }
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
            try
            {
                var uri = dm.Call<AndroidJavaObject>("getUriForDownloadedFile", downloadId);
                var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");
                intent.Call<AndroidJavaObject>("setDataAndType", uri, "application/vnd.android.package-archive");
                intent.Call<AndroidJavaObject>("addFlags", 0x10000000 | 0x00000001);   // NEW_TASK | GRANT_READ_URI_PERMISSION
                act.Call("startActivity", intent);
                label.text = "Installe la mise à jour (tes personnages sont conservés)";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Korvalune] installation : " + e.Message);
                label.text = "Téléchargé : ouvre la notification « Korvalune » pour installer";
            }
            busy = false;
        }
    }
}
