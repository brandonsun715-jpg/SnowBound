using UnityEngine;
using SnowBound.Buildings;

namespace SnowBound.Player
{
    /// <summary>
    /// The player's body. Owns the CharacterController, works out what the
    /// ground under the feet is doing, and hands the frame to whichever
    /// locomotion mode is active.
    ///
    /// It deliberately knows nothing about walking, skiing or snowboarding.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Stronger than real gravity because it makes jumps feel crisp.")]
        public float gravity = -25f;
        [Tooltip("Layers counted as ground. Leave as Everything.")]
        public LayerMask groundMask = ~0;

        [Header("Start")]
        public LocomotionKind startMode = LocomotionKind.Walk;
        [Tooltip("Drop the player on the lodge deck when the game starts.")]
        public bool spawnAtLodge = true;

        // ---- state other systems read ----

        public CharacterController Body { get; private set; }
        public PlayerInputReader Input { get; private set; }

        public Vector3 Velocity;
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundSlopeDegrees { get; private set; }
        public LocomotionKind CurrentKind => _active != null ? _active.Kind : startMode;

        public float Gravity => gravity;
        public float Speed => new Vector3(Velocity.x, 0f, Velocity.z).magnitude;

        /// <summary>Camera forward, flattened. Movement is relative to this.</summary>
        public Vector3 CameraForward
        {
            get
            {
                Vector3 f = _camera != null ? _camera.forward : Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
            }
        }

        public Vector3 CameraRight
        {
            get
            {
                Vector3 r = CameraForward;
                return new Vector3(r.z, 0f, -r.x);
            }
        }

        Transform _camera;
        PlayerVisual _visual;
        LocomotionMode _active;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        void Awake()
        {
            Body = GetComponent<CharacterController>();
            Input = GetComponent<PlayerInputReader>();

            foreach (var mode in GetComponents<LocomotionMode>()) mode.Bind(this);

            _visual = GetComponentInChildren<PlayerVisual>(true);

            if (Camera.main != null) _camera = Camera.main.transform;
        }

        void Start()
        {
            if (spawnAtLodge) SpawnAtLodge();
            SetMode(startMode);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            ProbeGround();

            int gear = Input.GearPressed;
            if (gear > 0) SetMode((LocomotionKind)gear);

            if (_active != null) _active.Tick(dt);

            Body.Move(Velocity * dt);
        }

        // ---------------- ground ----------------------------------------

        void ProbeGround()
        {
            float radius = Body.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.25f);

            int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits,
                                               0.5f, groundMask, QueryTriggerInteraction.Ignore);

            IsGrounded = false;
            GroundNormal = Vector3.up;
            float nearest = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;

                // The cast starts inside our own capsule; ignore ourselves.
                if (h.collider.transform == transform) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;

                // A zero normal means the cast began already overlapping.
                if (h.normal.sqrMagnitude < 0.0001f) continue;

                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    IsGrounded = true;
                    GroundNormal = h.normal;
                }
            }

            GroundSlopeDegrees = Vector3.Angle(GroundNormal, Vector3.up);
        }

        /// <summary>Direction of steepest descent along the ground, flattened length 1.</summary>
        public Vector3 DownhillDirection()
        {
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
            down.y = 0f;
            return down.sqrMagnitude < 0.0001f ? Vector3.zero : down.normalized;
        }

        // ---------------- modes -----------------------------------------

        public void SetMode(LocomotionKind kind)
        {
            if (_active != null && _active.Kind == kind) return;

            LocomotionMode next = null;
            foreach (var mode in GetComponents<LocomotionMode>())
            {
                if (mode.Kind == kind) { next = mode; break; }
            }

            if (next == null)
            {
                Debug.LogWarning("[PlayerController] No locomotion component for " + kind, this);
                return;
            }

            if (_active != null) _active.OnExit();
            _active = next;
            _active.OnEnter();

            if (_visual != null) _visual.ShowGear(kind);
        }

        // ---------------- placement -------------------------------------

        public void Teleport(Vector3 position)
        {
            Body.enabled = false;
            transform.position = position;
            Body.enabled = true;
            Velocity = Vector3.zero;
        }

        public void SpawnAtLodge()
        {
            var lodge = LodgeBuilder.Instance;
            if (lodge == null) return;
            Teleport(lodge.EntrancePosition + Vector3.up * 0.3f);
        }
    }
}
