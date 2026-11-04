#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Ajoute au manifeste Android la permission REQUEST_INSTALL_PACKAGES : sans elle, Android refuse que le jeu
    /// ouvre lui-même l'installateur de la mise à jour qu'il vient de télécharger (il fallait aller l'installer à la main
    /// depuis les téléchargements).</summary>
    public class AndroidManifestPatch : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var mf = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(mf)) { Debug.LogWarning("[Korvalune] manifeste introuvable : " + mf); return; }
            var s = File.ReadAllText(mf);
            const string perm = "<uses-permission android:name=\"android.permission.REQUEST_INSTALL_PACKAGES\" />";
            if (s.Contains("REQUEST_INSTALL_PACKAGES")) return;
            int i = s.IndexOf("<application", System.StringComparison.Ordinal);
            if (i < 0) return;
            s = s.Insert(i, perm + "\n  ");
            File.WriteAllText(mf, s);
            Debug.Log("[Korvalune] manifeste : permission d'installer les mises à jour ajoutée");
        }
    }
}
#endif
