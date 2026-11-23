using UnityEngine;
using RoadSafety.Core;
using RoadSafety.World;
using RoadSafety.AI;

namespace RoadSafety.Player
{
    /// <summary>
    /// Counts the dangers right in front of the player's car (cars, pedestrians on the road,
    /// the ball) and writes the number into GameManager.EnemyNumber. The teacher's behaviour
    /// tree in PlayerActions then switches to "Avoid", and the HUD shows a warning sign.
    /// If an oncoming car is in the player's lane, the avatar says "انتبه! سيّارة أمامك".
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class ThreatSensor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 18f;
        [SerializeField, Min(0.5f)] private float halfWidth = 1.8f;
        [SerializeField, Min(0.05f)] private float interval = 0.2f;
        [SerializeField, Min(0f)] private float warningCooldown = 8f;

        private CarController car;
        private float timer;
        private float nextWarning;

        private void Awake() => car = GetComponent<CarController>();

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval)
                return;
            timer = 0f;

            int threats = 0;
            bool headOn = false;

            foreach (Vehicle other in Vehicle.All)
            {
                if (other == car || !other.isActiveAndEnabled)
                    continue;
                if (!InPath(other.transform.position))
                    continue;
                threats++;
                if (Vector3.Dot(other.transform.forward, transform.forward) < -0.5f)
                    headOn = true;
            }
            foreach (Pedestrian pedestrian in Pedestrian.All)
                if (pedestrian.IsOnRoad && InPath(pedestrian.transform.position))
                    threats++;
            foreach (Ball ball in Ball.All)
                if (ball.IsRolling && InPath(ball.transform.position))
                    threats++;

            GameManager game = GameManager.Instance;
            if (game != null && game.EnemyNumber != threats)
                game.SetEnemyNumber(threats);

            if (headOn && car.Speed > 1f && Time.time >= nextWarning)
            {
                nextWarning = Time.time + warningCooldown;
                GameMessages.Say(Lines.WrongLane);
            }
        }

        private bool InPath(Vector3 point)
        {
            car.Measure(point, out float ahead, out float sideways);
            return ahead > 1f && ahead < range && Mathf.Abs(sideways) < halfWidth;
        }
    }
}
