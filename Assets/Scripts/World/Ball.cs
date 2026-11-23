using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// A football that rolls across the road (US 2.3). If the car touches it, it flies away
    /// like in a cartoon (the car's CrashFeedback takes care of the star and the message).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Ball : MonoBehaviour
    {
        private static readonly List<Ball> registered = new List<Ball>();

        [SerializeField, Min(0.1f)] private float radius = 0.35f;

        private Rigidbody body;
        private Coroutine rolling;

        public static IReadOnlyList<Ball> All => registered;

        public bool IsRolling { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
        }

        private void OnEnable() => registered.Add(this);

        private void OnDisable() => registered.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();

        public void RollTo(Vector3 target, float speed)
        {
            if (rolling != null)
                StopCoroutine(rolling);
            rolling = StartCoroutine(Roll(target, speed));
        }

        private IEnumerator Roll(Vector3 target, float speed)
        {
            IsRolling = true;
            target.y = transform.position.y;
            while (body.isKinematic)
            {
                Vector3 position = body.position;
                Vector3 delta = target - position;
                float step = speed * Time.fixedDeltaTime;
                if (delta.magnitude <= step)
                {
                    body.MovePosition(target);
                    break;
                }
                Vector3 dir = delta.normalized;
                body.MovePosition(position + dir * step);
                Vector3 axis = Vector3.Cross(Vector3.up, dir);
                body.MoveRotation(Quaternion.AngleAxis(step / radius * Mathf.Rad2Deg, axis) * body.rotation);
                yield return new WaitForFixedUpdate();
            }
            IsRolling = false;
            rolling = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!body.isKinematic || collision.collider.GetComponentInParent<CarController>() == null)
                return;
            // Comic "boing": the ball jumps away.
            if (rolling != null)
                StopCoroutine(rolling);
            IsRolling = false;
            body.isKinematic = false;
            Vector3 push = collision.collider.attachedRigidbody != null ? collision.collider.attachedRigidbody.transform.forward : Vector3.forward;
            body.AddForce((push * 4f + Vector3.up * 5f), ForceMode.VelocityChange);
        }
    }
}
