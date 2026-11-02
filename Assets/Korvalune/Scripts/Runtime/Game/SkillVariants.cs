using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Variantes de compétence (choix de build) : dès le rang 2, chaque compétence se spécialise dans l'une de deux
    /// voies qui s'excluent. Les voies dépendent du type de compétence (projectile, zone, frappe, invocation…), si bien que
    /// deux héros de la même classe peuvent jouer très différemment. Le choix se change chez soi, gratuitement, dans l'arbre.</summary>
    public static class SkillVariants
    {
        public const int MinRank = 2;
        public struct Variant { public string name, desc; public float dmg, cd, radius, range, mana; public int count; public bool pierce, heal, stun; }

        public static (Variant a, Variant b) For(SkillDef d)
        {
            switch (d.kind)
            {
                case SkillKind.Projectile:
                    return (new Variant { name = "Salve", desc = "+2 projectiles en éventail, −15 % de dégâts chacun", count = 2, dmg = -0.15f },
                            new Variant { name = "Perforante", desc = "Traverse tous les ennemis, +20 % de dégâts", pierce = true, dmg = 0.2f });
                case SkillKind.Barrage:
                    return (new Variant { name = "Déluge", desc = "+4 projectiles, −10 % de dégâts", count = 4, dmg = -0.1f },
                            new Variant { name = "Rafale précise", desc = "+35 % de dégâts, −2 projectiles", count = -2, dmg = 0.35f });
                case SkillKind.Nova: case SkillKind.GroundAoE: case SkillKind.Zone: case SkillKind.Rain:
                    return (new Variant { name = "Étendue", desc = "Zone +40 %, dégâts −10 %", radius = 0.4f, dmg = -0.1f },
                            new Variant { name = "Concentrée", desc = "Dégâts +40 %, zone −20 %", radius = -0.2f, dmg = 0.4f });
                case SkillKind.Strike: case SkillKind.Cleave: case SkillKind.Combo: case SkillKind.Whirl: case SkillKind.Line:
                    return (new Variant { name = "Brutale", desc = "+45 % de dégâts, recharge +20 %", dmg = 0.45f, cd = 0.2f },
                            new Variant { name = "Vampirique", desc = "Rend de la vie à chaque ennemi touché, recharge −15 %", heal = true, cd = -0.15f });
                case SkillKind.Chain: case SkillKind.Beam:
                    return (new Variant { name = "Ramifiée", desc = "+3 rebonds / cibles, −10 % de dégâts", count = 3, dmg = -0.1f },
                            new Variant { name = "Paralysante", desc = "Étourdit brièvement, +10 % de dégâts", stun = true, dmg = 0.1f });
                case SkillKind.Leap: case SkillKind.Dash: case SkillKind.Teleport: case SkillKind.Evade:
                    return (new Variant { name = "Fulgurante", desc = "Recharge −35 %", cd = -0.35f },
                            new Variant { name = "Fracassante", desc = "Onde de choc à l'arrivée : étourdit, +60 % de dégâts", stun = true, dmg = 0.6f });
                case SkillKind.Summon: case SkillKind.Totem: case SkillKind.Trap:
                    return (new Variant { name = "Légion", desc = "+1 invocation / piège", count = 1, dmg = -0.1f },
                            new Variant { name = "Colosse", desc = "+50 % de dégâts et de durée de vie", dmg = 0.5f });
                case SkillKind.Buff: case SkillKind.Shield: case SkillKind.Heal: case SkillKind.Curse:
                    return (new Variant { name = "Prolongée", desc = "Effet et zone +30 %", radius = 0.3f, dmg = 0.15f },
                            new Variant { name = "Économe", desc = "Coût en mana −40 %, recharge −20 %", mana = -0.4f, cd = -0.2f });
                default:
                    return (new Variant { name = "Puissante", desc = "+30 % de dégâts", dmg = 0.3f }, new Variant { name = "Rapide", desc = "Recharge −30 %", cd = -0.3f });
            }
        }

        /// <summary>Applique la voie choisie (0 = aucune, 1 = A, 2 = B) à une copie de la compétence.</summary>
        public static SkillDef Apply(SkillDef d, int choice)
        {
            if (choice <= 0) return d;
            var (a, b) = For(d); var v = choice == 1 ? a : b;
            var c = d.Clone();
            c.mult *= 1 + v.dmg; c.cooldown *= Mathf.Max(0.2f, 1 + v.cd); c.radius *= Mathf.Max(0.3f, 1 + v.radius); c.mana *= Mathf.Max(0.2f, 1 + v.mana);
            c.count = Mathf.Max(1, c.count + v.count);
            if (v.pierce) c.pierce = true; if (v.heal) c.heal = true; if (v.stun) { c.stun = true; if (c.duration <= 0) c.duration = 0.8f; }
            c.empowered = true;
            return c;
        }
    }
}
