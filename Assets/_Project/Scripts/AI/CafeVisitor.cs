using UnityEngine;
using RoadSafety.Player;

namespace RoadSafety.AI
{
    /// <summary>
    /// Someone who goes to a café: walks along the pavement, feels like a coffee, walks in through
    /// the café door, stays inside a while, comes out and strolls off - and back again later.
    /// Run by a Behavior Designer tree:
    /// <code>
    /// Selector "Café visitor"
    /// ├─ Sequence "Horn heard"     : Car honked nearby?   → Wave at the car
    /// ├─ Sequence "Inside the café": Inside?              → Have a coffee inside
    /// ├─ Sequence "Leaving"        : Just had a coffee?   → Walk out and away
    /// ├─ Sequence "Going in"       : Wants a coffee?      → Walk in through the door
    /// └─ UtilitySelector "On the pavement": Stroll (0.35) | Look at the shops (curiosity)
    /// </code>
    /// The body is the same primitive rig as the townspeople (dressed by RiggedNpc).
    /// </summary>
    public class CafeVisitor : MonoBehaviour, IBTAgent
    {
        [Header("Café")]
        public Vector3 door;        // in front of the café door, on the pavement
        public Vector3 inside;      // a point inside the café (hidden by the walls)
        public Vector3 strollA, strollB; // pavement walk, left and right of the café
        [Min(0.1f)] public float walkSpeed = 1.2f;

        [Header("Body (animated in code)")]
        public Transform legL, legR, armL, armR, upper;

        private enum Stage { Outside, GoingIn, Inside, Leaving }

        private Stage stage = Stage.Outside;
        private float thirst, curiosity, stageUntil, waveUntil, walkPhase, offset;
        private int strollDir = 1;
        private DriverControls controls;
        private Node root;
        private BTBindings bindings;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;

        public BTBindings Bindings => bindings ?? (root != null ? bindings = BTBindings.From(root) : null);

        public Node DescribeTree() => BuildTree();

        private Node BuildTree() => new Selector("Café visitor",
            new Sequence("Horn heard",
                new Condition("Car honked nearby?", () => Time.time < waveUntil && stage != Stage.Inside),
                new ActionNode("Wave at the car", Wave)),
            new Sequence("Inside the café",
                new Condition("Inside?", () => stage == Stage.Inside),
                new ActionNode("Have a coffee inside", Inside)),
            new Sequence("Leaving",
                new Condition("Just had a coffee?", () => stage == Stage.Leaving),
                new ActionNode("Walk out and away", Leave)),
            new Sequence("Going in",
                new Condition("Wants a coffee?", () => stage == Stage.GoingIn || thirst >= 1f),
                new ActionNode("Walk in through the door", GoIn)),
            new UtilitySelector("On the pavement",
                new UtilityOption(new ActionNode("Stroll", Stroll), () => 0.35f),
                new UtilityOption(new ActionNode("Look at the shops", LookAround), () => Time.time < stageUntil ? 1f : curiosity)));

        private void Awake()
        {
            offset = Random.Range(0f, 10f);
            thirst = Random.Range(0f, 0.8f);
            curiosity = Random.Range(0f, 0.3f);
            root = BuildTree();
        }

        private void OnDisable()
        {
            if (controls != null) controls.Honked -= OnHonk;
            controls = null;
        }

        private void Update()
        {
            if (controls == null && DriverControls.Instance != null)
            {
                controls = DriverControls.Instance;
                controls.Honked += OnHonk;
            }
            if (bdTree == null) bdTree = BDBridge.Ensure(this, root, "Café visitor");
            BDBridge.Tick(bdTree);
        }

        private void OnHonk()
        {
            CarController car = CarController.Player;
            if (car != null && Vector3.Distance(car.transform.position, transform.position) < 18f)
                waveUntil = Time.time + Random.Range(1.4f, 2.2f);
        }

        // ---------- actions ----------

        private NodeState Wave()
        {
            CarController car = CarController.Player;
            if (car != null) Face(car.transform.position, 6f);
            SetLimb(armR, -155f, Mathf.Sin(Time.time * 10f) * 22f);
            SetLimb(armL, 0f, 0f);
            StandStill();
            return NodeState.Running;
        }

        private NodeState Stroll()
        {
            thirst = Mathf.Clamp01(thirst + Time.deltaTime * 0.04f);
            curiosity = Mathf.Clamp01(curiosity + Time.deltaTime * 0.03f);
            Vector3 goal = strollDir > 0 ? strollB : strollA;
            if (MoveTo(goal, walkSpeed)) strollDir = -strollDir;
            return NodeState.Running;
        }

        private NodeState LookAround()
        {
            if (stageUntil < Time.time) stageUntil = Time.time + Random.Range(2f, 4f);
            curiosity = 0f;
            StandStill();
            // turn towards the shops and look around
            Face(transform.position + new Vector3(Mathf.Sign(door.x), 0f, Mathf.Sin(Time.time * 0.8f + offset)), 2f);
            SetLimb(armR, 0f, 0f);
            SetLimb(armL, 0f, 0f);
            return NodeState.Running;
        }

        private NodeState GoIn()
        {
            stage = Stage.GoingIn;
            // first to the door, then through it
            Vector3 target = Flat(transform.position - door).magnitude > 0.3f && !PastDoor() ? door : inside;
            if (MoveTo(target, walkSpeed) && target == inside)
            {
                stage = Stage.Inside;
                stageUntil = Time.time + Random.Range(6f, 12f);
            }
            return NodeState.Running;
        }

        private NodeState Inside()
        {
            StandStill();
            if (Time.time >= stageUntil)
            {
                stage = Stage.Leaving;
                thirst = 0f;
            }
            return NodeState.Running;
        }

        private NodeState Leave()
        {
            Vector3 target = PastDoor() ? door : (strollDir > 0 ? strollB : strollA);
            if (MoveTo(target, walkSpeed) && target != door)
            {
                stage = Stage.Outside;
                strollDir = -strollDir;
            }
            return NodeState.Running;
        }

        // ---------- helpers ----------

        private bool PastDoor() => Mathf.Abs(transform.position.x) > Mathf.Abs(door.x) + 0.2f;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private bool MoveTo(Vector3 target, float speed)
        {
            Vector3 to = Flat(target - transform.position);
            if (to.magnitude < 0.15f)
            {
                StandStill();
                return true;
            }
            transform.position += Vector3.ClampMagnitude(to, speed * Time.deltaTime);
            Face(transform.position + to, 6f);
            walkPhase += Time.deltaTime * speed * 5f;
            float swing = Mathf.Sin(walkPhase) * 28f;
            SetLimb(legL, swing, 0f);
            SetLimb(legR, -swing, 0f);
            SetLimb(armL, -swing * 0.7f, 0f);
            SetLimb(armR, swing * 0.7f, 0f);
            return false;
        }

        private void Face(Vector3 point, float speed)
        {
            Vector3 flat = Flat(point - transform.position);
            if (flat.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat, Vector3.up), 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private void StandStill()
        {
            SetLimb(legL, 0f, 0f);
            SetLimb(legR, 0f, 0f);
        }

        private static void SetLimb(Transform limb, float pitch, float roll)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
