using UnityEngine;
using SnowBound.Mountain;

namespace SnowBound.Resort
{
    /// <summary>
    /// The snow park as a business. It draws a particular kind of guest, so
    /// its level decides how many features get built as well as how much it
    /// costs to shape them every morning.
    /// </summary>
    public class ParkFacility : Facility
    {
        public TerrainPark park;

        [Header("By level")]
        public int[] kickersByLevel = { 2, 3, 4 };
        [Tooltip("Multiplier on what park riders spend.")]
        public float[] spendMultipliers = { 1f, 1.6f, 2.3f };

        public int Kickers { get { return Pick(kickersByLevel, 3); } }
        public float SpendMultiplier { get { return Pick(spendMultipliers, 1f); } }

        public override string LevelSummary
        {
            get { return Kickers + " kickers  ·  boxes"; }
        }

        public override void ApplyLevel()
        {
            if (park == null) park = FindAnyObjectByType<TerrainPark>();
            if (park == null) return;

            if (park.kickerCount == Kickers) return;

            park.kickerCount = Kickers;
            if (Application.isPlaying) park.Build();
        }

        T Pick<T>(T[] table, T fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(level - 1, 0, table.Length - 1)];
        }
    }
}
