using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RoadSafety.Core;
using RoadSafety.Player;
using RoadSafety.World;

namespace RoadSafety.AI.Pedestrians
{
    public enum PedestrianKind { DistractedPhone, UnpredictableChild, BehindTheBus, OldMan }

    public enum PedestrianPoint { StartKerb, FarKerb, NearestKerb, HideSpot, Exit }

    /// <summary>
    /// Body and memory of an evaluation pedestrian (PNJ behaviours 1, 2 and 4).
    /// It makes NO decision: the Behavior Designer tree on the same object (Behavior Tree
    /// component, made of the tasks in PedestrianTasks.cs) decides everything and calls this
    /// component to move, remember and animate.
    /// Select the pedestrian and open Tools > Behavior Designer > Editor to see / edit its tree.
    /// </summary>
    [DisallowMultipleComponent]
    public class PedestrianAgent : MonoBehaviour
    {
        [Header("Who")]
        public PedestrianKind kind;

        [Header("Points")]
        public Transform startKerb;
        public Transform farKerb;
        public Transform hideSpot;
        public Transform exitPoint;

        [Header("Voice")]
        [Tooltip("Resources path of the remark the old man makes when the player honks at him.")]
        public string remarkVoice = "Audio/VO/oldman_remark";

        // ---------- memory (read / written by the Behavior Designer tasks)
        [System.NonSerialized] public bool onRoad;
        [System.NonSerialized] public bool startled;
        [System.NonSerialized] public bool gone;
        [System.NonSerialized] public float waited;      // seconds on the kerb ("temps écoulé")
        [System.NonSerialized] public float roll;        // "aléatoire" 0..1
        [System.NonSerialized] public float stateTime;
        [System.NonSerialized] public float greenAt = -99f;
        [System.NonSerialized] public float honkedAt = -99f;   // last time the player honked nearby
        [System.NonSerialized] public bool raisingHand;        // old man: "wait, I'm crossing"
        [System.NonSerialized] public bool remarking;          // old man: telling the driver off
        [System.NonSerialized] public string currentTask = "-";
        public readonly Dictionary<string, float> utilities = new Dictionary<string, float>();

        private BehaviorDesigner.Runtime.BehaviorTree tree;
        private Transform legL, legR, armL, armR, upper;
        private Vector3 upperRest;
        private float walkPhase;
        private bool moving;
        private bool hitCounted;
        private TrafficLight trafficLight;
        private LightColor lastMain = LightColor.Green;
        private TrafficLight hooked;
        private DriverControls controls;
        private AudioSource voice;

        public void Configure(PedestrianKind who, Transform start, Transform far, Transform hide = null, Transform exit = null)
        {
            kind = who;
            startKerb = start;
            farKerb = far;
            hideSpot = hide;
            exitPoint = exit;
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            tree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            legL = transform.Find("Leg_L");
            legR = transform.Find("Leg_R");
            upper = transform.Find("Upper");
            armL = transform.Find("Upper/Arm_L");
            armR = transform.Find("Upper/Arm_R");
            if (upper != null) upperRest = upper.localPosition;
            roll = Random.value;
        }

        private void Start()
        {
            if (kind == PedestrianKind.DistractedPhone) AddPhone();
            if (kind == PedestrianKind.BehindTheBus && hideSpot != null)
            {
                transform.position = hideSpot.position;
                SnapToGround();
            }
            HookLight();
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0.6f;
            voice.minDistance = 6f;
            voice.maxDistance = 60f;
            if (tree == null)
                Debug.LogWarning($"[{name}] has no Behavior Designer tree: run RoadSafety > AI > Build NPC scenarios (1, 2, 4).");
        }

        private void OnDestroy()
        {
            if (hooked != null) hooked.MainChanged -= OnLightChanged;
            if (controls != null) controls.Honked -= OnHonk;
        }

        private void OnHonk() => honkedAt = Time.time;

        /// <summary>Plays the recorded remark (voice only, no text on screen).</summary>
        public float SayRemark()
        {
            AudioClip clip = Resources.Load<AudioClip>(remarkVoice);
            if (clip != null && voice != null && AudioPrefs.SfxOn)
            {
                voice.Stop();
                voice.clip = clip;
                voice.Play();
                return clip.length;
            }
            return 2.5f;
        }

        private void Update()
        {
            stateTime += Time.deltaTime;
            moving = false;
            if (controls == null && DriverControls.Instance != null)
            {
                controls = DriverControls.Instance;
                controls.Honked += OnHonk;
            }

            // The other agents of the game tick Behavior Designer by hand (BehaviorManager in Manual mode):
            // in that case this tree is ticked here too, otherwise Behavior Designer runs it on its own.
            var manager = BehaviorDesigner.Runtime.BehaviorManager.instance;
            if (tree != null && tree.isActiveAndEnabled && manager != null && manager.UpdateInterval == BehaviorDesigner.Runtime.UpdateIntervalType.Manual)
                manager.Tick(tree);

            CheckHit();
            Animate();
        }

        // ------------------------------------------------------------------ world (asked by the tasks)

        public static CarController Car => CarController.Player;

        public TrafficLight Light => trafficLight != null ? trafficLight : (trafficLight = FindFirstObjectByType<TrafficLight>());

        public bool LightIs(LightColor c) => Light != null && Light.Main == c;

        public static float PlayerSpeedPercent =>
            Car == null || Car.MaxSpeed <= 0f ? 0f : Mathf.Abs(Car.Speed) / Car.MaxSpeed * 100f;

        /// <summary>How far the player's car is before me (the road runs along +Z). Negative: already past.</summary>
        public float PlayerDistance => Car == null ? float.MaxValue : transform.position.z - Car.transform.position.z;

        public static bool PlayerOnMainRoad => Car != null && Mathf.Abs(Car.transform.position.x) < 7f;

        public bool CarAboutToHitMe()
        {
            if (Car == null || Car.Speed < 1.2f) return false;
            Vector3 me = transform.position, car = Car.transform.position;
            float dz = me.z - car.z;
            return Mathf.Abs(me.x) < 6.2f && dz > 0f && dz < 3.5f + Car.Speed * 0.35f && Mathf.Abs(me.x - car.x) < 1.6f;
        }

        public Transform Point(PedestrianPoint p)
        {
            switch (p)
            {
                case PedestrianPoint.StartKerb: return startKerb;
                case PedestrianPoint.FarKerb: return farKerb;
                case PedestrianPoint.HideSpot: return hideSpot;
                case PedestrianPoint.Exit: return exitPoint;
                default:
                    if (startKerb == null || farKerb == null) return startKerb;
                    return (transform.position - startKerb.position).sqrMagnitude < (transform.position - farKerb.position).sqrMagnitude ? startKerb : farKerb;
            }
        }

        public void Reroll() => roll = Random.value;

        /// <summary>Made it to the other side: that side becomes home.</summary>
        public void SwapKerbs() => (startKerb, farKerb) = (farKerb, startKerb);

        public void ReportUtility(string option, float value) => utilities[option] = value;

        // ------------------------------------------------------------------ traffic light memory

        private void HookLight()
        {
            if (hooked != null || Light == null) return;
            hooked = Light;
            lastMain = hooked.Main;
            hooked.MainChanged += OnLightChanged;
        }

        private void OnLightChanged(LightColor c)
        {
            if (lastMain == LightColor.Red && c == LightColor.Green)
            {
                greenAt = Time.time;
                Reroll(); // the child's mood at that moment
            }
            lastMain = c;
        }

        // ------------------------------------------------------------------ movement

        // ---- NavMesh: every movement goes through the NavMeshAgent (baked NavMeshSurface "Pedestrian NavMesh")
        private NavMeshAgent nav;
        private Vector3 navTarget = new Vector3(float.NaN, 0f, 0f);
        private bool warnedOffMesh;

        public NavMeshAgent Nav
        {
            get
            {
                if (nav == null)
                {
                    nav = GetComponent<NavMeshAgent>();
                    if (nav == null) nav = gameObject.AddComponent<NavMeshAgent>();
                    nav.radius = 0.3f;
                    nav.height = 1.8f;
                    nav.acceleration = 30f;
                    nav.angularSpeed = 540f;
                    nav.stoppingDistance = 0.05f;
                    nav.autoBraking = true;
                    nav.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                }
                return nav;
            }
        }

        /// <summary>Walks on the NavMesh towards target at speed. True once arrived.</summary>
        public bool MoveTo(Vector3 target, float speed)
        {
            NavMeshAgent a = Nav;
            if (!a.isOnNavMesh)
            {
                SnapToGround();
                if (!a.isOnNavMesh)
                {
                    if (!warnedOffMesh) { warnedOffMesh = true; Debug.LogWarning($"[{name}] is not on the NavMesh: run RoadSafety > AI > Build NPC scenarios to bake it."); }
                    return false;
                }
            }
            a.isStopped = false;
            a.speed = speed;
            if (float.IsNaN(navTarget.x) || (navTarget - target).sqrMagnitude > 0.01f)
            {
                navTarget = target;
                a.SetDestination(target);
            }
            if (a.pathPending) { moving = true; return false; }
            if (a.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                // no way there on the NavMesh: give up on this move instead of waiting forever
                navTarget.x = float.NaN;
                return true;
            }
            bool arrived = a.remainingDistance <= a.stoppingDistance + 0.08f;
            if (arrived)
            {
                a.isStopped = true;
                a.velocity = Vector3.zero;
                navTarget.x = float.NaN;
                return true;
            }
            moving = true;
            walkPhase += Time.deltaTime * speed * 5.5f;
            return false;
        }

        /// <summary>Stops the NavMeshAgent where it is.</summary>
        public void StopMoving()
        {
            if (nav != null && nav.isOnNavMesh) { nav.isStopped = true; nav.velocity = Vector3.zero; }
            navTarget.x = float.NaN;
        }

        /// <summary>Puts the pedestrian on the closest NavMesh point (used after a teleport).</summary>
        public void SnapToGround()
        {
            NavMeshAgent a = Nav;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                a.Warp(hit.position);
            navTarget.x = float.NaN;
        }

        public void Face(Vector3 target, float sharpness = 10f)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // ------------------------------------------------------------------ hit by the player

        private void CheckHit()
        {
            if (Car == null) return;
            Vector3 d = transform.position - Car.transform.position;
            bool touching = Mathf.Abs(d.z) < 2.3f && Mathf.Abs(d.x) < 1.1f && Mathf.Abs(Car.Speed) > 0.8f;
            if (touching && !hitCounted)
            {
                hitCounted = true;
                Debug.Log($"[{name}] The player's car hit me!");
                GameMessages.Say(Lines.NearMiss); // voiced too (US 5.1)
                if (ProgressionManager.Instance != null)
                    ProgressionManager.Instance.LoseHeart("Hit a pedestrian (" + kind + ")");
            }
            if (!touching && d.sqrMagnitude > 64f) hitCounted = false;
        }

        // ------------------------------------------------------------------ look

        private void AddPhone()
        {
            Transform body = transform.Find(RiggedNpc.BodyName);
            Transform hand = FindDeep(body, "RightHand") ?? FindDeep(body, "R_Hand");
            bool rigged = hand != null;
            if (!rigged) hand = transform.Find("Upper/Arm_R");
            if (hand == null) return;

            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Smartphone";
            Destroy(p.GetComponent<Collider>());
            if (rigged)
            {
                p.transform.SetParent(hand, true);
                p.transform.position = hand.position + transform.forward * 0.05f;
                p.transform.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up) * Quaternion.Euler(-30f, 0f, 0f);
                Vector3 s = hand.lossyScale;
                p.transform.localScale = new Vector3(0.075f / Mathf.Max(1e-4f, s.x), 0.14f / Mathf.Max(1e-4f, s.y), 0.012f / Mathf.Max(1e-4f, s.z));
            }
            else
            {
                p.transform.SetParent(hand, false);
                p.transform.localPosition = new Vector3(0f, -0.5f, 0.06f);
                p.transform.localScale = new Vector3(0.08f, 0.15f, 0.015f);
            }
            var r = p.GetComponent<Renderer>();
            var m = new Material(r.sharedMaterial);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.07f));
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.3f, 0.55f, 1f) * 0.6f); }
            r.sharedMaterial = m;
        }

        private static Transform FindDeep(Transform root, string bone)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone || t.name.EndsWith(":" + bone) || t.name.StartsWith("CC_Base_" + bone)) return t;
            return null;
        }

        private void Animate()
        {
            float swing = moving ? Mathf.Sin(walkPhase) * 32f : 0f;
            if (legL != null) legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (armL != null) armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);

            Quaternion right = Quaternion.Euler(swing * 0.8f, 0f, 0f);
            if (startled) right = Quaternion.Euler(-150f, 0f, 20f);                                  // hands up, surprised
            else if (remarking) right = Quaternion.Euler(-110f + Mathf.Sin(Time.time * 9f) * 18f, 0f, 15f); // wagging his hand at the driver
            else if (raisingHand) right = Quaternion.Euler(-160f, 0f, 0f);                            // "wait, I'm crossing"
            else if (kind == PedestrianKind.DistractedPhone) right = Quaternion.Euler(-80f, 0f, -25f); // phone in front of the face
            else if (kind == PedestrianKind.UnpredictableChild && !onRoad && !moving)
                right = Quaternion.Euler(-20f + Mathf.Sin(Time.time * 6f) * 25f, 0f, 0f);         // fidgeting
            if (armR != null) armR.localRotation = right;

            float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f : 0f;
            if (kind == PedestrianKind.UnpredictableChild && !onRoad && !moving) bob = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.08f;
            if (startled && stateTime < 0.3f) bob = 0.25f * (1f - stateTime / 0.3f);
            if (kind == PedestrianKind.OldMan) bob *= 0.4f;                                         // a slow, heavy step
            if (upper != null)
            {
                upper.localPosition = upperRest + Vector3.up * bob;
                upper.localRotation = kind == PedestrianKind.OldMan ? Quaternion.Euler(12f, 0f, 0f) : Quaternion.identity; // stooped
            }
        }
    }
}
