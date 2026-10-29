using System;
using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Central game state for a level. The player's behaviour tree (PlayerActions)
    /// reads Health, Stamina and EnemyNumber from here to decide what to do.
    /// Keep exactly one GameManager per scene (Main.unity: _Managers/GameManager).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public const float MaxHealth = 100f;
        public const float MaxStamina = 100f;

        public static GameManager Instance { get; private set; }

        [Header("Behaviour-tree variables")]
        [Tooltip("Condition of the player's car. 0 = crashed / game over.")]
        [SerializeField, Range(0f, MaxHealth)] private float health = MaxHealth;

        [Tooltip("Driver focus / energy. Goes down while driving, comes back when resting.")]
        [SerializeField, Range(0f, MaxStamina)] private float stamina = MaxStamina;

        [Tooltip("How many threats are near the player (reckless drivers, obstacles...).")]
        [SerializeField, Min(0)] private int enemyNumber = 0;

        [Header("Thresholds")]
        [SerializeField, Range(0f, MaxHealth)] private float lowHealthThreshold = 30f;
        [SerializeField, Range(0f, MaxStamina)] private float lowStaminaThreshold = 25f;

        [Header("Testing")]
        [Tooltip("Play mode keys: H = damage, E = add enemy, R = remove enemy.")]
        [SerializeField] private bool debugKeys = true;

        public float Health => health;
        public float Stamina => stamina;
        public int EnemyNumber => enemyNumber;

        public bool IsHealthLow => health <= lowHealthThreshold;
        public bool IsStaminaLow => stamina <= lowStaminaThreshold;
        public bool HasEnemies => enemyNumber > 0;
        public bool IsGameOver => health <= 0f;

        /// <summary>Raised when Health, Stamina or EnemyNumber changes (hook the HUD here).</summary>
        public event Action StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] More than one GameManager in the scene; removing the extra one.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!debugKeys)
                return;
            if (Input.GetKeyDown(KeyCode.H)) TakeDamage(20f);
            if (Input.GetKeyDown(KeyCode.E)) AddEnemy();
            if (Input.GetKeyDown(KeyCode.R)) RemoveEnemy();
#endif
        }

        // ---------- Health ----------
        public void TakeDamage(float amount) => SetHealth(health - amount);
        public void Repair(float amount) => SetHealth(health + amount);

        // ---------- Stamina ----------
        public void UseStamina(float amount) => SetStamina(stamina - amount);
        public void RecoverStamina(float amount) => SetStamina(stamina + amount);

        // ---------- Enemies ----------
        public void AddEnemy() => SetEnemyNumber(enemyNumber + 1);
        public void RemoveEnemy() => SetEnemyNumber(enemyNumber - 1);

        public void SetEnemyNumber(int count)
        {
            enemyNumber = Mathf.Max(0, count);
            StateChanged?.Invoke();
        }

        private void SetHealth(float value)
        {
            bool wasGameOver = IsGameOver;
            health = Mathf.Clamp(value, 0f, MaxHealth);
            StateChanged?.Invoke();

            if (IsGameOver && !wasGameOver)
            {
                // TODO: game over screen / restart the level.
                Debug.Log("[GameManager] Game over: Health reached 0.");
            }
        }

        private void SetStamina(float value)
        {
            stamina = Mathf.Clamp(value, 0f, MaxStamina);
            StateChanged?.Invoke();
        }

        // TODO (team): score, road-safety infractions (red light, speeding, crosswalk),
        //              level timer, win condition, pause.
    }
}
