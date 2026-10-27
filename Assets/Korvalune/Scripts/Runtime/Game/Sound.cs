using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Sons du jeu (effets générés, réalistes) : une banque chargée depuis Resources/Audio, des voix réutilisées
    /// (pas de création en combat), légère variation de hauteur et de volume pour éviter la répétition, sons spatialisés
    /// autour du héros, limite des sons identiques simultanés. Volumes « Musique » et « Effets » réglables dans les options.</summary>
    public static class Sound
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static Transform root;

        public static float Effects { get => PlayerPrefs.GetFloat("korvalune.effets", 1f); set => PlayerPrefs.SetFloat("korvalune.effets", value); }
        public static float Music { get => PlayerPrefs.GetFloat("korvalune.musique", 0.7f); set { PlayerPrefs.SetFloat("korvalune.musique", value); MusicDirector.Instance?.ApplyVolume(); } }

        public static AudioClip Clip(string id)
        {
            if (string.IsNullOrEmpty(id) || missing.Contains(id)) return null;
            if (clips.TryGetValue(id, out var c) && c) return c;
            c = Resources.Load<AudioClip>("Audio/" + id);
            if (!c) { missing.Add(id); return null; }
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clips[id] = c;
        }

        static AudioSource Voice()
        {
            if (!root) { var go = new GameObject("Sons"); Object.DontDestroyOnLoad(go); root = go.transform; pool.Clear(); }
            foreach (var s in pool) if (s && !s.isPlaying) return s;
            if (pool.Count >= 28) { AudioSource oldest = pool[0]; foreach (var s in pool) if (s && s.time > oldest.time) oldest = s; return oldest; }
            var v = new GameObject("Voix").AddComponent<AudioSource>(); v.transform.SetParent(root, false);
            v.playOnAwake = false; v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = 6f; v.maxDistance = 55f; v.dopplerLevel = 0;
            pool.Add(v); return v;
        }

        /// <summary>Joue un effet. pos : son placé dans le monde (sinon interface, non spatialisé).</summary>
        public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitchVariation = 0.07f, float minGap = 0.05f)
        {
            if (!Application.isPlaying) return;
            var c = Clip(id); if (!c) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out var t) && now - t < minGap) return;         // pas dix fois le même son dans la même image
            lastPlayed[id] = now;
            var v = Voice(); v.Stop();
            v.clip = c; v.loop = false;
            v.volume = Mathf.Clamp01(volume * Effects * Random.Range(0.9f, 1f));
            v.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            if (pos.HasValue) { v.transform.position = pos.Value; v.spatialBlend = 0.75f; }
            else { v.spatialBlend = 0f; }
            v.Play();
        }

        public static void UI(string id, float volume = 0.8f) => Play(id, null, volume, 0.03f, 0.03f);

        // ------------------------------------------------------------------ correspondances du jeu
        /// <summary>Son lancé avec une compétence (selon son type et son élément).</summary>
        public static string CastOf(SkillDef d) => d.kind switch
        {
            SkillKind.Leap or SkillKind.Dash or SkillKind.Evade => "sfx_bond",
            SkillKind.Teleport => "sfx_teleport",
            SkillKind.Buff => d.element == Element.Sang || d.element == Element.Physique ? "sfx_cri_guerre" : ElementCast(d.element),
            SkillKind.Shield => "sfx_bouclier_magique",
            SkillKind.Heal => "sfx_soin",
            SkillKind.Summon => "sfx_invocation",
            SkillKind.Trap => "sfx_piege",
            SkillKind.Rain => "sfx_pluie",
            SkillKind.Curse => "sfx_malediction",
            SkillKind.Beam => "sfx_rayon",
            SkillKind.Chain => "sfx_foudre_chaine",
            SkillKind.Whirl => "sfx_tourbillon",
            SkillKind.Nova => "sfx_nova",
            SkillKind.Strike or SkillKind.Cleave or SkillKind.Combo => d.element == Element.Physique ? "sfx_epee_whoosh" : ElementCast(d.element),
            _ => ElementCast(d.element),
        };

        public static string ElementCast(Element e) => e switch
        {
            Element.Feu => "sfx_feu_lancer", Element.Glace => "sfx_glace_lancer", Element.Foudre => "sfx_foudre", Element.Neant => "sfx_neant_lancer",
            Element.Ombre => "sfx_ombre", Element.Sacre => "sfx_sacre", Element.Poison => "sfx_poison", Element.Sang => "sfx_sang", Element.Os => "sfx_os",
            Element.Cendre => "sfx_cendre", _ => "sfx_epee_whoosh"
        };

        /// <summary>Son d'explosion / d'impact d'un élément.</summary>
        public static string ImpactOf(Element e) => e switch
        {
            Element.Feu => "sfx_feu_explosion", Element.Cendre => "sfx_feu_explosion", Element.Glace => "sfx_glace_impact", Element.Foudre => "sfx_foudre",
            Element.Neant => "sfx_neant_explosion", Element.Ombre => "sfx_neant_explosion", Element.Sacre => "sfx_sacre", Element.Poison => "sfx_poison",
            Element.Os => "sfx_os", Element.Sang => "sfx_sang", _ => "sfx_sol_impact"
        };

        /// <summary>Arme de base de chaque classe : son du coup porté dans le vide et de l'impact.</summary>
        public static (string swing, string hit) WeaponOf(string cls) => cls switch
        {
            Powers.T => ("sfx_baton_whoosh", "sfx_marteau_impact"),
            Powers.V => ("sfx_arbalete_tir", "sfx_carreau_impact"),
            Powers.E => ("sfx_baton_whoosh", "sfx_ennemi_touche"),
            Powers.M => ("sfx_baton_whoosh", "sfx_ennemi_touche"),
            _ => ("sfx_epee_whoosh", "sfx_epee_coup"),
        };

        public static string DeathOf(string enemyId) => enemyId switch
        {
            "squelette" => "sfx_squelette_mort", "loup" => "sfx_loup_mort", "araignee" => "sfx_araignee", "demon" => "sfx_demon_rugit", _ => "sfx_cri_mort"
        };

        public static string DropOf(Rarity r) => r >= Rarity.Mythique ? "sfx_butin_ancestral" : r >= Rarity.Legendaire ? "sfx_butin_legendaire" : "sfx_butin_commun";
    }

    /// <summary>Directeur musical : une musique par lieu (menus, vallée, ville, donjon, grotte), musique de combat quand des
    /// ennemis attaquent, musique de boss face aux démons ; fondus enchaînés entre deux pistes, ambiance sonore en boucle.</summary>
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }
        AudioSource a, b, amb; bool aActive = true;
        string current = "", currentAmb = "";
        float fade = 1f, combatUntil, nextCheck;
        public static Vector3 TownCenter = new Vector3(0, 0, -45);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Musique"); DontDestroyOnLoad(go); Instance = go.AddComponent<MusicDirector>();
        }

        void Awake()
        {
            a = Src(true); b = Src(true); amb = Src(true);
            SceneManager.sceneLoaded += (s, m) => nextCheck = 0;
            Health.AnyDamaged += OnDamage;
        }
        void OnDestroy() { Health.AnyDamaged -= OnDamage; }

        AudioSource Src(bool loop) { var s = gameObject.AddComponent<AudioSource>(); s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; return s; }

        void OnDamage(Health h, float amount, bool crit)
        {
            // un coup échangé avec un monstre déclenche la musique de combat (et la prolonge)
            if (h && (h.faction == Faction.Player || (h.faction == Faction.Enemy && !h.Dead))) combatUntil = Time.time + 7f;
        }

        public void ApplyVolume() { fade = Mathf.Max(fade, 0.99f); }

        void Update()
        {
            if (Time.unscaledTime >= nextCheck) { nextCheck = Time.unscaledTime + 0.5f; Choose(); }
            // fondu enchaîné (2 s)
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 2f);
            float mv = Sound.Music;
            var on = aActive ? a : b; var off = aActive ? b : a;
            on.volume = mv * fade; off.volume = mv * (1 - fade); if (off.volume <= 0.001f && off.isPlaying) off.Stop();
            amb.volume = Mathf.MoveTowards(amb.volume, string.IsNullOrEmpty(currentAmb) ? 0 : 0.55f * Sound.Effects, Time.unscaledDeltaTime * 0.5f);
        }

        void Choose()
        {
            string scene = SceneManager.GetActiveScene().name;
            string track, ambience = "";
            var p = Object.FindFirstObjectByType<PlayerController>();
            bool fighting = p && !p.Health.Dead && Time.time < combatUntil && Health.Around(p.transform.position, 22f, Faction.Player).Count > 0;
            bool boss = false;
            if (fighting) foreach (var e in Health.Around(p.transform.position, 26f, Faction.Player)) { var ai = e.GetComponent<EnemyAI>(); if (ai && ai.type != null && ai.type.xp >= 250) boss = true; }
            if (scene.Contains("Crypte")) { track = "mus_donjon"; ambience = "amb_donjon"; }
            else if (scene.Contains("Terriers")) { track = "mus_donjon"; ambience = "amb_grotte"; }
            else if (scene.Contains("Vallee"))
            {
                bool town = p && Vector3.Distance(new Vector3(p.transform.position.x, 0, p.transform.position.z), TownCenter) < 70f;
                track = town ? "mus_ville" : "mus_vallee"; ambience = town ? "amb_ville" : "amb_vallee";
            }
            else track = "mus_menu";
            if (fighting) track = boss ? "mus_boss" : "mus_combat";
            SetTrack(track); SetAmbience(ambience);
        }

        public void SetTrack(string id)
        {
            if (id == current) return;
            var clip = Sound.Clip(id); if (!clip) return;
            current = id; aActive = !aActive; var on = aActive ? a : b;
            on.clip = clip; on.time = 0; on.volume = 0; on.Play(); fade = 0;
        }

        void SetAmbience(string id)
        {
            if (id == currentAmb) return;
            currentAmb = id;
            var clip = Sound.Clip(id);
            if (!clip) { amb.Stop(); return; }
            amb.clip = clip; amb.volume = 0; amb.Play();
        }
    }
}
