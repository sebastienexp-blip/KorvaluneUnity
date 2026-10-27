using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    public enum SkillKind { Strike, Cleave, Line, Projectile, Barrage, Nova, GroundAoE, Zone, Chain, Beam, Leap, Dash, Teleport, Buff, Shield, Heal, Summon, Trap, Rain, Curse, Pull, Whirl, Totem, Combo, Evade }

    /// <summary>Définition d'une compétence (données) : l'exécution et les effets sont génériques par type (SkillRunner).</summary>
    public class SkillDef
    {
        public string id, name, description, classId;
        /// <summary>Rang de la compétence dans le livre de sa classe (sert à retrouver son icône peinte).</summary>
        public int index;
        public SkillKind kind; public Element element;
        public int unlockLevel; public float cooldown, mana, mult, radius, range, duration; public int count;
        public bool stun, pierce, heal;
        /// <summary>Animation propre au héros (état « Sk_* » de son Animator) ; vide = attaque de base.</summary>
        public string anim;
        public int MaxRank => 5;
        /// <summary>Vrai pour une copie transformée par un pouvoir légendaire ou un ensemble.</summary>
        public bool empowered;
        /// <summary>Copie spectrale (écho) : ne rejoue pas l'animation du héros.</summary>
        public bool ghost;
        public SkillDef Clone() => (SkillDef)MemberwiseClone();
    }

    /// <summary>Les 160 compétences des 5 classes (32 chacune), débloquées du niveau 1 au niveau 170.</summary>
    public static class SkillBook
    {
        static readonly Dictionary<string, List<SkillDef>> byClass = new Dictionary<string, List<SkillDef>>();

        static SkillDef S(string cls, string name, SkillKind k, Element e, int lvl, float cd, float mana, float mult, float radius = 3, float range = 8, int count = 1, float dur = 0, string desc = "", bool stun = false, bool pierce = false, bool heal = false)
        {
            var d = new SkillDef { classId = cls, id = cls + "." + name, name = name, kind = k, element = e, unlockLevel = lvl, cooldown = cd, mana = mana, mult = mult, radius = radius, range = range, count = count, duration = dur, description = desc, stun = stun, pierce = pierce, heal = heal };
            d.anim = SkillAnims.Pick(cls, name, k);
            if (!byClass.TryGetValue(cls, out var l)) byClass[cls] = l = new List<SkillDef>();
            d.index = l.Count; l.Add(d); return d;
        }

        public static List<SkillDef> For(string classId) { Init(); return byClass.TryGetValue(classId, out var l) ? l : byClass["KaelDraven"]; }

        // ------------------------------------------------------------------------------------------ arbre
        /// <summary>Paliers de l'arbre (niveau d'accès minimal de chaque colonne).</summary>
        public static readonly int[] TierLevels = { 1, 10, 21, 34, 50, 66, 100, 150 };
        public class Tree { public List<List<int>> columns = new List<List<int>>(); public int[] parent, column, row; }
        static readonly Dictionary<string, Tree> trees = new Dictionary<string, Tree>();

        /// <summary>Arbre de compétences d'une classe : colonnes par palier de niveau ; chaque compétence descend de la plus
        /// proche du palier précédent (même élément de préférence). Une compétence s'apprend quand son parent a au moins un rang.</summary>
        public static Tree TreeOf(string classId)
        {
            if (trees.TryGetValue(classId ?? "", out var t)) return t;
            var book = For(classId); t = new Tree { parent = new int[book.Count], column = new int[book.Count], row = new int[book.Count] };
            for (int k = 0; k < TierLevels.Length; k++) t.columns.Add(new List<int>());
            var order = new List<int>(); for (int i = 0; i < book.Count; i++) order.Add(i);
            order.Sort((a, b) => book[a].unlockLevel != book[b].unlockLevel ? book[a].unlockLevel.CompareTo(book[b].unlockLevel) : a.CompareTo(b));
            foreach (var i in order)
            {
                int c = 0; for (int k = 0; k < TierLevels.Length; k++) if (book[i].unlockLevel >= TierLevels[k]) c = k;
                t.column[i] = c; t.row[i] = t.columns[c].Count; t.columns[c].Add(i);
            }
            for (int c = 0; c < t.columns.Count; c++)
                foreach (var i in t.columns[c])
                {
                    t.parent[i] = -1; if (c == 0) continue;
                    var prev = t.columns[c - 1]; if (prev.Count == 0) continue;
                    float me = (t.row[i] + 0.5f) / t.columns[c].Count; int best = -1; float bd = float.MaxValue;
                    foreach (var j in prev)
                    {
                        float d = Mathf.Abs((t.row[j] + 0.5f) / prev.Count - me) + (book[j].element == book[i].element ? 0 : 0.35f);
                        if (d < bd) { bd = d; best = j; }
                    }
                    t.parent[i] = best;
                }
            return trees[classId ?? ""] = t;
        }

        static bool done;
        static void Init()
        {
            if (done) return; done = true;
            const string K = "KaelDraven", E = "ElyraNoctis", V = "Varek", M = "Morvanna", T = "Tharion";
            // ---- Kael Draven, le Chevalier déchu : acier, ombre et sang
            S(K, "Tranche-ombre", SkillKind.Strike, Element.Ombre, 1, 2.5f, 8, 1.8f, 2.6f, desc: "Une entaille chargée d'ombre qui frappe devant vous.");
            S(K, "Coup de bouclier", SkillKind.Strike, Element.Physique, 3, 6, 12, 1.2f, 2.2f, dur: 1.5f, desc: "Frappe du bouclier qui étourdit.", stun: true);
            S(K, "Charge du déchu", SkillKind.Dash, Element.Ombre, 5, 8, 15, 1.5f, 2, 9, desc: "Fonce à travers les ennemis.");
            S(K, "Tourbillon d'acier", SkillKind.Whirl, Element.Physique, 8, 7, 22, 0.9f, 3, dur: 1.4f, desc: "Tournoie en frappant tout autour de vous.");
            S(K, "Cri de guerre", SkillKind.Buff, Element.Sang, 10, 18, 20, 0.5f, 5, dur: 8, desc: "Dégâts +50 % pendant 8 s.");
            S(K, "Fente perçante", SkillKind.Line, Element.Physique, 12, 5, 14, 2.2f, 1.2f, 7, desc: "Estoc foudroyant qui transperce une ligne d'ennemis.");
            S(K, "Bond fracassant", SkillKind.Leap, Element.Physique, 15, 9, 25, 2.4f, 3.5f, 8, desc: "Bondit et écrase le sol.", stun: true, dur: 0.8f);
            S(K, "Lame de sang", SkillKind.Projectile, Element.Sang, 18, 4, 16, 2.0f, 1.2f, 14, desc: "Projette une vague de sang tranchante.", pierce: true);
            S(K, "Écrasement sismique", SkillKind.GroundAoE, Element.Physique, 21, 10, 28, 3.0f, 4, 6, desc: "Le sol se fend sous un coup titanesque.");
            S(K, "Rempart d'ombre", SkillKind.Shield, Element.Ombre, 24, 20, 25, 0.35f, 2, dur: 6, desc: "Un bouclier d'ombre absorbe les coups.");
            S(K, "Faucheuse", SkillKind.Cleave, Element.Ombre, 27, 6, 20, 2.0f, 3.4f, desc: "Balayage circulaire d'acier noir.");
            S(K, "Chaînes funestes", SkillKind.Pull, Element.Ombre, 30, 12, 24, 1.0f, 7, desc: "Des chaînes ramènent les ennemis vers vous.");
            S(K, "Frénésie sanglante", SkillKind.Buff, Element.Sang, 34, 22, 25, 0.35f, 4, dur: 10, desc: "Vitesse et dégâts accrus.");
            S(K, "Onde de choc", SkillKind.Nova, Element.Physique, 38, 9, 26, 2.2f, 5, desc: "Une onde de force repousse tout autour de vous.");
            S(K, "Riposte", SkillKind.Shield, Element.Ombre, 42, 16, 20, 0.5f, 2, dur: 4, desc: "Pare et renvoie une partie des dégâts.");
            S(K, "Exécution", SkillKind.Strike, Element.Sang, 46, 10, 30, 4.5f, 2.6f, desc: "Coup fatal, dévastateur contre les ennemis affaiblis.");
            S(K, "Lames spectrales", SkillKind.Barrage, Element.Ombre, 50, 8, 28, 1.1f, 1, 14, 6, desc: "Six lames d'ombre jaillissent en éventail.");
            S(K, "Marque du déchu", SkillKind.Curse, Element.Ombre, 55, 14, 22, 0.6f, 5, 8, dur: 8, desc: "Les ennemis marqués subissent davantage de dégâts.");
            S(K, "Brise-armure", SkillKind.Strike, Element.Physique, 60, 8, 24, 2.6f, 2.6f, desc: "Frappe qui fracasse les armures.");
            S(K, "Tempête de lames", SkillKind.Rain, Element.Physique, 66, 15, 40, 1.4f, 6, 8, 14, 3, desc: "Une pluie d'épées s'abat sur la zone.");
            S(K, "Étendard maudit", SkillKind.Totem, Element.Sang, 72, 25, 35, 0.8f, 6, dur: 12, desc: "Plante un étendard qui draine les ennemis proches.");
            S(K, "Saignée", SkillKind.Zone, Element.Sang, 78, 14, 32, 0.7f, 4.5f, 6, dur: 6, desc: "Une mare de sang qui ronge les ennemis.");
            S(K, "Avatar de l'ombre", SkillKind.Buff, Element.Ombre, 85, 45, 50, 1.0f, 6, dur: 15, desc: "Devient un avatar d'ombre : dégâts doublés.");
            S(K, "Fissure abyssale", SkillKind.Line, Element.Ombre, 92, 12, 38, 3.2f, 2, 12, desc: "Une crevasse d'ombre s'ouvre en ligne droite.");
            S(K, "Garde inébranlable", SkillKind.Shield, Element.Physique, 100, 30, 40, 0.6f, 3, dur: 8, desc: "Réduit fortement les dégâts subis.");
            S(K, "Moisson d'âmes", SkillKind.Nova, Element.Ombre, 110, 18, 45, 2.8f, 6, desc: "Arrache les âmes alentour et vous soigne.", heal: true);
            S(K, "Lame du jugement", SkillKind.GroundAoE, Element.Sang, 120, 20, 55, 6.0f, 5, 8, desc: "Une épée géante de sang tombe du ciel.");
            S(K, "Légion déchue", SkillKind.Summon, Element.Ombre, 135, 40, 60, 0.6f, 3, count: 3, dur: 20, desc: "Invoque trois chevaliers spectraux.");
            S(K, "Éclipse d'acier", SkillKind.Rain, Element.Ombre, 150, 30, 70, 2.0f, 9, 8, 24, 4, desc: "Le ciel s'obscurcit et des lames noires pleuvent.");
            S(K, "Dernier serment", SkillKind.Nova, Element.Sang, 170, 60, 90, 9.0f, 9, desc: "Explosion de sang et d'acier d'une puissance ultime.");

            // ---- Elyra Noctis, l'Archimage du Néant : Néant, foudre, givre
            S(E, "Trait du Néant", SkillKind.Projectile, Element.Neant, 1, 1.2f, 6, 1.6f, 1, 16, desc: "Un trait d'énergie du Néant.");
            S(E, "Éclair", SkillKind.Chain, Element.Foudre, 3, 3, 12, 1.4f, 6, 10, 3, desc: "Un éclair rebondit entre trois ennemis.");
            S(E, "Nova de givre", SkillKind.Nova, Element.Glace, 5, 8, 18, 1.4f, 5, desc: "Gèle les ennemis proches.", stun: true, dur: 1.5f);
            S(E, "Orbe du Néant", SkillKind.Projectile, Element.Neant, 8, 5, 20, 1.6f, 1.6f, 14, desc: "Un orbe lent qui traverse tout.", pierce: true);
            S(E, "Téléportation", SkillKind.Teleport, Element.Neant, 10, 8, 15, 0.8f, 3, 10, desc: "Se téléporte et repousse les ennemis.");
            S(E, "Lance de glace", SkillKind.Projectile, Element.Glace, 12, 3, 14, 2.2f, 1.2f, 18, desc: "Une lance de glace perforante.", pierce: true);
            S(E, "Champ d'éclairs", SkillKind.Zone, Element.Foudre, 15, 12, 26, 0.8f, 4.5f, 8, dur: 5, desc: "La foudre frappe sans cesse dans la zone.");
            S(E, "Bouclier arcanique", SkillKind.Shield, Element.Neant, 18, 18, 22, 0.4f, 2, dur: 8, desc: "Un bouclier d'énergie absorbe les dégâts.");
            S(E, "Effondrement stellaire", SkillKind.GroundAoE, Element.Neant, 21, 10, 30, 3.2f, 4, 10, desc: "Une étoile s'effondre sur la cible.");
            S(E, "Rayon du Néant", SkillKind.Beam, Element.Neant, 24, 9, 30, 0.6f, 1, 14, dur: 2.5f, desc: "Un rayon continu qui consume tout.");
            S(E, "Tempête de glace", SkillKind.Zone, Element.Glace, 27, 14, 32, 0.7f, 5, 10, dur: 6, desc: "Un blizzard ralentit et blesse.");
            S(E, "Chaîne de foudre", SkillKind.Chain, Element.Foudre, 30, 6, 25, 1.8f, 7, 12, 6, desc: "La foudre bondit sur six ennemis.");
            S(E, "Trou noir", SkillKind.Pull, Element.Neant, 34, 16, 35, 1.4f, 6, 10, desc: "Aspire les ennemis vers un point.");
            S(E, "Prison de givre", SkillKind.Curse, Element.Glace, 38, 14, 28, 0.8f, 4, 10, dur: 3, desc: "Emprisonne les ennemis dans la glace.", stun: true);
            S(E, "Pluie d'étoiles", SkillKind.Rain, Element.Neant, 42, 15, 40, 1.5f, 6, 10, 12, 3, desc: "Des fragments d'étoiles s'abattent.");
            S(E, "Éclats arcaniques", SkillKind.Barrage, Element.Neant, 46, 7, 26, 0.9f, 1, 16, 8, desc: "Huit éclats en éventail.");
            S(E, "Familier du Néant", SkillKind.Summon, Element.Neant, 50, 30, 40, 0.5f, 2, count: 1, dur: 25, desc: "Invoque une entité du Néant qui combat à vos côtés.");
            S(E, "Surcharge", SkillKind.Buff, Element.Foudre, 55, 25, 25, 0.4f, 4, dur: 10, desc: "Puissance des sorts accrue.");
            S(E, "Distorsion", SkillKind.Dash, Element.Neant, 60, 7, 18, 1.2f, 2, 8, desc: "Glisse dans le Néant en blessant sur le passage.");
            S(E, "Cercle de runes", SkillKind.Zone, Element.Neant, 66, 18, 40, 1.0f, 5, 6, dur: 7, desc: "Un cercle runique qui déchire la réalité.");
            S(E, "Comète", SkillKind.GroundAoE, Element.Feu, 72, 14, 45, 4.5f, 5, 12, desc: "Une comète embrasée frappe la cible.");
            S(E, "Lame de foudre", SkillKind.Line, Element.Foudre, 78, 9, 35, 2.8f, 1.5f, 14, desc: "Un arc de foudre rectiligne.");
            S(E, "Tornade de givre", SkillKind.Projectile, Element.Glace, 85, 10, 38, 2.4f, 2.2f, 16, desc: "Une tornade glacée qui traverse tout.", pierce: true);
            S(E, "Singularité", SkillKind.Nova, Element.Neant, 92, 20, 50, 4.0f, 7, desc: "Une implosion du Néant autour de vous.");
            S(E, "Miroir astral", SkillKind.Summon, Element.Neant, 100, 40, 55, 0.6f, 2, count: 2, dur: 20, desc: "Deux reflets astraux vous assistent.");
            S(E, "Hiver éternel", SkillKind.Zone, Element.Glace, 110, 30, 65, 1.2f, 8, 10, dur: 8, desc: "Un hiver mortel s'abat sur la région.", stun: true);
            S(E, "Jugement céleste", SkillKind.Beam, Element.Foudre, 120, 22, 60, 1.2f, 2, 18, dur: 3, desc: "Un torrent de foudre divine.");
            S(E, "Faille dimensionnelle", SkillKind.Teleport, Element.Neant, 135, 15, 45, 3.5f, 5, 12, desc: "Ouvre une faille, s'y glisse et la fait exploser.");
            S(E, "Apocalypse du Néant", SkillKind.Rain, Element.Neant, 150, 45, 85, 2.5f, 10, 12, 30, 5, desc: "Le Néant déverse sa fureur sur le monde.");
            S(E, "Éclipse totale", SkillKind.Nova, Element.Neant, 170, 60, 100, 10f, 11, desc: "Le soleil s'éteint ; tout est englouti.");

            // ---- Varek, le Chasseur des cendres : arbalète, pièges, cendres, poison
            S(V, "Carreau perçant", SkillKind.Projectile, Element.Physique, 1, 1.0f, 5, 1.5f, 0.8f, 20, desc: "Un carreau qui transperce.", pierce: true);
            S(V, "Volée", SkillKind.Barrage, Element.Physique, 3, 4, 12, 0.9f, 0.8f, 16, 5, desc: "Cinq carreaux en éventail.");
            S(V, "Carreau incendiaire", SkillKind.Projectile, Element.Feu, 5, 4, 14, 2.2f, 2.5f, 18, desc: "Un carreau qui explose en flammes.");
            S(V, "Piège à mâchoires", SkillKind.Trap, Element.Physique, 8, 8, 12, 1.8f, 2, dur: 20, desc: "Un piège qui immobilise.", stun: true);
            S(V, "Roulade", SkillKind.Dash, Element.Physique, 10, 5, 8, 0.0f, 1, 6, desc: "Esquive rapide.");
            S(V, "Flèche empoisonnée", SkillKind.Projectile, Element.Poison, 12, 4, 14, 1.4f, 2, 18, desc: "Empoisonne la cible et ses voisins.");
            S(V, "Grenade de cendres", SkillKind.GroundAoE, Element.Cendre, 15, 8, 22, 2.6f, 3.5f, 10, desc: "Une bombe de cendres brûlantes.");
            S(V, "Marque du chasseur", SkillKind.Curse, Element.Cendre, 18, 12, 15, 0.5f, 4, 14, dur: 10, desc: "Marque les proies : dégâts accrus.");
            S(V, "Pluie de carreaux", SkillKind.Rain, Element.Physique, 21, 12, 30, 1.0f, 5, 14, 16, 3, desc: "Une averse de carreaux sur la zone.");
            S(V, "Piège explosif", SkillKind.Trap, Element.Feu, 24, 10, 20, 3.0f, 3, dur: 20, desc: "Explose au passage d'un ennemi.");
            S(V, "Tourelle", SkillKind.Totem, Element.Physique, 27, 20, 30, 0.6f, 12, dur: 15, desc: "Une arbalète automatique tire sur les ennemis.");
            S(V, "Faucon de cendres", SkillKind.Summon, Element.Cendre, 30, 30, 30, 0.5f, 2, count: 1, dur: 30, desc: "Un faucon de braise attaque vos cibles.");
            S(V, "Tir de barrage", SkillKind.Barrage, Element.Physique, 34, 9, 28, 0.8f, 0.8f, 18, 10, desc: "Dix carreaux en rafale.");
            S(V, "Nuage toxique", SkillKind.Zone, Element.Poison, 38, 14, 28, 0.8f, 4.5f, 12, dur: 7, desc: "Un nuage de poison corrosif.");
            S(V, "Carreau éclatant", SkillKind.Barrage, Element.Feu, 42, 8, 26, 1.2f, 1.5f, 16, 3, desc: "Trois carreaux qui éclatent en flammes.");
            S(V, "Camouflage", SkillKind.Buff, Element.Ombre, 46, 25, 20, 0.6f, 3, dur: 6, desc: "Devient insaisissable ; prochaines attaques renforcées.");
            S(V, "Filet", SkillKind.Curse, Element.Physique, 50, 12, 18, 0.3f, 3.5f, 12, dur: 3, desc: "Un filet immobilise les ennemis.", stun: true);
            S(V, "Tir fatal", SkillKind.Projectile, Element.Physique, 55, 10, 35, 5.0f, 1.5f, 24, desc: "Un tir chargé dévastateur.", pierce: true);
            S(V, "Flèches de feu", SkillKind.Rain, Element.Feu, 60, 14, 40, 1.3f, 5, 14, 18, 3, desc: "Une pluie de flèches enflammées.");
            S(V, "Loup spectral", SkillKind.Summon, Element.Ombre, 66, 30, 40, 0.6f, 2, count: 1, dur: 30, desc: "Un loup d'ombre chasse avec vous.");
            S(V, "Fumigène", SkillKind.Zone, Element.Ombre, 72, 18, 20, 0.2f, 5, 0, dur: 6, desc: "Un rideau de fumée qui aveugle.", stun: true);
            S(V, "Mines", SkillKind.Trap, Element.Feu, 78, 14, 30, 2.5f, 3, count: 4, dur: 25, desc: "Quatre mines incendiaires autour de vous.");
            S(V, "Chasseur des cendres", SkillKind.Buff, Element.Cendre, 85, 35, 40, 0.8f, 4, dur: 12, desc: "Tous vos tirs embrasent leurs cibles.");
            S(V, "Carreau tonnerre", SkillKind.Chain, Element.Foudre, 92, 9, 35, 2.0f, 7, 18, 5, desc: "Un carreau chargé de foudre qui rebondit.");
            S(V, "Tir en éventail", SkillKind.Barrage, Element.Physique, 100, 8, 35, 1.1f, 0.8f, 18, 9, desc: "Neuf carreaux en éventail.");
            S(V, "Éruption", SkillKind.GroundAoE, Element.Feu, 110, 18, 55, 5.5f, 6, 12, desc: "La terre vomit les cendres et le feu.");
            S(V, "Meute", SkillKind.Summon, Element.Ombre, 120, 45, 60, 0.6f, 2, count: 3, dur: 25, desc: "Trois loups d'ombre déferlent.");
            S(V, "Brasier", SkillKind.Zone, Element.Feu, 135, 20, 55, 1.4f, 6, 12, dur: 8, desc: "Un incendie qui dévore tout.");
            S(V, "Œil du faucon", SkillKind.Buff, Element.Physique, 150, 40, 50, 1.0f, 4, dur: 15, desc: "Coups critiques assurés.");
            S(V, "Déluge de cendres", SkillKind.Rain, Element.Cendre, 170, 55, 95, 2.6f, 10, 14, 34, 5, desc: "Le ciel brûle et déverse des cendres ardentes.");

            // ---- Morvanna, la Nécromancienne : os, sang, âmes
            S(M, "Lance d'os", SkillKind.Projectile, Element.Os, 1, 1.4f, 6, 1.6f, 1, 18, desc: "Une lance d'os qui transperce.", pierce: true);
            S(M, "Éclat d'âme", SkillKind.Projectile, Element.Os, 3, 2.5f, 10, 1.6f, 2, 16, desc: "Une âme hurlante qui explose.");
            S(M, "Lever les morts", SkillKind.Summon, Element.Os, 5, 20, 25, 0.5f, 2, count: 2, dur: 30, desc: "Deux guerriers squelettes se relèvent.");
            S(M, "Explosion de cadavre", SkillKind.GroundAoE, Element.Os, 8, 6, 18, 2.6f, 3.5f, 10, desc: "Fait exploser un cadavre.");
            S(M, "Prison d'os", SkillKind.Curse, Element.Os, 10, 14, 20, 0.6f, 3.5f, 10, dur: 3, desc: "Des os jaillissent et emprisonnent.", stun: true);
            S(M, "Siphon de sang", SkillKind.Beam, Element.Sang, 12, 8, 20, 0.5f, 1, 10, dur: 2.5f, desc: "Draine la vie d'un ennemi.", heal: true);
            S(M, "Nova d'os", SkillKind.Nova, Element.Os, 15, 8, 24, 1.8f, 5, desc: "Des éclats d'os jaillissent en cercle.");
            S(M, "Armure d'os", SkillKind.Shield, Element.Os, 18, 20, 22, 0.4f, 2, dur: 8, desc: "Une armure d'os absorbe les dégâts.");
            S(M, "Malédiction de faiblesse", SkillKind.Curse, Element.Ombre, 21, 15, 20, 0.4f, 5, 12, dur: 10, desc: "Affaiblit les ennemis maudits.");
            S(M, "Esprit vengeur", SkillKind.Barrage, Element.Os, 24, 6, 24, 1.2f, 1.5f, 16, 3, desc: "Trois esprits vengeurs fondent sur les ennemis.");
            S(M, "Mur d'ossements", SkillKind.Zone, Element.Os, 27, 16, 26, 0.6f, 4, 8, dur: 6, desc: "Un champ d'ossements qui lacère.");
            S(M, "Lame de sang", SkillKind.Strike, Element.Sang, 30, 4, 14, 2.4f, 3, desc: "Une faux de sang tranche devant vous.");
            S(M, "Âmes errantes", SkillKind.Barrage, Element.Os, 34, 8, 28, 0.9f, 1.2f, 16, 7, desc: "Sept âmes jaillissent en éventail.");
            S(M, "Golem d'os", SkillKind.Summon, Element.Os, 38, 40, 45, 1.2f, 2, count: 1, dur: 40, desc: "Un colosse d'ossements combat pour vous.");
            S(M, "Peste", SkillKind.Zone, Element.Poison, 42, 14, 30, 0.8f, 5, 12, dur: 7, desc: "Un miasme mortel se répand.");
            S(M, "Moisson", SkillKind.Cleave, Element.Os, 46, 6, 22, 2.0f, 3.5f, desc: "Une faux spectrale fauche autour de vous.");
            S(M, "Tempête d'âmes", SkillKind.Rain, Element.Os, 50, 15, 40, 1.4f, 6, 12, 14, 3, desc: "Les âmes s'abattent en hurlant.");
            S(M, "Pacte de sang", SkillKind.Buff, Element.Sang, 55, 25, 0, 0.6f, 4, dur: 10, desc: "Sacrifie de la vie pour une puissance accrue.");
            S(M, "Fossoyeur", SkillKind.Pull, Element.Ombre, 60, 14, 26, 1.0f, 7, 10, desc: "Des mains de morts attirent les vivants.");
            S(M, "Mages squelettes", SkillKind.Summon, Element.Os, 66, 35, 40, 0.6f, 2, count: 2, dur: 30, desc: "Deux mages squelettes vous assistent.");
            S(M, "Vague de mort", SkillKind.Line, Element.Os, 72, 9, 32, 2.6f, 2, 14, desc: "Une vague d'ossements en ligne droite.");
            S(M, "Nuée de crânes", SkillKind.Barrage, Element.Os, 78, 10, 36, 1.0f, 1.4f, 18, 10, desc: "Dix crânes enflammés d'âme.");
            S(M, "Linceul", SkillKind.Shield, Element.Ombre, 85, 30, 35, 0.6f, 2, dur: 8, desc: "Un linceul d'ombre vous dérobe aux coups.");
            S(M, "Cri de la banshee", SkillKind.Nova, Element.Ombre, 92, 16, 40, 2.4f, 7, desc: "Un cri qui paralyse de terreur.", stun: true, dur: 2);
            S(M, "Marée de sang", SkillKind.Nova, Element.Sang, 100, 20, 50, 3.4f, 7, desc: "Une marée de sang qui soigne et blesse.", heal: true);
            S(M, "Armée des morts", SkillKind.Summon, Element.Os, 110, 60, 70, 0.6f, 3, count: 5, dur: 30, desc: "Cinq morts se relèvent pour vous.");
            S(M, "Déchirure de l'âme", SkillKind.GroundAoE, Element.Os, 120, 18, 55, 5.5f, 5, 12, desc: "L'âme du monde se déchire sous l'ennemi.");
            S(M, "Faux spectrale", SkillKind.Cleave, Element.Os, 135, 10, 45, 4.0f, 5, desc: "Une faux géante tourne autour de vous.");
            S(M, "Fléau", SkillKind.Zone, Element.Poison, 150, 35, 75, 1.8f, 9, 12, dur: 10, desc: "Le fléau ravage toute la région.");
            S(M, "Seigneur de la mort", SkillKind.Buff, Element.Os, 170, 60, 90, 1.5f, 6, dur: 20, desc: "Devient seigneur de la mort : puissance démultipliée.");

            // ---- Tharion, le Gardien solaire : lumière, marteau, pavois
            S(T, "Marteau béni", SkillKind.Strike, Element.Sacre, 1, 2.5f, 8, 1.8f, 2.8f, desc: "Un coup de marteau nimbé de lumière.");
            S(T, "Coup de pavois", SkillKind.Strike, Element.Physique, 3, 6, 12, 1.2f, 2.4f, dur: 1.5f, desc: "Frappe du pavois qui étourdit.", stun: true);
            S(T, "Marteau tournoyant", SkillKind.Projectile, Element.Sacre, 5, 3, 14, 2.0f, 1.5f, 14, desc: "Lance un marteau sacré.", pierce: true);
            S(T, "Consécration", SkillKind.Zone, Element.Sacre, 8, 12, 22, 0.7f, 4.5f, 0, dur: 6, desc: "Sanctifie le sol : les ennemis brûlent.");
            S(T, "Charge sacrée", SkillKind.Dash, Element.Sacre, 10, 8, 15, 1.6f, 2, 9, desc: "Charge auréolée de lumière.");
            S(T, "Bénédiction", SkillKind.Heal, Element.Sacre, 12, 15, 20, 0.35f, 3, dur: 0, desc: "Rend 35 % de la vie.");
            S(T, "Jugement", SkillKind.GroundAoE, Element.Sacre, 15, 9, 24, 2.8f, 4, 10, desc: "Un jugement céleste frappe la cible.");
            S(T, "Bouclier de foi", SkillKind.Shield, Element.Sacre, 18, 18, 22, 0.45f, 2, dur: 8, desc: "La foi vous protège.");
            S(T, "Rayon solaire", SkillKind.Beam, Element.Sacre, 21, 9, 28, 0.6f, 1.2f, 14, dur: 2.5f, desc: "Un rayon de soleil brûlant.");
            S(T, "Aura de courage", SkillKind.Buff, Element.Sacre, 24, 22, 20, 0.4f, 5, dur: 10, desc: "Dégâts et armure accrus.");
            S(T, "Colère divine", SkillKind.Rain, Element.Sacre, 27, 15, 36, 1.4f, 6, 10, 12, 3, desc: "Des colonnes de lumière s'abattent.");
            S(T, "Martèlement", SkillKind.Nova, Element.Physique, 30, 8, 24, 2.0f, 5, desc: "Le sol tremble sous le marteau.", stun: true, dur: 0.8f);
            S(T, "Lumière guérisseuse", SkillKind.Heal, Element.Sacre, 34, 20, 30, 0.6f, 5, desc: "Rend 60 % de la vie.");
            S(T, "Chaîne sacrée", SkillKind.Chain, Element.Sacre, 38, 7, 26, 1.6f, 7, 12, 5, desc: "La lumière bondit entre cinq ennemis.");
            S(T, "Bond du croisé", SkillKind.Leap, Element.Sacre, 42, 10, 28, 2.6f, 4, 9, desc: "Bondit et frappe dans un éclat de lumière.", stun: true, dur: 1);
            S(T, "Marteau de feu", SkillKind.GroundAoE, Element.Feu, 46, 10, 30, 3.2f, 4, 10, desc: "Un marteau embrasé s'écrase.");
            S(T, "Lance de lumière", SkillKind.Line, Element.Sacre, 50, 8, 28, 2.6f, 1.4f, 14, desc: "Une lance de lumière traverse les rangs.");
            S(T, "Gardien céleste", SkillKind.Summon, Element.Sacre, 55, 40, 45, 0.8f, 2, count: 1, dur: 30, desc: "Un gardien céleste combat à vos côtés.");
            S(T, "Éblouissement", SkillKind.Curse, Element.Sacre, 60, 14, 22, 0.4f, 5, 0, dur: 2.5f, desc: "Aveugle les ennemis alentour.", stun: true);
            S(T, "Châtiment", SkillKind.Strike, Element.Sacre, 66, 9, 32, 4.0f, 3, desc: "Un coup de marteau dévastateur.");
            S(T, "Pluie de marteaux", SkillKind.Rain, Element.Sacre, 72, 16, 45, 1.6f, 6, 10, 16, 3, desc: "Des marteaux sacrés pleuvent.");
            S(T, "Sanctuaire", SkillKind.Zone, Element.Sacre, 78, 25, 40, 0.5f, 5, 0, dur: 8, desc: "Un sanctuaire qui soigne et punit.", heal: true);
            S(T, "Fureur solaire", SkillKind.Buff, Element.Feu, 85, 30, 35, 0.6f, 5, dur: 12, desc: "Vos coups embrasent les ennemis.");
            S(T, "Onde purificatrice", SkillKind.Nova, Element.Sacre, 92, 14, 40, 2.6f, 7, desc: "Une onde de lumière purificatrice.");
            S(T, "Ange gardien", SkillKind.Shield, Element.Sacre, 100, 45, 50, 0.8f, 3, dur: 8, desc: "Un ange vous rend presque invulnérable.");
            S(T, "Soleil levant", SkillKind.Nova, Element.Feu, 110, 22, 55, 4.0f, 8, desc: "Un soleil naît de votre marteau.");
            S(T, "Juge céleste", SkillKind.Beam, Element.Sacre, 120, 22, 60, 1.4f, 2, 18, dur: 3, desc: "La sentence divine en un rayon.");
            S(T, "Croisade", SkillKind.Buff, Element.Sacre, 135, 50, 60, 1.0f, 6, dur: 15, desc: "Ferveur de croisade : puissance accrue.");
            S(T, "Avènement", SkillKind.GroundAoE, Element.Sacre, 150, 35, 80, 8.0f, 8, 12, desc: "Le ciel s'ouvre et une lumière écrasante tombe.");
            S(T, "Supernova", SkillKind.Nova, Element.Feu, 170, 60, 100, 10f, 11, desc: "Vous devenez un soleil qui explose.");

            // ---- compétences signatures, construites autour des animations propres à chaque héros
            S(K, "Assaut du déchu", SkillKind.Combo, Element.Ombre, 7, 6, 18, 0.9f, 2.8f, count: 4, desc: "Enchaînement de quatre coups d'épée, le dernier vers le sol.");
            S(K, "Pieu d'acier", SkillKind.Nova, Element.Physique, 63, 12, 30, 2.4f, 4.5f, dur: 1.2f, desc: "Plante l'épée dans le sol : des lames jaillissent tout autour et étourdissent.", stun: true);
            S(E, "Rafale arcanique", SkillKind.Barrage, Element.Neant, 7, 4, 14, 0.8f, 1, 14, 4, desc: "Quatre éclats lancés d'un revers de la main.");
            S(E, "Spirale du Néant", SkillKind.Whirl, Element.Neant, 63, 12, 32, 0.9f, 4, dur: 1.0f, desc: "Tournoie sur vous-même en libérant une spirale d'énergie.");
            S(V, "Salto arrière", SkillKind.Evade, Element.Cendre, 7, 7, 12, 1.2f, 2.5f, 6, desc: "Bondit en arrière et laisse une explosion de cendres.");
            S(V, "Ruade", SkillKind.Strike, Element.Physique, 63, 8, 16, 2.0f, 2.2f, dur: 1.5f, desc: "Coup de pied qui repousse et étourdit.", stun: true);
            S(M, "Sacrifice de sang", SkillKind.Buff, Element.Sang, 7, 16, 0, 0.6f, 3, dur: 8, desc: "S'entaille la paume : 10 % de vie contre +60 % de dégâts.");
            S(M, "Étreinte d'outre-tombe", SkillKind.Nova, Element.Ombre, 63, 14, 34, 2.6f, 6, desc: "Ouvre les bras : les âmes alentour sont arrachées et vous soignent.", heal: true);
            S(T, "Marteau boomerang", SkillKind.Projectile, Element.Sacre, 7, 5, 14, 1.8f, 1.2f, 14, desc: "Lance le marteau qui traverse les ennemis.", pierce: true);
            S(T, "Prière de l'aube", SkillKind.Heal, Element.Sacre, 63, 24, 30, 0.45f, 4, desc: "S'agenouille et prie : rend une grande partie de la vie.");
        }
    }
}
