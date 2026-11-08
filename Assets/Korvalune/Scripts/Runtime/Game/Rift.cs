using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Les Brèches astrales : des failles ouvertes dans les donjons, remplies de monstres de toutes sortes.
    /// Chaque monstre abattu remplit la jauge de la brèche ; pleine, elle fait surgir le Gardien de la brèche.
    /// Brèche astrale : sans limite de temps ; le Gardien lâche un Sceau de brèche (clé des Grandes Brèches).
    /// Grande Brèche (niveau 1 et plus) : monstres de plus en plus robustes, 15 minutes pour abattre le Gardien ; réussie,
    /// elle débloque les niveaux suivants (jusqu'à +3 si l'on a été rapide) et rapporte davantage.</summary>
    public static class Rift
    {
        public static bool Active;
        public static int Tier;                          // 0 = brèche astrale ; ≥ 1 = Grande Brèche de ce niveau
        public const float TimeLimit = 15 * 60f;
        // brèche astrale laissée ouverte (gardien vaincu, ou quittée par un portail) : reprise ou fermeture à la statue
        public static int? KeepSeed, KeepTheme;
        public static bool Suspended, ResumeDone;
        static string susScene; static Vector3 susPos; static int susSeed, susTheme, susTier; static bool susDone;
        public static bool SuspendedDone => susDone;
        public static void Suspend(string scene, Vector3 pos, int seed, int theme, int tier, bool done)
        {
            Suspended = true; susScene = scene; susPos = pos; susSeed = seed; susTheme = theme; susTier = tier; susDone = done;
        }
        /// <summary>Ferme la brèche laissée ouverte (bouton de la statue).</summary>
        public static void Forget() { Suspended = false; }
        /// <summary>Reprend la brèche laissée ouverte, au même endroit et avec le même niveau.</summary>
        public static void ResumeOpen()
        {
            if (!Suspended) return;
            Suspended = false; Active = true; Tier = susTier; ResumeDone = susDone;
            KeepSeed = susSeed; KeepTheme = susTheme;
            GameBootstrap.nextSpawn = susPos;
            SceneManager.LoadScene(susScene);
        }
        static readonly string[] Scenes = { "Korvalune_Breche" };                               // niveau généré à chaque entrée (RiftLevel)
        static readonly string[] Legacy = { "Korvalune_Crypte", "Korvalune_Terriers" };
        public static bool IsRiftScene(string s) => System.Array.IndexOf(Scenes, s) >= 0 || System.Array.IndexOf(Legacy, s) >= 0;
        public static float HpMult => Tier <= 0 ? 1.4f : 1.4f * Mathf.Pow(1.17f, Tier);
        public static float DmgMult => Tier <= 0 ? 1.15f : 1.15f * Mathf.Pow(1.08f, Tier);
        public static float XpMult => 1.5f + 0.15f * Tier;
        public static string Title => Tier <= 0 ? "Brèche astrale" : $"Grande Brèche — niveau {Tier}";

        public static Vector3? ReturnPoint;                 // devant la statue des Brèches (arrivée au retour)
        public static int Deaths;

        /// <summary>Retour en ville, devant la statue des Brèches.</summary>
        public static void ReturnToTown()
        {
            var game = Object.FindFirstObjectByType<GameBootstrap>(); if (game) game.Persist();
            GameBootstrap.nextSpawn = ReturnPoint; Active = false;
            SceneManager.LoadScene(TownPortal.Town);
        }

        public static void Enter(int tier)
        {
            Deaths = 0;
            KeepSeed = null; KeepTheme = null; Suspended = false; ResumeDone = false;   // une nouvelle brèche remplace l'ancienne
            var game = Object.FindFirstObjectByType<GameBootstrap>();
            if (game) game.Persist();
            Active = true; Tier = Mathf.Max(0, tier);
            GameBootstrap.nextSpawn = null;
            var scene = Scenes[Random.Range(0, Scenes.Length)];
            if (!Application.CanStreamedLevelBeLoaded(scene)) scene = Legacy[Random.Range(0, Legacy.Length)];
            SceneManager.LoadScene(scene);
        }

        /// <summary>Appelé par la scène au démarrage, avant le peuplement : la brèche remplace les camps et la quête.</summary>
        public static void Prepare(GameBootstrap game)
        {
            if (Active && !IsRiftScene(SceneManager.GetActiveScene().name)) Active = false;     // retour en ville : brèche refermée
            if (!Active || !game.config) return;
            var pool = new List<string>();
            bool themed = game.GetComponent<RiftLevel>() && RiftLevel.ThemeEnemies.Count > 0;            // niveau généré : ennemis du décor
            foreach (var e in game.config.enemies) if (e.prefab && (!themed || RiftLevel.ThemeEnemies.Contains(e.id))) pool.Add(e.id);
            if (pool.Count == 0) foreach (var e in game.config.enemies) if (e.prefab) pool.Add(e.id);
            var rng = new System.Random();
            var list = new List<GameBootstrap.Camp>();
            foreach (var c in game.camps)
            {
                // chaque camp devient un groupe mêlé, plus fourni
                int n = Mathf.CeilToInt(Mathf.Max(2, c.count) * 1.7f);
                for (int i = 0; i < n; i++)
                    list.Add(new GameBootstrap.Camp { enemyId = pool[rng.Next(pool.Count)], center = c.center, count = 1, radius = c.radius + 1.5f });
            }
            game.camps.Clear(); game.camps.AddRange(list);
            game.questSteps.Clear();
            game.questSteps.Add(new Quest.Objective { text = "Remplir la jauge de la brèche", enemyId = "__breche__", count = 1 });
            game.questTitle = Title; game.zoneName = themed ? RiftLevel.ThemeName : Title;
        }
    }

    /// <summary>Déroulement d'une brèche : jauge, chrono, apparition du Gardien, récompenses et portail de retour.</summary>
    public class RiftDirector : MonoBehaviour
    {
        GameBootstrap game; float required, progress, startedAt; bool guardianUp, done, timedOut;
        Health guardian; Text bar, timer; Image fill; RectTransform ui;

        public static void Begin(GameBootstrap g)
        {
            if (!Rift.Active) return;
            var d = g.gameObject.AddComponent<RiftDirector>(); d.game = g; d.startedAt = Time.time;
            int total = 0; foreach (var c in g.camps) total += c.count;
            d.required = Mathf.Max(20, total * 0.6f);
            // ambiance de brèche : lumière violacée, brume
            if (!g.GetComponent<RiftLevel>())
            {   // anciennes scènes : teinte violacée et brume légère (linéaire : seul le lointain s'estompe)
                RenderSettings.ambientSkyColor *= new Color(0.95f, 0.85f, 1.1f); RenderSettings.ambientEquatorColor *= new Color(0.95f, 0.85f, 1.1f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 28f; RenderSettings.fogEndDistance = 80f; RenderSettings.fogColor = new Color(0.16f, 0.1f, 0.22f);
            }
            Settings.Rebase(); Settings.Apply();
            Health.AnyDied += d.OnDied;
            var pop = g.GetComponent<WorldPopulation>(); if (pop) pop.noRespawn = true;
            d.BuildUi();
            g.Hud.Notify(Rift.Title);
            if (Rift.ResumeDone) { Rift.ResumeDone = false; d.done = true; d.OpenBack(g.Player.transform.position); g.Hud.Notify("Brèche rouverte : le gardien a déjà été vaincu"); }
        }

        void OnDestroy() { Health.AnyDied -= OnDied; }

        public bool Done => done;

        /// <summary>Portail « Revenir » : depuis une brèche astrale, on quitte en la laissant ouverte.</summary>
        void OpenBack(Vector3 pos)
        {
            var back = new GameObject("Retour en ville").AddComponent<Portal>();
            back.transform.position = LootDrop.Near(pos, new Vector3(3, 0, 0));
            back.targetScene = "Korvalune_Vallee"; back.label = "Revenir"; back.displayName = "Vallée de Korvalune"; back.glowColor = new Color(0.75f, 0.45f, 1f);
            back.arrival = game.playerSpawn;          // remplacé par le point d'arrivée de la vallée (voir Portal)
            back.useSceneSpawn = true; back.gate = true;
            back.onEnter = () => { if (Rift.Tier <= 0) LeaveOpen(); else Rift.ReturnToTown(); };
        }

        /// <summary>Quitte la brèche astrale sans la fermer : elle reste ouverte jusqu'à fermeture à la statue.</summary>
        void LeaveOpen()
        {
            var lvl = game.GetComponent<RiftLevel>();
            Rift.Suspend(SceneManager.GetActiveScene().name, game.Player.transform.position, lvl ? lvl.Seed : 0, lvl ? lvl.ThemeIndex : -1, Rift.Tier, done);
            Rift.ReturnToTown();
        }

        void OnDied(Health h)
        {
            if (done || !h || h.faction != Faction.Enemy) return;
            if (h == guardian) { Victory(); return; }
            var ai = h.GetComponent<EnemyAI>(); if (ai && ai.side != Faction.Enemy) return;
            float pts = 1f + (ai && ai.champion ? 4f : 0f) + (h.GetComponent<TreasureThief>() ? 2f : 0f);
            progress = Mathf.Min(required, progress + pts);
            if (!guardianUp && progress >= required) SummonGuardian();
        }

        void SummonGuardian()
        {
            guardianUp = true;
            var p = game.Player.transform;
            var at = LootDrop.Near(p.position, p.forward * 7f);
            var t = game.config.Enemy("demon") ?? game.config.enemies[0];
            Vfx.Shock(at, new Color(0.8f, 0.4f, 1f), 5f); Vfx.Explosion(at, Element.Neant, 3f); Sound.Play("sfx_demon_rugit", at, 1f);
            var e = game.Spawn(t, at, Mathf.Max(1, game.Player.level + 2 + Rift.Tier / 4));
            guardian = e.GetComponent<Health>();
            guardian.maxHp = Mathf.Round(guardian.maxHp * 9f); guardian.hp = guardian.maxHp; guardian.armor *= 1.5f;
            guardian.displayName = "Gardien de la brèche";
            var ai = e.GetComponent<EnemyAI>(); if (ai) { ai.champion = true; ai.damage = (ai.damage > 0 ? ai.damage : ai.type.damage) * 1.6f; ai.xp = (ai.xp > 0 ? ai.xp : ai.type.xp) * 10f; }
            e.transform.localScale *= 1.6f;
            var l = new GameObject("Aura").AddComponent<Light>(); l.transform.SetParent(e.transform, false); l.transform.localPosition = Vector3.up * 2f;
            l.type = LightType.Point; l.color = new Color(0.75f, 0.35f, 1f); l.intensity = 3.5f; l.range = 8f; l.shadows = LightShadows.None;
            game.Hud.Notify("<color=#c58cff>Le Gardien de la brèche surgit !</color>");
        }

        void Victory()
        {
            done = true; QuestEvents.Raise("breche"); if (Rift.Tier > 0) QuestEvents.Raise("grande_breche");
            var pos = guardian ? guardian.transform.position : game.Player.transform.position;
            var w = game.Player.wallet; var save = SaveSystem.Current;
            float used = Time.time - startedAt; var rng = new System.Random(); int lvl = game.Player.level;
            string msg;
            if (Rift.Tier <= 0)
            {
                w.riftKeys++; msg = "Gardien vaincu ! +1 Sceau de brèche — la brèche reste ouverte, fermez-la à la statue";
            }
            else if (!timedOut)
            {
                int gain = used < Rift.TimeLimit * 0.5f ? 3 : used < Rift.TimeLimit * 0.8f ? 2 : 1;
                w.riftBest = Mathf.Max(w.riftBest, Rift.Tier + gain - 1);
                msg = $"Grande Brèche {Rift.Tier} réussie en {Mathf.FloorToInt(used / 60)} min {Mathf.FloorToInt(used % 60):00} s — niveau {Rift.Tier + gain} débloqué";
            }
            else msg = "Gardien vaincu, mais trop tard : pas de nouveau niveau";
            // récompenses : pluie d'or, objets (au moins un légendaire), éclats
            int items = 4 + Mathf.Min(6, Rift.Tier / 5) + (timedOut ? -2 : 0);
            for (int i = 0; i < items; i++)
            {
                var it = Loot.Roll(rng, lvl, 3f + Rift.Tier * 0.4f + Difficulty.Luck);
                if (i == 0 && it.rarity < Rarity.Legendaire && !timedOut) it = Loot.Roll(rng, lvl, 200f);
                var o = Random.insideUnitCircle * 2.5f; LootDrop.Spawn(it, LootDrop.Near(pos, new Vector3(o.x, 0, o.y)));
            }
            for (int i = 0; i < 10; i++) { var o = Random.insideUnitCircle * 3f; LootDrop.SpawnGold((long)(Economy.GoldDrop(rng, lvl, 1f) * (2 + Rift.Tier * 0.3f)), LootDrop.Near(pos, new Vector3(o.x, 0, o.y))); }
            w.starShard += 2 + Rift.Tier; w.shadow += 3 + Rift.Tier;
            game.Player.Notify?.Invoke($"<color=#c58cff>{msg}</color>"); game.Hud.Notify(msg);
            Sound.Play("sfx_quete", pos, 1f);
            OpenBack(pos);
            if (save != null) game.Persist();
            if (Rift.Tier > 0) returnAt = Time.time + 15f;   // Grande Brèche : retour automatique ; brèche astrale : reste ouverte
        }

        float returnAt = -1;
        /// <summary>Mort dans une brèche : on revient au départ de la brèche, sans retour en ville. En Grande Brèche, chaque
        /// mort coûte du temps (5 s, puis 10 s, 15 s… jusqu'à 30 s par mort).</summary>
        public static int Penalty()
        {
            Rift.Deaths++;
            var d = FindFirstObjectByType<RiftDirector>(); if (!d || Rift.Tier <= 0 || d.done) return 0;
            int sec = Mathf.Min(30, 5 * Rift.Deaths);
            d.startedAt -= sec;
            return sec;
        }

        void Update()
        {
            if (done && returnAt > 0)
            {
                float r = returnAt - Time.time;
                if (r <= 0) { returnAt = -1; Rift.ReturnToTown(); return; }
                if (bar) bar.text = $"Brèche refermée — retour en ville dans {Mathf.CeilToInt(r)} s";
                return;
            }
            if (!ui) return;
            float left = Rift.TimeLimit - (Time.time - startedAt);
            if (Rift.Tier > 0 && !done && !timedOut && left <= 0) { timedOut = true; game.Hud.Notify("<color=#ff6060>Temps écoulé</color>"); }
            fill.fillAmount = guardianUp ? (guardian && !guardian.Dead ? guardian.hp / guardian.maxHp : 0) : progress / required;
            fill.color = guardianUp ? new Color(0.85f, 0.2f, 0.25f) : new Color(0.7f, 0.4f, 1f);
            bar.text = done ? (Rift.Tier <= 0 ? "Brèche ouverte — gardien vaincu" : "Brèche refermée") : guardianUp ? "Gardien de la brèche" : $"{Rift.Title} — {Mathf.FloorToInt(100 * progress / required)} %";
            if (timer) timer.text = Rift.Tier <= 0 ? (Rift.Deaths > 0 ? $"☠ {Rift.Deaths}" : "") : timedOut ? "00:00" : $"{Mathf.FloorToInt(Mathf.Max(0, left) / 60):00}:{Mathf.FloorToInt(Mathf.Max(0, left) % 60):00}";
            if (timer) timer.color = left < 60 && !done ? new Color(1f, 0.4f, 0.35f) : Color.white;
        }

        void BuildUi()
        {
            var canvas = game.Hud.Canvas; if (!canvas) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui = new GameObject("Jauge de brèche", typeof(RectTransform)).GetComponent<RectTransform>();
            ui.SetParent(canvas.transform, false); ui.anchorMin = ui.anchorMax = new Vector2(0.5f, 1); ui.pivot = new Vector2(0.5f, 1); ui.anchoredPosition = new Vector2(0, -130); ui.sizeDelta = new Vector2(620, 70);
            var back = new GameObject("Fond", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); back.rectTransform.SetParent(ui, false);
            back.rectTransform.anchorMin = new Vector2(0, 0); back.rectTransform.anchorMax = new Vector2(1, 0.5f); back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            back.sprite = UIStyle.Bar(); back.type = Image.Type.Sliced; back.color = new Color(0.08f, 0.05f, 0.1f, 0.9f); back.raycastTarget = false;
            fill = new GameObject("Jauge", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.rectTransform.SetParent(back.rectTransform, false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.offsetMin = new Vector2(4, 4); fill.rectTransform.offsetMax = new Vector2(-4, -4);
            fill.sprite = UIStyle.Bar(); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.raycastTarget = false;
            Text T(string n, Vector2 amin, Vector2 amax, TextAnchor a, int size)
            {
                var t = new GameObject(n, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(ui, false);
                t.rectTransform.anchorMin = amin; t.rectTransform.anchorMax = amax; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false; t.gameObject.AddComponent<Outline>().effectColor = Color.black; return t;
            }
            bar = T("Titre", new Vector2(0, 0.5f), new Vector2(1, 1), TextAnchor.LowerCenter, 24);
            timer = T("Chrono", new Vector2(1, 0), new Vector2(1.25f, 0.5f), TextAnchor.MiddleLeft, 26);
        }
    }

    /// <summary>Oracle des Brèches (en ville) : on s'en approche pour ouvrir une brèche astrale ou une Grande Brèche.</summary>
    public class RiftKeeper : MonoBehaviour
    {
        Transform hero; bool shown; GameObject panel; int pick = 1;

        Transform gateAt; Portal gate;

        /// <summary>Statue des Brèches (une seule, sur la place de la ville principale) : socle de pierre à degrés, mystique
        /// de pierre veinée de violet qui tend les bras, cristal flottant au-dessus d'elle. On s'en approche pour choisir une
        /// brèche ; un portail violet s'ouvre alors devant la statue.</summary>
        public static void Place(GameBootstrap game, Vector3 pos, float yaw)
        {
            var root = new GameObject("Statue des Brèches"); root.transform.position = pos; root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var lit = game && game.config && game.config.litMat ? game.config.litMat : LootDrop.litBase;
            Material Stone(Color c, Color glow)
            {
                var m = lit ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
                if (glow.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", glow); }
                return m;
            }
            var stone = Waypoint.TexturedStone(new Color(0.9f, 0.88f, 0.96f), 2f) ?? Stone(new Color(0.46f, 0.44f, 0.50f), Color.black);
            var dark = Waypoint.TexturedStone(new Color(0.62f, 0.6f, 0.68f), 3f) ?? Stone(new Color(0.30f, 0.28f, 0.34f), Color.black);
            var statueMat = Stone(new Color(0.52f, 0.50f, 0.56f), new Color(0.12f, 0.04f, 0.22f));
            GameObject Cyl(string n, Vector3 lp, Vector3 sc, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = n; g.transform.SetParent(root.transform, false); g.transform.localPosition = lp; g.transform.localScale = sc;
                g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            Cyl("Degré", new Vector3(0, 0.15f, 0), new Vector3(3.6f, 0.15f, 3.6f), dark);
            Cyl("Degré", new Vector3(0, 0.45f, 0), new Vector3(2.8f, 0.15f, 2.8f), stone);
            Cyl("Socle", new Vector3(0, 1.05f, 0), new Vector3(1.6f, 0.45f, 1.6f), stone);
            Cyl("Corniche", new Vector3(0, 1.55f, 0), new Vector3(1.9f, 0.06f, 1.9f), dark);
            // la mystique de pierre
            var pf = Res.Load<GameObject>("Prefabs/Characters/PNJ_Mystique");
            if (pf)
            {
                var st = Object.Instantiate(pf, root.transform); st.name = "Statue"; st.transform.localPosition = new Vector3(0, 1.6f, 0); st.transform.localRotation = Quaternion.identity; st.transform.localScale = Vector3.one * 1.35f;
                foreach (var c in st.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                foreach (var r in st.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = statueMat; r.sharedMaterials = ms; }
                st.AddComponent<StatuePose>();
            }
            // cristal au-dessus des mains, runes au sol là où s'ouvre le portail
            var crys = RiftLevel.CrystalCluster(root.transform, new Vector3(0, 4.7f, 0), 1.0f, new Color(0.72f, 0.40f, 1f), lit, 7);
            var fl = crys.gameObject.AddComponent<Floater>(); fl.bob = 0.25f; fl.period = 4f; fl.spin = 22f;
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0, 4.6f, 0.6f); l.type = LightType.Point; l.color = new Color(0.7f, 0.4f, 1f); l.intensity = 3f; l.range = 10f; l.shadows = LightShadows.None;
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1.2f, 0); col.radius = 1.6f; col.height = 2.4f;
            var tag = new GameObject("Nom").AddComponent<TextMesh>(); tag.transform.SetParent(root.transform, false); tag.transform.localPosition = Vector3.up * 5.8f;
            tag.text = "Statue des Brèches"; tag.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tag.GetComponent<MeshRenderer>().sharedMaterial = tag.font.material;
            tag.fontSize = 56; tag.characterSize = 0.05f; tag.anchor = TextAnchor.LowerCenter; tag.color = new Color(0.8f, 0.6f, 1f); tag.fontStyle = FontStyle.Bold;
            // le nom fait toujours face à la caméra et ne projette pas d'ombre (sinon, vu d'en haut : des plaques grises)
            var tr = tag.GetComponent<MeshRenderer>(); tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
            tag.gameObject.AddComponent<FaceCamera>();
            var k = root.AddComponent<RiftKeeper>();
            k.gateAt = new GameObject("Seuil").transform; k.gateAt.SetParent(root.transform, false); k.gateAt.localPosition = new Vector3(0, 0, 4.2f);
            Rift.ReturnPoint = GameBootstrap.Ground(k.gateAt.position + root.transform.forward * 2.5f);
        }

        /// <summary>Ouvre le portail violet devant la statue (un seul à la fois).</summary>
        void OpenGate(int tier)
        {
            if (gate) Destroy(gate.gameObject);
            gate = new GameObject("Portail de brèche").AddComponent<Portal>(); gate.transform.position = GameBootstrap.Ground(gateAt.position);
            gate.gate = true; gate.glowColor = TownPortal.Violet; gate.displayName = tier <= 0 ? "Brèche astrale" : $"Grande Brèche {tier}"; gate.label = gate.displayName; gate.targetScene = "Korvalune_Breche";
            gate.onEnter = () => Rift.Enter(tier);
            Vfx.Shock(gate.transform.position, TownPortal.Violet, 4f); Sound.Play("sfx_portail", gate.transform.position, 1f);
            FindFirstObjectByType<GameBootstrap>()?.Hud?.Notify("<color=#c58cff>Un portail s'ouvre devant la statue</color>");
        }

        void Update()
        {
            if (!hero) { var p = FindFirstObjectByType<PlayerController>(); if (!p) return; hero = p.transform; }
            var cam = Camera.main; var tag = transform.Find("Nom"); if (tag && cam) tag.rotation = Quaternion.LookRotation(tag.position - cam.transform.position);
            if (!GetComponent<Usable>()) Usable.Add(gameObject, "Statue des brèches", 6f, () => { if (!panel) Open(); });
            float d = Vector3.Distance(hero.position, transform.position);
            if (d < 4.4f && !shown && !panel) { shown = true; Open(); }
            if (d > 6.5f) shown = false;
        }

        void Open()
        {
            var w = FindFirstObjectByType<PlayerController>().wallet;
            pick = Mathf.Clamp(pick, 1, w.riftBest + 1);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Brèches", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = panel.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 250;
            var sc = panel.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
            var root = (RectTransform)panel.transform;
            Image I(RectTransform parent, Sprite s, Vector2 pos, Vector2 size, Color c) { var im = new GameObject("I", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); im.rectTransform.SetParent(parent, false); im.rectTransform.anchoredPosition = pos; im.rectTransform.sizeDelta = size; im.sprite = s; im.type = Image.Type.Sliced; im.color = c; return im; }
            Text L(RectTransform parent, string s, Vector2 pos, Vector2 size, int fs, Color c) { var t = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.rectTransform.SetParent(parent, false); t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = size; t.font = font; t.fontSize = fs; t.alignment = TextAnchor.MiddleCenter; t.color = c; t.text = s; t.raycastTarget = false; t.gameObject.AddComponent<Outline>().effectColor = Color.black; return t; }
            Button B(RectTransform parent, string s, Vector2 pos, Vector2 size, System.Action a) { var im = I(parent, UIStyle.Slot(), pos, size, new Color(0.7f, 0.55f, 0.85f)); im.raycastTarget = true; L(im.rectTransform, s, Vector2.zero, size, 30, Color.white); var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => { Sound.UI("sfx_ui_clic", 0.6f); a(); }); return b; }
            var shade = I(root, null, Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0.6f)); shade.raycastTarget = true;
            var p = I(root, UIStyle.Ornate(), Vector2.zero, new Vector2(1100, 760), Color.white).rectTransform;
            L(p, "Statue des Brèches", new Vector2(0, 310), new Vector2(900, 70), 44, UIStyle.Gold).fontStyle = FontStyle.Bold;
            L(p, "« Les failles s'ouvrent sur des terres que la lune a oubliées. Remplis la jauge, abats le Gardien. »\nBrèche astrale : mourir ne fait pas quitter la brèche.  Grande Brèche : chaque mort coûte du temps.", new Vector2(0, 230), new Vector2(980, 90), 23, UIStyle.Parch);
            if (Rift.Suspended)
            {
                B(p, "Reprendre la brèche", new Vector2(-190, 140), new Vector2(480, 84), () => { Close(); Rift.ResumeOpen(); });
                B(p, "Fermer la brèche", new Vector2(190, 140), new Vector2(420, 84), () => { Rift.Forget(); Close(); Open(); });
            }
            else B(p, "Ouvrir une Brèche astrale", new Vector2(0, 140), new Vector2(620, 84), () => { Close(); OpenGate(0); });
            L(p, $"Sceaux de brèche : {w.riftKeys}    —    meilleur niveau : {w.riftBest}", new Vector2(0, 50), new Vector2(940, 50), 26, UIStyle.Parch);
            var lvl = L(p, "", new Vector2(0, -40), new Vector2(500, 70), 40, Color.white);
            System.Action refresh = () => lvl.text = $"Grande Brèche : niveau {pick}";
            B(p, "−", new Vector2(-330, -40), new Vector2(110, 84), () => { pick = Mathf.Max(1, pick - 1); refresh(); });
            B(p, "+", new Vector2(330, -40), new Vector2(110, 84), () => { pick = Mathf.Min(w.riftBest + 1, pick + 1); refresh(); });
            refresh();
            B(p, "Ouvrir la Grande Brèche (1 sceau)", new Vector2(0, -140), new Vector2(620, 84), () =>
            {
                if (w.riftKeys <= 0) { L(p, "Il te faut un Sceau de brèche (Gardien d'une brèche astrale)", new Vector2(0, -210), new Vector2(940, 40), 22, new Color(1f, 0.5f, 0.45f)); return; }
                w.riftKeys--; Close(); OpenGate(pick);
            });
            B(p, "Plus tard", new Vector2(0, -290), new Vector2(300, 74), Close);
            Sound.UI("sfx_ui_ouvrir", 0.7f);
        }

        void Close() { if (panel) Destroy(panel); panel = null; }
    }

    /// <summary>Statue : la pose de repos est jouée une image puis figée (la pierre ne bouge plus).</summary>
    public class StatuePose : MonoBehaviour
    {
        int frames;
        void LateUpdate()
        {
            if (++frames < 3) return;
            foreach (var a in GetComponentsInChildren<Animator>()) a.enabled = false;
            enabled = false;
        }
    }

    /// <summary>Tourne l'objet (nom flottant) vers la caméra à chaque image.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate() { var c = Camera.main; if (c) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position); }
    }
}
