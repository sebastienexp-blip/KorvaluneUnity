using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Quête de la région (objectifs successifs : éliminer des ennemis d'un type, atteindre un lieu). Dans une
    /// région du mode histoire, c'est le chapitre principal : son avancement est gardé dans la sauvegarde, il s'ouvre sur
    /// une cinématique à la première arrivée et se ferme sur une autre, avec la récompense du chapitre (QuestLog).</summary>
    public class Quest : MonoBehaviour
    {
        [System.Serializable] public class Objective
        {
            public string text; public string enemyId; public int count; public int done;
            public Vector3 place; public float radius;
            public bool Complete => enemyId != null && enemyId.Length > 0 ? done >= count : done > 0;
        }
        public string title = "Purifier la vallée";
        public List<Objective> steps = new List<Objective>();
        public int current;
        public System.Action<string> Notify;
        public Transform player;
        public Story.Def chapter;                 // chapitre principal de cette région (null : brèche, donjon annexe)
        public bool Finished => current >= steps.Count;
        public Objective Current => Finished ? null : steps[current];
        /// <summary>Lieu de l'objectif en cours (pour la carte), s'il en a un.</summary>
        public Vector3? CurrentPlace { get { var c = Current; return c != null && (c.enemyId == null || c.enemyId.Length == 0) && c.place != Vector3.zero ? c.place : (Vector3?)null; } }

        static readonly Dictionary<string, Quest> byScene = new Dictionary<string, Quest>();
        public static Quest ForScene(string scene) => scene != null && byScene.TryGetValue(scene, out var q) && q ? q : null;

        void OnEnable() { Health.AnyDied += OnDied; }
        void OnDisable() { Health.AnyDied -= OnDied; }

        void Start()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (Rift.Active) return;
            chapter = Story.ChapterOf(scene);
            if (chapter == null) return;
            byScene[scene] = this;
            title = $"Chapitre {Story.ChapterNumber(chapter)} — {chapter.title}";
            var st = QuestLog.State(chapter.id);
            if (st != null)
            {
                if (st.completed) current = steps.Count;
                else { current = Mathf.Clamp(st.step, 0, steps.Count); if (!Finished) steps[current].done = st.done; }
            }
            // première arrivée dans la région : cinématique d'ouverture du chapitre
            if (Application.isPlaying && !Finished && !QuestLog.Seen("intro_" + chapter.id) && !AutoTest.Enabled)
                StartCoroutine(PlayIntro());
        }

        System.Collections.IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.8f);
            QuestLog.MarkSeen("intro_" + chapter.id);
            Cinematic.ChapterIntro(this);
        }

        void OnDied(Health h)
        {
            if (Finished || h.faction != Faction.Enemy) return;
            var ai = h.GetComponent<EnemyAI>();
            var o = Current;
            if (ai && ai.type != null && o.enemyId == ai.type.id) { o.done++; Save(); Advance(); }
        }

        void Update()
        {
            if (Finished || !player || Cinematic.Active) return;
            var o = Current;
            if (string.IsNullOrEmpty(o.enemyId) && o.radius > 0)
            {
                var d = player.position - o.place; d.y = 0;
                if (d.magnitude < o.radius) { o.done = 1; Advance(); }
            }
        }

        void Save()
        {
            if (chapter == null) return;
            var st = QuestLog.State(chapter.id); if (st == null || st.completed) return;
            st.step = current; st.done = Finished ? 0 : Current.done;
        }

        void Advance()
        {
            if (!Current.Complete) return;
            // chaque objectif rapporte de l'expérience (part du niveau en cours) et un peu d'or ; la fin de chapitre
            // (récompense du QuestLog) bien davantage
            var hero = player ? player.GetComponent<PlayerController>() : null;
            bool last = current == steps.Count - 1;
            if (hero && !(last && chapter != null))
            {
                float xp = (chapter != null ? Mathf.Round(PlayerController.XpForLevel(hero.level) * 0.10f) : (60f + 18f * hero.level) * (last ? 4f : 1f)) * Difficulty.Quest;
                hero.GainXp(Mathf.Round(xp)); long gold = (long)((20 + 6 * hero.level) * (last ? 5 : 1) * Difficulty.Quest); hero.AddGold(gold);
                Notify?.Invoke($"<color=#ffd24a>+{Mathf.Round(xp):N0} XP, +{gold} or</color>");
            }
            Notify?.Invoke("Objectif accompli : " + Current.text);
            Sound.UI("sfx_quete", 0.9f);
            current++;
            Save();
            if (Finished)
            {
                if (chapter != null)
                {
                    var ch = chapter;
                    Cinematic.ChapterOutro(this, () => QuestLog.Complete(ch));
                }
                else Notify?.Invoke($"Quête terminée : {title} !");
            }
            else { Notify?.Invoke("Nouvel objectif : " + Current.text); GameBootstrap.Instance?.Persist(); }
        }

        public string CurrentText()
        {
            var o = Current; if (o == null) return "Terminé";
            return o.text + (string.IsNullOrEmpty(o.enemyId) ? "" : $" ({o.done}/{o.count})");
        }

        public string Describe()
        {
            // quête suivie choisie dans le journal (secondaire, tutoriel ou chapitre d'une autre région)
            var tr = QuestLog.TrackedExplicit;
            if (tr != null && tr != chapter)
                return $"<b>{tr.title}</b>\n{QuestLog.Objective(tr)}" + (tr.kind == Story.Kind.Principale && tr.scene != SceneManager.GetActiveScene().name ? $"\n<size=18>{QuestLog.RegionName(tr.scene)}</size>" : "");
            if (Finished && chapter != null)
            {
                var next = QuestLog.CurrentChapter;
                return next != null && next != chapter ? $"<b>Chapitre {Story.ChapterNumber(next)} — {next.title}</b>\nSe rendre : {QuestLog.RegionName(next.scene)}\n<size=18>Pierre de voyage · niveau {next.level} conseillé</size>"
                                                       : $"<b>{title}</b>\nChapitre terminé";
            }
            if (Finished) return $"<b>{title}</b>\nTerminée !";
            return $"<b>{title}</b>\n{CurrentText()}";
        }

        /// <summary>Position vers laquelle pointe la boussole du HUD (lieu à atteindre ou ennemi vivant le plus proche du type demandé).</summary>
        public Vector3? Waypoint()
        {
            if (Finished || !player) return null;
            var o = Current;
            if (string.IsNullOrEmpty(o.enemyId)) return o.place;
            float best = 1e9f; Vector3? p = null;
            foreach (var h in Health.All)
            {
                var ai = h.GetComponent<EnemyAI>();
                if (h.Dead || !ai || ai.type == null || ai.type.id != o.enemyId) continue;
                float d = (h.transform.position - player.position).sqrMagnitude; if (d < best) { best = d; p = h.transform.position; }
            }
            return p;
        }
    }
}
