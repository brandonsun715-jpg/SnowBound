using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Game;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The owner's interface: one strip of figures across the top, a small
    /// chip underneath saying where and when you are, and nothing else. The
    /// tools live in the dock along the bottom and the details live in the
    /// rail on the right, so the middle of the screen — the mountain, which is
    /// the thing being decided about — is never covered.
    ///
    /// Every piece is anchored to the edge it belongs to and sized against the
    /// narrowest window worth supporting, so the bar cannot run off the side
    /// of a square window or drift apart on an ultrawide one.
    /// </summary>
    public class ManagementHud : MonoBehaviour
    {
        public ModeDirector modes;
        public ManagementScreen overview;
        public ToolDock dock;
        public Ledger ledger;
        public ResortClock clock;
        public ResortTraffic traffic;
        public ResortRating rating;
        public GuestDirector guests;
        public ResortIdentity identity;
        public MountainGenerator mountain;

        Canvas _canvas;
        UIPanel _panel;

        Text _cash, _guestCount, _ratingValue, _happiness, _profit;
        Text _place, _clockText, _state;
        UIStars _stars;

        /// <summary>Three lines and their padding. Other panels clear this.</summary>
        public const float ChipHeight = 86f;

        void Start()
        {
            if (modes == null) modes = ModeDirector.Instance;
            if (overview == null) overview = FindAnyObjectByType<ManagementScreen>();
            if (dock == null) dock = FindAnyObjectByType<ToolDock>();
            if (ledger == null) ledger = Ledger.Instance;
            if (clock == null) clock = ResortClock.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (rating == null) rating = ResortRating.Instance;
            if (guests == null) guests = GuestDirector.Instance;
            if (identity == null) identity = ResortIdentity.Instance;
            if (mountain == null) mountain = MountainGenerator.Instance;

            Build();
            _panel.HideInstantly();
        }

        public void SetVisible(bool visible)
        {
            if (_panel == null) return;
            if (visible) _panel.Show(); else _panel.Hide();
        }

        // ---------------- building --------------------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "ManagementHud", 11);

            RectTransform layer = UIBuilder.Stretch(UIBuilder.Node(_canvas.transform, "Layer"));
            layer.gameObject.AddComponent<CanvasGroup>();
            _panel = layer.gameObject.AddComponent<UIPanel>();
            _panel.riseDistance = 10f;

            BuildTopBar(layer);
            BuildPlaceChip(layer);
        }

        void BuildTopBar(Transform layer)
        {
            RectTransform bar = UIBuilder.Glass(layer, "TopBar",
                                                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                new Vector2(0f, -UILayout.Margin),
                                                new Vector2(UILayout.SafeWidth, UILayout.TopBarHeight));
            UIPointer.Block(bar);

            const float stride = 138f;
            float x = UITheme.Pad;

            _cash = Figure(bar, "Cash", "CASH", x, UIIcons.Cash);
            _guestCount = Figure(bar, "Guests", "GUESTS", x + stride, UIIcons.Guests);
            _happiness = Figure(bar, "Happiness", "HAPPINESS", x + stride * 2f, UIIcons.Star);
            _ratingValue = Figure(bar, "Rating", "REPUTATION", x + stride * 3f, UIIcons.Mountain);
            _profit = Figure(bar, "Profit", "TODAY", x + stride * 4f, UIIcons.ArrowUp);

            _stars = UIStars.Create(bar, "Stars", new Vector2(0f, 0f), new Vector2(0f, 0f),
                                    new Vector2(x + stride * 3f, 12f), 9f, 3f);

            // One button on the bar. Everything the owner can open lives in the
            // row of tabs along the bottom, including the resort overview, so
            // there is one place to look rather than two.
            UIButton enter = Chip(bar, "ENTER MOUNTAIN", -UITheme.Pad, 186f);
            enter.SetRestColour(UITheme.CardHover);
            enter.Clicked += () => { if (modes != null) modes.EnterMountain(); };
        }

        void BuildPlaceChip(Transform layer)
        {
            RectTransform chip = UIBuilder.Glass(layer, "PlaceChip",
                                                 new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                 new Vector2(UILayout.Margin, -UILayout.UnderTopBar),
                                                 new Vector2(288f, ChipHeight), UITheme.RadiusSmall);

            // All three lines are placed from the top of the chip. Mixing a
            // top-anchored line with a bottom-anchored one is how they end up
            // printed over each other.
            _place = UIBuilder.Label(chip, "Place", UITheme.Label, UITheme.Ink,
                                     TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_place.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -12f), new Vector2(258f, 20f));

            _clockText = UIBuilder.Label(chip, "Clock", UITheme.Micro, UITheme.InkMuted,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(_clockText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -36f), new Vector2(258f, 16f));

            _state = UIBuilder.Label(chip, "State", UITheme.Micro, UITheme.Ice,
                                     TextAnchor.UpperLeft);
            UIBuilder.Place(_state.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -58f), new Vector2(258f, 16f));
        }

        /// <summary>One figure in the top bar: a caption, an icon and a number.</summary>
        Text Figure(Transform bar, string name, string caption, float x, Sprite icon)
        {
            var topLeft = new Vector2(0f, 1f);

            Text label = UIBuilder.Label(bar, name + "Caption", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(label.rectTransform, topLeft, topLeft,
                            new Vector2(x, -12f), new Vector2(130f, 16f));
            label.text = UITheme.Track(caption);

            UIBuilder.Icon(bar, name + "Icon", icon, UITheme.Ice, topLeft, topLeft,
                           new Vector2(x, -34f), 15f);

            Text value = UIBuilder.Label(bar, name, UITheme.Heading, UITheme.Ink,
                                         TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(value.rectTransform, topLeft, topLeft,
                            new Vector2(x + 22f, -32f), new Vector2(112f, 26f));

            return value;
        }

        UIButton Chip(Transform bar, string text, float x, float width)
        {
            RectTransform rect = UIBuilder.Place(UIBuilder.Node(bar, text),
                                                 new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                                 new Vector2(x, 0f), new Vector2(width, 44f));

            var fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = UISprites.Fill(UITheme.RadiusSmall);
            fill.type = Image.Type.Sliced;
            fill.color = UITheme.Card;

            var border = UIBuilder.Stretch(UIBuilder.Node(rect, "Hairline"))
                                  .gameObject.AddComponent<Image>();
            border.sprite = UISprites.Outline(UITheme.RadiusSmall, 1);
            border.type = Image.Type.Sliced;
            border.color = UITheme.Hairline;
            border.raycastTarget = false;

            Text label = UIBuilder.Label(rect, "Label", UITheme.Micro, UITheme.Ink,
                                         TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch(label.rectTransform, 4f);
            label.text = UITheme.Track(text);

            var button = rect.gameObject.AddComponent<UIButton>();
            button.background = fill;
            button.border = border;
            button.label = label;
            button.SetRestColour(UITheme.Card);

            return button;
        }

        // ---------------- running ----------------------------------------------

        void Update()
        {
            if (_panel == null || !_panel.Visible) return;

            if (ledger != null)
            {
                _cash.text = Ledger.Money(ledger.Cash);

                float profit = ledger.Today.Profit;
                _profit.text = Ledger.Signed(profit);
                _profit.color = profit >= 0f ? UITheme.Positive : UITheme.Negative;
            }

            if (traffic != null) _guestCount.text = traffic.GuestsToday.ToString();
            if (guests != null) _happiness.text = Mathf.RoundToInt(guests.Happiness * 100f) + "%";

            if (rating != null)
            {
                _ratingValue.text = rating.Stars.ToString("0.0");
                _stars.Set(rating.Stars);
            }

            if (identity != null) _place.text = identity.resortName.ToUpperInvariant();

            if (clock != null)
                _clockText.text = UITheme.Track("DAY " + clock.Day + "     " + clock.TimeText);

            _state.text = UITheme.Track(StateLine());
        }

        /// <summary>
        /// What the resort still needs. On a bare mountain this is the whole
        /// tutorial: it names the next thing worth doing and then gets out of
        /// the way once there is nothing obvious left.
        /// </summary>
        float _liftCheckedAt = -1f;
        bool _hasLift;

        string StateLine()
        {
            if (mountain == null) return "MANAGEMENT MODE";

            if (mountain.TrailCount == 0) return "NO RUNS YET  ·  CUT ONE";

            // Searching the scene every frame for a lift would be silly; twice
            // a second is far quicker than anyone can build one.
            if (Time.time - _liftCheckedAt > 0.5f)
            {
                _liftCheckedAt = Time.time;
                _hasLift = FindAnyObjectByType<SnowBound.Lifts.Chairlift>() != null;
            }

            if (!_hasLift) return "NO LIFT YET  ·  BUY ONE";

            return "MANAGEMENT MODE";
        }
    }
}
