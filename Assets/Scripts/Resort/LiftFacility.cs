using UnityEngine;
using SnowBound.Lifts;

namespace SnowBound.Resort
{
    /// <summary>
    /// A lift as a business: it costs money to run, its level decides how fast
    /// it moves, how close together the carriers are and how many each holds,
    /// and all three are things a guest waiting at the bottom can feel.
    ///
    /// What sort of lift it is comes from the catalogue entry it was bought
    /// from, so this does not need to know what a gondola is.
    /// </summary>
    public class LiftFacility : Facility
    {
        public Chairlift lift;
        public LiftKind kind = LiftKind.Chair;

        LiftDefinition _definition;

        public LiftDefinition Definition
        {
            get
            {
                if (_definition == null || _definition.kind != kind) _definition = LiftCatalogue.Find(kind);
                return _definition;
            }
        }

        public int Seats { get { return Definition.seats + (level - 1); } }
        public float LineSpeed { get { return Definition.lineSpeed * (1f + 0.18f * (level - 1)); } }
        public float Spacing { get { return Definition.carrierSpacing * (1f - 0.12f * (level - 1)); } }

        /// <summary>The figure that matters: people this lift moves per hour.</summary>
        public int GuestsPerHour
        {
            get { return Mathf.RoundToInt(LineSpeed * 3600f / Mathf.Max(1f, Spacing) * Seats); }
        }

        public override string LevelSummary
        {
            get
            {
                return GuestsPerHour.ToString("N0") + " guests / hour  ·  "
                     + Mathf.RoundToInt(LineSpeed * 3.6f) + " km/h";
            }
        }

        /// <summary>Set this facility up around a lift that was just bought.</summary>
        public void Adopt(Chairlift rig, LiftDefinition definition)
        {
            lift = rig;
            kind = definition.kind;
            _definition = definition;

            displayName = definition.name;
            baseDailyUpkeep = definition.dailyUpkeep;
            upkeepPerLevel = definition.dailyUpkeep * 0.55f;
            baseQuality = definition.quality;
            qualityPerLevel = 0.14f;
            baseUpgradeCost = definition.cost * 0.6f;
            upgradeCostPerLevel = definition.cost * 0.75f;
            maxLevel = 3;

            ApplyLevel();
        }

        public override void ApplyLevel()
        {
            if (lift == null) lift = GetComponent<Chairlift>();
            if (lift == null) return;

            float spacing = Spacing;
            int seats = Seats;

            bool needsRebuild = !Mathf.Approximately(lift.chairSpacing, spacing) || lift.seats != seats;

            lift.lineSpeed = LineSpeed;
            lift.chairSpacing = spacing;
            lift.seats = seats;

            // Speed is live, but how many carriers are on the rope is not.
            if (needsRebuild && Application.isPlaying) lift.Build();
        }
    }
}
