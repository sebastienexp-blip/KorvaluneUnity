using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Événements du monde ouvert : toutes les quelques minutes, un événement s'ouvre non loin du héros (repère doré
    /// sur la carte et bandeau à l'écran avec la distance). Trois sortes :
    /// • Assaut des ombres : vagues de monstres à repousser avant la fin du temps ;
    /// • Autels profanés : trois autels à briser, gardés par des monstres ;
    /// • Champion errant : un champion et sa garde rôdent ; l'abattre.
    /// Réussi, il fait apparaître un coffre de récompense (or, objets, expérience). Raté ou abandonné (trop loin), il se referme.</summary>
    public class WorldEvents : MonoBehaviour
    {
        public static WorldEvents Current;
        enum Kind { Assaut, Autels, Champion }
        GameBootstrap game; float nextAt; bool running, started; Kind kind; Vector3 spot; float startAt, endsAt;
        int goal, done; readonly List<Health> spawned = new List<Health>(); readonly List<GameObject> props = new List<GameObject>();
        Text banner; GameObject marker; int wave;
        public Vector3? Spot => running ? spot : (Vector3?)null;

        public static void Begin(GameBootstrap g)
        {
            if (!Application.isPlaying || g.safeRadius <= 0 || !Terrain.activeTerrain || Rift.Active) return;
            var w = g.gameObject.AddComponent<WorldEvents>(); w.game = g; w.nextAt = Time.time + 60f; Current = w;
            Health.AnyDied += w.OnDied;
        }
        void OnDestroy() { Health.AnyDied -= OnDied; if (Current == this) Current = null; }

        string Title => kind switch { Kind.Assaut => "Assaut des ombres", Kind.Autels => "Autels profanés", _ => "Champion errant" };

        void Update()
        {
            if (!game || !game.Player) return;
            var hero = game.Player.transform.position;
            if (!running)
            {
                if (Time.time >= nextAt && !GameBootstrap.InSafeZone(hero)) Open(hero);
                return;
            }
            float dist = Flat(spot - hero).magnitude;
            if (!started && dist < 16f) Start_();
            if (started && kind == Kind.Assaut && Time.time > endsAt) { if (done >= goal) Win(); else Fail("Le temps est écoulé"); return; }
            if (dist > 160f) { Fail("Événement abandonné"); return; }
            if (banner)
            {
                string state = !started ? $"à {Mathf.RoundToInt(dist)} m — rejoins le repère doré"
                    : kind == Kind.Assaut ? $"{done} / {goal} monstres — {Mathf.Max(0, Mathf.CeilToInt(endsAt - Time.time))} s"
                    : kind == Kind.Autels ? $"{done} / {goal} autels brisés" : "abats le champion";
                banner.text = $"<b>{Title}</b>\n{state}";
            }
            if (marker) marker.transform.Rotate(0, 60 * Time.deltaTime, 0);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void Open(Vector3 hero)
        {
            // un lieu praticable à 45-90 m, hors de la ville et de l'eau
            for (int t = 0; t < 30; t++)
            {
                var o = Random.insideUnitCircle.normalized * Random.Range(45f, 90f);
                var p = GameBootstrap.Ground(hero + new Vector3(o.x, 0, o.y));
                if (GameBootstrap.InSafeZone(p) || p.y < GameBootstrap.WaterLevel + 0.5f) continue;
                var t2 = Terrain.activeTerrain; if (t2) { var lp = p - t2.transform.position; var s = t2.terrainData.size; if (lp.x < 20 || lp.z < 20 || lp.x > s.x - 20 || lp.z > s.z - 20) continue; }
                spot = p; break;
            }
            if (spot == Vector3.zero) { nextAt = Time.time + 30f; return; }
            kind = (Kind)Random.Range(0, 3); running = true; started = false; done = 0; wave = 0;
            // repère : colonne de lumière dorée visible de loin
            marker = new GameObject("Événement"); marker.transform.position = spot;
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(col.GetComponent<Collider>()); col.transform.SetParent(marker.transform, false);
            col.transform.localPosition = Vector3.up * 12f; col.transform.localScale = new Vector3(1.6f, 12f, 1.6f);
            if (LootDrop.beamMat) { var m = new Material(LootDrop.beamMat); m.SetColor("_BaseColor", new Color(1f, 0.75f, 0.25f, 0.45f)); col.GetComponent<Renderer>().sharedMaterial = m; }
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(marker.transform, false); l.transform.localPosition = Vector3.up * 2;
            l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.3f); l.range = 12; l.intensity = 2.5f; l.shadows = LightShadows.None;
            BuildBanner();
            game.Hud.Notify($"<color=#ffcc55>Événement : {Title}</color>"); Sound.UI("sfx_quete", 0.8f);
        }

        void Start_()
        {
            started = true; startAt = Time.time;
            int lvl = GameBootstrap.LevelAt(spot);
            switch (kind)
            {
                case Kind.Assaut: goal = 18 + Random.Range(0, 8); endsAt = Time.time + 75f; StartCoroutine(Waves(lvl)); break;
                case Kind.Autels:
                    goal = 3;
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI * 2 / 3; var p = GameBootstrap.Ground(spot + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7f);
                        var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); altar.name = "Autel profané"; altar.transform.position = p + Vector3.up * 0.6f; altar.transform.localScale = new Vector3(1.1f, 0.6f, 1.1f);
                        if (LootDrop.litBase) { var m = new Material(LootDrop.litBase); m.SetColor("_BaseColor", new Color(0.25f, 0.2f, 0.22f)); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.8f, 0.1f, 0.15f)); altar.GetComponent<Renderer>().sharedMaterial = m; }
                        var h = altar.AddComponent<Health>(); h.faction = Faction.Enemy; h.maxHp = h.hp = 120 + 40 * lvl; h.displayName = "Autel profané"; h.level = lvl;
                        spawned.Add(h); props.Add(altar);
                        Guards(p, lvl, 2);
                    }
                    break;
                default:
                    goal = 1;
                    var t = game.config.Enemy(lvl >= 10 ? "chevalier" : lvl >= 5 ? "sorcier" : "loup") ?? game.config.enemies[0];
                    var e = game.Spawn(t, spot, lvl + 1); var hh = e.GetComponent<Health>();
                    hh.maxHp *= 4; hh.hp = hh.maxHp; hh.displayName = "Champion errant"; e.transform.localScale *= 1.35f;
                    var ai = e.GetComponent<EnemyAI>(); if (ai) { ai.champion = true; ai.damage = (ai.damage > 0 ? ai.damage : t.damage) * 1.5f; }
                    spawned.Add(hh); Guards(spot, lvl, 4);
                    break;
            }
        }

        System.Collections.IEnumerator Waves(int lvl)
        {
            while (running && started && Time.time < endsAt && done < goal)
            {
                wave++;
                for (int i = 0; i < 5 + wave; i++)
                {
                    var o = Random.insideUnitCircle.normalized * Random.Range(9f, 14f);
                    var pool = game.config.enemies; var t = pool[Random.Range(0, pool.Count)];
                    if (t.id == "demon" && lvl < 10) t = game.config.Enemy("squelette") ?? t;
                    var e = game.Spawn(t, GameBootstrap.Ground(spot + new Vector3(o.x, 0, o.y)), lvl);
                    if (e) { spawned.Add(e.GetComponent<Health>()); Vfx.Shock(e.transform.position, new Color(0.5f, 0.2f, 0.7f), 1.8f); }
                }
                yield return new WaitForSeconds(16f);
            }
        }

        void Guards(Vector3 at, int lvl, int n)
        {
            var t = game.config.Enemy(lvl >= 7 ? "chevalier" : lvl >= 4 ? "araignee" : "squelette") ?? game.config.enemies[0];
            for (int i = 0; i < n; i++) { var o = Random.insideUnitCircle * 3f; var e = game.Spawn(t, GameBootstrap.Ground(at + new Vector3(o.x, 0, o.y)), lvl); if (e) spawned.Add(e.GetComponent<Health>()); }
        }

        void OnDied(Health h)
        {
            if (!running || !started || !h || !spawned.Contains(h)) return;
            switch (kind)
            {
                case Kind.Assaut: done++; if (done >= goal) Win(); break;
                case Kind.Autels: if (h.displayName == "Autel profané") { done++; Vfx.Explosion(h.transform.position, Element.Feu, 2f); Destroy(h.gameObject, 0.2f); if (done >= goal) Win(); } break;
                default: if (h.displayName == "Champion errant") Win(); break;
            }
        }

        void Win()
        {
            if (!running) return;
            int lvl = Mathf.Max(game.Player.level, GameBootstrap.LevelAt(spot));
            var pf = Resources.Load<GameObject>("Prefabs/World/Ville/S_Coffre");
            var go = pf ? Instantiate(pf) : new GameObject("Coffre de l'événement"); go.name = "Coffre de l'événement";
            go.transform.position = spot; go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            var chest = go.AddComponent<TreasureChest>(); chest.luck = 4f;
            float xp = Mathf.Round((120 + 30 * lvl) * Difficulty.Xp); game.Player.GainXp(xp);
            game.Hud.Notify($"<color=#ffcc55>Événement réussi : {Title} ! +{xp} XP — un coffre est apparu</color>");
            Sound.Play("sfx_quete", spot, 1f);
            Close(true);
        }

        void Fail(string why) { game.Hud.Notify(why); Close(false); }

        void Close(bool won)
        {
            running = false; started = false; StopAllCoroutines();
            if (marker) Destroy(marker); marker = null;
            foreach (var p in props) if (p) Destroy(p); props.Clear();
            if (!won) foreach (var h in spawned) if (h && !h.Dead && h.displayName == "Autel profané") Destroy(h.gameObject);
            spawned.Clear();
            if (banner) Destroy(banner.transform.parent.gameObject); banner = null;
            nextAt = Time.time + Random.Range(150f, 240f);
        }

        void BuildBanner()
        {
            var canvas = game.Hud.Canvas; if (!canvas) return;
            var box = new GameObject("Événement", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); box.rectTransform.SetParent(canvas.transform, false);
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(1, 1); box.rectTransform.pivot = new Vector2(1, 1);
            box.rectTransform.anchoredPosition = new Vector2(-30, -570); box.rectTransform.sizeDelta = new Vector2(380, 92);
            box.sprite = UIStyle.Panel(); box.type = Image.Type.Sliced; box.color = new Color(1f, 0.85f, 0.55f, 0.95f); box.raycastTarget = false;
            banner = new GameObject("T", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); banner.rectTransform.SetParent(box.rectTransform, false);
            banner.rectTransform.anchorMin = Vector2.zero; banner.rectTransform.anchorMax = Vector2.one; banner.rectTransform.offsetMin = new Vector2(18, 8); banner.rectTransform.offsetMax = new Vector2(-18, -8);
            banner.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); banner.fontSize = 22; banner.color = new Color(1f, 0.9f, 0.6f); banner.alignment = TextAnchor.MiddleLeft; banner.raycastTarget = false;
            banner.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }
    }
}
