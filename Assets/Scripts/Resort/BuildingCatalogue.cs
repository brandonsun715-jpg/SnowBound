using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Resort
{
    public enum BuildingKind
    {
        SkiRental,
        Restaurant,
        SkiShop,
        TicketBooth,
        WarmingHut
    }

    /// <summary>
    /// Everything the player can put down: what it costs, what it takes, and
    /// what it looks like. Data only, so adding a building is adding an entry
    /// rather than writing a class.
    /// </summary>
    [System.Serializable]
    public class BuildingDefinition
    {
        public BuildingKind kind;
        public string name = "Building";
        public string firstEffect = "";
        public string secondEffect = "";

        public float cost = 5000f;
        public float dailyUpkeep = 600f;

        [Tooltip("Width and depth in metres.")]
        public Vector2 footprint = new Vector2(10f, 8f);
        public float wallHeight = 4.5f;
        public float roofHeight = 2.6f;

        public Color wall = new Color(0.44f, 0.29f, 0.19f);
        public Color roof = new Color(0.18f, 0.17f, 0.20f);
        public Color trim = new Color(0.20f, 0.62f, 0.85f);

        [Header("Trade")]
        public LedgerLine line = LedgerLine.Rentals;
        [Range(0f, 1f)] public float visitChance = 0.3f;
        public float spendPerVisit = 24f;

        [Header("Resort")]
        [Range(0f, 1f)] public float quality = 0.5f;
        [Tooltip("Added to every guest's happiness while this stands.")]
        public float happiness = 0.03f;
    }

    /// <summary>The build menu's contents.</summary>
    public static class BuildingCatalogue
    {
        static List<BuildingDefinition> _all;

        public static IReadOnlyList<BuildingDefinition> All
        {
            get
            {
                if (_all != null) return _all;

                _all = new List<BuildingDefinition>
                {
                    new BuildingDefinition
                    {
                        kind = BuildingKind.SkiRental, name = "Ski Rental",
                        firstEffect = "Rental income  +$34 / guest",
                        secondEffect = "Happiness  +4",
                        cost = 5200f, dailyUpkeep = 620f,
                        footprint = new Vector2(12f, 8f), wallHeight = 4.4f,
                        wall = new Color(0.40f, 0.27f, 0.18f), trim = new Color(0.20f, 0.62f, 0.85f),
                        line = LedgerLine.Rentals, visitChance = 0.34f, spendPerVisit = 34f,
                        quality = 0.55f, happiness = 0.04f
                    },
                    new BuildingDefinition
                    {
                        kind = BuildingKind.Restaurant, name = "Restaurant",
                        firstEffect = "Food income  +$41 / guest",
                        secondEffect = "Happiness  +7",
                        cost = 9400f, dailyUpkeep = 1350f,
                        footprint = new Vector2(15f, 11f), wallHeight = 5.2f, roofHeight = 3.2f,
                        wall = new Color(0.46f, 0.31f, 0.21f), trim = new Color(0.93f, 0.75f, 0.42f),
                        line = LedgerLine.Lodge, visitChance = 0.46f, spendPerVisit = 41f,
                        quality = 0.68f, happiness = 0.07f
                    },
                    new BuildingDefinition
                    {
                        kind = BuildingKind.SkiShop, name = "Ski Shop",
                        firstEffect = "Retail income  +$52 / guest",
                        secondEffect = "Happiness  +3",
                        cost = 7600f, dailyUpkeep = 900f,
                        footprint = new Vector2(11f, 9f), wallHeight = 4.8f,
                        wall = new Color(0.34f, 0.30f, 0.26f), trim = new Color(0.47f, 0.82f, 0.59f),
                        line = LedgerLine.Rentals, visitChance = 0.22f, spendPerVisit = 52f,
                        quality = 0.52f, happiness = 0.03f
                    },
                    new BuildingDefinition
                    {
                        kind = BuildingKind.TicketBooth, name = "Ticket Booth",
                        firstEffect = "Shorter queues",
                        secondEffect = "Happiness  +5",
                        cost = 2400f, dailyUpkeep = 320f,
                        footprint = new Vector2(5f, 5f), wallHeight = 3.4f, roofHeight = 1.8f,
                        wall = new Color(0.30f, 0.32f, 0.38f), trim = new Color(0.56f, 0.78f, 0.95f),
                        line = LedgerLine.Tickets, visitChance = 0.5f, spendPerVisit = 8f,
                        quality = 0.45f, happiness = 0.05f
                    },
                    new BuildingDefinition
                    {
                        kind = BuildingKind.WarmingHut, name = "Warming Hut",
                        firstEffect = "Drinks income  +$16 / guest",
                        secondEffect = "Happiness  +6",
                        cost = 3600f, dailyUpkeep = 430f,
                        footprint = new Vector2(8f, 7f), wallHeight = 3.8f,
                        wall = new Color(0.38f, 0.26f, 0.18f), trim = new Color(0.90f, 0.47f, 0.45f),
                        line = LedgerLine.Lodge, visitChance = 0.38f, spendPerVisit = 16f,
                        quality = 0.50f, happiness = 0.06f
                    }
                };

                return _all;
            }
        }
    }
}
