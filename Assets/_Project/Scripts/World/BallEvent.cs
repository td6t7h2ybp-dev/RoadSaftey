using System.Collections;
using UnityEngine;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// US 2.3, anticipation: near the garden, children are playing behind a parked van.
    ///   1. When the car comes near, a warning sign "!" pops up and the avatar says
    ///      "انتبه! أطفال يلعبون، خفّف السرعة" (visual + sound indicator).
    ///   2. A little later the ball rolls out from behind the van, across the road.
    ///   3. If the car was already slow when the ball came out → bonus star.
    /// The object's blue arrow points in the player's driving direction.
    /// </summary>
    public class BallEvent : MonoBehaviour
    {
        [SerializeField] private Ball ball;
        [SerializeField] private Transform rollTarget;
        [SerializeField] private GameObject warningSign;
        [SerializeField, Min(5f)] private float warnDistance = 34f;
        [SerializeField, Min(2f)] private float releaseDistance = 17f;
        [Tooltip("At or under this speed (m/s) when the ball comes out, the child gets the bonus.")]
        [SerializeField, Min(0.5f)] private float slowSpeed = 4f;
        [SerializeField, Min(0.5f)] private float rollSpeed = 4.5f;
        [SerializeField, Min(1f)] private float roadHalfWidth = 6f;

        private bool warned;
        private bool released;

        public void Configure(Ball rollingBall, Transform target, GameObject sign)
        {
            ball = rollingBall;
            rollTarget = target;
            warningSign = sign;
        }

        private void Start()
        {
            if (warningSign != null)
                warningSign.SetActive(false);
        }

        private void Update()
        {
            CarController car = CarController.Player;
            if (car == null || ball == null || released)
                return;
            float d = RoadMath.DistanceBefore(transform, car.transform.position, out float sideways);
            if (sideways > roadHalfWidth || d < 0f)
                return;

            if (!warned && d < warnDistance)
            {
                warned = true;
                if (warningSign != null)
                {
                    warningSign.SetActive(true);
                    StartCoroutine(Pop(warningSign.transform));
                }
                GameMessages.Say(Lines.KidsPlaying);
            }

            if (warned && d < releaseDistance)
            {
                released = true;
                ball.RollTo(rollTarget.position, rollSpeed);
                if (car.Speed <= slowSpeed)
                {
                    if (ProgressionManager.Instance != null)
                        ProgressionManager.Instance.AddStarOnce("ball", GoodAction.SlowedDownForDanger);
                    GameMessages.Say(Lines.SlowedDown);
                }
                else
                {
                    GameMessages.Say(Lines.BallTooFast);
                }
                if (warningSign != null)
                    StartCoroutine(HideLater(warningSign, 6f));
            }
        }

        private static IEnumerator Pop(Transform sign)
        {
            Vector3 rest = sign.localScale;
            float t = 0f;
            while (t < 0.4f)
            {
                float k = t / 0.4f;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.4f;
                sign.localScale = rest * (k * s);
                t += Time.deltaTime;
                yield return null;
            }
            sign.localScale = rest;
            Vector3 basePos = sign.localPosition;
            while (sign != null && sign.gameObject.activeSelf)
            {
                sign.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 4f) * 0.12f;
                yield return null;
            }
        }

        private static IEnumerator HideLater(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
                go.SetActive(false);
        }
    }
}
