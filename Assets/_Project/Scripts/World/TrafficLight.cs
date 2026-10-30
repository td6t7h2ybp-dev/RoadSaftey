using System;
using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.World
{
    public enum LightColor
    {
        Red,
        Amber,
        Green
    }

    /// <summary>
    /// Traffic light of a crossroads (US 2.1). Two groups: the main road (the child's road)
    /// and the side road, always opposite. The colours change in a loop:
    /// main green → main amber → all red → side green → side amber → all red → ...
    /// When the child comes near, <see cref="SkipToAmber"/> is called so the child always
    /// meets a red light and can practise stopping.
    /// </summary>
    public class TrafficLight : MonoBehaviour
    {
        [Serializable]
        public class LampSet
        {
            public Renderer red;
            public Renderer amber;
            public Renderer green;
        }

        [Header("Durations (seconds)")]
        [SerializeField, Min(0.5f)] private float mainGreenTime = 8f;
        [SerializeField, Min(0.5f)] private float amberTime = 2f;
        [SerializeField, Min(0f)] private float allRedTime = 1.2f;
        [SerializeField, Min(0.5f)] private float sideGreenTime = 5f;

        [Header("Lamps")]
        [SerializeField] private LampSet[] mainHeads = new LampSet[0];
        [SerializeField] private LampSet[] sideHeads = new LampSet[0];
        [SerializeField] private Material offMaterial;
        [SerializeField] private Material redMaterial;
        [SerializeField] private Material amberMaterial;
        [SerializeField] private Material greenMaterial;

        private int phase;
        private float timer;

        public LightColor Main { get; private set; } = LightColor.Green;
        public LightColor Side { get; private set; } = LightColor.Red;

        /// <summary>Raised when the main-road colour changes.</summary>
        public event Action<LightColor> MainChanged;

        public void Configure(LampSet[] main, LampSet[] side, Material off, Material red, Material amber, Material green)
        {
            mainHeads = main;
            sideHeads = side;
            offMaterial = off;
            redMaterial = red;
            amberMaterial = amber;
            greenMaterial = green;
        }

        private void Start() => SetPhase(0);

        /// <summary>If the main road is green, start the change to red now.</summary>
        public void SkipToAmber()
        {
            if (phase == 0)
                SetPhase(1);
        }

        /// <summary>
        /// Makes sure the child meets a red light (US 2.1): a green light turns amber now,
        /// and a red light that is about to end stays red a little longer
        /// (the side road gets its green again).
        /// </summary>
        public void TurnRedSoon(float minRedSeconds = 6f)
        {
            if (phase == 0)
            {
                SetPhase(1);
                return;
            }
            if (phase == 1)
                return;

            float remaining = Duration(phase) - timer;
            for (int p = phase + 1; p < 6; p++)
                remaining += Duration(p);
            if (remaining < minRedSeconds)
                SetPhase(3);
        }

        private float Duration(int p)
        {
            float d;
            switch (p)
            {
                case 0: d = mainGreenTime; break;
                case 1: d = amberTime; break;
                case 3: d = sideGreenTime; break;
                case 4: d = amberTime; break;
                default: d = allRedTime; break;
            }
            return d * LevelSession.LightTimeScale;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= Duration(phase))
                SetPhase((phase + 1) % 6);
        }

        private void SetPhase(int p)
        {
            phase = p;
            timer = 0f;
            LightColor oldMain = Main;
            Main = p == 0 ? LightColor.Green : p == 1 ? LightColor.Amber : LightColor.Red;
            Side = p == 3 ? LightColor.Green : p == 4 ? LightColor.Amber : LightColor.Red;
            Apply(mainHeads, Main);
            Apply(sideHeads, Side);
            if (Main != oldMain)
                MainChanged?.Invoke(Main);
        }

        private void Apply(LampSet[] heads, LightColor color)
        {
            if (heads == null)
                return;
            foreach (LampSet head in heads)
            {
                if (head == null)
                    continue;
                Set(head.red, color == LightColor.Red ? redMaterial : offMaterial);
                Set(head.amber, color == LightColor.Amber ? amberMaterial : offMaterial);
                Set(head.green, color == LightColor.Green ? greenMaterial : offMaterial);
            }
        }

        private static void Set(Renderer lamp, Material material)
        {
            if (lamp != null && material != null)
                lamp.sharedMaterial = material;
        }
    }
}
