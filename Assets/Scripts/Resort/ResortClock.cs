using UnityEngine;

namespace SnowBound.Resort
{
    /// <summary>
    /// The resort's working day. Everything in the tycoon layer is measured
    /// against this: guests arrive over it, upkeep is charged across it, and
    /// the books are closed at the end of it.
    ///
    /// The clock only ever covers opening hours. There is no night, because a
    /// night is time the player cannot do anything with; closing time rolls
    /// straight into the next morning once the day's figures are read.
    ///
    /// It also owns where the sun is, since the time of day is the only thing
    /// that should decide that.
    /// </summary>
    public class ResortClock : MonoBehaviour
    {
        static ResortClock _instance;

        public static ResortClock Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<ResortClock>();
                return _instance;
            }
        }

        [Header("Length")]
        [Tooltip("Real minutes for one full resort day.")]
        public float dayLengthMinutes = 7f;

        [Header("Opening hours")]
        public float opensAt = 8f;
        public float closesAt = 17f;

        [Header("Sun")]
        [Tooltip("Degrees above the horizon at opening and closing.")]
        public float sunLowAngle = 9f;
        [Tooltip("Degrees above the horizon at midday. Winter sun stays low.")]
        public float sunHighAngle = 42f;
        public float sunriseBearing = 110f;
        public float sunsetBearing = 250f;

        public int Day { get; private set; } = 1;
        public float Hour { get; private set; }
        public bool Closed { get; private set; }

        /// <summary>0 at opening, 1 at closing.</summary>
        public float DayProgress { get { return Mathf.InverseLerp(opensAt, closesAt, Hour); } }

        /// <summary>Where the sun is. Low and long at either end of the day.</summary>
        public Quaternion SunRotation { get; private set; } = Quaternion.Euler(35f, 150f, 0f);

        /// <summary>How much light there is, before the weather takes its cut.</summary>
        public float Daylight { get; private set; } = 1f;

        public event System.Action<int> DayEnded;

        public string TimeText
        {
            get
            {
                int hours = Mathf.FloorToInt(Hour);
                int minutes = Mathf.FloorToInt((Hour - hours) * 60f);
                return hours.ToString("00") + ":" + minutes.ToString("00");
            }
        }

        void OnEnable() { _instance = this; }

        void Awake()
        {
            Hour = opensAt;
            UpdateSun();
        }

        void Update()
        {
            if (Closed) return;

            float hoursPerSecond = (closesAt - opensAt) / Mathf.Max(10f, dayLengthMinutes * 60f);
            Hour += hoursPerSecond * Time.deltaTime;

            UpdateSun();

            if (Hour < closesAt) return;

            Hour = closesAt;
            Closed = true;
            if (DayEnded != null) DayEnded(Day);
        }

        /// <summary>Roll on to tomorrow morning. Called once the books are read.</summary>
        public void StartNextDay()
        {
            Day++;
            Hour = opensAt;
            Closed = false;
            UpdateSun();
        }

        void UpdateSun()
        {
            float t = Mathf.Clamp01(DayProgress);
            float arc = Mathf.Sin(t * Mathf.PI);

            float elevation = Mathf.Lerp(sunLowAngle, sunHighAngle, arc);
            float bearing = Mathf.Lerp(sunriseBearing, sunsetBearing, t);

            SunRotation = Quaternion.Euler(elevation, bearing, 0f);

            // Never quite dark, because the lifts are shut before it would be.
            Daylight = Mathf.Clamp01(0.38f + arc * 0.72f);
        }
    }
}
