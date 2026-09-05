using UnityEngine;
using SnowBound.Mountain;

namespace SnowBound.Game
{
    /// <summary>
    /// The owner's view: a camera you fly.
    ///
    /// WASD moves it about the mountain, holding a mouse button looks around,
    /// and the wheel takes it up and down. It is not an orbit rig round a
    /// focus point, because planning a resort means going and looking at
    /// things — under the trees, along a ridge, up the line a lift would take
    /// — and an orbit camera can only ever look at the middle of the map.
    ///
    /// It will not go below the snow or outside the resort, and it will not
    /// look straight down: a ski hill is elevation, and looking down the
    /// vertical axis throws away the one thing that makes the place legible.
    /// </summary>
    public class ManagementCamera : MonoBehaviour
    {
        public MountainGenerator mountain;

        [Header("Flying")]
        [Tooltip("Metres per second at ground level. Faster the higher you are.")]
        public float moveSpeed = 46f;
        [Tooltip("Multiplier while shift is held.")]
        public float sprint = 3.2f;
        [Tooltip("Metres per second straight up, on the wheel or Q and E.")]
        public float liftSpeed = 42f;

        [Header("Looking")]
        public float lookSpeed = 3.4f;
        [Tooltip("Furthest the camera will tip down. Never quite vertical.")]
        public float maxPitch = 78f;
        public float minPitch = -30f;

        [Header("Height")]
        [Tooltip("Closest the camera comes to the snow.")]
        public float minHeightAboveGround = 8f;
        public float maxHeightAboveGround = 900f;
        public float startHeight = 190f;
        public float edgeMargin = 30f;

        [Header("Feel")]
        public float moveSmoothing = 12f;
        public float lookSmoothing = 18f;
        public float fieldOfView = 55f;

        Camera _camera;

        Vector3 _position, _targetPosition;
        float _yaw, _targetYaw;
        float _pitch, _targetPitch;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (mountain == null) mountain = MountainGenerator.Instance;

            // Yaw zero looks up the mountain. A new resort should open on the
            // ground it has to develop, not on the car park.
            _targetYaw = _yaw = 0f;
            _targetPitch = _pitch = 28f;

            Vector3 start = DefaultPosition();
            start.y = GroundHeight(start) + startHeight;

            _targetPosition = _position = Settle(start);
        }

        Vector3 DefaultPosition()
        {
            if (mountain == null) return new Vector3(0f, startHeight, 0f);

            // Looking up the hill from just below the base area, so the first
            // thing a new resort shows you is the mountain you get to develop.
            return new Vector3(0f, 0f, mountain.length * 0.08f);
        }

        // ---------------- other systems ------------------------------------

        /// <summary>Fly to a place in the world. Keeps whatever heading you had.</summary>
        public void FocusOn(Vector3 worldPoint, float distance = -1f)
        {
            float back = distance > 0f ? distance : 120f;

            Quaternion facing = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
            _targetPosition = Settle(worldPoint - facing * Vector3.forward * back);
        }

        public void SnapTo(Vector3 worldPoint, float distance, float yaw)
        {
            _targetYaw = _yaw = yaw;
            _targetPitch = _pitch = 34f;

            Quaternion facing = Quaternion.Euler(_pitch, _yaw, 0f);
            _targetPosition = _position = Settle(worldPoint - facing * Vector3.forward * distance);
        }

        /// <summary>Where the camera wants to be, without moving it.</summary>
        public void ComputePose(out Vector3 position, out Quaternion rotation, out float fov)
        {
            position = _position;
            rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            fov = fieldOfView;
        }

        // ---------------- running ------------------------------------------

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            Look(dt);
            Fly(dt);

            _position = Vector3.Lerp(_position, _targetPosition, 1f - Mathf.Exp(-moveSmoothing * dt));
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-lookSmoothing * dt));
            _pitch = Mathf.Lerp(_pitch, _targetPitch, 1f - Mathf.Exp(-lookSmoothing * dt));

            transform.SetPositionAndRotation(_position, Quaternion.Euler(_pitch, _yaw, 0f));
            if (_camera != null) _camera.fieldOfView = fieldOfView;
        }

        void Look(float dt)
        {
            if (!ManagementInput.LookHeld) return;

            Vector2 delta = ManagementInput.MouseDelta;

            _targetYaw += delta.x * lookSpeed;
            _targetPitch = Mathf.Clamp(_targetPitch - delta.y * lookSpeed, minPitch, maxPitch);
        }

        void Fly(float dt)
        {
            Vector2 pan = ManagementInput.Pan;
            float lift = ManagementInput.Lift;

            if (pan.sqrMagnitude < 0.0001f && Mathf.Abs(lift) < 0.0001f) return;

            // Forward is where you are looking, flattened. Looking down and
            // pressing W should take you further down the hill, not into it.
            Quaternion flat = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 move = flat * new Vector3(pan.x, 0f, pan.y);

            // Higher up means bigger strides, so crossing the resort takes the
            // same time whether you are inspecting a lift or looking at the map.
            float above = Mathf.Max(1f, _targetPosition.y - GroundHeight(_targetPosition));
            float scale = moveSpeed * Mathf.Lerp(1f, 6f, Mathf.InverseLerp(minHeightAboveGround, 500f, above));

            if (ManagementInput.Faster) scale *= sprint;

            _targetPosition += move * scale * dt;
            _targetPosition += Vector3.up * lift * liftSpeed * dt;
            _targetPosition = Settle(_targetPosition);
        }

        /// <summary>Keep the camera inside the resort and above the snow.</summary>
        Vector3 Settle(Vector3 point)
        {
            if (mountain != null)
            {
                float half = mountain.width * 0.5f + edgeMargin;
                point.x = Mathf.Clamp(point.x, -half, half);
                point.z = Mathf.Clamp(point.z, -edgeMargin, mountain.length + edgeMargin);
            }

            float ground = GroundHeight(point);
            point.y = Mathf.Clamp(point.y,
                                  ground + minHeightAboveGround,
                                  ground + maxHeightAboveGround);

            return point;
        }

        float GroundHeight(Vector3 point)
        {
            return mountain != null ? mountain.SampleHeight(point.x, point.z) : 0f;
        }
    }
}
