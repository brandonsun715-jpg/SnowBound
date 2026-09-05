using UnityEngine;

namespace SnowBound.Resort
{
    /// <summary>
    /// Anything the resort owns, runs and can improve: the lift, the lodge,
    /// the park, and later every building the player puts down.
    ///
    /// A facility answers three questions and no others. What does it cost to
    /// run for a day, how good is it, and what does its level do. Everything
    /// else — the money, the rating, the upgrade menu — reads those answers
    /// rather than knowing what a chairlift is.
    /// </summary>
    public abstract class Facility : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Facility";

        [Header("Level")]
        [Range(1, 5)] public int level = 1;
        public int maxLevel = 3;

        [Header("Running costs")]
        public float baseDailyUpkeep = 400f;
        public float upkeepPerLevel = 260f;

        [Header("Quality")]
        [Range(0f, 1f)] public float baseQuality = 0.45f;
        public float qualityPerLevel = 0.18f;

        [Header("Upgrading")]
        public float baseUpgradeCost = 6000f;
        public float upgradeCostPerLevel = 5000f;

        public float DailyUpkeep { get { return baseDailyUpkeep + (level - 1) * upkeepPerLevel; } }

        /// <summary>0 to 1. Feeds the resort rating.</summary>
        public float Quality { get { return Mathf.Clamp01(baseQuality + (level - 1) * qualityPerLevel); } }

        public bool CanUpgrade { get { return level < maxLevel; } }
        public float UpgradeCost { get { return baseUpgradeCost + (level - 1) * upgradeCostPerLevel; } }

        /// <summary>One line describing what this level actually gives you.</summary>
        public abstract string LevelSummary { get; }

        /// <summary>
        /// Whether this facility actually exists yet. A component can be in the
        /// scene waiting to be built; until it is, it costs nothing to run and
        /// counts for nothing.
        /// </summary>
        public virtual bool Operating { get { return true; } }

        void Start() { ApplyLevel(); }

        /// <summary>Push this facility's level into whatever it actually controls.</summary>
        public virtual void ApplyLevel() { }

        public void SetLevel(int value)
        {
            level = Mathf.Clamp(value, 1, maxLevel);
            ApplyLevel();
        }
    }
}
