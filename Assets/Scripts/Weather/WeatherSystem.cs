using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Player;
using SnowBound.Resort;

namespace SnowBound.Weather
{
    public enum WeatherKind
    {
        Clear,
        Overcast,
        LightSnow,
        Storm
    }

    /// <summary>
    /// The one authority on what the sky is doing.
    ///
    /// Everything else asks this component rather than deciding for itself,
    /// so the light, the fog, the falling snow and how the mountain rides all
    /// move together. A single number, storminess, drives the lot.
    ///
    /// Snow on the ground is tracked separately as powder: it builds while it
    /// snows and packs back down afterwards, so the run is slow and forgiving
    /// after a dump and quick and skittish once it has been skied out.
    /// </summary>
    [ExecuteAlways]
    public class WeatherSystem : MonoBehaviour
    {
        static WeatherSystem _instance;

        public static WeatherSystem Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<WeatherSystem>();
                return _instance;
            }
        }

        [Header("Cycle")]
        [Tooltip("Let the weather drift on its own. Turn off to hold one setting.")]
        public bool automatic = true;
        [Tooltip("Real minutes for one pass from clear to storm and back.")]
        public float cycleMinutes = 6f;
        [Range(0f, 1f)]
        [Tooltip("0 is a bluebird day, 1 is a whiteout.")]
        public float storminess = 0.15f;

        [Header("Visibility")]
        public Color clearFog = new Color(0.72f, 0.80f, 0.88f);
        public Color stormFog = new Color(0.80f, 0.82f, 0.86f);
        public float clearFogDensity = 0.0018f;
        public float stormFogDensity = 0.011f;

        [Header("Light")]
        public Light sun;
        public float clearSunIntensity = 1.25f;
        public float stormSunIntensity = 0.40f;
        public Color clearSunColour = new Color(1f, 0.957f, 0.839f);
        public Color stormSunColour = new Color(0.82f, 0.85f, 0.92f);

        [Header("Wind")]
        public float windDirectionDegrees = 210f;
        public float clearWindSpeed = 1.5f;
        public float stormWindSpeed = 14f;

        [Header("Snow on the ground")]
        [Tooltip("How fast fresh powder builds while it is snowing.")]
        public float powderGain = 0.045f;
        [Tooltip("How fast powder packs down again once it stops.")]
        public float powderLoss = 0.014f;
        [Range(0f, 1f)] public float powder = 0.2f;

        [Header("How powder rides")]
        [Tooltip("Drag multiplier in deep powder. Above 1 means slower.")]
        public float powderDrag = 1.45f;
        [Tooltip("Edge grip multiplier in deep powder. Above 1 means it holds better.")]
        public float powderGrip = 1.25f;
        [Tooltip("Spray multiplier on hardpack, then in deep powder.")]
        public float hardpackSpray = 0.85f;
        public float powderSpray = 1.9f;

        // ---------------------------------------------------------------

        /// <summary>0 until the cloud thickens enough to actually snow.</summary>
        public float Snowfall { get { return Mathf.Clamp01((storminess - 0.32f) / 0.5f); } }

        public Vector3 Wind
        {
            get
            {
                float speed = Mathf.Lerp(clearWindSpeed, stormWindSpeed, storminess);
                float radians = windDirectionDegrees * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * speed;
            }
        }

        public float DragMultiplier { get { return Mathf.Lerp(1f, powderDrag, powder); } }
        public float GripMultiplier { get { return Mathf.Lerp(1f, powderGrip, powder); } }
        public float SprayMultiplier { get { return Mathf.Lerp(hardpackSpray, powderSpray, powder); } }

        public WeatherKind Kind
        {
            get
            {
                if (storminess > 0.68f) return WeatherKind.Storm;
                if (storminess > 0.40f) return WeatherKind.LightSnow;
                if (storminess > 0.22f) return WeatherKind.Overcast;
                return WeatherKind.Clear;
            }
        }

        public string Description
        {
            get
            {
                switch (Kind)
                {
                    case WeatherKind.Storm: return "Storm";
                    case WeatherKind.LightSnow: return "Snowing";
                    case WeatherKind.Overcast: return "Overcast";
                    default: return "Clear";
                }
            }
        }

        public string SnowDescription
        {
            get
            {
                if (powder > 0.65f) return "Deep powder";
                if (powder > 0.35f) return "Fresh snow";
                if (powder > 0.15f) return "Packed";
                return "Hardpack";
            }
        }

        PlayerInputReader _input;

        void OnEnable() { _instance = this; }

        void Update()
        {
            // Writing to RenderSettings in the editor would dirty the scene on
            // every frame, so the weather only actually runs during play.
            if (!Application.isPlaying) return;

            if (sun == null) sun = FindSun();

            if (_input == null) _input = FindAnyObjectByType<PlayerInputReader>();
            if (_input != null && _input.WeatherPressed) CycleManually();

            if (automatic) Drift();

            Settle(Time.deltaTime);
            Apply();
        }

        void Drift()
        {
            float minutes = Mathf.Max(0.25f, cycleMinutes);
            float t = Time.time / (minutes * 60f);

            // Perlin noise hugs the middle, so stretch it back out. Without
            // this the sky would sit permanently overcast and never commit to
            // either a clear day or a real storm.
            float raw = Mathf.PerlinNoise(t, 0.37f);
            storminess = Mathf.Clamp01((raw - 0.34f) / 0.36f);
        }

        void Settle(float dt)
        {
            powder += (Snowfall * powderGain - powderLoss) * dt;
            powder = Mathf.Clamp01(powder);
        }

        void Apply()
        {
            // Time of day decides where the sun is and how much light there
            // is; the weather only decides how much of it gets through.
            ResortClock clock = ResortClock.Instance;
            float daylight = clock != null ? clock.Daylight : 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(clearFogDensity, stormFogDensity, storminess);
            RenderSettings.fogColor = Color.Lerp(clearFog, stormFog, storminess)
                                    * Mathf.Lerp(0.72f, 1f, daylight);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            float ambient = Mathf.Lerp(0.55f, 1f, daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.60f, 0.70f, 0.85f),
                                                        new Color(0.66f, 0.68f, 0.72f), storminess) * ambient;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.55f, 0.60f, 0.68f),
                                                            new Color(0.62f, 0.64f, 0.68f), storminess) * ambient;
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.70f, 0.72f, 0.75f),
                                                           new Color(0.66f, 0.67f, 0.70f), storminess) * ambient;

            if (sun != null)
            {
                if (clock != null) sun.transform.rotation = clock.SunRotation;

                sun.intensity = Mathf.Lerp(clearSunIntensity, stormSunIntensity, storminess) * daylight;
                sun.color = Color.Lerp(clearSunColour, stormSunColour, storminess);
                sun.shadowStrength = Mathf.Lerp(1f, 0.35f, storminess);
            }
        }

        static Light FindSun()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }
            return null;
        }

        /// <summary>Step through the presets. Used by the debug key.</summary>
        public void CycleManually()
        {
            automatic = false;
            if (storminess < 0.22f) storminess = 0.35f;
            else if (storminess < 0.5f) storminess = 0.55f;
            else if (storminess < 0.8f) storminess = 0.9f;
            else storminess = 0.05f;
        }
    }
}
