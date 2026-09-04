using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Weather
{
    /// <summary>
    /// Falling snow. A box of particles that rides above the player, so a
    /// few thousand flakes are enough to fill a whole mountain: you only ever
    /// see the ones near you.
    ///
    /// Rate and drift come from the WeatherSystem, never from here.
    /// </summary>
    public class Snowfall : MonoBehaviour
    {
        public WeatherSystem weather;
        [Tooltip("Usually the player. The snow box follows this.")]
        public Transform follow;

        [Header("Volume")]
        [Tooltip("Width of the box of snow around the player, in metres.")]
        public float boxSize = 44f;
        [Tooltip("How far above the player the snow starts.")]
        public float height = 15f;
        public float fallSpeed = 4f;

        [Header("Density")]
        public float maxRate = 620f;
        public int maxParticles = 2600;

        ParticleSystem _system;

        void Start()
        {
            if (weather == null) weather = WeatherSystem.Instance;
            Build();
        }

        void Build()
        {
            var go = new GameObject("FallingSnow");
            go.transform.SetParent(transform, false);

            _system = go.AddComponent<ParticleSystem>();

            var main = _system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(height / Mathf.Max(0.5f, fallSpeed));
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.19f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f));
            main.gravityModifier = 0f;

            var emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boxSize, 1f, boxSize);

            var velocity = _system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed);

            // Flakes wander rather than falling on rails.
            var noise = _system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.7f);
            noise.frequency = 0.28f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);

            var renderer = _system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MaterialFactory.CreateParticle(
                "FallingSnowMaterial", Color.white, PrimitiveTextures.SoftCircle());
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.hideFlags = HideFlags.DontSaveInEditor;
            _system.Play();
        }

        void LateUpdate()
        {
            if (_system == null) return;
            if (weather == null) weather = WeatherSystem.Instance;

            if (follow != null)
                _system.transform.position = follow.position + Vector3.up * height;

            float amount = weather != null ? weather.Snowfall : 0f;

            var emission = _system.emission;
            emission.rateOverTime = amount * maxRate;

            if (amount <= 0.001f) return;

            // Wind blows the snow sideways as it falls.
            Vector3 wind = weather != null ? weather.Wind : Vector3.zero;
            var velocity = _system.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.6f);
        }
    }
}
