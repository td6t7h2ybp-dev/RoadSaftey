using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.AI
{
    /// <summary>
    /// Turns on as many oncoming cars as the level asks for (more traffic in level 2 and 3)
    /// and sets their speed.
    /// </summary>
    public class TrafficDirector : MonoBehaviour
    {
        [SerializeField] private NpcCar[] oncomingCars = new NpcCar[0];
        [SerializeField] private NpcCar[] otherCars = new NpcCar[0];

        public void Configure(NpcCar[] oncoming, NpcCar[] others)
        {
            oncomingCars = oncoming;
            otherCars = others;
        }

        private void Awake()
        {
            int count = LevelSession.OncomingCars;
            for (int i = 0; i < oncomingCars.Length; i++)
            {
                if (oncomingCars[i] == null)
                    continue;
                oncomingCars[i].gameObject.SetActive(i < count);
                oncomingCars[i].CruiseSpeed = LevelSession.TrafficSpeed;
            }
            foreach (NpcCar car in otherCars)
                if (car != null)
                    car.CruiseSpeed = LevelSession.TrafficSpeed;
        }
    }
}
