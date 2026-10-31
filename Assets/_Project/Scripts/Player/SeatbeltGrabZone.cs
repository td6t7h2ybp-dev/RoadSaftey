using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoadSafety.UI;

namespace RoadSafety.Player
{
    /// <summary>
    /// An invisible touch zone that follows the seatbelt's metal tongue on screen, so the child
    /// can grab the real 3D belt with a finger (or the mouse) and pull it into the buckle.
    /// Uses the UI event system, so it works with touch, mouse, and both input systems,
    /// and never adds a collider to the car.
    /// </summary>
    public class SeatbeltGrabZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float ZoneSize = 240f; // reference pixels (canvas 1920x1080)

        private CarInterior interior;
        private RectTransform rect;
        private RectTransform canvasRect;
        private Canvas canvas;
        private Vector2 downPosition;
        private float downTime;
        private bool moved;
        private int pointerId = int.MinValue;

        internal static SeatbeltGrabZone Create(CarInterior owner, HUDController hud)
        {
            Canvas target = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (target == null)
            {
                var go = new GameObject("SeatbeltCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                target = go.GetComponent<Canvas>();
                target.renderMode = RenderMode.ScreenSpaceOverlay;
                target.sortingOrder = 11;
            }
            target = target.rootCanvas;

            var zoneGo = new GameObject("SeatbeltGrabZone", typeof(RectTransform), typeof(Image));
            zoneGo.transform.SetParent(target.transform, false);
            zoneGo.transform.SetAsLastSibling();
            var image = zoneGo.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f); // invisible, still catches the touch
            image.raycastTarget = true;
            var zone = zoneGo.AddComponent<SeatbeltGrabZone>();
            zone.interior = owner;
            zone.canvas = target;
            zone.canvasRect = (RectTransform)target.transform;
            zone.rect = (RectTransform)zoneGo.transform;
            zone.rect.anchorMin = zone.rect.anchorMax = new Vector2(0.5f, 0.5f);
            zone.rect.sizeDelta = new Vector2(ZoneSize, ZoneSize);
            return zone;
        }

        private Camera EventCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (interior == null || cam == null)
                return;
            if (pointerId != int.MinValue)
                return; // keep the zone still under the finger while dragging
            Vector3 screen = cam.WorldToScreenPoint(interior.TongueWorld);
            bool visible = screen.z > 0f;
            rect.localScale = visible ? Vector3.one : Vector3.zero;
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, EventCamera, out Vector2 local))
                rect.anchoredPosition = local;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue)
                return;
            pointerId = eventData.pointerId;
            downPosition = eventData.position;
            downTime = Time.unscaledTime;
            moved = false;
            rect.sizeDelta = new Vector2(ZoneSize * 6f, ZoneSize * 6f); // keep receiving the drag anywhere around
            interior.BeginGrab();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId)
                return;
            if ((eventData.position - downPosition).magnitude > Screen.height * 0.03f)
                moved = true;
            interior.Drag(eventData.position, Camera.main);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId)
                return;
            pointerId = int.MinValue;
            rect.sizeDelta = new Vector2(ZoneSize, ZoneSize);
            bool tap = !moved && Time.unscaledTime - downTime < 0.6f;
            interior.EndGrab(tap);
        }

        private void OnDisable()
        {
            if (pointerId != int.MinValue)
            {
                pointerId = int.MinValue;
                if (rect != null) rect.sizeDelta = new Vector2(ZoneSize, ZoneSize);
                if (interior != null) interior.EndGrab(false);
            }
        }
    }
}
