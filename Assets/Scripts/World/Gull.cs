using UnityEngine;
using RoadSafety.AI;

namespace RoadSafety.World
{
    /// <summary>
    /// A seagull over the street, run by a Behavior Designer tree.
    /// It makes NO decision itself: its brain is the Behavior Designer tree BT_Seagull
    /// (Assets/_Project/BehaviorTrees, built in Tools > Behavior Designer), plugged into the
    /// External Behavior field of its Behavior Tree component. This script is only the body:
    /// it moves and animates, and answers the questions the tree's tasks ask.
    /// </summary>
    public class Gull : MonoBehaviour
    {
        public Vector3 centre;
        public float radius = 12f, speed = 0.3f, phase;

        private float flapUntil, nextFlap;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;




        private void Awake()
        {
            nextFlap = Time.time + Random.Range(2f, 6f);
        }

        private void Update()
        {
            if (bdTree == null) bdTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            BDBridge.Tick(bdTree);
        }

        // ---------- utilities and actions used by the Behavior Designer tasks ----------

        public float FlapUtility() => Time.time < flapUntil || Time.time > nextFlap ? 0.9f : 0f;
        public NodeState Glide() => Move(8f);

        public NodeState Flap()
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
