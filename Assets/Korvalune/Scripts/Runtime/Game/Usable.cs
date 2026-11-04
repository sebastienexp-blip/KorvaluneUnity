using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Tout ce que le héros peut activer (parler à un marchand, ouvrir un coffre, la pierre de voyage, un portail,
    /// la statue des brèches…). Le bouton « Utiliser » du HUD (touche E au clavier) agit sur le plus proche à portée :
    /// plus besoin de viser l'objet du doigt.</summary>
    public class Usable : MonoBehaviour
    {
        public static readonly List<Usable> All = new List<Usable>();
        public string label = "Utiliser";
        public float range = 3.5f;
        public System.Func<bool> available;
        public System.Action use;

        public static Usable Add(GameObject go, string label, float range, System.Action use, System.Func<bool> available = null)
        {
            var u = go.GetComponent<Usable>() ?? go.AddComponent<Usable>();
            u.label = label; u.range = range; u.use = use; u.available = available;
            return u;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>L'objet activable le plus proche du point (à portée, disponible), ou null.</summary>
        public static Usable Nearest(Vector3 p)
        {
            Usable best = null; float bd = float.MaxValue;
            foreach (var u in All)
            {
                if (!u || u.use == null || !u.isActiveAndEnabled) continue;
                if (u.available != null && !u.available()) continue;
                var d = u.transform.position - p; d.y = 0; float m = d.magnitude;
                if (m <= u.range && m < bd) { bd = m; best = u; }
            }
            return best;
        }

        public static bool UseNearest(Vector3 p)
        {
            var u = Nearest(p); if (!u) return false;
            Sound.UI("sfx_ui_clic", 0.5f); u.use(); return true;
        }
    }
}
