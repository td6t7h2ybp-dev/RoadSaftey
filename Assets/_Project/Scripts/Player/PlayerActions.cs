using UnityEngine;
using RoadSafety.AI;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// The player's decision-making, built as a behaviour tree with a Selector at the root.
    /// The Selector tries each branch from top to bottom and runs the first one that works:
    ///   1. Health low    -> Repair
    ///   2. Stamina low   -> Rest
    ///   3. Enemies near  -> Avoid
    ///   4. otherwise     -> Drive
    /// Health, Stamina and EnemyNumber live in the GameManager.
    /// The actions are stubs: they log and change the values a little so the tree
    /// can be tested in Play mode. Replace the TODOs with real gameplay.
    /// </summary>
    public class PlayerActions : MonoBehaviour
    {
        private const float RepairPerSecond = 10f;
        private const float StaminaRecoveryPerSecond = 15f;
        private const float StaminaDrainPerSecond = 3f;

        [Tooltip("Seconds between two evaluations of the tree.")]
        [SerializeField, Min(0.02f)] private float tickInterval = 0.25f;

        [Tooltip("Log in the Console every time the chosen action changes.")]
        [SerializeField] private bool logDecisions = true;

        [Tooltip("Show Health / Stamina / EnemyNumber and the current action in the Game view.")]
        [SerializeField] private bool showDebugOverlay = true;

        private GameManager game;
        private Node root;
        private float timer;

        public string CurrentAction { get; private set; } = "None";

        private void Start()
        {
            game = GameManager.Instance;
            if (game == null)
            {
                Debug.LogError("[PlayerActions] No GameManager found in the scene.", this);
                enabled = false;
                return;
            }
            root = BuildTree();
        }

        private Node BuildTree()
        {
            // Once Repair or Rest has started it continues until the value is full,
            // so the player doesn't flip back and forth around the threshold.
            return new Selector("Root",
                new Sequence("Damaged",
                    new Condition("Health low?", () =>
                        game.IsHealthLow || (CurrentAction == "Repair" && game.Health < GameManager.MaxHealth)),
                    new ActionNode("Repair", Repair)),
                new Sequence("Tired",
                    new Condition("Stamina low?", () =>
                        game.IsStaminaLow || (CurrentAction == "Rest" && game.Stamina < GameManager.MaxStamina)),
                    new ActionNode("Rest", Rest)),
                new Sequence("In danger",
                    new Condition("Enemies near?", () => game.HasEnemies),
                    new ActionNode("Avoid", Avoid)),
                new ActionNode("Drive", Drive));
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < tickInterval)
                return;
            timer = 0f;
            root.Evaluate();
        }

        // ---------- Actions (stubs) ----------

        private NodeState Repair()
        {
            SetAction("Repair");
            // TODO: slow down, pull over safely, hazard lights on, then repair.
            game.Repair(RepairPerSecond * tickInterval);
            return NodeState.Running;
        }

        private NodeState Rest()
        {
            SetAction("Rest");
            // TODO: stop in a safe place (parking / rest area) before resting.
            game.RecoverStamina(StaminaRecoveryPerSecond * tickInterval);
            return NodeState.Running;
        }

        private NodeState Avoid()
        {
            SetAction("Avoid");
            // TODO: slow down, keep a safe distance, change lane only when it is safe.
            return NodeState.Running;
        }

        private NodeState Drive()
        {
            SetAction("Drive");
            // TODO: follow the road, respect the speed limit, traffic lights and crosswalks.
            game.UseStamina(StaminaDrainPerSecond * tickInterval);
            return NodeState.Running;
        }

        private void SetAction(string actionName)
        {
            if (CurrentAction == actionName)
                return;
            CurrentAction = actionName;
            if (logDecisions)
                Debug.Log($"[PlayerActions] {actionName}  (Health {game.Health:0}, Stamina {game.Stamina:0}, Enemies {game.EnemyNumber})", this);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay || game == null)
                return;
            GUI.Box(new Rect(10, 10, 240, 122), "Player behaviour tree");
            GUI.Label(new Rect(20, 32, 220, 20), $"Health: {game.Health:0}");
            GUI.Label(new Rect(20, 50, 220, 20), $"Stamina: {game.Stamina:0}");
            GUI.Label(new Rect(20, 68, 220, 20), $"Enemies: {game.EnemyNumber}");
            GUI.Label(new Rect(20, 86, 220, 20), $"Action: {CurrentAction}");
            GUI.Label(new Rect(20, 106, 220, 20), "H damage | E / R add / remove enemy");
        }
    }
}
