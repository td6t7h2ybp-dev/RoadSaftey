using System.Collections.Generic;
using UnityEngine;
using RoadSafety.World;

namespace RoadSafety.AI
{
    public enum PedestrianState
    {
        Idle,
        WalkingToKerb,
        Waiting,
        Crossing,
        SteppingBack,
        Thanking,
        Leaving,
        Done
    }

    /// <summary>
    /// A pedestrian driven by a behaviour tree (US 2.2 and US 4.3):
    /// <code>
    /// Selector
    /// ├─ Sequence: crossing and a car rushes?   → Step back to the kerb
    /// ├─ Sequence: stepping back?               → Step back
    /// ├─ Sequence: crossing?                    → Cross
    /// ├─ Sequence: waiting, all cars stopped?   → Start crossing
    /// ├─ Sequence: walking to the kerb?         → Walk to the kerb
    /// ├─ Sequence: crossed?                     → Wave "thank you", then leave
    /// └─ Wait at the kerb, hand up
    /// </code>
    /// The model is made of simple parts (legs, arms) animated in code.
    /// </summary>
    public class Pedestrian : MonoBehaviour, IBTAgent
    {
        private static readonly List<Pedestrian> registered = new List<Pedestrian>();

        [Header("Path")]
        [SerializeField] private Transform kerbPoint;
        [SerializeField] private Transform farKerbPoint;
        [SerializeField] private Transform exitPoint;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.4f;
        [SerializeField, Min(0.1f)] private float runSpeed = 3.2f;
        [SerializeField, Min(0f)] private float waitBeforeCrossing = 1f;

        [Header("Body parts (animated)")]
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform body;

        private CrosswalkRule rule;
        private Node root;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree; // the Behavior Designer tree that runs it (BDBridge)
        private BTBindings bindings;

        /// <summary>IBTAgent: the conditions and actions the Behavior Designer tasks call by name.</summary>
        public BTBindings Bindings => bindings ?? (root != null ? bindings = BTBindings.From(root) : null);

        /// <summary>IBTAgent: the tree, as saved into the Behavior Designer component.</summary>
        public Node DescribeTree() => BuildTree();
        private float waitTimer;
        private float stateTimer;
        private float walkPhase;
        private bool moving;
        private Vector3 bodyRest;

        public static IReadOnlyList<Pedestrian> All => registered;

        public PedestrianState State { get; private set; } = PedestrianState.Idle;

        public string CurrentAction { get; private set; } = "Idle";

        /// <summary>True from the moment they head for the kerb until they reach the other side.</summary>
        public bool WantsToCross =>
            State == PedestrianState.WalkingToKerb || State == PedestrianState.Waiting ||
            State == PedestrianState.Crossing || State == PedestrianState.SteppingBack;

        public bool IsOnRoad => State == PedestrianState.Crossing || State == PedestrianState.SteppingBack;

        public void Configure(Transform kerb, Transform farKerb, Transform exit, Transform legL, Transform legR, Transform armL, Transform armR, Transform torso)
        {
            kerbPoint = kerb;
            farKerbPoint = farKerb;
            exitPoint = exit;
            leftLeg = legL;
            rightLeg = legR;
            leftArm = armL;
            rightArm = armR;
            body = torso;
        }

        private void OnEnable() => registered.Add(this);

        private void OnDisable() => registered.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();

        private void Awake()
        {
            if (body != null)
                bodyRest = body.localPosition;
            root = BuildTree();
        }

        /// <summary>Called by the crossing when the player's car comes near.</summary>
        public void Activate(CrosswalkRule crosswalk)
        {
            rule = crosswalk;
            if (State == PedestrianState.Idle)
                SetState(PedestrianState.WalkingToKerb);
        }

        /// <summary>A car drove over the crossing: jump back to the kerb.</summary>
        public void GetScared()
        {
            if (State == PedestrianState.Crossing)
                SetState(PedestrianState.SteppingBack);
            else if (State == PedestrianState.Waiting)
                waitTimer = -1.5f; // wait a bit longer after the fright
        }

        private Node BuildTree()
        {
            return new Selector("Pedestrian",
                new Sequence("Danger",
                    new Condition("Crossing and a car rushes?", () => State == PedestrianState.Crossing && rule != null && rule.CarRushing() && FractionCrossed() < 0.5f),
                    new ActionNode("Step back", () => { SetState(PedestrianState.SteppingBack); return NodeState.Running; })),
                new Sequence("Stepping back",
                    new Condition("Stepping back?", () => State == PedestrianState.SteppingBack),
                    new ActionNode("Run to the kerb", StepBack)),
                new Sequence("Crossing",
                    new Condition("Crossing?", () => State == PedestrianState.Crossing),
                    new ActionNode("Cross", Cross)),
                new Sequence("Safe to cross",
                    new Condition("Waited and all cars stopped?", () => State == PedestrianState.Waiting && waitTimer >= waitBeforeCrossing && rule != null && rule.IsSafeToCross()),
                    new ActionNode("Start crossing", () => { SetState(PedestrianState.Crossing); return NodeState.Running; })),
                new Sequence("Go to the kerb",
                    new Condition("Walking to the kerb?", () => State == PedestrianState.WalkingToKerb),
                    new ActionNode("Walk to the kerb", WalkToKerb)),
                new Sequence("After crossing",
                    new Condition("Crossed?", () => State == PedestrianState.Thanking || State == PedestrianState.Leaving),
                    new ActionNode("Thank and leave", ThankAndLeave)),
                new ActionNode("Wait, hand up", Wait));
        }

        private void Update()
        {
            if (State == PedestrianState.Idle || State == PedestrianState.Done)
                return;
            stateTimer += Time.deltaTime;
            moving = false;
            if (bdTree == null) bdTree = BDBridge.Ensure(this, root, "Pedestrian");
            BDBridge.Tick(bdTree); // Behavior Designer runs the tree
            Animate();
        }

        private void SetState(PedestrianState next)
        {
            State = next;
            stateTimer = 0f;
            CurrentAction = next.ToString();
            if (next == PedestrianState.Waiting)
                waitTimer = 0f;
        }

        // ---------- actions ----------

        private NodeState WalkToKerb()
        {
            if (MoveTo(kerbPoint.position, walkSpeed))
            {
                Face(farKerbPoint.position);
                SetState(PedestrianState.Waiting);
            }
            return NodeState.Running;
        }

        private NodeState Wait()
        {
            waitTimer += Time.deltaTime;
            CurrentAction = "Wait";
            return NodeState.Running;
        }

        private NodeState Cross()
        {
            if (MoveTo(farKerbPoint.position, walkSpeed))
            {
                SetState(PedestrianState.Thanking);
                if (rule != null)
                    rule.OnPedestrianCrossed();
            }
            return NodeState.Running;
        }

        private NodeState StepBack()
        {
            if (MoveTo(kerbPoint.position, runSpeed))
            {
                Face(farKerbPoint.position);
                SetState(PedestrianState.Waiting);
                waitTimer = -1f;
            }
            return NodeState.Running;
        }

        private NodeState ThankAndLeave()
        {
            if (State == PedestrianState.Thanking)
            {
                if (Camera.main != null)
                    Face(Camera.main.transform.position);
                if (stateTimer > 1.6f)
                    SetState(PedestrianState.Leaving);
                return NodeState.Running;
            }
            if (exitPoint == null || MoveTo(exitPoint.position, walkSpeed))
            {
                SetState(PedestrianState.Done);
                gameObject.SetActive(false);
            }
            return NodeState.Running;
        }

        // ---------- helpers ----------

        private float FractionCrossed()
        {
            float total = Vector3.Distance(kerbPoint.position, farKerbPoint.position);
            return total < 0.01f ? 1f : Vector3.Distance(kerbPoint.position, transform.position) / total;
        }

        private bool MoveTo(Vector3 target, float speed)
        {
            Vector3 position = transform.position;
            target.y = position.y;
            Vector3 delta = target - position;
            float step = speed * Time.deltaTime;
            if (delta.magnitude <= step)
            {
                transform.position = target;
                SnapToGround();
                return true;
            }
            transform.position = position + delta.normalized * step;
            SnapToGround();
            Face(target);
            moving = true;
            walkPhase += Time.deltaTime * speed * 5.5f;
            return false;
        }

        /// <summary>Keeps the feet on the pavement or the road (they are not at the same height).</summary>
        private void SnapToGround()
        {
            Vector3 p = transform.position;
            const int mask = ~(1 << 2);
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore)
                && hit.collider.attachedRigidbody == null)
                transform.position = new Vector3(p.x, hit.point.y, p.z);
        }

        private void Face(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void Animate()
        {
            float swing = moving ? Mathf.Sin(walkPhase) * 30f : 0f;
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);

            if (rightArm != null)
            {
                if (State == PedestrianState.Waiting)
                    rightArm.localRotation = Quaternion.Euler(-160f, 0f, 0f); // hand up: "I want to cross"
                else if (State == PedestrianState.Thanking)
                    rightArm.localRotation = Quaternion.Euler(-150f, 0f, Mathf.Sin(stateTimer * 12f) * 25f); // waving
                else
                    rightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
            }

            if (body != null)
            {
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f : 0f;
                if (State == PedestrianState.SteppingBack && stateTimer < 0.3f)
                    bob = 0.25f * (1f - stateTimer / 0.3f); // little jump of surprise
                body.localPosition = bodyRest + Vector3.up * bob;
            }
        }
    }
}
