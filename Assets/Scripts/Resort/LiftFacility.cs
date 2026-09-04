using UnityEngine;
using SnowBound.Lifts;

namespace SnowBound.Resort
{
    /// <summary>
    /// The chairlift as a business: it costs money to run, its level decides
    /// how fast it moves and how close together the chairs are, and both of
    /// those are things a guest waiting at the bottom can feel.
    /// </summary>
    public class LiftFacility : Facility
    {
        public Chairlift lift;

        [Header("By level")]
        public float[] lineSpeeds = { 7f, 9.5f, 12f };
        public float[] chairSpacings = { 32f, 26f, 20f };
        public int[] seatsPerChair = { 4, 6, 8 };

        public int Seats { get { return Pick(seatsPerChair, 4); } }
        public float LineSpeed { get { return Pick(lineSpeeds, 9f); } }

        public override string LevelSummary
        {
            get
            {
                return "Capacity " + Seats + " per chair  ·  " +
                       Mathf.RoundToInt(LineSpeed * 3.6f) + " km/h";
            }
        }

        public override void ApplyLevel()
        {
            if (lift == null) lift = Chairlift.Instance;
            if (lift == null) return;

            float spacing = Pick(chairSpacings, 26f);
            bool needsRebuild = !Mathf.Approximately(lift.chairSpacing, spacing);

            lift.lineSpeed = LineSpeed;
            lift.chairSpacing = spacing;

            // Speed is live, but the number of chairs on the cable is not.
            if (needsRebuild && Application.isPlaying) lift.Build();
        }

        T Pick<T>(T[] table, T fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(level - 1, 0, table.Length - 1)];
        }
    }
}
