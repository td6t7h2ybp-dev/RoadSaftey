using System.Collections.Generic;
using UnityEngine;
using RoadSafety.AI;

namespace RoadSafety.World
{
    /// <summary>
    /// A flock of pigeons, run by a Behavior Designer tree:
    /// <code>
    /// Selector "Pigeons"
    /// ├─ Sequence "Car coming": Car within 9 m?  → Fly off
    /// ├─ Sequence "In the air": Flying?          → Fly and land further on
    /// └─ UtilitySelector "On the ground": Peck at crumbs (0.5) | Hop around (wish to move) | Rest (tiredness)
    /// </code>
    /// </summary>
    public class PigeonFlock : MonoBehaviour, IBTAgent
    {
        private class P
        {
            public Transform t;
            public Vector3 home, target, vel;
            public float phase;
        }

        private readonly List<P> birds = new List<P>();
        private bool flying;
        private float flyTime;
        private Vector3 landing;
        private float hopWish, tired;
        private Node root;
        private BTBindings bindings;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;

        public BTBindings Bindings => bindings ?? (root != null ? bindings = BTBindings.From(root) : null);

        public Node DescribeTree() => BuildTree();

        private Node BuildTree() => new Selector("Pigeons",
            new Sequence("Car coming",
                new Condition("Car within 9 m?", () => !flying && StreetLife.CarDistance(transform.position) < 9f),
                new ActionNode("Fly off", TakeOff)),
            new Sequence("In the air",
                new Condition("Flying?", () => flying),
                new ActionNode("Fly and land further on", Fly)),
            new UtilitySelector("On the ground",
                new UtilityOption(new ActionNode("Peck at crumbs", Peck), () => 0.5f),
                new UtilityOption(new ActionNode("Hop around", Hop), () => hopWish),
                new UtilityOption(new ActionNode("Rest", Rest), () => tired)));

        private void Awake()
        {
            foreach (Transform t in transform)
                if (t.name == "Bird")
                    birds.Add(new P { t = t, home = t.localPosition, target = t.localPosition, phase = Random.value * 10f });
            hopWish = Random.Range(0f, 0.4f);
            root = BuildTree();
        }

        private void Update()
        {
            if (birds.Count == 0) return;
            if (bdTree == null) bdTree = BDBridge.Ensure(this, root, "Pigeons");
            BDBridge.Tick(bdTree);
        }

        private NodeState TakeOff()
        {
            flying = true;
            flyTime = 0f;
            landing = transform.position + new Vector3(0f, 0f, Random.Range(18f, 30f));
            foreach (P p in birds) p.vel = new Vector3(Random.Range(-1f, 1f), Random.Range(3f, 5f), Random.Range(1f, 3f));
            return NodeState.Success;
        }

        private NodeState Fly()
        {
            flyTime += Time.deltaTime;
            foreach (P p in birds)
            {
                Vector3 goal = transform.InverseTransformPoint(landing) + p.home + Vector3.up * Mathf.Clamp01(3.5f - flyTime) * 4f;
                p.vel = Vector3.Lerp(p.vel, (goal - p.t.localPosition) * 0.9f, Time.deltaTime * 1.4f);
                p.t.localPosition += p.vel * Time.deltaTime;
                if (p.vel.sqrMagnitude > 0.01f)
                    p.t.localRotation = Quaternion.Slerp(p.t.localRotation, Quaternion.LookRotation(p.vel), Time.deltaTime * 6f);
                StreetLife.Flap(p.t, Mathf.Sin((Time.time + p.phase) * 28f) * 55f);
            }
            if (flyTime > 5.5f)
            {
                Vector3 shift = landing - transform.position;
                transform.position = landing;
                foreach (P p in birds)
                {
                    p.t.localPosition -= shift; // the flock root moved: keep the birds where they are
                    p.target = p.home;
                    StreetLife.Flap(p.t, 0f);
                }
                flying = false;
            }
            return NodeState.Running;
        }

        private NodeState Peck()
        {
            hopWish = Mathf.Clamp01(hopWish + Time.deltaTime * 0.12f);
            tired = Mathf.Clamp01(tired + Time.deltaTime * 0.03f);
            Ground(0f, true);
            return NodeState.Running;
        }

        private NodeState Hop()
        {
            if (hopWish > 0.55f)
                foreach (P p in birds)
                    p.target = p.home + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
            hopWish = 0f;
            Ground(0.8f, false);
            return NodeState.Running;
        }

        private NodeState Rest()
        {
            tired = Mathf.Clamp01(tired - Time.deltaTime * 0.2f);
            Ground(0f, false);
            return NodeState.Running;
        }

        private void Ground(float speed, bool peck)
        {
            foreach (P p in birds)
            {
                Vector3 to = p.target - p.t.localPosition;
                to.y = 0f;
                if (to.magnitude > 0.05f)
                {
                    p.t.localPosition += Vector3.ClampMagnitude(to, Time.deltaTime * Mathf.Max(speed, 0.6f));
                    p.t.localRotation = Quaternion.Slerp(p.t.localRotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
                }
                Vector3 lp = p.t.localPosition;
                lp.y = Mathf.Lerp(lp.y, 0f, Time.deltaTime * 6f);
                p.t.localPosition = lp;
                float down = peck && Mathf.Sin((Time.time + p.phase) * 3.1f) > 0.85f ? 25f : 0f;
                p.t.localRotation = Quaternion.Euler(down, p.t.localEulerAngles.y, 0f);
            }
        }
    }
}
