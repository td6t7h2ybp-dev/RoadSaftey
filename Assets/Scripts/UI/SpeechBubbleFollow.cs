using UnityEngine;
using UnityEngine.UI;
using RoadSafety.Player;

namespace RoadSafety.UI
{
    /// <summary>
    /// Keeps the friend's speech bubble next to the friend's head on screen, like a comic:
    /// the bubble sits on the screen-centre side of the head, a little above it, with a pointer
    /// aimed at the mouth. It stays inside the screen. If the friend can't be seen (or isn't there)
    /// it falls back to the top centre of the screen.
    /// </summary>
    public class SpeechBubbleFollow : MonoBehaviour
    {
        private RectTransform bubble, tail;
        private Canvas canvas;
        private Vector2 smooth;
        private bool placed;

        public static void Attach(RectTransform bubble)
        {
            if (bubble == null || bubble.GetComponent<SpeechBubbleFollow>() != null) return;
            var f = bubble.gameObject.AddComponent<SpeechBubbleFollow>();
            f.bubble = bubble;
            f.canvas = bubble.GetComponentInParent<Canvas>()?.rootCanvas;

            RectTransform t = UIKit.MakeRect("Pointer", bubble, new Vector2(0f, 0f), Vector2.zero, new Vector2(64f, 76f));
            var img = t.gameObject.AddComponent<Image>();
            img.sprite = HudRestyle.BubbleTail();
            img.raycastTarget = false;
            t.pivot = new Vector2(0.5f, 1f);
            f.tail = t;
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            Camera cam = Camera.main;
            PassengerFriend friend = PassengerFriend.Current;
            Vector3 sp = Vector3.zero;
            bool ok = friend != null && cam != null;
            if (ok)
            {
                sp = cam.WorldToScreenPoint(friend.HeadWorld + Vector3.up * 0.1f);
                ok = sp.z > 0.05f && sp.x > -50f && sp.x < Screen.width + 50f && sp.y > -50f && sp.y < Screen.height + 50f;
            }

            float k = canvas.scaleFactor;
            Vector2 size = bubble.rect.size * k;
            Vector2 target;
            bool toLeft;
            if (ok)
            {
                // the bubble goes towards the middle of the screen, so it never runs off the edge
                toLeft = sp.x > Screen.width * 0.5f;
                Vector2 gap = new Vector2(70f, 60f) * k;
                target = new Vector2(toLeft ? sp.x - gap.x - size.x * 0.5f : sp.x + gap.x + size.x * 0.5f, sp.y + gap.y + size.y * 0.5f);
                target.x = Mathf.Clamp(target.x, size.x * 0.5f + 16f, Screen.width - size.x * 0.5f - 16f);
                target.y = Mathf.Clamp(target.y, size.y * 0.5f + 16f, Screen.height - size.y * 0.5f - 200f * k);
            }
            else
            {
                toLeft = false;
                target = new Vector2(Screen.width * 0.5f, Screen.height - 200f * k - size.y * 0.5f);
            }

            // follow smoothly (the head bobs with the car); jump there the first time
            smooth = placed ? Vector2.Lerp(smooth, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime)) : target;
            placed = true;

            bubble.pivot = new Vector2(0.5f, 0.5f);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                bubble.position = new Vector3(smooth.x, smooth.y, 0f);
            else if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, smooth, canvas.worldCamera, out Vector3 w))
                bubble.position = w;

            // the pointer: under the bubble, on the corner nearest the head, tip towards the mouth
            tail.gameObject.SetActive(ok);
            if (ok)
            {
                tail.anchorMin = tail.anchorMax = new Vector2(toLeft ? 0.82f : 0.18f, 0f);
                tail.anchoredPosition = new Vector2(0f, 14f);
                Vector2 tailScreen = smooth + new Vector2((toLeft ? 0.32f : -0.32f) * size.x, -size.y * 0.5f);
                Vector2 dir = new Vector2(sp.x, sp.y) - tailScreen;
                float angle = Vector2.SignedAngle(Vector2.down, dir);
                tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -60f, 60f));
            }
        }
    }
}
