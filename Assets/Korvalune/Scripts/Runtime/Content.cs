using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Korvalune
{
    /// <summary>Contenu téléchargeable : les paquets (AssetBundles) récupérés par le lanceur dans le dossier de données du jeu
    /// (« contenu/ ») sont ouverts une fois, puis les modèles s'y chargent par le même chemin qu'avant (« Prefabs/Mounts/… »).
    /// Sans paquet (éditeur, ancienne version), on retombe sur Resources. Les scènes des paquets se chargent par leur nom.</summary>
    public static class Content
    {
        const string Root = "assets/korvalune/contenu/";
        static readonly Dictionary<string, (AssetBundle bundle, string path)> index = new Dictionary<string, (AssetBundle, string)>();
        static readonly List<AssetBundle> bundles = new List<AssetBundle>();
        public static bool Mounted { get; private set; }
        public static int SceneCount { get; private set; }

        public static string Dir => Path.Combine(Application.persistentDataPath, "contenu");

        /// <summary>Ouvre tous les paquets présents (le catalogue local dit lesquels). Renvoie faux si un paquet manque.</summary>
        public static bool Mount(IEnumerable<string> names)
        {
            if (Mounted) return true;
            foreach (var b in bundles) if (b) b.Unload(false);
            bundles.Clear(); index.Clear(); SceneCount = 0;
            foreach (var n in names)
            {
                var f = Path.Combine(Dir, n);
                if (!File.Exists(f)) { Debug.LogWarning("[Contenu] paquet absent : " + n); return false; }
                var b = AssetBundle.LoadFromFile(f);
                if (!b) { Debug.LogWarning("[Contenu] paquet illisible : " + n); return false; }
                bundles.Add(b);
                if (b.isStreamedSceneAssetBundle) { SceneCount += b.GetAllScenePaths().Length; continue; }
                foreach (var a in b.GetAllAssetNames())
                {
                    if (!a.StartsWith(Root)) continue;
                    var rel = a.Substring(Root.Length); int dot = rel.LastIndexOf('.'); if (dot > 0) rel = rel.Substring(0, dot);
                    index[rel] = (b, a);
                }
            }
            Mounted = true;
            Debug.Log($"[Contenu] {bundles.Count} paquets ouverts, {index.Count} modèles, {SceneCount} scènes");
            return true;
        }

        /// <summary>Charge un élément par son chemin « à la Resources » (sans extension) : paquet d'abord, Resources sinon.</summary>
        public static T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (index.Count > 0 && index.TryGetValue(path.ToLowerInvariant(), out var e) && e.bundle)
            {
                var o = e.bundle.LoadAsset<T>(e.path); if (o) return o;
            }
            return Resources.Load<T>(path);
        }
    }
}
