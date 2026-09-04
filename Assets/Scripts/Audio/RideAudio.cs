using UnityEngine;
using SnowBound.Core;
using SnowBound.Player;
using SnowBound.Weather;

namespace SnowBound.Audio
{
    /// <summary>
    /// Everything the player hears about themselves: wind past the ears, the
    /// hiss of snow under an edge, boots in snow, and the thump of a landing.
    ///
    /// Like the spray, none of it reads the keyboard. It reads speed, slip,
    /// what is underfoot and what the sky is doing, so the sound always
    /// describes what is actually happening rather than what was asked for.
    /// </summary>
    public class RideAudio : MonoBehaviour
    {
        public PlayerController player;
        public WeatherSystem weather;

        [Header("Master")]
        [Range(0f, 1f)] public float volume = 0.9f;

        [Header("Wind")]
        public float windAtRest = 0.05f;
        public float windFlatOut = 0.42f;
        [Tooltip("Speed, m/s, at which wind noise is at its loudest.")]
        public float windFullSpeed = 26f;
        [Tooltip("Extra wind volume in a full storm.")]
        public float windFromStorm = 0.30f;
        public float windCutoffCalm = 340f;
        public float windCutoffHowling = 2600f;

        [Header("Snow under the edges")]
        public float carveVolume = 0.55f;
        [Tooltip("Extra volume per metre/second of sideways slide.")]
        public float slipVolume = 0.10f;
        public float carveMinSpeed = 2f;
        public float carveFullSpeed = 22f;
        [Tooltip("Hardpack is bright and sharp; deep powder is muffled.")]
        public float carveToneHardpack = 8000f;
        public float carveTonePowder = 2400f;

        [Header("Footsteps")]
        [Tooltip("Metres between footfalls at walking pace.")]
        public float stride = 1.7f;

        AudioSource _wind;
        AudioSource _carve;
        AudioSource _oneShots;
        AudioLowPassFilter _windTone;
        AudioLowPassFilter _carveTone;
        AudioHighPassFilter _carveEdge;

        AudioClip[] _steps;
        AudioClip _landing;
        AudioClip _takeOff;

        float _stepDistance;
        bool _wasGrounded = true;
        float _lastFallSpeed;

        void Start()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (weather == null) weather = WeatherSystem.Instance;
            Build();
        }

        void Build()
        {
            AudioClip windClip = ProceduralAudio.PinkNoiseLoop("WindNoise", 4f, 8801);
            AudioClip snowClip = ProceduralAudio.PinkNoiseLoop("SnowNoise", 3f, 5507);

            _wind = Loop("Wind", windClip, 0f);
            _windTone = _wind.gameObject.AddComponent<AudioLowPassFilter>();
            _windTone.cutoffFrequency = windCutoffCalm;

            _carve = Loop("Carve", snowClip, 0f);
            _carveTone = _carve.gameObject.AddComponent<AudioLowPassFilter>();
            _carveTone.cutoffFrequency = carveToneHardpack;
            _carveEdge = _carve.gameObject.AddComponent<AudioHighPassFilter>();
            _carveEdge.cutoffFrequency = 900f;

            var shots = new GameObject("OneShots");
            shots.transform.SetParent(transform, false);
            shots.hideFlags = HideFlags.DontSaveInEditor;
            _oneShots = shots.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;
            _oneShots.spatialBlend = 0f;

            // Three crunches so repeated footfalls do not sound mechanical.
            _steps = new[]
            {
                ProceduralAudio.Burst("Step1", 0.13f, 700f, 5200f, 3.2f, 11),
                ProceduralAudio.Burst("Step2", 0.15f, 620f, 4600f, 2.8f, 22),
                ProceduralAudio.Burst("Step3", 0.12f, 800f, 6000f, 3.6f, 33)
            };

            _landing = ProceduralAudio.Burst("Landing", 0.42f, 60f, 1500f, 2.4f, 44);
            _takeOff = ProceduralAudio.Burst("TakeOff", 0.22f, 300f, 3400f, 2.0f, 55);
        }

        AudioSource Loop(string name, AudioClip clip, float startVolume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;      // it is happening to you, not near you
            source.volume = startVolume;
            source.Play();

            return source;
        }

        void Update()
        {
            if (player == null || _wind == null) return;
            if (weather == null) weather = WeatherSystem.Instance;

            float dt = Time.deltaTime;
            float speed = player.Speed;
            float storm = weather != null ? weather.storminess : 0f;
            float powder = weather != null ? weather.powder : 0f;

            DriveWind(speed, storm, dt);
            DriveCarve(speed, powder, dt);
            DriveFootsteps(speed, dt);
            DriveImpacts();

            _wasGrounded = player.IsGrounded;
            _lastFallSpeed = -player.Velocity.y;
        }

        void DriveWind(float speed, float storm, float dt)
        {
            float rush = Mathf.Clamp01(speed / Mathf.Max(1f, windFullSpeed));

            float target = Mathf.Lerp(windAtRest, windFlatOut, rush) + storm * windFromStorm;

            // Gusting, stronger in worse weather, so the wind breathes.
            float gust = 1f + Mathf.Sin(Time.time * 0.7f) * 0.12f * storm
                            + Mathf.Sin(Time.time * 1.9f) * 0.07f * storm;

            _wind.volume = Approach(_wind.volume, target * gust * volume, 3f, dt);
            _windTone.cutoffFrequency = Approach(_windTone.cutoffFrequency,
                Mathf.Lerp(windCutoffCalm, windCutoffHowling, Mathf.Max(rush, storm * 0.8f)), 4f, dt);
        }

        void DriveCarve(float speed, float powder, float dt)
        {
            bool cutting = player.IsRidingSnow && player.OnSnow && speed > carveMinSpeed;

            float target = 0f;
            if (cutting)
            {
                float bite = Mathf.Clamp01((speed - carveMinSpeed) /
                                            Mathf.Max(1f, carveFullSpeed - carveMinSpeed));
                target = (carveVolume * bite + Mathf.Abs(player.LateralSlip) * slipVolume) * volume;
            }

            _carve.volume = Approach(_carve.volume, Mathf.Min(target, volume), 8f, dt);
            _carve.pitch = Mathf.Lerp(0.82f, 1.28f, Mathf.Clamp01(speed / carveFullSpeed));

            // Deep snow swallows the high frequencies; hardpack rings.
            _carveTone.cutoffFrequency = Approach(_carveTone.cutoffFrequency,
                Mathf.Lerp(carveToneHardpack, carveTonePowder, powder), 3f, dt);

            // A skid is broader and rougher than a clean carve.
            _carveEdge.cutoffFrequency = Approach(_carveEdge.cutoffFrequency,
                Mathf.Lerp(1100f, 500f, Mathf.Clamp01(Mathf.Abs(player.LateralSlip) / 4f)), 5f, dt);
        }

        void DriveFootsteps(float speed, float dt)
        {
            bool walking = !player.IsRidingSnow && player.IsGrounded && !player.IsRiding && speed > 0.6f;
            if (!walking) { _stepDistance = stride * 0.6f; return; }

            _stepDistance += speed * dt;
            if (_stepDistance < stride) return;

            _stepDistance = 0f;

            AudioClip step = _steps[Random.Range(0, _steps.Length)];
            _oneShots.pitch = Random.Range(0.9f, 1.12f);
            _oneShots.PlayOneShot(step, 0.35f * volume);
        }

        void DriveImpacts()
        {
            if (player.IsRiding) return;

            // Left the ground going up: a jump, not a drop off a lip.
            if (_wasGrounded && !player.IsGrounded && player.Velocity.y > 2f)
            {
                _oneShots.pitch = Random.Range(0.95f, 1.05f);
                _oneShots.PlayOneShot(_takeOff, 0.30f * volume);
            }

            if (!_wasGrounded && player.IsGrounded && _lastFallSpeed > 2.5f)
            {
                float weight = Mathf.Clamp01(_lastFallSpeed / 14f);
                _oneShots.pitch = Mathf.Lerp(1.15f, 0.85f, weight);
                _oneShots.PlayOneShot(_landing, (0.2f + 0.6f * weight) * volume);
            }
        }

        /// <summary>Frame-rate independent ease, so nothing ever clicks.</summary>
        static float Approach(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }
    }
}
