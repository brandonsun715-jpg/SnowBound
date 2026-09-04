using UnityEngine;

namespace SnowBound.Resort
{
    /// <summary>
    /// The lodge as a business. A bigger lodge takes more money per guest and
    /// makes them happier, and costs more to keep the lights on.
    /// </summary>
    public class LodgeFacility : Facility
    {
        [Header("By level")]
        [Tooltip("Multiplier on what each guest spends inside.")]
        public float[] spendMultipliers = { 1f, 1.5f, 2.1f };
        public string[] descriptions =
        {
            "Bar and boot room",
            "Bar, cafe and boot room",
            "Bar, restaurant, shop and boot room"
        };

        public float SpendMultiplier
        {
            get
            {
                if (spendMultipliers == null || spendMultipliers.Length == 0) return 1f;
                return spendMultipliers[Mathf.Clamp(level - 1, 0, spendMultipliers.Length - 1)];
            }
        }

        public override string LevelSummary
        {
            get
            {
                if (descriptions == null || descriptions.Length == 0) return "Lodge";
                return descriptions[Mathf.Clamp(level - 1, 0, descriptions.Length - 1)];
            }
        }
    }
}
