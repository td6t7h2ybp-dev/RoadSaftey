using UnityEngine;
using RoadSafety.World;

namespace RoadSafety.Player
{
    /// <summary>
    /// The child's car (US 1.1 and 1.2). It only reads <see cref="DriverControls"/>:
    ///   - Go held: speeds up to <see cref="maxSpeed"/>;
    ///   - Go released: slows down progressively (it rolls on, like a real car);
    ///   - Stop: brakes hard;
    ///   - arrows: turns progressively, more when the car is moving.
    /// Arcade physics on purpose: simple and predictable for 5-9 year olds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : Vehicle
    {
        [Header("Speed (US 1.1)")]
        [Tooltip("Top speed in m/s (9 m/s = about 32 km/h).")]
        [SerializeField, Min(1f)] private float maxSpeed = 9f;
        [SerializeField, Min(0.1f)] private float acceleration = 3.2f;
        [Tooltip("How fast the car slows down when Go is released (m/s per second).")]
        [SerializeField, Min(0.1f)] private float coastDeceleration = 2.2f;
        [Tooltip("How fast the Stop button stops the car (m/s per second).")]
        [SerializeField, Min(0.1f)] private float brakeDeceleration = 10f;

        [Header("Steering (US 1.2)")]
        [Tooltip("Turn speed in degrees per second at full steering.")]
        [SerializeField, Min(1f)] private float maxTurnRate = 70f;
        [Tooltip("Speed (m/s) from which the car turns at full rate.")]
        [SerializeField, Min(0.1f)] private float fullTurnSpeed = 4f;
        [Tooltip("Lets the car turn a little when Go is held but the car is blocked.")]
        [SerializeField, Range(0f, 1f)] private float minTurnFactor = 0.35f;

        private Rigidbody body;
        private float speed;

        /// <summary>The player's car in the current scene.</summary>
        public static CarController Player { get; private set; }

        public override float Speed => speed;

        public float MaxSpeed => maxSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Player = null;

        private void Awake()
        {
            Player = this;
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void OnDestroy()
        {
            if (Player == this)
                Player = null;
        }

        /// <summary>Small comic bounce backwards after a bump (US 1.5).</summary>
        public void Bounce(float backSpeed)
        {
            speed = -Mathf.Abs(backSpeed);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            DriverControls input = DriverControls.Instance;
            float throttle = input != null ? input.Throttle : 0f;
            bool braking = input != null && input.Braking;
            float steer = input != null ? input.Steer : 0f;

            // If something blocks the car (a wall), it can't go faster than it really goes.
            float actual = Vector3.Dot(body.linearVelocity, transform.forward);
            if (speed > 0f && actual < speed - 0.8f)
                speed = Mathf.Max(0f, actual);

            if (speed < 0f)
                speed = Mathf.MoveTowards(speed, 0f, coastDeceleration * 3f * dt);
            else if (braking)
                speed = Mathf.MoveTowards(speed, 0f, brakeDeceleration * dt);
            else if (throttle > 0f)
                speed = Mathf.MoveTowards(speed, maxSpeed * throttle, acceleration * dt);
            else
                speed = Mathf.MoveTowards(speed, 0f, coastDeceleration * dt);

            float turnFactor = Mathf.Clamp01(Mathf.Abs(speed) / fullTurnSpeed);
            if (throttle > 0f)
                turnFactor = Mathf.Max(turnFactor, minTurnFactor);
            float yaw = steer * maxTurnRate * turnFactor * dt * (speed < 0f ? -1f : 1f);
            if (Mathf.Abs(yaw) > 0.0001f)
                body.MoveRotation(body.rotation * Quaternion.Euler(0f, yaw, 0f));

            Vector3 velocity = transform.forward * speed;
            velocity.y = body.linearVelocity.y;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
        }
    }
}
