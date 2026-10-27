using System.Collections.Generic;
using System.Text;

namespace Korvalune
{
    /// <summary>Noms français des options du créateur de personnage (coiffures, yeux, peaux, curseurs de morphologie) à la
    /// place des identifiants techniques des packs MakeHuman, et filtre des options hors univers (tenues modernes, science-fiction,
    /// contenu adulte).</summary>
    public static class CreatorNames
    {
        static readonly Dictionary<string, string> Hair = new Dictionary<string, string>
        {
            { "afro01", "Afro" }, { "bob01", "Carré court" }, { "bob02", "Carré mi-long" }, { "braid01", "Longue tresse" },
            { "cortu_shaggy_green_hair", "Mèches sauvages" }, { "cortu_short_messy_hair", "Courts en bataille" }, { "cortu_straight_bangs", "Frange droite" },
            { "cortu_strawberry_cloud_hair", "Boucles vaporeuses" }, { "culturalibre_hair_01", "Mi-longs ondulés" }, { "culturalibre_hair_02", "Longs et lisses" },
            { "culturalibre_hair_05", "Crinière" }, { "culturalibre_hair_06", "Mèche de côté" }, { "elvs_double_mh_braid", "Double tresse" },
            { "elvs_french_braid_variation", "Tresse de cour" }, { "elvs_reverse_french_braid_bun", "Tresse en chignon" }, { "elvs_unkempt_french_braid", "Tresse défaite" },
            { "faydaen_hair_1", "Longs dégradés" }, { "learning_anime_hair", "Mèches en pointes" }, { "littleright_bobcut_hair", "Coupe au bol" },
            { "long01", "Longs" }, { "o4saken_long01", "Longs de rôdeur" }, { "ponytail01", "Queue de cheval" }, { "rehmanpolanski_hair_bun_brown", "Chignon" },
            { "short01", "Courts" }, { "short02", "Courts plaqués" }, { "short03", "Courts du soldat" }, { "short04", "Ras" },
            { "sonntag78_blond_with_headband", "Bandeau de guerrière" }, { "sonntag78_junglebook_hair", "Hirsutes" }, { "toigo_blunt_bob", "Carré net" },
            { "toigo_blunt_bob_with_bangs", "Carré net à frange" }, { "toigo_curled_under_bob", "Carré bombé" }, { "toigo_curled_under_bob_with_bangs", "Carré bombé à frange" },
            { "toigo_inverted_bob", "Carré plongeant" }, { "toigo_inverted_bob_with_bangs", "Carré plongeant à frange" },
        };

        static readonly Dictionary<string, string> Eyes = new Dictionary<string, string>
        {
            { "blue", "Bleus" }, { "bluegreen", "Bleu-vert" }, { "brown", "Bruns" }, { "brownlight", "Noisette" }, { "deepblue", "Bleu nuit" }, { "green", "Verts" },
            { "grey", "Gris" }, { "ice", "Glace" }, { "lightblue", "Bleu clair" }, { "bobby_03_daz_dragon_eyes", "Œil de dragon" }, { "culturalibre_6_eyes", "Six pupilles" },
            { "culturalibre_hero_white_eyes", "Blancs du héros" }, { "culturalibre_hero_yellow_eyes", "Or du héros" }, { "culturalibre_zombie_eyes", "Morts-vivants" },
            { "gpedroso_blank_eye", "Aveugles" }, { "hatshj_alien_eye_2", "Abyssaux II" }, { "hatshj_alien_eyes", "Abyssaux" }, { "hatshj_cross_eye", "Croix" },
            { "hatshj_facette_eye", "À facettes" }, { "hatshj_facette_eye_2", "À facettes II" }, { "hatshj_reptile_eyes", "Reptile" }, { "nyloseth_bluecateyes", "Félins bleus" },
            { "nyloseth_brown_cat_eyes", "Félins bruns" }, { "nyloseth_green_cat_eyes", "Félins verts" }, { "nyloseth_sapphire_cat_eyes", "Félins saphir" },
            { "nyloseth_yellow_cat_eyes", "Félins dorés" }, { "sonntag78_orcifyhead_eye", "Orque" }, { "titleknown_trans_eyes", "Translucides" },
            { "callharvey3d_harvey_eye1", "Ambre" }, { "callharvey3d_harvey_eye2", "Ambre sombre" }, { "culturalibre_snake_eyes", "Serpent" }, { "culturalibre_target_eyes", "Cible" },
            { "elvs_anime_eye_1", "Grands yeux" }, { "elvs_bads_hippy-cat_eye", "Chat sauvage" }, { "elvs_bionic_eye_red", "Braise" }, { "elvs_blurple_eye", "Améthyste" },
            { "elvs_catyellow_rainbow_eye", "Prisme doré" }, { "elvs_devilian_eye1", "Démoniaques" }, { "elvs_earth_moon_eyes", "Terre et lune" }, { "elvs_fruitcake_eyes", "Mosaïque" },
            { "elvs_fushia_rainbow_eye", "Prisme fuchsia" }, { "elvs_midnight_rainbow_eyes", "Prisme de minuit" }, { "elvs_rainbow_hueshift_eyes", "Arc-en-ciel" },
            { "elvs_santa_helpers_eyes", "Givre féerique" }, { "elvs_saphire_rainbow_eyes", "Prisme saphir" }, { "elvs_sunspot_eyes", "Tache solaire" },
            { "elvs_zfashion_eye1", "Rubis" }, { "mtknife_violet_eyes", "Violets" },
        };

        static readonly string[] HiddenSkinWords = { "genitals", "suit", "uniform", "manhattan", "alien", "baboon", "hairless" };

        /// <summary>Peau présentable dans un univers médiéval-fantastique (ni tenue moderne, ni contenu adulte).</summary>
        public static bool SkinAllowed(string id, string[] tags)
        {
            foreach (var w in HiddenSkinWords) if (id.Contains(w)) return false;
            if (tags != null) foreach (var t in tags) if (t == "adult") return false;
            return true;
        }

        public static string HairName(string id) => Hair.TryGetValue(id, out var n) ? n : Clean(id);
        public static string EyeName(string id) => Eyes.TryGetValue(id, out var n) ? n : Clean(id);

        public static string SkinName(string id)
        {
            var w = new List<string>(id.Split('_'));
            string age = w.Contains("young") ? "jeune" : w.Contains("middleage") || w.Contains("middle") ? "d'âge mûr" : w.Contains("old") ? "âgée" : "";
            string tone = id.Contains("african") ? "Peau ébène" : id.Contains("asian") ? "Peau dorée" : id.Contains("indian") ? "Peau cuivrée"
                : id.Contains("eurasian") ? "Peau mate" : id.Contains("bronze") ? "Peau hâlée" : id.Contains("ginger") || id.Contains("red_head") ? "Peau de rousse"
                : id.Contains("freckles") ? "Peau tachetée" : id.Contains("elf") ? "Peau elfique" : id.Contains("zombie") ? "Peau de mort-vivant"
                : id.Contains("tigra") ? "Peau tigrée" : id.Contains("leopard") ? "Peau léopard" : id.Contains("white_walker") ? "Peau de spectre des glaces"
                : id.Contains("orki") ? "Peau d'orque" : id.Contains("grey_skinned") ? "Peau grise" : id.Contains("tattoo") ? "Peau tatouée"
                : id.Contains("viking") ? "Peau de nordique tatouée" : id.Contains("slavic") ? "Peau de nordique barbu" : id.Contains("makeup") || id.Contains("eyeliner") ? "Peau fardée"
                : id.Contains("goth") || id.Contains("emo") ? "Peau fardée de noir" : id.Contains("caucasian") || id.Contains("light") ? "Peau claire" : "Peau";
            if (id.Contains("violet_makeup")) tone = "Peau fardée de violet";
            return age.Length > 0 ? $"{tone}, {age}" : tone;
        }

        static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        {
            { "black", "noir" }, { "brown", "brun" }, { "gold", "doré" }, { "green", "vert" }, { "pink", "rose" }, { "platinum", "platine" }, { "red", "roux" },
            { "violet", "violet" }, { "gray", "gris" }, { "grey", "gris" }, { "blond", "blond" }, { "ginger", "roux" }, { "copper", "cuivré" }, { "honey", "miel" },
            { "pumpkin", "citrouille" }, { "tangerine", "mandarine" }, { "retro", "rétro" }, { "dark", "foncé" }, { "light", "clair" }, { "blood", "sang" }, { "lime", "citron" },
            { "flat", "mat" }, { "gothic", "gothique" }, { "new", "neuf" }, { "old", "vieil" }, { "candy", "bonbon" }, { "fushia", "fuchsia" }, { "rustbrown", "rouille" },
            { "deep", "profond" }, { "mermaid", "sirène" }, { "cherrybomb", "cerise" }, { "oilslickster", "irisé" },
        };

        /// <summary>Nom d'une variante de couleur de coiffure (« ponytail01_platinum » → « Platine »).</summary>
        public static string VariantName(string id, int index)
        {
            var parts = id.Split('_'); var sb = new List<string>();
            foreach (var p in parts) if (Colors.TryGetValue(p, out var c)) sb.Add(c);
            if (sb.Count == 0) return $"Variante {index + 1}";
            var s = string.Join(" ", sb);
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        static readonly Dictionary<string, string> Morph = new Dictionary<string, string>
        {
            { "bulge", "renflement" }, { "tone", "fermeté" }, { "angles", "commissures" }, { "cupidsbow", "arc de Cupidon" }, { "cupidsbow width", "largeur de l'arc" },
            { "dimples", "fossettes" }, { "laugh lines", "plis du sourire" }, { "lowerlip ext", "lèvre inférieure (bords)" }, { "lowerlip middle", "lèvre inférieure (centre)" },
            { "lowerlip volume", "volume lèvre inférieure" }, { "lowerlip width", "largeur lèvre inférieure" }, { "philtrum volume", "philtrum" }, { "scale depth", "profondeur" },
            { "scale horiz", "largeur" }, { "upperlip ext", "lèvre supérieure (bords)" }, { "upperlip middle", "lèvre supérieure (centre)" }, { "upperlip volume", "volume lèvre supérieure" },
            { "upperlip width", "largeur lèvre supérieure" }, { "back scale depth", "nuque" }, { "double", "double menton" }, { "volume", "volume" }, { "nubian", "bombé" },
            { "temple", "tempes" }, { "waist", "taille" }, { "bones", "pommettes" }, { "inner", "creux" }, { "hand fingers correction", "doigts" }, { "hand hypothenar", "tranche de la main" },
            { "hand thenar eminence", "base du pouce" }, { "knobby knuckles", "jointures noueuses" }, { "foot scale depth", "longueur des pieds" }, { "foot scale horiz", "largeur des pieds" },
            { "hand fingers diameter", "épaisseur des doigts" }, { "hand fingers distance", "écart des doigts" }, { "leg valgus", "jambes en X" }, { "lowerarm fat", "avant-bras (gras)" },
            { "lowerarm muscle", "avant-bras (muscle)" }, { "lowerleg fat", "mollets (gras)" }, { "lowerleg muscle", "mollets (muscle)" }, { "upperarm fat", "bras (gras)" },
            { "upperarm muscle", "biceps" }, { "upperarm shoulder muscle", "épaules" }, { "upperleg fat", "cuisses (gras)" }, { "upperleg muscle", "cuisses (muscle)" },
            { "bust circ", "tour de poitrine" }, { "hips circ", "tour de hanches" }, { "neck circ", "tour de cou" }, { "underbust circ", "sous la poitrine" }, { "waist circ", "tour de taille" },
            { "cleft", "fossette" }, { "jaw drop", "mâchoire basse" }, { "prognathism", "prognathisme" }, { "prominent", "saillant" }, { "triangle", "en pointe" }, { "width", "largeur" },
            { "base", "base" }, { "compression", "pincement" }, { "curve", "courbure" }, { "flaring", "ailes évasées" }, { "greek", "nez grec" }, { "hump", "bosse" },
            { "nostrils angle", "angle des narines" }, { "nostrils width", "largeur des narines" }, { "point", "pointe" }, { "point width", "largeur de la pointe" },
            { "septumangle", "angle de la cloison" }, { "width1", "largeur (racine)" }, { "width2", "largeur (milieu)" }, { "width3", "largeur (bas)" }, { "ear flap", "lobe décollé" },
            { "ear lobe", "lobe" }, { "ear rot", "inclinaison" }, { "ear scale", "taille" }, { "ear scale depth", "profondeur" }, { "ear shape pointed", "pointues (elfe)" },
            { "ear shape round", "rondes" }, { "ear shape square", "carrées" }, { "ear shape triangle", "triangulaires" }, { "ear wing", "décollées" }, { "angle", "angle" },
            { "muscle dorsi", "dorsaux" }, { "muscle pectoral", "pectoraux" }, { "vshape", "carrure en V" }, { "navel", "nombril" }, { "pregnant", "ventre arrondi" },
            { "eye bag", "cernes" }, { "eye corner1", "coin intérieur" }, { "eye corner2", "coin extérieur" }, { "eye epicanthus", "pli de l'épicanthus" }, { "eye eyefold", "paupière" },
            { "eye eyefold angle", "angle de la paupière" }, { "eye height1", "hauteur (intérieur)" }, { "eye height2", "hauteur (milieu)" }, { "eye height3", "hauteur (extérieur)" },
            { "eye push1", "enfoncement" }, { "eye push2", "saillie" }, { "eye scale", "taille des yeux" },
            { "head angle", "inclinaison" }, { "head back scale depth", "arrière du crâne" }, { "head diamond", "forme losange" }, { "head fat", "visage plein" },
            { "head invertedtriangular", "triangle inversé" }, { "head oval", "ovale" }, { "head rectangular", "rectangulaire" }, { "head round", "rond" },
            { "head scale depth", "profondeur" }, { "head scale horiz", "largeur" }, { "head square", "carré" }, { "head triangular", "triangulaire" },
        };

        /// <summary>Libellé français d'un curseur (« Nez : hump » → « bosse »).</summary>
        public static string MorphLabel(string label)
        {
            var part = label.Contains(":") ? label.Split(':')[1].Trim() : label;
            if (Morph.TryGetValue(part, out var f)) part = f;
            return part.Length > 0 ? char.ToUpper(part[0]) + part.Substring(1) : part;
        }

        static string Clean(string id)
        {
            var parts = id.Split('_'); var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++) { if (i == 0 && parts.Length > 2) continue; if (sb.Length > 0) sb.Append(' '); sb.Append(parts[i]); }
            var s = sb.ToString(); return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : id;
        }
    }
}
