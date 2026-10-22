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
            public GameObject male, female;              // modèles ajustés aux corps de base homme / femme
            public List<Variant> variants = new List<Variant>();
            public GameObject For(bool female) => female ? (this.female ? this.female : male) : (male ? male : this.female);
        }
        [Serializable] public class Morph { public string id; public string group; public string label; public bool face; public bool bipolar; }
        [Serializable] public class PlayableClass { public string id; public string label; public string description; public GameObject male, female; }

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
}
