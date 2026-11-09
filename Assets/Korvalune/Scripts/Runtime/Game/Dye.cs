using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Teinture d'un objet ou de l'armure du héros. Format de la chaîne :
    /// "" = aucune ; "#RRGGBB" = couleur unie ; "#RRGGBB|#RRGGBB" = mélange de deux couleurs (une par moitié des matériaux) ;
    /// "rainbow" = arc-en-ciel fixe (six bandes, comme l'Égide, sans changement dans le temps).</summary>
    public static class Dye
    {
        public const string Rainbow = "rainbow";

        /// <summary>Palette proposée chez le teinturier.</summary>
        public static readonly string[] Palette =
        {
            "#E8E4D8", "#2B2B2E", "#8A1C1C", "#C0392B", "#E67E22", "#F1C40F", "#2E8B57", "#1E6F3A",
            "#2A6FDB", "#1B2A5C", "#6A3FB5", "#B0159B", "#C9A46A", "#7B5A3A", "#AAB7C4", "#0E7C86",
        };

        public static bool Has(string dye) => !string.IsNullOrEmpty(dye);

        /// <summary>Couleurs à appliquer, dans l'ordre des matériaux (une couleur unie donne une seule couleur).</summary>
        public static Color[] Colors(string dye)
        {
            if (!Has(dye)) return new Color[0];
            if (dye == Rainbow)
            {
                var r = new Color[6];
                for (int k = 0; k < 6; k++) r[k] = Color.HSVToRGB(k / 6f, 0.75f, 1f);
                return r;
            }
            var parts = dye.Split('|');
            var list = new List<Color>();
            foreach (var p in parts) if (ColorUtility.TryParseHtmlString(p, out var c)) list.Add(c);
            return list.ToArray();
        }

        /// <summary>Multiplie la couleur de base des matériaux du rendu par la teinture (les matériaux exclus gardent leur couleur).</summary>
        static readonly Dictionary<int, Color> baseColor = new Dictionary<int, Color>();   // couleur d'origine de chaque instance

        public static void Apply(Renderer rd, string dye, System.Func<Material, bool> include = null)
        {
            if (!rd || !Application.isPlaying) return;       // en édition (captures CI) : pas d'instances de matériau, la teinture se joue en jeu
            var cols = Colors(dye);
            var mats = rd.materials;                              // instances : ne modifie pas les assets partagés
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (!m || !m.HasProperty("_BaseColor")) continue;
                if (include != null && !include(m)) continue;
                int id = m.GetInstanceID();
                if (!baseColor.TryGetValue(id, out var original)) { original = m.GetColor("_BaseColor"); baseColor[id] = original; }
                if (cols.Length == 0) { m.SetColor("_BaseColor", original); continue; }   // teinture retirée : retour à la couleur d'origine
                // mélange : première moitié des matériaux dans la couleur A, seconde dans la couleur B ; arc-en-ciel : une bande par matériau
                Color c = dye == Rainbow || cols.Length == 1 ? cols[i % cols.Length]
                        : cols[Mathf.Min(cols.Length - 1, i * cols.Length / mats.Length)];
                m.SetColor("_BaseColor", original * Color.Lerp(Color.white, c, 0.85f));
            }
            rd.materials = mats;
        }
    }
}
