using System;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune
{
    /// <summary>
    /// Équipement détachable d'un personnage ModelForge : chaque pièce (sous-vêtement, plastron, bottes…) est un
    /// SkinnedMeshRenderer séparé, sur le même squelette que le corps. On équipe / retire une pièce par son emplacement ;
    /// les pièces « couvrantes » masquent ce qu'elles recouvrent (ex. le sous-vêtement bas masque l'anatomie).
    /// <para>contenuAdulte = false : l'anatomie n'est jamais affichée et les sous-vêtements ne peuvent pas être retirés
    /// (réglage par défaut, compatible avec un classement tout public / PEGI 12-16).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterEquipment : MonoBehaviour
    {
        public const string Anatomy = "Anatomie";
        public const string UnderwearLower = "SousVetementBas";
        public const string UnderwearUpper = "SousVetementHaut";

        [Serializable]
        public class Slot
        {
            public string name;
            public SkinnedMeshRenderer renderer;
            public bool equipped = true;
            public List<string> hides = new List<string>();   // emplacements masqués quand celui-ci est équipé
        }

        [Tooltip("Autorise l'affichage de l'anatomie et le retrait des sous-vêtements.")]
        public bool contenuAdulte = false;
        [SerializeField] List<Slot> slots = new List<Slot>();

        public IReadOnlyList<Slot> Slots => slots;

        void Reset() { Collect(); }
        void OnEnable() { Apply(); }
        void OnValidate() { if (slots.Count > 0) Apply(); }

        /// <summary>Recense les pièces : tous les SkinnedMeshRenderer enfants sauf le maillage principal « *_Mesh ».</summary>
        public void Collect()
        {
            var old = new Dictionary<string, Slot>();
            foreach (var s in slots) if (s != null && !string.IsNullOrEmpty(s.name)) old[s.name] = s;
            slots.Clear();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.EndsWith("_Mesh")) continue;
                var s = old.TryGetValue(r.name, out var o) ? o : new Slot { name = r.name, equipped = r.name != Anatomy };
                s.renderer = r;
                if (s.name == UnderwearLower && !s.hides.Contains(Anatomy)) s.hides.Add(Anatomy);
                slots.Add(s);
            }
            Apply();
        }

        public Slot Find(string name) => slots.Find(s => s != null && s.name == name);

        /// <summary>Déclare ce qu'une pièce masque quand elle est équipée (zones de peau « Peau_* », autres pièces).</summary>
        public void SetHides(string name, IEnumerable<string> hides)
        {
            var s = Find(name);
            if (s == null || hides == null) return;
            foreach (var h in hides) if (!s.hides.Contains(h)) s.hides.Add(h);
        }

        /// <summary>Vrai pour les zones de peau du corps de base : jamais « retirées », seulement masquées par l'équipement.</summary>
        public static bool IsBody(string name) => name.StartsWith("Peau_");
        public bool IsEquipped(string name) { var s = Find(name); return s != null && s.equipped; }

        /// <summary>Équipe ou retire une pièce. Renvoie faux si l'emplacement n'existe pas ou si le retrait est interdit.</summary>
        public bool Equip(string name, bool on)
        {
            var s = Find(name);
            if (s == null) return false;
            if (!on && IsBody(name)) return false;
            if (!on && !contenuAdulte && (name == UnderwearLower || name == UnderwearUpper)) return false;
            s.equipped = on;
            Apply();
            return true;
        }

        public void Toggle(string name) { var s = Find(name); if (s != null) Equip(name, !s.equipped); }

        /// <summary>Met à jour l'affichage : pièce visible si équipée et non masquée par une pièce couvrante équipée.</summary>
        public void Apply()
        {
            var hidden = new HashSet<string>();
            foreach (var s in slots)
            {
                if (s == null) continue;
                bool worn = s.equipped || IsBody(s.name) || (!contenuAdulte && (s.name == UnderwearLower || s.name == UnderwearUpper));
                if (worn) foreach (var h in s.hides) hidden.Add(h);
            }
            foreach (var s in slots)
            {
                if (s == null || s.renderer == null) continue;
                bool show = s.equipped && !hidden.Contains(s.name);
                if (s.name == Anatomy && !contenuAdulte) show = false;
                if (!contenuAdulte && (s.name == UnderwearLower || s.name == UnderwearUpper)) show = !hidden.Contains(s.name);
                s.renderer.enabled = show;
            }
        }
    }
}
