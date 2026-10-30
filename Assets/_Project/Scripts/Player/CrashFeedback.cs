using System.Collections;
using UnityEngine;
using RoadSafety.Core;
using RoadSafety.World;

namespace RoadSafety.Player
{
    /// <summary>
    /// Soft, comic reaction to a collision (US 1.5): no blood, no crash.
    /// The car bounces back, squashes like a cartoon, little stars turn above it,
    /// the child loses one star and hears "انتبه، انظر قبل أن تتقدّم!", then the game
    /// goes on by itself after a few seconds.
    /// It also lowers the car's Health in the GameManager (behaviour tree: Repair).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CrashFeedback : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float freezeSeconds = 2.5f;
        [SerializeField, Min(0f)] private float cooldown = 3f;
        [Tooltip("Bumps slower than this (m/s) are ignored.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 1.2f;
        [SerializeField, Min(0f)] private float healthDamage = 15f;
        [Tooltip("The visual model that squashes (child named \"Model\" if empty).")]
        [SerializeField] private Transform model;

        private CarController car;
        private float nextAllowed;
        private Sprite starSprite;

        private void Awake()
        {
            car = GetComponent<CarController>();
            if (model == null)
                model = transform.Find("Model");
            starSprite = Resources.Load<Sprite>("HUD/icon_star");
        }

        private void OnEnable()
        {
            // Settling on the road when the level starts is not a crash.
            nextAllowed = Time.time + 1f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextAllowed)
                return;
            if (collision.collider.GetComponentInParent<Boundary>() != null)
                return; // the edge of the road only stops the car
            if (collision.contactCount == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (contact.normal.y > 0.6f)
                return; // the ground, a kerb top or a ramp: driving on it is not a crash

            // Only the part of the speed that goes into the obstacle counts.
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (impact < minImpactSpeed && Mathf.Abs(car.Speed) < minImpactSpeed)
                return;

            nextAllowed = Time.time + cooldown;
            Debug.Log($"[CrashFeedback] Bumped into {collision.collider.name} ({impact:0.0} m/s).");
            StartCoroutine(Bump());
        }

        private IEnumerator Bump()
        {
            car.Bounce(2.5f);
            if (DriverControls.Instance != null)
                DriverControls.Instance.Freeze(freezeSeconds);
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.RemoveStar(Mistake.Collision);
            if (GameManager.Instance != null)
                GameManager.Instance.TakeDamage(healthDamage);
            GameMessages.Say(Lines.Collision);

            GameObject dizzy = MakeDizzyStars();
            if (model != null)
                StartCoroutine(Squash());

            float t = 0f;
            while (t < freezeSeconds)
            {
                if (dizzy != null)
                {
                    dizzy.transform.position = transform.position + Vector3.up * 2.3f;
                    dizzy.transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.World);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (dizzy != null)
                Destroy(dizzy);
        }

        private IEnumerator Squash()
        {
            Vector3 rest = Vector3.one;
            float t = 0f;
            const float duration = 0.5f;
            while (t < duration)
            {
                float k = t / duration;
                float wobble = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k) * 0.18f;
                model.localScale = new Vector3(rest.x + wobble, rest.y - wobble, rest.z + wobble);
                t += Time.deltaTime;
                yield return null;
            }
            model.localScale = rest;
        }

        private GameObject MakeDizzyStars()
        {
            if (starSprite == null)
                return null;
            var root = new GameObject("DizzyStars");
            root.transform.position = transform.position + Vector3.up * 2.3f;
            for (int i = 0; i < 3; i++)
            {
                var star = new GameObject("Star", typeof(SpriteRenderer), typeof(Billboard));
                star.transform.SetParent(root.transform, false);
                float a = i * Mathf.PI * 2f / 3f;
                star.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.8f, 0f, Mathf.Sin(a) * 0.8f);
                star.transform.localScale = Vector3.one * 0.35f;
                star.GetComponent<SpriteRenderer>().sprite = starSprite;
            }
            return root;
        }
    }
}
