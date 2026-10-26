using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Lumière de flamme vacillante (braseros, torches) : intensité et position légèrement bruitées.</summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.25f, speed = 9f;
        Light l; float baseI; Vector3 basePos; float seed;
        void Start() { l = GetComponent<Light>(); baseI = l.intensity; basePos = transform.localPosition; seed = Random.value * 100; }
        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            l.intensity = baseI * (1 + n * amount * 2);
            transform.localPosition = basePos + new Vector3(Mathf.PerlinNoise(seed + 3, Time.time * 4) - 0.5f, 0, Mathf.PerlinNoise(seed + 7, Time.time * 4) - 0.5f) * 0.04f;
        }
    }
}
