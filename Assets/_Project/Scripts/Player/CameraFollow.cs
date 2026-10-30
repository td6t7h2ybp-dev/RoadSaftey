using UnityEngine;

namespace RoadSafety.Player
{
    /// <summary>
    /// Chase camera behind the player's car. The default offset and angle are the framing
    /// Fakhreddine chose (camera 6.1 m behind and 3 m above the car, looking 15° down).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.96f, -6.14f);
        [SerializeField, Range(0f, 60f)] private float pitch = 15f;
        [Tooltip("How quickly the camera catches up with the car.")]
        [SerializeField, Min(0.1f)] private float followSharpness = 6f;
        [Tooltip("How quickly the camera turns behind the car.")]
        [SerializeField, Min(0.1f)] private float turnSharpness = 3f;

        private float yaw;

        public Transform Target
        {
            get => target;
            set { target = value; Snap(); }
        }

        private void Start()
        {
            if (target == null && CarController.Player != null)
                target = CarController.Player.transform;
            Snap();
        }

        private void Snap()
        {
            if (target == null)
                return;
            yaw = target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(target.position + rotation * offset, Quaternion.Euler(pitch, yaw, 0f));
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (CarController.Player == null)
                    return;
                target = CarController.Player.transform;
                Snap();
            }
            float dt = Time.deltaTime;
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-turnSharpness * dt));
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = target.position + rotation * offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * dt));
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
