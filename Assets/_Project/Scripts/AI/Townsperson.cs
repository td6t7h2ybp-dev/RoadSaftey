using System.Collections.Generic;
using UnityEngine;
using RoadSafety.Player;

namespace RoadSafety.AI
{
    public enum TownMood
    {
        Walking,
        Chatting,
        Resting,
        Talking,
        Seated,
        Waving
    }

    /// <summary>
    /// A person of the town, to make the street feel alive (US 4.2 / US 4.3).
    /// Three kinds:
    ///   Walker      : walks along the pavement, stops to chat with a neighbour, rests a moment;
    ///   GroupTalker : stands with friends (in front of a shop, the mosque...) and talks in turn;
    ///   Seated      : sits at the café or on a bench, talks in turn and drinks tea.
    /// Everyone waves at the car when the child honks (a friendly horn, US 1.4).
    ///
    /// Behaviour tree (decisions of a walker use utility scores):
    ///   Selector
    ///   ├─ Sequence: horn heard?          → Wave at the car
    ///   ├─ Sequence: seated?              → Sit, talk in turn, drink tea
    ///   ├─ Sequence: in a group?          → Talk in turn
    ///   ├─ Sequence: chatting with someone? → Chat (face to face, speech bubble)
    ///   └─ UtilitySelector "What next?"
    ///        ├─ Walk on               (always 0.35)
    ///        ├─ Greet a neighbour     (wish to talk, if someone is close)
    ///        └─ Rest a moment         (tiredness)
    /// People never step onto the road: they stay on the pavement.
    /// </summary>
    public class Townsperson : MonoBehaviour
    {
        public enum Kind
        {
            Walker,
            GroupTalker,
            Seated
        }

        [SerializeField] private Kind kind = Kind.Walker;

        [Header("Walker: back and forth between A and B on the pavement")]
        [SerializeField] private Vector3 pathA;
        [SerializeField] private Vector3 pathB;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.2f;

        [Header("Group or seated: who they talk with")]
        [Tooltip("Centre of the group or of the table (empty = keep facing forward).")]
        [SerializeField] private Transform lookAt;
        [SerializeField, Min(0)] private int turn;
        [SerializeField, Min(1)] private int groupSize = 2;

        [Header("Body (animated in code)")]
        [SerializeField] private Transform legL;
        [SerializeField] private Transform legR;
        [SerializeField] private Transform armL;
        [SerializeField] private Transform armR;
        [SerializeField] private Transform upper;
        [SerializeField] private GameObject talkBubble;

        public static readonly List<Townsperson> All = new List<Townsperson>();

        public TownMood Mood { get; private set; }
        public Kind Role => kind;

        private Node tree;
        private UtilitySelector choice;
        private DriverControls controls;

        private float social;      // 0..1, wish to talk to someone
        private float tired;       // 0..1
        private int direction = 1; // +1 towards B, -1 towards A
        private float walkPhase;
        private float offset;      // so that people don't move in sync
        private float restUntil;
        private float waveUntil;
        private float cooldownUntil;

        private Townsperson partner;
        private bool chatLeader;
        private float chatStart;
        private float chatUntil;

        public void Configure(Kind role, Vector3 a, Vector3 b, Transform target, int turnIndex, int size,
            Transform legLeft, Transform legRight, Transform armLeft, Transform armRight, Transform body, GameObject bubble)
        {
            kind = role;
            pathA = a;
            pathB = b;
            lookAt = target;
            turn = turnIndex;
            groupSize = Mathf.Max(1, size);
            legL = legLeft;
            legR = legRight;
            armL = armLeft;
            armR = armRight;
            upper = body;
            talkBubble = bubble;
        }

        private void Awake()
        {
            tree = new Selector("Townsperson",
                new Sequence("Horn heard",
                    new Condition("Car honked nearby?", () => Time.time < waveUntil),
                    new ActionNode("Wave at the car", Wave)),
                new Sequence("Seated",
                    new Condition("Has a chair?", () => kind == Kind.Seated),
                    new ActionNode("Sit, talk, drink tea", SitAndTalk)),
                new Sequence("In a group",
                    new Condition("With friends?", () => kind == Kind.GroupTalker),
                    new ActionNode("Talk in turn", GroupTalk)),
                new Sequence("Chatting",
                    new Condition("Has a partner?", () => partner != null),
                    new ActionNode("Chat", Chat)),
                choice = new UtilitySelector("What next?",
                    new UtilityOption(new ActionNode("Walk on", Walk), () => 0.35f),
                    new UtilityOption(new ActionNode("Greet a neighbour", StartChat), () => FindPartner() != null ? social : 0f),
                    new UtilityOption(new ActionNode("Rest a moment", Rest), () => Time.time < restUntil ? 1f : tired * 0.8f)));
        }

        private void OnEnable()
        {
            All.Add(this);
            offset = Random.Range(0f, 10f);
            social = Random.Range(0f, 0.6f);
            tired = Random.Range(0f, 0.3f);
        }

        private void OnDisable()
        {
            All.Remove(this);
            EndChat();
            if (controls != null)
                controls.Honked -= OnHonk;
            controls = null;
        }

        private void Update()
        {
            if (controls == null && DriverControls.Instance != null)
            {
                controls = DriverControls.Instance;
                controls.Honked += OnHonk;
            }
            tree.Evaluate();
            if (talkBubble != null && Mood != TownMood.Chatting && Mood != TownMood.Talking && Mood != TownMood.Seated)
                talkBubble.SetActive(false);
        }

        // ---------- horn ----------

        private void OnHonk()
        {
            CarController car = CarController.Player;
            if (car == null || Vector3.Distance(car.transform.position, transform.position) > 18f)
                return;
            waveUntil = Time.time + Random.Range(1.4f, 2.2f);
        }

        private NodeState Wave()
        {
            Mood = TownMood.Waving;
            CarController car = CarController.Player;
            if (car != null && kind != Kind.Seated)
                Face(car.transform.position, 6f);
            float t = Time.time * 10f;
            SetLimb(armR, -155f, Mathf.Sin(t) * 22f);
            SetLimb(armL, 0f, 0f);
            if (kind != Kind.Seated)
                StandStill();
            return NodeState.Running;
        }

        // ---------- seated / group ----------

        private NodeState SitAndTalk()
        {
            Mood = TownMood.Seated;
            if (lookAt != null)
                Face(lookAt.position, 3f);
            SetLimb(legL, -90f, 0f);
            SetLimb(legR, -90f, 0f);
            bool speaking = SpeakingTurn(2.6f);
            if (speaking)
                Gesture();
            else
                SipTea();
            ShowBubble(speaking);
            return NodeState.Running;
        }

        private NodeState GroupTalk()
        {
            Mood = TownMood.Talking;
            if (lookAt != null)
                Face(lookAt.position, 3f);
            StandStill();
            bool speaking = SpeakingTurn(2.2f);
            if (speaking)
                Gesture();
            else
                Listen();
            ShowBubble(speaking);
            return NodeState.Running;
        }

        private bool SpeakingTurn(float seconds)
        {
            // the whole group uses the same clock, so only one person speaks at a time
            float groupPhase = lookAt != null ? lookAt.position.z * 0.37f : 0f;
            int current = Mathf.FloorToInt((Time.time + groupPhase) / seconds) % groupSize;
            return current == turn;
        }

        // ---------- walker ----------

        private NodeState Walk()
        {
            Mood = TownMood.Walking;
            social = Mathf.Clamp01(social + Time.deltaTime * 0.06f);
            tired = Mathf.Clamp01(tired + Time.deltaTime * 0.012f);

            Vector3 target = direction > 0 ? pathB : pathA;
            Vector3 along = (pathB - pathA).normalized * direction;
            Vector3 keepRight = Vector3.Cross(Vector3.up, along) * 0.35f; // walk on your right, like the cars
            Vector3 goal = target + keepRight;
            goal.y = transform.position.y;

            Vector3 toGoal = goal - transform.position;
            if (toGoal.magnitude < 0.2f)
            {
                direction = -direction;
                return NodeState.Success;
            }
            Vector3 step = Vector3.ClampMagnitude(toGoal, walkSpeed * Time.deltaTime);
            transform.position += step;
            Face(transform.position + toGoal, 6f);

            walkPhase += Time.deltaTime * walkSpeed * 5f;
            float swing = Mathf.Sin(walkPhase) * 28f;
            SetLimb(legL, swing, 0f);
            SetLimb(legR, -swing, 0f);
            SetLimb(armL, -swing * 0.7f, 0f);
            SetLimb(armR, swing * 0.7f, 0f);
            if (upper != null)
                upper.localRotation = Quaternion.identity;
            return NodeState.Running;
        }

        private NodeState Rest()
        {
            Mood = TownMood.Resting;
            if (restUntil < Time.time)
                restUntil = Time.time + Random.Range(2.5f, 4.5f);
            StandStill();
            tired = Mathf.Clamp01(tired - Time.deltaTime * 0.25f);
            if (Time.time >= restUntil - 0.05f)
            {
                tired = 0f;
                restUntil = 0f;
                return NodeState.Success;
            }
            return NodeState.Running;
        }

        private Townsperson FindPartner()
        {
            if (kind != Kind.Walker || partner != null || Time.time < cooldownUntil || social < 0.5f)
                return null;
            Townsperson best = null;
            float bestDistance = 3.2f;
            foreach (Townsperson other in All)
            {
                if (other == this || other.kind != Kind.Walker || other.partner != null || Time.time < other.cooldownUntil)
                    continue;
                if (other.social < 0.35f || other.Mood == TownMood.Waving)
                    continue;
                float d = Vector3.Distance(other.transform.position, transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = other;
                }
            }
            return best;
        }

        private NodeState StartChat()
        {
            Townsperson other = FindPartner();
            if (other == null)
                return NodeState.Failure;
            float length = Random.Range(5f, 8f);
            partner = other;
            other.partner = this;
            chatLeader = true;
            other.chatLeader = false;
            chatStart = other.chatStart = Time.time;
            chatUntil = other.chatUntil = Time.time + length;
            return NodeState.Success;
        }

        private NodeState Chat()
        {
            Mood = TownMood.Chatting;
            if (partner == null || !partner.isActiveAndEnabled)
            {
                EndChat();
                return NodeState.Failure;
            }
            if (Time.time >= chatUntil)
            {
                EndChat();
                return NodeState.Success;
            }
            Vector3 toPartner = partner.transform.position - transform.position;
            toPartner.y = 0f;
            if (chatLeader && toPartner.magnitude > 1.3f)
            {
                // walk up to the neighbour first
                transform.position += Vector3.ClampMagnitude(toPartner.normalized * (toPartner.magnitude - 1.2f), walkSpeed * Time.deltaTime);
                Face(partner.transform.position, 6f);
                walkPhase += Time.deltaTime * walkSpeed * 5f;
                float swing = Mathf.Sin(walkPhase) * 28f;
                SetLimb(legL, swing, 0f);
                SetLimb(legR, -swing, 0f);
                chatUntil += Time.deltaTime;
                partner.chatUntil = chatUntil;
                ShowBubble(false);
                return NodeState.Running;
            }
            Face(partner.transform.position, 5f);
            StandStill();
            bool leaderSpeaks = Mathf.FloorToInt((Time.time - chatStart) / 1.7f) % 2 == 0;
            bool speaking = leaderSpeaks == chatLeader;
            if (speaking)
                Gesture();
            else
                Listen();
            ShowBubble(speaking);
            return NodeState.Running;
        }

        private void EndChat()
        {
            if (partner != null && partner.partner == this)
            {
                partner.partner = null;
                partner.social = 0f;
                partner.cooldownUntil = Time.time + 15f;
                partner.ShowBubble(false);
            }
            partner = null;
            social = 0f;
            cooldownUntil = Time.time + 15f;
            ShowBubble(false);
        }

        // ---------- body ----------

        private void Face(Vector3 point, float speed)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return;
            Quaternion target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private void StandStill()
        {
            SetLimb(legL, 0f, 0f);
            SetLimb(legR, 0f, 0f);
        }

        private void Gesture()
        {
            float t = (Time.time + offset) * 5f;
            SetLimb(armR, -55f + Mathf.Sin(t) * 18f, -12f);
            SetLimb(armL, -25f + Mathf.Sin(t * 0.7f + 1f) * 12f, 8f);
            if (upper != null)
                upper.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 3f, Mathf.Sin(t * 0.4f) * 6f, 0f);
        }

        private void Listen()
        {
            SetLimb(armR, 0f, 0f);
            SetLimb(armL, 0f, 0f);
            if (upper != null)
                upper.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin((Time.time + offset) * 2.2f)) * 5f, 0f, 0f); // nods
        }

        private void SipTea()
        {
            // every few seconds, the glass goes up to the mouth
            float cycle = Mathf.Repeat(Time.time + offset, 6f);
            float lift = cycle < 1.2f ? Mathf.Sin(cycle / 1.2f * Mathf.PI) : 0f;
            SetLimb(armR, -40f - lift * 80f, -10f * lift);
            SetLimb(armL, -40f, 0f);
            if (upper != null)
                upper.localRotation = Quaternion.identity;
        }

        private void ShowBubble(bool on)
        {
            if (talkBubble != null && talkBubble.activeSelf != on)
                talkBubble.SetActive(on);
        }

        private static void SetLimb(Transform limb, float pitch, float roll)
        {
            if (limb != null)
                limb.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (kind != Kind.Walker)
                return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pathA + Vector3.up * 0.2f, pathB + Vector3.up * 0.2f);
            if (Application.isPlaying && choice != null && choice.LastChoice != null)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, $"{Mood} ({choice.LastChoice.Name} {choice.LastScore:0.00})");
        }
#endif
    }
}
