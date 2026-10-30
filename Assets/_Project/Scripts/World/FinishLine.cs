using UnityEngine;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// End of the street (under the gate "وصلت بالسلامة!"): when the car passes the line,
    /// the level ends and the summary screen opens (US 3.3).
    /// The object's blue arrow points in the player's driving direction.
    /// </summary>
    public class FinishLine : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float roadHalfWidth = 6f;

        private float previous = float.MaxValue;
        private bool done;

        private void Update()
        {
            CarController car = CarController.Player;
            if (car == null || done)
                return;
            float d = RoadMath.DistanceBefore(transform, car.transform.position, out float sideways);
            if (sideways <= roadHalfWidth && previous > 0f && d <= 0f)
            {
                done = true;
                GameMessages.Say(Lines.Finish);
                if (ProgressionManager.Instance != null)
                    ProgressionManager.Instance.EndLevel();
            }
            previous = d;
        }
    }
}
