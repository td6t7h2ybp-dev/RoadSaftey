using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// The child's friend, sitting on the passenger seat. They replace the avatar of the HUD:
    /// every line of the game (GameMessages.Say) is said by them, with the game's recorded voices
    /// (Resources/Audio/VO, the same files as before), from their mouth.
    ///   - talking: turns to the driver, the mouth follows the voice, the hand moves;
    ///   - seatbelt reminder: points at the driver's belt;
    ///   - danger ahead (red light, children, ball...): points at the road;
    ///   - well done / thank you / arrived: cheers with both arms;
    ///   - collision: holds on to the dashboard;
    ///   - otherwise: breathes, blinks, looks around, sometimes at the driver; giggles at the horn.
    /// They always wear their seatbelt (a good example).
    /// </summary>
    public class PassengerFriend : MonoBehaviour
    {
        public static PassengerFriend Current { get; private set; }

        private enum Gesture { None, PointBelt, PointRoad, Cheer, Brace, Wave }

        private KidRig rig;
        private RiggedKid rigged;        // the real rigged boy, when the model is in the project
        private Vector3 smoothL, smoothR;
        private bool hasSmooth;
        private CarInterior interior;
        private Vector3 hip;
        private AudioSource voice;
        private readonly float[] samples = new float[256];

        private float talkStart = -10f, talkUntil = -10f;
        private Gesture gesture;
        private float gestureUntil;
        private float bounce;
        private float headYaw, headPitch;
        private float idleTarget, nextIdleLook;
        private float nextBlink, blinkUntil;
        private float mouthOpen;
        private float lean, roll;
        private DriverControls controls;

        public bool IsTalking => Time.time < talkUntil;

        /// <summary>Where the friend's head is in the world (the speech bubble points there).</summary>
        public Vector3 HeadWorld => rigged != null ? rigged.HeadPosition : transform.position + Vector3.up * 0.9f;
        public float TalkElapsed => Time.time - talkStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        internal void Build(Transform root, Vector3 passengerHip, Material skin, Material dark, Material metal, Material strap, CarInterior owner)
        {
            interior = owner;
            // sits turned towards the driver, so the child sees the friend's face (3/4 view)
            Transform seat = CockpitKit.Group("FriendSeat", root, passengerHip);
            seat.localRotation = Quaternion.identity; // sits straight in the seat, facing the road
            hip = Vector3.zero;
            Material top = CockpitKit.Mat("FriendTee", new Color(0.46f, 0.45f, 0.47f));      // grey T-shirt
            Material shorts = CockpitKit.Mat("FriendShorts", new Color(0.24f, 0.3f, 0.46f));  // blue shorts
            Material friendSkin = CockpitKit.Mat("FriendSkin", new Color(0.7f, 0.46f, 0.31f));
            Material hair = CockpitKit.Mat("FriendHair", new Color(0.13f, 0.08f, 0.05f));
            rig = KidRig.Build(seat, "FriendBody", Vector3.zero, top, shorts, friendSkin, hair, dark, true, true, top);
            // the real rigged boy (Sketchfab, CC-BY) replaces the simple shapes when it is in the project
            Texture2D stripes = CockpitKit.Paint(64, (u, v) => Mathf.Repeat(v * 9f, 1f) < 0.5f ? new Color(0.93f, 0.93f, 0.9f) : new Color(0.32f, 0.34f, 0.4f));
            stripes.wrapMode = TextureWrapMode.Repeat;
            rigged = RiggedKid.Spawn(seat, 1.15f, false, stripes, Color.white);
            if (rigged != null)
                foreach (Renderer r in seat.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(rigged.Root)) r.enabled = false;
            bool hideBelt = rigged != null; // the simple belt pieces do not fit the real body


            // their seatbelt, always on
            Vector3 anchor = new Vector3(0.78f, 1.55f, -0.7f), chest = passengerHip + new Vector3(0.02f, 0.17f, 0.115f);
            Vector3 buckle = new Vector3(0.1f, 1.2f, -0.05f), lapMid = passengerHip + new Vector3(0f, 0.01f, 0.11f), lapAnchor = new Vector3(0.62f, 1.2f, -0.1f);
            CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Cube, "FriendBelt", root, Vector3.zero, Vector3.one, strap), anchor, chest, 0.05f, 0.008f);
            CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Cube, "FriendBelt", root, Vector3.zero, Vector3.one, strap), chest, buckle, 0.05f, 0.008f);
            CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Cube, "FriendBelt", root, Vector3.zero, Vector3.one, strap), buckle, lapMid, 0.05f, 0.008f);
            CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Cube, "FriendBelt", root, Vector3.zero, Vector3.one, strap), lapMid, lapAnchor, 0.05f, 0.008f);
            CockpitKit.Part(PrimitiveType.Cube, "FriendBuckle", root, buckle, new Vector3(0.055f, 0.075f, 0.045f), metal, Quaternion.Euler(0f, 0f, 20f));
            CockpitKit.Part(PrimitiveType.Cube, "FriendBeltGuide", root, anchor, new Vector3(0.06f, 0.05f, 0.04f), metal);
            if (hideBelt)
                foreach (Transform t in root)
                    if (t.name.StartsWith("FriendBelt") || t.name == "FriendBuckle") t.gameObject.SetActive(false);

            // the voice comes from their mouth
            var mouthGo = new GameObject("Voice");
            mouthGo.transform.SetParent(rig.HeadPivot, false);
            voice = mouthGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.6f; // mostly from the friend's side, still clear for small ears
            voice.minDistance = 1.5f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = 30f;
            voice.dopplerLevel = 0f;
            voice.priority = 0;

            nextBlink = Time.time + 2f;
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        private void Start()
        {
            controls = DriverControls.Instance;
            if (controls != null)
                controls.Honked += OnHonked;
            // says hello when the level starts
            gesture = Gesture.Wave;
            gestureUntil = Time.time + 2.5f;
        }

        private void OnDestroy()
        {
            if (controls != null)
                controls.Honked -= OnHonked;
        }

        private void OnHonked()
        {
            bounce = Mathf.Max(bounce, 0.6f); // a little laugh at the friendly horn
            if (!IsTalking)
            {
                gesture = Gesture.Wave; // and waves at the people outside
                gestureUntil = Time.time + 1.5f;
            }
        }

        /// <summary>The friend says a line of the game (the HUD shows the bubble), with its recorded voice.</summary>
        public void Say(string line, AudioClip clip)
        {
            talkStart = Time.time;
            talkUntil = Time.time + (clip != null ? clip.length : 2f);
            if (clip != null && voice != null && AudioPrefs.SfxOn)
            {
                voice.Stop();
                voice.clip = clip;
                voice.Play();
            }

            gesture = GestureFor(line);
            gestureUntil = Time.time + (gesture == Gesture.Cheer ? 1.4f : gesture == Gesture.Brace ? 1.6f : Mathf.Max(1.6f, talkUntil - Time.time));
            if (gesture == Gesture.Cheer) bounce = 1f;
        }

        private static Gesture GestureFor(string line)
        {
            switch (line)
            {
                case Lines.SeatbeltFirst:
                    return Gesture.PointBelt;
                case Lines.Collision:
                    return Gesture.Brace;
                case Lines.SeatbeltDone:
                case Lines.WellDoneRedLight:
                case Lines.PedestrianThanks:
                case Lines.SlowedDown:
                case Lines.Finish:
                    return Gesture.Cheer;
                case Lines.LightTurnsRed:
                case Lines.LightGreenGo:
                case Lines.PedestrianWaiting:
                case Lines.PedestrianViolation:
                case Lines.KidsPlaying:
                case Lines.BallTooFast:
                case Lines.WrongLane:
                case Lines.RanRedLight:
                    return Gesture.PointRoad;
                default:
                    return Gesture.None;
            }
        }

        internal void SetLean(float pitchDeg, float rollDeg)
        {
            lean = pitchDeg;
            roll = rollDeg;
        }

        private void LateUpdate()
        {
            if (Time.deltaTime > 0f) Tick(Time.deltaTime);
        }

        internal void Tick(float dt)
        {
            if (rig == null || dt <= 0f) return;
            float t = Time.time;
            bool talking = IsTalking;
            Gesture g = t < gestureUntil ? gesture : Gesture.None;

            // body: breathing, bounce (cheer / horn), lean with the car
            bounce = Mathf.MoveTowards(bounce, 0f, dt * 0.9f);
            float hop = Mathf.Abs(Mathf.Sin(t * 14f)) * bounce * 0.035f;
            float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.015f;
            rig.Upper.localPosition = hip + new Vector3(0f, hop, 0f);
            rig.Upper.localScale = new Vector3(1f, breathe, 1f);
            float braceLean = g == Gesture.Brace ? 10f : 0f;
            rig.Lean(lean + braceLean, roll);

            // head: towards the driver while talking, at the belt / road when pointing, else looks around
            float yaw, pitch;
            if (g == Gesture.PointBelt) { yaw = -35f; pitch = 20f; }
            else if (g == Gesture.PointRoad) { yaw = 0f; pitch = 0f; }
            else if (g == Gesture.Wave) { yaw = -30f; pitch = -2f; }
            else if (talking) { yaw = -25f; pitch = 4f + Mathf.Sin(t * 5f) * 3f; }
            else
            {
                if (t > nextIdleLook)
                {
                    idleTarget = Random.value < 0.4f ? -20f : Random.Range(-8f, 30f); // sometimes at the driver, often out of the window
                    nextIdleLook = t + Random.Range(1.8f, 4.5f);
                }
                yaw = idleTarget;
                pitch = Mathf.Sin(t * 0.7f) * 3f;
            }
            float k = 1f - Mathf.Exp(-6f * dt);
            headYaw = Mathf.LerpAngle(headYaw, yaw, k);
            headPitch = Mathf.Lerp(headPitch, pitch, k);
            rig.HeadPivot.localRotation = Quaternion.Euler(headPitch, headYaw, Mathf.Sin(t * 3f) * (talking ? 4f : 1f));

            // blinking
            if (t > nextBlink)
            {
                blinkUntil = t + 0.12f;
                nextBlink = t + Random.Range(2f, 5f);
            }
            float eyeY = t < blinkUntil ? 0.15f : 1f;
            if (rig.EyeL != null) rig.EyeL.localScale = new Vector3(1f, eyeY, 1f);
            if (rig.EyeR != null) rig.EyeR.localScale = new Vector3(1f, eyeY, 1f);

            // mouth follows the loudness of the voice
            float target = 0f;
            if (voice != null && voice.isPlaying)
            {
                voice.GetOutputData(samples, 0);
                float sum = 0f;
                for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
                target = Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 9f);
            }
            else if (talking)
            {
                target = 0.5f + 0.5f * Mathf.Sin(t * 18f); // sound off: the mouth still moves
            }
            mouthOpen = Mathf.Lerp(mouthOpen, target, 1f - Mathf.Exp(-25f * dt));
            if (rig.Mouth != null) rig.Mouth.localScale = new Vector3(1f + mouthOpen * 0.15f, 1f + mouthOpen * 1.6f, 1f);

            // arms
            Vector3 restL = hip + new Vector3(-0.1f, 0.03f, 0.2f);
            Vector3 restR = hip + new Vector3(0.1f, 0.03f, 0.2f);
            Vector3 handL = restL, handR = restR;
            switch (g)
            {
                case Gesture.PointBelt:
                    if (interior != null)
                    {
                        Vector3 belt = rig.Root.InverseTransformPoint(interior.TongueWorld);
                        Vector3 s = rig.ShoulderPosition(true);
                        handL = s + (belt - s).normalized * 0.45f;
                    }
                    break;
                case Gesture.PointRoad:
                    handR = hip + new Vector3(0.0f, 0.3f, 0.48f);
                    break;
                case Gesture.Cheer:
                    float wave = Mathf.Sin(t * 12f) * 0.04f;
                    handL = hip + new Vector3(-0.2f, 0.4f + wave, 0.06f);
                    handR = hip + new Vector3(0.2f, 0.4f - wave, 0.06f);
                    break;
                case Gesture.Wave:
                    handR = hip + new Vector3(0.12f + Mathf.Sin(t * 9f) * 0.06f, 0.62f, 0.12f); // big friendly wave, like the picture
                    break;
                case Gesture.Brace:
                    handL = hip + new Vector3(-0.15f, 0.12f, 0.5f);
                    handR = hip + new Vector3(0.15f, 0.12f, 0.5f);
                    break;
            }
            if (talking && g == Gesture.None)
                handL = hip + new Vector3(-0.17f, 0.15f + Mathf.Sin(t * 6f) * 0.04f, 0.2f);
            float openL = g == Gesture.Wave || g == Gesture.Cheer ? 1f : g == Gesture.PointBelt ? 0.5f : talking ? 0.7f : 0.2f;
            float openR = g == Gesture.Cheer || g == Gesture.Brace || g == Gesture.Wave ? 1f : g == Gesture.PointRoad ? 0.5f : 0.2f;
            rig.ReachArm(true, handL, 10f, dt, openL);
            rig.ReachArm(false, handR, 10f, dt, openR);

            if (rigged != null)
            {
                float kk = 1f - Mathf.Exp(-10f * dt);
                if (!hasSmooth) { smoothL = handL; smoothR = handR; hasSmooth = true; }
                smoothL = Vector3.Lerp(smoothL, handL, kk);
                smoothR = Vector3.Lerp(smoothR, handR, kk);
                rigged.HandTargetL = smoothL;
                rigged.HandTargetR = smoothR;
                rigged.GripL = 1f - openL;
                rigged.GripR = 1f - openR;
                rigged.HeadYaw = headYaw;
                rigged.SpineYaw = 0f;
                rigged.HeadPitch = headPitch;
                rigged.JawOpen = mouthOpen;
                rigged.Lean = lean + braceLean;
                rigged.Roll = roll;
                rigged.Bounce = hop;
                rigged.Pose();
                if (voice != null) voice.transform.position = rigged.HeadPosition;
            }
        }
    }
}
