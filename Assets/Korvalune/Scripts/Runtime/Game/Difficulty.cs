using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Niveaux de difficulté (façon Diablo) : Normal, Cauchemar, Enfer puis Tourment I à IV, débloqués avec le niveau du
    /// héros. Les monstres gagnent vie et dégâts ; en échange, expérience, or et chance de butin rare augmentent.
    /// Choisi par personnage dans le menu pause ; s'applique aux monstres qui (ré)apparaissent.</summary>
    public static class Difficulty
    {
        public static readonly string[] Names = { "Normal", "Cauchemar", "Enfer", "Tourment I", "Tourment II", "Tourment III", "Tourment IV" };
        public static readonly int[] UnlockLevel = { 1, 20, 40, 60, 80, 100, 120 };
        static readonly float[] hp = { 1f, 1.6f, 2.5f, 4f, 6f, 9f, 13f };
        static readonly float[] dmg = { 1f, 1.3f, 1.7f, 2.2f, 2.8f, 3.5f, 4.3f };
        static readonly float[] xp = { 1f, 1.5f, 2.2f, 3f, 4f, 5.5f, 7f };
        static readonly float[] luck = { 0f, 0.6f, 1.2f, 2f, 2.8f, 3.6f, 4.5f };
        static readonly float[] gold = { 1f, 1.4f, 1.9f, 2.6f, 3.4f, 4.3f, 5.5f };

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
