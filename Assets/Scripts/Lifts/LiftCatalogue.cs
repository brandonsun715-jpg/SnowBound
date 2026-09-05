using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Lifts
{
    /// <summary>
    /// What sort of lift this is. The differences are mechanical, not
    /// cosmetic: a surface lift tows a rider along the snow, a chair carries
    /// them above it, and a gondola encloses them.
    /// </summary>
    public enum LiftKind { Surface, Chair, HighSpeedChair, Gondola }

    /// <summary>
    /// A lift the player can buy: what it costs, what it moves, and what it
    /// is physically like. Adding a lift type is adding an entry here.
    /// </summary>
    [System.Serializable]
    public class LiftDefinition
    {
        public LiftKind kind = LiftKind.Chair;
        public string name = "Chairlift";
        public string speedWord = "Medium";
        public string comfortWord = "Fair";

        public float cost = 15000f;
        public float dailyUpkeep = 2100f;

        [Tooltip("Metres per second along the line.")]
        public float lineSpeed = 2.4f;
        [Tooltip("Metres between carriers.")]
        public float carrierSpacing = 22f;
        [Tooltip("People per carrier.")]
        public int seats = 4;

        [Tooltip("Longest line this lift can span.")]
        public float maxLength = 420f;
        [Tooltip("Steepest average grade this lift will run up.")]
        public float maxGrade = 0.55f;

        [Range(0f, 1f)] public float quality = 0.5f;

        /// <summary>The figure the shop quotes: people carried per hour.</summary>
        public int GuestsPerHour
        {
            get
            {
                if (carrierSpacing < 0.1f) return 0;
                return Mathf.RoundToInt(lineSpeed * 3600f / carrierSpacing * seats);
            }
        }

        /// <summary>Riders keep their skis on the snow rather than hanging above it.</summary>
        public bool Towed { get { return kind == LiftKind.Surface; } }
    }

    /// <summary>The lift shop's contents.</summary>
    public static class LiftCatalogue
    {
        static List<LiftDefinition> _all;

        public static IReadOnlyList<LiftDefinition> All
        {
            get
            {
                if (_all != null) return _all;

                _all = new List<LiftDefinition>
                {
                    // A drag lift. Cheap, slow, and it cannot pull anyone up
                    // anything steep, which is exactly why it belongs on the
                    // beginner slope and nowhere else.
                    new LiftDefinition
                    {
                        kind = LiftKind.Surface, name = "Surface Lift",
                        speedWord = "Slow", comfortWord = "Basic",
                        cost = 2500f, dailyUpkeep = 340f,
                        lineSpeed = 2.2f, carrierSpacing = 12f, seats = 1,
                        maxLength = 260f, maxGrade = 0.34f,
                        quality = 0.32f
                    },

                    new LiftDefinition
                    {
                        kind = LiftKind.Chair, name = "Chairlift",
                        speedWord = "Medium", comfortWord = "Fair",
                        cost = 15000f, dailyUpkeep = 2100f,
                        lineSpeed = 2.5f, carrierSpacing = 26f, seats = 4,
                        maxLength = 620f, maxGrade = 0.7f,
                        quality = 0.55f
                    },

                    // Detachable grip: the carriers slow to walking pace in the
                    // terminals and run much faster on the line, which is what
                    // lets it be both quicker and easier to get on.
                    new LiftDefinition
                    {
                        kind = LiftKind.HighSpeedChair, name = "High-Speed Chair",
                        speedWord = "Fast", comfortWord = "Good",
                        cost = 35000f, dailyUpkeep = 4200f,
                        lineSpeed = 5f, carrierSpacing = 18f, seats = 6,
                        maxLength = 900f, maxGrade = 0.75f,
                        quality = 0.78f
                    },

                    new LiftDefinition
                    {
                        kind = LiftKind.Gondola, name = "Gondola",
                        speedWord = "Fast", comfortWord = "High",
                        cost = 60000f, dailyUpkeep = 6400f,
                        lineSpeed = 6f, carrierSpacing = 22f, seats = 8,
                        maxLength = 1200f, maxGrade = 0.85f,
                        quality = 0.92f
                    }
                };

                return _all;
            }
        }

        public static LiftDefinition Find(LiftKind kind)
        {
            IReadOnlyList<LiftDefinition> all = All;

            for (int i = 0; i < all.Count; i++)
                if (all[i].kind == kind) return all[i];

            return all[1];
        }
    }
}
