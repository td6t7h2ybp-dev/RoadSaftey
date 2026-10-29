using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadSafety.Core
{
    /// <summary>
    /// Stars, hearts and the list of good actions / mistakes for the current level
    /// (Epic 3: US 3.1 stars, US 3.4 hearts; the lists feed the end-of-level screen, US 3.3).
    ///
    /// The world scripts only CALL it, for example:
    /// <code>
    ///     ProgressionManager.Instance.AddStar(GoodAction.StoppedAtRedLight);   // good behaviour
    ///     ProgressionManager.Instance.RemoveStar(Mistake.Collision);           // soft penalty
    ///     ProgressionManager.Instance.LoseHeart(Mistake.RanRedLight);          // one try lost
    /// </code>
    /// The HUD only LISTENS to the events. One ProgressionManager is added automatically
    /// to every scene that has a GameManager (see GameplayBootstrap).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        [Header("Hearts (US 3.4)")]
        [SerializeField, Min(1)] private int maxHearts = 3;

        [Tooltip("Seconds the gentle 'let's try again' screen stays before the level restarts.")]
        [SerializeField, Min(0f)] private float restartDelay = 2.5f;

        [Header("Testing")]
        [Tooltip("Play mode keys: 1 = add a star, 2 = remove a star, 3 = lose a heart.")]
        [SerializeField] private bool debugKeys = true;

        private readonly List<string> goodActions = new List<string>();
        private readonly List<string> mistakes = new List<string>();
        private readonly HashSet<string> awardedOnce = new HashSet<string>();

        public int Stars { get; private set; }
        public int Hearts { get; private set; }
        public int MaxHearts => maxHearts;

        /// <summary>Every good action of this level, in order (Arabic text, for the recap screen).</summary>
        public IReadOnlyList<string> GoodActions => goodActions;

        /// <summary>Every mistake of this level, in order (Arabic text, for the recap screen).</summary>
        public IReadOnlyList<string> Mistakes => mistakes;

        /// <summary>True while the level is about to reload (no hearts left). Controls are locked.</summary>
        public bool IsRestarting { get; private set; }

        /// <summary>True once EndLevel() was called.</summary>
        public bool IsLevelOver { get; private set; }

        /// <summary>(new total, reason). The HUD shows a flying star + joyful sound.</summary>
        public event Action<int, string> StarGained;

        /// <summary>(new total, reason). The total never goes below 0.</summary>
        public event Action<int, string> StarLost;

        /// <summary>(hearts left).</summary>
        public event Action<int> HeartsChanged;

        /// <summary>No hearts left: the HUD shows a soft message, then the level restarts by itself.</summary>
        public event Action OutOfHearts;

        /// <summary>The level is finished (Day 2: the end-of-level screen listens to this).</summary>
        public event Action LevelEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ProgressionManager] More than one ProgressionManager in the scene; removing the extra one.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            Hearts = maxHearts;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private bool Locked => IsRestarting || IsLevelOver;

        // ---------- Stars ----------

        /// <summary>A good behaviour: +1 star.</summary>
        public void AddStar(string reason)
        {
            if (Locked)
                return;
            Stars++;
            goodActions.Add(reason);
            StarGained?.Invoke(Stars, reason);
        }

        /// <summary>Gives the star only the first time this key is used in the level (e.g. "seatbelt").</summary>
        public bool AddStarOnce(string key, string reason)
        {
            if (Locked || !awardedOnce.Add(key))
                return false;
            AddStar(reason);
            return true;
        }

        /// <summary>A mistake: -1 star (never below 0) and the mistake is remembered for the recap.</summary>
        public void RemoveStar(string reason)
        {
            if (Locked)
                return;
            mistakes.Add(reason);
            Stars = Mathf.Max(0, Stars - 1);
            StarLost?.Invoke(Stars, reason);
        }

        // ---------- Hearts ----------

        /// <summary>One try lost. At 0 hearts the level restarts from the beginning (no game-over screen).</summary>
        public void LoseHeart(string reason)
        {
            if (Locked || Hearts <= 0)
                return;
            Hearts--;
            mistakes.Add(reason);
            HeartsChanged?.Invoke(Hearts);

            if (Hearts == 0)
            {
                IsRestarting = true;
                OutOfHearts?.Invoke();
                StartCoroutine(RestartAfterDelay());
            }
        }

        // ---------- Level ----------

        /// <summary>Call when the player reaches the end of the road.</summary>
        public void EndLevel()
        {
            if (Locked)
                return;
            IsLevelOver = true;
            LevelEnded?.Invoke();
        }

        /// <summary>Reloads the current scene (everything starts again: stars, hearts, seatbelt).</summary>
        public void RestartLevel()
        {
            IsRestarting = true;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(restartDelay);
            RestartLevel();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!debugKeys)
                return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) AddStar("Debug +1");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RemoveStar("Debug -1");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) LoseHeart("Debug heart");
#endif
        }
    }

    /// <summary>Good actions (Modern Standard Arabic, shown on the recap screen). Use these so the texts stay consistent.</summary>
    public static class GoodAction
    {
        public const string Seatbelt = "وضعت حزام الأمان";
        public const string StoppedAtRedLight = "توقفت عند الضوء الأحمر";
        public const string LetPedestriansCross = "تركت المشاة يعبرون";
        public const string SlowedDownForDanger = "خففت السرعة أمام الخطر";
    }

    /// <summary>Mistakes (Modern Standard Arabic, shown on the recap screen).</summary>
    public static class Mistake
    {
        public const string Collision = "اصطدمت بشيء";
        public const string RanRedLight = "لم تتوقف عند الضوء الأحمر";
        public const string DidNotLetPedestriansCross = "لم تترك المشاة يعبرون";
    }
}
