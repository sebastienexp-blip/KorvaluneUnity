using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Lance la tranche jouable : héros (choisi dans le créateur ou par défaut), caméra, interface, camps
    /// d'ennemis, quête « Purifier la vallée ». Les points de la scène sont posés par WorldBuilder.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [System.Serializable] public class Camp { public string enemyId; public Vector3 center; public int count = 3; public float radius = 5f; }
        public GameConfig config;
        public Vector3 playerSpawn;
        public Vector3 castleGate;
        public List<Camp> camps = new List<Camp>();
        public static GameObject chosenHero;            // posé par le créateur de personnage avant de charger la scène
        public static Vector3? nextSpawn;               // posé par un portail avant de charger la scène suivante
        // zone et quête propres à la scène (vallée par défaut)
        public string zoneName = "Vallée de Korvalune";
        [Tooltip("Lumière portée par le héros (donjons) : halo chaud qui éclaire le sol et les ennemis autour de lui, 0 = aucune")]
        public float heroLight = 0f;
        public string questTitle = "Purifier la vallée";
        [Header("Monde ouvert")]
        [Tooltip("Rayon (m) de la zone sûre autour de safeCenter : aucun monstre n'y apparaît ni n'y entre (0 = aucune)")]
        public float safeRadius = 0f;
        public Vector3 safeCenter;
        [Tooltip("Niveau fixe des monstres de la zone (donjons) ; 0 = niveau selon la distance à la zone sûre")]
        public int zoneLevel = 0;
        [Tooltip("Mètres à parcourir depuis le bord de la zone sûre pour gagner un niveau de monstre")]
        public float metersPerLevel = 13f;
        [Tooltip("Hauteur de la surface de l'eau (nage) ; très bas = pas d'eau")]
        public float waterLevel = -10000f;
        [Tooltip("Peupler la zone de camps de monstres générés (vallée)")]
        public bool populate = false;
        [Tooltip("Lieux (hameaux) où aucun camp de monstres n'est généré")]
        public List<Vector3> noCamps = new List<Vector3>();
        public static GameBootstrap Instance { get; private set; }
        public static float WaterLevel => Instance ? Instance.waterLevel : -10000f;

        /// <summary>Le point est-il dans la zone sûre (la ville) ?</summary>
        public static bool InSafeZone(Vector3 p)
        {
            if (!Instance || Instance.safeRadius <= 0) return false;
            var d = p - Instance.safeCenter; d.y = 0; return d.magnitude < Instance.safeRadius;
        }

        /// <summary>Niveau des monstres à cet endroit : fixe dans un donjon ; dans la vallée, 1 aux portes de la ville puis
        /// un niveau tous les « metersPerLevel » mètres, et +3 dans les montagnes de l'ouest.</summary>
        public static int LevelAt(Vector3 p)
        {
            if (!Instance) return 1;
            if (Instance.zoneLevel > 0) return Instance.zoneLevel;
            var d = p - Instance.safeCenter; d.y = 0;
            float beyond = Mathf.Max(0, d.magnitude - Instance.safeRadius - 8f);
            int l = 1 + Mathf.FloorToInt(beyond / Mathf.Max(1f, Instance.metersPerLevel));
            if (p.x < -140f) l += 3;
            return Mathf.Clamp(l, 1, 80);
        }
        public List<Quest.Objective> questSteps = new List<Quest.Objective>();
        public PlayerController Player { get; private set; }
        public HUD Hud { get; private set; }
        public Camera Cam { get; private set; }
        public bool Started { get; private set; }

        void Start() { Begin(); }

        public void Begin()
        {
            if (Started) return; Started = true; Instance = this;
            FX.additive = config ? config.fxAdditive : null; FX.alpha = config ? config.fxAlpha : null; Vfx.lib = config ? config.vfx : null;
            if (Application.isPlaying) GraphicsQuality.Apply(config, GraphicsQuality.Detect());
            if (Application.isPlaying) EnsureGroundCollision();
            Cam = Camera.main;
            if (!Cam) { Cam = new GameObject("Caméra").AddComponent<Camera>(); Cam.tag = "MainCamera"; }
            Cam.fieldOfView = 45; Cam.farClipPlane = 900;
            var rig = Cam.GetComponent<CameraRig>(); if (!rig) rig = Cam.gameObject.AddComponent<CameraRig>();

            // ---- héros
            var prefab = chosenHero ? chosenHero : config.defaultHero;
            if (Application.isPlaying && nextSpawn.HasValue) { playerSpawn = nextSpawn.Value; nextSpawn = null; }
            var spawnAt = Ground(playerSpawn) + Vector3.up * 0.3f;          // légèrement au-dessus du sol : jamais encastré dans le relief
            var hero = prefab ? Instantiate(prefab, spawnAt, Quaternion.identity) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = prefab ? prefab.name : "Héros";
            foreach (var c in hero.GetComponents<Collider>()) Kill(c);
            var cc = hero.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.92f, 0); cc.slopeLimit = 50; cc.stepOffset = 0.4f;
            var h = hero.AddComponent<Health>(); h.faction = Faction.Player; h.maxHp = 220; h.maxMana = 100; h.manaRegen = 6; h.hpRegen = 1.5f; h.armor = 2; h.displayName = hero.name;
            h.hp = h.maxHp; h.mana = h.maxMana;
            Player = hero.AddComponent<PlayerController>(); Player.cam = Cam.transform; Player.Init();
            if (heroLight > 0f)
            {   // halo du héros : lumière chaude au-dessus de lui (sans ombre portée, peu coûteuse sur mobile)
                var hl = new GameObject("Halo du héros").AddComponent<Light>(); hl.transform.SetParent(hero.transform, false);
                hl.transform.localPosition = new Vector3(0, 2.6f, -0.4f); hl.type = LightType.Point; hl.color = new Color(1f, 0.86f, 0.70f);
                hl.intensity = heroLight; hl.range = 11f; hl.shadows = LightShadows.None; hl.renderMode = LightRenderMode.ForcePixel;
            }
            rig.target = hero.transform; rig.Snap();
            LootDrop.beamMat = config ? config.fxAdditive : null;
            // personnage sauvegardé (choisi dans l'écran de sélection)
            var save = SaveSystem.Current;
            if (save != null)
            {
                hero.name = save.name;
                Player.level = Mathf.Clamp(save.level, 1, PlayerController.MaxLevel); Player.xp = save.xp; Player.skillPoints = save.skillPoints;
                Player.xpNext = PlayerController.XpForLevel(Player.level);
                h.maxHp += 15 * (Player.level - 1); h.maxMana += 8 * (Player.level - 1); Player.attackDamage += 3 * (Player.level - 1); h.hp = h.maxHp; h.mana = h.maxMana;
                Player.inventory.AddRange(save.inventory);
                if (save.wallet != null) Player.wallet = save.wallet;
                if (config) SaveSystem.ApplyLook(hero, config.catalog, save.look, save.female);
                StripChildColliders(hero);
            }
            // compétences propres à la classe (32 par classe, rangs et barre de 5 emplacements sauvegardés)
            Runner = hero.AddComponent<SkillRunner>();
            string cls = save != null && !string.IsNullOrEmpty(save.classId) ? save.classId : ClassOf(hero.name, prefab);
            Runner.Init(cls, config);
            if (save != null)
            {
                if (save.skillRanks != null && save.skillRanks.Count == Runner.book.Count) Runner.ranks = save.skillRanks.ToArray();
                if (save.loadout != null && save.loadout.Count == Runner.loadout.Length) Runner.loadout = save.loadout.ToArray();
            }
            // équipement : caractéristiques de base (niveau) + objets portés
            var eq = hero.AddComponent<Equipment>(); eq.Init();
            if (save != null) eq.FromSave(save.equipped);
            hero.AddComponent<HeroOutfit>().Init();
            StripChildColliders(hero);     // haillons au départ, la tenue apparaît à mesure qu'on s'équipe
            h.hp = h.maxHp;
            h.modifyIncoming = dmgIn => Time.time < Runner.shieldUntil ? dmgIn * (1f - Runner.shieldPower) : dmgIn;

            // ---- interface
            var ui = new GameObject("Interface"); Hud = ui.AddComponent<HUD>(); Hud.player = Player; Hud.runner = Runner; Hud.worldCam = Cam;
            var q = ui.AddComponent<Quest>(); q.player = hero.transform; q.title = questTitle; Hud.zoneName = zoneName;
            if (questSteps.Count > 0) foreach (var o in questSteps) q.steps.Add(new Quest.Objective { text = o.text, enemyId = o.enemyId, count = o.count, place = o.place, radius = o.radius });
            else
            {
                q.steps.Add(new Quest.Objective { text = "Éliminer les squelettes du vieux cimetière", enemyId = "squelette", count = 5 });
                q.steps.Add(new Quest.Objective { text = "Chasser les loups de la lisière", enemyId = "loup", count = 3 });
                q.steps.Add(new Quest.Objective { text = "Vaincre le sorcier corrompu du pont", enemyId = "sorcier", count = 1 });
                q.steps.Add(new Quest.Objective { text = "Rejoindre la porte du Château de Korvalune", place = castleGate, radius = 8f });
            }
            Hud.quest = q;
            Hud.Build(Cam);
            Player.joystick = Hud.Joystick;
            Hud.Notify(zoneName);

            // ---- ennemis : camps vivants (apparition à l'approche, réapparition après nettoyage) ; la ville reste sûre
            if (Application.isPlaying) { var pop = gameObject.AddComponent<WorldPopulation>(); pop.Init(this, camps, populate); }
            else foreach (var camp in camps) SpawnCamp(camp);
            if (Application.isPlaying)
            {
                var t = Terrain.activeTerrain;
                Debug.Log($"[Korvalune] zone « {zoneName} » : héros {hero.name} en {hero.transform.position}, terrain {(t ? t.name : "aucun")}, " +
                          $"collision du terrain {(t && t.GetComponent<TerrainCollider>() ? (t.GetComponent<TerrainCollider>().enabled ? "active" : "désactivée") : "absente")}, " +
                          $"qualité {GraphicsQuality.Current}, {SystemInfo.graphicsDeviceType}, {SystemInfo.deviceModel}");
                if (AutoTest.Enabled) hero.AddComponent<AutoTest>();
            }
        }

        public void SpawnCamp(Camp camp)
        {
            var t = config ? config.Enemy(camp.enemyId) : null;
            if (t == null || !t.prefab) return;
            for (int i = 0; i < camp.count; i++)
            {
                var a = 2 * Mathf.PI * i / Mathf.Max(1, camp.count) + Random.Range(-0.3f, 0.3f);
                var p = camp.center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * camp.radius * Random.Range(0.4f, 1f);
                Spawn(t, Ground(p));
            }
        }

        public SkillRunner Runner { get; private set; }

        /// <summary>Classe d'un héros d'après le nom de son prefab (KaelDraven_M → KaelDraven).</summary>
        public static string ClassOf(string heroName, GameObject prefab)
        {
            var n = prefab ? prefab.name : heroName;
            foreach (var c in new[] { "KaelDraven", "ElyraNoctis", "Varek", "Morvanna", "Tharion" }) if (n.StartsWith(c)) return c;
            return "KaelDraven";
        }

        /// <summary>Courbes de progression des monstres : vie +30 %, dégâts +20 %, expérience +35 % par niveau.</summary>
        public static float HpAt(int l) => 1f + 0.30f * (l - 1);
        public static float DmgAt(int l) => 1f + 0.20f * (l - 1);
        public static float XpAt(int l) => 1f + 0.35f * (l - 1);

        public GameObject Spawn(GameConfig.EnemyType t, Vector3 pos, int level = 0)
        {
            if (level <= 0) level = LevelAt(pos);
            var e = Instantiate(t.prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            e.name = t.label;
            e.transform.localScale *= t.scale;
            float height = 1.8f * t.scale, radius = 0.4f * t.scale;
            var cap = e.GetComponent<CapsuleCollider>();
            if (cap) { height = cap.height * t.scale; radius = Mathf.Min(cap.radius * t.scale, height * 0.45f); }
            foreach (var c in e.GetComponents<Collider>()) Kill(c);
            var cc = e.AddComponent<CharacterController>(); cc.height = height / e.transform.localScale.y; cc.radius = radius / e.transform.localScale.y; cc.center = new Vector3(0, cc.height / 2 + 0.02f, 0);
            int bl = Mathf.Max(1, t.level);
            float hpM = Mathf.Max(0.55f, HpAt(level) / HpAt(bl)), dmgM = Mathf.Max(0.6f, DmgAt(level) / DmgAt(bl)), xpM = Mathf.Max(0.5f, XpAt(level) / XpAt(bl));
            hpM *= Difficulty.Hp; dmgM *= Difficulty.Damage; xpM *= Difficulty.Xp;             // difficulté choisie (Cauchemar, Enfer, Tourments)
            var h = e.AddComponent<Health>(); h.faction = Faction.Enemy; h.maxHp = Mathf.Round(t.hp * hpM); h.hp = h.maxHp; h.armor = t.armor * Mathf.Min(2f, dmgM); h.displayName = t.label; h.level = level;
            var ai = e.AddComponent<EnemyAI>(); ai.type = t; ai.damage = t.damage * dmgM; ai.xp = Mathf.Round(t.xp * xpM);
            return e;
        }

        float nextSave;
        void Update() { if (Application.isPlaying && Time.time > nextSave) { nextSave = Time.time + 30f; Persist(); } }
        void OnApplicationPause(bool paused) { if (paused) Persist(); }
        void OnApplicationQuit() { Persist(); }

        /// <summary>Écrit l'état du héros (niveau, expérience, inventaire) dans la sauvegarde du compte.</summary>
        public void Persist()
        {
            var s = SaveSystem.Current; if (s == null || !Player) return;
            s.level = Player.level; s.xp = Player.xp; s.skillPoints = Player.skillPoints;
            if (Runner) { s.skillRanks = new List<int>(Runner.ranks); s.loadout = new List<int>(Runner.loadout); }
            var eqp = Player.GetComponent<Equipment>(); if (eqp) s.equipped = eqp.ToSave();
            s.wallet = Player.wallet; s.gold = Player.wallet.gold;
            s.inventory = new List<ItemSave>(Player.inventory); s.playSeconds += 30f; s.lastPlayed = System.DateTime.UtcNow.ToString("o");
            SaveSystem.Save();
        }

        /// <summary>Après la mort : retour en ville. Depuis un donjon, on recharge la vallée (apparition en ville).</summary>
        public void RespawnPlayer()
        {
            if (!Player) return;
            if (safeRadius <= 0 && Application.isPlaying && Application.CanStreamedLevelBeLoaded("Korvalune_Vallee"))
            {
                Player.Health.Revive(); Persist(); nextSpawn = null;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Korvalune_Vallee"); return;
            }
            var cc = Player.GetComponent<CharacterController>(); cc.enabled = false;
            Player.transform.position = Ground(playerSpawn); cc.enabled = true;
            Player.Health.Revive();
            Cam.GetComponent<CameraRig>()?.Snap();
        }

        /// <summary>Seule la collision du personnage (CharacterController) doit rester sur le héros : toute autre collision
        /// rattachée (coiffure, pièce d'armure, arme) la chevaucherait et le pousserait à travers le sol.</summary>
        public static void StripChildColliders(GameObject hero)
        {
            foreach (var c in hero.GetComponentsInChildren<Collider>(true)) if (!(c is CharacterController)) { c.enabled = false; Kill(c); }
            foreach (var rb in hero.GetComponentsInChildren<Rigidbody>(true)) Kill(rb);
        }

        /// <summary>Garantit une collision au sol : si le collisionneur du terrain manque ou n'a pas de données, on construit un
        /// maillage de collision équivalent à partir du relief.</summary>
        static void EnsureGroundCollision()
        {
            var t = Terrain.activeTerrain; if (!t || !t.terrainData) return;
            var tc = t.GetComponent<TerrainCollider>();
            if (tc && tc.enabled && tc.terrainData) return;
            Debug.LogWarning("[Korvalune] collision du terrain absente : maillage de secours");
            var td = t.terrainData; const int N = 257;
            var verts = new Vector3[N * N]; var tris = new int[(N - 1) * (N - 1) * 6];
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                float u = i / (float)(N - 1), v = j / (float)(N - 1);
                verts[j * N + i] = new Vector3(u * td.size.x, td.GetInterpolatedHeight(u, v), v * td.size.z);
            }
            int k = 0;
            for (int j = 0; j < N - 1; j++) for (int i = 0; i < N - 1; i++)
            {
                int a = j * N + i;
                tris[k++] = a; tris[k++] = a + N; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + N; tris[k++] = a + N + 1;
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts, triangles = tris };
            var go = new GameObject("Sol de secours"); go.transform.position = t.transform.position;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y + 0.05f;
            // sondage court au-dessus du point : sous un toit, une voûte ou la masse rocheuse d'une grotte, on reste au sol
            if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) p.y = Mathf.Max(t ? p.y : -1e6f, hit.point.y + 0.05f);
            return p;
        }
    }
}
