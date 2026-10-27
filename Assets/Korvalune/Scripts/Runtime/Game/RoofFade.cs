using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Toit d'échoppe ou de tente : il disparaît quand le héros s'approche (on voit le marchand et l'étal depuis la
    /// caméra plongeante) et réapparaît quand il s'éloigne.</summary>
    public class RoofFade : MonoBehaviour
    {
        public float radius = 7f;
        Renderer[] rs; bool hidden; Transform player;

        void Start() { rs = GetComponentsInChildren<Renderer>(); }

        void Update()
        {
            if (!player) { var p = FindFirstObjectByType<PlayerController>(); if (!p) return; player = p.transform; }
            var d = player.position - transform.position; d.y = 0;
            bool want = d.sqrMagnitude < radius * radius;
            if (want == hidden) return;
            hidden = want;
            foreach (var r in rs) if (r) r.shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
