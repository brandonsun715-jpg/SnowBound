using UnityEngine;
using UnityEngine.UI;
using SnowBound.Player;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The day's figures, shown once the lifts stop.
    ///
    /// Two columns rather than padded text: a proportional font will never
    /// line a table up on spaces alone, and a table that does not line up is
    /// the single fastest way to make an interface look unfinished.
    /// </summary>
    public class DaySummary : MonoBehaviour
    {
        public ResortClock clock;
        public Ledger ledger;
        public ResortTraffic traffic;
        public PlayerController player;

        Canvas _canvas;
        UIPanel _panel;
        Text _title, _labels, _values, _profit, _cash, _footer;

        bool _open;
        bool _inputWasEnabled = true;

        public bool IsOpen { get { return _open; } }

        void Start()
        {
            if (clock == null) clock = ResortClock.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            Build();

            if (clock != null) clock.DayEnded += OnDayEnded;
        }

        void OnDestroy()
        {
            if (clock != null) clock.DayEnded -= OnDayEnded;
            if (_open) Time.timeScale = 1f;
        }

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "DaySummary", 30);

            RectTransform layer = UIBuilder.Stretch(UIBuilder.Node(_canvas.transform, "Layer"));
            layer.gameObject.AddComponent<CanvasGroup>();
            _panel = layer.gameObject.AddComponent<UIPanel>();

            var scrim = layer.gameObject.AddComponent<Image>();
            scrim.sprite = UISprites.Pixel;
            scrim.color = new Color(0.02f, 0.03f, 0.05f, 0.74f);
            scrim.raycastTarget = false;

            RectTransform card = UIBuilder.Glass(layer, "Card", new Vector2(0.5f, 0.5f),
                                                 new Vector2(0.5f, 0.5f), Vector2.zero,
                                                 new Vector2(640f, 540f));

            var topLeft = new Vector2(0f, 1f);

            Text caption = UIBuilder.Label(card, "Caption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 8f, -UITheme.Pad - 4f), new Vector2(400f, 18f));
            caption.text = UITheme.Track("END OF DAY", 2);

            _title = UIBuilder.Label(card, "Title", UITheme.Title, UITheme.Ink,
                                     TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_title.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 6f, -UITheme.Pad - 22f), new Vector2(500f, 42f));

            UIBuilder.Rule(card, "Rule", topLeft, topLeft,
                           new Vector2(UITheme.Pad + 6f, -UITheme.Pad - 74f), 640f - UITheme.Pad * 2f - 12f);

            _labels = UIBuilder.Label(card, "Labels", UITheme.Body, UITheme.InkMuted,
                                      TextAnchor.UpperLeft);
            UIBuilder.Place(_labels.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 6f, -UITheme.Pad - 96f), new Vector2(320f, 200f));

            _values = UIBuilder.Label(card, "Values", UITheme.Body, UITheme.Ink,
                                      TextAnchor.UpperRight);
            UIBuilder.Place(_values.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-UITheme.Pad - 6f, -UITheme.Pad - 96f), new Vector2(320f, 200f));

            UIBuilder.Rule(card, "Rule2", topLeft, topLeft,
                           new Vector2(UITheme.Pad + 6f, -UITheme.Pad - 236f), 640f - UITheme.Pad * 2f - 12f);

            Text profitCaption = UIBuilder.Label(card, "ProfitCaption", UITheme.Micro,
                                                 UITheme.InkFaint, TextAnchor.UpperLeft);
            UIBuilder.Place(profitCaption.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 6f, -UITheme.Pad - 258f), new Vector2(300f, 18f));
            profitCaption.text = UITheme.Track("PROFIT");

            _profit = UIBuilder.Label(card, "Profit", UITheme.Display, UITheme.Positive,
                                      TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_profit.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 2f, -UITheme.Pad - 278f), new Vector2(500f, 84f));

            Text cashCaption = UIBuilder.Label(card, "CashCaption", UITheme.Micro,
                                               UITheme.InkFaint, TextAnchor.UpperRight);
            UIBuilder.Place(cashCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-UITheme.Pad - 6f, -UITheme.Pad - 258f), new Vector2(300f, 18f));
            cashCaption.text = UITheme.Track("CASH");

            _cash = UIBuilder.Label(card, "Cash", UITheme.Title, UITheme.Ink,
                                    TextAnchor.UpperRight, FontStyle.Bold);
            UIBuilder.Place(_cash.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-UITheme.Pad - 6f, -UITheme.Pad - 280f), new Vector2(400f, 44f));

            _footer = UIBuilder.Label(card, "Footer", UITheme.Label, UITheme.InkFaint,
                                      TextAnchor.LowerCenter);
            UIBuilder.Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, UITheme.Pad + 4f), new Vector2(560f, 24f));

            _panel.HideInstantly();
        }

        void OnDayEnded(int day)
        {
            if (ledger == null) return;

            DayRecord record = ledger.Today;

            _title.text = "DAY " + day + " COMPLETE";

            _labels.text = "Lift tickets\nLodge\nTerrain park\nRentals\nOperating costs";
            _values.text = Ledger.Signed(record[LedgerLine.Tickets]) + "\n"
                         + Ledger.Signed(record[LedgerLine.Lodge]) + "\n"
                         + Ledger.Signed(record[LedgerLine.TerrainPark]) + "\n"
                         + Ledger.Signed(record[LedgerLine.Rentals]) + "\n"
                         + Ledger.Signed(record[LedgerLine.Maintenance]);

            float profit = record.Profit;
            _profit.text = Ledger.Signed(profit);
            _profit.color = profit >= 0f ? UITheme.Positive : UITheme.Negative;

            _cash.text = Ledger.Money(ledger.Cash);
            _footer.text = UITheme.Track(
                (traffic != null ? traffic.GuestsToday + " GUESTS        " : "") + "SPACE  OPEN TOMORROW");

            _panel.Show();
            _open = true;

            Time.timeScale = 0f;
            if (player != null && player.Input != null)
            {
                _inputWasEnabled = player.Input.enableInput;
                player.Input.enableInput = false;
            }
        }

        void Update()
        {
            if (!_open || player == null || player.Input == null) return;
            if (!player.Input.ContinuePressed) return;

            _open = false;
            _panel.Hide();

            Time.timeScale = 1f;
            player.Input.enableInput = _inputWasEnabled;

            if (ledger != null && clock != null) ledger.CloseDay(clock.Day);
            if (clock != null) clock.StartNextDay();
        }
    }
}
