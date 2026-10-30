using UnityEngine;

namespace RoadSafety.World
{
    /// <summary>Keeps a sign or sprite turned towards the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private bool yawOnly = true;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 toCamera = cam.transform.position - transform.position;
            if (yawOnly)
                toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
