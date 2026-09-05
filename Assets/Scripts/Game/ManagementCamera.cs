using UnityEngine;
using SnowBound.Mountain;

namespace SnowBound.Game
{
    /// <summary>
    /// The owner's view: an angled camera orbiting a point on the mountain.
    ///
    /// Never straight down. A ski resort is elevation, and a top-down view
    /// throws away the one thing that makes the place legible — which way the
    /// hill falls. So the pitch flattens as you zoom in, and even at maximum
    /// height the camera stays well off vertical.
    ///
    /// It holds a focus point, a distance and a heading, and everything is
    /// eased towards a target rather than set, so nothing ever snaps.
    /// </summary>
    public class ManagementCamera : MonoBehaviour
    {
        public MountainGenerator mountain;

        [Header("Zoom")]
        public float minZoom = 38f;
        public float maxZoom = 780f;
        public float zoomSpeed = 900f;
        public float startZoom = 320f;

        [Header("Angle")]
        [Tooltip("Degrees below horizontal when zoomed right in.")]
        public float pitchClose = 24f;
        [Tooltip("Degrees below horizontal when zoomed right out. Never ninety.")]
        public float pitchFar = 52f;
        public float startYaw = 180f;
        public float rotateSpeed = 4.2f;

        [Header("Panning")]
        [Tooltip("Metres per second at the closest zoom. Scales up as you pull back.")]
        public float panSpeed = 26f;
        public float edgeMargin = 40f;

        [Header("Feel")]
        public float focusSmoothing = 9f;
        public float zoomSmoothing = 7f;
        public float yawSmoothing = 10f;
        public float fieldOfView = 55f;

        Vector3 _focus, _targetFocus;
        float _zoom, _targetZoom;
        float _yaw, _targetYaw;
        Camera _camera;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (mountain == null) mountain = MountainGenerator.Instance;

            _targetZoom = _zoom = startZoom;
            _targetYaw = _yaw = startYaw;

            _targetFocus = _focus = GroundedFocus(DefaultFocus());
        }

        Vector3 DefaultFocus()
        {
            if (mountain == null) return Vector3.zero;
            return new Vector3(0f, 0f, mountain.length * 0.42f);
        }

        /// <summary>Point the camera at a place in the world without a jump cut.</summary>
        public void FocusOn(Vector3 worldPoint, float zoom = -1f)
        {
            _targetFocus = GroundedFocus(worldPoint);
            if (zoom > 0f) _targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        public void SnapTo(Vector3 worldPoint, float zoom, float yaw)
        {
            _targetFocus = _focus = GroundedFocus(worldPoint);
            _targetZoom = _zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            _targetYaw = _yaw = yaw;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            ReadInput(dt);
            Settle(dt);

            Vector3 position;
            Quaternion rotation;
            float fov;
            ComputePose(out position, out rotation, out fov);

            transform.SetPositionAndRotation(position, rotation);
            if (_camera != null) _camera.fieldOfView = fov;
        }

        void ReadInput(float dt)
        {
            _targetZoom = Mathf.Clamp(_targetZoom - ManagementInput.Zoom * zoomSpeed, minZoom, maxZoom);

            if (ManagementInput.RotateHeld)
                _targetYaw += ManagementInput.MouseDelta.x * rotateSpeed;

            Vector2 pan = ManagementInput.Pan;
            if (pan.sqrMagnitude < 0.0001f) return;

            // Pan in the direction the camera is facing, and faster the
            // further out you are, so crossing the resort always feels the
            // same however close you were looking.
            Quaternion flat = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 move = flat * new Vector3(pan.x, 0f, pan.y);

            float scale = panSpeed * Mathf.Lerp(1f, 5.5f, Mathf.InverseLerp(minZoom, maxZoom, _zoom));
            _targetFocus += move * scale * dt;
            _targetFocus = GroundedFocus(_targetFocus);
        }

        void Settle(float dt)
        {
            _focus = Vector3.Lerp(_focus, _targetFocus, 1f - Mathf.Exp(-focusSmoothing * dt));
            _zoom = Mathf.Lerp(_zoom, _targetZoom, 1f - Mathf.Exp(-zoomSmoothing * dt));
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-yawSmoothing * dt));

            _focus.y = GroundHeight(_focus.x, _focus.z);
        }

        /// <summary>Where the camera wants to be, without moving it.</summary>
        public void ComputePose(out Vector3 position, out Quaternion rotation, out float fov)
        {
            float closeness = Mathf.InverseLerp(minZoom, maxZoom, _zoom);
            float pitch = Mathf.Lerp(pitchClose, pitchFar, closeness);

            rotation = Quaternion.Euler(pitch, _yaw, 0f);
            position = _focus - rotation * Vector3.forward * _zoom;
            fov = fieldOfView;
        }

        Vector3 GroundedFocus(Vector3 point)
        {
            if (mountain != null)
            {
                float half = mountain.width * 0.5f - edgeMargin;
                point.x = Mathf.Clamp(point.x, -half, half);
                point.z = Mathf.Clamp(point.z, edgeMargin, mountain.length - edgeMargin);
            }

            point.y = GroundHeight(point.x, point.z);
            return point;
        }

        float GroundHeight(float x, float z)
        {
            return mountain != null ? mountain.SampleHeight(x, z) : 0f;
        }
    }
}
