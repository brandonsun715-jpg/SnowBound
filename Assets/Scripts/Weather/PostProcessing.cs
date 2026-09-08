using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SnowBound.Weather
{
    /// <summary>
    /// The grade.
    ///
    /// A snow scene is the hardest thing to grade: it is almost entirely made
    /// of the brightest value there is, so anything heavy-handed either clips
    /// it to flat white or drags it down to grey. So this is deliberately
    /// restrained — a neutral film response, exposure that follows the light,
    /// and a white balance that goes warm at the ends of the day and cold in a
    /// storm. Bloom is here but kept below the threshold where snow starts to
    /// glow, because glowing snow is the single clearest sign of a scene that
    /// has been over-processed.
    ///
    /// The profile is built in code, so there is no asset to keep in sync and
    /// no way for the scene and the grade to disagree.
    /// </summary>
    [ExecuteAlways]
    public class PostProcessing : MonoBehaviour
    {
        public LightingDirector lighting;
        public WeatherSystem weather;

        [Header("Exposure")]
        [Tooltip("Stops of exposure with the sun high and the sky clear.")]
        public float clearExposure = -0.10f;
        [Tooltip("Stops in a whiteout. Up, because there is less light about.")]
        public float stormExposure = 0.35f;
        [Tooltip("Extra stops at sunrise and sunset.")]
        public float goldenExposure = 0.55f;

        [Header("Colour")]
        public float clearContrast = 9f;
        public float stormContrast = -6f;
        public float clearSaturation = 6f;
        public float stormSaturation = -22f;
        [Tooltip("White balance in a storm. Negative is colder.")]
        public float stormTemperature = -14f;
        [Tooltip("White balance at the ends of the day.")]
        public float goldenTemperature = 22f;

        [Header("Bloom")]
        [Tooltip("Kept high. Below this and snow starts to glow, which is wrong.")]
        public float bloomThreshold = 1.25f;
        public float clearBloom = 0.22f;
        public float goldenBloom = 0.55f;

        [Header("Frame")]
        public float vignette = 0.20f;

        Volume _volume;
        VolumeProfile _profile;

        Tonemapping _tone;
        ColorAdjustments _colour;
        WhiteBalance _balance;
        Bloom _bloom;
        Vignette _vignette;

        void OnEnable() { Build(); }

        void OnDisable()
        {
            if (_profile == null) return;

            if (Application.isPlaying) Destroy(_profile);
            else DestroyImmediate(_profile);

            _profile = null;
        }

        void Build()
        {
            if (lighting == null) lighting = LightingDirector.Instance;
            if (weather == null) weather = WeatherSystem.Instance;

            if (_volume != null && _profile != null) return;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SnowboundGrade";
            _profile.hideFlags = HideFlags.DontSave;

            // Neutral rather than ACES. ACES is the right answer for most
            // scenes and the wrong one for a mountain of white: it pulls the
            // highlights down until the snow reads as grey card.
            _tone = _profile.Add<Tonemapping>(true);
            _tone.mode.overrideState = true;
            _tone.mode.value = TonemappingMode.Neutral;

            _colour = _profile.Add<ColorAdjustments>(true);
            _colour.postExposure.overrideState = true;
            _colour.contrast.overrideState = true;
            _colour.saturation.overrideState = true;

            _balance = _profile.Add<WhiteBalance>(true);
            _balance.temperature.overrideState = true;
            _balance.tint.overrideState = true;

            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.62f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;

            _vignette = _profile.Add<Vignette>(true);
            _vignette.intensity.overrideState = true;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.5f;
            _vignette.intensity.value = vignette;

            _volume = gameObject.GetComponent<Volume>();
            if (_volume == null) _volume = gameObject.AddComponent<Volume>();

            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = 1f;
            _volume.profile = _profile;
        }

        void LateUpdate()
        {
            if (_profile == null || _colour == null) Build();
            if (_colour == null) return;

            if (lighting == null) lighting = LightingDirector.Instance;

            float storm = weather != null ? Mathf.Clamp01(weather.storminess) : 0.15f;
            float golden = lighting != null ? lighting.GoldenHour : 0f;
            float height = lighting != null ? lighting.SunHeight : 0.6f;

            // Exposure follows the light rather than fighting it: open up as
            // the sun drops and as cloud takes the sun away.
            float exposure = Mathf.Lerp(clearExposure, stormExposure, storm)
                           + golden * goldenExposure
                           + Mathf.Lerp(0.35f, 0f, Mathf.Clamp01(height * 2f));

            _colour.postExposure.value = exposure;
            _colour.contrast.value = Mathf.Lerp(clearContrast, stormContrast, storm);

            // Flat light is genuinely less saturated, and pretending otherwise
            // is what makes a storm look like a filter rather than weather.
            _colour.saturation.value = Mathf.Lerp(clearSaturation, stormSaturation, storm);

            _balance.temperature.value = Mathf.Lerp(0f, stormTemperature, storm)
                                       + golden * goldenTemperature * (1f - storm);
            _balance.tint.value = Mathf.Lerp(0f, -4f, storm);

            _bloom.threshold.value = bloomThreshold;
            _bloom.intensity.value = Mathf.Lerp(clearBloom, goldenBloom, golden) * Mathf.Lerp(1f, 0.4f, storm);

            _vignette.intensity.value = vignette * Mathf.Lerp(1f, 1.4f, storm);
        }
    }
}
