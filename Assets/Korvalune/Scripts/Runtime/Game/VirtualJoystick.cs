using UnityEngine;
using UnityEngine.EventSystems;

namespace Korvalune.Game
{
    /// <summary>Joystick tactile (bas gauche) : le pouce se déplace dans un anneau ; Value ∈ disque unité.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public float radius = 110f;
        public Vector2 Value { get; private set; }
        RectTransform rt;

        void Awake() { rt = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) { OnDrag(e); }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var p);
            var v = Vector2.ClampMagnitude(p, radius);
            if (knob) knob.anchoredPosition = v;
            Value = v / radius;
        }

        public void OnPointerUp(PointerEventData e) { Value = Vector2.zero; if (knob) knob.anchoredPosition = Vector2.zero; }
    }
}
