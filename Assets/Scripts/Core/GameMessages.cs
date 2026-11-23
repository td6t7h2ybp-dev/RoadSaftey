using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Small message bus between the world and the HUD, so world scripts never touch the UI:
    /// <code>
    ///     GameMessages.Say(Lines.LightTurnsRed);     // the avatar says it (bubble + voice)
    ///     GameMessages.PlaySfx(myClip);              // short sound effect (respects the SFX setting)
    /// </code>
    /// Texts are Modern Standard Arabic, in logical order (the HUD shapes them with ArabicText).
    /// The lines of <see cref="Lines"/> also have a recorded voice (Resources/Audio/VO), because
    /// 5-year-olds can't read yet.
    /// </summary>
    public static class GameMessages
    {
        /// <summary>Raised when the avatar must say something.</summary>
        public static event Action<string> Said;

        /// <summary>Raised when a sound effect must be played by the HUD.</summary>
        public static event Action<AudioClip> SfxRequested;

        public static void Say(string arabicText)
        {
            if (!string.IsNullOrEmpty(arabicText))
                Said?.Invoke(arabicText);
        }

        public static void PlaySfx(AudioClip clip)
        {
            if (clip != null)
                SfxRequested?.Invoke(clip);
        }

        // "Enter Play Mode" without domain reload keeps static events alive between two Play
        // sessions: clear them so old listeners from the previous session are not called.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Said = null;
            SfxRequested = null;
        }
    }

    /// <summary>Arabic texts used by the rules, in one place so they stay consistent.</summary>
    public static class Lines
    {
        public const string SeatbeltFirst = "ضع حزام الأمان\nقبل أن تنطلق!";
        public const string SeatbeltDone = "أحسنت! حزام الأمان يحميك";
        public const string Collision = "انتبه، انظر\nقبل أن تتقدّم!";
        public const string LightTurnsRed = "الضوء أحمر،\nتوقّف قبل الخط!";
        public const string LightGreenGo = "الضوء أخضر،\nيمكنك الانطلاق!";
        public const string RanRedLight = "توقّف! لا نمرّ\nعندما يكون الضوء أحمر";
        public const string WellDoneRedLight = "أحسنت! انتظرت\nالضوء الأخضر";
        public const string PedestrianWaiting = "طفل يريد أن يعبر،\nتوقّف ليمرّ";
        public const string PedestrianViolation = "قف! اترك المشاة\nيعبرون أوّلًا";
        public const string PedestrianThanks = "شكرًا! أنت سائق لطيف";
        public const string KidsPlaying = "انتبه! أطفال يلعبون،\nخفّف السرعة";
        public const string SlowedDown = "أحسنت! خفّفت السرعة\nفي الوقت المناسب";
        public const string BallTooFast = "انتبه للكرة!";
        public const string Finish = "وصلت بالسلامة!";
        public const string WrongLane = "انتبه! سيّارة أمامك";
        public const string TryAgain = "لا بأس! لنحاول مرة أخرى";

        private static readonly Dictionary<string, string> Voices = new Dictionary<string, string>
        {
            { SeatbeltFirst, "VO_Seatbelt_ar" },
            { SeatbeltDone, "vo_belt_done" },
            { Collision, "vo_collision" },
            { LightTurnsRed, "vo_light_red" },
            { LightGreenGo, "vo_light_green" },
            { RanRedLight, "vo_ran_red" },
            { WellDoneRedLight, "vo_well_done_red" },
            { PedestrianWaiting, "vo_ped_waiting" },
            { PedestrianViolation, "vo_ped_violation" },
            { PedestrianThanks, "vo_thanks" },
            { KidsPlaying, "vo_kids" },
            { SlowedDown, "vo_slowed" },
            { BallTooFast, "vo_ball" },
            { Finish, "vo_finish" },
            { WrongLane, "vo_wrong_lane" },
            { TryAgain, "vo_try_again" },
        };

        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>The voice clip for this line (Resources/Audio/VO/...), or null if it has none.</summary>
        public static AudioClip VoiceFor(string line)
        {
            if (line == null || !Voices.TryGetValue(line, out string file))
                return null;
            if (!Cache.TryGetValue(file, out AudioClip clip) || clip == null)
            {
                clip = Resources.Load<AudioClip>("Audio/VO/" + file);
                Cache[file] = clip;
            }
            return clip;
        }
    }
}
