using System.Collections.Generic;

namespace Korvalune.Game
{
    /// <summary>Animations de compétences propres à chaque héros (clips « Sk_* » générés par ModelForge) :
    /// choix par type de compétence, avec des choix particuliers pour certaines compétences, et instant d'impact de chaque clip.</summary>
    public static class SkillAnims
    {
        // instant (s) où le coup / le sort part, dans chaque clip
        public static readonly Dictionary<string, float> Impact = new Dictionary<string, float>
        {
            {"Sk_Uppercut",0.34f},{"Sk_Spin",0.24f},{"Sk_ShieldBash",0.30f},{"Sk_LeapSlam",0.60f},{"Sk_Thrust",0.30f},{"Sk_WarCry",0.42f},
            {"Sk_Raise",0.26f},{"Sk_Combo",0.26f},{"Sk_Charge",0.12f},{"Sk_GroundStab",0.40f},
            {"Sk_GroundPound",0.48f},{"Sk_PaviseBash",0.32f},{"Sk_Throw",0.38f},{"Sk_Prayer",0.30f},{"Sk_Brace",0.20f},
            {"Sk_SkyShot",0.50f},{"Sk_KneelShot",0.30f},{"Sk_RapidFire",0.18f},{"Sk_BackFlip",0.12f},{"Sk_Trap",0.40f},{"Sk_Kick",0.30f},
            {"Sk_Sweep",0.40f},{"Sk_Whistle",0.75f},{"Sk_Dash",0.12f},
            {"Sk_StaffSlam",0.62f},{"Sk_SkyCall",0.45f},{"Sk_Ward",0.25f},{"Sk_Spiral",0.18f},{"Sk_Blink",0.18f},{"Sk_CastFwd",0.28f},
            {"Sk_Channel",0.25f},{"Sk_Flick",0.18f},{"Sk_Summon",0.75f},
            {"Sk_RaiseDead",0.95f},{"Sk_Curse",0.38f},{"Sk_Drain",0.25f},{"Sk_BoneSpear",0.40f},{"Sk_Embrace",0.45f},{"Sk_Sacrifice",0.30f},
        };
        // instants des coups successifs des enchaînements
        public static readonly Dictionary<string, float[]> Hits = new Dictionary<string, float[]>
        {
            {"Sk_Combo", new[]{0.26f,0.40f,0.54f,0.80f}}, {"Sk_RapidFire", new[]{0.18f,0.36f,0.54f}}, {"Sk_Flick", new[]{0.18f,0.38f}},
            {"Sk_Spin", new[]{0.24f,0.42f,0.60f}}, {"Sk_Sweep", new[]{0.12f,0.26f,0.40f,0.54f}},
        };

        static readonly Dictionary<string, Dictionary<SkillKind, string>> byKind = new Dictionary<string, Dictionary<SkillKind, string>>
        {
            {"KaelDraven", new Dictionary<SkillKind, string>{
                {SkillKind.Strike,"Sk_Uppercut"},{SkillKind.Cleave,"Sk_Spin"},{SkillKind.Whirl,"Sk_Spin"},{SkillKind.Line,"Sk_Thrust"},
                {SkillKind.Projectile,"Sk_Uppercut"},{SkillKind.Barrage,"Sk_Raise"},{SkillKind.Nova,"Sk_GroundStab"},{SkillKind.GroundAoE,"Sk_GroundStab"},
                {SkillKind.Rain,"Sk_Raise"},{SkillKind.Zone,"Sk_GroundStab"},{SkillKind.Totem,"Sk_GroundStab"},{SkillKind.Buff,"Sk_WarCry"},
                {SkillKind.Shield,"Sk_Charge"},{SkillKind.Dash,"Sk_Charge"},{SkillKind.Leap,"Sk_LeapSlam"},{SkillKind.Pull,"Sk_WarCry"},
                {SkillKind.Curse,"Sk_Raise"},{SkillKind.Summon,"Sk_Raise"},{SkillKind.Combo,"Sk_Combo"},{SkillKind.Heal,"Sk_Raise"}}},
            {"Tharion", new Dictionary<SkillKind, string>{
                {SkillKind.Strike,"Sk_Uppercut"},{SkillKind.Cleave,"Sk_Spin"},{SkillKind.Whirl,"Sk_Spin"},{SkillKind.Line,"Sk_GroundPound"},
                {SkillKind.Projectile,"Sk_Throw"},{SkillKind.Nova,"Sk_GroundPound"},{SkillKind.GroundAoE,"Sk_GroundPound"},{SkillKind.Rain,"Sk_Raise"},
                {SkillKind.Zone,"Sk_Prayer"},{SkillKind.Buff,"Sk_Raise"},{SkillKind.Shield,"Sk_Brace"},{SkillKind.Heal,"Sk_Prayer"},
                {SkillKind.Dash,"Sk_Charge"},{SkillKind.Leap,"Sk_LeapSlam"},{SkillKind.Beam,"Sk_Raise"},{SkillKind.Chain,"Sk_Throw"},
                {SkillKind.Curse,"Sk_Raise"},{SkillKind.Summon,"Sk_Prayer"}}},
            {"Varek", new Dictionary<SkillKind, string>{
                {SkillKind.Projectile,"Sk_KneelShot"},{SkillKind.Barrage,"Sk_Sweep"},{SkillKind.Rain,"Sk_SkyShot"},{SkillKind.Trap,"Sk_Trap"},
                {SkillKind.Dash,"Sk_Dash"},{SkillKind.Evade,"Sk_BackFlip"},{SkillKind.GroundAoE,"Sk_Throw"},{SkillKind.Zone,"Sk_Throw"},
                {SkillKind.Curse,"Sk_Whistle"},{SkillKind.Totem,"Sk_Trap"},{SkillKind.Summon,"Sk_Whistle"},{SkillKind.Buff,"Sk_Whistle"},
                {SkillKind.Chain,"Sk_KneelShot"},{SkillKind.Strike,"Sk_Kick"}}},
            {"ElyraNoctis", new Dictionary<SkillKind, string>{
                {SkillKind.Projectile,"Sk_CastFwd"},{SkillKind.Chain,"Sk_Flick"},{SkillKind.Nova,"Sk_StaffSlam"},{SkillKind.Teleport,"Sk_Blink"},
                {SkillKind.Zone,"Sk_StaffSlam"},{SkillKind.Shield,"Sk_Ward"},{SkillKind.GroundAoE,"Sk_SkyCall"},{SkillKind.Beam,"Sk_Channel"},
                {SkillKind.Pull,"Sk_Channel"},{SkillKind.Curse,"Sk_Sweep"},{SkillKind.Rain,"Sk_SkyCall"},{SkillKind.Barrage,"Sk_Flick"},
                {SkillKind.Summon,"Sk_Summon"},{SkillKind.Buff,"Sk_Ward"},{SkillKind.Dash,"Sk_Blink"},{SkillKind.Line,"Sk_Sweep"},
                {SkillKind.Whirl,"Sk_Spiral"}}},
            {"Morvanna", new Dictionary<SkillKind, string>{
                {SkillKind.Projectile,"Sk_BoneSpear"},{SkillKind.Summon,"Sk_RaiseDead"},{SkillKind.GroundAoE,"Sk_Curse"},{SkillKind.Curse,"Sk_Curse"},
                {SkillKind.Beam,"Sk_Drain"},{SkillKind.Nova,"Sk_StaffSlam"},{SkillKind.Shield,"Sk_Ward"},{SkillKind.Barrage,"Sk_CastFwd"},
                {SkillKind.Zone,"Sk_StaffSlam"},{SkillKind.Strike,"Sk_Sweep"},{SkillKind.Cleave,"Sk_Spiral"},{SkillKind.Rain,"Sk_SkyCall"},
                {SkillKind.Buff,"Sk_Embrace"},{SkillKind.Pull,"Sk_Drain"},{SkillKind.Line,"Sk_Sweep"},{SkillKind.Heal,"Sk_Sacrifice"}}},
        };

        static readonly Dictionary<string, string> byName = new Dictionary<string, string>
        {
            {"Coup de bouclier","Sk_ShieldBash"},{"Exécution","Sk_GroundStab"},{"Brise-armure","Sk_Combo"},{"Bond fracassant","Sk_LeapSlam"},
            {"Lame du jugement","Sk_Raise"},{"Écrasement sismique","Sk_LeapSlam"},{"Fissure abyssale","Sk_GroundStab"},{"Moisson d'âmes","Sk_WarCry"},
            {"Coup de pavois","Sk_PaviseBash"},{"Martèlement","Sk_GroundPound"},{"Bénédiction","Sk_Prayer"},{"Châtiment","Sk_GroundPound"},
            {"Supernova","Sk_Raise"},{"Soleil levant","Sk_Raise"},{"Avènement","Sk_Raise"},{"Marteau béni","Sk_Uppercut"},
            {"Volée","Sk_RapidFire"},{"Tir de barrage","Sk_RapidFire"},{"Roulade","Sk_Dash"},{"Tir fatal","Sk_KneelShot"},{"Carreau perçant","Sk_KneelShot"},
            {"Flèche empoisonnée","Sk_RapidFire"},{"Carreau incendiaire","Sk_KneelShot"},{"Grenade de cendres","Sk_Throw"},{"Mines","Sk_Trap"},
            {"Éclipse totale","Sk_SkyCall"},{"Singularité","Sk_Spiral"},{"Nova de givre","Sk_StaffSlam"},{"Hiver éternel","Sk_Spiral"},
            {"Cercle de runes","Sk_Spiral"},{"Tornade de givre","Sk_Sweep"},{"Lame de foudre","Sk_Sweep"},{"Miroir astral","Sk_Ward"},
            {"Nova d'os","Sk_Embrace"},{"Cri de la banshee","Sk_Embrace"},{"Marée de sang","Sk_StaffSlam"},{"Pacte de sang","Sk_Sacrifice"},
            {"Sacrifice de sang","Sk_Sacrifice"},{"Étreinte d'outre-tombe","Sk_Embrace"},{"Seigneur de la mort","Sk_Embrace"},{"Linceul","Sk_Ward"},
            {"Explosion de cadavre","Sk_Curse"},{"Déchirure de l'âme","Sk_SkyCall"},{"Siphon de sang","Sk_Drain"},{"Fossoyeur","Sk_Drain"},
            {"Lame de sang","Sk_Sweep"},{"Faux spectrale","Sk_Spiral"},{"Moisson","Sk_Spiral"},
        };

        public static string Pick(string cls, string name, SkillKind k)
        {
            if (byName.TryGetValue(name, out var a))
            {
                // un nom partagé entre classes (« Lame de sang ») : n'accepter l'animation que si la classe la possède
                if (Owns(cls, a)) return a;
            }
            return byKind.TryGetValue(cls, out var m) && m.TryGetValue(k, out var b) ? b : "";
        }

        static bool Owns(string cls, string anim)
        {
            if (!byKind.TryGetValue(cls, out var m)) return false;
            if (m.ContainsValue(anim)) return true;
            switch (cls)
            {
                case "KaelDraven": return anim == "Sk_ShieldBash" || anim == "Sk_Combo";
                case "Tharion": return anim == "Sk_PaviseBash" || anim == "Sk_GroundPound";
                case "Varek": return anim == "Sk_RapidFire" || anim == "Sk_BackFlip";
                case "ElyraNoctis": return anim == "Sk_Spiral" || anim == "Sk_Sweep";
                case "Morvanna": return anim == "Sk_Embrace" || anim == "Sk_Sacrifice" || anim == "Sk_Spiral" || anim == "Sk_SkyCall";
            }
            return false;
        }
    }
}
