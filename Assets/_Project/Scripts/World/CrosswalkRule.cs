using UnityEngine;
using RoadSafety.AI;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// US 2.2, a zebra crossing without lights:
    ///   - when the player's car comes near, a pedestrian walks to the kerb and waits;
    ///   - the pedestrian only crosses when every car near the crossing has stopped;
    ///   - if the child stopped to let them cross, the child gets a star and a "thank you";
    ///   - driving over the crossing while the pedestrian waits or crosses costs a heart.
    /// The other cars also stop here while the pedestrian wants to cross (see StopLine).
    /// </summary>
    public class CrosswalkRule : MonoBehaviour
    {
        [SerializeField] private Pedestrian pedestrian;
        [Tooltip("The player's stop line in front of the crossing (blue arrow in the driving direction).")]
        [SerializeField] private Transform stopLine;
        [SerializeField, Min(5f)] private float activateDistance = 95f;
        [Tooltip("Stopping within this distance before the line counts as letting the pedestrian cross (m).")]
        [SerializeField, Min(2f)] private float yieldZone = 40f;
        [SerializeField, Min(5f)] private float warnDistance = 32f;
        [SerializeField, Min(1f)] private float roadHalfWidth = 6f;

        private bool activated;
        private bool yielded;
        private bool violated;
        private bool passed;
        private bool saidWaiting;
        private float previous = float.MaxValue;

        /// <summary>Other cars must stop while the pedestrian wants to cross.</summary>
        public bool BlocksTraffic => pedestrian != null && pedestrian.WantsToCross;

        public void Configure(Pedestrian walker, Transform line)
        {
            pedestrian = walker;
            stopLine = line;
        }

        private void Update()
        {
            CarController car = CarController.Player;
            if (car == null || pedestrian == null || stopLine == null)
                return;

            float d = RoadMath.DistanceBefore(stopLine, car.transform.position, out float sideways);
            bool onThisRoad = sideways <= roadHalfWidth;

            if (!activated && onThisRoad && d > 0f && d < activateDistance)
            {
                activated = true;
                pedestrian.Activate(this);
            }

            if (onThisRoad && pedestrian.WantsToCross && d > 0f && d < yieldZone && car.IsStopped)
                yielded = true;

            if (!saidWaiting && onThisRoad && pedestrian.State == PedestrianState.Waiting && d > 0f && d < warnDistance)
            {
                saidWaiting = true;
                GameMessages.Say(Lines.PedestrianWaiting);
            }

            if (!passed && onThisRoad && previous > 0f && d <= 0f)
            {
                passed = true;
                if (pedestrian.WantsToCross)
                {
                    violated = true;
                    // Say it first: on the last heart the gentle "try again" must be heard last.
                    GameMessages.Say(Lines.PedestrianViolation);
                    if (ProgressionManager.Instance != null)
                        ProgressionManager.Instance.LoseHeart(Mistake.DidNotLetPedestriansCross);
                    pedestrian.GetScared();
                }
            }
            previous = d;
        }

        /// <summary>Called by the pedestrian when they reach the other side.</summary>
        public void OnPedestrianCrossed()
        {
            if (violated || !yielded)
                return;
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.AddStarOnce("pedestrian", GoodAction.LetPedestriansCross);
            GameMessages.Say(Lines.PedestrianThanks);
        }

        /// <summary>The pedestrian asks: have all the cars near the crossing stopped (or passed)?</summary>
        public bool IsSafeToCross()
        {
            // The lesson is for the child driving: the pedestrian waits at the kerb
            // until the player's car has stopped to let her cross (or has gone past).
            CarController car = CarController.Player;
            if (car != null && stopLine != null && !passed)
            {
                float d = RoadMath.DistanceBefore(stopLine, car.transform.position, out float sideways);
                bool playerStoppedForMe = d > 0f && d < yieldZone && car.IsStopped;
                if (sideways <= roadHalfWidth && !playerStoppedForMe)
                    return false;
            }

            foreach (Vehicle vehicle in Vehicle.All)
            {
                if (!vehicle.isActiveAndEnabled)
                    continue;
                vehicle.Measure(transform.position, out float ahead, out float sideways);
                if (ahead < -2f || ahead > 30f)
                    continue; // already past, or far away
                if (Mathf.Abs(sideways) > roadHalfWidth)
                    continue; // not on this road
                if (!vehicle.IsStopped)
                    return false;
            }
            return true;
        }

        /// <summary>A car is about to drive over the crossing (the pedestrian steps back).</summary>
        public bool CarRushing()
        {
            foreach (Vehicle vehicle in Vehicle.All)
            {
                if (!vehicle.isActiveAndEnabled)
                    continue;
                vehicle.Measure(transform.position, out float ahead, out float sideways);
                if (ahead > -1f && ahead < 9f && Mathf.Abs(sideways) < roadHalfWidth && vehicle.Speed > 2f)
                    return true;
            }
            return false;
        }
    }
}
