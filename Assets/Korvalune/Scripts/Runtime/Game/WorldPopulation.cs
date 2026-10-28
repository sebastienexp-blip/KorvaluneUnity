using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Population du monde ouvert : camps de monstres répartis dans toute la vallée (hors de la ville et de l'eau),
    /// dont le niveau monte à mesure qu'on s'éloigne de la ville. Un camp n'apparaît qu'à l'approche du héros (économie
    /// d'images sur mobile), disparaît quand il s'éloigne, et se repeuple 2 min 30 après avoir été nettoyé.</summary>
    public class WorldPopulation : MonoBehaviour
    {
        class Camp
        {
            public Vector3 center; public float radius; public int level;
            public List<string> ids = new List<string>();
            public readonly List<Health> alive = new List<Health>();
            public bool active; public float respawnAt; public bool everSpawned;
            public readonly List<GameObject> props = new List<GameObject>();
        }

        const float SpawnDist = 70f, DespawnDist = 115f, MinSpawnDist = 26f, Respawn = 150f;
        readonly List<Camp> camps = new List<Camp>();
        GameBootstrap game; float nextTick;
        public int CampCount => camps.Count;

        public void Init(GameBootstrap g, List<GameBootstrap.Camp> fixedCamps, bool generate)
        {
            game = g;
            foreach (var c in fixedCamps)
            {
                if (GameBootstrap.InSafeZone(c.center)) continue;
                var camp = new Camp { center = c.center, radius = c.radius, level = GameBootstrap.LevelAt(c.center) };
                for (int i = 0; i < c.count; i++) camp.ids.Add(c.enemyId);
                camps.Add(camp);
            }
            if (generate) Generate();
            Debug.Log($"[Korvalune] population : {camps.Count} camps (niveaux {MinLevel()} à {MaxLevel()})");
        }

        int MinLevel() { int m = 999; foreach (var c in camps) m = Mathf.Min(m, c.level); return camps.Count > 0 ? m : 0; }
        int MaxLevel() { int m = 0; foreach (var c in camps) m = Mathf.Max(m, c.level); return m; }

        // ------------------------------------------------------------------------------------------ génération
        void Generate()
        {
            var cfg = game.config; if (!cfg || cfg.enemies.Count == 0) return;
            var t = Terrain.activeTerrain; if (!t || !t.terrainData) return;
            var o = t.transform.position; var s = t.terrainData.size;
            var rng = new System.Random(20261028);
            float water = game.waterLevel;
            int wanted = 72, tries = 0;
            while (camps.Count < wanted && tries++ < 6000)
            {
                var p = new Vector3(o.x + 18 + (float)rng.NextDouble() * (s.x - 36), 0, o.z + 18 + (float)rng.NextDouble() * (s.z - 36));
                var flat = p - game.safeCenter; flat.y = 0;
                if (flat.magnitude < game.safeRadius + 24f) continue;                 // ni en ville ni à ses portes
                p = GameBootstrap.Ground(p);
                if (p.y < water + 0.8f || p.y > water + 62f) continue;                // pas dans l'eau ni sur les crêtes
                float slope = 0;
                foreach (var dd in new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back })
                    slope = Mathf.Max(slope, Mathf.Abs(GameBootstrap.Ground(p + dd * 5f).y - p.y));
                if (slope > 3.2f) continue;                                           // terrain praticable
                bool near = false; foreach (var q in game.noCamps) if ((q - p).sqrMagnitude < 30f * 30f) { near = true; break; }
                foreach (var c in camps) if ((c.center - p).sqrMagnitude < 34f * 34f) { near = true; break; }
                if (near) continue;
                var camp = new Camp { center = p, radius = 5f + (float)rng.NextDouble() * 3f, level = GameBootstrap.LevelAt(p) };
                Compose(camp, cfg, rng);
                if (camp.ids.Count == 0) continue;
                camps.Add(camp);
                Props(camp, rng);
            }
        }

        /// <summary>Choisit les monstres d'un camp selon son niveau : bêtes et squelettes près de la ville, araignées, sorciers
        /// et chevaliers plus loin, démons (rares, escortés) dans les terres les plus dangereuses.</summary>
        static void Compose(Camp camp, GameConfig cfg, System.Random rng)
        {
            int L = camp.level;
            var pool = new List<(GameConfig.EnemyType t, float w)>(); float total = 0;
            foreach (var e in cfg.enemies)
            {
                if (!e.prefab || e.level > L + 1) continue;
                float w = 1f / (1f + Mathf.Abs(L - e.level) * 0.35f);
                if (e.id == "demon") w *= 0.35f;
                pool.Add((e, w)); total += w;
            }
            if (pool.Count == 0) return;
            double r = rng.NextDouble() * total; var main = pool[0].t;
            foreach (var (t, w) in pool) { r -= w; if (r <= 0) { main = t; break; } }
            if (main.id == "demon")
            {
                camp.ids.Add("demon");
                var escort = pool.Find(x => x.t.id != "demon").t;
                if (escort != null) { camp.ids.Add(escort.id); camp.ids.Add(escort.id); }
                return;
            }
            int n = main.id == "sorcier" ? 1 + rng.Next(2) : 2 + rng.Next(3);
            for (int i = 0; i < n; i++) camp.ids.Add(main.id);
            if (main.id == "sorcier") { var guard = pool.Find(x => x.t.id == "squelette").t; if (guard != null) { camp.ids.Add("squelette"); camp.ids.Add("squelette"); } }
        }

        /// <summary>Restes de campement autour du camp (tonneaux, caisses, charrette, foin, bannière) : la vallée paraît habitée.</summary>
        void Props(Camp camp, System.Random rng)
        {
            string[] kit = { "M_Tonneaux", "M_Caisses", "M_Foin", "M_Charrette", "M_Banniere" };
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                var pf = Resources.Load<GameObject>("Prefabs/World/Ville/" + kit[rng.Next(kit.Length)]);
                if (!pf) continue;
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                var p = GameBootstrap.Ground(camp.center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (camp.radius + 2.5f));
                var go = Instantiate(pf, p - Vector3.up * 0.05f, Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), transform);
                go.SetActive(false); camp.props.Add(go);
            }
        }

        // ------------------------------------------------------------------------------------------ vie des camps
        void Update()
        {
            if (Time.time < nextTick || !game || !game.Player) return;
            nextTick = Time.time + 0.5f;
            var pp = game.Player.transform.position;
            foreach (var c in camps)
            {
                var d = c.center - pp; d.y = 0; float dist = d.magnitude;
                bool propsOn = dist < 140f;
                foreach (var g in c.props) if (g && g.activeSelf != propsOn) g.SetActive(propsOn);
                if (c.active)
                {
                    c.alive.RemoveAll(h => !h || h.Dead);
                    if (c.alive.Count == 0) { c.active = false; c.respawnAt = Time.time + Respawn; continue; }
                    if (dist > DespawnDist)
                    {   // héros parti : le camp se range (il réapparaîtra intact à son retour)
                        foreach (var h in c.alive) if (h) Destroy(h.gameObject);
                        c.alive.Clear(); c.active = false; c.respawnAt = 0;
                    }
                }
                else if (Time.time >= c.respawnAt && dist < SpawnDist && (dist > MinSpawnDist || !c.everSpawned))
                    SpawnCamp(c);
            }
        }

        /// <summary>Range tous les camps actifs : ils réapparaîtront aussitôt avec la nouvelle difficulté.</summary>
        public void Refresh()
        {
            foreach (var c in camps)
            {
                foreach (var h in c.alive) if (h && !h.Dead) Destroy(h.gameObject);
                c.alive.Clear(); c.active = false; c.respawnAt = 0; c.everSpawned = false;
            }
            nextTick = 0;
        }

        void SpawnCamp(Camp c)
        {
            var cfg = game.config; if (!cfg) return;
            int n = c.ids.Count;
            for (int i = 0; i < n; i++)
            {
                var t = cfg.Enemy(c.ids[i]); if (t == null || !t.prefab) continue;
                float a = 2 * Mathf.PI * i / Mathf.Max(1, n) + Random.Range(-0.3f, 0.3f);
                var p = c.center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * c.radius * Random.Range(0.35f, 1f);
                int lvl = Mathf.Max(1, c.level + (t.id == "demon" ? 2 : Random.Range(0, 2)));
                var e = game.Spawn(t, GameBootstrap.Ground(p), lvl);
                if (e) c.alive.Add(e.GetComponent<Health>());
            }
            c.active = c.alive.Count > 0; c.everSpawned = true;
        }
    }
}
