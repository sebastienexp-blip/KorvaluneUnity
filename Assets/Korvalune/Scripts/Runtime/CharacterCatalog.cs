using System;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune
{
    /// <summary>
    /// Catalogue du créateur de personnage (généré par l'éditeur depuis Art/Catalog/catalog.json) : classes jouables en
    /// version homme / femme, peaux, yeux, coiffures (et leurs variantes), sourcils, cils, curseurs de morphologie.
    /// Toutes les ressources viennent des packs MakeHuman (CC0, ou CC-BY avec auteur cité dans les crédits).
    /// </summary>
    [CreateAssetMenu(menuName = "Korvalune/Catalogue de personnalisation")]
    public class CharacterCatalog : ScriptableObject
    {
        [Serializable] public class Credit { public string license = "CC0"; public string author = ""; }

        [Serializable] public class Skin : Credit { public string id; public Texture2D baseMap; public Texture2D normal; public string sex = "MF"; public string[] tags = new string[0]; }
        [Serializable] public class Eye : Credit { public string id; public Texture2D baseMap; }
        [Serializable] public class Variant { public string id; public Texture2D baseMap; public Texture2D normal; }
        [Serializable] public class Part : Credit
        {
            public string id; public string sex = "MF";
            // modèles ajustés aux corps de base homme / femme : chemins Resources, chargés à la demande (mémoire mobile)
            public string maleRes, femaleRes;
            public GameObject male => Res.Load<GameObject>(maleRes);
            public GameObject female => Res.Load<GameObject>(femaleRes);
            public List<Variant> variants = new List<Variant>();
            public GameObject For(bool female) => female ? (!string.IsNullOrEmpty(femaleRes) ? this.female : male) : (!string.IsNullOrEmpty(maleRes) ? male : this.female);
        }
        [Serializable] public class Morph { public string id; public string group; public string label; public bool face; public bool bipolar; }
        [Serializable] public class PlayableClass
        {
            public string id; public string label; public string description;
            public string maleRes, femaleRes;            // héros : chargés seulement quand on les affiche ou les joue
            public GameObject male => Res.Load<GameObject>(maleRes);
            public GameObject female => Res.Load<GameObject>(femaleRes);
            public bool HasMale => !string.IsNullOrEmpty(maleRes); public bool HasFemale => !string.IsNullOrEmpty(femaleRes);
        }

        public List<PlayableClass> classes = new List<PlayableClass>();
        public List<Skin> skins = new List<Skin>();
        public List<Eye> eyes = new List<Eye>();
        public List<Part> hair = new List<Part>();
        public List<Part> eyebrows = new List<Part>();
        public List<Part> eyelashes = new List<Part>();
        public List<Morph> morphsMale = new List<Morph>();
        public List<Morph> morphsFemale = new List<Morph>();

        public List<Morph> Morphs(bool female) => female ? morphsFemale : morphsMale;
        public static bool Fits(string sex, bool female) => string.IsNullOrEmpty(sex) || sex == "MF" || sex == (female ? "F" : "M");
    }

    /// <summary>Chargement à la demande depuis Resources, avec cache : seuls les héros et pièces réellement affichés sont en mémoire
    /// (tout charger d'un coup dépassait 7 Go dans l'éditeur, impossible sur téléphone).</summary>
    public static class Res
    {
        static readonly Dictionary<string, UnityEngine.Object> cache = new Dictionary<string, UnityEngine.Object>();
        public static T Load<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (cache.TryGetValue(path, out var o) && o) return o as T;
            var r = Resources.Load<T>(path); if (r) cache[path] = r; return r;
        }
        /// <summary>« Assets/Korvalune/Resources/Prefabs/X/Y.prefab » → « Prefabs/X/Y ».</summary>
        public static string PathOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            int i = assetPath.IndexOf("/Resources/"); if (i < 0) return "";
            var r = assetPath.Substring(i + 11); int dot = r.LastIndexOf('.'); return dot > 0 ? r.Substring(0, dot) : r;
        }
        public static void Clear() { cache.Clear(); Resources.UnloadUnusedAssets(); }
    }
}
