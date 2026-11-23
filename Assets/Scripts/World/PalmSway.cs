using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.World
{
    /// <summary>Palm leaves swaying gently in the sea breeze.</summary>
    public class PalmSway : MonoBehaviour
    {
        private readonly List<(Transform t, Quaternion rest, float phase)> leaves = new List<(Transform, Quaternion, float)>();

        private void Start()
        {
            foreach (Transform c in transform)
                if (c.name == "Leaf")
                    leaves.Add((c, c.localRotation, Random.value * 6.28f));
        }

        private void Update()
        {
            float wind = Mathf.Sin(Time.time * 0.6f) * 0.5f + 0.5f;
            foreach (var (t, rest, phase) in leaves)
                t.localRotation = rest * Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + phase) * (2f + wind * 3f), 0f, Mathf.Sin(Time.time * 1.1f + phase) * 2f);
        }
    }
}
