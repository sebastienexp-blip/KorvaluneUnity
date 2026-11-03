using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Niveau de brèche généré à chaque entrée : un des cinq décors, salles et couloirs tirés au hasard, repaire du
    /// gardien au bout du parcours. Tout est modélisé (dalles, murs, piliers, sarcophages, braseros, torches de la crypte ;
    /// falaises sculptées, rochers, arbres pétrifiés ou gelés, cristaux) et éclairé pour rester lisible sur téléphone :
    /// ambiance claire, brume légère qui ne mange que le lointain, aucune ombre de voûte.
    ///   Crypte engloutie — dalles froides, torches bleues, eau sombre ;
    ///   Fournaise des damnés — dalles roussies, failles de lave, braseros ;
    ///   Nécropole astrale — îlots de dalles au-dessus du vide étoilé, cristaux violets flottants ;
    ///   Bois pétrifié — canyon de roche grise, arbres morts, brume verdâtre ;
    ///   Glacier des âmes — gorge de glace et de neige, cristaux bleus, flocons.</summary>
    [DefaultExecutionOrder(-500)]
    public class RiftLevel : MonoBehaviour
    {
        public const float T = 4f;
        public static readonly List<string> ThemeEnemies = new List<string>();
        public static string ThemeName = "";
        static int lastTheme = -1;

        class Theme
        {
            public string name; public bool crypt, astral, ice;
            public Color tint, ground, rock, fog, bg, light, torch, sky, eq, gnd, crystal; public string[] enemies; public float fogStart = 26, fogEnd = 75;
            public string motes;   // poussière, braises, étoiles, spores, neige
        }

        static readonly Theme[] Themes =
        {
            new Theme { name = "Crypte engloutie", crypt = true, tint = new Color(0.72f, 0.86f, 0.95f), torch = new Color(0.45f, 0.75f, 1f), fog = new Color(0.10f, 0.16f, 0.22f), bg = new Color(0.03f, 0.05f, 0.07f),
                        light = new Color(0.65f, 0.78f, 0.95f), sky = new Color(0.50f, 0.58f, 0.68f), eq = new Color(0.36f, 0.40f, 0.44f), gnd = new Color(0.16f, 0.18f, 0.20f), crystal = new Color(0.3f, 0.8f, 1f),
                        enemies = new[] { "squelette", "chevalier", "sorcier", "araignee" }, motes = "poussiere" },
            new Theme { name = "Fournaise des damnés", crypt = true, tint = new Color(1.0f, 0.74f, 0.60f), torch = new Color(1f, 0.55f, 0.2f), fog = new Color(0.22f, 0.08f, 0.04f), bg = new Color(0.07f, 0.02f, 0.01f),
                        light = new Color(1f, 0.70f, 0.5f), sky = new Color(0.62f, 0.45f, 0.38f), eq = new Color(0.45f, 0.30f, 0.24f), gnd = new Color(0.22f, 0.10f, 0.06f), crystal = new Color(1f, 0.45f, 0.1f),
                        enemies = new[] { "demon", "sorcier", "squelette", "chevalier" }, motes = "braises" },
            new Theme { name = "Nécropole astrale", crypt = true, astral = true, tint = new Color(0.82f, 0.74f, 1.0f), torch = new Color(0.75f, 0.45f, 1f), fog = new Color(0.12f, 0.07f, 0.20f), bg = new Color(0.025f, 0.01f, 0.05f),
                        light = new Color(0.80f, 0.70f, 1f), sky = new Color(0.56f, 0.50f, 0.70f), eq = new Color(0.40f, 0.34f, 0.48f), gnd = new Color(0.16f, 0.12f, 0.22f), crystal = new Color(0.72f, 0.40f, 1f),
                        enemies = new[] { "sorcier", "araignee", "loup", "demon" }, motes = "etoiles", fogStart = 34, fogEnd = 95 },
            new Theme { name = "Bois pétrifié", ground = new Color(0.36f, 0.36f, 0.31f), rock = new Color(0.42f, 0.41f, 0.38f), tint = new Color(0.70f, 0.72f, 0.66f), torch = new Color(0.65f, 1f, 0.45f),
                        fog = new Color(0.20f, 0.24f, 0.17f), bg = new Color(0.12f, 0.14f, 0.10f), light = new Color(0.85f, 0.92f, 0.75f), sky = new Color(0.62f, 0.66f, 0.58f), eq = new Color(0.45f, 0.46f, 0.40f),
                        gnd = new Color(0.20f, 0.20f, 0.16f), crystal = new Color(0.55f, 1f, 0.35f), enemies = new[] { "loup", "araignee", "squelette" }, motes = "spores" },
            new Theme { name = "Glacier des âmes", ice = true, ground = new Color(0.60f, 0.66f, 0.74f), rock = new Color(0.42f, 0.50f, 0.60f), tint = new Color(0.80f, 0.90f, 1.0f), torch = new Color(0.45f, 0.85f, 1f),
                        fog = new Color(0.55f, 0.64f, 0.74f), bg = new Color(0.40f, 0.48f, 0.58f), light = new Color(0.90f, 0.95f, 1f), sky = new Color(0.66f, 0.72f, 0.80f), eq = new Color(0.50f, 0.56f, 0.62f),
                        gnd = new Color(0.40f, 0.44f, 0.50f), crystal = new Color(0.45f, 0.85f, 1f), enemies = new[] { "squelette", "loup", "chevalier", "sorcier" }, motes = "neige" },
        };

        // ---------------------------------------------------------------- état de génération
        Theme th; System.Random rng; Transform root;
        bool[,] open; int W, H; Vector2Int origin;
        readonly List<RectInt> rooms = new List<RectInt>();
        readonly Dictionary<Material, Material> tinted = new Dictionary<Material, Material>();
        Material litBase;

        void Awake()
        {
            if (!Application.isPlaying) return;
            Generate(System.Environment.TickCount, -1);
        }

        /// <summary>Construit le niveau dans la scène et configure le jeu (départ, camps, ambiance). theme &lt; 0 : au hasard.</summary>
        public GameBootstrap Generate(int seed, int theme)
        {
            rng = new System.Random(seed);
            if (theme < 0) { theme = rng.Next(Themes.Length); if (theme == lastTheme) theme = (theme + 1 + rng.Next(Themes.Length - 1)) % Themes.Length; }
            lastTheme = theme; th = Themes[theme]; ThemeName = th.name;
            ThemeEnemies.Clear(); ThemeEnemies.AddRange(th.enemies);
            var game = GetComponent<GameBootstrap>() ? GetComponent<GameBootstrap>() : FindFirstObjectByType<GameBootstrap>();
            litBase = game && game.config && game.config.litMat ? game.config.litMat : null;
            var old = transform.Find("Niveau"); if (old) Kill(old.gameObject);
            root = new GameObject("Niveau").transform; root.SetParent(transform, false);
            Layout();
            if (th.crypt) BuildCrypt(); else BuildCanyon();
            Atmosphere(game);
            Populate(game);
            return game;
        }

        static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
        float R() => (float)rng.NextDouble();
        Vector3 Cell(int x, int y) => new Vector3((x + origin.x) * T, 0, (y + origin.y) * T);
        bool Open(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && open[x, y];

        // ---------------------------------------------------------------- plan : salles reliées par des couloirs
        void Layout()
        {
            rooms.Clear();
            int n = 7 + rng.Next(4);
            var placed = new List<RectInt> { new RectInt(0, 0, 5, 5) };
            var dirs = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            var links = new List<(int, int)>();
            Vector2Int heading = dirs[rng.Next(4)];
            for (int i = 1; i < n; i++)
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    int from = rng.Next(4) == 0 ? rng.Next(placed.Count) : placed.Count - 1;
                    var a = placed[from];
                    var d = rng.Next(3) == 0 ? dirs[rng.Next(4)] : heading;
                    bool last = i == n - 1;
                    int w = last ? 9 + rng.Next(3) : 5 + rng.Next(5), h = last ? 9 + rng.Next(3) : 5 + rng.Next(5);
                    int gap = 3 + rng.Next(4);
                    var c = new Vector2Int((int)a.center.x, (int)a.center.y);
                    Vector2Int pos;
                    if (d.x != 0) pos = new Vector2Int(d.x > 0 ? a.xMax + gap : a.xMin - gap - w, c.y - h / 2 + rng.Next(-2, 3));
                    else pos = new Vector2Int(c.x - w / 2 + rng.Next(-2, 3), d.y > 0 ? a.yMax + gap : a.yMin - gap - h);
                    var r = new RectInt(pos.x, pos.y, w, h);
                    bool clash = false;
                    foreach (var o in placed) { var g = new RectInt(o.x - 2, o.y - 2, o.width + 4, o.height + 4); if (g.Overlaps(r)) { clash = true; break; } }
                    if (clash) continue;
                    placed.Add(r); links.Add((from, placed.Count - 1));
                    if (rng.Next(3) == 0) heading = dirs[rng.Next(4)];
                    break;
                }
            }
            // grille englobante (marge de 4 cases pour les falaises et le décor extérieur)
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var r in placed) { x0 = Mathf.Min(x0, r.xMin); y0 = Mathf.Min(y0, r.yMin); x1 = Mathf.Max(x1, r.xMax); y1 = Mathf.Max(y1, r.yMax); }
            const int M = 5;
            W = x1 - x0 + 2 * M; H = y1 - y0 + 2 * M; origin = new Vector2Int(x0 - M, y0 - M);
            open = new bool[W, H];
            foreach (var r in placed)
            {
                var rr = new RectInt(r.x - origin.x, r.y - origin.y, r.width, r.height); rooms.Add(rr);
                for (int x = rr.xMin; x < rr.xMax; x++) for (int y = rr.yMin; y < rr.yMax; y++) open[x, y] = true;
                // coins rognés : salles moins carrées
                if (rr.width > 5 && rr.height > 5 && rng.Next(2) == 0) { open[rr.xMin, rr.yMin] = open[rr.xMax - 1, rr.yMax - 1] = false; }
            }
            foreach (var (a, b) in links) Corridor(rooms[a], rooms[b]);
        }

        void Corridor(RectInt a, RectInt b)
        {
            int ax = (int)a.center.x, ay = (int)a.center.y, bx = (int)b.center.x, by = (int)b.center.y;
            bool xFirst = rng.Next(2) == 0;
            void Carve(int x, int y) { for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++) if (x + dx >= 0 && y + dy >= 0 && x + dx < W && y + dy < H) open[x + dx, y + dy] = true; }
            int cx = ax, cy = ay;
            if (xFirst) { while (cx != bx) { Carve(cx, cy); cx += System.Math.Sign(bx - cx); } while (cy != by) { Carve(cx, cy); cy += System.Math.Sign(by - cy); } }
            else { while (cy != by) { Carve(cx, cy); cy += System.Math.Sign(by - cy); } while (cx != bx) { Carve(cx, cy); cx += System.Math.Sign(bx - cx); } }
            Carve(cx, cy);
        }

        // ---------------------------------------------------------------- matériaux teintés (un exemplaire par matériau)
        Material Tinted(Material m, Color tint)
        {
            if (!m) return m;
            if (tinted.TryGetValue(m, out var t)) return t;
            t = new Material(m) { name = m.name + " (" + th.name + ")" };
            if (t.HasProperty("_BaseColor")) t.SetColor("_BaseColor", t.GetColor("_BaseColor") * tint);
            else if (t.HasProperty("_Color")) t.color *= tint;
            return tinted[m] = t;
        }

        GameObject Put(string path, Vector3 pos, float yaw, float scale = 1f, Color? tint = null)
        {
            var pf = Resources.Load<GameObject>("Prefabs/World/" + path); if (!pf) return null;
            var go = Instantiate(pf, root); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale *= scale;
            var c = tint ?? th.tint;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = Tinted(ms[i], c); r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        Material Mat(Color c, float smooth, Color emission = default, Texture2D tex = null)
        {
            var m = litBase ? new Material(litBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); if (tex) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (emission.maxColorComponent > 0.01f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return m;
        }

        Light Glow(Vector3 pos, Color c, float intensity, float range, bool flicker)
        {
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(root); l.transform.position = pos;
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (flicker) l.gameObject.AddComponent<Flicker>();
            return l;
        }

        // ---------------------------------------------------------------- décors de crypte (kit de la crypte)
        void BuildCrypt()
        {
            var sides = new (int dx, int dy, Vector3 off, float yaw)[] { (0, 1, new Vector3(0, 0, T / 2), 0), (0, -1, new Vector3(0, 0, -T / 2), 180), (1, 0, new Vector3(T / 2, 0, 0), 90), (-1, 0, new Vector3(-T / 2, 0, 0), 270) };
            var edgeMat = th.astral ? Mat(th.crystal * 0.4f, 0.5f, th.crystal * 2.2f) : null;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!open[x, y]) continue;
                    var p = Cell(x, y);
                    Put("Crypte/D_Sol" + rng.Next(3), p, 90 * rng.Next(4));
                    foreach (var (dx, dy, off, yaw) in sides)
                    {
                        if (Open(x + dx, y + dy)) continue;
                        if (th.astral)
                        {   // bord de l'îlot : liseré lumineux, garde invisible, roches qui flottent en contrebas
                            var e = GameObject.CreatePrimitive(PrimitiveType.Cube); e.name = "Bord"; e.transform.SetParent(root);
                            e.transform.position = p + off + Vector3.up * 0.06f; e.transform.rotation = Quaternion.Euler(0, yaw, 0); e.transform.localScale = new Vector3(T, 0.12f, 0.12f);
                            e.GetComponent<Renderer>().sharedMaterial = edgeMat; e.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            var wall = new GameObject("Garde").AddComponent<BoxCollider>(); wall.transform.SetParent(root); wall.transform.position = p + off + Vector3.up * 1.5f;
                            wall.transform.rotation = Quaternion.Euler(0, yaw, 0); wall.size = new Vector3(T, 3f, 0.4f);
                            if (rng.Next(3) == 0)
                            {
                                var rk = Put("Rocher" + rng.Next(3), p + off * 2.4f + Vector3.down * (3f + R() * 5f), rng.Next(360), 0.6f + R() * 0.8f);
                                if (rk) { foreach (var col in rk.GetComponentsInChildren<Collider>()) Kill(col); var f = rk.AddComponent<Floater>(); f.bob = 0.6f + R(); f.period = 5f + R() * 4f; f.spin = 4f + R() * 8f; }
                            }
                            continue;
                        }
                        Put("Crypte/D_Mur" + rng.Next(3), p + off, yaw);
                        if (rng.Next(4) == 0)
                        {
                            var inward = -off.normalized;
                            if (Put("Crypte/D_Torche", p + off + inward * 0.42f, yaw + 180)) Glow(p + off + inward * 0.7f + Vector3.up * 2.1f, th.torch, 2.4f, 8f, true);
                        }
                    }
                }
            // mobilier des salles
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i]; bool boss = i == rooms.Count - 1;
                if (r.width >= 7 && r.height >= 7)
                    for (int x = r.xMin + 2; x < r.xMax - 1; x += 3)
                        foreach (var y in new[] { r.yMin + 1, r.yMax - 2 })
                            if (Open(x, y)) Put("Crypte/D_Pilier", Cell(x, y), 0);
                int deco = boss ? 0 : 2 + rng.Next(3);
                for (int k = 0; k < deco; k++)
                {
                    var c = Cell(r.xMin + 1 + rng.Next(Mathf.Max(1, r.width - 2)), r.yMin + 1 + rng.Next(Mathf.Max(1, r.height - 2)));
                    switch (rng.Next(4))
                    {
                        case 0: Put("Crypte/D_Sarcophage0", c, 90 * rng.Next(4)); break;
                        case 1: case 2: Put("Crypte/D_Os" + rng.Next(2), c + new Vector3(R() - 0.5f, 0, R() - 0.5f), rng.Next(360)); break;
                        default: var b = Put("Ville/M_Tonneaux", c, rng.Next(360), 0.9f); if (b) Breakable.Make(b, 1.0f); break;
                    }
                }
                // braseros : un au centre des salles moyennes, quatre autour du repaire du gardien
                var ctr = Cell((int)r.center.x, (int)r.center.y);
                if (boss) foreach (var d in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                    { var q = ctr + d * T * 1.6f; Put("Crypte/D_Brasero", q, 0); Glow(q + Vector3.up * 1.3f, th.torch, 3.4f, 11f, true); Flame(q + Vector3.up * 1.25f); }
                else if (rng.Next(2) == 0 && i > 0) { Put("Crypte/D_Brasero", ctr + new Vector3(T * 0.5f, 0, 0), 0); Glow(ctr + new Vector3(T * 0.5f, 1.3f, 0), th.torch, 3f, 10f, true); Flame(ctr + new Vector3(T * 0.5f, 1.25f, 0)); }
                if (th.name.StartsWith("Fournaise") && !boss && i > 0) LavaCrack(ctr + new Vector3(-T, 0.03f, T * 0.5f));
                if (th.name.StartsWith("Crypte")) Puddle(ctr + new Vector3(T * 0.8f, 0.03f, -T * 0.6f));
                if (th.astral) for (int k = 0; k < 3; k++) Crystal(Cell(r.xMin + rng.Next(r.width), r.yMin + rng.Next(r.height)) + new Vector3(0, 6f + R() * 4f, 0), 1.2f + R(), true);
            }
        }

        void Flame(Vector3 p)
        {
            if (!FX.additive) return;
            var ps = new GameObject("Flamme").AddComponent<ParticleSystem>(); ps.transform.SetParent(root); ps.transform.position = p;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.startLifetime = 0.6f; m.startSpeed = 0.6f; m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f); m.maxParticles = 50;
            m.startColor = Color.Lerp(th.torch, Color.white, 0.25f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.2f;
            var em = ps.emission; em.rateOverTime = 34; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(th.torch, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) }); col.color = g;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = FX.additive; ps.Play();
        }

        void LavaCrack(Vector3 p)
        {
            var m = Mat(new Color(0.2f, 0.05f, 0.02f), 0.2f, new Color(2.4f, 0.7f, 0.12f));
            for (int k = 0; k < 4; k++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube); q.name = "Faille de lave"; Kill(q.GetComponent<Collider>()); q.transform.SetParent(root);
                q.transform.position = p + new Vector3(R() * 2 - 1, 0, R() * 2 - 1) * 1.6f; q.transform.rotation = Quaternion.Euler(0, rng.Next(180), 0);
                q.transform.localScale = new Vector3(0.18f + R() * 0.2f, 0.04f, 1.2f + R() * 2.2f); q.GetComponent<Renderer>().sharedMaterial = m;
            }
            Glow(p + Vector3.up * 0.6f, new Color(1f, 0.45f, 0.1f), 1.8f, 6f, true);
        }

        void Puddle(Vector3 p)
        {
            var m = Mat(new Color(0.05f, 0.09f, 0.12f), 0.97f);
            var q = GameObject.CreatePrimitive(PrimitiveType.Cylinder); q.name = "Eau sombre"; Kill(q.GetComponent<Collider>()); q.transform.SetParent(root);
            q.transform.position = p; q.transform.localScale = new Vector3(2.2f + R() * 2f, 0.01f, 1.6f + R() * 1.6f); q.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ---------------------------------------------------------------- canyon (bois pétrifié, glacier) : sol et falaises sculptés
        void BuildCanyon()
        {
            // distance de chaque point à la zone praticable → sol plat dedans, falaises qui montent dehors
            const float res = 2f;
            int nx = Mathf.CeilToInt(W * T / res) + 1, nz = Mathf.CeilToInt(H * T / res) + 1;
            var basePos = Cell(0, 0) - new Vector3(T / 2, 0, T / 2);
            var V = new Vector3[nx * nz]; var UV = new Vector2[nx * nz];
            float seedX = R() * 100, seedZ = R() * 100;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    var wp = basePos + new Vector3(i * res, 0, k * res);
                    float d = DistToOpen(wp);
                    float n = Mathf.PerlinNoise(seedX + wp.x * 0.09f, seedZ + wp.z * 0.09f);
                    float h = d <= 0 ? (n - 0.5f) * 0.25f : Mathf.Min(9f, Mathf.Pow(d, 1.15f) * 1.5f) + (n - 0.5f) * Mathf.Min(d, 3f) * 1.4f;
                    V[i * nz + k] = new Vector3(wp.x, h, wp.z); UV[i * nz + k] = new Vector2(wp.x, wp.z) / 6f;
                }
            var floorT = new List<int>(); var wallT = new List<int>();
            for (int i = 0; i < nx - 1; i++)
                for (int k = 0; k < nz - 1; k++)
                {
                    int a = i * nz + k, b = a + 1, c = a + nz, d = c + 1;
                    float slope = Mathf.Max(Mathf.Abs(V[a].y - V[d].y), Mathf.Abs(V[b].y - V[c].y));
                    var list = slope > 0.9f ? wallT : floorT;
                    list.Add(a); list.Add(b); list.Add(d); list.Add(a); list.Add(d); list.Add(c);
                }
            var mesh = new Mesh { name = "Canyon", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = V; mesh.uv = UV; mesh.subMeshCount = 2; mesh.SetTriangles(floorT, 0); mesh.SetTriangles(wallT, 1);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject("Sol du canyon"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Mat(Color.white, th.ice ? 0.30f : 0.12f, default, GroundTex(th.ground, th.ice ? 0.22f : 0.40f, 11)), Mat(Color.white, th.ice ? 0.45f : 0.18f, default, GroundTex(th.rock, 0.30f, 12, true)) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh; Physics.SyncTransforms();
            // décor : rochers sur les pentes, arbres morts ou gelés sur les crêtes, quelques blocs dans les salles
            var rockTint = th.ice ? new Color(0.75f, 0.88f, 1.05f) : new Color(0.78f, 0.78f, 0.74f);
            var treeTint = th.ice ? new Color(0.75f, 0.85f, 0.95f) : new Color(0.55f, 0.55f, 0.52f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (open[x, y]) continue;
                    var p = Cell(x, y); float d = DistToOpen(p);
                    if (d < 1f || d > 9f) continue;
                    var gp = Surface(p + new Vector3(R() * 2 - 1, 0, R() * 2 - 1), true);
                    int roll = rng.Next(10);
                    if (roll < 4) Put("Rocher" + rng.Next(3), gp + Vector3.down * 0.3f, rng.Next(360), 0.9f + R() * 1.1f, rockTint);
                    else if (roll < 7 && d > 2f) Put("Arbres/Pin" + rng.Next(3) + "_Arbre", gp + Vector3.down * 0.2f, rng.Next(360), 0.8f + R() * 0.5f, treeTint);
                    else if (roll < 8 && th.ice) Crystal(gp, 1.5f + R() * 2.5f, false);
                }
            for (int i = 1; i < rooms.Count; i++)
            {
                var r = rooms[i]; int deco = i == rooms.Count - 1 ? 0 : 1 + rng.Next(3);
                for (int k = 0; k < deco; k++)
                {
                    var p = Surface(Cell(r.xMin + 1 + rng.Next(Mathf.Max(1, r.width - 2)), r.yMin + 1 + rng.Next(Mathf.Max(1, r.height - 2))));
                    if (rng.Next(3) == 0) Put("Rocher" + rng.Next(3), p + Vector3.down * 0.2f, rng.Next(360), 0.5f + R() * 0.4f, rockTint);
                    else if (th.ice) Crystal(p, 1.2f + R() * 1.6f, false);
                    else { var b = Put("Ville/M_Caisses", p, rng.Next(360), 0.9f, new Color(0.7f, 0.7f, 0.65f)); if (b) Breakable.Make(b, 1.0f); }
                }
                // feux de camp / lueurs des salles
                var c = Surface(Cell((int)r.center.x, (int)r.center.y));
                if (th.ice) Crystal(c + new Vector3(T * 0.7f, 0, 0), 2.6f, false);
                else { Glow(c + new Vector3(0, 2.2f, 0), th.torch, 2.2f, 12f, true); Put("Crypte/D_Brasero", c + new Vector3(T * 0.7f, 0, 0), 0, 1f, new Color(0.6f, 0.6f, 0.55f)); Flame(c + new Vector3(T * 0.7f, 1.25f, 0)); }
            }
        }

        float DistToOpen(Vector3 wp)
        {
            int cx = Mathf.RoundToInt(wp.x / T) - origin.x, cy = Mathf.RoundToInt(wp.z / T) - origin.y;
            if (Open(cx, cy))
            {   // à l'intérieur : sol plat, sauf tout près d'un bord (pente douce de raccord)
                return -1f;
            }
            float best = 99f;
            for (int dx = -4; dx <= 4; dx++)
                for (int dy = -4; dy <= 4; dy++)
                {
                    if (!Open(cx + dx, cy + dy)) continue;
                    var c = Cell(cx + dx, cy + dy);
                    float ex = Mathf.Max(0, Mathf.Abs(wp.x - c.x) - T / 2), ez = Mathf.Max(0, Mathf.Abs(wp.z - c.z) - T / 2);
                    best = Mathf.Min(best, Mathf.Sqrt(ex * ex + ez * ez));
                }
            return best;
        }

        /// <summary>Point du sol : sondage depuis le haut (falaises du canyon) ou juste au-dessus (sols de crypte, sous les piliers).</summary>
        Vector3 Surface(Vector3 p, bool high = false)
        {
            if (Physics.Raycast(p + Vector3.up * (high ? 30f : 2.5f), Vector3.down, out var hit, high ? 60f : 10f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return p;
        }

        Texture2D GroundTex(Color c, float contrast, int seed, bool cracks = false)
        {
            const int S = 256; var px = new Color[S * S]; float o = seed * 13.7f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S, n = 0, amp = 0.5f, f = 4f;
                    for (int k = 0; k < 4; k++) { n += amp * (Tile(u, v, f, o + k * 5.3f) - 0.5f); amp *= 0.5f; f *= 2f; }
                    float s = 1f + n * contrast * 2.2f;
                    if (!cracks) { float patch = Tile(u, v, 2f, o + 120f); s *= Mathf.Lerp(0.78f, 1.12f, patch); }   // taches plus sombres / plus claires
                    if (cracks) { float w = Tile(u, v, 3f, o + 40f); s *= 0.82f + 0.18f * Mathf.Sin((v * 9f + w * 2.5f) * Mathf.PI * 2f); s *= Mathf.Lerp(0.8f, 1.05f, Tile(u, v, 16f, o + 80f)); }   // strates de roche
                    px[y * S + x] = new Color(c.r * s, c.g * s, c.b * s, 1);
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Sol de brèche" }; t.SetPixels(px); t.Apply(true, true);
            return t;
        }

        static float Tile(float u, float v, float f, float o)
        {   // bruit de Perlin rendu raccordable (mélange des quatre décalages périodiques)
            float a = Mathf.PerlinNoise(o + u * f, o + v * f), b = Mathf.PerlinNoise(o + (u - 1) * f, o + v * f);
            float c = Mathf.PerlinNoise(o + u * f, o + (v - 1) * f), d = Mathf.PerlinNoise(o + (u - 1) * f, o + (v - 1) * f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static Mesh crystalMesh;
        static Mesh CrystalMesh()
        {
            if (!crystalMesh)
            {
                var v = new List<Vector3>(); var tri = new List<int>(); const int N = 6;
                for (int i = 0; i < N; i++)
                {
                    float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                    var b0 = new Vector3(Mathf.Cos(a0) * 0.22f, 0, Mathf.Sin(a0) * 0.22f); var b1 = new Vector3(Mathf.Cos(a1) * 0.22f, 0, Mathf.Sin(a1) * 0.22f);
                    var t0 = new Vector3(Mathf.Cos(a0) * 0.2f, 0.78f, Mathf.Sin(a0) * 0.2f); var t1 = new Vector3(Mathf.Cos(a1) * 0.2f, 0.78f, Mathf.Sin(a1) * 0.2f); var tip = new Vector3(0, 1f, 0);
                    void Q(Vector3 a, Vector3 b, Vector3 c) { int k = v.Count; v.Add(a); v.Add(b); v.Add(c); tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); }
                    Q(b0, t1, b1); Q(b0, t0, t1); Q(t0, tip, t1);
                }
                crystalMesh = new Mesh { name = "Cristal" }; crystalMesh.SetVertices(v); crystalMesh.SetTriangles(tri, 0); crystalMesh.RecalculateNormals(); crystalMesh.RecalculateBounds();
            }
            return crystalMesh;
        }

        /// <summary>Amas de cristaux (prismes hexagonaux à pointe), utilisable hors des brèches (pierre de l'Oracle).</summary>
        public static Transform CrystalCluster(Transform parent, Vector3 localPos, float height, Color c, Material lit, int seed)
        {
            var r = new System.Random(seed);
            var m = lit ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c * 0.6f); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.6f);
            var cl = new GameObject("Cristaux").transform; cl.SetParent(parent, false); cl.localPosition = localPos;
            for (int i = 0; i < 6; i++)
            {
                var g = new GameObject("Cristal"); g.transform.SetParent(cl, false);
                g.transform.localPosition = new Vector3((float)r.NextDouble() - 0.5f, 0, (float)r.NextDouble() - 0.5f) * 0.6f;
                g.transform.localRotation = Quaternion.Euler((float)r.NextDouble() * 40 - 20, (float)r.NextDouble() * 360, (float)r.NextDouble() * 40 - 20);
                float s = height * (i == 0 ? 1f : 0.35f + (float)r.NextDouble() * 0.45f); g.transform.localScale = new Vector3(s * 0.9f, s, s * 0.9f);
                g.AddComponent<MeshFilter>().sharedMesh = CrystalMesh(); var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return cl;
        }

        void Crystal(Vector3 p, float height, bool floating)
        {
            CrystalMesh();
            var m = Mat(th.crystal * 0.6f, 0.95f, th.crystal * (th.ice ? 0.7f : 1.4f));
            var cl = new GameObject("Cristaux").transform; cl.SetParent(root); cl.position = p;
            int n = 3 + rng.Next(4);
            for (int i = 0; i < n; i++)
            {
                var g = new GameObject("Cristal"); g.transform.SetParent(cl, false);
                g.transform.localPosition = new Vector3(R() - 0.5f, 0, R() - 0.5f) * 0.9f;
                g.transform.localRotation = Quaternion.Euler(R() * 50 - 25, R() * 360, R() * 50 - 25);
                float s = height * (i == 0 ? 1f : 0.35f + R() * 0.5f); g.transform.localScale = new Vector3(s * 0.9f, s, s * 0.9f);
                g.AddComponent<MeshFilter>().sharedMesh = crystalMesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (floating) { var f = cl.gameObject.AddComponent<Floater>(); f.bob = 0.5f + R() * 0.5f; f.period = 5f + R() * 3f; f.spin = 10f + R() * 10f; }
            else { var col = cl.gameObject.AddComponent<CapsuleCollider>(); col.radius = 0.5f; col.height = height; col.center = Vector3.up * height * 0.5f; }
            Glow(p + Vector3.up * (floating ? 0 : height * 0.6f), th.crystal, floating ? 1.6f : 1.4f, floating ? 9f : 7f, false);
        }

        // ---------------------------------------------------------------- ambiance et lumières
        void Atmosphere(GameBootstrap game)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = th.sky; RenderSettings.ambientEquatorColor = th.eq; RenderSettings.ambientGroundColor = th.gnd;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = th.fog;
            RenderSettings.fogStartDistance = th.fogStart; RenderSettings.fogEndDistance = th.fogEnd;
            var cam = Camera.main; if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = th.bg; }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.transform.parent == null) Kill(l.gameObject);
            var sun = new GameObject("Lumière de la brèche").AddComponent<Light>(); sun.transform.SetParent(root); sun.type = LightType.Directional;
            sun.color = th.light; sun.intensity = th.crypt ? 0.85f : 1.05f; sun.shadows = LightShadows.None; sun.transform.rotation = Quaternion.Euler(58, 20 + rng.Next(140), 0);
            // particules d'ambiance qui suivent la caméra
            if (FX.alpha || FX.additive)
            {
                var ps = new GameObject("Ambiance").AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.AddComponent<FollowCamera>();
                var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 300; m.startLifetime = 7f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40, 12, 40);
                var em = ps.emission; em.rateOverTime = 30;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                switch (th.motes)
                {
                    case "neige": m.startColor = new Color(1, 1, 1, 0.85f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f); m.startSpeed = 0.3f; m.gravityModifier = 0.06f; em.rateOverTime = 70; r.sharedMaterial = FX.alpha ? FX.alpha : FX.additive; break;
                    case "braises": m.startColor = new Color(1f, 0.5f, 0.15f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f); m.startSpeed = 0.4f; m.gravityModifier = -0.04f; r.sharedMaterial = FX.additive; break;
                    case "etoiles": m.startColor = new Color(0.8f, 0.6f, 1f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f); m.startSpeed = 0.15f; m.gravityModifier = -0.01f; r.sharedMaterial = FX.additive; break;
                    case "spores": m.startColor = new Color(0.7f, 1f, 0.5f, 0.8f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); m.startSpeed = 0.2f; m.gravityModifier = -0.01f; r.sharedMaterial = FX.additive; break;
                    default: m.startColor = new Color(0.8f, 0.85f, 0.9f, 0.35f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f); m.startSpeed = 0.1f; r.sharedMaterial = FX.alpha ? FX.alpha : FX.additive; break;
                }
                if (r.sharedMaterial) ps.Play(); else Kill(ps.gameObject);
            }
            if (game) { game.heroLight = 0f; game.zoneName = th.name; }
        }

        // ---------------------------------------------------------------- départ, camps, sortie
        void Populate(GameBootstrap game)
        {
            if (!game) return;
            Physics.SyncTransforms();
            var start = rooms[0];
            game.playerSpawn = Surface(Cell((int)start.center.x, (int)start.center.y)) + Vector3.up * 0.1f;
            game.castleGate = game.playerSpawn;
            game.camps.Clear();
            for (int i = 1; i < rooms.Count; i++)
            {
                var r = rooms[i]; bool boss = i == rooms.Count - 1;
                int groups = boss ? 3 : 1 + (r.width * r.height > 40 ? 1 : 0);
                for (int g = 0; g < groups; g++)
                {
                    var c = Surface(Cell(r.xMin + 1 + rng.Next(Mathf.Max(1, r.width - 2)), r.yMin + 1 + rng.Next(Mathf.Max(1, r.height - 2))));
                    game.camps.Add(new GameBootstrap.Camp { enemyId = th.enemies[rng.Next(th.enemies.Length)], center = c, count = 2 + rng.Next(3), radius = Mathf.Min(r.width, r.height) * T * 0.3f });
                }
            }
            // cercle de runes au point de départ
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Cercle d'arrivée"; Kill(ring.GetComponent<Collider>()); ring.transform.SetParent(root);
            ring.transform.position = game.playerSpawn + Vector3.up * 0.02f; ring.transform.localScale = new Vector3(3.2f, 0.01f, 3.2f);
            ring.GetComponent<Renderer>().sharedMaterial = Mat(th.crystal * 0.3f, 0.6f, th.crystal * 0.55f);
            Glow(game.playerSpawn + Vector3.up * 2f, th.crystal, 1.6f, 9f, false);
        }
    }

    /// <summary>Suit la caméra principale (particules d'ambiance autour du joueur).</summary>
    public class FollowCamera : MonoBehaviour
    {
        void LateUpdate() { var c = Camera.main; if (c) transform.position = c.transform.position + c.transform.forward * 12f; }
    }
}
