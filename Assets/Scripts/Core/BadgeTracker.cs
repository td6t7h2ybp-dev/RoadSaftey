using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Watches the stars and the end of the level and unlocks the badges (US 3.2):
    ///   seatbelt / red light / pedestrians / ball → the badge of that scenario;
    ///   10 stars in total → "جامع النجوم"; a level without mistakes → "بطل السلامة".
    /// Added automatically by GameplayBootstrap.
    /// </summary>
    public class BadgeTracker : MonoBehaviour
    {
        private ProgressionManager progression;

        public int StarsAtLevelEnd { get; private set; }

        private void Start()
        {
            progression = ProgressionManager.Instance;
            if (progression == null)
            {
                enabled = false;
                return;
            }
            progression.StarGained += OnStarGained;
            progression.LevelEnded += OnLevelEnded;
        }

        private void OnDestroy()
        {
            if (progression == null)
                return;
            progression.StarGained -= OnStarGained;
            progression.LevelEnded -= OnLevelEnded;
        }

        private void OnStarGained(int total, string reason)
        {
            switch (reason)
            {
                case GoodAction.Seatbelt: Badges.Unlock(BadgeId.Seatbelt); break;
                case GoodAction.StoppedAtRedLight: Badges.Unlock(BadgeId.PatientDriver); break;
                case GoodAction.LetPedestriansCross: Badges.Unlock(BadgeId.PedestrianFriend); break;
                case GoodAction.SlowedDownForDanger: Badges.Unlock(BadgeId.EagleEye); break;
            }
        }

        private void OnLevelEnded()
        {
            StarsAtLevelEnd = progression.Stars;
            Badges.AddToTotalStars(progression.Stars);
            if (Badges.TotalStars >= Badges.StarsForCollector)
                Badges.Unlock(BadgeId.StarCollector);
            if (progression.Mistakes.Count == 0)
                Badges.Unlock(BadgeId.SafetyHero);
        }
    }
}
