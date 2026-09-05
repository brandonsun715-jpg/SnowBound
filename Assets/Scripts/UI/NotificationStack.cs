using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// Small glass cards that slide in, say one thing, and leave.
    ///
    /// Takings are batched before they are shown. A resort makes a sale every
    /// couple of seconds, and a card for each one would be a strobe light
    /// rather than information; one card saying what the last few seconds
    /// earned is the same fact, legible.
    ///
    /// They live down the right-hand edge, clear of the middle of the screen
    /// and clear of the speed readout.
    /// </summary>
    public class NotificationStack : MonoBehaviour
    {
        public Ledger ledger;

        [Header("Behaviour")]
        [Tooltip("Seconds of takings gathered into one card.")]
        public float batchSeconds = 6f;
        public float cardSeconds = 4.2f;
        public int maxCards = 4;

        class Card
        {
            public RectTransform rect;
            public UIPanel panel;
            public float life;
        }

        readonly List<Card> _cards = new List<Card>();
        readonly float[] _pending = new float[System.Enum.GetValues(typeof(LedgerLine)).Length];
        float _batchLeft;

        Transform _root;

        UIPanel _announcePanel;
        Text _announceHeadline, _announceSub;
        float _announceLeft;

        void Start()
        {
            if (ledger == null) ledger = Ledger.Instance;

            Canvas canvas = UIBuilder.Canvas(transform, "Notifications", 20);
            _root = canvas.transform;

            BuildAnnouncement(_root);

            if (ledger != null) ledger.Booked += OnBooked;
            _batchLeft = batchSeconds;
        }

        void OnDestroy()
        {
            if (ledger != null) ledger.Booked -= OnBooked;
        }

        void OnBooked(LedgerLine line, float amount)
        {
            _pending[(int)line] += amount;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _batchLeft -= dt;
            if (_batchLeft <= 0f)
            {
                _batchLeft = batchSeconds;
                FlushBatch();
            }

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                Card card = _cards[i];
                card.life -= dt;

                if (card.life <= 0f && card.panel.Visible) card.panel.Hide();
                if (card.life > -1.5f) continue;

                if (card.rect != null) Destroy(card.rect.gameObject);
                _cards.RemoveAt(i);
            }

            Restack();

            if (_announceLeft <= 0f) return;

            _announceLeft -= dt;
            if (_announceLeft <= 0f) _announcePanel.Hide();
        }

        void FlushBatch()
        {
            for (int i = 0; i < _pending.Length; i++)
            {
                // A rounding error is not news.
                if (Mathf.Abs(_pending[i]) < 1f) { _pending[i] = 0f; continue; }

                Push((LedgerLine)i, _pending[i]);
                _pending[i] = 0f;
            }
        }

        // ---------------- cards --------------------------------------------

        void Push(LedgerLine line, float amount)
        {
            bool positive = amount > 0f;

            RectTransform card = UIBuilder.Glass(_root, "Note", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                                 new Vector2(-UITheme.Margin, -200f),
                                                 new Vector2(300f, 62f), UITheme.RadiusSmall,
                                                 UITheme.Card);

            var group = card.gameObject.AddComponent<CanvasGroup>();
            var panel = card.gameObject.AddComponent<UIPanel>();
            panel.riseDistance = 0f;

            UIBuilder.Icon(card, "Icon", IconFor(line),
                           positive ? UITheme.Positive : UITheme.Warning,
                           new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                           new Vector2(UITheme.Gap + 4f, 0f), 22f);

            Text value = UIBuilder.Label(card, "Value", UITheme.Body,
                                         positive ? UITheme.Positive : UITheme.Warning,
                                         TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(44f, -11f), new Vector2(240f, 24f));
            value.text = Ledger.Signed(amount);

            Text caption = UIBuilder.Label(card, "Caption", UITheme.Micro, UITheme.InkMuted,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(44f, -34f), new Vector2(240f, 18f));
            caption.text = UITheme.Track(Describe(line));

            group.alpha = 0f;
            panel.Show();

            _cards.Add(new Card { rect = card, panel = panel, life = cardSeconds });

            while (_cards.Count > maxCards)
            {
                Card oldest = _cards[0];
                if (oldest.life > 0f) oldest.life = 0f;
                break;
            }
        }

        /// <summary>Slide the stack up as cards below it expire.</summary>
        void Restack()
        {
            float y = -200f;

            for (int i = 0; i < _cards.Count; i++)
            {
                RectTransform rect = _cards[i].rect;
                if (rect == null) continue;

                Vector2 target = new Vector2(-UITheme.Margin, y);
                Vector2 current = rect.anchoredPosition;
                rect.anchoredPosition = Vector2.Lerp(current, target,
                    1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

                _cards[i].panel.MarkRestPosition();
                y -= 74f;
            }
        }

        static Sprite IconFor(LedgerLine line)
        {
            switch (line)
            {
                case LedgerLine.Tickets: return UIIcons.Lift;
                case LedgerLine.Lodge: return UIIcons.Lodge;
                case LedgerLine.TerrainPark: return UIIcons.Park;
                case LedgerLine.Rentals: return UIIcons.Guests;
                case LedgerLine.Construction: return UIIcons.ArrowUp;
                default: return UIIcons.Clock;
            }
        }

        static string Describe(LedgerLine line)
        {
            switch (line)
            {
                case LedgerLine.Tickets: return "LIFT TICKETS";
                case LedgerLine.Lodge: return "LODGE";
                case LedgerLine.TerrainPark: return "TERRAIN PARK";
                case LedgerLine.Rentals: return "RENTALS";
                case LedgerLine.Construction: return "CONSTRUCTION";
                default: return "OPERATING COSTS";
            }
        }

        // ---------------- the big ones -------------------------------------

        void BuildAnnouncement(Transform root)
        {
            RectTransform card = UIBuilder.Place(UIBuilder.Node(root, "Announcement"),
                                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                 new Vector2(0f, 210f), new Vector2(760f, 120f));
            card.gameObject.AddComponent<CanvasGroup>();
            _announcePanel = card.gameObject.AddComponent<UIPanel>();
            _announcePanel.riseDistance = 20f;

            _announceHeadline = UIBuilder.Label(card, "Headline", UITheme.Title, UITheme.Ice,
                                                TextAnchor.UpperCenter, FontStyle.Bold);
            UIBuilder.Place(_announceHeadline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            Vector2.zero, new Vector2(760f, 40f));

            UIBuilder.Rule(card, "Rule", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                           new Vector2(0f, -50f), 160f);

            _announceSub = UIBuilder.Label(card, "Sub", UITheme.Body, UITheme.Ink,
                                           TextAnchor.UpperCenter);
            UIBuilder.Place(_announceSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -66f), new Vector2(760f, 28f));

            _announcePanel.HideInstantly();
        }

        /// <summary>A moment worth stopping for. Used sparingly on purpose.</summary>
        public void Announce(string headline, string detail, float seconds = 3.8f)
        {
            if (_announceHeadline == null) return;

            _announceHeadline.text = UITheme.Track(headline.ToUpperInvariant(), 2);
            _announceSub.text = detail;
            _announceLeft = seconds;
            _announcePanel.Show();
        }
    }
}
