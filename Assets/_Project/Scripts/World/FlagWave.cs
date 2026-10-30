using UnityEngine;

namespace RoadSafety.World
{
    /// <summary>Gentle wave for flags and banners.</summary>
    public class FlagWave : MonoBehaviour
    {
        [SerializeField] private float amplitude = 6f;
        [SerializeField] private float speed = 2.2f;

        private Quaternion rest;
        private float offset;

        private void Awake()
        {
            rest = transform.localRotation;
            offset = Random.value * 10f;
        }

        private void Update()
        {
            float a = Mathf.Sin((Time.time + offset) * speed) * amplitude;
            transform.localRotation = rest * Quaternion.Euler(0f, a, 0f);
        }
    }
}
