using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Habillage de l'interface généré à l'exécution (aucune image à importer) : panneaux de cuivre patiné à
    /// liseré d'or (9-tranches), boutons ronds bombés, anneaux, icônes de compétences dessinées.</summary>
    public static class UIStyle
    {
        public static readonly Color Copper = new Color(0.47f, 0.27f, 0.16f), CopperDark = new Color(0.20f, 0.11f, 0.07f),
            Gold = new Color(0.93f, 0.74f, 0.42f), GoldDark = new Color(0.52f, 0.36f, 0.16f), Parch = new Color(0.98f, 0.92f, 0.80f);

        static Sprite panel, button, ring, disc, bar, glow;
        static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();

        static float Noise(int x, int y) { unchecked { int n = x * 374761393 + y * 668265263; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 1023) / 1023f; } }

        static Texture2D Tex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        /// <summary>Panneau : fond cuivre dégradé et patiné, double liseré or, coins arrondis.</summary>
        public static Sprite Panel()
        {
            if (panel) return panel;
            int S = 128, B = 28; var t = Tex(S, S); var px = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Min(x, S - 1 - x), dy = Mathf.Min(y, S - 1 - y);
                float cx = Mathf.Max(0, 10 - dx), cy = Mathf.Max(0, 10 - dy);
                float corner = Mathf.Sqrt(cx * cx + cy * cy);
                float a = Mathf.Clamp01(10.5f - corner);
                float e = Mathf.Min(dx, dy) - (corner > 0 ? corner - Mathf.Max(cx, cy) : 0);
                var c = Color.Lerp(CopperDark, Copper, y / (float)S * 0.7f + 0.15f) * (0.88f + 0.2f * Noise(x / 3, y / 3));
                if (e < 3) c = Color.Lerp(GoldDark, Gold, 0.4f + 0.6f * (y / (float)S));
                else if (e < 4.5f) c = CopperDark * 0.6f;
                else if (e < 6.5f) c = Color.Lerp(GoldDark, Gold, 0.3f);
                c.a = a; px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return panel = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        }

        static Sprite Round(ref Sprite cache, int S, System.Func<float, float, float, Color> shade)
        {
            if (cache) return cache;
            var t = Tex(S, S); var px = new Color[S * S]; float R = S / 2f;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f - R) / R, v = (y + 0.5f - R) / R, r = Mathf.Sqrt(u * u + v * v);
                var c = shade(u, v, r); c.a *= Mathf.Clamp01((1 - r) * R); px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return cache = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>Bouton rond bombé : cuivre sombre, reflet, anneau d'or.</summary>
        public static Sprite Button() => Round(ref button, 256, (u, v, r) =>
        {
            if (r > 0.86f) return Color.Lerp(Gold, GoldDark, (r - 0.86f) / 0.14f + 0.2f * v);
            if (r > 0.80f) return CopperDark * 0.5f + new Color(0, 0, 0, 0.5f);
            var c = Color.Lerp(Copper * 0.9f, CopperDark * 0.6f, r) * (1 + 0.35f * Mathf.Clamp01(v * 0.8f - u * 0.2f));
            c.a = 1; return c;
        });

        /// <summary>Anneau doré épais (cadre de mini-carte, base du joystick).</summary>
        public static Sprite Ring() => Round(ref ring, 256, (u, v, r) =>
        {
            if (r < 0.84f) return new Color(0, 0, 0, 0);
            float k = Mathf.Abs(r - 0.92f) / 0.08f;
            var c = Color.Lerp(Gold, GoldDark, k + 0.25f * v); c.a = 1; return c;
        });

        public static Sprite Disc() => Round(ref disc, 128, (u, v, r) => Color.white);

        public static Sprite Glow() => Round(ref glow, 128, (u, v, r) => new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 2)));

        public static Sprite Bar()
        {
            if (bar) return bar;
            int W = 64, H = 16; var t = Tex(W, H); var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float v = y / (float)(H - 1); float k = 0.75f + 0.45f * Mathf.Sin(v * Mathf.PI) - 0.15f * v;
                px[y * W + x] = new Color(k, k, k, 1);
            }
            t.SetPixels(px); t.Apply();
            return bar = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        }

        /// <summary>Icônes dessinées (sur fond transparent) : attaque, tourbillon, bond, cri, piétinement, sac, livre.</summary>
        public static Sprite Icon(string id, Color col)
        {
            string cacheKey = id + ColorUtility.ToHtmlStringRGB(col);
            if (icons.TryGetValue(cacheKey, out var s)) return s;
            int S = 128; var t = Tex(S, S); var px = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x - 64f) / 64f, v = (y - 64f) / 64f, r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                float m = 0;
                switch (id)
                {
                    case "attack":   // épée en diagonale
                        { float p = (u + v) * 0.7071f, q = (u - v) * 0.7071f;
                          m = (Mathf.Abs(q) < 0.08f - 0.06f * Mathf.Clamp01((p - 0.2f) / 0.6f) && p > -0.35f && p < 0.82f) ? 1 : 0;
                          if (Mathf.Abs(p + 0.38f) < 0.05f && Mathf.Abs(q) < 0.3f) m = 1;
                          if (p < -0.42f && p > -0.72f && Mathf.Abs(q) < 0.05f) m = 0.8f; } break;
                    case "whirlwind": { float sp = Mathf.Repeat(a / (2 * Mathf.PI) * 3 + r * 1.6f, 1); m = (r < 0.85f && r > 0.15f && sp < 0.35f) ? 1 : 0; } break;
                    case "leap": { float yy = 0.7f - 2.2f * u * u - 0.25f; m = (Mathf.Abs(v - yy) < 0.09f && u > -0.75f && u < 0.55f) ? 1 : 0;
                          if (u > 0.4f && u < 0.85f && Mathf.Abs(v - (-0.35f - (u - 0.4f) * 1.3f)) < 0.25f - (u - 0.4f) * 0.5f && v < -0.1f) m = 1;
                          if (v < -0.62f && v > -0.72f && Mathf.Abs(u) < 0.8f) m = 1; } break;
                    case "warcry": { m = (r < 0.28f) ? 1 : 0; float ray = Mathf.Abs(Mathf.Repeat(a / (2 * Mathf.PI) * 10, 1) - 0.5f); if (r > 0.4f && r < 0.85f && ray < 0.12f) m = 1; } break;
                    case "stomp": { m = (Mathf.Abs(r - 0.25f) < 0.07f || Mathf.Abs(r - 0.52f) < 0.06f || Mathf.Abs(r - 0.78f) < 0.05f) && v < 0.35f ? 1 : 0; if (r < 0.12f) m = 1; } break;
                    case "bag": { m = (Mathf.Abs(u) < 0.55f - 0.15f * v && v < 0.35f && v > -0.7f) ? 1 : 0; if (Mathf.Abs(r - 0.0f) < 0 ) m = 0; if (v > 0.3f && v < 0.6f && Mathf.Abs(Mathf.Sqrt(u * u + (v - 0.35f) * (v - 0.35f)) - 0.22f) < 0.06f) m = 1; } break;
                    case "potion": { float rb = Mathf.Sqrt(u * u + (v + 0.25f) * (v + 0.25f)); m = rb < 0.48f || (Mathf.Abs(u) < 0.14f && v > -0.3f && v < 0.55f) || (Mathf.Abs(u) < 0.2f && v >= 0.55f && v < 0.72f) ? 1 : 0; if (rb < 0.48f && v > -0.12f && v < -0.02f) m = 0.5f; } break;
                    case "coin": { m = r < 0.62f ? 1 : 0; if (Mathf.Abs(r - 0.48f) < 0.035f) m = 0.45f; } break;
                    case "book": { m = (Mathf.Abs(u) < 0.7f && Mathf.Abs(v) < 0.55f && Mathf.Abs(u) > 0.04f) ? 1 : 0; if (Mathf.Abs(u) > 0.15f && Mathf.Abs(u) < 0.6f && Mathf.Abs(Mathf.Repeat(v * 5, 1) - 0.5f) < 0.08f && Mathf.Abs(v) < 0.4f) m = 0.4f; } break;
                }
                var c = col * (0.8f + 0.4f * (1 - r)); c.a = m; px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return icons[cacheKey] = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
