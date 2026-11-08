using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Portail magique ovale et vertical : anneau de particules violettes qui tourbillonnent, voile lumineux qui
    /// ondule au centre, éclats aspirés vers le cœur, lueur au sol.</summary>
    public static class PortalGate
    {
        public static void Build(Transform parent, Color c)
        {
            var root = new GameObject("Portail").transform; root.SetParent(parent, false); root.localPosition = Vector3.up * 1.55f;
            var add = FX.additive; if (!add) return;
            // voile central
            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad); veil.name = "Voile"; Object.Destroy(veil.GetComponent<Collider>()); veil.transform.SetParent(root, false);
            veil.transform.localScale = new Vector3(2.0f, 3.0f, 1); var vm = new Material(add); vm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.75f)); veil.GetComponent<Renderer>().sharedMaterial = vm;
            var back = Object.Instantiate(veil, root); back.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var core = Object.Instantiate(veil, root); core.transform.localScale = new Vector3(1.1f, 1.8f, 1); var cm = new Material(add); cm.SetColor("_BaseColor", new Color(1f, 0.85f, 1f, 0.6f)); core.GetComponent<Renderer>().sharedMaterial = cm;
            root.gameObject.AddComponent<GateWobble>().veil = veil.transform;
            // anneau tourbillonnant
            var ps = new GameObject("Anneau").AddComponent<ParticleSystem>(); ps.transform.SetParent(root, false); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.startLifetime = 0.9f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.30f); m.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.5f)); m.maxParticles = 400; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 220;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.0f; sh.radiusThickness = 0.08f; sh.scale = new Vector3(1f, 1.5f, 1f); sh.rotation = new Vector3(0, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.orbitalZ = 2.6f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); col.color = g;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = add; ps.Play();
            // éclats aspirés vers le centre
            var suck = new GameObject("Aspiration").AddComponent<ParticleSystem>(); suck.transform.SetParent(root, false); suck.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = suck.main; m2.loop = true; m2.startLifetime = 0.8f; m2.startSpeed = -1.6f; m2.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f); m2.startColor = Color.Lerp(c, Color.white, 0.6f); m2.simulationSpace = ParticleSystemSimulationSpace.Local;
            var e2 = suck.emission; e2.rateOverTime = 40; var s2 = suck.shape; s2.shapeType = ParticleSystemShapeType.Circle; s2.radius = 1.3f; s2.scale = new Vector3(1f, 1.5f, 1f);
            suck.GetComponent<ParticleSystemRenderer>().sharedMaterial = add; suck.Play();
            var l = new GameObject("Lueur du portail").AddComponent<Light>(); l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(0, -0.3f, 0.8f);
            l.type = LightType.Point; l.color = c; l.intensity = 2.8f; l.range = 8f; l.shadows = LightShadows.None;
        }
    }

    public class GateWobble : MonoBehaviour
    {
        public Transform veil; Vector3 baseScale;
        void Start() { if (veil) baseScale = veil.localScale; }
        void Update()
        {
            var cam = Camera.main;
            if (cam) { var d = cam.transform.position - transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-d); }   // toujours face à la caméra
            if (veil) veil.localScale = new Vector3(baseScale.x * (1 + 0.04f * Mathf.Sin(Time.time * 3f)), baseScale.y * (1 + 0.03f * Mathf.Sin(Time.time * 2.3f + 1)), 1);
        }
    }

    /// <summary>Portail de ville (violet) : canalisé quelques instants, il ouvre un passage vers la ville ; en ville, un
    /// second portail ramène exactement là où l'on était. Indisponible dans les brèches.</summary>
    public static class TownPortal
    {
        public static readonly Color Violet = new Color(0.68f, 0.32f, 1f);
        public const string Town = "Korvalune_Vallee";
        static string returnScene; static Vector3 returnPos;
        public static bool HasReturn => !string.IsNullOrEmpty(returnScene);
        static bool casting; static bool riftReturn; static int riftTier;

        public static void Cast()
        {
            var game = GameBootstrap.Instance; var p = game ? game.Player : null; if (!p || p.Health.Dead || casting) return;
            if (Rift.Active && Rift.Tier > 0) { p.Notify?.Invoke("La Grande Brèche retient votre âme : impossible d'ouvrir un portail"); return; }   // brèche astrale normale : portail permis, la brèche reste ouverte
            if (SceneManager.GetActiveScene().name == Town && GameBootstrap.InSafeZone(p.transform.position)) { p.Notify?.Invoke("Vous êtes déjà en ville"); return; }
            game.StartCoroutine(Channel(game, p)); QuestEvents.Raise("portail_ville");
        }

        static IEnumerator Channel(GameBootstrap game, PlayerController p)
        {
            casting = true;
            var start = p.transform.position; float hp = p.Health.hp;
            Sound.Play("sfx_portail", start, 0.7f);
            for (float t = 0; t < 1.6f; t += 0.2f)
            {
                FX.Ring(p.transform.position, Violet, 1.4f, 0.4f);
                yield return new WaitForSeconds(0.2f);
                if (!p || p.Health.Dead || (p.transform.position - start).sqrMagnitude > 0.5f || p.Health.hp < hp - 0.5f)
                { casting = false; p?.Notify?.Invoke("Portail interrompu"); yield break; }
            }
            casting = false;
            var here = SceneManager.GetActiveScene().name; var at = p.transform.position;
            bool inRift = Rift.Active; int tier = Rift.Tier;
            Open(LootDrop.Near(at, p.transform.forward * 2.2f), "Portail de ville", () => { returnScene = here; returnPos = at; riftReturn = inRift; riftTier = tier; GameBootstrap.nextSpawn = null; SceneManager.LoadScene(Town); });
        }

        static Portal Open(Vector3 pos, string name, System.Action enter)
        {
            var g = new GameObject(name).AddComponent<Portal>(); g.transform.position = pos;
            g.gate = true; g.glowColor = Violet; g.displayName = name; g.label = name; g.targetScene = Town; g.onEnter = enter;
            Sound.Play("sfx_teleport", pos, 0.8f);
            return g;
        }

        /// <summary>En ville : portail de retour vers l'endroit quitté (s'il y en a un).</summary>
        public static void PlaceReturn(GameBootstrap game)
        {
            if (!HasReturn || SceneManager.GetActiveScene().name != Town) return;
            var scene = returnScene; var pos = returnPos;
            Open(GameBootstrap.Ground(game.playerSpawn + new Vector3(2.5f, 0, 2.5f)), "Retour au combat", () =>
            {
                returnScene = null; GameBootstrap.nextSpawn = pos;
                // brèche astrale quittée par le portail : elle est rouverte telle quelle au retour (même endroit)
                if (riftReturn && Rift.IsRiftScene(scene)) { Rift.Active = true; Rift.Tier = riftTier; }
                riftReturn = false;
                SceneManager.LoadScene(scene);
            });
        }

        public static void Clear() { returnScene = null; riftReturn = false; }
    }
}
