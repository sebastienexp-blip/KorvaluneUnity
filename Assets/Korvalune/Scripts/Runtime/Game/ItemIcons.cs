using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Icônes d'inventaire : une image par type d'objet de base (Resources/Icons/&lt;nom sans accents&gt;),
    /// peintes par Tools/ModelForge/icons/items.py.</summary>
    public static class ItemIcons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static string Slug(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD); var sb = new StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c > 127) continue;
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString().Trim('_');
        }

        static readonly System.Collections.Generic.HashSet<string> missing = new System.Collections.Generic.HashSet<string>();
        public static Sprite Get(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return null;
            if (cache.TryGetValue(baseName, out var s) && (s || missing.Contains(baseName))) return s;   // revalidé : déchargé au changement de scène ?
            s = Resources.Load<Sprite>("Icons/" + Slug(baseName));
            if (!s) { var t = Resources.Load<Texture2D>("Icons/" + Slug(baseName)); if (t) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); }
            if (!s) missing.Add(baseName); cache[baseName] = s; return s;
        }
    }
}
