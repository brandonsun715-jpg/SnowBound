using UnityEngine;
using UnityEngine.UI;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Player;
using SnowBound.Resort;
using SnowBound.Weather;
using SnowBound.Game;

namespace SnowBound.Hud
{
    /// <summary>
    /// The riding HUD. Four quiet clusters in the corners and nothing at all
    /// in the middle, because the middle is where the mountain is.
    ///
    /// Everything transient — the lift card, the trail introduction, the
    /// control hints — comes in, says its piece and leaves. Nothing that is
    /// only useful once is allowed to stay on screen forever.
    /// </summary>
    public class SkiHud : MonoBehaviour
    {
        public PlayerController player;
        public Chairlift lift;
        public GearRack rack;
        public RunTimer runTimer;
        public MountainGenerator mountain;
        public WeatherSystem weather;
        public ResortRating rating;
        public ResortTraffic traffic;
        public Ledger ledger;
        public ResortIdentity identity;

        [Header("Feel")]
        public float speedEase = 9f;
        public float cashEase = 6f;
        [Tooltip("Seconds the control hints stay up after the player changes what they are doing.")]
        public float hintSeconds = 7f;
        public float trailCardSeconds = 3.6f;

        Canvas _canvas;

        Text _brand, _mountainName, _conditions, _temperature;
        Image _weatherIcon;

        Text _cash, _guests;
        UIStars _stars;
        Text _speed, _speedUnit, _trailName, _trailGrade, _runClock;
        Text _hints;
        UIPanel _hintPanel;

        UIPanel _promptPanel;
        Text _promptText;

        UIPanel _liftPanel;
        Text _liftName, _liftVertical, _liftSeats, _liftFooter;

        UIPanel _trailPanel;
        Text _trailTitle, _trailSub, _trailStats;

        float _shownSpeed, _shownCash;
        float _hintsLeft, _trailCardLeft;
        int _lastPiste = -1;
        LocomotionKind _lastKind = LocomotionKind.Walk;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (lift == null) lift = Chairlift.Instance;
            if (rack == null) rack = FindAnyObjectByType<GearRack>();
            if (runTimer == null) runTimer = FindAnyObjectByType<RunTimer>();
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (weather == null) weather = WeatherSystem.Instance;
            if (rating == null) rating = ResortRating.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (ledger == null) ledger = Ledger.Instance;
            if (identity == null) identity = ResortIdentity.Instance;

            if (ledger != null) _shownCash = ledger.Cash;

            Build();
            _hintsLeft = hintSeconds;
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        // ---------------- building ----------------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "SkiHud", 0);
            Transform root = _canvas.transform;

            BuildResortCluster(root);
            BuildStatusCluster(root);
            BuildSpeedCluster(root);
            BuildHints(root);
            BuildPrompt(root);
            BuildLiftCard(root);
            BuildTrailCard(root);
        }

        void BuildResortCluster(Transform root)
        {
            var topLeft = new Vector2(0f, 1f);

            _brand = UIBuilder.Label(root, "Brand", UITheme.Micro, UITheme.InkFaint, TextAnchor.UpperLeft);
            UIBuilder.Place(_brand.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Margin, -UITheme.Margin), new Vector2(360f, 20f));

            _mountainName = UIBuilder.Label(root, "Mountain", UITheme.Heading, UITheme.Ink,
                                            TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_mountainName.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Margin, -UITheme.Margin - 20f), new Vector2(360f, 32f));

            UIBuilder.Rule(root, "Rule", topLeft, topLeft,
                           new Vector2(UITheme.Margin, -UITheme.Margin - 62f), 148f);

            _weatherIcon = UIBuilder.Icon(root, "WeatherIcon", UIIcons.Sun, UITheme.InkMuted,
                                          topLeft, topLeft,
                                          new Vector2(UITheme.Margin, -UITheme.Margin - 76f), 20f);

            _conditions = UIBuilder.Label(root, "Conditions", UITheme.Label, UITheme.InkMuted,
                                          TextAnchor.UpperLeft);
            UIBuilder.Place(_conditions.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Margin + 28f, -UITheme.Margin - 77f), new Vector2(300f, 20f));

            _temperature = UIBuilder.Label(root, "Temperature", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(_temperature.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Margin + 28f, -UITheme.Margin - 96f), new Vector2(300f, 18f));
        }

        void BuildStatusCluster(Transform root)
        {
            var topRight = new Vector2(1f, 1f);

            _cash = UIBuilder.Label(root, "Cash", UITheme.Title, UITheme.Ink,
                                    TextAnchor.UpperRight, FontStyle.Bold);
            UIBuilder.Place(_cash.rectTransform, topRight, topRight,
                            new Vector2(-UITheme.Margin, -UITheme.Margin), new Vector2(340f, 40f));

            _guests = UIBuilder.Label(root, "Guests", UITheme.Label, UITheme.InkMuted, TextAnchor.UpperRight);
            UIBuilder.Place(_guests.rectTransform, topRight, topRight,
                            new Vector2(-UITheme.Margin, -UITheme.Margin - 42f), new Vector2(340f, 20f));

            _stars = UIStars.Create(root, "Stars", topRight, topRight,
                                    new Vector2(-UITheme.Margin, -UITheme.Margin - 72f), 15f, 4f);
        }

        void BuildSpeedCluster(Transform root)
        {
            var bottomRight = new Vector2(1f, 0f);

            _runClock = UIBuilder.Label(root, "RunClock", UITheme.Label, UITheme.Ice, TextAnchor.LowerRight);
            UIBuilder.Place(_runClock.rectTransform, bottomRight, bottomRight,
                            new Vector2(-UITheme.Margin, UITheme.Margin + 154f), new Vector2(340f, 22f));

            _speed = UIBuilder.Label(root, "Speed", UITheme.Display, UITheme.Ink,
                                     TextAnchor.LowerRight, FontStyle.Bold);
            UIBuilder.Place(_speed.rectTransform, bottomRight, bottomRight,
                            new Vector2(-UITheme.Margin, UITheme.Margin + 74f), new Vector2(340f, 84f));

            _speedUnit = UIBuilder.Label(root, "SpeedUnit", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.LowerRight);
            UIBuilder.Place(_speedUnit.rectTransform, bottomRight, bottomRight,
                            new Vector2(-UITheme.Margin, UITheme.Margin + 56f), new Vector2(340f, 18f));
            _speedUnit.text = UITheme.Track("KM/H");

            _trailName = UIBuilder.Label(root, "TrailName", UITheme.Label, UITheme.Ink, TextAnchor.LowerRight);
            UIBuilder.Place(_trailName.rectTransform, bottomRight, bottomRight,
                            new Vector2(-UITheme.Margin, UITheme.Margin + 24f), new Vector2(340f, 22f));

            _trailGrade = UIBuilder.Label(root, "TrailGrade", UITheme.Micro, UITheme.InkFaint,
                                          TextAnchor.LowerRight);
            UIBuilder.Place(_trailGrade.rectTransform, bottomRight, bottomRight,
                            new Vector2(-UITheme.Margin, UITheme.Margin + 4f), new Vector2(340f, 18f));
        }

        void BuildHints(Transform root)
        {
            var bottomLeft = new Vector2(0f, 0f);

            RectTransform holder = UIBuilder.Place(UIBuilder.Node(root, "Hints"), bottomLeft, bottomLeft,
                                                   new Vector2(UITheme.Margin, UITheme.Margin),
                                                   new Vector2(300f, 80f));
            holder.gameObject.AddComponent<CanvasGroup>();
            _hintPanel = holder.gameObject.AddComponent<UIPanel>();
            _hintPanel.riseDistance = 8f;

            _hints = UIBuilder.Label(holder, "HintText", UITheme.Label, UITheme.InkMuted,
                                     TextAnchor.LowerLeft);
            UIBuilder.Stretch(_hints.rectTransform);

            _hintPanel.ShowInstantly();
        }

        void BuildPrompt(Transform root)
        {
            RectTransform pill = UIBuilder.Glass(root, "Prompt", new Vector2(0.5f, 0f),
                                                 new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                                                 new Vector2(430f, 52f), UITheme.RadiusSmall);
            pill.gameObject.AddComponent<CanvasGroup>();
            _promptPanel = pill.gameObject.AddComponent<UIPanel>();

            _promptText = UIBuilder.Label(pill, "PromptText", UITheme.Label, UITheme.Ink,
                                          TextAnchor.MiddleCenter);
            UIBuilder.Stretch(_promptText.rectTransform);

            _promptPanel.HideInstantly();
        }

        void BuildLiftCard(Transform root)
        {
            RectTransform card = UIBuilder.Glass(root, "LiftCard", new Vector2(0.5f, 0f),
                                                 new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                                                 new Vector2(400f, 186f));
            card.gameObject.AddComponent<CanvasGroup>();
            _liftPanel = card.gameObject.AddComponent<UIPanel>();

            var topLeft = new Vector2(0f, 1f);

            UIBuilder.Icon(card, "LiftIcon", UIIcons.Lift, UITheme.Ice, topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad), 22f);

            _liftName = UIBuilder.Label(card, "LiftName", UITheme.Heading, UITheme.Ink,
                                        TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_liftName.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 32f, -UITheme.Pad - 2f), new Vector2(320f, 28f));

            UIBuilder.Rule(card, "LiftRule", topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad - 42f), 400f - UITheme.Pad * 2f);

            _liftVertical = UIBuilder.Label(card, "LiftVertical", UITheme.Body, UITheme.Ink,
                                            TextAnchor.UpperLeft);
            UIBuilder.Place(_liftVertical.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 56f), new Vector2(320f, 24f));

            _liftSeats = UIBuilder.Label(card, "LiftSeats", UITheme.Body, UITheme.InkMuted,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(_liftSeats.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 82f), new Vector2(320f, 24f));

            _liftFooter = UIBuilder.Label(card, "LiftFooter", UITheme.Micro, UITheme.Ice,
                                          TextAnchor.LowerCenter);
            UIBuilder.Place(_liftFooter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, UITheme.Pad), new Vector2(360f, 22f));

            _liftPanel.HideInstantly();
        }

        void BuildTrailCard(Transform root)
        {
            RectTransform card = UIBuilder.Place(UIBuilder.Node(root, "TrailIntro"),
                                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                 new Vector2(0f, 190f), new Vector2(700f, 150f));
            card.gameObject.AddComponent<CanvasGroup>();
            _trailPanel = card.gameObject.AddComponent<UIPanel>();
            _trailPanel.riseDistance = 22f;

            _trailTitle = UIBuilder.Label(card, "TrailTitle", UITheme.Hero, UITheme.Ink,
                                          TextAnchor.UpperCenter, FontStyle.Bold);
            UIBuilder.Place(_trailTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            Vector2.zero, new Vector2(700f, 58f));

            _trailSub = UIBuilder.Label(card, "TrailSub", UITheme.Label, UITheme.Ice,
                                        TextAnchor.UpperCenter);
            UIBuilder.Place(_trailSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -62f), new Vector2(700f, 22f));

            UIBuilder.Rule(card, "TrailRule", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                           new Vector2(0f, -94f), 180f);

            _trailStats = UIBuilder.Label(card, "TrailStats", UITheme.Label, UITheme.InkMuted,
                                          TextAnchor.UpperCenter);
            UIBuilder.Place(_trailStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -108f), new Vector2(700f, 24f));

            _trailPanel.HideInstantly();
        }

        // ---------------- running -----------------------------------------

        void Update()
        {
            if (player == null || _canvas == null || !_canvas.enabled) return;

            float dt = Time.unscaledDeltaTime;

            UpdateResort();
            UpdateStatus(dt);
            UpdateSpeed(dt);
            UpdateContext(dt);
        }

        void UpdateResort()
        {
            if (_brand == null) return;

            string resort = identity != null ? identity.resortName : "Snowbound";
            string peak = identity != null ? identity.mountainName : "Larch Peak";

            _brand.text = UITheme.Track(resort.ToUpperInvariant(), 2);
            _mountainName.text = peak.ToUpperInvariant();

            if (weather == null) return;

            _weatherIcon.sprite = UIIcons.Weather(weather.storminess);
            _conditions.text = weather.Description.ToUpperInvariant() + "   ·   " +
                               weather.SnowDescription.ToUpperInvariant();
            _temperature.text = Mathf.RoundToInt(weather.TemperatureC) + "°C";
        }

        void UpdateStatus(float dt)
        {
            if (ledger != null)
            {
                _shownCash = UITheme.Approach(_shownCash, ledger.Cash, cashEase, dt);
                if (Mathf.Abs(_shownCash - ledger.Cash) < 1f) _shownCash = ledger.Cash;
                _cash.text = Ledger.Money(_shownCash);
            }

            _guests.text = traffic != null
                ? UITheme.Track(traffic.GuestsToday + " GUESTS")
                : string.Empty;

            if (rating != null) _stars.Set(rating.Stars);
        }

        void UpdateSpeed(float dt)
        {
            float kph = player.Speed * 3.6f;
            _shownSpeed = UITheme.Approach(_shownSpeed, kph, speedEase, dt);
            _speed.text = Mathf.RoundToInt(_shownSpeed).ToString();

            int piste = CurrentPiste();
            if (piste >= 0 && mountain != null)
            {
                PisteDefinition run = mountain.pistes[piste];
                _trailName.text = run.name.ToUpperInvariant();
                _trailGrade.text = UITheme.Track(GradeName(run.grade));
                _trailGrade.color = GradeColour(run.grade);
            }
            else
            {
                _trailName.text = UITheme.Track("OFF PISTE");
                _trailGrade.text = string.Empty;
            }

            _runClock.text = runTimer != null && runTimer.Running
                ? UITheme.Track("RUN " + RunTimer.Format(runTimer.Elapsed))
                : string.Empty;
        }

        void UpdateContext(float dt)
        {
            bool onSnow = player.IsRidingSnow;

            if (player.CurrentKind != _lastKind)
            {
                _lastKind = player.CurrentKind;
                _hintsLeft = hintSeconds;
            }

            // The lift card takes priority: it is the only thing you can act on.
            bool showLift = lift != null && lift.PlayerInLoadingArea && !player.IsRiding;
            bool showRack = !showLift && rack != null && rack.PlayerInRange && !player.IsRiding;

            if (showLift) ShowLiftCard(); else _liftPanel.Hide();

            if (showRack)
            {
                _promptText.text = UITheme.Track("1  BOOTS      2  SKIS      3  SNOWBOARD");
                _promptPanel.Show();
            }
            else if (player.IsRiding)
            {
                _promptText.text = UITheme.Track("RIDING TO THE SUMMIT");
                _promptPanel.Show();
            }
            else
            {
                _promptPanel.Hide();
            }

            // Controls say their piece, then get out of the way.
            _hints.text = onSnow
                ? "CARVE   A D\nBRAKE   S\nJUMP   SPACE"
                : "MOVE   W A S D\nSPRINT   SHIFT\nJUMP   SPACE";

            if (_hintsLeft > 0f)
            {
                _hintsLeft -= dt;
                _hintPanel.Show();
            }
            else
            {
                _hintPanel.Hide();
            }

            UpdateTrailIntro(dt);
        }

        void ShowLiftCard()
        {
            var facility = lift.GetComponent<LiftFacility>();

            string peak = identity != null ? identity.mountainName : "Larch";
            _liftName.text = peak.ToUpperInvariant() + " EXPRESS";
            _liftVertical.text = Mathf.RoundToInt(LiftVertical()) + " M VERTICAL";
            _liftSeats.text = facility != null
                ? facility.Seats + " SEAT   ·   " + Mathf.RoundToInt(facility.LineSpeed * 3.6f) + " KM/H"
                : "FIXED GRIP";
            _liftFooter.text = UITheme.Track("STAND CLEAR - NEXT CHAIR INCOMING");

            _liftPanel.Show();
        }

        float LiftVertical()
        {
            if (lift == null) return 0f;
            return Mathf.Max(0f, lift.UnloadPoint.y - lift.BoardingPoint.y);
        }

        void UpdateTrailIntro(float dt)
        {
            int piste = CurrentPiste();

            if (piste >= 0 && piste != _lastPiste && player.IsRidingSnow && player.Speed > 4f)
            {
                _lastPiste = piste;
                PisteDefinition run = mountain.pistes[piste];

                _trailTitle.text = run.name.ToUpperInvariant();
                _trailSub.text = UITheme.Track(GradeName(run.grade) + " RUN", 2);
                _trailSub.color = GradeColour(run.grade);
                _trailStats.text = (mountain.PisteLength(piste) / 1000f).ToString("0.0") + " KM"
                                 + "        " + Mathf.RoundToInt(mountain.PisteVertical(piste)) + " M VERTICAL";

                _trailCardLeft = trailCardSeconds;
                _trailPanel.Show();
            }

            if (piste < 0) _lastPiste = -1;

            if (_trailCardLeft <= 0f) return;

            _trailCardLeft -= dt;
            if (_trailCardLeft <= 0f) _trailPanel.Hide();
        }

        int CurrentPiste()
        {
            if (mountain == null || player == null) return -1;

            Vector3 at = player.transform.position;
            if (!mountain.IsOnPiste(at.x, at.z, 4f)) return -1;

            return mountain.NearestPiste(at.x, at.z);
        }

        public static string GradeName(PisteGrade grade)
        {
            switch (grade)
            {
                case PisteGrade.Beginner: return "GREEN";
                case PisteGrade.Advanced: return "BLACK";
                default: return "BLUE";
            }
        }

        public static Color GradeColour(PisteGrade grade)
        {
            switch (grade)
            {
                case PisteGrade.Beginner: return UITheme.GradeGreen;
                case PisteGrade.Advanced: return UITheme.GradeRed;
                default: return UITheme.GradeBlue;
            }
        }
    }
}
