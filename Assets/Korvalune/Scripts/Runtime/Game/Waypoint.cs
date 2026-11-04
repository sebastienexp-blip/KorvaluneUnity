using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Pierres de voyage : une dans chaque région extérieure (les donjons, Crypte et Terriers, n'en ont pas), près du point
    /// d'arrivée. S'en approcher ouvre le même menu qui réunit TOUTES les régions du monde ; une région s'ouvre à partir de
    /// son niveau. Voyager charge la région et pose le héros à côté de sa pierre de voyage.</summary>
    public class Waypoint : MonoBehaviour
    {
        public class Region { public string scene, name, desc; public int level; }

        /// <summary>Toutes les régions du monde (ajouter ici chaque nouvelle région : elle apparaît dans tous les téléporteurs).</summary>
        public static readonly Region[] Regions =
        {
            new Region { scene = "Korvalune_Vallee",   name = "Vallée de Korvalune",   level = 1,   desc = "La ville, le château, le vieux cimetière" },
            new Region { scene = "Korvalune_Foret",    name = "Forêt de l'Astral",     level = 8,   desc = "Bois profonds et menhirs astraux" },
            new Region { scene = "Korvalune_Eclipse",  name = "Cité de l'Éclipse",     level = 14,  desc = "Le Refuge Lunaire, au bord du golfe" },
            new Region { scene = "Korvalune_Desert",   name = "Désert des Étoiles",    level = 25,  desc = "Dunes et temple des étoiles" },
            new Region { scene = "Korvalune_Pics",     name = "Pics Sélénites",        level = 45,  desc = "Cols de montagne et tours de guet" },
            new Region { scene = "Korvalune_Cote",     name = "Côte des Naufrages",    level = 65,  desc = "Falaises, épaves et Mer des Tempêtes" },
            new Region { scene = "Korvalune_Marais",   name = "Marais de Brumenoire",  level = 85,  desc = "Vase, mares et feux follets" },
            new Region { scene = "Korvalune_Toundra",  name = "Toundra de Givrecœur",  level = 105, desc = "Neige éternelle et lacs gelés" },
            new Region { scene = "Korvalune_Volcan",   name = "Terres de Cendrefer",   level = 125, desc = "Cendre, lave et forges oubliées" },
            new Region { scene = "Korvalune_Canyon",   name = "Canyon Écarlate",       level = 150, desc = "Gorges et pitons de grès rouge" },
            new Region { scene = "Korvalune_Abime",    name = "Abîme du Néant",        level = 175, desc = "Le bord du monde, jusqu'au niveau 200" },
        };

        public static Region Here { get { var s = SceneManager.GetActiveScene().name; foreach (var r in Regions) if (r.scene == s) return r; return null; } }

        Transform hero; bool shown; GameObject panel;

        /// <summary>Sol libre (sans bâtiment, étal, arbre…) le plus proche autour de « center », entre rMin et rMax, à
        /// distance « gap » des points déjà pris.</summary>
        static Vector3? FreeSpot(Vector3 center, float rMin, float rMax, float half, System.Collections.Generic.List<Vector3> taken, float gap)
        {
            var hits = new Collider[8];
            for (float r = rMin; r <= rMax; r += 2f)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f + r * 0.37f;
                    var p = GameBootstrap.Ground(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
                    bool ok = true; foreach (var t in taken) if ((t - p).sqrMagnitude < gap * gap) { ok = false; break; }
                    if (!ok) continue;
                    int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.6f, new Vector3(half, 1.4f, half), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider) && !hits[i].GetComponentInParent<PlayerController>()) { ok = false; break; }
                    if (ok) return p;
                }
            return null;
        }

        /// <summary>Pose la pierre de voyage de la région actuelle, sur un sol libre près de l'arrivée.</summary>
        public static void Place(GameBootstrap game)
        {
            if (Here == null || Rift.Active) return;
            var spawn = game.playerSpawn; var taken = new System.Collections.Generic.List<Vector3> { spawn };
            var pos = FreeSpot(spawn, 4f, 16f, 1.6f, taken, 3f) ?? GameBootstrap.Ground(spawn + new Vector3(5f, 0, 4f));
            Build(game, pos);
        }

        /// <summary>En ville (vallée) : un portail magique par région extérieure, sur des emplacements libres autour de
        /// l'arrivée (à appeler après la pierre de voyage et l'écurie, dont les colliders sont évités).</summary>
        public static void PlaceTownPortals(GameBootstrap game)
        {
            if (Here == null || Rift.Active || Here.scene != "Korvalune_Vallee") return;
            var spawn = game.playerSpawn; var taken = new System.Collections.Generic.List<Vector3> { spawn };
            foreach (var w in FindObjectsByType<Waypoint>(FindObjectsSortMode.None)) taken.Add(w.transform.position);
            foreach (var r in Regions)
            {
                if (r.scene == Here.scene) continue;
                var p = FreeSpot(spawn, 7f, 44f, 1.5f, taken, 4.5f); if (p == null) continue;
                taken.Add(p.Value);
                var target = r;
                var g = new GameObject("Portail : " + r.name).AddComponent<Portal>(); g.transform.position = p.Value;
                g.gate = true; g.glowColor = new Color(0.45f, 0.75f, 1f); g.label = r.name; g.targetScene = r.scene;
                int lvl = game.Player ? game.Player.level : 1;
                g.displayName = lvl >= r.level ? r.name : $"{r.name}\n<size=40>Niveau {r.level} requis</size>";
                g.canEnter = () =>
                {
                    if ((game.Player ? game.Player.level : 1) >= target.level) return true;
                    game.Hud?.Notify($"{target.name} : niveau {target.level} requis");
                    return false;
                };
                g.onEnter = () => { GameBootstrap.nextSpawn = null; SceneManager.LoadScene(target.scene); };
            }
        }

        /// <summary>Pierre taillée texturée (celle des maisons et de la fontaine de la ville), teintée, répétée « tiling » fois ;
        /// null si le décor de la ville n'est pas chargé.</summary>
        public static Material TexturedStone(Color tint, float tiling, string source = "Prefabs/World/Ville/M_Fontaine")
        {
            var pf = Res.Load<GameObject>(source); if (!pf) return null;
            var r = pf.GetComponentInChildren<Renderer>(); if (!r || !r.sharedMaterial) return null;
            var m = new Material(r.sharedMaterial);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            return m;
        }

        static void Build(GameBootstrap game, Vector3 pos)
        {
            var root = new GameObject("Pierre de voyage"); root.transform.position = pos;
            var lit = game && game.config && game.config.litMat ? game.config.litMat : LootDrop.litBase;
            Material M(Color c, Color glow)
            {
                var m = lit ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
                if (glow.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", glow); }
                return m;
            }
            var stone = TexturedStone(new Color(0.92f, 0.9f, 0.95f), 2f) ?? M(new Color(0.48f, 0.47f, 0.50f), Color.black); var rune = M(new Color(0.2f, 0.5f, 0.9f), new Color(0.35f, 0.9f, 2.2f));
            void Part(PrimitiveType t, Vector3 lp, Vector3 sc, Vector3 rot, Material m, bool keepCol = false)
            {
                var g = GameObject.CreatePrimitive(t); g.transform.SetParent(root.transform, false); g.transform.localPosition = lp; g.transform.localScale = sc; g.transform.localRotation = Quaternion.Euler(rot);
                g.GetComponent<Renderer>().sharedMaterial = m; if (!keepCol) Destroy(g.GetComponent<Collider>());
            }
            // dalle ronde, trois menhirs inclinés vers le centre, rune flottante
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0), new Vector3(3.2f, 0.08f, 3.2f), Vector3.zero, stone);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f; var d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Part(PrimitiveType.Cube, d * 1.15f + Vector3.up * 1.1f, new Vector3(0.45f, 2.2f, 0.32f), new Vector3(-8, a, 0), stone, true);
                Part(PrimitiveType.Cube, d * 1.02f + Vector3.up * 1.4f, new Vector3(0.12f, 0.9f, 0.02f), new Vector3(-8, a, 0), rune);   // rune gravée lumineuse
            }
            var crys = RiftLevel.CrystalCluster(root.transform, new Vector3(0, 1.9f, 0), 0.7f, new Color(0.4f, 0.75f, 1f), lit, 3);
            var fl = crys.gameObject.AddComponent<Floater>(); fl.bob = 0.2f; fl.period = 3.5f; fl.spin = 30f;
            RiftLevel.RuneCircle(root.transform, pos + Vector3.up * 0.17f, 3.0f, new Color(0.4f, 0.75f, 1f));
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0, 2.2f, 0);
            l.type = LightType.Point; l.color = new Color(0.45f, 0.75f, 1f); l.intensity = 2.4f; l.range = 8f; l.shadows = LightShadows.None;
            var tag = new GameObject("Nom").AddComponent<TextMesh>(); tag.transform.SetParent(root.transform, false); tag.transform.localPosition = Vector3.up * 3.3f;
            tag.text = "Pierre de voyage"; tag.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tag.GetComponent<MeshRenderer>().sharedMaterial = tag.font.material;
            tag.fontSize = 56; tag.characterSize = 0.045f; tag.anchor = TextAnchor.LowerCenter; tag.color = new Color(0.6f, 0.85f, 1f); tag.fontStyle = FontStyle.Bold;
            tag.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tag.gameObject.AddComponent<FaceCamera>();
            root.AddComponent<Waypoint>();
        }

        void Update()
        {
            if (!hero) { var p = FindFirstObjectByType<PlayerController>(); if (!p) return; hero = p.transform; }
            if (!GetComponent<Usable>()) Usable.Add(gameObject, "Pierre de voyage", 4.5f, () => { if (!panel) Open(); });
            float d = Vector3.Distance(hero.position, transform.position);
            if (d < 2.6f && !shown && !panel) { shown = true; Open(); }
            if (d > 5f) shown = false;
        }

        void Open()
        {
            var pc = FindFirstObjectByType<PlayerController>(); int lvl = pc ? pc.level : 1;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Voyage", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = panel.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 250;
            var sc = panel.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            var root = (RectTransform)panel.transform;
            Image I(RectTransform parent, Sprite s, Vector2 pos, Vector2 size, Color c) { var im = new GameObject("I", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); im.rectTransform.SetParent(parent, false); im.rectTransform.anchoredPosition = pos; im.rectTransform.sizeDelta = size; im.sprite = s; im.type = Image.Type.Sliced; im.color = c; return im; }
            Text L(RectTransform parent, string s, Vector2 pos, Vector2 size, int fs, Color c, TextAnchor a = TextAnchor.MiddleCenter) { var t = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(parent, false); t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = size; t.font = font; t.fontSize = fs; t.alignment = a; t.color = c; t.text = s; t.raycastTarget = false; t.supportRichText = true; t.gameObject.AddComponent<Outline>().effectColor = Color.black; return t; }
            var shade = I(root, null, Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0.6f)); shade.raycastTarget = true;
            int rows = (Regions.Length + 1) / 2;                                   // deux colonnes
            float h = 250 + rows * 112, W = 1700;
            var p = I(root, UIStyle.Ornate(), Vector2.zero, new Vector2(W, h), Color.white).rectTransform;
            I(p, null, Vector2.zero, new Vector2(W - 70, h - 70), new Color(0.30f, 0.235f, 0.17f, 0.96f));
            L(p, "Pierre de voyage", new Vector2(0, h / 2 - 70), new Vector2(900, 70), 44, UIStyle.Gold).fontStyle = FontStyle.Bold;
            var here = Here;
            for (int k = 0; k < Regions.Length; k++)
            {
                var r = Regions[k]; int col = k / rows, line = k % rows;
                float x = col == 0 ? -400 : 400, y = h / 2 - 160 - line * 112;
                bool open = lvl >= r.level, cur = here != null && here.scene == r.scene;
                var row = I(p, UIStyle.Slot(), new Vector2(x, y), new Vector2(780, 100), cur ? new Color(0.55f, 0.75f, 1f) : open ? new Color(0.95f, 0.82f, 0.62f) : new Color(0.5f, 0.46f, 0.42f));
                L(row.rectTransform, $"<b>{r.name}</b>", new Vector2(-120, 18), new Vector2(500, 44), 30, open ? Color.white : new Color(0.75f, 0.72f, 0.68f), TextAnchor.MiddleLeft);
                L(row.rectTransform, r.desc, new Vector2(-120, -22), new Vector2(500, 34), 21, UIStyle.Parch, TextAnchor.MiddleLeft);
                L(row.rectTransform, cur ? "<color=#9fd7ff>Vous êtes ici</color>" : open ? $"Niv. {r.level}+\n<color=#ffd95a>Voyager</color>" : $"<color=#ff8a7a>Niveau {r.level}\nrequis</color>", new Vector2(270, 0), new Vector2(220, 90), 25, Color.white);
                if (open && !cur)
                {
                    row.raycastTarget = true; var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = row; var target = r;
                    b.onClick.AddListener(() => { Sound.UI("sfx_ui_clic", 0.6f); Travel(target); });
                }
            }
            var close = I(p, UIStyle.Slot(), new Vector2(0, -h / 2 + 70), new Vector2(300, 74), new Color(0.8f, 0.7f, 0.6f)); close.raycastTarget = true;
            L(close.rectTransform, "Fermer", Vector2.zero, new Vector2(300, 74), 30, UIStyle.Gold);
            var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = close; cb.onClick.AddListener(Close);
            Sound.UI("sfx_ui_ouvrir", 0.7f);
        }

        void Travel(Region r)
        {
            Close();
            FindFirstObjectByType<GameBootstrap>()?.Persist();
            GameBootstrap.nextSpawn = null;            // arrivée : point de départ de la région, à côté de sa pierre de voyage
            Sound.Play("sfx_teleport", transform.position, 0.9f);
            Vfx.Shock(transform.position, new Color(0.45f, 0.75f, 1f), 4f);
            SceneManager.LoadScene(r.scene);
        }

        void Close() { if (panel) Destroy(panel); panel = null; }
    }
}
