using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.World
{
    /// <summary>
    /// Anything that drives (the player's car, the other cars). Pedestrians and the other
    /// cars use <see cref="All"/> to know who is coming and how fast.
    /// </summary>
    public abstract class Vehicle : MonoBehaviour
    {
        private static readonly List<Vehicle> registered = new List<Vehicle>();

        public static IReadOnlyList<Vehicle> All => registered;

        /// <summary>Forward speed in m/s (negative when bouncing back).</summary>
        public abstract float Speed { get; }

        public bool IsStopped => Mathf.Abs(Speed) < 0.35f;

        protected virtual void OnEnable() => registered.Add(this);

        protected virtual void OnDisable() => registered.Remove(this);

        /// <summary>Distance in front of this vehicle to a point (negative = behind), and sideways offset.</summary>
        public void Measure(Vector3 point, out float ahead, out float sideways)
        {
            Vector3 rel = point - transform.position;
            ahead = Vector3.Dot(rel, transform.forward);
            sideways = Vector3.Dot(rel, transform.right);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();
    }
}
