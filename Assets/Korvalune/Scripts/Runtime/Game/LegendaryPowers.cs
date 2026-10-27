using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Pouvoir légendaire (comme dans Diablo 3/4) : il transforme une compétence précise, un élément ou un type de
    /// compétence — dégâts, rayon (donc la taille des effets), nombre de projectiles, durée, recharge, changement d'élément
    /// (couleur et effets de l'élément), perforation, étourdissement et « écho » (la compétence se déclenche une seconde fois).
    /// Les pouvoirs portés se CUMULENT (multiplicativement) : c'est ce qui permet les builds spectaculaires.</summary>
    public class PowerDef
    {
        public string id, name, cls = "";              // cls vide = pouvoir utilisable par toutes les classes
        public string skill = "";                      // nom exact de la compétence visée (vide = filtre par élément / type)
        public Element? element; public SkillKind? kind; // filtres (aucun filtre = toutes les compétences)
        public float dmg, radius, duration, cooldown, echo;
        public int count;
        public Element? toElement;
        public bool pierce, stun;
        public bool setRing;                           // anneau de convergence : les ensembles comptent une pièce de plus

        public bool Matches(SkillDef d)
        {
            if (!string.IsNullOrEmpty(skill)) return d.name == skill;
            if (element.HasValue && d.element != element.Value) return false;
            if (kind.HasValue && d.kind != kind.Value) return false;
            return true;
        }
    }

    /// <summary>Ensemble d'objets (sets) : six pièces, bonus à 2, 4 et 6 pièces portées.</summary>
    public class SetDef
    {
        public string id, name, of, cls;               // of : complément du nom des pièces (« du Serment déchu »)
        public Slot[] slots = Powers.SetSlots;
        public List<AffixSave> bonus2 = new List<AffixSave>();
        public string power4, power6;
    }

    /// <summary>Catalogue des pouvoirs légendaires (une douzaine par classe + quelques pouvoirs communs) et des ensembles
    /// (4 par classe, 20 en tout).</summary>
    public static class Powers
    {
        public const string K = "KaelDraven", E = "ElyraNoctis", V = "Varek", M = "Morvanna", T = "Tharion";
        public static readonly Slot[] SetSlots = { Slot.Casque, Slot.Torse, Slot.Gants, Slot.Jambes, Slot.Bottes, Slot.Amulette };
        public static readonly Color SetColor = new Color(0.24f, 0.86f, 0.32f);

        static readonly Dictionary<string, PowerDef> byId = new Dictionary<string, PowerDef>();
        static readonly Dictionary<string, List<PowerDef>> legendaryByClass = new Dictionary<string, List<PowerDef>>();
        static readonly Dictionary<string, SetDef> sets = new Dictionary<string, SetDef>();
        static readonly Dictionary<string, List<SetDef>> setsByClass = new Dictionary<string, List<SetDef>>();

        public static PowerDef Get(string id) { Init(); return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var p) ? p : null; }
        public static SetDef Set(string id) { Init(); return !string.IsNullOrEmpty(id) && sets.TryGetValue(id, out var s) ? s : null; }
        public static List<PowerDef> Legendary(string cls) { Init(); return legendaryByClass.TryGetValue(cls ?? "", out var l) ? l : legendaryByClass[""]; }
        public static List<SetDef> SetsOf(string cls) { Init(); return setsByClass.TryGetValue(cls ?? "", out var l) ? l : setsByClass[K]; }

        /// <summary>Force du pouvoir selon la rareté de l'objet qui le porte.</summary>
        public static float Strength(Rarity r) => r switch { Rarity.Unique => 1.15f, Rarity.Mythique => 1.35f, Rarity.Ancestral => 1.6f, _ => 1f };

        // ------------------------------------------------------------------------------------------ textes
        public static string ElementName(Element e) => e switch
        {
            Element.Physique => "physiques", Element.Feu => "de feu", Element.Foudre => "de foudre", Element.Glace => "de givre",
            Element.Neant => "du Néant", Element.Ombre => "d'ombre", Element.Sacre => "sacrées", Element.Poison => "de poison",
            Element.Sang => "de sang", Element.Os => "d'os", _ => "de cendres"
        };
        static string ElementNoun(Element e) => e switch
        {
            Element.Physique => "l'acier", Element.Feu => "le feu", Element.Foudre => "la foudre", Element.Glace => "le givre",
            Element.Neant => "le Néant", Element.Ombre => "l'ombre", Element.Sacre => "la lumière sacrée", Element.Poison => "le poison",
            Element.Sang => "le sang", Element.Os => "l'os", _ => "les cendres"
        };
        static string KindName(SkillKind k) => k switch
        {
            SkillKind.Projectile => "projectiles", SkillKind.Barrage => "tirs en éventail", SkillKind.Rain => "pluies", SkillKind.Summon => "invocations",
            SkillKind.Trap => "pièges", SkillKind.Nova => "novas", SkillKind.GroundAoE => "impacts au sol", SkillKind.Zone => "zones", SkillKind.Strike => "coups",
            SkillKind.Chain => "chaînes", SkillKind.Beam => "rayons", SkillKind.Leap => "bonds", SkillKind.Dash => "charges", _ => "compétences"
        };

        /// <summary>Description lisible d'un pouvoir (valeurs déjà multipliées par la force de l'objet).</summary>
        public static string Describe(PowerDef p, float s = 1f)
        {
            if (p.setRing) return "Les ensembles portés comptent une pièce de plus (à partir de 2 pièces).";
            string who = !string.IsNullOrEmpty(p.skill) ? $"« {p.skill} »"
                : p.element.HasValue && p.kind.HasValue ? $"Vos {KindName(p.kind.Value)} {ElementName(p.element.Value)}"
                : p.element.HasValue ? $"Vos compétences {ElementName(p.element.Value)}"
                : p.kind.HasValue ? $"Vos {KindName(p.kind.Value)}" : "Toutes vos compétences";
            var parts = new List<string>();
            if (p.dmg > 0) parts.Add($"dégâts +{Mathf.RoundToInt(p.dmg * s * 100)} %");
            if (p.count > 0) parts.Add($"+{Count(p, s)} {(p.kind == SkillKind.Summon || p.skill.Contains("morts") || p.skill.Contains("Golem") || p.skill.Contains("Meute") ? "serviteur(s)" : "projectile(s) ou impact(s)")}");
            if (p.radius > 0) parts.Add($"rayon +{Mathf.RoundToInt(p.radius * s * 100)} %");
            if (p.duration > 0) parts.Add($"durée +{Mathf.RoundToInt(p.duration * s * 100)} %");
            if (p.cooldown > 0) parts.Add($"recharge −{Mathf.RoundToInt(Mathf.Min(0.6f, p.cooldown * s) * 100)} %");
            if (p.pierce) parts.Add("transperce les ennemis");
            if (p.stun) parts.Add("étourdit");
            if (p.toElement.HasValue) parts.Add($"se change en {ElementNoun(p.toElement.Value)}");
            if (p.echo > 0) parts.Add($"un écho la relance aussitôt à {Mathf.RoundToInt(Mathf.Min(1f, p.echo * s) * 100)} %");
            return who + " : " + string.Join(", ", parts) + ".";
        }

        public static int Count(PowerDef p, float s) => p.count + (s >= 1.5f ? 1 : 0);

        // ------------------------------------------------------------------------------------------ catalogue
        static PowerDef Add(PowerDef p, bool legendary = true)
        {
            byId[p.id] = p;
            if (legendary)
            {
                if (!legendaryByClass.TryGetValue(p.cls, out var l)) legendaryByClass[p.cls] = l = new List<PowerDef>();
                l.Add(p);
            }
            return p;
        }
        static PowerDef L(string cls, string id, string name, string skill, float dmg = 0, float radius = 0, int count = 0, float duration = 0, float cooldown = 0, float echo = 0, Element? to = null, bool pierce = false, bool stun = false)
            => Add(new PowerDef { cls = cls, id = id, name = name, skill = skill, dmg = dmg, radius = radius, count = count, duration = duration, cooldown = cooldown, echo = echo, toElement = to, pierce = pierce, stun = stun });

        static void S(string cls, string id, string name, string of, AffixSave[] two, PowerDef four, PowerDef six)
        {
            four.cls = six.cls = cls; four.id = id + ".4"; six.id = id + ".6"; four.name = name + " (4)"; six.name = name + " (6)";
            Add(four, false); Add(six, false);
            var s = new SetDef { id = id, name = name, of = of, cls = cls, bonus2 = new List<AffixSave>(two), power4 = four.id, power6 = six.id };
            sets[id] = s;
            if (!setsByClass.TryGetValue(cls, out var l)) setsByClass[cls] = l = new List<SetDef>();
            l.Add(s);
        }
        static AffixSave A(string stat, float v) => new AffixSave { stat = stat, value = v };

        static bool done;
        static void Init()
        {
            if (done) return; done = true;

            // ---- pouvoirs communs (toutes classes)
            L("", "commun.fureur", "Fureur des anciens rois", "", dmg: 0.35f);
            L("", "commun.horloge", "Sablier de l'Éclipse", "", cooldown: 0.2f);
            L("", "commun.ampleur", "Cœur de la Comète", "", radius: 0.3f, dmg: 0.15f);
            L("", "commun.multitude", "Prisme d'Auralith", "", count: 1, dmg: 0.1f);
            Add(new PowerDef { cls = "", id = "commun.convergence", name = "Anneau de la Convergence", setRing = true });

            // ---- Kael Draven, le Chevalier déchu
            L(K, "kael.crepuscule", "Crépuscule de Kael", "Tranche-ombre", dmg: 1.2f, radius: 0.4f, echo: 0.6f);
            L(K, "kael.tourbillon", "Maelström écarlate", "Tourbillon d'acier", dmg: 1.5f, radius: 0.5f, duration: 0.5f, to: Element.Sang);
            L(K, "kael.meteore", "Chute du Météore", "Bond fracassant", dmg: 2.0f, radius: 0.6f, to: Element.Feu);
            L(K, "kael.sanguinaire", "Soif du Sanguinaire", "Lame de sang", dmg: 0.8f, count: 2, pierce: true);
            L(K, "kael.faille", "Faille de Ghorvax", "Écrasement sismique", dmg: 1.3f, radius: 0.7f, echo: 0.7f);
            L(K, "kael.faucheuse", "Faux de la Nuit sans fin", "Faucheuse", dmg: 1.4f, radius: 0.5f, echo: 0.5f);
            L(K, "kael.spectres", "Lames du Roi sans aube", "Lames spectrales", dmg: 0.6f, count: 4);
            L(K, "kael.bourreau", "Glas du Bourreau", "Exécution", dmg: 2.5f, cooldown: 0.3f);
            L(K, "kael.tempete", "Tempête d'acier noir", "Tempête de lames", dmg: 0.7f, count: 6, radius: 0.4f, to: Element.Ombre);
            L(K, "kael.assaut", "Assaut implacable", "Assaut du déchu", dmg: 1.2f, echo: 0.6f);
            L(K, "kael.onde", "Écho des Pics Sélénites", "Onde de choc", dmg: 1.4f, radius: 0.6f, stun: true);
            L(K, "kael.fissure", "Gouffre abyssal", "Fissure abyssale", dmg: 1.6f, radius: 0.8f, echo: 0.5f);

            // ---- Elyra Noctis, l'Archimage du Néant
            L(E, "elyra.trait", "Aiguilles du Vide", "Trait du Néant", dmg: 1.0f, count: 2, pierce: true);
            L(E, "elyra.eclair", "Tonnerre d'Arthésia", "Éclair", dmg: 0.8f, count: 4);
            L(E, "elyra.nova", "Souffle d'Auralith", "Nova de givre", dmg: 1.5f, radius: 0.6f, echo: 0.6f);
            L(E, "elyra.orbe", "Lune noire", "Orbe du Néant", dmg: 1.2f, radius: 0.8f);
            L(E, "elyra.lance", "Lances de l'Hiver", "Lance de glace", dmg: 0.9f, count: 3, stun: true);
            L(E, "elyra.effondrement", "Chute des Astres", "Effondrement stellaire", dmg: 1.5f, radius: 0.5f, echo: 0.7f);
            L(E, "elyra.rayon", "Fil de l'Orage", "Rayon du Néant", dmg: 1.2f, duration: 0.5f, to: Element.Foudre);
            L(E, "elyra.chaine", "Collier de Foudre", "Chaîne de foudre", dmg: 1.0f, count: 5);
            L(E, "elyra.trounoir", "Œil de l'Éclipse", "Trou noir", dmg: 2.0f, radius: 0.8f);
            L(E, "elyra.etoiles", "Pluie de Seralyth", "Pluie d'étoiles", dmg: 0.8f, count: 8, radius: 0.3f);
            L(E, "elyra.comete", "Comète gelée", "Comète", dmg: 2.0f, radius: 1.0f, to: Element.Glace);
            L(E, "elyra.singularite", "Cœur de la Singularité", "Singularité", dmg: 1.6f, radius: 0.6f, echo: 0.6f);

            // ---- Varek, le Chasseur des cendres
            L(V, "varek.percant", "Carreaux de Maldrakor", "Carreau perçant", dmg: 1.0f, count: 2, pierce: true);
            L(V, "varek.volee", "Nuée de traits", "Volée", dmg: 0.6f, count: 5);
            L(V, "varek.incendiaire", "Brandon des Cendres", "Carreau incendiaire", dmg: 1.3f, radius: 0.8f);
            L(V, "varek.machoires", "Crocs d'acier", "Piège à mâchoires", dmg: 1.5f, count: 2);
            L(V, "varek.venin", "Venin de la Vouivre", "Flèche empoisonnée", dmg: 1.0f, count: 3);
            L(V, "varek.grenade", "Cendres jumelles", "Grenade de cendres", dmg: 0.8f, radius: 0.4f, echo: 0.8f);
            L(V, "varek.pluie", "Ciel embrasé", "Pluie de carreaux", dmg: 0.6f, count: 10, to: Element.Feu);
            L(V, "varek.explosif", "Poudre noire", "Piège explosif", dmg: 1.2f, count: 3, radius: 0.3f);
            L(V, "varek.tourelle", "Sentinelle infatigable", "Tourelle", dmg: 1.5f, duration: 1.0f);
            L(V, "varek.fatal", "Dernier soupir", "Tir fatal", dmg: 3.0f, cooldown: 0.4f, pierce: true);
            L(V, "varek.salto", "Pas du Faucon", "Salto arrière", dmg: 2.0f, radius: 0.8f);
            L(V, "varek.eruption", "Gueule du Volcan", "Éruption", dmg: 1.6f, radius: 0.7f, echo: 0.6f);

            // ---- Morvanna, la Nécromancienne
            L(M, "morv.lance", "Échine du Wyrm", "Lance d'os", dmg: 1.0f, count: 3, pierce: true);
            L(M, "morv.eclat", "Âme fêlée", "Éclat d'âme", dmg: 1.2f, count: 2, to: Element.Ombre);
            L(M, "morv.levee", "Glas des Tombes", "Lever les morts", dmg: 1.0f, count: 3);
            L(M, "morv.cadavre", "Funérailles ardentes", "Explosion de cadavre", dmg: 1.5f, radius: 0.8f, echo: 0.6f);
            L(M, "morv.siphon", "Calice écarlate", "Siphon de sang", dmg: 1.2f, duration: 0.6f);
            L(M, "morv.nova", "Couronne d'ossements", "Nova d'os", dmg: 1.5f, radius: 0.6f);
            L(M, "morv.esprit", "Cortège vengeur", "Esprit vengeur", dmg: 0.8f, count: 4);
            L(M, "morv.peste", "Miasme de Ghorvax", "Peste", dmg: 1.0f, radius: 0.6f, duration: 1.0f);
            L(M, "morv.moisson", "Faux de la Veuve", "Moisson", dmg: 1.5f, radius: 0.5f, echo: 0.5f);
            L(M, "morv.golem", "Cœur de pierre tombale", "Golem d'os", dmg: 2.5f, count: 1);
            L(M, "morv.tempete", "Ouragan d'âmes", "Tempête d'âmes", dmg: 0.6f, count: 8, radius: 0.3f);
            L(M, "morv.vague", "Marée funèbre", "Vague de mort", dmg: 1.4f, radius: 0.8f, echo: 0.6f);

            // ---- Tharion, le Gardien solaire
            L(T, "thar.beni", "Poing de l'Aube", "Marteau béni", dmg: 1.5f, radius: 0.5f, echo: 0.6f);
            L(T, "thar.tournoyant", "Ronde des Marteaux", "Marteau tournoyant", dmg: 0.8f, count: 3);
            L(T, "thar.consecration", "Terre sainte", "Consécration", dmg: 1.2f, radius: 0.8f, duration: 0.6f);
            L(T, "thar.jugement", "Verdict céleste", "Jugement", dmg: 2.0f, radius: 0.6f);
            L(T, "thar.rayon", "Midi éternel", "Rayon solaire", dmg: 1.2f, duration: 0.5f, to: Element.Feu);
            L(T, "thar.colere", "Courroux des Cieux", "Colère divine", dmg: 0.8f, count: 8);
            L(T, "thar.martelement", "Enclume des Dieux", "Martèlement", dmg: 1.0f, radius: 0.4f, echo: 0.8f, stun: true);
            L(T, "thar.chaine", "Chapelet de lumière", "Chaîne sacrée", dmg: 1.0f, count: 4);
            L(T, "thar.bond", "Ailes du Croisé", "Bond du croisé", dmg: 2.0f, radius: 0.8f);
            L(T, "thar.boomerang", "Retour du Juste", "Marteau boomerang", dmg: 1.0f, count: 2, pierce: true);
            L(T, "thar.pluie", "Arsenal céleste", "Pluie de marteaux", dmg: 0.7f, count: 8, radius: 0.3f);
            L(T, "thar.soleil", "Second soleil", "Soleil levant", dmg: 1.6f, radius: 0.7f, echo: 0.6f);

            // ---- ensembles : 2 pièces = caractéristiques, 4 pièces = pouvoir, 6 pièces = pouvoir majeur
            S(K, "set.kael.serment", "Serment du Déchu", "du Serment déchu", new[] { A("Force", 60), A("Vie", 80) },
                new PowerDef { element = Element.Ombre, dmg = 1.0f, radius = 0.25f },
                new PowerDef { element = Element.Ombre, dmg = 2.5f, echo = 0.5f });
            S(K, "set.kael.sang", "Fureur de Sang", "de la Fureur sanglante", new[] { A("Chances de coup critique", 8), A("Vitesse d'attaque", 10) },
                new PowerDef { element = Element.Sang, dmg = 1.0f, cooldown = 0.15f },
                new PowerDef { element = Element.Sang, dmg = 2.5f, cooldown = 0.3f, radius = 0.3f });
            S(K, "set.kael.fer", "Rempart de Fer", "du Rempart de fer", new[] { A("Armure", 120), A("Vie", 120) },
                new PowerDef { element = Element.Physique, dmg = 0.8f, stun = true },
                new PowerDef { element = Element.Physique, dmg = 2.2f, radius = 0.5f, echo = 0.4f });
            S(K, "set.kael.legion", "Légion des Damnés", "de la Légion damnée", new[] { A("Force", 50), A("Réduction des temps de recharge", 8) },
                new PowerDef { kind = SkillKind.Summon, count = 2, dmg = 1.0f },
                new PowerDef { kind = SkillKind.Rain, dmg = 2.0f, count = 6, toElement = Element.Ombre });

            S(E, "set.elyra.eclipse", "Arcanes de l'Éclipse", "des Arcanes de l'Éclipse", new[] { A("Intelligence", 60), A("Régénération de mana", 15) },
                new PowerDef { element = Element.Neant, dmg = 1.0f, radius = 0.25f },
                new PowerDef { element = Element.Neant, dmg = 2.5f, echo = 0.5f });
            S(E, "set.elyra.orage", "Colère de l'Orage", "de l'Orage", new[] { A("Chances de coup critique", 8), A("Intelligence", 40) },
                new PowerDef { element = Element.Foudre, dmg = 1.0f, count = 2 },
                new PowerDef { element = Element.Foudre, dmg = 2.5f, count = 3, cooldown = 0.2f });
            S(E, "set.elyra.hiver", "Hiver d'Auralith", "de l'Hiver d'Auralith", new[] { A("Armure", 80), A("Intelligence", 50) },
                new PowerDef { element = Element.Glace, dmg = 0.8f, stun = true },
                new PowerDef { element = Element.Glace, dmg = 2.2f, radius = 0.6f, duration = 0.5f });
            S(E, "set.elyra.astres", "Astres errants", "des Astres errants", new[] { A("Intelligence", 50), A("Réduction des temps de recharge", 8) },
                new PowerDef { kind = SkillKind.Rain, count = 6, dmg = 0.8f },
                new PowerDef { kind = SkillKind.GroundAoE, dmg = 2.5f, radius = 0.6f, echo = 0.6f });

            S(V, "set.varek.cendres", "Traque des Cendres", "de la Traque", new[] { A("Dextérité", 60), A("Vitesse d'attaque", 10) },
                new PowerDef { element = Element.Cendre, dmg = 1.0f, radius = 0.3f },
                new PowerDef { element = Element.Cendre, dmg = 2.5f, echo = 0.6f });
            S(V, "set.varek.arsenal", "Arsenal du Franc-tireur", "du Franc-tireur", new[] { A("Dextérité", 50), A("Chances de coup critique", 8) },
                new PowerDef { kind = SkillKind.Projectile, count = 2, dmg = 0.8f, pierce = true },
                new PowerDef { kind = SkillKind.Barrage, count = 6, dmg = 2.0f });
            S(V, "set.varek.fievre", "Fièvre noire", "de la Fièvre noire", new[] { A("Dextérité", 50), A("Vie", 80) },
                new PowerDef { element = Element.Poison, dmg = 1.0f, duration = 0.5f },
                new PowerDef { element = Element.Poison, dmg = 2.5f, radius = 0.6f });
            S(V, "set.varek.pieges", "Maître des Pièges", "du Trappeur", new[] { A("Dextérité", 50), A("Réduction des temps de recharge", 8) },
                new PowerDef { kind = SkillKind.Trap, count = 2, dmg = 1.0f },
                new PowerDef { kind = SkillKind.Trap, dmg = 3.0f, radius = 0.6f, toElement = Element.Feu });

            S(M, "set.morv.ossuaire", "Ossuaire de Maldrakor", "de l'Ossuaire", new[] { A("Intelligence", 60), A("Armure", 80) },
                new PowerDef { element = Element.Os, dmg = 1.0f, count = 1 },
                new PowerDef { element = Element.Os, dmg = 2.5f, echo = 0.5f });
            S(M, "set.morv.pacte", "Pacte écarlate", "du Pacte écarlate", new[] { A("Vie", 120), A("Intelligence", 40) },
                new PowerDef { element = Element.Sang, dmg = 1.0f, duration = 0.4f },
                new PowerDef { element = Element.Sang, dmg = 2.5f, radius = 0.5f });
            S(M, "set.morv.tombes", "Seigneur des Tombes", "du Seigneur des tombes", new[] { A("Intelligence", 50), A("Vie", 80) },
                new PowerDef { kind = SkillKind.Summon, count = 2, dmg = 1.0f },
                new PowerDef { kind = SkillKind.Summon, count = 2, dmg = 3.0f, duration = 1.0f });
            S(M, "set.morv.peste", "Peste noire", "de la Peste noire", new[] { A("Intelligence", 50), A("Régénération de mana", 15) },
                new PowerDef { element = Element.Poison, dmg = 1.0f, radius = 0.4f },
                new PowerDef { element = Element.Poison, dmg = 2.5f, duration = 0.8f, echo = 0.4f });

            S(T, "set.thar.aube", "Aube de Seralyth", "de l'Aube", new[] { A("Force", 60), A("Vie", 80) },
                new PowerDef { element = Element.Sacre, dmg = 1.0f, radius = 0.25f },
                new PowerDef { element = Element.Sacre, dmg = 2.5f, echo = 0.5f });
            S(T, "set.thar.rois", "Marteau des Rois", "des Rois", new[] { A("Force", 50), A("Chances de coup critique", 8) },
                new PowerDef { kind = SkillKind.Projectile, count = 2, dmg = 0.8f },
                new PowerDef { kind = SkillKind.Strike, dmg = 2.5f, radius = 0.6f, echo = 0.5f });
            S(T, "set.thar.brasier", "Brasier solaire", "du Brasier solaire", new[] { A("Force", 50), A("Vitesse d'attaque", 10) },
                new PowerDef { element = Element.Feu, dmg = 1.0f, radius = 0.3f },
                new PowerDef { element = Element.Feu, dmg = 2.5f, radius = 0.4f, echo = 0.5f });
            S(T, "set.thar.bastion", "Bastion céleste", "du Bastion céleste", new[] { A("Armure", 150), A("Vie", 150) },
                new PowerDef { kind = SkillKind.Nova, dmg = 1.0f, stun = true },
                new PowerDef { kind = SkillKind.Nova, dmg = 2.5f, radius = 0.6f, cooldown = 0.25f });
        }
    }
}
