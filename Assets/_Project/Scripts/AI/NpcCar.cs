using UnityEngine;
using RoadSafety.World;

namespace RoadSafety.AI
{
    /// <summary>
    /// One of the other cars (taxi, louage...), driven by a behaviour tree with a
    /// utility choice at the end (US 4.3, slide 4 "Fonction utilitaire + BT"):
    /// <code>
    /// Selector
    /// ├─ Sequence: something in front (car, child, ball)?  → Brake behind it
    /// ├─ Sequence: red light or pedestrians at the line?   → Stop at the line
    /// └─ UtilitySelector (highest score wins)
    ///      ├─ Cruise     (utility 0.6)
    ///      └─ Slow down  (utility grows near crossings and lights)
    /// </code>
    /// It drives on a straight path and starts again from the beginning at the end.
    /// Kinematic: it never pushes the player's car; the player bumps into it instead.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NpcCar : Vehicle
    {
        [SerializeField] private Vector3 pathStart;
        [SerializeField] private Vector3 pathEnd;
        [SerializeField, Min(0.5f)] private float cruiseSpeed = 6f;
        [SerializeField, Min(0.1f)] private float acceleration = 2.5f;
        [SerializeField, Min(0.1f)] private float deceleration = 6f;
        [SerializeField, Min(1f)] private float lookAhead = 10f;
        [SerializeField, Min(0.5f)] private float halfLength = 2.2f;

        private Rigidbody body;
        private Node root;
        private UtilitySelector speedChoice;
        private float speed;
        private float targetSpeed;
        private float obstacleDistance;
        private float lineDistance;

        public override float Speed => speed;

        /// <summary>Name of the last action chosen by the tree (shown as a gizmo label in the Editor).</summary>
        public string Decision { get; private set; } = "";

        public void Configure(Vector3 start, Vector3 end, float speedMs)
        {
            pathStart = start;
            pathEnd = end;
            cruiseSpeed = speedMs;
        }

        public float CruiseSpeed
        {
            get => cruiseSpeed;
            set => cruiseSpeed = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            root = BuildTree();
        }

        private void Start()
        {
            speed = cruiseSpeed;
        }

        private Node BuildTree()
        {
            speedChoice = new UtilitySelector("Choose speed",
                new UtilityOption(new ActionNode("Cruise", Cruise), () => 0.6f),
                new UtilityOption(new ActionNode("Slow down", SlowDown), SlowDownUtility));

            return new Selector("Other car",
                new Sequence("Obstacle",
                    new Condition("Something in front?", ObstacleAhead),
                    new ActionNode("Brake", Brake)),
                new Sequence("Stop line",
                    new Condition("Red light or pedestrians?", BlockedLineAhead),
                    new ActionNode("Stop at the line", StopAtLine)),
                speedChoice);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            root.Evaluate();
            float rate = targetSpeed < speed ? deceleration : acceleration;
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

            Vector3 dir = (pathEnd - pathStart).normalized;
            Vector3 next = body.position + dir * speed * dt;
            if (Vector3.Dot(next - pathEnd, dir) > 0f)
            {
                // End of the road: start again from the beginning (teleport, no interpolation).
                body.position = pathStart;
                transform.position = pathStart;
                return;
            }
            body.MovePosition(next);
            body.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
        }

        // ---------- conditions ----------

        private bool ObstacleAhead()
        {
            Vector3 origin = body.position + transform.forward * halfLength + Vector3.up * 1f;
            const int mask = ~(1 << 2); // ignore "Ignore Raycast" (road-edge walls)
            if (Physics.SphereCast(origin, 0.7f, transform.forward, out RaycastHit hit, lookAhead, mask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.attachedRigidbody != body && !hit.collider.transform.IsChildOf(transform))
                {
                    obstacleDistance = hit.distance;
                    return true;
                }
            }
            return false;
        }

        private bool BlockedLineAhead()
        {
            lineDistance = float.MaxValue;
            foreach (StopLine line in StopLine.All)
            {
                if (Vector3.Dot(line.transform.forward, transform.forward) < 0.7f)
                    continue;
                Measure(line.transform.position, out float ahead, out float sideways);
                if (ahead > lookAhead + 12f || Mathf.Abs(sideways) > line.HalfWidth)
                    continue;
                // Front of the car already on the line, or too close to stop safely: go on.
                float room = ahead - halfLength;
                if (room < 0.3f || room < speed * speed / (2f * deceleration))
                    continue;
                if (line.IsBlocked && ahead < lineDistance)
                    lineDistance = ahead;
            }
            return lineDistance < float.MaxValue;
        }

        private float SlowDownUtility()
        {
            // Grows from 0 to 1 when a stop line (crossing, light) is 25 m → 0 m ahead.
            float best = 0f;
            foreach (StopLine line in StopLine.All)
            {
                if (Vector3.Dot(line.transform.forward, transform.forward) < 0.7f)
                    continue;
                Measure(line.transform.position, out float ahead, out float sideways);
                if (ahead < 0f || ahead > 25f || Mathf.Abs(sideways) > line.HalfWidth)
                    continue;
                best = Mathf.Max(best, 1f - ahead / 25f);
            }
            return best;
        }

        // ---------- actions ----------

        private NodeState Brake()
        {
            Decision = "Brake";
            targetSpeed = obstacleDistance > 3f ? (obstacleDistance - 3f) * 0.8f : 0f;
            return NodeState.Running;
        }

        private NodeState StopAtLine()
        {
            Decision = "Stop at line";
            float d = lineDistance - halfLength - 0.5f;
            targetSpeed = d > 0.3f ? Mathf.Min(cruiseSpeed, d * 0.9f) : 0f;
            return NodeState.Running;
        }

        private NodeState Cruise()
        {
            Decision = "Cruise";
            targetSpeed = cruiseSpeed;
            return NodeState.Running;
        }

        private NodeState SlowDown()
        {
            Decision = "Slow down";
            targetSpeed = cruiseSpeed * 0.55f;
            return NodeState.Running;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f, Decision);
        }
#endif
    }
}
