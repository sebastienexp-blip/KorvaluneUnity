using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Korvalune
{
    /// <summary>Point d'entrée de Korvalune (projet repris de zéro).</summary>
    public class Boot : MonoBehaviour
    {
        public ScriptableRendererFeature ssao;
        void Start() { Debug.Log("[Korvalune] Démarrage"); }
    }
}
