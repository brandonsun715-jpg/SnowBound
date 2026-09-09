using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Resort;

namespace SnowBound.Weather
{
    /// <summary>
    /// How the mountain is lit.
    ///
    /// Everything here is derived from two numbers: how high the sun is, and
    /// how bad the weather is. That is deliberate — a lighting model built out
    /// of discrete presets snaps between them, and a mountain that snaps from
    /// "clear" to "overcast" never feels like weather. Blending two continuous
    /// inputs gives every hour of every condition its own light for free.
    ///
    /// Sun elevation does the heavy lifting. Low sun means long light through
    /// a lot of air: warm, weak, and strongly reddened, with cold blue shadow
    /// filling in from the sky. High sun means white light and short shadows.
    /// That single relationship is most of what makes a sunset look like a
    /// sunset without painting an orange sheet over the screen.
    ///
    /// Snow is the other half of it. A snowfield is a huge white reflector, so
    /// the bounce light coming back up is far stronger than on any other
    /// terrain, and that is why shadows on a ski hill are bright and blue
    /// rather than black.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [ExecuteAlways]
    public class LightingDirector : MonoBehaviour
    {
        public Light sun;
        public WeatherSystem weather;
        public ResortClock clock;

        [Header("Sun")]
        [Tooltip("Strength of the sun with it high and the sky clear.")]
        public float peakIntensity = 2.1f;
        [Tooltip("What is left of it in a whiteout.")]
        public float stormIntensity = 0.55f;
        public Color highSun = new Color(1.00f, 0.985f, 0.955f);
        public Color lowSun = new Color(1.00f, 0.700f, 0.430f);
        public Color stormSun = new Color(0.80f, 0.845f, 0.920f);

        [Header("Sky and bounce")]
        [Tooltip("Zenith colour on a clear day. Snow light is cold from above.")]
        public Color clearZenith = new Color(0.36f, 0.53f, 0.86f);
        public Color clearHorizon = new Color(0.72f, 0.82f, 0.94f);
        public Color stormZenith = new Color(0.55f, 0.58f, 0.64f);
        public Color stormHorizon = new Color(0.74f, 0.76f, 0.80f);
        [Tooltip("Light coming back up off the snowfield.")]
        public Color snowBounce = new Color(0.78f, 0.83f, 0.92f);
        public float ambientStrength = 1.15f;

        [Header("Air")]
        public float clearFogDensity = 0.00016f;
        public float stormFogDensity = 0.0042f;
        [Tooltip("Extra haze low down, which is where the air actually is.")]
        public float valleyHaze = 1.35f;

        [Header("Shadows")]
        public float clearShadowStrength = 0.88f;
        public float stormShadowStrength = 0.25f;

        // ---- what the sky and the grade read ----

        /// <summary>0 with the sun on the horizon, 1 with it overhead.</summary>
        public float SunHeight { get; private set; }

        /// <summary>1 while the sun is low enough to be going gold.</summary>
        public float GoldenHour { get; private set; }

        public Color Zenith { get; private set; }
        public Color Horizon { get; private set; }
        public Color SunColour { get; private set; }
        public float Storminess { get; private set; }

        static LightingDirector _instance;

        public static LightingDirector Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<LightingDirector>();
                return _instance;
            }
        }

        void OnEnable() { _instance = this; }

        void LateUpdate()
        {
            if (sun == null) sun = FindSun();
            if (weather == null) weather = WeatherSystem.Instance;
            if (clock == null) clock = ResortClock.Instance;

            Aim();
            Measure();
            Apply();
        }

        /// <summary>
        /// Point the sun. This happens here rather than in the weather because
        /// everything downstream reads the sun's actual direction, so whatever
        /// sets it has to set it first.
        /// </summary>
        void Aim()
        {
            if (sun == null || clock == null) return;
            sun.transform.rotation = clock.SunRotation;
        }

        void Measure()
        {
            Storminess = weather != null ? Mathf.Clamp01(weather.storminess) : 0.15f;

            // Sun elevation straight off the light, so this works whatever is
            // driving the sun — the clock, a cutscene, or a hand-set rotation.
            float elevation = 0f;
            if (sun != null) elevation = -sun.transform.forward.y;
            else if (clock != null) elevation = Mathf.Sin(Mathf.Deg2Rad * clock.SunRotation.eulerAngles.x);

            SunHeight = Mathf.Clamp01(elevation);

            // Gold below about twenty degrees, and strongest right at the horizon.
            GoldenHour = 1f - Mathf.SmoothStep(0.05f, 0.36f, SunHeight);
        }

        void Apply()
        {
            float lit = Mathf.Clamp01(0.16f + SunHeight * 1.25f);
            float clear = 1f - Storminess;

            // ---- the sun itself ----

            SunColour = Color.Lerp(highSun, lowSun, GoldenHour * 0.9f);
            SunColour = Color.Lerp(SunColour, stormSun, Storminess * 0.85f);

            if (sun != null)
            {
                sun.color = SunColour;

                // Air mass: a low sun has to come through far more atmosphere,
                // so it loses most of its strength before it arrives.
                float airMass = Mathf.Lerp(0.22f, 1f, Mathf.Pow(SunHeight, 0.55f));

                sun.intensity = Mathf.Lerp(stormIntensity, peakIntensity, clear) * airMass;
                sun.shadowStrength = Mathf.Lerp(stormShadowStrength, clearShadowStrength, clear);

                // A low sun is a big soft source through haze; a high one is hard.
                sun.shadows = LightShadows.Soft;
            }

            // ---- the sky, and the light it throws down ----

            Zenith = Color.Lerp(stormZenith, clearZenith, clear) * lit;
            Horizon = Color.Lerp(stormHorizon, clearHorizon, clear) * lit;

            // The horizon takes the sun's colour near sunrise and sunset.
            Horizon = Color.Lerp(Horizon, Horizon * 0.55f + SunColour * 0.75f, GoldenHour * clear * 0.8f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Zenith * ambientStrength;
            RenderSettings.ambientEquatorColor = Horizon * ambientStrength * 0.9f;

            // The snowfield throwing light back up. This is why shadows on a
            // ski hill are pale blue rather than black.
            RenderSettings.ambientGroundColor =
                snowBounce * ambientStrength * Mathf.Lerp(0.55f, 1.15f, lit) * Mathf.Lerp(1f, 0.8f, Storminess);

            // ---- the air between here and the far peaks ----

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            float density = Mathf.Lerp(clearFogDensity, stormFogDensity, Storminess);

            // Haze pools in the valley, so looking down the hill is softer than
            // looking across the top of it.
            if (Camera.main != null)
            {
                float low = 1f - Mathf.Clamp01(Camera.main.transform.position.y / 320f);
                density *= Mathf.Lerp(1f, valleyHaze, low * 0.7f);
            }

            RenderSettings.fogDensity = density;

            // Distance fades into the sky it is seen against, never into grey.
            RenderSettings.fogColor = Color.Lerp(Horizon, Zenith, 0.25f);

            RenderSettings.reflectionIntensity = Mathf.Lerp(0.6f, 1f, clear);
        }

        static Light FindSun()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;

            return null;
        }
    }
}
