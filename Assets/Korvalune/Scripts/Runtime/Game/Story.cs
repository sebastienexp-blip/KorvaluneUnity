using System.Collections.Generic;

namespace Korvalune.Game
{
    /// <summary>Le mode histoire : douze chapitres principaux (un par région, dans l'ordre du voyage), quêtes secondaires
    /// et quêtes tutoriels. Les objectifs des chapitres sont ceux de la scène de la région (lieux et ennemis posés à la
    /// construction) ; secondaires et tutoriels avancent sur des évènements du jeu (QuestEvents) ou des ennemis tués.
    /// Chaque chapitre s'ouvre et se ferme sur une cinématique jouée dans le moteur (Cinematic).</summary>
    public static class Story
    {
        public enum Kind { Principale, Secondaire, Tutoriel }

        public class Obj
        {
            public string text;
            public string evt;              // évènement (QuestEvents) à compter
            public string enemyId;          // ou ennemi à abattre ("*" : n'importe lequel)
            public string scene;            // limité à cette scène (null : partout)
            public int count = 1;
        }

        public class Def
        {
            public string id, title, desc, scene, chiefName;
            public Kind kind;
            public int level = 1;           // niveau conseillé
            public string requires;         // quête à terminer avant
            public float xpShare = 0.3f;    // récompense : part de l'expérience du niveau en cours
            public long gold;
            public List<Obj> objs = new List<Obj>();
            public string[] intro, outro;   // sous-titres des cinématiques (chapitres)
        }

        public static readonly List<Def> All = new List<Def>();
        static readonly Dictionary<string, Def> byId = new Dictionary<string, Def>();
        public static Def Get(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;
        /// <summary>Chapitre principal de la scène, s'il y en a un.</summary>
        public static Def ChapterOf(string scene) { foreach (var d in All) if (d.kind == Kind.Principale && d.scene == scene) return d; return null; }
        public static int ChapterNumber(Def d) { int n = 0; foreach (var x in All) { if (x.kind == Kind.Principale) n++; if (x == d) return n; } return 0; }

        static Def Add(Def d) { All.Add(d); byId[d.id] = d; return d; }
        static Obj E(string text, string evt, int n = 1, string scene = null) => new Obj { text = text, evt = evt, count = n, scene = scene };
        static Obj K(string text, string enemy, int n, string scene = null) => new Obj { text = text, enemyId = enemy, count = n, scene = scene };

        static Story()
        {
            // ======================================================================================== histoire principale
            string prev = null;
            void Chap(string id, string scene, string title, int lvl, string chief, string desc, string[] intro, string[] outro)
            {
                Add(new Def { id = id, kind = Kind.Principale, scene = scene, title = title, level = lvl, requires = prev, chiefName = chief, desc = desc,
                              intro = intro, outro = outro, xpShare = 0.9f, gold = 150 + lvl * 40 });
                prev = id;
            }
            Chap("ch01", "Korvalune_Vallee", "Purifier la vallée", 1, "le sorcier corrompu",
                "Depuis la nuit où la lune s'est fendue, les morts du vieux cimetière se relèvent et les loups rôdent jusqu'aux portes de la ville. Repoussez-les, puis rejoignez le château de Korvalune.",
                new[] { "Il y a sept nuits, la lune de Korvalune s'est fendue au-dessus de la vallée.", "Depuis, les morts du vieux cimetière ne dorment plus… et quelque chose les appelle.",
                        "On murmure qu'un sorcier, au vieux pont, parle aux tombes.", "Toi seul es revenu de l'Éclipse les yeux ouverts. Lève-toi, Éveillé." },
                new[] { "Les portes du château s'ouvrent devant toi.", "L'archiviste du seigneur t'attendait : « Le mal ne vient pas des tombes… il monte d'en dessous. »",
                        "« Sous le cimetière dort la vieille crypte. C'est là qu'il faut descendre. »" });
            Chap("ch02", "Korvalune_Crypte", "Purger la crypte", 3, "le gardien de la crypte",
                "Sous le vieux cimetière, la crypte des anciens seigneurs grouille de morts-vivants. Un chevalier maudit et un gardien de chair et de cendre veillent sur ce qui a réveillé les tombes.",
                new[] { "La crypte n'a pas été ouverte depuis trois siècles.", "Pourtant l'air y brûle encore… et les dalles portent des marques fraîches.",
                        "Quelque chose, tout au fond, garde un fragment de lune." },
                new[] { "Sur l'autel du gardien repose un éclat de lune, encore chaud.", "Il pulse… et pointe vers l'ouest, vers les vieux bois de l'Astral.",
                        "Ce n'est pas un éclat. C'est une clé." });
            Chap("ch03", "Korvalune_Foret", "Les bois de l'Astral", 8, "le chevalier noir des menhirs",
                "Les menhirs astraux de la forêt se sont rallumés. Suivez le chemin à travers les bois, chassez ce qui les garde et libérez le hameau en ruine où mène l'éclat.",
                new[] { "Les anciens dressaient ces menhirs pour écouter le chant des étoiles.", "Ils chantent de nouveau. Et ce chant attire les bêtes.",
                        "Au bout du chemin, un hameau abandonné… et un chevalier qui n'a plus de visage." },
                new[] { "L'éclat se pose sur le menhir central, qui s'embrase de lumière bleue.", "Une vision : une cité de cristal, au bord d'un golfe, sous une lune noire.",
                        "La Cité de l'Éclipse. Le Refuge Lunaire t'appelle." });
            Chap("ch04", "Korvalune_Eclipse", "Le Refuge Lunaire", 14, "le démon du nord",
                "La Cité de l'Éclipse, dernier refuge des veilleurs de la lune. Gagnez le Cristal de l'Éclipse et brisez l'assaut des chevaliers maudits et du démon des Hautes-Terres.",
                new[] { "La Cité de l'Éclipse veille depuis mille ans sur le Cristal.", "Cette nuit, ses murs tremblent : les chevaliers maudits sont descendus des Hautes-Terres.",
                        "Derrière eux marche un démon… et derrière le démon, quelque chose regarde." },
                new[] { "Le Cristal parle enfin : « Sept sceaux retenaient le Néant hors du monde. »", "« Quand la lune s'est fendue, ils ont cédé un à un. Chaque terre lointaine garde le sien. »",
                        "« Referme-les, Éveillé, avant que l'Œil ne s'ouvre tout à fait. » La pierre de voyage s'illumine." });
            Chap("ch05", "Korvalune_Desert", "Sous les étoiles mortes", 25, "le démon des dunes",
                "Le premier sceau dort sous le temple des étoiles tombées, au cœur du Désert des Étoiles. Traversez les dunes et abattez le démon qui tient le hameau.",
                new[] { "Ici sont tombées les étoiles de la première guerre.", "Le sable a tout recouvert… sauf le temple, et le sceau qu'il garde.",
                        "Un démon s'y est installé. Il boit la lumière du sceau." },
                new[] { "Le premier sceau se referme dans un grondement de sable.", "Il en reste six. Au nord, au-dessus des nuages, les cols sélénites t'attendent." });
            Chap("ch06", "Korvalune_Pics", "Les cols sélénites", 45, "le démon des cimes",
                "Le sceau du ciel est gravé sur les tours de guet des Pics Sélénites. Franchissez les cols enneigés et reprenez le hameau des guetteurs.",
                new[] { "Les guetteurs sélénites surveillaient le ciel depuis ces tours.", "Ils ont vu la lune se fendre. Ils n'ont pas eu le temps de prévenir qui que ce soit.",
                        "Leur sceau vacille encore, là-haut, au bout des cols." },
                new[] { "Le vent tombe. Le sceau du ciel brille de nouveau sur la tour.", "Loin à l'ouest, sur la Côte des Naufrages, une cloche sonne sous la mer." });
            Chap("ch07", "Korvalune_Cote", "Les épaves de la Mer des Tempêtes", 65, "l'Abbé noyé",
                "Sur la Côte des Naufrages, un abbé englouti prêche aux noyés. Son monastère est tombé dans la mer avec le sceau des marées. Arrachez-le-lui.",
                new[] { "Le monastère de la côte a sombré en une seule nuit, avec ses moines et sa cloche.", "Leur abbé, lui, n'a jamais cessé de prêcher.",
                        "Il porte le sceau des marées autour du cou… et une pince à la place du bras." },
                new[] { "La crosse de l'abbé se brise sur les rochers. La cloche sous la mer se tait.", "Le sceau des marées est refermé. La brume monte des marais de Brumenoire." });
            Chap("ch08", "Korvalune_Marais", "Ce qui dort sous la vase", 85, "la Tisseuse des brumes",
                "Dans les marais de Brumenoire, une tisseuse perchée sur ses échasses enferme des âmes dans ses cages. Le sceau des brumes est pris dans sa toile.",
                new[] { "Les feux follets de Brumenoire ne sont pas des feux.", "Ce sont des âmes, enfermées dans des cages rouillées.",
                        "Celle qui les garde tisse la brume… et le sceau est pris dans sa toile." },
                new[] { "Les cages s'ouvrent. Les feux follets montent vers le ciel et s'éteignent en paix.", "Le sceau des brumes est refermé. Au nord, le givre a pris le cœur d'une forêt entière." });
            Chap("ch09", "Korvalune_Toundra", "Le cœur de givre", 105, "le Wendigo des glaces",
                "Sur la Toundra de Givrecœur, une faim sans fin marche entre les arbres gelés. Le wendigo a dévoré le gardien du sceau du givre… et le sceau avec lui.",
                new[] { "Il y avait un village ici. Il y avait des chasseurs.", "Ils ont mangé, l'hiver de la Fracture, ce qu'ils n'auraient jamais dû manger.",
                        "Ce qu'ils sont devenus a faim. Toujours." },
                new[] { "Le wendigo s'effondre et son crâne se fend. Dans sa cage thoracique, le sceau du givre luit, intact.",
                        "Plus que trois. Au sud, la terre fume : les forges de Cendrefer se sont rallumées." });
            Chap("ch10", "Korvalune_Volcan", "Les forges de Cendrefer", 125, "la Salamandre-forge",
                "Dans les Terres de Cendrefer, une salamandre cuirassée de fer porte les forges sur son dos. Le sceau de cendre est scellé sous ses plaques.",
                new[] { "Les forgerons de Cendrefer ont voulu enchaîner le feu de la terre.", "Ils ont riveté leurs forges sur le dos d'une salamandre géante.",
                        "Le feu a gagné. Et la bête porte maintenant le sceau de cendre." },
                new[] { "Les cheminées s'éteignent une à une. Le sceau de cendre refroidit dans ta main.", "Le Canyon Écarlate saigne : la pierre elle-même s'est mise à vivre." });
            Chap("ch11", "Korvalune_Canyon", "Le sang de la pierre", 150, "le Basilic écarlate",
                "Dans les gorges du Canyon Écarlate, le basilic pétrifie tout ce qui croise son regard d'ambre. Le sceau du sang est enfoui dans son repaire.",
                new[] { "Les caravanes ne traversent plus le canyon.", "On retrouve leurs marchands debout, changés en grès rouge, la bouche ouverte.",
                        "Ne croise pas son regard. Frappe avant qu'il ne se cabre." },
                new[] { "Le basilic s'abat dans la poussière rouge. Le sixième sceau est refermé.", "Il n'en reste qu'un. Au bord du monde, l'Œil du Néant s'est ouvert." });
            Chap("ch12", "Korvalune_Abime", "Au bord du Néant", 175, "le Voyant du Néant",
                "Le bord du monde. Les îles flottent au-dessus du vide et le Voyant du Néant, un œil immense, regarde à travers la Fracture. Refermez le dernier sceau.",
                new[] { "Ici finit le monde. Au-delà, il n'y a que le Néant.", "C'est par cet œil qu'il nous regarde depuis la nuit de la Fracture.",
                        "Referme le dernier sceau, Éveillé. Et ferme-lui les yeux." },
                new[] { "L'Œil se ferme. Le silence tombe sur l'Abîme.", "Au-dessus de Korvalune, la lune fendue se ressoude, lentement, comme une plaie qui guérit.",
                        "Les sept sceaux tiennent. Pour combien de temps… nul ne le sait. Mais cette nuit, la vallée dort en paix." });
            Chap("ch13", "Korvalune_Catacombes", "Les catacombes de verre", 185, "le Golem de cristal",
                "Sous Korvalune, des catacombes de verre noir descendent jusqu'à la racine de la Fracture. Un golem de cristal garde le cœur qui chante sous la vallée.",
                new[] { "Les anciens n'ont pas enterré leurs morts : ils les ont scellés dans le verre.", "Chaque corps porte une lueur violette, et toutes répètent le même nom.",
                        "Au fond, le golem veille sur un cœur de verre qui bat encore." },
                new[] { "Le golem se fend comme une vitre. Le cœur s'éteint, et le chant se tait.", "Le silence revient sous Korvalune. Pour la première fois, le sol est sûr." });

            // ======================================================================================== quêtes secondaires
            void Side(string id, string title, int lvl, string desc, string req, float xp, params Obj[] objs)
            { var d = Add(new Def { id = id, kind = Kind.Secondaire, title = title, level = lvl, desc = desc, requires = req, xpShare = xp, gold = 60 + lvl * 25 }); d.objs.AddRange(objs); }
            Side("s_vallee_tonneaux", "Le cellier du tavernier", 1, "Des rats nichent dans les vieux tonneaux et les caisses de la vallée. Brisez-en assez pour les chasser (et récupérez ce qui traîne dedans).", null, 0.35f,
                 E("Briser des tonneaux, caisses ou jarres", "casser", 12));
            Side("s_vallee_villageois", "Les rumeurs de la ville", 1, "Les villageois savent des choses. Écoutez ce qu'ils ont à dire sur la nuit de la Fracture.", null, 0.3f,
                 E("Parler aux villageois", "parler_villageois", 3));
            Side("s_vallee_loups", "La chasse du garde-chasse", 2, "Les loups des Ténèbres attaquent les troupeaux. Le garde-chasse paie pour chaque meute dispersée.", null, 0.45f,
                 K("Abattre des loups des Ténèbres", "loup", 12));
            Side("s_coffres", "Chercheur de trésors", 3, "Les ruines du pays regorgent de coffres oubliés. Ouvrez-en quelques-uns.", "ch01", 0.5f,
                 E("Ouvrir des coffres au trésor", "coffre", 3));
            Side("s_breche", "La statue des Brèches", 5, "La statue de la ville ouvre des passages vers des brèches mouvantes. Entrez-y et refermez-en une.", "ch02", 0.6f,
                 E("Terminer une brèche", "breche", 1));
            Side("s_monture", "Un fidèle compagnon", 8, "Le maître d'écurie vend des montures. Une monture vous fera gagner un temps précieux entre les régions.", "ch02", 0.4f,
                 E("Monter en selle", "monture", 1));
            Side("s_foret_araignees", "Toiles dans les bois", 8, "Les araignées géantes ont tendu leurs toiles entre les menhirs de l'Astral.", "ch02", 0.5f,
                 K("Abattre des araignées géantes", "araignee", 15, "Korvalune_Foret"));
            Side("s_eclipse_chevaliers", "La garde du Refuge", 14, "Les chevaliers maudits harcèlent encore les remparts de la Cité de l'Éclipse.", "ch03", 0.55f,
                 K("Abattre des chevaliers maudits", "chevalier", 12, "Korvalune_Eclipse"));
            Side("s_desert_demons", "Les démons des dunes", 25, "Les démons des dunes chassent les caravanes du Désert des Étoiles.", "ch04", 0.55f,
                 K("Abattre des démons", "demon", 10, "Korvalune_Desert"));
            Side("s_pics_sorciers", "Les voix dans la neige", 45, "Des sorciers corrompus chantent dans les cols sélénites.", "ch05", 0.55f,
                 K("Abattre des sorciers corrompus", "sorcier", 12, "Korvalune_Pics"));
            Side("s_cote_abbes", "La procession des noyés", 65, "D'autres abbés noyés remontent des épaves. Brisez leur procession.", "ch06", 0.6f,
                 K("Abattre des abbés noyés", "abbe", 8, "Korvalune_Cote"));
            Side("s_marais_tisseuses", "Libérer les feux follets", 85, "D'autres tisseuses gardent des cages dans la vase de Brumenoire.", "ch07", 0.6f,
                 K("Abattre des tisseuses des brumes", "tisseuse", 8, "Korvalune_Marais"));
            Side("s_toundra_wendigos", "La faim de l'hiver", 105, "Les wendigos chassent en meute sur la toundra.", "ch08", 0.6f,
                 K("Abattre des wendigos des glaces", "wendigo", 8, "Korvalune_Toundra"));
            Side("s_volcan_salamandres", "Éteindre les forges", 125, "D'autres salamandres-forges crachent leur feu dans les Terres de Cendrefer.", "ch09", 0.6f,
                 K("Abattre des salamandres-forges", "salamandre", 6, "Korvalune_Volcan"));
            Side("s_canyon_basilics", "Les gorges pétrifiées", 150, "La couvée du basilic rôde dans les gorges.", "ch10", 0.6f,
                 K("Abattre des basilics écarlates", "basilic", 6, "Korvalune_Canyon"));
            Side("s_abime_voyants", "Fermer les yeux du vide", 175, "Des voyants plus petits flottent au-dessus de l'Abîme.", "ch11", 0.6f,
                 K("Abattre des voyants du Néant", "voyant", 6, "Korvalune_Abime"));
            Side("s_grande_breche", "Plus profond dans la Brèche", 20, "La statue propose des Grandes Brèches de plus en plus profondes. Terminez-en une.", "s_breche", 0.8f,
                 E("Terminer une Grande Brèche", "grande_breche", 1));

            // ======================================================================================== tutoriels
            void Tuto(string id, string title, string desc, string req, Obj o) { var d = Add(new Def { id = id, kind = Kind.Tutoriel, title = title, desc = desc, requires = req, xpShare = 0.12f, gold = 25 }); d.objs.Add(o); }
            Tuto("t_bouger", "Premiers pas", "Le joystick (en bas à gauche) déplace votre héros. Au clavier : touches de direction ou ZQSD.", null, E("Marcher une vingtaine de mètres", "marcher", 20));
            Tuto("t_attaque", "Frapper", "Le gros bouton en bas à droite lance votre attaque de base (barre d'espace au clavier).", "t_bouger", E("Attaquer 5 fois", "attaque", 5));
            Tuto("t_competence", "Les compétences", "Les boutons ronds autour de l'attaque lancent vos compétences (touches 1 à 5). Elles coûtent du mana et se rechargent.", "t_attaque", E("Utiliser une compétence", "competence", 1));
            Tuto("t_butin", "Le butin", "Les ennemis laissent de l'or et des objets : marchez dessus pour les ramasser.", "t_attaque", E("Ramasser un objet", "butin", 1));
            Tuto("t_sac", "L'inventaire", "Le bouton du sac (en haut) ouvre l'inventaire : équipement, caractéristiques et objets.", "t_butin", E("Ouvrir l'inventaire", "sac", 1));
            Tuto("t_equiper", "S'équiper", "Dans l'inventaire, touchez un objet puis « Équiper » pour le porter.", "t_sac", E("Équiper un objet", "equiper", 1));
            Tuto("t_potion", "Les potions", "Les fioles rouge (vie) et bleue (mana) se boivent d'un toucher, avec un temps de recharge.", "t_attaque", E("Boire une potion", "potion", 1));
            Tuto("t_arbre", "L'arbre de compétences", "Chaque niveau donne un point de compétence : le bouton du livre (en haut) ouvre l'arbre.", "t_competence", E("Ouvrir l'arbre de compétences", "arbre", 1));
            Tuto("t_utiliser", "Le bouton « Utiliser »", "Quand quelque chose est à portée (marchand, coffre, pierre, portail), le bouton « Utiliser » apparaît au-dessus des compétences.", "t_bouger", E("Utiliser quelque chose", "utiliser", 1));
            Tuto("t_casser", "Tout se brise", "Tonneaux, caisses et jarres se brisent d'un coup et cachent parfois de l'or.", "t_attaque", E("Briser un objet", "casser", 1));
            Tuto("t_marchand", "Les marchands", "Forgeron, armurier, alchimiste… parlez-leur pour acheter, vendre ou améliorer.", "t_utiliser", E("Parler à un marchand", "marchand", 1));
            Tuto("t_carte", "La carte", "Touchez la mini-carte (en haut à droite) ou le bouton de la carte pour voir toute la région.", "t_bouger", E("Ouvrir la carte", "carte", 1));
            Tuto("t_journal", "Le journal de quêtes", "Le bouton du parchemin (en haut) ouvre le journal : histoire, quêtes secondaires et tutoriels, avec leurs récompenses.", "t_bouger", E("Ouvrir le journal de quêtes", "journal", 1));
            Tuto("t_pierre", "La pierre de voyage", "La pierre de voyage de la ville mène à toutes les régions déjà accessibles à votre niveau.", "ch01", E("Utiliser une pierre de voyage", "pierre", 1));
            Tuto("t_portail", "Le portail de ville", "Le bouton du portail violet ouvre un passage vers la ville, et un autre vous ramène où vous étiez.", "ch01", E("Ouvrir un portail de ville", "portail_ville", 1));
        }
    }
}
