using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Orbiting follow camera. Mouse turns it, scroll wheel pushes it in and
    /// out, and it pulls in close when terrain gets between it and the player.
    /// </summary>
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

        [Header("Collision")]
        public float collisionRadius = 0.32f;
        public LayerMask collisionMask = ~0;

        [Header("Cursor")]
        public bool lockCursor = true;

        float _yaw;
        float _pitch;
        float _currentDistance;
        Camera _camera;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        void Start()
        {
            _camera = GetComponent<Camera>();
            _pitch = startPitch;
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _currentDistance = distance;

            if (lockCursor && Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (input != null)
            {
                Vector2 look = input.Look;
                _yaw += look.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, minPitch, maxPitch);
                distance = Mathf.Clamp(distance - input.Zoom * zoomSpeed, minDistance, maxDistance);
            }

            float rush = player != null ? Mathf.Clamp01(player.Speed / fastSpeed) : 0f;

            if (_camera != null)
            {
                float wantFov = Mathf.Lerp(restingFieldOfView, fastFieldOfView, rush);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, wantFov,
                                                 1f - Mathf.Exp(-4f * Time.deltaTime));
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = target.position + Vector3.up * focusHeight;
            Vector3 back = rotation * Vector3.back;

            float wanted = ClearDistance(focus, back, distance + rush * speedPullback);
            // Snap in fast so the camera never clips, ease out slowly.
            float ease = wanted < _currentDistance ? 1f : 1f - Mathf.Exp(-8f * Time.deltaTime);
            _currentDistance = Mathf.Lerp(_currentDistance, wanted, ease);

            transform.SetPositionAndRotation(focus + back * _currentDistance, rotation);
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
