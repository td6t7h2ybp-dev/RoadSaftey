using System;
using UnityEngine;
using RoadSafety.AI;
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
    /// The cycle is run by a Behavior Designer tree (one branch per phase):
    /// <code>
    /// Selector "Traffic light"
    /// ├─ Sequence "Main road green" : Main road's turn?   → Keep main green, then amber
    /// ├─ Sequence "Main road amber" : Main road ending?   → Keep main amber (orange), then red
    /// ├─ Sequence "All red (1)"     : Clearing the crossing? → Keep all red, then side green
    /// ├─ Sequence "Side road green" : Side road's turn?   → Keep side green, then amber
    /// ├─ Sequence "Side road amber" : Side road ending?   → Keep side amber (orange), then red
    /// └─ Sequence "All red (2)"     : Clearing the crossing again? → Keep all red, then main green
    /// </code>
    /// </summary>
    public class TrafficLight : MonoBehaviour, IBTAgent
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
        private Node root;
        private BTBindings bindings;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree;
        private Material orange;

        /// <summary>IBTAgent: the conditions and actions the Behavior Designer tasks call by name.</summary>
        public BTBindings Bindings => bindings ?? (root != null ? bindings = BTBindings.From(root) : null);

        /// <summary>IBTAgent: the light's tree, as saved into the Behavior Designer component.</summary>
        public Node DescribeTree() => BuildTree();

        private Node BuildTree() => new Selector("Traffic light",
            Phase("Main road green", "Main road's turn?", "Keep main green, then amber", 0),
            Phase("Main road amber", "Main road ending?", "Keep main amber (orange), then red", 1),
            Phase("All red (1)", "Clearing the crossing?", "Keep all red, then side green", 2),
            Phase("Side road green", "Side road's turn?", "Keep side green, then amber", 3),
            Phase("Side road amber", "Side road ending?", "Keep side amber (orange), then red", 4),
            Phase("All red (2)", "Clearing the crossing again?", "Keep all red, then main green", 5));

        private Node Phase(string name, string question, string action, int p) =>
            new Sequence(name,
                new Condition(question, () => phase == p),
                new ActionNode(action, () => Hold(p)));

        /// <summary>Keeps the current colours; when the time is up, moves to the next phase.</summary>
        private NodeState Hold(int p)
        {
            timer += Time.deltaTime;
            if (timer >= Duration(p))
            {
                SetPhase((p + 1) % 6);
                return NodeState.Success;
            }
            return NodeState.Running;
        }

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

        private void Awake()
        {
            root = BuildTree();
            // a clear orange (the old amber looked yellow-red next to the golden light)
            if (amberMaterial != null)
            {
                orange = new Material(amberMaterial) { name = "M_Lamp_Orange (runtime)" };
                Color baseOrange = new Color(1f, 0.5f, 0.02f);
                if (orange.HasProperty("_BaseColor")) orange.SetColor("_BaseColor", baseOrange);
                if (orange.HasProperty("_EmissionColor")) orange.SetColor("_EmissionColor", new Color(1f, 0.42f, 0f) * 4f);
                orange.EnableKeyword("_EMISSION");
            }
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
                case 1: d = Mathf.Max(amberTime, 3f); break;
                case 3: d = sideGreenTime; break;
                case 4: d = Mathf.Max(amberTime, 3f); break;
                default: d = allRedTime; break;
            }
            return d * LevelSession.LightTimeScale;
        }

        private void Update()
        {
            if (bdTree == null) bdTree = BDBridge.Ensure(this, root, "Traffic light");
            BDBridge.Tick(bdTree); // Behavior Designer runs the cycle
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
                Set(head.amber, color == LightColor.Amber ? (orange != null ? orange : amberMaterial) : offMaterial);
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
