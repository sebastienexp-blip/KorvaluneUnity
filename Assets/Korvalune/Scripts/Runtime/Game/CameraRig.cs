using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Caméra façon Diablo 4 : plongée haute (≈ 55°) et lointaine, orientation fixe (le nord en haut de l'écran),
    /// suivi lissé avec léger amorti, zoom limité (molette / pincement à deux doigts), tremblement d'impact.</summary>
    [DefaultExecutionOrder(-100)]   // la caméra se place avant que l'interface ne projette noms et barres de vie
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public float pitch = 56f, distance = 17f, fov = 38f, yaw = 0f;
        public float minZoom = 0.65f, maxZoom = 1.15f, zoom = 1f;
        public float lookHeight = 1.0f, smooth = 7f;
        float shake;
        Camera cam;

        public void Shake(float amount) { if (Settings.Shake == 0) shake = Mathf.Max(shake, amount); }

        Vector3 Offset => Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance * zoom);

        void LateUpdate()
        {
            if (!target) return;
            if (!cam) cam = GetComponent<Camera>();
            if (cam) cam.fieldOfView = fov;
            float wheel = Input.mouseScrollDelta.y;
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float prev = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude, now = (a.position - b.position).magnitude;
                wheel = (now - prev) * 0.01f;
            }
            zoom = Mathf.Clamp(zoom - wheel * 0.08f, minZoom, maxZoom);
            var focus = target.position + Vector3.up * lookHeight;
            var want = focus + Offset;
            transform.position = Vector3.Lerp(transform.position, want, 1 - Mathf.Exp(-smooth * Time.deltaTime));
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (shake > 0.001f) { transform.position += Random.insideUnitSphere * shake; shake = Mathf.Lerp(shake, 0, 10 * Time.deltaTime); }
        }

        public void Snap()
        {
            if (!target) return;
            if (!cam) cam = GetComponent<Camera>();
            if (cam) cam.fieldOfView = fov;
            transform.position = target.position + Vector3.up * lookHeight + Offset; transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
