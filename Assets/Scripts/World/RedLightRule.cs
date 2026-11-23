using UnityEngine;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// US 2.1, for the player's car at one traffic light:
    ///   - when the car comes near, the light turns amber then red;
    ///   - stopping before the line while it is red, then leaving on green = good behaviour (+1 star);
    ///   - crossing the line while it is red = mistake (one heart lost + voice message).
    /// </summary>
    public class RedLightRule : MonoBehaviour
    {
        [SerializeField] private TrafficLight trafficLight;
        [Tooltip("The stop line, blue arrow in the driving direction.")]
        [SerializeField] private Transform stopLine;
        [Tooltip("Distance before the line where the light starts changing to red (m).")]
        [SerializeField, Min(5f)] private float triggerDistance = 34f;
        [Tooltip("Stopping within this distance before the line counts as waiting (m). Same as the trigger distance, so stopping early still counts.")]
        [SerializeField, Min(2f)] private float waitZone = 34f;
        [Tooltip("Distance before the line where the avatar says the light is red (m).")]
        [SerializeField, Min(2f)] private float warnDistance = 26f;
        [SerializeField, Min(1f)] private float roadHalfWidth = 6f;

        private bool triggered;
        private bool warnedRed;
        private bool waited;
        private bool saidGreen;
        private bool done;
        private float previous = float.MaxValue;

        public void Configure(TrafficLight light, Transform line)
        {
            trafficLight = light;
            stopLine = line;
        }

        private void Update()
        {
            CarController car = CarController.Player;
            if (car == null || trafficLight == null || stopLine == null || done)
                return;

            float d = RoadMath.DistanceBefore(stopLine, car.transform.position, out float sideways);
            if (sideways > roadHalfWidth)
            {
                previous = d;
                return;
            }

            if (!triggered && d > 0f && d < triggerDistance)
            {
                triggered = true;
                trafficLight.TurnRedSoon();
            }
            if (!warnedRed && d > 0f && d < warnDistance && trafficLight.Main != LightColor.Green)
            {
                warnedRed = true;
                GameMessages.Say(Lines.LightTurnsRed);
            }
            if (d > 0f && d < waitZone && trafficLight.Main == LightColor.Red && car.IsStopped)
                waited = true;
            if (waited && !saidGreen && trafficLight.Main == LightColor.Green)
            {
                saidGreen = true;
                GameMessages.Say(Lines.LightGreenGo);
            }

            if (previous > 0f && d <= 0f)
            {
                done = true;
                ProgressionManager progression = ProgressionManager.Instance;
                if (trafficLight.Main == LightColor.Red)
                {
                    // Say it first: on the last heart the gentle "try again" must be heard last.
                    GameMessages.Say(Lines.RanRedLight);
                    if (progression != null)
                        progression.LoseHeart(Mistake.RanRedLight);
                }
                else if (waited)
                {
                    if (progression != null)
                        progression.AddStarOnce("red_light", GoodAction.StoppedAtRedLight);
                    GameMessages.Say(Lines.WellDoneRedLight);
                }
            }
            previous = d;
        }
    }
}
