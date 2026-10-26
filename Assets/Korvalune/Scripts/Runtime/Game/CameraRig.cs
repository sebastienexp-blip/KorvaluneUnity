using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Caméra à la troisième personne, haute et légèrement inclinée (vue ARPG mobile), lissée ; tremblement d'impact.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 7.5f, -8.5f);
        public float lookHeight = 1.4f, smooth = 8f;
        float shake;

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            if (!target) return;
            var want = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, want, 1 - Mathf.Exp(-smooth * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * lookHeight);
            if (shake > 0.001f) { transform.position += Random.insideUnitSphere * shake; shake = Mathf.Lerp(shake, 0, 10 * Time.deltaTime); }
        }

        public void Snap() { if (target) { transform.position = target.position + offset; transform.LookAt(target.position + Vector3.up * lookHeight); } }
    }
}
