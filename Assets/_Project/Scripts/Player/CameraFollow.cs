using UnityEngine;

namespace RoadSafety.Player
{
    /// <summary>
    /// The player's camera. By default it is the child's eyes inside the car (first-person):
    /// it follows the driver's eye point of <see cref="CarInterior"/> and looks where the
    /// interior says (down at the seatbelt until it is on, into the turns, at the friend).
    /// V (keyboard, for testing) switches to the chase camera behind the car: the framing
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

        [Header("First person (the child's eyes)")]
        [SerializeField] private bool firstPerson = true;
        [SerializeField, Range(40f, 100f)] private float firstPersonFov = 80f;
        [Tooltip("How quickly the eyes turn to where the driver looks.")]
        [SerializeField, Min(0.1f)] private float lookSharpness = 4f;

        private float yaw;
        private float lookYaw, lookPitch;
        private Camera cam;
        private bool chaseSettingsSaved;
        private float chaseFov, chaseNear;
        private bool inFirstPerson;

        public bool FirstPerson
        {
            get => firstPerson;
            set => firstPerson = value;
        }

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
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.V))
                firstPerson = !firstPerson;
#endif
            CarInterior interior = CarInterior.Current;
            if (firstPerson && interior != null && interior.Eye != null)
            {
                FollowEyes(interior);
                return;
            }
            if (inFirstPerson)
            {
                RestoreChaseLens();
                Snap();
            }
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
    
        private void FollowEyes(CarInterior interior)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (!inFirstPerson)
            {
                if (cam != null && !chaseSettingsSaved)
                {
                    chaseFov = cam.fieldOfView;
                    chaseNear = cam.nearClipPlane;
                    chaseSettingsSaved = true;
                }
                interior.GetLook(out lookYaw, out lookPitch);
                inFirstPerson = true;
            }
            if (cam != null)
            {
                cam.nearClipPlane = 0.02f;
                cam.fieldOfView = Mathf.Max(firstPersonFov, 84f); // the cockpit needs this much to keep the friend in frame
            }

            interior.GetLook(out float wantYaw, out float wantPitch);
            float k = 1f - Mathf.Exp(-lookSharpness * Time.deltaTime);
            lookYaw = Mathf.LerpAngle(lookYaw, wantYaw, k);
            lookPitch = Mathf.Lerp(lookPitch, wantPitch, k);

            Transform eye = interior.Eye;
            float carYaw = (target != null ? target : interior.transform).eulerAngles.y;
            transform.SetPositionAndRotation(eye.position, Quaternion.Euler(0f, carYaw + lookYaw, 0f) * Quaternion.Euler(lookPitch, 0f, 0f));
        }

        private void RestoreChaseLens()
        {
            inFirstPerson = false;
            if (cam != null && chaseSettingsSaved)
            {
                cam.fieldOfView = chaseFov;
                cam.nearClipPlane = chaseNear;
            }
        }
    }
}
