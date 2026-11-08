using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Créatures nouvelles, dérivées des modèles existants : chacune a son nom, sa taille, ses statistiques et sa
    /// teinte. Enregistrées à l'exécution dans la configuration (pas besoin de nouveaux prefabs) et placées dans les camps
    /// selon leur niveau, comme les autres monstres.</summary>
    public static class MonsterVariants
    {
        public class Def
        {
            public string id, label, baseId; public int levelShift;
            public float scale = 1f, hpMult = 1f, dmgMult = 1f, speedMult = 1f; public Color tint;
        }

        public static readonly List<Def> All = new List<Def>
        {
            new Def { id = "charognard", label = "Charognard des cendres", baseId = "loup", levelShift = 1, scale = 0.85f, hpMult = 0.9f, dmgMult = 1f, speedMult = 1.2f, tint = new Color(0.55f, 0.45f, 0.35f) },
            new Def { id = "rampant", label = "Rampant d'os", baseId = "squelette", levelShift = 2, scale = 1.15f, hpMult = 1.3f, dmgMult = 1.2f, tint = new Color(0.9f, 0.88f, 0.78f) },
            new Def { id = "veilleur", label = "Veilleur des brumes", baseId = "spectre", levelShift = 0, scale = 1f, hpMult = 0.8f, dmgMult = 1.3f, speedMult = 1.1f, tint = new Color(0.5f, 0.8f, 1f) },
            new Def { id = "ombre_rampante", label = "Ombre rampante", baseId = "araignee", levelShift = 3, scale = 0.9f, hpMult = 0.85f, dmgMult = 1.4f, speedMult = 1.3f, tint = new Color(0.25f, 0.08f, 0.3f) },
            new Def { id = "hurleur", label = "Hurleur de l'Astral", baseId = "loup", levelShift = 4, scale = 1.25f, hpMult = 1.6f, dmgMult = 1.25f, tint = new Color(0.6f, 0.35f, 1f) },
            new Def { id = "cendrier", label = "Cendrier brûlant", baseId = "sorcier", levelShift = 4, scale = 1.05f, hpMult = 1.1f, dmgMult = 1.5f, tint = new Color(1f, 0.45f, 0.1f) },
        };

        static readonly Dictionary<string, Color> tints = new Dictionary<string, Color>();

        /// <summary>Ajoute les nouvelles créatures à la configuration (une seule fois, à partir du modèle de base).</summary>
        public static void Register(GameConfig cfg)
        {
            if (!cfg) return;
            foreach (var d in All)
            {
                if (cfg.Enemy(d.id) != null) continue;
                var b = cfg.Enemy(d.baseId); if (b == null || !b.prefab) continue;
                cfg.enemies.Add(new GameConfig.EnemyType
                {
                    id = d.id, label = d.label, prefab = b.prefab,
                    hp = b.hp * d.hpMult, damage = b.damage * d.dmgMult, speed = b.speed * d.speedMult, attackRange = b.attackRange,
                    attackCooldown = b.attackCooldown, aggro = b.aggro, xp = b.xp * 1.1f, scale = b.scale * d.scale, armor = b.armor,
                    hitDelay = b.hitDelay, level = Mathf.Max(1, b.level + d.levelShift)
                });
                tints[d.id] = d.tint;
            }
        }

        /// <summary>Teinte d'une créature nouvelle (blanc pour les autres).</summary>
        public static bool TintOf(string id, out Color c) => tints.TryGetValue(id ?? "", out c);
    }
}
