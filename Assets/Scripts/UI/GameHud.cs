using UnityEngine;
using UnityEngine.UI;
using SnowBound.Player;
using SnowBound.Lifts;
using SnowBound.Game;
using SnowBound.Weather;

namespace SnowBound.Hud
{
    /// <summary>
    /// The prototype heads-up display: what is on your feet, how fast you are
    /// going, the clock, and one line telling you what to do next.
    ///
    /// It only ever reads state from the other systems. Nothing here decides
    /// anything, so replacing it with a designed UI later touches this file
    /// alone.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        const string ContainerName = "GeneratedSkiHud";

        public PlayerController player;
        public Chairlift lift;
        public GearRack rack;
        public RunTimer timer;
        public WeatherSystem weather;

        Text _status;
        Text _clock;
        Text _prompt;

        void Start()
        {
            Build();
        }

        void Build()
        {
            Canvas canvas = HudFactory.Canvas(transform, ContainerName, 0);
            Transform root = canvas.transform;

            if (HudFactory.Font == null)
                Debug.LogWarning("[GameHud] No built-in font found; the HUD will be blank.", this);

            var wide = new Vector2(1000f, 220f);

            _status = HudFactory.Label(root, "Status", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(36f, -30f), wide, TextAnchor.UpperLeft, 34);

            _clock = HudFactory.Label(root, "Clock", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                      new Vector2(-36f, -30f), wide, TextAnchor.UpperRight, 34);

            _prompt = HudFactory.Label(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                       new Vector2(0f, 74f), wide, TextAnchor.LowerCenter, 30);
        }

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (lift == null) lift = Chairlift.Instance;
            if (rack == null) rack = FindAnyObjectByType<GearRack>();
            if (timer == null) timer = FindAnyObjectByType<RunTimer>();
            if (weather == null) weather = WeatherSystem.Instance;

            if (player == null || _status == null) return;

            string report = GearName(player.CurrentKind) + "\n" +
                            Mathf.RoundToInt(player.Speed * 3.6f) + " km/h";

            if (weather != null)
                report += "\n" + weather.Description + "  \u00b7  " + weather.SnowDescription;

            _status.text = report;

            _clock.text = ClockText();
            _prompt.text = PromptText();
        }

        static string GearName(LocomotionKind kind)
        {
            switch (kind)
            {
                case LocomotionKind.Ski: return "Skis";
                case LocomotionKind.Snowboard: return "Snowboard";
                default: return "On foot";
            }
        }

        string ClockText()
        {
            if (timer == null) return string.Empty;

            if (timer.Running)
                return "Run  " + RunTimer.Format(timer.Elapsed);

            string best = "Best  " + RunTimer.Format(timer.BestTime);
            if (timer.LastTime < 0f) return best;

            return "Last  " + RunTimer.Format(timer.LastTime) + "\n" + best;
        }

        string PromptText()
        {
            if (player.IsRiding) return "Riding up. Enjoy the view.";

            if (lift != null && lift.PlayerInLoadingArea)
                return "Wait here. The next chair will pick you up.";

            if (rack != null && rack.PlayerInRange)
                return "1  Boots        2  Skis        3  Snowboard";

            if (!player.IsRidingSnow)
                return "Collect your gear from the rack outside the lodge.";

            if (timer != null && timer.Running)
                return string.Empty;

            return "Skate to the chairlift and ride to the summit.";
        }
    }
}
