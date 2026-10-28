using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Objet qui flotte : lente rotation et léger balancement vertical (Cristal de l'Éclipse, éclats célestes).</summary>
    public class Floater : MonoBehaviour
    {
        public float spin = 12f, bob = 0.8f, period = 6f;
        Vector3 basePos; float phase;
        void Start() { basePos = transform.position; phase = Random.value * 10f; }
        void Update()
        {
            transform.Rotate(0, spin * Time.deltaTime, 0, Space.World);
            transform.position = basePos + Vector3.up * Mathf.Sin((Time.time + phase) * Mathf.PI * 2f / period) * bob;
        }
    }
}
