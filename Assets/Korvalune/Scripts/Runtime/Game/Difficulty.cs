using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Niveaux de difficulté : Normal, Éprouvant, Funeste puis Abîme I à IV. Chaque palier rend les monstres
    /// nettement plus durs (vie, dégâts, cadence d'attaque, vitesse, portée de repérage, camps plus fournis, champions
    /// plus fréquents) et rapporte d'autant plus : expérience, or, butin rare, récompenses de quêtes. Choisi par
    /// personnage dans le menu pause ; les camps se repeuplent aussitôt avec la nouvelle difficulté.</summary>
    public static class Difficulty
    {
        public static readonly string[] Names = { "Normal", "Éprouvant", "Funeste", "Abîme I", "Abîme II", "Abîme III", "Abîme IV" };
        public static readonly int[] UnlockLevel = { 1, 1, 1, 1, 1, 1, 1 };     // toutes accessibles dès le départ
        static readonly float[] hp = { 1f, 2.2f, 4f, 7f, 11f, 17f, 26f };
        static readonly float[] dmg = { 1f, 1.6f, 2.4f, 3.4f, 4.6f, 6.2f, 8.2f };
        static readonly float[] haste = { 1f, 0.88f, 0.78f, 0.7f, 0.63f, 0.57f, 0.52f };     // délai entre deux attaques
        static readonly float[] speed = { 1f, 1.08f, 1.15f, 1.22f, 1.28f, 1.34f, 1.4f };
        static readonly float[] aggro = { 1f, 1.15f, 1.3f, 1.45f, 1.6f, 1.7f, 1.8f };
        static readonly int[] extra = { 0, 0, 1, 1, 2, 2, 3 };                               // monstres en plus par camp
        static readonly float[] xp = { 1f, 1.8f, 2.8f, 4.2f, 6f, 8.5f, 12f };
        static readonly float[] luck = { 0f, 0.8f, 1.6f, 2.6f, 3.6f, 4.8f, 6f };
        static readonly float[] gold = { 1f, 1.7f, 2.6f, 3.8f, 5.2f, 7f, 9.5f };
        static readonly float[] quest = { 1f, 1.25f, 1.5f, 1.8f, 2.1f, 2.5f, 3f };           // récompenses de quêtes

        public static int Count => Names.Length;
        public static int Current
        {
            get { var s = SaveSystem.Current; return s != null ? Mathf.Clamp(s.difficulty, 0, Count - 1) : 0; }
            set { var s = SaveSystem.Current; if (s != null) s.difficulty = Mathf.Clamp(value, 0, Count - 1); }
        }
        public static string Name => Names[Current];
        public static float Hp => hp[Current];
        public static float Damage => dmg[Current];
        public static float Xp => xp[Current];
        public static float Luck => luck[Current];
        public static float Gold => gold[Current];
        public static float Haste => haste[Current];
        public static float Speed => speed[Current];
        public static float Aggro => aggro[Current];
        public static int ExtraPerCamp => extra[Current];
        public static float Quest => quest[Current];
        /// <summary>Résumé lisible des effets du palier (menu pause).</summary>
        public static string Summary(int d) => d <= 0 ? "Monstres normaux" :
            $"Monstres : vie ×{hp[d]:0.#}, dégâts ×{dmg[d]:0.#}, plus rapides  ·  Gains : XP ×{xp[d]:0.#}, or ×{gold[d]:0.#}, butin rare +{luck[d] * 25:0} %";

        public static bool Unlocked(int d, int heroLevel) => d >= 0 && d < Count && heroLevel >= UnlockLevel[d];

        /// <summary>Difficulté suivante débloquée (revient à Normal après la plus haute).</summary>
        public static int Next(int heroLevel)
        {
            int d = Current;
            for (int i = 1; i <= Count; i++) { int n = (d + i) % Count; if (Unlocked(n, heroLevel)) return n; }
            return 0;
        }

        public static Color ColorOf(int d) => d <= 0 ? new Color(0.86f, 0.82f, 0.72f) : d == 1 ? new Color(0.55f, 0.7f, 1f) : d == 2 ? new Color(1f, 0.6f, 0.25f)
            : new Color(1f, 0.28f, 0.22f);
    }
}
