using UnityEngine;
using RoadSafety.AI;

namespace RoadSafety.World
{
    /// <summary>
    /// A stray cat along the walls, run by a Behavior Designer tree.
    /// It makes NO decision itself: its brain is the Behavior Designer tree BT_Cat
    /// (Assets/_Project/BehaviorTrees, built in Tools > Behavior Designer), plugged into the
    /// External Behavior field of its Behavior Tree component. This script is only the body:
    /// it moves and animates, and answers the questions the tree's tasks ask.
    /// </summary>
    public class StreetCat : MonoBehaviour
    {
        public Vector3 pathA, pathB;
        public float speed = 1.1f;

        private int dir = 1;
        private float bored, sleepy, busyUntil, phase;
        private Transform body, tail;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;




        private void Awake()
        {
            body = transform.Find("Body");
            tail = body != null ? body.Find("Tail") : null;
            phase = Random.value * 10f;
            bored = Random.Range(0f, 0.3f);
            sleepy = Random.Range(0f, 0.2f);
        }

        private void Update()
        {
            if (bdTree == null) bdTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            BDBridge.Tick(bdTree);
            if (tail != null)
                tail.localRotation = Quaternion.Euler(-20f + Mathf.Sin((Time.time + phase) * 2.2f) * 10f, Mathf.Sin((Time.time + phase) * 1.3f) * 30f, 0f);
        }

        // ---------- questions and utilities used by the Behavior Designer tasks ----------

        public bool CarClose() => StreetLife.CarDistance(transform.position) < 7f;
        public float WashUtility() => Time.time < busyUntil ? 1f : bored;
        public float NapUtility() => sleepy;

        public NodeState Dash()
        {
            Vector3 wall = new Vector3(Mathf.Sign(pathA.x) * 10.3f, transform.position.y, transform.position.z);
            Walk(wall, speed * 4f, 18f);
            bored = 0f;
            busyUntil = 0f;
            return NodeState.Running;
        }

        public NodeState Stroll()
        {
            bored = Mathf.Clamp01(bored + Time.deltaTime * 0.05f);
            sleepy = Mathf.Clamp01(sleepy + Time.deltaTime * 0.015f);
            Vector3 goal = dir > 0 ? pathB : pathA;
            goal.y = transform.position.y;
            if (Vector3.Distance(transform.position, goal) < 0.2f) dir = -dir;
            Walk(goal, speed, 10f);
            return NodeState.Running;
        }

        public NodeState Wash()
        {
            if (busyUntil < Time.time) busyUntil = Time.time + Random.Range(3f, 6f);
            bored = 0f;
            Pose(0f, Mathf.Sin(Time.time * 6f) * 8f, -12f); // sitting up, head bobbing
            return NodeState.Running;
        }

        public NodeState Nap()
        {
            sleepy = Mathf.Clamp01(sleepy - Time.deltaTime * 0.05f);
            Pose(-0.08f, 0f, 0f); // curled low
            return NodeState.Running;
        }

        private void Walk(Vector3 goal, float v, float bobRate)
        {
            Vector3 to = goal - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0004f)
            {
                transform.position += Vector3.ClampMagnitude(to, v * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
            }
            Pose(Mathf.Abs(Mathf.Sin(Time.time * bobRate)) * 0.02f, 0f, 0f);
        }

        private void Pose(float lift, float roll, float pitch)
        {
            if (body == null) return;
            body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0f, lift, 0f), Time.deltaTime * 8f);
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(pitch, 0f, roll), Time.deltaTime * 6f);
        }
    }
}
