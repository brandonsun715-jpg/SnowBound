using UnityEngine;
using SnowBound.Core;
using SnowBound.Player;
using SnowBound.Weather;

namespace SnowBound.Audio
{
    /// <summary>
    /// Everything the player hears about themselves: wind past the ears, the
    /// rush of snow under an edge, boots in snow, and the muffled thump of a
    /// landing.
    ///
    /// Two rules keep it from sounding synthetic. Everything is dark: snow
    /// and wind have almost no high frequency energy, and it is high
    /// frequency energy that reads as harsh. And everything is quiet: the
    /// layers have to sum below one or the mix clips, and clipping is the
    /// harshest sound there is.
    ///
    /// Like the spray, none of it reads the keyboard. It reads speed, slip,
    /// what is underfoot and what the sky is doing.
    /// </summary>
    public class RideAudio : MonoBehaviour
    {
        public PlayerController player;
        public WeatherSystem weather;

        [Header("Master")]
        [Range(0f, 1f)] public float volume = 0.75f;

        [Header("Wind")]
        [Tooltip("Barely there when you are standing still.")]
        public float windAtRest = 0.02f;
        public float windFlatOut = 0.22f;
        [Tooltip("Speed, m/s, at which wind noise is at its loudest.")]
        public float windFullSpeed = 26f;
        public float windFromStorm = 0.16f;
        [Tooltip("Wind you cannot hear the top of. Both stay well under a kilohertz.")]
        public float windToneCalm = 190f;
        public float windToneHowling = 900f;

        [Header("Snow under the edges")]
        public float carveVolume = 0.20f;
        [Tooltip("Extra volume per metre/second of sideways slide.")]
        public float slipVolume = 0.045f;
        public float carveMinSpeed = 2.5f;
        public float carveFullSpeed = 22f;
        [Tooltip("Hardpack is brighter; deep powder swallows the top end.")]
        public float carveToneHardpack = 3200f;
        public float carveTonePowder = 900f;

        [Header("Footsteps")]
        [Tooltip("Metres between footfalls at walking pace.")]
        public float stride = 1.75f;
        public float stepVolume = 0.16f;

        [Header("Air")]
        public float takeOffVolume = 0.10f;
        public float landingVolume = 0.34f;

        AudioSource _wind;
        AudioSource _carve;
        AudioSource[] _shots;
        AudioLowPassFilter _windTone;
        AudioLowPassFilter _carveTone;
        AudioHighPassFilter _carveEdge;

        AudioClip[] _steps;
        AudioClip _landing;
        AudioClip _takeOff;

        int _nextShot;
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
            // Both beds are generated dark. The live filters then work inside
            // that ceiling, so no setting of theirs can make it shrill.
            AudioClip windClip = ProceduralAudio.PinkNoiseLoop("WindNoise", 4f, 8801, 700f, 3, 0f);
            AudioClip snowClip = ProceduralAudio.PinkNoiseLoop("SnowNoise", 3f, 5507, 3600f, 2, 160f);

            _wind = Loop("Wind", windClip);
            _windTone = _wind.gameObject.AddComponent<AudioLowPassFilter>();
            _windTone.cutoffFrequency = windToneCalm;

            _carve = Loop("Carve", snowClip);
            _carveTone = _carve.gameObject.AddComponent<AudioLowPassFilter>();
            _carveTone.cutoffFrequency = carveToneHardpack;
            _carveEdge = _carve.gameObject.AddComponent<AudioHighPassFilter>();
            _carveEdge.cutoffFrequency = 220f;

            // Three sources, used in turn. One shared source would drag every
            // still-ringing sound to the pitch of the newest one.
            _shots = new AudioSource[3];
            for (int i = 0; i < _shots.Length; i++)
            {
                var go = new GameObject("Impact " + i);
                go.transform.SetParent(transform, false);
                go.hideFlags = HideFlags.DontSaveInEditor;

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _shots[i] = source;
            }

            // Snow underfoot is a soft squeak, not gravel. Nothing above 1.6 kHz.
            _steps = new[]
            {
                ProceduralAudio.Burst("Step1", 0.20f, 11, 1500f, 2, 210f, 0.010f, 3.0f, 0.6f),
                ProceduralAudio.Burst("Step2", 0.23f, 22, 1250f, 2, 190f, 0.013f, 2.6f, 0.6f),
                ProceduralAudio.Burst("Step3", 0.18f, 33, 1750f, 2, 240f, 0.008f, 3.4f, 0.6f)
            };

            // A landing in snow is a whumpf: all body, no crack. The slow
            // attack is what stops it reading as a click.
            _landing = ProceduralAudio.Burst("Landing", 0.60f, 44, 300f, 3, 35f, 0.022f, 2.0f, 0.75f);

            // Unweighting off a lip: quieter still, and darker again.
            _takeOff = ProceduralAudio.Burst("TakeOff", 0.26f, 55, 620f, 3, 90f, 0.018f, 2.4f, 0.5f);
        }

        AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;      // it is happening to you, not near you
            source.volume = 0f;
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
            float target = Mathf.Lerp(windAtRest, windFlatOut, rush * rush) + storm * windFromStorm;

            // Gusting on two out of phase waves, so the wind breathes rather
            // than sitting on one level.
            float gust = 1f + Mathf.Sin(Time.time * 0.7f) * 0.10f * storm
                            + Mathf.Sin(Time.time * 1.9f) * 0.06f * storm;

            _wind.volume = Approach(_wind.volume, target * gust * volume, 2.5f, dt);
            _windTone.cutoffFrequency = Approach(_windTone.cutoffFrequency,
                Mathf.Lerp(windToneCalm, windToneHowling, Mathf.Max(rush, storm * 0.7f)), 3f, dt);
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

            _carve.volume = Approach(_carve.volume, Mathf.Min(target, volume * 0.5f), 6f, dt);

            // Only a hint of pitch. Sweeping noise about sounds like a sweep,
            // not like going faster.
            _carve.pitch = Mathf.Lerp(0.94f, 1.08f, Mathf.Clamp01(speed / carveFullSpeed));

            _carveTone.cutoffFrequency = Approach(_carveTone.cutoffFrequency,
                Mathf.Lerp(carveToneHardpack, carveTonePowder, powder), 2.5f, dt);

            // A skid is broader and rougher than a clean carve.
            _carveEdge.cutoffFrequency = Approach(_carveEdge.cutoffFrequency,
                Mathf.Lerp(300f, 150f, Mathf.Clamp01(Mathf.Abs(player.LateralSlip) / 4f)), 4f, dt);
        }

        void DriveFootsteps(float speed, float dt)
        {
            bool walking = !player.IsRidingSnow && player.IsGrounded &&
                           !player.IsRiding && player.OnSnow && speed > 0.6f;

            if (!walking) { _stepDistance = stride * 0.6f; return; }

            _stepDistance += speed * dt;
            if (_stepDistance < stride) return;
            _stepDistance = 0f;

            Play(_steps[Random.Range(0, _steps.Length)], stepVolume, Random.Range(0.92f, 1.08f));
        }

        void DriveImpacts()
        {
            if (player.IsRiding) return;

            // Left the ground going up: a jump, not a drop off a lip.
            if (_wasGrounded && !player.IsGrounded && player.Velocity.y > 2f)
                Play(_takeOff, takeOffVolume, Random.Range(0.96f, 1.04f));

            if (!_wasGrounded && player.IsGrounded && _lastFallSpeed > 3f)
            {
                float weight = Mathf.Clamp01((_lastFallSpeed - 3f) / 11f);
                Play(_landing, Mathf.Lerp(0.25f, 1f, weight) * landingVolume,
                     Mathf.Lerp(1.12f, 0.86f, weight));
            }
        }

        void Play(AudioClip clip, float level, float pitch)
        {
            if (clip == null || _shots == null) return;

            AudioSource source = _shots[_nextShot];
            _nextShot = (_nextShot + 1) % _shots.Length;

            source.pitch = pitch;
            source.PlayOneShot(clip, level * volume);
        }

        /// <summary>Frame-rate independent ease, so nothing ever clicks.</summary>
        static float Approach(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }
    }
}
