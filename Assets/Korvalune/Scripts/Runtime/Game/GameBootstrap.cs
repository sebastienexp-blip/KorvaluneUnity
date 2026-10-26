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
        public PlayerController Player { get; private set; }
        public HUD Hud { get; private set; }
        public Camera Cam { get; private set; }
        public bool Started { get; private set; }

        void Start() { Begin(); }

        public void Begin()
        {
            if (Started) return; Started = true;
            FX.additive = config ? config.fxAdditive : null; FX.alpha = config ? config.fxAlpha : null; Vfx.lib = config ? config.vfx : null;
            if (Application.isPlaying) GraphicsQuality.Apply(config, GraphicsQuality.Detect());
            Cam = Camera.main;
            if (!Cam) { Cam = new GameObject("Caméra").AddComponent<Camera>(); Cam.tag = "MainCamera"; }
            Cam.fieldOfView = 45; Cam.farClipPlane = 900;
            var rig = Cam.GetComponent<CameraRig>(); if (!rig) rig = Cam.gameObject.AddComponent<CameraRig>();

            // ---- héros
            var prefab = chosenHero ? chosenHero : config.defaultHero;
            var hero = prefab ? Instantiate(prefab, Ground(playerSpawn), Quaternion.identity) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = prefab ? prefab.name : "Héros";
            foreach (var c in hero.GetComponents<Collider>()) Kill(c);
            var cc = hero.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.92f, 0); cc.slopeLimit = 50; cc.stepOffset = 0.4f;
            var h = hero.AddComponent<Health>(); h.faction = Faction.Player; h.maxHp = 220; h.maxMana = 100; h.manaRegen = 6; h.hpRegen = 1.5f; h.armor = 2; h.displayName = hero.name;
            h.hp = h.maxHp; h.mana = h.maxMana;
            Player = hero.AddComponent<PlayerController>(); Player.cam = Cam.transform; Player.Init();
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
                if (config) SaveSystem.ApplyLook(hero, config.catalog, save.look, save.female);
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
            h.modifyIncoming = a => Time.time < Runner.shieldUntil ? a * (1f - Runner.shieldPower) : a;

            // ---- interface
            var ui = new GameObject("Interface"); Hud = ui.AddComponent<HUD>(); Hud.player = Player; Hud.runner = Runner; Hud.worldCam = Cam;
            var q = ui.AddComponent<Quest>(); q.player = hero.transform;
            q.steps.Add(new Quest.Objective { text = "Éliminer les squelettes du vieux cimetière", enemyId = "squelette", count = 5 });
            q.steps.Add(new Quest.Objective { text = "Chasser les loups de la lisière", enemyId = "loup", count = 3 });
            q.steps.Add(new Quest.Objective { text = "Vaincre le sorcier corrompu du pont", enemyId = "sorcier", count = 1 });
            q.steps.Add(new Quest.Objective { text = "Rejoindre la porte du Château de Korvalune", place = castleGate, radius = 8f });
            Hud.quest = q;
            Hud.Build(Cam);
            Player.joystick = Hud.Joystick;
            Hud.Notify("Vallée de Korvalune");

            // ---- ennemis
            foreach (var camp in camps) SpawnCamp(camp);
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

        public GameObject Spawn(GameConfig.EnemyType t, Vector3 pos)
        {
            var e = Instantiate(t.prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            e.name = t.label;
            e.transform.localScale *= t.scale;
            float height = 1.8f * t.scale, radius = 0.4f * t.scale;
            var cap = e.GetComponent<CapsuleCollider>();
            if (cap) { height = cap.height * t.scale; radius = Mathf.Min(cap.radius * t.scale, height * 0.45f); }
            foreach (var c in e.GetComponents<Collider>()) Kill(c);
            var cc = e.AddComponent<CharacterController>(); cc.height = height / e.transform.localScale.y; cc.radius = radius / e.transform.localScale.y; cc.center = new Vector3(0, cc.height / 2 + 0.02f, 0);
            var h = e.AddComponent<Health>(); h.faction = Faction.Enemy; h.maxHp = t.hp; h.hp = t.hp; h.armor = t.armor; h.displayName = t.label;
            var ai = e.AddComponent<EnemyAI>(); ai.type = t;
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
            s.inventory = new List<ItemSave>(Player.inventory); s.playSeconds += 30f; s.lastPlayed = System.DateTime.UtcNow.ToString("o");
            SaveSystem.Save();
        }

        public void RespawnPlayer()
        {
            if (!Player) return;
            var cc = Player.GetComponent<CharacterController>(); cc.enabled = false;
            Player.transform.position = Ground(playerSpawn); cc.enabled = true;
            Player.Health.Revive();
            Cam.GetComponent<CameraRig>()?.Snap();
        }

        static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y + 0.05f;
            if (Physics.Raycast(p + Vector3.up * 50, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) p.y = Mathf.Max(p.y, hit.point.y + 0.05f);
            return p;
        }
    }
}
