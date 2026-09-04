using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Player
{
    /// <summary>
    /// The plume of snow thrown up by the edges. Driven by two things the
    /// rider already reports: how fast it is going, and how hard it is
    /// sliding sideways. So a straight glide gives a thin trail and a hard
    /// carve or a hockey stop throws a wall of it.
    /// </summary>
    public class SnowSpray : MonoBehaviour
    {
        [Tooltip("Leave empty to use the PlayerController on this object.")]
        public PlayerController player;

        [Header("When it sprays")]
        [Tooltip("Below this speed, nothing is thrown up.")]
        public float minSpeed = 3f;
        [Tooltip("Speed at which the plain gliding spray is at full strength.")]
        public float fastSpeed = 20f;

        [Header("How much")]
        [Tooltip("Particles per second from gliding alone.")]
        public float glideRate = 70f;
        [Tooltip("Extra particles per second per metre/second of sideways slide.")]
        public float slipRate = 90f;
        public float maxRate = 400f;

        ParticleSystem _system;
        Transform _nozzle;

        void Start()
        {
            if (player == null) player = GetComponent<PlayerController>();
            Build();
        }

        void Build()
        {
            var nozzle = new GameObject("SnowSpray");
            nozzle.transform.SetParent(transform, false);
            nozzle.transform.localPosition = new Vector3(0f, 0.12f, -0.2f);
            _nozzle = nozzle.transform;

            _system = nozzle.AddComponent<ParticleSystem>();

            var main = _system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 700;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.8f));
            main.gravityModifier = 0.32f;

            var emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 26f;
            shape.radius = 0.2f;

            // Fade in fast, hang, then melt away.
            var colour = _system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = _system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

            var renderer = _system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MaterialFactory.CreateParticle(
                "SnowSprayMaterial", Color.white, PrimitiveTextures.SoftCircle());
            renderer.sortingFudge = -8f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            nozzle.hideFlags = HideFlags.DontSaveInEditor;
            _system.Play();
        }

        void Update()
        {
            if (player == null || _system == null) return;

            var emission = _system.emission;

            bool riding = player.IsRidingSnow && player.IsGrounded;
            if (!riding)
            {
                emission.rateOverTime = 0f;
                return;
            }

            float speed = player.Speed;
            float slip = Mathf.Abs(player.LateralSlip);

            float glide = Mathf.Clamp01((speed - minSpeed) / Mathf.Max(0.1f, fastSpeed - minSpeed));
            float rate = glide * glideRate + slip * slipRate;
            emission.rateOverTime = Mathf.Clamp(rate, 0f, maxRate);

            // Faster and slidier throws the snow further.
            var main = _system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f + slip * 0.6f,
                                                             3f + speed * 0.12f + slip * 1.6f);

            // Point the plume out to the side the edges are washing towards.
            float sideways = Mathf.Clamp(player.LateralSlip * 0.12f, -0.6f, 0.6f);
            Vector3 direction = new Vector3(sideways, 0.85f, -0.45f).normalized;
            _nozzle.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
