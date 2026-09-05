using System.Collections.Generic;
using UnityEngine;
using SnowBound.Mountain;
using SnowBound.Weather;

namespace SnowBound.Resort
{
    /// <summary>
    /// How good the resort is, out of five.
    ///
    /// Built out of weighted factors rather than one number, because the
    /// rating is only useful if the player can see which part of it is
    /// dragging. Each factor is 0 to 1 and says what it is called, so the
    /// dashboard can list them without knowing what any of them mean.
    ///
    /// Guest happiness joins this list once there are guests to be happy.
    /// </summary>
    public class ResortRating : MonoBehaviour
    {
        static ResortRating _instance;

        public static ResortRating Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<ResortRating>();
                return _instance;
            }
        }

        public class Factor
        {
            public string name;
            public float weight;
            public float value;
        }

        [Tooltip("How quickly the rating follows a change. Slow, so it reads as a trend.")]
        public float settleSpeed = 0.6f;

        public float Score { get; private set; }
        public float Stars { get { return Score * 5f; } }

        readonly List<Factor> _factors = new List<Factor>();
        public IReadOnlyList<Factor> Factors { get { return _factors; } }

        MountainGenerator _mountain;
        WeatherSystem _weather;
        LiftFacility _lift;
        LodgeFacility _lodge;
        ParkFacility _park;

        void OnEnable() { _instance = this; }

        void Awake()
        {
            _instance = this;

            _factors.Add(new Factor { name = "Lifts", weight = 0.22f });
            _factors.Add(new Factor { name = "Trails", weight = 0.20f });
            _factors.Add(new Factor { name = "Lodge", weight = 0.18f });
            _factors.Add(new Factor { name = "Variety", weight = 0.14f });
            _factors.Add(new Factor { name = "Conditions", weight = 0.14f });
            _factors.Add(new Factor { name = "Terrain Park", weight = 0.12f });
        }

        void Start()
        {
            _mountain = MountainGenerator.Instance;
            _weather = WeatherSystem.Instance;
            _lift = FindAnyObjectByType<LiftFacility>();
            _lodge = FindAnyObjectByType<LodgeFacility>();
            _park = FindAnyObjectByType<ParkFacility>();

            Measure();
            Score = Target();
        }

        void Update()
        {
            Measure();
            Score = Mathf.Lerp(Score, Target(), 1f - Mathf.Exp(-settleSpeed * Time.deltaTime));
        }

        float Target()
        {
            float total = 0f;
            float weights = 0f;

            for (int i = 0; i < _factors.Count; i++)
            {
                total += _factors[i].value * _factors[i].weight;
                weights += _factors[i].weight;
            }

            return weights > 0f ? Mathf.Clamp01(total / weights) : 0f;
        }

        void Measure()
        {
            if (_factors.Count < 6) return;

            _factors[0].value = _lift != null ? _lift.Quality : 0.2f;
            _factors[1].value = Trails();
            _factors[2].value = _lodge != null ? _lodge.Quality : 0.2f;
            _factors[3].value = Variety();
            _factors[4].value = Conditions();
            _factors[5].value = _park != null ? _park.Quality : 0f;
        }

        float Trails()
        {
            if (_mountain == null) return 0.3f;

            // More runs is better, with diminishing returns after a handful.
            float count = Mathf.Clamp01(_mountain.PisteCount / 5f);
            float groomed = 0.55f + 0.45f * count;

            // Fresh snow flatters a mountain; a whiteout does not.
            if (_weather != null) groomed *= Mathf.Lerp(1f, 1.12f, _weather.powder);

            return Mathf.Clamp01(groomed);
        }

        float Variety()
        {
            if (_mountain == null) return 0.2f;

            bool green = false, blue = false, black = false;
            for (int i = 0; i < _mountain.PisteCount; i++)
            {
                switch (_mountain.pistes[i].grade)
                {
                    case PisteGrade.Beginner: green = true; break;
                    case PisteGrade.Intermediate: blue = true; break;
                    default: black = true; break;
                }
            }

            float grades = (green ? 1f : 0f) + (blue ? 1f : 0f) + (black ? 1f : 0f);
            float park = _park != null ? 0.8f : 0f;

            return Mathf.Clamp01((grades + park) / 3.8f);
        }

        float Conditions()
        {
            if (_weather == null) return 0.7f;

            // A storm keeps people away; the powder it leaves behind does not.
            float sky = Mathf.Lerp(0.95f, 0.35f, _weather.storminess);
            return Mathf.Clamp01(sky + _weather.powder * 0.28f);
        }
    }
}
