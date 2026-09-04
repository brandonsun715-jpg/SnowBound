using UnityEngine;
using UnityEngine.UI;
using SnowBound.Player;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The tycoon side of the screen: the day, the time, the cash, and the
    /// end of day figures.
    ///
    /// The cash number is eased towards its real value rather than snapped,
    /// because a number that slides is a number you notice moving. The
    /// summary is not calculated here — it is the day's own ledger record
    /// read back, so what the player sees and what the books say cannot
    /// disagree.
    /// </summary>
    public class ResortHud : MonoBehaviour
    {
        public ResortClock clock;
        public Ledger ledger;
        public ResortTraffic traffic;
        public PlayerController player;

        [Header("Feel")]
        [Tooltip("How quickly the displayed cash catches up with the real figure.")]
        public float cashEase = 6f;
        public float tickerSeconds = 2.5f;

        Text _statusLine;
        Text _ticker;

        GameObject _summary;
        Text _summaryTitle;
        Text _summaryLabels;
        Text _summaryValues;
        Text _summaryFooter;

        float _shownCash;
        float _tickerLeft;
        bool _waitingForPlayer;
        bool _inputWasEnabled = true;

        void Start()
        {
            if (clock == null) clock = ResortClock.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            _shownCash = ledger != null ? ledger.Cash : 0f;

            Build();

            if (clock != null) clock.DayEnded += OnDayEnded;
            if (ledger != null) ledger.Booked += OnBooked;
        }

        void OnDestroy()
        {
            if (clock != null) clock.DayEnded -= OnDayEnded;
            if (ledger != null) ledger.Booked -= OnBooked;

            // timeScale survives leaving play mode, so a summary left open
            // when the player hits stop would freeze the next session.
            if (_waitingForPlayer) Time.timeScale = 1f;
        }

        // ---------------- building ---------------------------------------

        void Build()
        {
            Canvas canvas = HudFactory.Canvas(transform, "GeneratedResortHud", 10);
            Transform root = canvas.transform;

            HudFactory.Panel(root, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                             new Vector2(0f, -18f), new Vector2(560f, 62f),
                             new Color(0.05f, 0.07f, 0.11f, 0.72f));

            _statusLine = HudFactory.Label(root, "Status", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                           new Vector2(0f, -32f), new Vector2(560f, 44f),
                                           TextAnchor.UpperCenter, 32);

            _ticker = HudFactory.Label(root, "Ticker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                       new Vector2(0f, -88f), new Vector2(560f, 34f),
                                       TextAnchor.UpperCenter, 24);
            _ticker.color = new Color(0.62f, 0.92f, 0.66f);

            BuildSummary(root);
        }

        void BuildSummary(Transform root)
        {
            var panel = HudFactory.Panel(root, "DaySummary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         Vector2.zero, new Vector2(660f, 460f),
                                         new Color(0.05f, 0.07f, 0.11f, 0.94f));
            _summary = panel.gameObject;

            Transform card = panel.transform;

            _summaryTitle = HudFactory.Label(card, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                             new Vector2(0f, -34f), new Vector2(600f, 56f),
                                             TextAnchor.UpperCenter, 40);

            // Two columns rather than padded text: a proportional font will
            // never line a table up on spaces alone.
            _summaryLabels = HudFactory.Label(card, "Labels", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                              new Vector2(56f, -116f), new Vector2(320f, 300f),
                                              TextAnchor.UpperLeft, 28);

            _summaryValues = HudFactory.Label(card, "Values", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                              new Vector2(-56f, -116f), new Vector2(320f, 300f),
                                              TextAnchor.UpperRight, 28);

            _summaryFooter = HudFactory.Label(card, "Footer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                              new Vector2(0f, 28f), new Vector2(600f, 36f),
                                              TextAnchor.LowerCenter, 22);
            _summaryFooter.color = new Color(0.72f, 0.76f, 0.84f);

            _summary.SetActive(false);
        }

        // ---------------- running ----------------------------------------

        void Update()
        {
            if (ledger == null || _statusLine == null) return;

            // Unscaled, because the summary stops the clock.
            float dt = Time.unscaledDeltaTime;

            _shownCash = Mathf.Lerp(_shownCash, ledger.Cash, 1f - Mathf.Exp(-cashEase * dt));
            if (Mathf.Abs(_shownCash - ledger.Cash) < 1f) _shownCash = ledger.Cash;

            _statusLine.text = StatusText();

            if (_tickerLeft > 0f)
            {
                _tickerLeft -= dt;
                if (_tickerLeft <= 0f) _ticker.text = string.Empty;
            }

            if (_waitingForPlayer && player != null && player.Input != null &&
                player.Input.ContinuePressed)
            {
                StartTomorrow();
            }
        }

        string StatusText()
        {
            string day = clock != null ? "Day " + clock.Day : "Day 1";
            string time = clock != null ? clock.TimeText : "--:--";
            string guests = traffic != null ? traffic.GuestsToday + " guests" : string.Empty;

            return day + "   ·   " + time + "   ·   " + Ledger.Money(_shownCash) +
                   (string.IsNullOrEmpty(guests) ? "" : "   ·   " + guests);
        }

        void OnBooked(LedgerLine line, float amount)
        {
            // Upkeep drips in by the fraction of a penny; showing it would be
            // a strobe light rather than information.
            if (Mathf.Abs(amount) < 1f) return;

            _ticker.color = amount > 0f
                ? new Color(0.62f, 0.92f, 0.66f)
                : new Color(0.94f, 0.60f, 0.55f);

            _ticker.text = Ledger.Signed(amount) + "  " + Describe(line);
            _tickerLeft = tickerSeconds;
        }

        static string Describe(LedgerLine line)
        {
            switch (line)
            {
                case LedgerLine.Tickets: return "lift ticket";
                case LedgerLine.Lodge: return "lodge";
                case LedgerLine.TerrainPark: return "terrain park";
                case LedgerLine.Rentals: return "rental";
                case LedgerLine.Maintenance: return "maintenance";
                default: return "construction";
            }
        }

        // ---------------- end of day --------------------------------------

        void OnDayEnded(int day)
        {
            if (ledger == null) return;

            DayRecord record = ledger.Today;

            _summaryTitle.text = "DAY " + day + " COMPLETE";

            _summaryLabels.text = string.Join("\n", new[]
            {
                "Tickets", "Lodge", "Terrain Park", "Rentals", "Maintenance",
                "", "PROFIT", "", "CASH"
            });

            _summaryValues.text = string.Join("\n", new[]
            {
                Ledger.Signed(record[LedgerLine.Tickets]),
                Ledger.Signed(record[LedgerLine.Lodge]),
                Ledger.Signed(record[LedgerLine.TerrainPark]),
                Ledger.Signed(record[LedgerLine.Rentals]),
                Ledger.Signed(record[LedgerLine.Maintenance]),
                "",
                Ledger.Signed(record.Profit),
                "",
                Ledger.Money(ledger.Cash)
            });

            _summaryFooter.text = (traffic != null ? traffic.GuestsToday + " guests today          " : "")
                                + "Press Space to open tomorrow";

            _summary.SetActive(true);
            _waitingForPlayer = true;

            // Freeze the mountain while the books are open.
            Time.timeScale = 0f;
            if (player != null && player.Input != null)
            {
                _inputWasEnabled = player.Input.enableInput;
                player.Input.enableInput = false;
            }
        }

        void StartTomorrow()
        {
            _waitingForPlayer = false;
            _summary.SetActive(false);

            Time.timeScale = 1f;
            if (player != null && player.Input != null) player.Input.enableInput = _inputWasEnabled;

            if (ledger != null && clock != null) ledger.CloseDay(clock.Day);
            if (clock != null) clock.StartNextDay();
        }
    }
}
