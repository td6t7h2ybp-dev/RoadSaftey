using System;
using UnityEngine;

namespace RoadSafety.Core
{
    public enum BadgeId
    {
        Seatbelt,
        PatientDriver,
        PedestrianFriend,
        EagleEye,
        StarCollector,
        SafetyHero
    }

    /// <summary>One badge: Arabic title, how to get it, and its sprite in Resources/HUD.</summary>
    public sealed class BadgeInfo
    {
        public readonly BadgeId Id;
        public readonly string Title;
        public readonly string HowTo;
        public readonly string Sprite;

        public BadgeInfo(BadgeId id, string title, string howTo, string sprite)
        {
            Id = id;
            Title = title;
            HowTo = howTo;
            Sprite = sprite;
        }
    }

    /// <summary>
    /// Badges (US 3.2): unlocked after a specific scenario or after X stars in total,
    /// saved on the tablet (PlayerPrefs) so they stay from one game to the next.
    /// </summary>
    public static class Badges
    {
        /// <summary>Stars needed in total (all levels together) for the "star collector" badge.</summary>
        public const int StarsForCollector = 10;

        private const string TotalStarsKey = "rs_total_stars";

        public static readonly BadgeInfo[] All =
        {
            new BadgeInfo(BadgeId.Seatbelt, "حزام الأمان", "ضع الحزام قبل الانطلاق", "badge_seatbelt"),
            new BadgeInfo(BadgeId.PatientDriver, "السائق الصبور", "انتظر الضوء الأخضر", "badge_patient"),
            new BadgeInfo(BadgeId.PedestrianFriend, "صديق المشاة", "اترك المشاة يعبرون", "badge_pedestrian"),
            new BadgeInfo(BadgeId.EagleEye, "العين اليقظة", "خفّف السرعة أمام الخطر", "badge_eagle"),
            new BadgeInfo(BadgeId.StarCollector, "جامع النجوم", "اجمع 10 نجوم", "badge_stars"),
            new BadgeInfo(BadgeId.SafetyHero, "بطل السلامة", "أكمل مرحلة دون أخطاء", "badge_hero"),
        };

        /// <summary>Raised the first time a badge is unlocked (the HUD shows a popup).</summary>
        public static event Action<BadgeInfo> Unlocked;

        public static BadgeInfo Get(BadgeId id) => All[(int)id];

        public static bool IsUnlocked(BadgeId id) => PlayerPrefs.GetInt(Key(id), 0) == 1;

        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                foreach (BadgeInfo badge in All)
                    if (IsUnlocked(badge.Id))
                        n++;
                return n;
            }
        }

        /// <summary>Unlocks the badge; returns false if it was already unlocked.</summary>
        public static bool Unlock(BadgeId id)
        {
            if (IsUnlocked(id))
                return false;
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
            Unlocked?.Invoke(Get(id));
            return true;
        }

        public static int TotalStars
        {
            get => PlayerPrefs.GetInt(TotalStarsKey, 0);
            private set { PlayerPrefs.SetInt(TotalStarsKey, value); PlayerPrefs.Save(); }
        }

        public static void AddToTotalStars(int stars)
        {
            if (stars > 0)
                TotalStars += stars;
        }

        /// <summary>Forgets every badge and the star total (menu RoadSafety > Reset badges).</summary>
        public static void ResetAll()
        {
            foreach (BadgeInfo badge in All)
                PlayerPrefs.DeleteKey(Key(badge.Id));
            PlayerPrefs.DeleteKey(TotalStarsKey);
            PlayerPrefs.Save();
        }

        private static string Key(BadgeId id) => "rs_badge_" + id;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Unlocked = null;
    }
}
