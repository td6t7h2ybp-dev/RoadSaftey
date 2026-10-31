using UnityEngine;
using RoadSafety.AI;

namespace RoadSafety.World
{
    /// <summary>
    /// A seagull over the street, run by a Behavior Designer tree:
    /// UtilitySelector "Gull": Glide in circles (0.5) | Flap to climb (now and then).
    /// </summary>
    public class Gull : MonoBehaviour, IBTAgent
    {
        public Vector3 centre;
        public float radius = 12f, speed = 0.3f, phase;

        private float flapUntil, nextFlap;
        private Node root;
        private BTBindings bindings;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;

        public BTBindings Bindings => bindings ?? (root != null ? bindings = BTBindings.From(root) : null);

        public Node DescribeTree() => BuildTree();

        private Node BuildTree() => new UtilitySelector("Gull",
            new UtilityOption(new ActionNode("Glide in circles", () => Move(8f)), () => 0.5f),
            new UtilityOption(new ActionNode("Flap to climb", Flap), () => Time.time < flapUntil || Time.time > nextFlap ? 0.9f : 0f));

        private void Awake()
        {
            root = BuildTree();
            nextFlap = Time.time + Random.Range(2f, 6f);
        }

        private void Update()
        {
            if (bdTree == null) bdTree = BDBridge.Ensure(this, root, "Seagull");
            BDBridge.Tick(bdTree);
        }

        private NodeState Flap()
        {
            if (Time.time >= flapUntil)
            {
                flapUntil = Time.time + Random.Range(0.8f, 1.5f);
                nextFlap = flapUntil + Random.Range(3f, 7f);
            }
            return Move(Mathf.Sin(Time.time * 14f) * 35f);
        }

        private NodeState Move(float wing)
        {
            float a = Time.time * speed + phase;
            transform.position = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 0.7f) * 1.5f, Mathf.Sin(a) * radius);
            transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))) * Quaternion.Euler(0f, 0f, 18f);
            StreetLife.Flap(transform, wing);
            return NodeState.Running;
        }
    }
}
