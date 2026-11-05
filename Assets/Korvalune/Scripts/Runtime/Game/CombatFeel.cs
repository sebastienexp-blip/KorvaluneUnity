using System.Collections;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Sensations de combat : arrêt sur image très bref quand un coup porte (« hit-stop », plus marqué sur un
    /// critique) et recul des monstres frappés au corps à corps. Rend chaque coup lisible et percutant.</summary>
    public class CombatFeel : MonoBehaviour
    {
        static CombatFeel inst;
        static CombatFeel I
        {
            get
            {
                if (!inst) { var go = new GameObject("Sensations de combat"); go.hideFlags = HideFlags.HideInHierarchy; inst = go.AddComponent<CombatFeel>(); }
                return inst;
            }
        }
        float lastStop = -1;
        const float StopScale = 0.06f;

        /// <summary>Arrêt sur image (secondes réelles). Sans effet pendant une pause, une cinématique ou s'il vient d'y en avoir un.</summary>
        public static void HitStop(float seconds)
        {
            if (!Application.isPlaying || Time.timeScale < 0.99f || Cinematic.Active || Settings.HitStop == 1) return;
            var i = I; if (Time.unscaledTime - i.lastStop < 0.14f) return;
            i.lastStop = Time.unscaledTime; i.StartCoroutine(i.Stop(seconds));
        }
        IEnumerator Stop(float s)
        {
            Time.timeScale = StopScale;
            yield return new WaitForSecondsRealtime(s);
            if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;      // la pause a pu passer par là
        }

        /// <summary>Recul d'un monstre touché (les gros monstres et les boss reculent moins).</summary>
        public static void Knock(Health h, Vector3 from, float dist)
        {
            if (!Application.isPlaying || !h || h.Dead || h.faction == Faction.Player) return;
            var d = h.transform.position - from; d.y = 0; if (d.sqrMagnitude < 0.0001f) return;
            float size = Mathf.Max(0.6f, h.transform.lossyScale.y);
            I.StartCoroutine(I.Push(h, d.normalized * dist / size));
        }
        IEnumerator Push(Health h, Vector3 v)
        {
            var cc = h ? h.GetComponent<CharacterController>() : null;
            for (float t = 0; t < 0.14f && h && !h.Dead; t += Time.deltaTime)
            {
                var step = v * (Time.deltaTime / 0.14f) * (1.6f - 1.2f * t / 0.14f);    // départ vif, fin amortie
                if (cc && cc.enabled) cc.Move(step); else h.transform.position += step;
                yield return null;
            }
        }
    }
}
