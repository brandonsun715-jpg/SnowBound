using UnityEngine;
using SnowBound.Player;
using SnowBound.Resort;
using SnowBound.Weather;

namespace SnowBound.Hud
{
    /// <summary>
    /// Decides which interface the player is looking at.
    ///
    /// The player should never see everything at once, so exactly one of the
    /// riding HUD and the resort dashboard is up at a time, and the day's
    /// figures outrank both. This is also the only place the cursor is locked
    /// or freed, because two scripts fighting over the cursor is a bug you
    /// only find on someone else's machine.
    /// </summary>
    public class HudDirector : MonoBehaviour
    {
        public PlayerController player;
        public SkiHud skiHud;
        public ManagementScreen management;
        public DaySummary summary;
        public NotificationStack notifications;
        public ResortRating rating;
        public WeatherSystem weather;

        [Header("Announcements")]
        [Tooltip("Announce when the rating crosses a whole star.")]
        public bool announceRating = true;

        int _lastWholeStar = -1;
        bool _wasStorming;
        bool _cursorFree;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (skiHud == null) skiHud = FindAnyObjectByType<SkiHud>();
            if (management == null) management = FindAnyObjectByType<ManagementScreen>();
            if (summary == null) summary = FindAnyObjectByType<DaySummary>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
            if (rating == null) rating = ResortRating.Instance;
            if (weather == null) weather = WeatherSystem.Instance;

            SetCursorFree(false);
        }

        void Update()
        {
            if (player == null || player.Input == null) return;

            bool booksOpen = summary != null && summary.IsOpen;

            if (booksOpen)
            {
                if (management != null && management.IsOpen) management.Close();
                if (skiHud != null) skiHud.SetVisible(false);
                SetCursorFree(true);
                return;
            }

            if (player.Input.ManagementPressed) Toggle();

            bool managing = management != null && management.IsOpen;

            if (skiHud != null) skiHud.SetVisible(!managing);
            SetCursorFree(managing);
            player.Input.suspended = managing;

            // Riding is not a time to be reading a spreadsheet, but the
            // dashboard is safe to leave open while standing about.
            if (managing && player.IsRidingSnow && player.Speed > 6f) Toggle();

            WatchForMoments();
        }

        void Toggle()
        {
            if (management == null) return;

            if (management.IsOpen) management.Close();
            else management.Open();
        }

        void SetCursorFree(bool free)
        {
            if (free == _cursorFree) return;

            _cursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        /// <summary>The handful of things worth interrupting the player for.</summary>
        void WatchForMoments()
        {
            if (notifications == null) return;

            if (announceRating && rating != null)
            {
                int star = Mathf.FloorToInt(rating.Stars);
                if (_lastWholeStar < 0) _lastWholeStar = star;
                else if (star > _lastWholeStar)
                {
                    _lastWholeStar = star;
                    notifications.Announce("Resort rating increased",
                                           "Now " + rating.Stars.ToString("0.0") + " out of 5.");
                }
                else if (star < _lastWholeStar)
                {
                    _lastWholeStar = star;
                }
            }

            if (weather == null) return;

            bool storming = weather.storminess > 0.7f;
            if (storming && !_wasStorming)
                notifications.Announce("Major snowstorm", "Guests are staying away. The powder will be worth it.");

            _wasStorming = storming;
        }
    }
}
