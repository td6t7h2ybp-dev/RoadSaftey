using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.World
{
    /// <summary>
    /// Where the other cars must stop: in front of a red light or a crossing with pedestrians.
    /// Its blue arrow (forward) points in the direction the cars drive.
    /// </summary>
    public class StopLine : MonoBehaviour
    {
        private static readonly List<StopLine> registered = new List<StopLine>();

        [SerializeField] private TrafficLight trafficLight;
        [Tooltip("Ticked: this line follows the side-road colour of the traffic light.")]
        [SerializeField] private bool sideRoad;
        [SerializeField] private CrosswalkRule crossing;
        [Tooltip("Half the width of the lane this line covers (m).")]
        [SerializeField, Min(0.5f)] private float halfWidth = 2.6f;

        public static IReadOnlyList<StopLine> All => registered;

        public float HalfWidth => halfWidth;

        public bool IsBlocked =>
            (trafficLight != null && (sideRoad ? trafficLight.Side : trafficLight.Main) != LightColor.Green) ||
            (crossing != null && crossing.BlocksTraffic);

        public void Configure(TrafficLight light, bool isSideRoad, CrosswalkRule crosswalk, float width)
        {
            trafficLight = light;
            sideRoad = isSideRoad;
            crossing = crosswalk;
            halfWidth = width;
        }

        private void OnEnable() => registered.Add(this);

        private void OnDisable() => registered.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();

        private void OnDrawGizmos()
        {
            Gizmos.color = IsBlocked ? Color.red : Color.green;
            Vector3 side = transform.right * halfWidth;
            Gizmos.DrawLine(transform.position - side + Vector3.up * 0.1f, transform.position + side + Vector3.up * 0.1f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 1.5f);
        }
    }

    /// <summary>Distances between the player's car and a line across the road.</summary>
    public static class RoadMath
    {
        /// <summary>
        /// How far the point is before the line, along the line's forward direction
        /// (positive = not reached yet), and how far it is to the side of the line's centre.
        /// </summary>
        public static float DistanceBefore(Transform line, Vector3 point, out float sideways)
        {
            Vector3 rel = point - line.position;
            sideways = Mathf.Abs(Vector3.Dot(rel, line.right));
            return -Vector3.Dot(rel, line.forward);
        }
    }
}
