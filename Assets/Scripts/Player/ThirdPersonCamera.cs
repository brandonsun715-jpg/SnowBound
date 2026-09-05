using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Orbiting follow camera. Mouse turns it, scroll wheel pushes it in and
    /// out, and it pulls in close when terrain gets between it and the player.
    /// </summary>
    // Runs after everything else so it always frames where the player
    // actually ended up this frame, including on a moving chairlift.
    [DefaultExecutionOrder(100)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public PlayerInputReader input;

        [Header("Framing")]
        [Tooltip("Metres above the player's feet that the camera looks at.")]
        public float focusHeight = 1.5f;
        public float distance = 6.5f;
        public float minDistance = 2.5f;
        public float maxDistance = 14f;
        public float zoomSpeed = 40f;

        [Header("Look")]
        public float sensitivity = 2.6f;
        public float minPitch = -30f;
        public float maxPitch = 70f;
        public float startPitch = 14f;

        [Header("Speed")]
        [Tooltip("Optional. Lets the camera drop back and widen out as you gain speed.")]
        public PlayerController player;
        [Tooltip("Speed, m/s, at which the speed effects reach full strength.")]
        public float fastSpeed = 24f;
        [Tooltip("Extra metres the camera falls back at full speed.")]
        public float speedPullback = 3f;
        public float restingFieldOfView = 60f;
        [Tooltip("Widening the lens is most of why fast feels fast.")]
        public float fastFieldOfView = 74f;

        [Header("Chairlift")]
        [Tooltip("The camera sits further back while riding so you can see the chair and the view.")]
        public float ridingDistance = 9f;
        public float ridingFocusHeight = 1.1f;

        [Header("Collision")]
        public float collisionRadius = 0.32f;
        public LayerMask collisionMask = ~0;

        [Header("Cursor")]
        [Tooltip("Off: HudDirector owns the cursor, so nothing fights over it.")]
        public bool lockCursor = false;

        float _yaw;
        float _pitch;
        float _currentDistance;
        float _fov = 60f;
        Camera _camera;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        // Awake rather than Start, because this component spends the start of
        // the game disabled and Start would never have run.
        void Awake()
        {
            _camera = GetComponent<Camera>();
            _pitch = startPitch;
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _currentDistance = distance;
            _fov = restingFieldOfView;

            if (lockCursor && Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            UpdateState(Time.deltaTime);

            Vector3 position;
            Quaternion rotation;
            float fov;
            ComputePose(out position, out rotation, out fov);

            transform.SetPositionAndRotation(position, rotation);
            if (_camera != null) _camera.fieldOfView = fov;
        }

        void UpdateState(float dt)
        {
            if (input != null)
            {
                Vector2 look = input.Look;
                _yaw += look.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, minPitch, maxPitch);
                distance = Mathf.Clamp(distance - input.Zoom * zoomSpeed, minDistance, maxDistance);
            }

            bool riding = player != null && player.IsRiding;
            float rush = !riding && player != null ? Mathf.Clamp01(player.Speed / fastSpeed) : 0f;

            _fov = Mathf.Lerp(_fov, Mathf.Lerp(restingFieldOfView, fastFieldOfView, rush),
                              1f - Mathf.Exp(-4f * dt));

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = target.position + Vector3.up * (riding ? ridingFocusHeight : focusHeight);
            Vector3 back = rotation * Vector3.back;

            float reach = riding ? ridingDistance : distance + rush * speedPullback;
            float wanted = ClearDistance(focus, back, reach);
            // Snap in fast so the camera never clips, ease out slowly.
            float ease = wanted < _currentDistance ? 1f : 1f - Mathf.Exp(-8f * dt);
            _currentDistance = Mathf.Lerp(_currentDistance, wanted, ease);
        }

        /// <summary>Where the camera wants to be, without moving it.</summary>
        public void ComputePose(out Vector3 position, out Quaternion rotation, out float fov)
        {
            bool riding = player != null && player.IsRiding;

            rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 focus = target != null
                ? target.position + Vector3.up * (riding ? ridingFocusHeight : focusHeight)
                : transform.position;

            position = focus + rotation * Vector3.back * _currentDistance;
            fov = _fov;
        }

        /// <summary>Face the way the player is, used when handing control back.</summary>
        public void AlignBehind()
        {
            if (target == null) return;
            _yaw = target.eulerAngles.y;
            _pitch = startPitch;
        }

        /// <summary>How far back the camera can sit before something blocks it.</summary>
        float ClearDistance(Vector3 focus, Vector3 back, float wanted)
        {
            int n = Physics.SphereCastNonAlloc(focus, collisionRadius, back, _hits,
                                               wanted, collisionMask, QueryTriggerInteraction.Ignore);

            float closest = wanted;

            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;
                if (target != null && (h.collider.transform == target ||
                                       h.collider.transform.IsChildOf(target))) continue;
                if (h.distance <= 0f) continue;
                if (h.distance < closest) closest = h.distance;
            }

            return Mathf.Max(minDistance * 0.5f, closest - 0.1f);
        }
    }
}
