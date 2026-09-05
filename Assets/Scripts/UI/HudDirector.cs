using UnityEngine;
using SnowBound.Game;
using SnowBound.Player;
using SnowBound.Resort;
using SnowBound.Weather;

namespace SnowBound.Hud
{
    /// <summary>
    /// Decides which interface the player is looking at.
    ///
    /// The mode is not decided here — ModeDirector owns that, and the cursor
    /// with it. This only dresses whichever mode is current, and makes sure
    /// exactly one interface is up: the riding HUD on the mountain, the
    /// dashboard in management, neither during the flight between them, and
    /// the day's figures over the top of all three.
    /// </summary>
    public class HudDirector : MonoBehaviour
    {
        public ModeDirector modes;
        public PlayerController player;
        public SkiHud skiHud;
        public ManagementHud managementHud;
        public ManagementScreen overview;
        public BuildPanel build;
        public BuildController builder;
        public TrailBuilder trails;
        public DaySummary summary;
        public NotificationStack notifications;
        public ResortRating rating;
        public WeatherSystem weather;

        [Header("Announcements")]
        public bool announceRating = true;

        int _lastWholeStar = -1;
        bool _wasStorming;

        void Start()
        {
            if (modes == null) modes = ModeDirector.Instance;
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (skiHud == null) skiHud = FindAnyObjectByType<SkiHud>();
            if (managementHud == null) managementHud = FindAnyObjectByType<ManagementHud>();
            if (overview == null) overview = FindAnyObjectByType<ManagementScreen>();
            if (build == null) build = FindAnyObjectByType<BuildPanel>();
            if (builder == null) builder = FindAnyObjectByType<BuildController>();
            if (trails == null) trails = FindAnyObjectByType<TrailBuilder>();
            if (summary == null) summary = FindAnyObjectByType<DaySummary>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
            if (rating == null) rating = ResortRating.Instance;
            if (weather == null) weather = WeatherSystem.Instance;
        }

        void Update()
        {
            bool booksOpen = summary != null && summary.IsOpen;

            if (booksOpen)
            {
                Dress(false, false);
                if (overview != null) overview.Close();
                if (build != null) build.Close();
                return;
            }

            bool flying = modes != null && modes.Transitioning;
            bool managing = modes != null && modes.Mode == GameMode.Management && !flying;
            bool riding = modes != null && modes.Mode == GameMode.Mountain && !flying;

            Dress(riding, managing);

            if (!flying) ReadShortcuts(managing, riding);

            WatchForMoments();
        }

        void Dress(bool mountain, bool management)
        {
            if (skiHud != null) skiHud.SetVisible(mountain);
            if (managementHud != null) managementHud.SetVisible(management);

            // Nothing but the world during a transition.
            if (management) return;

            if (overview != null && overview.IsOpen) overview.Close();
            if (build != null && build.IsOpen) build.Close();
        }

        /// <summary>
        /// Escape steps back exactly one level, and this is the only place
        /// that decides what one level means: put down what you are holding,
        /// then close what is open, then come off the mountain.
        /// </summary>
        void ReadShortcuts(bool managing, bool riding)
        {
            if (ManagementInput.BackPressed)
            {
                if (builder != null && builder.Placing) { builder.Cancel(); return; }
                if (trails != null && trails.Planning) { trails.Cancel(); return; }

                if (managing && build != null && build.IsOpen) { build.Close(); return; }
                if (managing && overview != null && overview.IsOpen) { overview.Close(); return; }

                if (riding && modes != null) modes.EnterManagement();
                return;
            }

            if (player == null || player.Input == null) return;
            if (!player.Input.ManagementPressed) return;

            if (riding && modes != null) { modes.EnterManagement(); return; }
            if (!managing || overview == null) return;

            if (build != null) build.Close();

            if (overview.IsOpen) overview.Close();
            else overview.Open();
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
                notifications.Announce("Major snowstorm",
                                       "Guests are staying away. The powder will be worth it.");

            _wasStorming = storming;
        }
    }
}
