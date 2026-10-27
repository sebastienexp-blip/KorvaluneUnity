using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Apparence du héros en jeu, pilotée par l'équipement porté : un héros qui débute n'a que des haillons (tunique
    /// et braies de toile grossière) et son arme de base ; chaque objet équipé fait apparaître les pièces de tenue
    /// correspondantes du modèle (casque, torse, gants, jambes, bottes). Les pièces sont celles du modèle de la classe, rangées
    /// par emplacement d'objet selon leur nom.</summary>
    [RequireComponent(typeof(Equipment))]
    public class HeroOutfit : MonoBehaviour
    {
        public const string RagsTop = "HaillonsHaut", RagsBottom = "HaillonsBas";
        CharacterEquipment look; Equipment eq;
        readonly Dictionary<string, Slot?> map = new Dictionary<string, Slot?>();

        static readonly (Regex re, Slot? slot)[] Rules =
        {
            (new Regex(@"^(peau_|cheveux|anatomie|sousvetement|haillons|carquois)", RegexOptions.IgnoreCase), null),
            (new Regex(@"(casque|capuche|heaume|couronne|bassinet|coiffe|masque)", RegexOptions.IgnoreCase), Slot.Casque),
            (new Regex(@"(gant|mitaine|brassard)", RegexOptions.IgnoreCase), Slot.Gants),
            (new Regex(@"(botte|soleret|greve|sandale|chaussure)", RegexOptions.IgnoreCase), Slot.Bottes),
            (new Regex(@"(cuissot|cuissard|genou|tassette|pantalon|jambiere|jupe|^pans|chausse)", RegexOptions.IgnoreCase), Slot.Jambes),
        };

        /// <summary>Emplacement d'objet qui fait apparaître cette pièce de tenue ; null = toujours portée (corps, haillons…).</summary>
        public static Slot? SlotOf(string piece)
        {
            foreach (var (re, slot) in Rules) if (re.IsMatch(piece)) return slot;
            return Slot.Torse;      // plastron, gambison, maille, épaulières, tabard, tunique, corset, cape, ceinture…
        }

        public void Init()
        {
            look = GetComponentInChildren<CharacterEquipment>(); eq = GetComponent<Equipment>();
            if (!look) return;
            if (look.Slots.Count == 0) look.Collect();
            map.Clear();
            foreach (var s in look.Slots) if (s != null && !string.IsNullOrEmpty(s.name)) map[s.name] = SlotOf(s.name);
            eq.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() { if (eq) eq.Changed -= Refresh; }

        public void Refresh()
        {
            if (!look || !eq) return;
            if (look.Find(RagsTop) == null || look.Find(RagsBottom) == null) return;   // modèle sans haillons (ancien export) : tenue complète
            foreach (var kv in map)
            {
                if (!kv.Value.HasValue) continue;
                look.Find(kv.Key).equipped = eq.In(kv.Value.Value) != null;
            }
            // haillons : visibles tant que l'emplacement correspondant est vide
            var top = look.Find(RagsTop); if (top != null) top.equipped = eq.In(Slot.Torse) == null;
            var bot = look.Find(RagsBottom); if (bot != null) bot.equipped = eq.In(Slot.Jambes) == null;
            look.Apply();
        }
    }
}
