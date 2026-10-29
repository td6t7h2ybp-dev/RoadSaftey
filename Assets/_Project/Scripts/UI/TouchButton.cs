using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoadSafety.UI
{
    /// <summary>
    /// A big touch button for small fingers. Unlike the standard uGUI Button it reports
    /// press AND release, so it works for "hold to drive" buttons (Go, Stop, arrows),
    /// and it keeps the press even if the finger slides a bit off the button.
    /// Several TouchButtons can be held at the same time (multi-touch: Go + an arrow).
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("The image that shrinks while pressed (a child, so the touch zone never changes size).")]
        [SerializeField] private RectTransform visual;

        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.88f;
        [SerializeField, Min(1f)] private float animationSpeed = 18f;

        private int pointersDown;

        /// <summary>First finger down.</summary>
        public event Action Pressed;

        /// <summary>Last finger up.</summary>
        public event Action Released;

        /// <summary>true on press, false on release (handy for hold buttons).</summary>
        public event Action<bool> HeldChanged;

        public bool IsHeld => pointersDown > 0;

        public RectTransform Visual
        {
            get => visual;
            set => visual = value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointersDown++;
            if (pointersDown == 1)
            {
                Pressed?.Invoke();
                HeldChanged?.Invoke(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointersDown == 0)
                return;
            pointersDown--;
            if (pointersDown == 0)
                ReleaseNow();
        }

        /// <summary>Forces the button up (used when the app loses focus or the button is hidden).</summary>
        public void ForceRelease()
        {
            if (pointersDown == 0)
                return;
            pointersDown = 0;
            ReleaseNow();
        }

        private void ReleaseNow()
        {
            Released?.Invoke();
            HeldChanged?.Invoke(false);
        }

        private void OnDisable() => ForceRelease();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ForceRelease();
        }

        private void Update()
        {
            if (visual == null)
                return;
            float target = IsHeld ? pressedScale : 1f;
            float current = visual.localScale.x;
            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime));
            visual.localScale = new Vector3(next, next, 1f);
        }
    }
}
