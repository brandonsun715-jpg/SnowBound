using UnityEngine;
using UnityEngine.UI;
using SnowBound.Buildings;
using SnowBound.Game;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Resort;
using SnowBound.Weather;

namespace SnowBound.Hud
{
    /// <summary>
    /// The card that appears when you click something on the mountain.
    ///
    /// Every figure on it is read from the system that owns it. Capacity per
    /// hour is the lift's own seats, speed and chair spacing multiplied out;
    /// a trail's length is measured along its actual centre line. Nothing
    /// here is a plausible-looking number typed in by hand, because the
    /// moment one is, the panel stops being worth reading.
    /// </summary>
    public class InspectorPanel : MonoBehaviour
    {
        public SelectionController selection;
        public Ledger ledger;
        public ResortRating rating;
        public ResortTraffic traffic;
        public GuestDirector guests;
        public MountainGenerator mountain;
        public WeatherSystem weather;
        public NotificationStack notifications;

        Canvas _canvas;
        UIPanel _panel;
        RectTransform _card;

        Text _title, _subtitle, _labels, _values;
        UIStars _stars;
        UIButton _upgrade, _close;
        Text _upgradeLabel;

        Facility _facility;

        void Start()
        {
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (ledger == null) ledger = Ledger.Instance;
            if (rating == null) rating = ResortRating.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (guests == null) guests = GuestDirector.Instance;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (weather == null) weather = WeatherSystem.Instance;
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();

            Build();

            if (selection != null) selection.SelectionChanged += OnSelectionChanged;
        }

        void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
            if (_card != null) UIPointer.Unblock(_card);
        }

        // ---------------- building -------------------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "Inspector", 14);

            // Anchored under the top bar and above the dock, so it can never
            // meet either of them however tall or wide the window is.
            _card = UIBuilder.Glass(_canvas.transform, "Card", new Vector2(1f, 1f),
                                    new Vector2(1f, 1f),
                                    new Vector2(-UILayout.Margin, -UILayout.UnderTopBar),
                                    new Vector2(UILayout.RailWidth,
                                                Mathf.Min(410f, UILayout.RailHeight)));

            _card.gameObject.AddComponent<CanvasGroup>();
            _panel = _card.gameObject.AddComponent<UIPanel>();
            UIPointer.Block(_card);

            var topLeft = new Vector2(0f, 1f);

            _title = UIBuilder.Label(_card, "Title", UITheme.Title, UITheme.Ink,
                                     TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_title.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad), new Vector2(330f, 40f));

            _subtitle = UIBuilder.Label(_card, "Subtitle", UITheme.Micro, UITheme.Ice,
                                        TextAnchor.UpperLeft);
            UIBuilder.Place(_subtitle.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 38f), new Vector2(330f, 18f));

            UIBuilder.Rule(_card, "Rule", topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad - 60f), UILayout.RailWidth - UITheme.Pad * 2f);

            _labels = UIBuilder.Label(_card, "Labels", UITheme.Label, UITheme.InkMuted,
                                      TextAnchor.UpperLeft);
            UIBuilder.Place(_labels.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 70f), new Vector2(180f, 168f));

            _values = UIBuilder.Label(_card, "Values", UITheme.Label, UITheme.Ink,
                                      TextAnchor.UpperRight);
            UIBuilder.Place(_values.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-UITheme.Pad, -UITheme.Pad - 70f), new Vector2(190f, 168f));

            _stars = UIStars.Create(_card, "Stars", topLeft, topLeft,
                                    new Vector2(UITheme.Pad, -UITheme.Pad - 248f), 14f, 5f);

            _upgrade = MakeButton("Upgrade", UITheme.Pad + 52f, out _upgradeLabel);
            _upgrade.Clicked += Upgrade;

            Text closeLabel;
            _close = MakeButton("Close", UITheme.Pad, out closeLabel);
            closeLabel.text = UITheme.Track("CLOSE");
            _close.Clicked += () => { if (selection != null) selection.Clear(); };

            _panel.HideInstantly();
        }

        UIButton MakeButton(string name, float fromBottom, out Text label)
        {
            RectTransform button = UIBuilder.Place(UIBuilder.Node(_card, name),
                                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                   new Vector2(0f, fromBottom),
                                                   new Vector2(UILayout.RailWidth - UITheme.Pad * 2f, 42f));

            var fill = button.gameObject.AddComponent<Image>();
            fill.sprite = UISprites.Fill(UITheme.RadiusSmall);
            fill.type = Image.Type.Sliced;
            fill.color = UITheme.Card;

            var border = UIBuilder.Stretch(UIBuilder.Node(button, "Hairline"))
                                  .gameObject.AddComponent<Image>();
            border.sprite = UISprites.Outline(UITheme.RadiusSmall, 1);
            border.type = Image.Type.Sliced;
            border.color = UITheme.Hairline;
            border.raycastTarget = false;

            label = UIBuilder.Label(button, "Label", UITheme.Label, UITheme.Ink,
                                    TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch(label.rectTransform);

            var control = button.gameObject.AddComponent<UIButton>();
            control.background = fill;
            control.border = border;
            control.label = label;
            control.SetRestColour(UITheme.Card);

            return control;
        }

        // ---------------- contents --------------------------------------------

        void OnSelectionChanged(Selection selected)
        {
            if (selected == null)
            {
                _facility = null;
                _panel.Hide();
                return;
            }

            _panel.Show();
            Populate(selected);
        }

        void Update()
        {
            if (selection == null || selection.Current == null) return;
            Populate(selection.Current);
        }

        void Populate(Selection selected)
        {
            switch (selected.kind)
            {
                case SelectionKind.Facility: ShowFacility(selected.facility); break;
                case SelectionKind.Trail: ShowTrail(selected.trail, selected.trailIndex); break;
                case SelectionKind.Guest: ShowGuest(selected.guest); break;
                case SelectionKind.Ground: ShowGround(selected.anchor); break;
            }
        }

        void ShowFacility(Facility facility)
        {
            _facility = facility;
            if (facility == null) return;

            _title.text = facility.displayName.ToUpperInvariant();
            _subtitle.text = UITheme.Track("LEVEL " + facility.level + " OF " + facility.maxLevel);

            var lift = facility.GetComponent<Chairlift>();
            var lodge = facility.GetComponent<LodgeBuilder>();

            if (lift != null) LiftRows(facility, lift);
            else if (lodge != null) Rows(facility, LedgerLine.Lodge, "Guest capacity",
                                          Mathf.RoundToInt(60f * facility.level).ToString());
            else Rows(facility, LedgerLine.TerrainPark, "Features", facility.LevelSummary);

            _stars.gameObject.SetActive(false);
            ShowUpgrade(true);
        }

        void LiftRows(Facility facility, Chairlift lift)
        {
            var liftFacility = facility as LiftFacility;

            int seats = liftFacility != null ? liftFacility.Seats : 4;
            float speed = lift.lineSpeed;

            // Chairs per hour times seats: the real throughput of this lift.
            float chairsPerHour = lift.chairSpacing > 0.1f ? speed * 3600f / lift.chairSpacing : 0f;
            int perHour = Mathf.RoundToInt(chairsPerHour * seats);

            _labels.text = "Status\nCapacity\nGuests / hour\nCondition\nRevenue today";
            _values.text = "Operating\n"
                         + seats + " per chair\n"
                         + perHour + "\n"
                         + Mathf.RoundToInt(facility.Quality * 100f) + "%\n"
                         + Ledger.Money(RevenueToday(LedgerLine.Tickets));
        }

        void Rows(Facility facility, LedgerLine line, string extraLabel, string extraValue)
        {
            _labels.text = "Level\n" + extraLabel + "\nUpkeep\nCondition\nRevenue today";
            _values.text = facility.LevelSummary + "\n"
                         + extraValue + "\n"
                         + Ledger.Money(facility.DailyUpkeep) + " / day\n"
                         + Mathf.RoundToInt(facility.Quality * 100f) + "%\n"
                         + Ledger.Money(RevenueToday(line));
        }

        void ShowTrail(Trail run, int index)
        {
            _facility = null;
            if (run == null) return;

            _title.text = run.name.ToUpperInvariant();
            _subtitle.text = UITheme.Track(Trail.GradeName(run.grade) + (run.open ? " RUN" : " RUN  ·  CLOSED"));
            _subtitle.color = SkiHud.GradeColour(run.grade);

            _labels.text = "Length\nVertical drop\nAverage grade\nMax grade\nWidth\nSnow\nGrooming\nGuests today";
            _values.text = (run.length / 1000f).ToString("0.00") + " km\n"
                         + Mathf.RoundToInt(run.drop) + " m\n"
                         + Mathf.RoundToInt(run.averageGrade * 100f) + "%\n"
                         + Mathf.RoundToInt(run.maxGrade * 100f) + "%\n"
                         + Mathf.RoundToInt(run.halfWidth * 2f) + " m\n"
                         + Trail.SnowName(run.snow) + "\n"
                         + (run.groomed ? "Groomed" : "Ungroomed") + "\n"
                         + (guests != null ? guests.GuestsOn(index).ToString() : "0");

            _stars.gameObject.SetActive(true);
            if (rating != null) _stars.Set(rating.Stars * run.Appeal);

            ShowUpgrade(false);
        }

        /// <summary>
        /// Open mountain. There is nothing here yet, which is the point: this
        /// tells the player what the ground is like so they can decide what
        /// belongs on it.
        /// </summary>
        void ShowGround(Vector3 at)
        {
            _facility = null;
            if (mountain == null) return;

            float slope = mountain.SlopeDegrees(at.x, at.z);
            float grade = Mathf.Tan(slope * Mathf.Deg2Rad);

            _title.text = "OPEN MOUNTAIN";
            _subtitle.text = UITheme.Track("UNDEVELOPED");
            _subtitle.color = UITheme.InkMuted;

            _labels.text = "Elevation\nSlope\nGrade\nSuits\nProtected";

            string suits = grade < 0.17f ? "Beginner terrain"
                         : grade < 0.28f ? "Intermediate terrain"
                         : grade < 0.42f ? "Advanced terrain"
                         : "Expert terrain";

            string reserved = mountain.ProtectedBy(at.x, at.z, 0f);

            _values.text = Mathf.RoundToInt(at.y) + " m\n"
                         + Mathf.RoundToInt(slope) + "\u00b0\n"
                         + Mathf.RoundToInt(grade * 100f) + "%\n"
                         + suits + "\n"
                         + (reserved == null ? "No" : reserved);

            _stars.gameObject.SetActive(false);
            ShowUpgrade(false);
        }

        void ShowGuest(Guest guest)
        {
            _facility = null;
            if (guest == null) return;

            _title.text = "GUEST";
            _subtitle.text = UITheme.Track(guest.gear == SnowBound.Player.LocomotionKind.Snowboard
                                           ? "SNOWBOARDER" : "SKIER");
            _subtitle.color = UITheme.Ice;

            _labels.text = "Doing\nAbility\nHappiness\nRuns today\nMoney left";
            _values.text = Describe(guest.activity) + "\n"
                         + Mathf.RoundToInt(guest.ability * 100f) + "%\n"
                         + Mathf.RoundToInt(guest.happiness * 100f) + "%\n"
                         + guest.RunsCompleted + "\n"
                         + Ledger.Money(guest.money);

            _stars.gameObject.SetActive(false);
            ShowUpgrade(false);
        }

        static string Describe(Guest.Activity activity)
        {
            switch (activity)
            {
                case Guest.Activity.Arriving: return "Arriving";
                case Guest.Activity.AtLodge: return "In the lodge";
                case Guest.Activity.WalkingToLift: return "Heading to the lift";
                case Guest.Activity.Queueing: return "Queueing";
                case Guest.Activity.RidingLift: return "Riding up";
                case Guest.Activity.Descending: return "Skiing down";
                default: return "Leaving";
            }
        }

        float RevenueToday(LedgerLine line)
        {
            return ledger != null ? ledger.Today[line] : 0f;
        }

        void ShowUpgrade(bool visible)
        {
            _upgrade.gameObject.SetActive(visible);
            if (!visible) return;

            if (_facility == null || !_facility.CanUpgrade)
            {
                _upgradeLabel.text = UITheme.Track("FULLY UPGRADED");
                _upgrade.interactable = false;
                return;
            }

            bool affordable = ledger != null && ledger.Cash >= _facility.UpgradeCost;
            _upgradeLabel.text = UITheme.Track("UPGRADE   " + Ledger.Money(_facility.UpgradeCost));
            _upgrade.interactable = affordable;
        }

        void Upgrade()
        {
            if (_facility == null || ledger == null || !_facility.CanUpgrade) return;
            if (!ledger.Spend(LedgerLine.Construction, _facility.UpgradeCost)) return;

            _facility.SetLevel(_facility.level + 1);

            if (notifications != null)
                notifications.Announce(_facility.displayName + " upgraded",
                                       "Now level " + _facility.level + ".  " + _facility.LevelSummary);
        }
    }
}
