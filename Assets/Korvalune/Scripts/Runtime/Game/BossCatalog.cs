using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Marque d'un boss (repérée par la carte et les quêtes).</summary>
    public class BossMarker : MonoBehaviour { }

    /// <summary>Les cinq boss de Korvalune, tirés de l'imagerie du jeu. Chacun reprend un monstre existant (corps, animations,
    /// IA) agrandi et teinté, puis habillé de formes primitives : couronne, cornes, ailes, cristaux, crânes, halo.
    /// Un boss a beaucoup plus de vie et de dégâts qu'un monstre ordinaire ; à sa mort il lâche un butin de bien meilleure qualité.</summary>
    public static class BossCatalog
    {
        public class Def
        {
            public string id, title, baseEnemy;
            public float scale, hpMult, dmgMult, lootLuck;
            public Color glow;
            public System.Action<GameObject, Color> dress;
        }

        public static readonly List<Def> All = new List<Def>();

        static BossCatalog()
        {
            All.Add(new Def { id = "liche", title = "Roi-liche des Tombes", baseEnemy = "squelette", scale = 1.8f, hpMult = 6f, dmgMult = 1.6f, lootLuck = 2.5f,
                              glow = new Color(0.3f, 0.7f, 1.6f), dress = Liche });
            All.Add(new Def { id = "demon", title = "Seigneur démon de la Fracture", baseEnemy = "demon", scale = 1.9f, hpMult = 7f, dmgMult = 1.7f, lootLuck = 2.8f,
                              glow = new Color(1.6f, 0.35f, 0.1f), dress = Demon });
            All.Add(new Def { id = "golem", title = "Golem de cristal", baseEnemy = "golem", scale = 2.2f, hpMult = 8f, dmgMult = 1.4f, lootLuck = 3f,
                              glow = new Color(0.4f, 0.8f, 1.6f), dress = Golem });
            All.Add(new Def { id = "succube", title = "Succube aux ailes de sang", baseEnemy = "sorcier", scale = 1.7f, hpMult = 5f, dmgMult = 1.8f, lootLuck = 2.5f,
                              glow = new Color(1.5f, 0.15f, 0.25f), dress = Succube });
            All.Add(new Def { id = "araignee", title = "Mère-araignée des Ténèbres", baseEnemy = "araignee", scale = 2.6f, hpMult = 7f, dmgMult = 1.5f, lootLuck = 2.8f,
                              glow = new Color(1.4f, 0.3f, 0.1f), dress = Araignee });
        }

        public static Def Get(string id) { foreach (var d in All) if (d.id == id) return d; return null; }

        /// <summary>Fait apparaître le boss à « pos », à deux niveaux au-dessus du héros.</summary>
        public static GameObject Spawn(GameBootstrap game, Def d, Vector3 pos)
        {
            var cfg = game.config; if (!cfg) return null;
            var t = cfg.Enemy(d.baseEnemy) ?? cfg.Enemy("demon"); if (t == null || !t.prefab) return null;
            var type = new GameConfig.EnemyType
            {
                id = t.id, label = d.title, prefab = t.prefab, hp = t.hp, damage = t.damage, speed = t.speed, attackRange = t.attackRange,
                attackCooldown = t.attackCooldown, aggro = t.aggro, xp = t.xp * 4f, scale = t.scale * d.scale, armor = t.armor,
                hitDelay = t.hitDelay, level = t.level
            };
            int lvl = (game.Player ? game.Player.level : 1) + 2;
            var e = game.Spawn(type, pos, lvl); if (!e) return null;
            e.name = d.title;
            var h = e.GetComponent<Health>(); h.displayName = d.title; h.maxHp = Mathf.Round(h.maxHp * d.hpMult); h.hp = h.maxHp;
            var ai = e.GetComponent<EnemyAI>(); if (ai) ai.damage *= d.dmgMult;
            d.dress(e, d.glow);
            e.AddComponent<BossMarker>();
            h.Died += _ => Reward(game, d, e.transform.position, lvl);
            return e;
        }

        /// <summary>Butin de boss : une dizaine d'objets de haute rareté, beaucoup d'or, des gemmes de qualité supérieure.</summary>
        static void Reward(GameBootstrap game, Def d, Vector3 at, int lvl)
        {
            var rng = new System.Random();
            for (int i = 0; i < 7; i++)
            {
                var it = Loot.Roll(rng, lvl, d.lootLuck);
                var off = Random.insideUnitCircle * 2.4f;
                LootDrop.Spawn(it, LootDrop.Near(at, new Vector3(off.x, 0, off.y)));
            }
            LootDrop.SpawnGold(Economy.GoldDrop(rng, lvl, 1f) * 8, LootDrop.Near(at, Vector3.zero));
            for (int i = 0; i < 3; i++)
            {
                var o = Random.insideUnitCircle * 2f;
                GroundPickup.SpawnGem(Economy.GemKinds[rng.Next(Economy.GemKinds.Length)], 2, LootDrop.Near(at, new Vector3(o.x, 0, o.y)));
            }
            game.Hud?.Notify($"<color=#ffd24a>{d.title} est vaincu ! Butin de boss.</color>");
            Sound.Play("sfx_butin_legendaire", at, 0.9f);
        }

        // ------------------------------------------------------------------------------------------ habillage

        static void Tint(GameObject e, Color mul)
        {
            foreach (var r in e.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * mul);
        }

        static Material Mat(Color c, Color emit)
        {
            var m = LootDrop.litBase ? new Material(LootDrop.litBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            if (emit.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit); }
            return m;
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 sc, Vector3 rot, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = sc; g.transform.localRotation = Quaternion.Euler(rot);
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void Halo(Transform parent, Vector3 lp, Color c, float intensity, float range)
        {
            var go = new GameObject("Halo"); go.transform.SetParent(parent, false); go.transform.localPosition = lp;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }

        // roi-liche : crâne-couronne, crânes spectraux qui tournent autour de lui, halo bleu
        static void Liche(GameObject e, Color g)
        {
            Tint(e, new Color(0.85f, 0.95f, 1.35f));
            var root = e.transform; var bone = Mat(new Color(0.8f, 0.9f, 1f), new Color(0.2f, 0.5f, 1.2f));
            Halo(root, new Vector3(0, 1.6f, 0), g, 4f, 9f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.95f, 1.3f + (i % 2) * 0.5f, Mathf.Sin(a) * 0.95f), Vector3.one * 0.22f, Vector3.zero, bone);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Part(root, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.2f, 2.05f, Mathf.Sin(a) * 0.2f), new Vector3(0.05f, 0.28f, 0.05f), new Vector3(0, a * Mathf.Rad2Deg, 0), bone);
            }
        }

        // seigneur démon : deux cornes recourbées, braises rouges, halo de feu
        static void Demon(GameObject e, Color g)
        {
            Tint(e, new Color(1.1f, 0.55f, 0.45f));
            var root = e.transform; var dark = Mat(new Color(0.12f, 0.04f, 0.03f), new Color(0.6f, 0.05f, 0f));
            Halo(root, new Vector3(0, 1.8f, 0.3f), g, 5f, 10f);
            foreach (var s in new[] { -1f, 1f })
                Part(root, PrimitiveType.Cube, new Vector3(s * 0.22f, 2.1f, 0), new Vector3(0.1f, 0.55f, 0.1f), new Vector3(-25f, 0, -s * 22f), dark);
        }

        // golem de cristal : éclats bleus incrustés dans la roche des épaules et du torse
        static void Golem(GameObject e, Color g)
        {
            Tint(e, new Color(0.8f, 0.9f, 1.2f));
            var root = e.transform; var cr = Mat(new Color(0.4f, 0.8f, 1.2f), new Color(0.2f, 0.7f, 1.6f));
            Halo(root, new Vector3(0, 1.5f, 0), g, 3f, 9f);
            var pts = new[] { (0.7f, 1.6f, 0.2f), (-0.7f, 1.4f, -0.1f), (0.3f, 2.2f, -0.4f), (-0.4f, 2.4f, 0.3f), (0.9f, 1.1f, -0.5f), (-0.9f, 0.9f, 0.5f) };
            int k = 0;
            foreach (var (x, y, z) in pts)
            {
                Part(root, PrimitiveType.Capsule, new Vector3(x, y, z), new Vector3(0.14f, 0.32f, 0.14f), new Vector3(20f * (k % 3 - 1), 0, 25f * (k % 2 == 0 ? 1 : -1)), cr);
                k++;
            }
        }

        // succube aux ailes de sang : deux ailes membraneuses dans le dos, halo rouge
        static void Succube(GameObject e, Color g)
        {
            Tint(e, new Color(1.2f, 0.5f, 0.5f));
            var root = e.transform; var wing = Mat(new Color(0.35f, 0.02f, 0.05f), new Color(0.8f, 0.05f, 0.1f) * 0.3f);
            Halo(root, new Vector3(0, 1.6f, 0), g, 3.5f, 8f);
            Part(root, PrimitiveType.Cube, new Vector3(-0.35f, 1.4f, -0.25f), new Vector3(0.05f, 1.1f, 0.7f), new Vector3(0, 0, 20f), wing);
            Part(root, PrimitiveType.Cube, new Vector3(0.35f, 1.4f, -0.25f), new Vector3(0.05f, 1.1f, 0.7f), new Vector3(0, 0, -20f), wing);
        }

        // mère-araignée : yeux rouges en grappe, halo de braise
        static void Araignee(GameObject e, Color g)
        {
            Tint(e, new Color(1f, 0.6f, 0.5f));
            var root = e.transform; var eye = Mat(new Color(1f, 0.2f, 0.1f), new Color(1.5f, 0.2f, 0.05f));
            Halo(root, new Vector3(0, 0.8f, 0.3f), g, 2.5f, 7f);
            foreach (var x in new[] { -0.12f, -0.04f, 0.04f, 0.12f })
                Part(root, PrimitiveType.Sphere, new Vector3(x, 0.7f, 0.55f), Vector3.one * 0.1f, Vector3.zero, eye);
        }
    }
}
