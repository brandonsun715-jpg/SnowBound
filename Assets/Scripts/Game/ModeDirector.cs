using UnityEngine;
using SnowBound.Player;

namespace SnowBound.Game
{
    public enum GameMode
    {
        Management,
        Mountain
    }

    /// <summary>
    /// Which perspective the player is looking through.
    ///
    /// There is one world and one camera. Management and mountain are two
    /// rigs that both know where the camera should be, and switching modes
    /// flies it from one answer to the other rather than cutting. That is the
    /// whole trick: the player is not opening a different game, they are
    /// walking down into the resort they were just looking at.
    ///
    /// The world keeps running throughout. Nothing is paused, reset or
    /// reloaded, so the guests you were watching from above are the guests
    /// you ski past.
    /// </summary>
    public class ModeDirector : MonoBehaviour
    {
        static ModeDirector _instance;

        public static ModeDirector Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<ModeDirector>();
                return _instance;
            }
        }

        public PlayerController player;
        public ManagementCamera managementCamera;
        public ThirdPersonCamera playerCamera;
        public SelectionController selection;

        [Header("Start")]
        public GameMode startMode = GameMode.Management;

        [Header("Transition")]
        public float flightSeconds = 1.25f;
        [Tooltip("How far out the management camera sits when you come back up.")]
        public float returnZoom = 150f;

        public GameMode Mode { get; private set; }
        public bool Transitioning { get; private set; }

        public event System.Action<GameMode> ModeChanged;

        Camera _camera;
        Vector3 _fromPosition, _toPosition;
        Quaternion _fromRotation, _toRotation;
        float _fromFov, _toFov;
        float _flight;
        GameMode _flyingTo;

        void OnEnable() { _instance = this; }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (managementCamera == null) managementCamera = FindAnyObjectByType<ManagementCamera>();
            if (playerCamera == null) playerCamera = FindAnyObjectByType<ThirdPersonCamera>();
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();

            _camera = managementCamera != null
                ? managementCamera.GetComponent<Camera>()
                : Camera.main;

            Mode = startMode;
            ApplyMode(Mode);
        }

        void Update()
        {
            if (!Transitioning) return;

            _flight += Time.unscaledDeltaTime / Mathf.Max(0.05f, flightSeconds);

            float t = Mathf.Clamp01(_flight);
            // Ease in and out, so the flight leaves and arrives softly.
            float eased = t * t * (3f - 2f * t);

            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(
                    Vector3.Lerp(_fromPosition, _toPosition, eased),
                    Quaternion.Slerp(_fromRotation, _toRotation, eased));
                _camera.fieldOfView = Mathf.Lerp(_fromFov, _toFov, eased);
            }

            if (t < 1f) return;

            Transitioning = false;
            Mode = _flyingTo;
            ApplyMode(Mode);

            if (ModeChanged != null) ModeChanged(Mode);
        }

        // ---------------- switching ------------------------------------------

        public void EnterMountain()
        {
            if (Transitioning || Mode == GameMode.Mountain || player == null) return;

            if (selection != null) { selection.Clear(); selection.Active = false; }

            // Point the player camera the way the player is standing, then ask
            // it where it would sit, and fly there.
            if (playerCamera != null) playerCamera.AlignBehind();

            BeginFlight(GameMode.Mountain);
        }

        public void EnterManagement()
        {
            if (Transitioning || Mode == GameMode.Management) return;

            // Come back up looking at wherever the player got to.
            if (managementCamera != null && player != null)
            {
                float yaw = _camera != null ? _camera.transform.eulerAngles.y : 180f;
                managementCamera.SnapTo(player.transform.position, returnZoom, yaw);
            }

            BeginFlight(GameMode.Management);
        }

        public void Toggle()
        {
            if (Mode == GameMode.Management) EnterMountain();
            else EnterManagement();
        }

        void BeginFlight(GameMode destination)
        {
            _flyingTo = destination;

            if (_camera != null)
            {
                _fromPosition = _camera.transform.position;
                _fromRotation = _camera.transform.rotation;
                _fromFov = _camera.fieldOfView;
            }

            // Both rigs go quiet during the flight; the director drives.
            if (managementCamera != null) managementCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = false;

            if (destination == GameMode.Mountain && playerCamera != null)
                playerCamera.ComputePose(out _toPosition, out _toRotation, out _toFov);
            else if (managementCamera != null)
                managementCamera.ComputePose(out _toPosition, out _toRotation, out _toFov);

            // The player is only in charge once the camera has arrived.
            if (player != null && player.Input != null) player.Input.suspended = true;

            _flight = 0f;
            Transitioning = true;
        }

        void ApplyMode(GameMode mode)
        {
            bool managing = mode == GameMode.Management;

            if (managementCamera != null) managementCamera.enabled = managing;
            if (playerCamera != null) playerCamera.enabled = !managing;
            if (selection != null) selection.Active = managing;

            if (player != null && player.Input != null) player.Input.suspended = managing;

            Cursor.lockState = managing ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = managing;
        }
    }
}
