using UnityEngine;
using UnityEngine.Rendering;
using RoadSafety.UI;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// The inside of the player's car, seen through the child's eyes (first-person):
    ///   - dashboard, steering wheel that turns with the arrows, seats, pillars, mirror;
    ///   - the driver (the player): legs, chest and arms, hands on the wheel;
    ///   - the friend on the passenger seat (<see cref="PassengerFriend"/>), who says every line of the game;
    ///   - a real seatbelt (US 1.3): the child grabs the belt's metal tongue and pulls it across
    ///     the chest into the red buckle. A simple tap also works: the hand pulls it by itself.
    ///     There is no seatbelt button on the HUD any more.
    /// Everything is built in code when the level starts (no scene edit), with no colliders.
    /// Layout in the car's local space: front = +Z, the driver sits on the left.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarInterior : MonoBehaviour
    {
        public static CarInterior Current { get; private set; }

        // ---- layout (car local space, metres) ----
        private const float DriverX = -0.38f;
        private const float PassengerX = 0.3f;
        private static readonly Vector3 DriverHip = new Vector3(DriverX, 1.06f, -0.4f);
        private static readonly Vector3 PassengerHip = new Vector3(PassengerX, 1.06f, -0.14f); // same height as the driver, a little forward so he is in view
        private static readonly Vector3 EyePoint = new Vector3(-0.33f, 1.58f, -0.78f); // the child's eyes, a little behind the head so wheel, hands, road and friend are all in view
        private static readonly Vector3 WheelCentre = new Vector3(DriverX, 1.4f, 0.04f);
        private const float WheelRadius = 0.17f;

        // driver's seatbelt
        private static readonly Vector3 BeltAnchor = new Vector3(-0.66f, 1.66f, -0.56f);   // top of the pillar, left shoulder
        private static readonly Vector3 BeltChest = new Vector3(-0.4f, 1.42f, -0.43f);
        private static readonly Vector3 BuckleAt = new Vector3(-0.13f, 1.2f, -0.22f);     // red buckle, on the console between the seats
        private static readonly Vector3 LapMiddle = new Vector3(-0.38f, 1.18f, -0.24f);
        private static readonly Vector3 LapAnchor = new Vector3(-0.66f, 1.14f, -0.42f);
        private static readonly Vector3 TongueRest = new Vector3(-0.64f, 1.36f, -0.1f);    // hanging by the left door
        private const float BeltLength = 1.25f;

        [Header("Seatbelt (US 1.3)")]
        [Tooltip("Metres between the tongue and the buckle for it to click in.")]
        [SerializeField, Min(0.02f)] private float snapDistance = 0.1f;
        [Tooltip("Also clicks in when the finger is this close to the buckle on screen (fraction of screen height).")]
        [SerializeField, Range(0.01f, 0.3f)] private float snapScreenFraction = 0.08f;
        [Tooltip("Seconds the hand takes to pull the belt when the child just taps it.")]
        [SerializeField, Min(0.1f)] private float assistSeconds = 0.8f;

        [Header("Camera look (degrees)")]
        [SerializeField] private Vector2 lookAtBelt = new Vector2(2f, 22f);
        [SerializeField] private Vector2 lookAtFriend = new Vector2(30f, 4f);
        [SerializeField] private float lookAhead = 11f;
        [SerializeField] private float lookIntoTurn = 10f;

        private enum Belt { Hanging, Dragging, Returning, Assisting, Fastened }

        private Transform root;
        private Transform wheelSpin;
        private KidRig driver;
        private RiggedKid driverRigged;   // the real rigged model: only its arms and hands are seen
        private PassengerFriend friend;
        private CarController car;
        private DriverControls controls;

        private Transform tongue, buckle;
        private Transform strapA, strapB, strapC, strapD;
        private Material tongueMat, buckleMat;
        private Color tongueColor, buckleColor;
        private Belt belt = Belt.Hanging;
        private Vector3 tonguePos = TongueRest;
        private Vector3 moveFrom;
        private float moveTime;
        private float clickPunch;
        private SeatbeltGrabZone grabZone;

        private float lastSpeed, accel, lean, roll;
        private Vector3 driverL, driverR;
        private bool driverSmooth;

        public Transform Eye { get; private set; }
        public bool SeatbeltFastened => belt == Belt.Fastened;
        public Vector3 TongueWorld => root.TransformPoint(tonguePos);
        public Vector3 BuckleWorld => root.TransformPoint(BuckleAt);
        public Transform InteriorRoot => root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        /// <summary>Adds the interior to the player's car if it has none (GameplayBootstrap calls it).</summary>
        public static CarInterior Ensure()
        {
            CarController playerCar = CarController.Player != null ? CarController.Player : FindAnyObjectByType<CarController>();
            if (playerCar == null)
                return null;
            CarInterior interior = playerCar.GetComponent<CarInterior>();
            return interior != null ? interior : playerCar.gameObject.AddComponent<CarInterior>();
        }

        private void Awake()
        {
            Current = this;
            car = GetComponent<CarController>();
            Build();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (controls != null)
                controls.SeatbeltChanged -= OnSeatbeltChanged;
            if (grabZone != null)
                EditorPreviewLog.Kill(grabZone.gameObject);
            if (root != null && !Application.isPlaying)
                EditorPreviewLog.Kill(root.gameObject);
        }

        /// <summary>Editor preview (no Play): builds the inside of the car and poses everyone once.</summary>
        public void EditorBuildPreview()
        {
            Current = this;
            car = GetComponent<CarController>();
            Build();
            for (int i = 0; i < 4; i++)
                Tick(0.1f);
        }

        private void Start()
        {
            controls = DriverControls.Instance;
            if (controls != null)
            {
                controls.SeatbeltChanged += OnSeatbeltChanged;
                if (controls.SeatbeltOn)
                    OnSeatbeltChanged(true);
            }

            HUDController hud = FindAnyObjectByType<HUDController>();
            if (hud != null)
                hud.UseInCarFriend();
            grabZone = SeatbeltGrabZone.Create(this, hud);
            if (grabZone != null)
                grabZone.gameObject.SetActive(belt != Belt.Fastened);
        }

        // ===================================================================== build

        private void Build()
        {
            Transform model = transform.Find("Model");
            if (model == null) model = transform;

            Renderer paint = null;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Body") paint = r;
                // the glass block would hide the street from the inside; the roof stays (closed car)
                if (r.name == "Cabin") EditorPreviewLog.SetEnabled(r, false);
                if (r.name == "Roof") EditorPreviewLog.SetEnabled(r, false); // open-top car (convertible), like the reference picture
            }
            CockpitKit.SetTemplate(paint != null ? paint.sharedMaterial : null);
            // warm coral paint like the reference (the bonnet edge is seen from the inside)
            Material coral = CockpitKit.Mat("Paint", new Color(0.86f, 0.36f, 0.24f), 0.45f);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                if (r.name == "Body") EditorPreviewLog.SetMaterial(r, coral);

            root = CockpitKit.Group("Interior", model, Vector3.zero);

            Material dash = CockpitKit.Mat("Dash", new Color(0.26f, 0.25f, 0.29f));
            Material dashLight = CockpitKit.Mat("DashLight", new Color(0.36f, 0.35f, 0.39f));
            Material pillar = CockpitKit.Mat("Pillar", new Color(0.33f, 0.27f, 0.27f));
            Material liner = CockpitKit.Mat("Liner", new Color(0.3f, 0.24f, 0.24f));
            Material doorMat = CockpitKit.Mat("Door", new Color(0.82f, 0.7f, 0.6f));
            Material seat = CockpitKit.Mat("Seat", new Color(0.5f, 0.47f, 0.48f));
            Material floor = CockpitKit.Mat("Floor", new Color(0.22f, 0.2f, 0.22f));
            Material wheel = CockpitKit.Mat("Wheel", new Color(0.12f, 0.11f, 0.13f), 0.35f);
            Material strap = CockpitKit.Mat("Strap", new Color(0.13f, 0.13f, 0.16f));
            Material metal = CockpitKit.Mat("Metal", new Color(0.82f, 0.83f, 0.88f), 0.6f);
            Material mirror = CockpitKit.Mat("Mirror", new Color(0.86f, 0.78f, 0.82f), 0.9f);
            Material skin = CockpitKit.Mat("Skin", new Color(0.93f, 0.68f, 0.5f));
            Material dark = CockpitKit.Mat("Dark", new Color(0.08f, 0.07f, 0.08f));
            Material hair = CockpitKit.Mat("Hair", new Color(0.16f, 0.1f, 0.06f));
            Material knobRed = CockpitKit.Mat("KnobRed", new Color(0.85f, 0.25f, 0.2f));
            Material knobBlue = CockpitKit.Mat("KnobBlue", new Color(0.25f, 0.45f, 0.85f));
            tongueMat = CockpitKit.Mat("Tongue", new Color(0.85f, 0.86f, 0.9f), 0.6f);
            buckleMat = CockpitKit.Mat("Buckle", new Color(0.88f, 0.2f, 0.18f));
            tongueColor = new Color(0.85f, 0.86f, 0.9f);
            buckleColor = new Color(0.88f, 0.2f, 0.18f);
            var noShadow = ShadowCastingMode.Off;

            // soft warm light inside the cabin, like sunset light bouncing off the street
            var fill = new GameObject("CabinLight").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.localPosition = new Vector3(0f, 1.6f, 0.1f);
            fill.type = LightType.Point;
            fill.range = 2.6f;
            fill.intensity = 1.6f;
            fill.color = new Color(1f, 0.82f, 0.68f);
            fill.shadows = LightShadows.None;

            // ---- body of the cabin: floor, roof lining, thick pillars, doors ----
            CockpitKit.Part(PrimitiveType.Cube, "Floor", root, new Vector3(0f, 1.016f, -0.3f), new Vector3(1.64f, 0.01f, 1.9f), floor, null, noShadow);
            // windscreen frame: two pillars and the top bar, with the mirror hanging from it
            CockpitKit.Part(PrimitiveType.Cube, "WindscreenTop", root, new Vector3(0f, 1.66f, 0.5f), new Vector3(1.5f, 0.05f, 0.07f), pillar);
            CockpitKit.Part(PrimitiveType.Cube, "WindscreenBase", root, new Vector3(0f, 1.25f, 0.76f), new Vector3(1.6f, 0.04f, 0.06f), pillar);
            foreach (float s in new[] { -1f, 1f })
            {
                Transform aPillar = CockpitKit.Part(PrimitiveType.Cube, "APillar", root, Vector3.zero, Vector3.one, pillar);
                CockpitKit.Stretch(aPillar, new Vector3(s * 0.79f, 1.22f, 0.78f), new Vector3(s * 0.75f, 1.68f, 0.5f), 0.05f, 0.06f);
                CockpitKit.Part(PrimitiveType.Cube, "DoorTop", root, new Vector3(s * 0.85f, 1.27f, -0.25f), new Vector3(0.1f, 0.05f, 1.75f), coral);
                CockpitKit.Part(PrimitiveType.Cylinder, "Speaker", root, new Vector3(s * 0.825f, 1.06f, 0.3f), new Vector3(0.11f, 0.01f, 0.11f), dark, Quaternion.Euler(0f, 0f, 90f));
                // door: light panel, armrest, handle, window sill
                CockpitKit.Part(PrimitiveType.Cube, "DoorPanel", root, new Vector3(s * 0.85f, 1.1f, -0.1f), new Vector3(0.05f, 0.22f, 1.5f), doorMat);
                CockpitKit.Part(PrimitiveType.Cube, "DoorSill", root, new Vector3(s * 0.84f, 1.22f, -0.1f), new Vector3(0.09f, 0.04f, 1.5f), pillar);
                CockpitKit.Part(PrimitiveType.Cube, "Armrest", root, new Vector3(s * 0.8f, 1.13f, -0.25f), new Vector3(0.07f, 0.04f, 0.4f), dashLight);
                CockpitKit.Part(PrimitiveType.Cube, "Handle", root, new Vector3(s * 0.815f, 1.17f, 0.12f), new Vector3(0.03f, 0.05f, 0.12f), dark);
            }

            // ---- side mirror (outside, left, like the reference) ----
            Transform mirrorArm = CockpitKit.Group("SideMirror", root, new Vector3(-0.98f, 1.22f, 0.58f));
            mirrorArm.localRotation = Quaternion.Euler(0f, -15f, 0f);
            CockpitKit.Part(PrimitiveType.Cube, "Housing", mirrorArm, Vector3.zero, new Vector3(0.24f, 0.14f, 0.06f), dash);
            CockpitKit.Part(PrimitiveType.Cube, "Glass", mirrorArm, new Vector3(0f, 0f, -0.032f), new Vector3(0.21f, 0.115f, 0.01f), mirror);
            CockpitKit.Part(PrimitiveType.Cube, "Mount", root, new Vector3(-0.88f, 1.2f, 0.6f), new Vector3(0.12f, 0.04f, 0.04f), dash);

            // ---- dashboard ----
            CockpitKit.Part(PrimitiveType.Cube, "Dashboard", root, new Vector3(0f, 1.11f, 0.62f), new Vector3(1.62f, 0.2f, 0.36f), dash);
            CockpitKit.Part(PrimitiveType.Cube, "DashTop", root, new Vector3(0f, 1.22f, 0.6f), new Vector3(1.6f, 0.05f, 0.34f), doorMat, Quaternion.Euler(-10f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Cube, "DashFace", root, new Vector3(0f, 1.1f, 0.43f), new Vector3(1.58f, 0.18f, 0.03f), dashLight);
            // gauge hood + three round gauges with needles
            CockpitKit.Part(PrimitiveType.Cube, "GaugeHood", root, new Vector3(DriverX, 1.27f, 0.44f), new Vector3(0.42f, 0.05f, 0.14f), dash, Quaternion.Euler(-8f, 0f, 0f));
            Texture2D gaugeTex = CockpitKit.Paint(128, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 0.48f) return new Color(0.1f, 0.1f, 0.12f);
                if (r > 0.42f) return new Color(0.95f, 0.95f, 0.95f);
                float ang = Mathf.Atan2(dy, dx);
                if (r > 0.32f && Mathf.Abs(Mathf.Repeat(ang * 8f / Mathf.PI, 1f) - 0.5f) < 0.08f) return new Color(0.9f, 0.9f, 0.9f);
                return new Color(0.07f, 0.07f, 0.09f);
            });
            Material gauge = CockpitKit.Screen("Gauge", gaugeTex, 0.25f);
            Material needle = CockpitKit.Mat("Needle", new Color(0.95f, 0.25f, 0.2f));
            float[] gaugeX = { -0.11f, 0f, 0.11f };
            float[] gaugeSize = { 0.1f, 0.07f, 0.1f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3(DriverX + gaugeX[i], 1.225f + (i == 1 ? 0.02f : 0f), 0.425f);
                CockpitKit.Part(PrimitiveType.Quad, "Gauge", root, at, Vector3.one * gaugeSize[i], gauge, Quaternion.Euler(10f, 0f, 0f), noShadow);
                Transform n = CockpitKit.Part(PrimitiveType.Cube, "Needle", root, at + new Vector3(0f, 0f, -0.004f), new Vector3(0.004f, gaugeSize[i] * 0.4f, 0.003f), needle, Quaternion.Euler(10f, 0f, 40f - i * 30f), noShadow);
                n.localPosition += n.localRotation * new Vector3(0f, gaugeSize[i] * 0.16f, 0f);
            }
            // centre screen with a little street map (like a car navigation screen)
            Texture2D mapTex = CockpitKit.Paint(256, (u, v) =>
            {
                Color bg = new Color(0.93f, 0.91f, 0.86f);
                bool road = Mathf.Abs(u - 0.55f) < 0.06f || Mathf.Abs(v - 0.3f) < 0.035f || Mathf.Abs(v - 0.72f) < 0.03f || Mathf.Abs(u - 0.2f) < 0.03f;
                bool main = Mathf.Abs(u - 0.55f) < 0.035f;
                bool park = u < 0.45f && v > 0.35f && v < 0.68f && u > 0.25f;
                bool arrow = Mathf.Abs(u - 0.55f) < 0.06f * (1f - Mathf.Abs(v - 0.5f) / 0.08f) && Mathf.Abs(v - 0.5f) < 0.08f && v > 0.44f;
                if (u < 0.06f || u > 0.94f || v < 0.04f || v > 0.96f) return new Color(0.08f, 0.08f, 0.1f);
                if (arrow) return new Color(0.15f, 0.45f, 0.95f);
                if (main) return new Color(0.98f, 0.78f, 0.3f);
                if (road) return Color.white;
                if (park) return new Color(0.66f, 0.84f, 0.58f);
                return bg;
            });
            CockpitKit.Part(PrimitiveType.Cube, "ScreenFrame", root, new Vector3(0.02f, 1.2f, 0.45f), new Vector3(0.24f, 0.17f, 0.03f), dark, Quaternion.Euler(-12f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Quad, "Screen", root, new Vector3(0.02f, 1.2f, 0.433f), new Vector3(0.21f, 0.145f, 1f), CockpitKit.Screen("Map", mapTex, 0.8f), Quaternion.Euler(-12f, 0f, 0f), noShadow);
            // vents, climate knobs, console, gear stick
            foreach (float vx in new[] { -0.15f, 0.19f, 0.55f })
            {
                Transform vent = CockpitKit.Group("Vent", root, new Vector3(vx, 1.12f, 0.425f));
                CockpitKit.Part(PrimitiveType.Cube, "Frame", vent, Vector3.zero, new Vector3(0.12f, 0.06f, 0.02f), dark);
                for (int i = 0; i < 3; i++)
                    CockpitKit.Part(PrimitiveType.Cube, "Slat", vent, new Vector3(0f, -0.018f + i * 0.018f, -0.012f), new Vector3(0.11f, 0.005f, 0.006f), dashLight);
            }
            CockpitKit.Part(PrimitiveType.Cube, "Console", root, new Vector3(0.02f, 1.07f, 0.28f), new Vector3(0.24f, 0.13f, 0.32f), dash);
            CockpitKit.Part(PrimitiveType.Cylinder, "Knob_Hot", root, new Vector3(-0.05f, 1.07f, 0.418f), new Vector3(0.035f, 0.01f, 0.035f), knobRed, Quaternion.Euler(90f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Cylinder, "Knob_Cold", root, new Vector3(0.09f, 1.07f, 0.418f), new Vector3(0.035f, 0.01f, 0.035f), knobBlue, Quaternion.Euler(90f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Cylinder, "GearStick", root, new Vector3(0.02f, 1.17f, 0.18f), new Vector3(0.02f, 0.06f, 0.02f), dark, Quaternion.Euler(-15f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Sphere, "GearKnob", root, new Vector3(0.02f, 1.235f, 0.2f), Vector3.one * 0.045f, dark);
            CockpitKit.Part(PrimitiveType.Cube, "Mirror", root, new Vector3(0.05f, 1.57f, 0.47f), new Vector3(0.28f, 0.08f, 0.03f), mirror, Quaternion.Euler(8f, 0f, 0f));
            CockpitKit.Part(PrimitiveType.Cube, "MirrorStem", root, new Vector3(0.05f, 1.62f, 0.49f), new Vector3(0.02f, 0.07f, 0.02f), dark);

            // seats
            // each seat is built under the child who sits in it: cushion just under the hips, back behind them
            foreach (Vector3 hip in new[] { DriverHip, PassengerHip })
            {
                ShadowCastingMode own = hip.x < 0f ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                float top = hip.y - 0.03f; // the cushion comes right up under the child: he sits on it, not above it
                CockpitKit.Part(PrimitiveType.Cube, "SeatCushion", root, new Vector3(hip.x, top - 0.07f, hip.z + 0.06f), new Vector3(0.5f, 0.14f, 0.54f), seat, null, own);
                CockpitKit.Part(PrimitiveType.Cube, "SeatBase", root, new Vector3(hip.x, top - 0.22f, hip.z + 0.06f), new Vector3(0.42f, 0.18f, 0.44f), dark, null, own);
                // a low back: enough to read as a chair, never tall enough to hide the child sitting in it
                CockpitKit.Part(PrimitiveType.Cube, "SeatBack", root, new Vector3(hip.x, top + 0.17f, hip.z - 0.24f), new Vector3(0.5f, 0.4f, 0.12f), seat, Quaternion.Euler(-8f, 0f, 0f), own);
                CockpitKit.Part(PrimitiveType.Cube, "Headrest", root, new Vector3(hip.x, top + 0.5f, hip.z - 0.28f), new Vector3(0.28f, 0.14f, 0.1f), seat, null, ShadowCastingMode.ShadowsOnly);
            }

            // ---- three-spoke steering wheel (turns with the arrows) ----
            Transform column = CockpitKit.Part(PrimitiveType.Cylinder, "Column", root, Vector3.zero, Vector3.one, dash);
            Vector3 columnStart = new Vector3(DriverX, 1.12f, 0.42f);
            column.localPosition = (columnStart + WheelCentre) * 0.5f;
            column.localRotation = Quaternion.FromToRotation(Vector3.up, (WheelCentre - columnStart).normalized);
            column.localScale = new Vector3(0.06f, (WheelCentre - columnStart).magnitude * 0.5f, 0.06f);
            Transform wheelTilt = CockpitKit.Group("SteeringWheel", root, WheelCentre);
            wheelTilt.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            wheelSpin = CockpitKit.Group("Spin", wheelTilt, Vector3.zero);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Transform rim = CockpitKit.Part(PrimitiveType.Cylinder, "Rim", wheelSpin, Vector3.zero, Vector3.one, wheel);
                CockpitKit.Stretch(rim, new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * WheelRadius, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * WheelRadius, 0.042f);
            }
            CockpitKit.Part(PrimitiveType.Cylinder, "Hub", wheelSpin, new Vector3(0f, 0f, 0.005f), new Vector3(0.1f, 0.025f, 0.1f), wheel, Quaternion.Euler(90f, 0f, 0f));
            foreach (float a in new[] { 0f, 180f, 270f })
            {
                Transform spoke = CockpitKit.Part(PrimitiveType.Cube, "Spoke", wheelSpin, Vector3.zero, Vector3.one, wheel);
                Vector3 end = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * WheelRadius;
                CockpitKit.Stretch(spoke, Vector3.zero, end, a == 270f ? 0.045f : 0.06f, 0.02f);
            }

            // the driver: the child's own body; only the bare arms and hands are seen
            Transform fallback = CockpitKit.Group("DriverSimple", root, Vector3.zero);
            driver = KidRig.Build(fallback, "Driver", DriverHip, skin, skin, skin, hair, dark, false, false);
            Eye = CockpitKit.Group("DriverEye", root, EyePoint);
            Transform driverSeat = CockpitKit.Group("DriverSeat", root, DriverHip);
            driverRigged = RiggedKid.Spawn(driverSeat, 1.45f, true, null, new Color(0.93f, 0.68f, 0.5f)); // bare arms
            if (driverRigged != null)
            {
                foreach (Renderer r in fallback.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
                // the eyes are the child's own eyes, in the driver's seat
                Eye.localPosition = root.InverseTransformPoint(driverRigged.HeadPosition) + new Vector3(0.07f, 0.27f, -0.17f); // a touch back, up and towards the middle: wheel, hands, road and the friend all in frame
            }

            // the seatbelt
            strapA = CockpitKit.Part(PrimitiveType.Cube, "Belt_Shoulder", root, Vector3.zero, Vector3.one, strap);
            strapB = CockpitKit.Part(PrimitiveType.Cube, "Belt_Chest", root, Vector3.zero, Vector3.one, strap);
            strapC = CockpitKit.Part(PrimitiveType.Cube, "Belt_Lap1", root, Vector3.zero, Vector3.one, strap);
            strapD = CockpitKit.Part(PrimitiveType.Cube, "Belt_Lap2", root, Vector3.zero, Vector3.one, strap);
            CockpitKit.Part(PrimitiveType.Cube, "Belt_Guide", root, BeltAnchor, new Vector3(0.06f, 0.05f, 0.04f), metal);
            Transform stalk = CockpitKit.Part(PrimitiveType.Cube, "Buckle_Stalk", root, Vector3.zero, Vector3.one, strap);
            CockpitKit.Stretch(stalk, BuckleAt + new Vector3(0.02f, -0.1f, -0.04f), BuckleAt, 0.03f, 0.03f);
            buckle = CockpitKit.Part(PrimitiveType.Cube, "Buckle", root, BuckleAt, new Vector3(0.055f, 0.075f, 0.045f), buckleMat, Quaternion.Euler(0f, 0f, -20f));
            tongue = CockpitKit.Part(PrimitiveType.Cube, "Belt_Tongue", root, TongueRest, new Vector3(0.05f, 0.065f, 0.016f), tongueMat);

            // the friend on the passenger seat
            var friendGo = new GameObject("Friend");
            friendGo.transform.SetParent(root, false);
            friend = friendGo.AddComponent<PassengerFriend>();
            friend.Build(root, PassengerHip, skin, dark, metal, strap, this);

            UpdateBelt(0f);
        }

        // ===================================================================== seatbelt

        private void OnSeatbeltChanged(bool on)
        {
            if (!on) return;
            belt = Belt.Fastened;
            tonguePos = BuckleAt;
            clickPunch = 1f;
            if (grabZone != null)
                grabZone.gameObject.SetActive(false);
        }

        /// <summary>The finger touched the belt.</summary>
        internal void BeginGrab()
        {
            if (belt == Belt.Fastened || belt == Belt.Assisting)
                return;
            belt = Belt.Dragging;
        }

        /// <summary>The finger moves: the tongue follows it, on a plane facing the camera.</summary>
        internal void Drag(Vector2 screenPoint, Camera cam)
        {
            if (belt != Belt.Dragging || cam == null)
                return;
            Vector3 restWorld = root.TransformPoint(TongueRest);
            var plane = new Plane(-cam.transform.forward, restWorld);
            Ray ray = cam.ScreenPointToRay(screenPoint);
            if (!plane.Raycast(ray, out float enter))
                return;
            Vector3 p = root.InverseTransformPoint(ray.GetPoint(enter));
            Vector3 fromAnchor = p - BeltAnchor;
            if (fromAnchor.magnitude > BeltLength)
                p = BeltAnchor + fromAnchor.normalized * BeltLength;
            p.y = Mathf.Max(p.y, 1.15f);
            p.x = Mathf.Clamp(p.x, -0.8f, 0.05f);
            tonguePos = p;

            Vector2 buckleScreen = cam.WorldToScreenPoint(BuckleWorld);
            if (Vector3.Distance(tonguePos, BuckleAt) < snapDistance ||
                Vector2.Distance(screenPoint, buckleScreen) < snapScreenFraction * Screen.height)
                Click();
        }

        /// <summary>The finger is lifted. A tap (no real drag) lets the hand pull the belt by itself.</summary>
        internal void EndGrab(bool wasTap)
        {
            if (belt != Belt.Dragging)
                return;
            moveFrom = tonguePos;
            moveTime = 0f;
            belt = wasTap ? Belt.Assisting : Belt.Returning;
        }

        private void Click()
        {
            tonguePos = BuckleAt;
            belt = Belt.Fastened;
            clickPunch = 1f;
            if (grabZone != null)
                grabZone.gameObject.SetActive(false);
            if (controls != null)
            {
                controls.FastenSeatbelt();
                if (!controls.SeatbeltOn)
                {
                    // the level is restarting: the belt goes back
                    belt = Belt.Returning;
                    moveFrom = tonguePos;
                    moveTime = 0f;
                    if (grabZone != null) grabZone.gameObject.SetActive(true);
                }
            }
        }

        private void UpdateBelt(float dt)
        {
            switch (belt)
            {
                case Belt.Returning:
                    moveTime += dt / 0.3f;
                    tonguePos = Vector3.Lerp(moveFrom, TongueRest, Smooth(moveTime));
                    if (moveTime >= 1f) belt = Belt.Hanging;
                    break;
                case Belt.Assisting:
                    moveTime += dt / assistSeconds;
                    float t = Smooth(moveTime);
                    Vector3 mid = (moveFrom + BuckleAt) * 0.5f + new Vector3(0f, 0.12f, 0.1f); // across the chest
                    tonguePos = Vector3.Lerp(Vector3.Lerp(moveFrom, mid, t), Vector3.Lerp(mid, BuckleAt, t), t);
                    if (moveTime >= 1f) Click();
                    break;
            }

            bool fastened = belt == Belt.Fastened;
            if (fastened)
            {
                Vector3 chest = driverRigged != null
                    ? root.InverseTransformPoint(driverRigged.ChestPosition) + new Vector3(0.02f, 0.06f, 0.16f)
                    : BeltChest;
                CockpitKit.Stretch(strapA, BeltAnchor, chest, 0.06f, 0.01f);
                CockpitKit.Stretch(strapB, chest, BuckleAt, 0.06f, 0.01f);
                CockpitKit.Stretch(strapC, BuckleAt, LapMiddle, 0.05f, 0.008f);
                CockpitKit.Stretch(strapD, LapMiddle, LapAnchor, 0.05f, 0.008f);
            }
            else
            {
                CockpitKit.Stretch(strapA, BeltAnchor, tonguePos, 0.05f, 0.008f);
            }
            // once fastened, the shoulder part runs right under the eyes: it only throws its shadow
            var shoulderMode = ShadowCastingMode.On; // the belt across the chest is seen: it is on
            strapA.GetComponent<MeshRenderer>().shadowCastingMode = shoulderMode;
            strapB.GetComponent<MeshRenderer>().shadowCastingMode = shoulderMode;
            strapB.gameObject.SetActive(fastened);
            strapC.gameObject.SetActive(fastened);
            strapD.gameObject.SetActive(fastened);

            tongue.localPosition = tonguePos;
            tongue.localRotation = Quaternion.LookRotation(Vector3.forward, (BeltAnchor - tonguePos).normalized);

            // gentle glow + pulse until the belt is on, so the child sees what to grab
            float pulse = fastened ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            Color sun = new Color(1f, 0.85f, 0.2f);
            CockpitKit.SetColor(tongueMat, Color.Lerp(tongueColor, sun, pulse * 0.8f));
            CockpitKit.SetColor(buckleMat, belt == Belt.Dragging ? Color.Lerp(buckleColor, sun, pulse) : buckleColor);
            tongue.localScale = new Vector3(0.05f, 0.065f, 0.016f) * (1f + pulse * 0.35f);
            clickPunch = Mathf.MoveTowards(clickPunch, 0f, dt * 3f);
            buckle.localScale = new Vector3(0.055f, 0.075f, 0.045f) * (1f + clickPunch * 0.4f);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ===================================================================== animation

        private void LateUpdate()
        {
            if (Time.deltaTime > 0f) Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            if (friend != null && !Application.isPlaying) friend.Tick(dt);
            float steer = controls != null ? controls.Steer : 0f;

            // the wheel turns with the arrows
            wheelSpin.localRotation = Quaternion.Euler(0f, 0f, -steer * 110f);

            // bodies lean back when the car speeds up, forward when it brakes, sideways in turns
            float speed = car != null ? car.Speed : 0f;
            float maxSpeed = car != null ? Mathf.Max(1f, car.MaxSpeed) : 9f;
            accel = Mathf.Lerp(accel, (speed - lastSpeed) / dt, 1f - Mathf.Exp(-6f * dt));
            lastSpeed = speed;
            lean = Mathf.Lerp(lean, Mathf.Clamp(-accel * 1.6f, -9f, 7f), 1f - Mathf.Exp(-5f * dt));
            roll = Mathf.Lerp(roll, -steer * Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed) * 7f, 1f - Mathf.Exp(-5f * dt));
            driver.Lean(lean, roll);
            if (friend != null) friend.SetLean(lean, roll);

            UpdateBelt(dt);

            // hands on the wheel (they turn with it); the left hand holds the belt while it is being put on
            Vector3 handR = WheelPoint(steer, 30f);
            Vector3 handL = WheelPoint(steer, 150f);
            if (belt == Belt.Dragging || belt == Belt.Assisting)
                handL = tonguePos + new Vector3(-0.02f, 0.02f, -0.03f);
            else if (belt != Belt.Fastened)
                handL = TongueRest + new Vector3(0.1f, -0.12f, 0.12f); // ready by the belt, out of the way
            driver.ReachArm(true, handL, belt == Belt.Dragging ? 30f : 12f, dt, belt == Belt.Fastened || belt == Belt.Hanging ? 0f : 0.3f);
            driver.ReachArm(false, handR, 12f, dt);
            if (driverRigged != null)
            {
                float kk = 1f - Mathf.Exp(-(belt == Belt.Dragging ? 30f : 12f) * dt);
                if (!driverSmooth) { driverL = handL; driverR = handR; driverSmooth = true; }
                driverL = Vector3.Lerp(driverL, handL, kk);
                driverR = Vector3.Lerp(driverR, handR, 1f - Mathf.Exp(-12f * dt));
                driverRigged.HandTargetL = driverL - DriverHip;
                driverRigged.HandTargetR = driverR - DriverHip;
                driverRigged.GripL = belt == Belt.Dragging || belt == Belt.Assisting ? 0.75f : 1f; // both hands hold the wheel
                driverRigged.GripR = 1f;
                driverRigged.Lean = lean;
                driverRigged.Roll = roll;
                driverRigged.Pose();
            }
        }

        private Vector3 WheelPoint(float steer, float angleDeg)
        {
            float a = (angleDeg - steer * 110f) * Mathf.Deg2Rad;
            Vector3 onRim = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * WheelRadius;
            return root.InverseTransformPoint(wheelSpin.parent.TransformPoint(onRim)) + new Vector3(0f, -0.03f, -0.08f); // wrist just behind the rim, fingers round it
        }

        /// <summary>Where the driver looks (camera yaw / pitch relative to the car, in degrees).</summary>
        public void GetLook(out float yaw, out float pitch)
        {
            if (belt == Belt.Hanging || belt == Belt.Returning)
            {
                // turn to the belt hanging by the left shoulder
                yaw = lookAtBelt.x;
                pitch = lookAtBelt.y;
                return;
            }
            if (belt == Belt.Dragging || belt == Belt.Assisting)
            {
                // follow the belt across the body to the buckle at the right hip
                yaw = 38f;
                pitch = 52f;
                return;
            }
            float steer = controls != null ? controls.Steer : 0f;
            yaw = 9f + steer * lookIntoTurn * 0.6f; // a little towards the middle, so the friend is in view
            pitch = lookAhead;
            // a quick glance at the friend when they talk while the car is (almost) stopped
            if (friend != null && friend.IsTalking && friend.TalkElapsed < 1.6f && car != null && Mathf.Abs(car.Speed) < 1.5f)
            {
                yaw = lookAtFriend.x;
                pitch = lookAtFriend.y;
            }
        }
    }
}
