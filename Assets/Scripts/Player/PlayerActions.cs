using UnityEngine;
using RoadSafety.AI;
using RoadSafety.Core;
using RoadSafety.UI;
using RoadSafety.World;

namespace RoadSafety.Player
{
    /// <summary>
    /// The player's action management: a behaviour tree whose last branch is a utility function
    /// (slide 4: "Gestion des actions : Fonction utilitaire + BT", US 4.3).
    /// It makes NO decision itself: its brain is the Behavior Designer tree BT_Player
    /// (Assets/_Project/BehaviorTrees, built in Tools > Behavior Designer), plugged into the
    /// External Behavior field of its Behavior Tree component. This script is only the body:
    /// it moves and animates, and answers the questions the tree's tasks ask.
    /// Every real action the child does goes through one central function,
    /// <see cref="DriverControls.Perform"/>. This tree decides which action is the right one
    /// now and gently pulses that button on the HUD, so a 5-year-old knows what to press.
    /// </summary>
    public class PlayerActions : MonoBehaviour
    {
        private const float RepairPerSecond = 10f;
        private const float StaminaRecoveryPerSecond = 15f;
        private const float StaminaDrainPerSecond = 3f;
        private const float LookAhead = 28f; // metres in front of the car where rules matter

        [Tooltip("Seconds between two evaluations of the tree.")]
        [SerializeField, Min(0.02f)] private float tickInterval = 0.25f;

        [Tooltip("Log in the Console every time the chosen action changes.")]
        [SerializeField] private bool logDecisions = true;

        [Tooltip("Show the tree state and the utility score in the Game view (F1).")]
        [SerializeField] private bool showDebugOverlay = true;

        [Tooltip("Pulse the recommended button on the HUD.")]
        [SerializeField] private bool hintOnHud = true;

        [Tooltip("While the child drives without the seatbelt, the voice reminds them every ... seconds.")]
        [SerializeField, Min(2f)] private float beltLessonInterval = 8f;

        private GameManager game;
        private DriverControls controls;
        private CarController car;
        private TrafficLight trafficLight;
        private RedLightRule redLight;
        private CrosswalkRule crosswalk;
        private HUDController hud;
        private BehaviorDesigner.Runtime.BehaviorTree bdTree; // its Behavior Designer tree (custom tasks in AI/BehaviorDesignerTasks)


        private string lastChoice = "-";
        private float lastScore;
        private float timer;
        private TouchButton hinted;
        private float nextBeltLesson;

        public string CurrentAction { get; private set; } = "None";

        /// <summary>The action the utility function picked at the last tick (null: none).</summary>
        public PlayerAction? Recommended { get; private set; }

        private void Start()
        {
            game = GameManager.Instance;
            if (game == null)
            {
                Debug.LogError("[PlayerActions] No GameManager found in the scene.", this);
                enabled = false;
                return;
            }
            car = GetComponent<CarController>();
            if (car == null) car = FindAnyObjectByType<CarController>();
            trafficLight = FindAnyObjectByType<TrafficLight>();
            redLight = FindAnyObjectByType<RedLightRule>();
            crosswalk = FindAnyObjectByType<CrosswalkRule>();
        }


        private void Update()
        {
            if (controls == null) controls = DriverControls.Instance;
            if (hud == null) hud = FindAnyObjectByType<HUDController>();

            timer += Time.deltaTime;
            if (timer < tickInterval)
                return;
            timer = 0f;
            if (bdTree == null) bdTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            BDBridge.Tick(bdTree); // Behavior Designer runs the tree
        }

        // after TouchButton.Update, so the pulse is not smoothed away
        private void LateUpdate() => PulseHint();

        // ------------------------------------------------------------------ utility scores (0..1)

        private float Speed => car != null ? Mathf.Abs(car.Speed) : 0f;

        // Seatbelt off: 1 while stopped (put it on before leaving), 0.7 while driving (a danger, a red light
        // or a pedestrian still win, then the seatbelt comes back).
        public float ScoreSeatbelt()
        {
            if (controls == null || controls.SeatbeltOn) return 0f;
            return Speed < 0.3f ? 1f : 0.7f;
        }

        public float ScoreBrake()
        {
            if (controls == null || Speed < 0.3f)
                return 0f; // not driving yet, or already stopped
            float score = 0f;
            if (game.HasEnemies) score = Mathf.Max(score, 0.9f); // ThreatSensor: something in front
            if (trafficLight != null && trafficLight.Main != LightColor.Green && Ahead(redLight)) score = Mathf.Max(score, 0.85f);
            if (crosswalk != null && crosswalk.BlocksTraffic && Ahead(crosswalk)) score = Mathf.Max(score, 0.8f);
            return score;
        }

        public float ScoreHonk()
        {
            // a short friendly "hello" when stopped and a child is crossing in front (never a weapon, US 1.4)
            if (controls == null || Speed > 0.3f)
                return 0f;
            return crosswalk != null && crosswalk.BlocksTraffic && Ahead(crosswalk) ? 0.4f : 0f;
        }

        private bool Ahead(Component rule)
        {
            if (rule == null || car == null) return false;
            float dz = rule.transform.position.z - car.transform.position.z;
            return dz > -2f && dz < LookAhead;
        }

        // ------------------------------------------------------------------ questions and actions used by the Behavior Designer tasks

        public bool HealthLow() => game != null && (game.IsHealthLow || (CurrentAction == "Repair" && game.Health < GameManager.MaxHealth));
        public bool StaminaLow() => game != null && (game.IsStaminaLow || (CurrentAction == "Rest" && game.Stamina < GameManager.MaxStamina));

        /// <summary>
        /// The child is free to drive without the seatbelt; the tree educates instead: the seatbelt button
        /// is hinted and, while the car moves unbelted, the voice reminds them every few seconds
        /// ("ضع حزام الأمان قبل أن تنطلق!", the friend points at the belt).
        /// </summary>
        public NodeState RecommendSeatbelt()
        {
            if (controls != null && !controls.SeatbeltOn && Speed > 0.3f && Time.time >= nextBeltLesson)
            {
                nextBeltLesson = Time.time + beltLessonInterval;
                controls.RemindSeatbelt();
            }
            return Recommend(PlayerAction.Seatbelt, "Fasten seatbelt");
        }
        public NodeState RecommendBrake() => Recommend(PlayerAction.Brake, "Brake");
        public NodeState RecommendHonk() => Recommend(PlayerAction.Horn, "Honk");

        /// <summary>The Utility Selector's pick (for the log and the F1 overlay).</summary>
        public void NoteChoice(string option, float utility)
        {
            lastChoice = option;
            lastScore = utility;
        }

        // ------------------------------------------------------------------ actions

        private NodeState Recommend(PlayerAction action, string actionName)
        {
            Recommended = action;
            SetAction(actionName);
            return NodeState.Running;
        }

        public NodeState Repair()
        {
            Recommended = null;
            SetAction("Repair");
            game.Repair(RepairPerSecond * tickInterval);
            return NodeState.Running;
        }

        public NodeState Rest()
        {
            Recommended = null;
            SetAction("Rest");
            game.RecoverStamina(StaminaRecoveryPerSecond * tickInterval);
            return NodeState.Running;
        }

        public NodeState Drive()
        {
            Recommended = PlayerAction.Accelerate;
            SetAction("Drive");
            game.UseStamina(StaminaDrainPerSecond * tickInterval);
            return NodeState.Running;
        }

        private void SetAction(string actionName)
        {
            if (CurrentAction == actionName)
                return;
            CurrentAction = actionName;
            if (logDecisions)
            {
                float score = lastScore;
                Debug.Log($"[PlayerActions] {actionName}  (utility {score:0.00}, Health {game.Health:0}, Stamina {game.Stamina:0}, Enemies {game.EnemyNumber})", this);
            }
        }

        // ------------------------------------------------------------------ HUD hint

        private void PulseHint()
        {
            TouchButton target = null;
            if (hintOnHud && hud != null && hud.UI != null && Recommended.HasValue)
            {
                switch (Recommended.Value)
                {
                    case PlayerAction.Seatbelt: target = hud.UI.belt; break;
                    case PlayerAction.Brake: target = hud.UI.stop; break;
                    case PlayerAction.Accelerate:
                        // only hint "Go" while the car stands still (belt on), otherwise it would never stop pulsing
                        target = Speed < 0.3f ? hud.UI.go : null;
                        break;
                }
            }
            if (target != null && !target.isActiveAndEnabled) target = null;

            if (hinted != null && hinted != target && hinted.Visual != null)
                hinted.Visual.localScale = Vector3.one;
            hinted = target;
            if (hinted == null || hinted.Visual == null || hinted.IsHeld)
                return;
            float s = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            hinted.Visual.localScale = new Vector3(s, s, 1f);
        }

        private void OnDisable()
        {
            if (hinted != null && hinted.Visual != null)
                hinted.Visual.localScale = Vector3.one;
            hinted = null;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay || !DebugView.Visible || game == null)
                return;
            float y = DebugView.Top;
            string choice = lastChoice;
            float score = lastScore;
            GUI.Box(new Rect(10, y, 240, 122), "Player BT + utility");
            GUI.Label(new Rect(20, y + 22, 220, 20), $"Health: {game.Health:0}   Stamina: {game.Stamina:0}");
            GUI.Label(new Rect(20, y + 40, 220, 20), $"Enemies: {game.EnemyNumber}");
            GUI.Label(new Rect(20, y + 58, 220, 20), $"Action: {CurrentAction}");
            GUI.Label(new Rect(20, y + 76, 220, 20), $"Utility: {choice} {score:0.00}");
            GUI.Label(new Rect(20, y + 96, 220, 20), "H damage | E / R add / remove enemy");
        }
    }
}
