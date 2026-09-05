using UnityEngine;
using SnowBound.Core;
using SnowBound.Buildings;
using SnowBound.Lifts;

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
    public class PlayerController : MonoBehaviour, ILiftPassenger
    {
        [Header("Physics")]
        [Tooltip("Stronger than real gravity because it makes jumps feel crisp.")]
        public float gravity = -25f;
        [Tooltip("Steepest ground the body will walk onto. Skiing needs this high:\nat the default 45 degrees a black run is a wall, and the controller\nresolves being pressed into a wall by squeezing through it.")]
        public float slopeLimit = 82f;
        [Tooltip("Longest single move before it is split up. Keeps a fast rider\nfrom stepping straight past a face between one frame and the next.")]
        public float maxStepDistance = 0.35f;
        [Tooltip("Layers counted as ground. Leave as Everything.")]
        public LayerMask groundMask = ~0;
        [Tooltip("The terrain mesh is faceted, so raw hit normals step from one\ntriangle to the next. Smoothing them keeps the ride steady.")]
        public float groundNormalSmoothing = 14f;

        [Header("Gear")]
        [Tooltip("Off by default so gear can only be changed at the lodge, which\nis what makes the loop a loop. Tick it to test freely.")]
        public bool allowGearKeysAnywhere = false;

        [Header("Start")]
        public LocomotionKind startMode = LocomotionKind.Walk;
        [Tooltip("Drop the player on the lodge deck when the game starts.")]
        public bool spawnAtLodge = true;

        // ---- state other systems read ----

        public CharacterController Body { get; private set; }
        public PlayerInputReader Input { get; private set; }

        public Vector3 Velocity;

        /// <summary>
        /// Downward push that keeps the rider glued to the snow. Kept out of
        /// Velocity on purpose: if it lived there, it would be carried into
        /// the air and kill every jump off a roller.
        /// </summary>
        public Vector3 GroundStick;

        /// <summary>
        /// How hard the edges are sliding sideways, metres per second.
        /// Signed: positive means slipping to the rider's right. Snow spray
        /// reads the sign to throw the plume out on the correct side.
        /// </summary>
        public float LateralSlip;

        public bool IsGrounded { get; private set; }

        /// <summary>
        /// True only when the surface underfoot is marked SnowSurface.
        /// Spray and tracks check this, so neither happens on the lodge deck.
        /// </summary>
        public bool OnSnow { get; private set; }

        /// <summary>
        /// Multiplier on snow drag for whatever is underfoot. 1 on snow, far
        /// lower on a park box, because steel does not hold you back.
        /// </summary>
        public float SurfaceFriction { get; private set; } = 1f;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundSlopeDegrees { get; private set; }
        public LocomotionKind CurrentKind => _active != null ? _active.Kind : startMode;

        /// <summary>True while sitting on a chairlift. Input and physics are off.</summary>
        public bool IsRiding { get; private set; }

        /// <summary>True on skis or a board, as opposed to on foot.</summary>
        public bool IsRidingSnow =>
            CurrentKind == LocomotionKind.Ski || CurrentKind == LocomotionKind.Snowboard;

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
        Collider _groundCollider;
        bool _groundIsSnow;
        float _groundFriction = 1f;
        Transform _seat;
        Vector3 _seatOffset;
        LocomotionMode _active;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        void Awake()
        {
            Body = GetComponent<CharacterController>();
            Input = GetComponent<PlayerInputReader>();

            ConfigureBody();

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
            if (IsRiding)
            {
                if (_seat != null)
                    transform.SetPositionAndRotation(_seat.TransformPoint(_seatOffset), _seat.rotation);
                return;
            }

            float dt = Time.deltaTime;

            ProbeGround();

            if (allowGearKeysAnywhere)
            {
                int gear = Input.GearPressed;
                if (gear > 0) SetMode((LocomotionKind)gear);
            }

            if (_active != null) _active.Tick(dt);

            StepBody((Velocity + GroundStick) * dt);
            GroundStick = Vector3.zero;
        }

        /// <summary>
        /// The CharacterController sweeps its capsule, but only once per call.
        /// At forty metres a second a single frame is most of a metre, and a
        /// sweep that long can step over a thin face or resolve on the far
        /// side of one. Splitting the move keeps every step shorter than the
        /// body is wide, which is the condition under which sweeping is
        /// actually reliable.
        /// </summary>
        void StepBody(Vector3 move)
        {
            float distance = move.magnitude;
            if (distance <= 0.0001f) return;

            float limit = Mathf.Max(0.05f, maxStepDistance);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / limit), 1, 12);

            Vector3 slice = move / steps;
            for (int i = 0; i < steps; i++) Body.Move(slice);
        }

        /// <summary>
        /// Collision settings the rest of the game depends on. Left at Unity's
        /// defaults, a CharacterController treats anything past 45 degrees as
        /// a wall, which on a mountain is most of the interesting terrain.
        /// </summary>
        void ConfigureBody()
        {
            if (Body == null) return;

            Body.slopeLimit = Mathf.Clamp(slopeLimit, 30f, 89f);
            Body.stepOffset = Mathf.Min(Body.stepOffset, Body.height * 0.3f);
            Body.skinWidth = Mathf.Max(0.02f, Body.radius * 0.1f);
            Body.minMoveDistance = 0f;
        }

        // ---------------- ground ----------------------------------------

        void ProbeGround()
        {
            float radius = Body.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.25f);

            int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits,
                                               0.5f, groundMask, QueryTriggerInteraction.Ignore);

            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            Vector3 raw = Vector3.up;
            Collider surface = null;
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
                    raw = h.normal;
                    surface = h.collider;
                }
            }

            // Only look the markers up when the surface actually changes.
            if (surface != _groundCollider)
            {
                _groundCollider = surface;
                _groundIsSnow = surface != null && surface.GetComponent<SnowSurface>() != null;

                _groundFriction = 1f;
                if (surface != null)
                {
                    var slick = surface.GetComponent<SlickSurface>();
                    if (slick != null) _groundFriction = slick.frictionScale;
                }
            }

            OnSnow = IsGrounded && _groundIsSnow;
            SurfaceFriction = IsGrounded ? _groundFriction : 1f;

            if (!IsGrounded) GroundNormal = Vector3.up;
            else if (!wasGrounded) GroundNormal = raw;   // land on the real slope at once
            else GroundNormal = Vector3.Slerp(GroundNormal, raw,
                                              1f - Mathf.Exp(-groundNormalSmoothing * Time.deltaTime));

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

            if (_visual != null)
            {
                _visual.ShowGear(kind);
                _visual.SetBodyYawOffset(_active.BodyYawOffset);
            }
        }

        // ---------------- placement -------------------------------------

        public void Teleport(Vector3 position)
        {
            Body.enabled = false;
            transform.position = Surfaced(position);
            Body.enabled = true;
            Velocity = Vector3.zero;
        }

        /// <summary>
        /// Never put the body below the snow. Re-enabling a CharacterController
        /// inside a collider is how a spawn or a lift unload turns into a fall
        /// through the world.
        /// </summary>
        Vector3 Surfaced(Vector3 position)
        {
            var mountain = SnowBound.Mountain.MountainGenerator.Instance;
            if (mountain == null || !mountain.Ready) return position;

            float floor = mountain.SampleHeight(position.x, position.z) + Body.height * 0.5f;
            if (position.y < floor) position.y = floor;

            return position;
        }

        // ---------------- riding a lift ---------------------------------

        Transform ILiftPassenger.Transform { get { return transform; } }
        LocomotionKind ILiftPassenger.Gear { get { return CurrentKind; } }
        bool ILiftPassenger.WaitingToBoard { get { return !IsRiding && IsGrounded; } }

        void ILiftPassenger.BoardLift(Transform seat, Vector3 seatOffset)
        {
            AttachTo(seat, seatOffset);
        }

        void ILiftPassenger.LeaveLift(Vector3 position, Vector3 facing, Vector3 velocity)
        {
            Detach(position, facing, velocity, CurrentKind);
        }


        /// <summary>
        /// Hand control to a chairlift seat. Physics and input stop; the body
        /// simply follows the seat until the lift lets go.
        /// </summary>
        public void AttachTo(Transform seat, Vector3 localOffset)
        {
            if (seat == null) return;

            IsRiding = true;
            _seat = seat;
            _seatOffset = localOffset;

            Velocity = Vector3.zero;
            GroundStick = Vector3.zero;
            LateralSlip = 0f;
            IsGrounded = false;
            OnSnow = false;

            Body.enabled = false;
            Input.enableInput = false;   // but Input.enableLook stays on
            if (_visual != null) _visual.SetSeated(true);
        }

        /// <summary>Step off the lift, facing <paramref name="facing"/>, already moving.</summary>
        public void Detach(Vector3 position, Vector3 facing, Vector3 velocity, LocomotionKind kind)
        {
            IsRiding = false;
            _seat = null;

            if (_visual != null) _visual.SetSeated(false);
            Input.enableInput = true;

            Body.enabled = false;
            transform.position = Surfaced(position);
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            Body.enabled = true;

            Velocity = velocity;
            SetMode(kind);

            // SetMode is a no-op when the gear has not changed, so re-enter
            // deliberately: the mode has to pick up the new heading.
            ReEnterMode();
        }

        void ReEnterMode()
        {
            if (_active == null) return;
            _active.OnExit();
            _active.OnEnter();
        }

        public void SpawnAtLodge()
        {
            var lodge = LodgeBuilder.Instance;
            if (lodge == null) return;
            Teleport(lodge.EntrancePosition + Vector3.up * 0.3f);
        }
    }
}
