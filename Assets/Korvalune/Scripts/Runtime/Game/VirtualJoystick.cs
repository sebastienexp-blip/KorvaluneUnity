using UnityEngine;
using UnityEngine.EventSystems;

namespace Korvalune.Game
{
    /// <summary>Joystick tactile flottant (bas gauche) : toute la zone réagit au pouce ; l'anneau vient se placer sous le
    /// doigt au premier contact, le pouce s'y déplace, puis l'anneau revient à sa place au relâchement. Value ∈ disque unité.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        /// <summary>Anneau du joystick (enfant de la zone tactile) ; sans anneau, la zone elle-même sert de repère.</summary>
        public RectTransform ring;
        public float radius = 110f;
        public Vector2 Value { get; private set; }
        RectTransform rt; Vector3 home; int pointer = int.MinValue;

        void Awake() { rt = (RectTransform)transform; if (ring) home = ring.position; }
        void OnDisable() { Release(); }

        RectTransform Frame => ring ? ring : rt;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;           // un seul doigt pilote le joystick
            pointer = e.pointerId;
            if (ring)
            {
                if (home == Vector3.zero) home = ring.position;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, e.position, e.pressEventCamera, out var w)) ring.position = w;
            }
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Frame, e.position, e.pressEventCamera, out var p);
            p -= Frame.rect.center;
            var v = Vector2.ClampMagnitude(p, radius);
            if (knob) knob.anchoredPosition = v;
            Value = v / radius;
        }

        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) Release(); }

        void Release()
        {
            pointer = int.MinValue; Value = Vector2.zero;
            if (knob) knob.anchoredPosition = Vector2.zero;
            if (ring && home != Vector3.zero) ring.position = home;
        }
    }
}
