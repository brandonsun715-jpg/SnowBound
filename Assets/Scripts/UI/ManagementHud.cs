using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Game;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The owner's interface: a bar of figures across the top, categories
    /// along the bottom, and the mountain filling everything in between.
    ///
    /// It is a layer over the resort, not a window instead of it, so the
    /// centre of the screen stays empty and the world keeps running behind
    /// every panel.
    /// </summary>
    public class ManagementHud : MonoBehaviour
    {
        public ModeDirector modes;
        public ManagementScreen overview;
        public BuildPanel build;
        public Ledger ledger;
        public ResortClock clock;
        public ResortTraffic traffic;
        public ResortRating rating;
        public GuestDirector guests;
        public ResortIdentity identity;

        Canvas _canvas;
        UIPanel _panel;

        Text _cash, _guestCount, _ratingValue, _happiness, _profit, _clockText;
        UIStars _stars;
        Text _note;
        UIButton _enter;

        readonly List<UIButton> _categories = new List<UIButton>();

        void Start()
        {
            if (modes == null) modes = ModeDirector.Instance;
            if (overview == null) overview = FindAnyObjectByType<ManagementScreen>();
            if (build == null) build = FindAnyObjectByType<BuildPanel>();
            if (ledger == null) ledger = Ledger.Instance;
            if (clock == null) clock = ResortClock.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (rating == null) rating = ResortRating.Instance;
            if (guests == null) guests = GuestDirector.Instance;
            if (identity == null) identity = ResortIdentity.Instance;

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
            _canvas = UIBuilder.Canvas(transform, "ManagementHud", 8);

            RectTransform layer = UIBuilder.Stretch(UIBuilder.Node(_canvas.transform, "Layer"));
            layer.gameObject.AddComponent<CanvasGroup>();
            _panel = layer.gameObject.AddComponent<UIPanel>();
            _panel.riseDistance = 10f;

            BuildTopBar(layer);
            BuildModeChip(layer);
            BuildCategories(layer);
            BuildEnterButton(layer);
        }

        void BuildTopBar(Transform layer)
        {
            RectTransform bar = UIBuilder.Glass(layer, "TopBar", new Vector2(0.5f, 1f),
                                                new Vector2(0.5f, 1f),
                                                new Vector2(0f, -22f),
                                                new Vector2(1240f, 96f));
            UIPointer.Block(bar);

            _cash = Stat(bar, "Cash", UIIcons.Cash, "CASH", -480f);
            _guestCount = Stat(bar, "Guests", UIIcons.Guests, "GUESTS TODAY", -240f);
            _happiness = Stat(bar, "Happiness", UIIcons.Lodge, "HAPPINESS", 0f);
            _ratingValue = Stat(bar, "Rating", UIIcons.Star, "RATING", 240f);
            _profit = Stat(bar, "Profit", UIIcons.ArrowUp, "PROFIT TODAY", 480f);

            _stars = UIStars.Create(bar, "Stars", new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                                    new Vector2(240f - 56f, -72f), 12f, 3f);

            _clockText = UIBuilder.Label(bar, "Clock", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.LowerCenter);
            UIBuilder.Place(_clockText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -6f), new Vector2(400f, 20f));
        }

        Text Stat(Transform bar, string name, Sprite icon, string caption, float x)
        {
            UIBuilder.Icon(bar, name + "Icon", icon, UITheme.InkFaint,
                           new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                           new Vector2(x - 56f, -24f), 16f);

            Text label = UIBuilder.Label(bar, name + "Caption", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                            new Vector2(x - 44f, -18f), new Vector2(220f, 18f));
            label.text = UITheme.Track(caption);

            Text value = UIBuilder.Label(bar, name + "Value", UITheme.Title, UITheme.Ink,
                                         TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                            new Vector2(x - 56f, -40f), new Vector2(230f, 40f));

            return value;
        }

        void BuildModeChip(Transform layer)
        {
            RectTransform chip = UIBuilder.Glass(layer, "ModeChip", new Vector2(0f, 1f),
                                                 new Vector2(0f, 1f),
                                                 new Vector2(UITheme.Margin, -UITheme.Margin),
                                                 new Vector2(230f, 40f), UITheme.RadiusSmall);

            Text label = UIBuilder.Label(chip, "Label", UITheme.Micro, UITheme.Ice,
                                         TextAnchor.MiddleCenter);
            UIBuilder.Stretch(label.rectTransform);
            label.text = UITheme.Track("MANAGEMENT MODE", 1);
        }

        void BuildCategories(Transform layer)
        {
            string[] names = { "BUILD", "LIFTS", "TERRAIN", "FACILITIES", "UPGRADES", "MAP" };

            RectTransform bar = UIBuilder.Glass(layer, "Categories", new Vector2(0f, 0f),
                                                new Vector2(0f, 0f),
                                                new Vector2(UITheme.Margin, UITheme.Margin),
                                                new Vector2(names.Length * 132f + 16f, 74f));
            UIPointer.Block(bar);

            for (int i = 0; i < names.Length; i++)
            {
                string category = names[i];
                UIButton button = Category(bar, category, 8f + i * 132f);
                button.Clicked += () => Choose(category);
                _categories.Add(button);
            }

            _note = UIBuilder.Label(layer, "Note", UITheme.Micro, UITheme.InkFaint,
                                    TextAnchor.LowerLeft);
            UIBuilder.Place(_note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(UITheme.Margin + 8f, UITheme.Margin + 82f),
                            new Vector2(700f, 20f));
        }

        UIButton Category(Transform bar, string name, float x)
        {
            RectTransform button = UIBuilder.Place(UIBuilder.Node(bar, name),
                                                   new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                                   new Vector2(x, 0f), new Vector2(124f, 56f));

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

            UIBuilder.Icon(button, "Icon", IconFor(name), UITheme.InkMuted,
                           new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), 18f);

            Text label = UIBuilder.Label(button, "Label", UITheme.Micro, UITheme.InkMuted,
                                         TextAnchor.LowerCenter);
            UIBuilder.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 8f), new Vector2(124f, 18f));
            label.text = UITheme.Track(name);

            var control = button.gameObject.AddComponent<UIButton>();
            control.background = fill;
            control.border = border;
            control.label = label;
            control.SetRestColour(UITheme.Card);

            return control;
        }

        static Sprite IconFor(string category)
        {
            switch (category)
            {
                case "BUILD": return UIIcons.Lodge;
                case "LIFTS": return UIIcons.Lift;
                case "TERRAIN": return UIIcons.Mountain;
                case "FACILITIES": return UIIcons.Lodge;
                case "UPGRADES": return UIIcons.ArrowUp;
                default: return UIIcons.Mountain;
            }
        }

        void BuildEnterButton(Transform layer)
        {
            RectTransform button = UIBuilder.Place(UIBuilder.Node(layer, "EnterMountain"),
                                                   new Vector2(1f, 0f), new Vector2(1f, 0f),
                                                   new Vector2(-UITheme.Margin, UITheme.Margin),
                                                   new Vector2(300f, 74f));
            UIPointer.Block(button);

            var fill = button.gameObject.AddComponent<Image>();
            fill.sprite = UISprites.Fill(UITheme.Radius);
            fill.type = Image.Type.Sliced;
            fill.color = UITheme.CardHover;

            var border = UIBuilder.Stretch(UIBuilder.Node(button, "Hairline"))
                                  .gameObject.AddComponent<Image>();
            border.sprite = UISprites.Outline(UITheme.Radius, 1);
            border.type = Image.Type.Sliced;
            border.color = UITheme.HairlineBright;
            border.raycastTarget = false;

            UIBuilder.Icon(button, "Icon", UIIcons.Mountain, UITheme.Ice,
                           new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), 24f);

            Text label = UIBuilder.Label(button, "Label", UITheme.Label, UITheme.Ink,
                                         TextAnchor.MiddleLeft, FontStyle.Bold);
            UIBuilder.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(58f, 4f), new Vector2(230f, 22f));
            label.text = UITheme.Track("ENTER MOUNTAIN");

            Text hint = UIBuilder.Label(button, "Hint", UITheme.Micro, UITheme.InkFaint,
                                        TextAnchor.MiddleLeft);
            UIBuilder.Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(58f, -16f), new Vector2(230f, 18f));
            hint.text = UITheme.Track("SKI YOUR OWN RESORT");

            _enter = button.gameObject.AddComponent<UIButton>();
            _enter.background = fill;
            _enter.border = border;
            _enter.label = label;
            _enter.SetRestColour(UITheme.CardHover);
            _enter.Clicked += () => { if (modes != null) modes.EnterMountain(); };
        }

        // ---------------- running ----------------------------------------------

        /// <summary>
        /// One screen at a time. Opening either of the two big panels closes
        /// the other, so the mountain is never buried under both at once.
        /// </summary>
        void Choose(string category)
        {
            if (category == "BUILD" || category == "TERRAIN")
            {
                if (overview != null) overview.Close();
                _note.text = string.Empty;

                if (build == null) { _note.text = UITheme.Track("BUILD MENU IS MISSING"); return; }

                if (build.IsOpen) build.Close(); else build.Open();
                return;
            }

            if (category == "FACILITIES" || category == "UPGRADES" || category == "LIFTS")
            {
                if (build != null) build.Close();
                _note.text = string.Empty;

                if (overview == null) return;

                if (overview.IsOpen) overview.Close(); else overview.Open();
                return;
            }

            if (overview != null) overview.Close();
            if (build != null) build.Close();

            _note.text = UITheme.Track(category + " ARRIVES IN A LATER UPDATE");
        }

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

            if (clock != null)
            {
                string place = identity != null ? identity.resortName.ToUpperInvariant() : "SNOWBOUND";
                _clockText.text = UITheme.Track(place + "     DAY " + clock.Day + "     " + clock.TimeText);
            }
        }
    }
}
