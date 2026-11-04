using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Évènements de jeu qui font avancer les quêtes secondaires et les tutoriels (« casser », « coffre »,
    /// « potion », « sac »…). Appelé depuis les systèmes concernés ; sans effet si aucune quête n'écoute.</summary>
    public static class QuestEvents
    {
        public static event System.Action<string, int> Raised;
        public static void Raise(string id, int n = 1) { if (Application.isPlaying) Raised?.Invoke(id, n); }
    }

    /// <summary>Journal du héros : état de toutes les quêtes (Story) dans sa sauvegarde, avancement des quêtes
    /// secondaires et tutoriels, récompenses (expérience proportionnelle au niveau, or), annonces des nouvelles quêtes.
    /// Les chapitres principaux avancent dans leur région (composant Quest de la scène), qui rend compte ici.</summary>
    public class QuestLog : MonoBehaviour
    {
        public static QuestLog Instance { get; private set; }
        public static System.Action<string> Notify;
        public static System.Action<Story.Def> Completed;
        public PlayerController hero;
        Vector3 lastPos; float walked;

        public static CharacterSave Save => SaveSystem.Current;
        public static QuestSave State(string id)
        {
            var s = Save; if (s == null) return null;
            if (s.quests == null) s.quests = new List<QuestSave>();
            foreach (var q in s.quests) if (q.id == id) return q;
            var n = new QuestSave { id = id }; s.quests.Add(n); return n;
        }
        public static bool Done(string id) { if (string.IsNullOrEmpty(id)) return true; var s = Save; if (s?.quests == null) return false; foreach (var q in s.quests) if (q.id == id) return q.completed; return false; }
        public static bool Available(Story.Def d) => Done(d.requires);
        public static bool Active(Story.Def d) => Available(d) && !Done(d.id);
        public static bool Seen(string key) => Save?.seen != null && Save.seen.Contains(key);
        public static void MarkSeen(string key) { var s = Save; if (s == null) return; if (s.seen == null) s.seen = new List<string>(); if (!s.seen.Contains(key)) s.seen.Add(key); }

        /// <summary>Quête suivie (affichée sous le portrait) : celle choisie dans le journal, sinon le chapitre en cours.</summary>
        public static Story.Def Tracked
        {
            get
            {
                var t = Story.Get(Save?.tracked); if (t != null && Active(t)) return t;
                foreach (var d in Story.All) if (d.kind == Story.Kind.Principale && Active(d)) return d;
                return null;
            }
            set { if (Save != null) Save.tracked = value?.id ?? ""; }
        }
        /// <summary>Quête choisie dans le journal (null si aucune, ou si elle est terminée).</summary>
        public static Story.Def TrackedExplicit { get { var t = Story.Get(Save?.tracked); return t != null && Active(t) ? t : null; } }
        public static Story.Def CurrentChapter { get { foreach (var d in Story.All) if (d.kind == Story.Kind.Principale && !Done(d.id)) return d; return null; } }

        void OnEnable()
        {
            Instance = this;
            QuestEvents.Raised += OnEvent; Health.AnyDied += OnDied; LootDrop.Picked += OnPicked;
        }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            QuestEvents.Raised -= OnEvent; Health.AnyDied -= OnDied; LootDrop.Picked -= OnPicked;
        }

        void Start()
        {
            if (hero) lastPos = hero.transform.position;
            // première partie : on annonce le premier tutoriel
            if (!Seen("journal_intro")) { MarkSeen("journal_intro"); Notify?.Invoke("<color=#ffd24a>Journal de quêtes :</color> touchez le parchemin en haut de l'écran."); }
        }

        void Update()
        {
            if (!hero || Cinematic.Active) return;
            var p = hero.transform.position; var d = p - lastPos; d.y = 0; lastPos = p;
            if (d.magnitude < 3f) { walked += d.magnitude; if (walked >= 1f) { int n = Mathf.FloorToInt(walked); walked -= n; QuestEvents.Raise("marcher", n); } }
        }

        void OnPicked(ItemSave it) => QuestEvents.Raise("butin");

        void OnDied(Health h)
        {
            if (h.faction != Faction.Enemy) return;
            var ai = h.GetComponent<EnemyAI>(); if (!ai || ai.type == null) return;
            Progress(o => o.enemyId == "*" || o.enemyId == ai.type.id, 1);
        }

        void OnEvent(string evt, int n) => Progress(o => o.evt == evt, n);

        void Progress(System.Func<Story.Obj, bool> match, int n)
        {
            if (Save == null) return;
            string scene = SceneManager.GetActiveScene().name;
            foreach (var d in Story.All)
            {
                if (d.kind == Story.Kind.Principale || !Active(d)) continue;
                var st = State(d.id); if (st.step >= d.objs.Count) continue;
                var o = d.objs[st.step];
                if (!match(o) || (o.scene != null && o.scene != scene)) continue;
                st.done = Mathf.Min(o.count, st.done + n);
                if (st.done >= o.count)
                {
                    st.step++; st.done = 0;
                    if (st.step >= d.objs.Count) Complete(d);
                    else { Notify?.Invoke($"Objectif accompli : {o.text}"); Notify?.Invoke($"{d.title} : {d.objs[st.step].text}"); }
                }
            }
        }

        /// <summary>Récompense et annonces de fin de quête (secondaire, tutoriel ou chapitre).</summary>
        public static void Complete(Story.Def d)
        {
            var st = State(d.id); if (st == null || st.completed) return;
            st.completed = true; st.step = d.objs.Count;
            var hero = Instance ? Instance.hero : Object.FindFirstObjectByType<PlayerController>();
            if (hero)
            {
                float xp = Mathf.Round(PlayerController.XpForLevel(hero.level) * d.xpShare * Difficulty.Quest);
                long gold = (long)((d.gold + hero.level * 8 * (d.kind == Story.Kind.Principale ? 4 : 1)) * Difficulty.Quest);
                hero.GainXp(xp); hero.AddGold(gold);
                string label = d.kind == Story.Kind.Principale ? $"Chapitre {Story.ChapterNumber(d)} terminé" : d.kind == Story.Kind.Tutoriel ? "Tutoriel terminé" : "Quête terminée";
                Notify?.Invoke($"<color=#ffd24a>{label} : {d.title}  —  +{xp:N0} XP, +{gold:N0} or</color>");
            }
            Sound.UI("sfx_quete", 1f);
            if (Save != null && Save.tracked == d.id) Save.tracked = "";
            // quêtes débloquées par celle-ci
            foreach (var n in Story.All)
                if (n.requires == d.id && !Done(n.id))
                    Notify?.Invoke(n.kind == Story.Kind.Principale ? $"<color=#ffd24a>Nouveau chapitre : {n.title}</color> ({RegionName(n.scene)}, niveau {n.level} conseillé)"
                                                                 : $"Nouvelle quête{(n.kind == Story.Kind.Tutoriel ? " (tutoriel)" : "")} : {n.title}");
            Completed?.Invoke(d);
            GameBootstrap.Instance?.Persist();
        }

        public static string RegionName(string scene)
        {
            foreach (var r in Waypoint.Regions) if (r.scene == scene) return r.name;
            return scene == "Korvalune_Crypte" ? "Crypte, sous le vieux cimetière" : scene;
        }

        /// <summary>Texte de l'objectif en cours d'une quête (avec compteur).</summary>
        public static string Objective(Story.Def d)
        {
            if (d.kind == Story.Kind.Principale)
            {
                var q = Quest.ForScene(d.scene);
                if (q != null) return q.Finished ? "Chapitre terminé" : q.CurrentText();
                var st = State(d.id);
                return Done(d.id) ? "Chapitre terminé" : $"Se rendre : {RegionName(d.scene)}" + (st != null && st.step > 0 ? $" (étape {st.step + 1})" : "");
            }
            var s = State(d.id); if (s == null) return "";
            if (s.completed || s.step >= d.objs.Count) return "Terminée";
            var o = d.objs[s.step];
            return o.count > 1 ? $"{o.text} ({s.done}/{o.count})" : o.text;
        }
    }
}
