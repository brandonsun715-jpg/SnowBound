using UnityEngine;

namespace SnowBound.Mountain
{
    public enum PisteGrade
    {
        Beginner,
        Intermediate,
        Advanced
    }

    /// <summary>
    /// One marked run down the mountain.
    ///
    /// Runs fan out from the base area, spread apart through the middle of
    /// the mountain and come back together at the summit, so a single lift
    /// serves all of them. That shape is what a real resort looks like from
    /// above, and it is why anchorX and spreadX are separate numbers.
    /// </summary>
    [System.Serializable]
    public class PisteDefinition
    {
        public string name = "Piste";
        public PisteGrade grade = PisteGrade.Intermediate;

        [Header("Line")]
        [Tooltip("Where this run sits at the base and at the summit, where all runs meet.")]
        public float anchorX = 10f;
        [Tooltip("How far it swings away from the anchor through the middle of the mountain.")]
        public float spreadX = 55f;
        [Tooltip("How far it snakes left and right, in metres.")]
        public float snakeAmplitude = 24f;
        public float snakeFrequency = 0.013f;
        [Tooltip("Offsets the snake so two runs do not weave in step.")]
        public float snakePhase = 0f;

        [Header("Width")]
        public float halfWidth = 24f;
        [Tooltip("Extra width near the base, so the bottom of the hill is open.")]
        public float baseExtraWidth = 28f;

        [Header("Surface")]
        [Tooltip("Bumpiness of the groomed snow. Higher reads as a harder run.")]
        public float surfaceNoise = 1.1f;
        [Tooltip("Whether the rollers are built into this run.")]
        public bool hasRollers = true;

        public Color MarkerColour
        {
            get
            {
                switch (grade)
                {
                    case PisteGrade.Beginner: return new Color(0.10f, 0.62f, 0.28f);
                    case PisteGrade.Advanced: return new Color(0.82f, 0.11f, 0.13f);
                    default: return new Color(0.10f, 0.35f, 0.85f);
                }
            }
        }
    }
}
