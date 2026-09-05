using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Buildings;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The resort dashboard: a management layer floating over the living
    /// mountain rather than a window that replaces it. The scrim is light
    /// enough to keep the resort visible behind, because what the player is
    /// deciding about is right there.
    ///
    /// A facility card knows nothing about chairlifts. It reads a Facility's
    /// name, level, quality, running cost and upgrade price, which is why
    /// every building added later gets a card for free.
    /// </summary>
    public class ManagementScreen : MonoBehaviour
    {
        public Ledger ledger;
        public ResortClock clock;
        public ResortTraffic traffic;
        public ResortRating rating;
        public ResortIdentity identity;
        public NotificationStack notifications;

        Canvas _canvas;
        UIPanel _panel;

        UIStars _ratingStars;
        Text _ratingValue;
        readonly List<Text> _factorValues = new List<Text>();
        readonly List<Image> _factorBars = new List<Image>();

        class Card
        {
            public RectTransform root;
            public Facility facility;
            public Text level;
            public Text summary;
            public Text upkeep;
            public Image qualityBar;
            public UIButton button;
            public Text buttonLabel;
        }

        readonly List<Card> _cards = new List<Card>();

        RectTransform _cardHost;
        Text _pageLabel;
        UIButton _older, _newer;
        int _page;

        const int PerPage = 3;
        const float CardWidth = 336f;
        const float CardStride = 356f;
        const float CardLeft = 510f;

        void Start()
        {
            if (ledger == null) ledger = Ledger.Instance;
            if (clock == null) clock = ResortClock.Instance;
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (rating == null) rating = ResortRating.Instance;
            if (identity == null) identity = ResortIdentity.Instance;
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();

            Build();
            _panel.HideInstantly();
        }

        /// <summary>Open, or still fading out. The panel deactivates itself when done.</summary>
        public bool IsOpen { get { return _panel != null && _panel.Visible; } }

        public void Open()
        {
            if (_panel == null) return;

            // Something may have been built since this was last opened.
            if (FacilityCount() != _cards.Count) Rebuild();

            Refresh();
            _panel.Show();
        }

        public void Close()
        {
            if (_panel == null || !_panel.Visible) return;
            _panel.Hide();
        }

        // ---------------- building ------------------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "ManagementScreen", 10);

            RectTransform layer = UIBuilder.Stretch(UIBuilder.Node(_canvas.transform, "Layer"));
            layer.gameObject.AddComponent<CanvasGroup>();
            _panel = layer.gameObject.AddComponent<UIPanel>();
            _panel.riseDistance = 18f;

            // Barely there. The management HUD is always up now, so this is
            // an overlay on a working screen rather than a screen of its own.
            var scrim = layer.gameObject.AddComponent<Image>();
            scrim.sprite = UISprites.Pixel;
            scrim.color = new Color(0.02f, 0.03f, 0.05f, 0.42f);
            scrim.raycastTarget = false;

            BuildHeading(layer);
            BuildRatingPanel(layer);
            BuildFacilityHeader(layer);

            // The cards themselves live in their own node so the whole set can
            // be thrown away and rebuilt when the player puts something up.
            _cardHost = UIBuilder.Stretch(UIBuilder.Node(layer, "Facilities"));
            BuildFacilityCards(_cardHost);

            BuildFooter(layer);
        }

        void BuildHeading(Transform layer)
        {
            Text brand = UIBuilder.Label(layer, "Brand", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(brand.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(UITheme.Margin + 20f, -140f), new Vector2(600f, 18f));
            brand.text = UITheme.Track(identity != null
                ? identity.resortName.ToUpperInvariant() : "SNOWBOUND", 2);

            Text heading = UIBuilder.Label(layer, "Heading", UITheme.Hero, UITheme.Ink,
                                           TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(UITheme.Margin + 18f, -162f), new Vector2(700f, 56f));
            heading.text = "RESORT OVERVIEW";
        }

        void BuildRatingPanel(Transform layer)
        {
            RectTransform panel = UIBuilder.Glass(layer, "RatingPanel", new Vector2(0f, 1f),
                                                  new Vector2(0f, 1f),
                                                  new Vector2(UITheme.Margin, -238f),
                                                  new Vector2(430f, 460f));

            var topLeft = new Vector2(0f, 1f);

            Text caption = UIBuilder.Label(panel, "Caption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad), new Vector2(380f, 18f));
            caption.text = UITheme.Track("RESORT RATING");

            _ratingValue = UIBuilder.Label(panel, "Value", UITheme.Hero, UITheme.Ink,
                                           TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_ratingValue.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 20f), new Vector2(380f, 56f));

            _ratingStars = UIStars.Create(panel, "Stars", topLeft, topLeft,
                                          new Vector2(UITheme.Pad + 130f, -UITheme.Pad - 46f), 18f, 6f);

            UIBuilder.Rule(panel, "Rule", topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad - 84f), 430f - UITheme.Pad * 2f);

            if (rating == null) return;

            for (int i = 0; i < rating.Factors.Count; i++)
            {
                float y = -UITheme.Pad - 104f - i * 41f;

                Text name = UIBuilder.Label(panel, "Factor" + i, UITheme.Label, UITheme.InkMuted,
                                            TextAnchor.UpperLeft);
                UIBuilder.Place(name.rectTransform, topLeft, topLeft,
                                new Vector2(UITheme.Pad, y), new Vector2(220f, 20f));
                name.text = rating.Factors[i].name;

                Text value = UIBuilder.Label(panel, "FactorValue" + i, UITheme.Label, UITheme.Ink,
                                             TextAnchor.UpperRight);
                UIBuilder.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                new Vector2(-UITheme.Pad, y), new Vector2(120f, 20f));
                _factorValues.Add(value);

                UIBuilder.Solid(panel, "Track" + i, topLeft, topLeft,
                                new Vector2(UITheme.Pad, y - 24f),
                                new Vector2(430f - UITheme.Pad * 2f, 3f),
                                new Color(1f, 1f, 1f, 0.08f));

                Image bar = UIBuilder.Solid(panel, "Bar" + i, topLeft, topLeft,
                                            new Vector2(UITheme.Pad, y - 24f),
                                            new Vector2(10f, 3f), UITheme.Ice);
                bar.rectTransform.pivot = new Vector2(0f, 1f);
                _factorBars.Add(bar);
            }
        }

        void BuildFacilityHeader(Transform layer)
        {
            Text caption = UIBuilder.Label(layer, "FacilitiesCaption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(CardLeft, -232f), new Vector2(500f, 18f));
            caption.text = UITheme.Track("FACILITIES");

            _pageLabel = UIBuilder.Label(layer, "Page", UITheme.Micro, UITheme.InkFaint,
                                         TextAnchor.MiddleCenter);
            UIBuilder.Place(_pageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(CardLeft + 1000f, -240f), new Vector2(70f, 22f));

            _older = Arrow(layer, "Older", CardLeft + 950f, "<", -1);
            _newer = Arrow(layer, "Newer", CardLeft + 1078f, ">", 1);
        }

        UIButton Arrow(Transform layer, string name, float x, string glyph, int step)
        {
            RectTransform button = UIBuilder.Place(UIBuilder.Node(layer, name),
                                                   new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                   new Vector2(x, -240f), new Vector2(40f, 26f));
            UIPointer.Block(button);

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

            Text label = UIBuilder.Label(button, "Label", UITheme.Label, UITheme.Ink,
                                         TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch(label.rectTransform);
            label.text = glyph;

            var control = button.gameObject.AddComponent<UIButton>();
            control.background = fill;
            control.border = border;
            control.label = label;
            control.SetRestColour(UITheme.Card);

            int delta = step;
            control.Clicked += () => Turn(delta);

            return control;
        }

        void BuildFacilityCards(Transform host)
        {
            Facility[] facilities = FindObjectsByType<Facility>(FindObjectsSortMode.None);
            System.Array.Sort(facilities, (a, b) => string.CompareOrdinal(a.displayName, b.displayName));

            for (int i = 0; i < facilities.Length; i++)
            {
                if (Ghost(facilities[i])) continue;
                _cards.Add(BuildCard(host, facilities[i], 0f));
            }

            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, Pages - 1));
            LayoutCards();
        }

        /// <summary>A building still being positioned is not a facility yet.</summary>
        static bool Ghost(Facility facility)
        {
            var placed = facility as PlacedBuilding;
            return placed != null && placed.ghost;
        }

        static int FacilityCount()
        {
            Facility[] all = FindObjectsByType<Facility>(FindObjectsSortMode.None);

            int count = 0;
            for (int i = 0; i < all.Length; i++) if (!Ghost(all[i])) count++;

            return count;
        }

        int Pages { get { return Mathf.Max(1, Mathf.CeilToInt(_cards.Count / (float)PerPage)); } }

        void Turn(int delta)
        {
            _page = Mathf.Clamp(_page + delta, 0, Pages - 1);
            LayoutCards();
        }

        /// <summary>Three cards at a time, so they never run off the screen.</summary>
        void LayoutCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                if (card.root == null) continue;

                int column = i - _page * PerPage;
                bool visible = column >= 0 && column < PerPage;

                card.root.gameObject.SetActive(visible);
                if (visible)
                    card.root.anchoredPosition = new Vector2(CardLeft + column * CardStride, -258f);
            }

            if (_pageLabel != null) _pageLabel.text = UITheme.Track((_page + 1) + " / " + Pages);
            if (_older != null) _older.interactable = _page > 0;
            if (_newer != null) _newer.interactable = _page < Pages - 1;
        }

        /// <summary>Throw the cards away and read the resort again.</summary>
        void Rebuild()
        {
            for (int i = _cardHost.childCount - 1; i >= 0; i--)
            {
                GameObject child = _cardHost.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            _cards.Clear();
            BuildFacilityCards(_cardHost);
        }

        Card BuildCard(Transform layer, Facility facility, float x)
        {
            RectTransform card = UIBuilder.Glass(layer, facility.displayName + "Card",
                                                 new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                 new Vector2(x, -258f), new Vector2(CardWidth, 352f),
                                                 UITheme.Radius, UITheme.Card);
            UIPointer.Block(card);

            var topLeft = new Vector2(0f, 1f);

            UIBuilder.Icon(card, "Icon", IconFor(facility), UITheme.Ice, topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad), 26f);

            Text name = UIBuilder.Label(card, "Name", UITheme.Heading, UITheme.Ink,
                                        TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(name.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 36f, -UITheme.Pad - 2f), new Vector2(260f, 28f));
            name.text = facility.displayName.ToUpperInvariant();

            var result = new Card { root = card, facility = facility };

            result.level = UIBuilder.Label(card, "Level", UITheme.Micro, UITheme.Ice,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(result.level.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 40f), new Vector2(280f, 18f));

            UIBuilder.Rule(card, "Rule", topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad - 62f), 336f - UITheme.Pad * 2f);

            result.summary = UIBuilder.Label(card, "Summary", UITheme.Label, UITheme.InkMuted,
                                             TextAnchor.UpperLeft);
            UIBuilder.Place(result.summary.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 78f), new Vector2(290f, 40f));

            Text qualityCaption = UIBuilder.Label(card, "QualityCaption", UITheme.Micro,
                                                  UITheme.InkFaint, TextAnchor.UpperLeft);
            UIBuilder.Place(qualityCaption.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 128f), new Vector2(200f, 18f));
            qualityCaption.text = UITheme.Track("QUALITY");

            UIBuilder.Solid(card, "QualityTrack", topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 150f),
                            new Vector2(336f - UITheme.Pad * 2f, 4f), new Color(1f, 1f, 1f, 0.08f));

            result.qualityBar = UIBuilder.Solid(card, "QualityBar", topLeft, topLeft,
                                                new Vector2(UITheme.Pad, -UITheme.Pad - 150f),
                                                new Vector2(10f, 4f), UITheme.Ice);
            result.qualityBar.rectTransform.pivot = new Vector2(0f, 1f);

            result.upkeep = UIBuilder.Label(card, "Upkeep", UITheme.Label, UITheme.InkMuted,
                                            TextAnchor.UpperLeft);
            UIBuilder.Place(result.upkeep.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 176f), new Vector2(290f, 22f));

            // The upgrade button, which is the only thing on this screen that
            // actually does something.
            RectTransform button = UIBuilder.Place(UIBuilder.Node(card, "Upgrade"),
                                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                   new Vector2(0f, UITheme.Pad),
                                                   new Vector2(336f - UITheme.Pad * 2f, 52f));

            var buttonFill = button.gameObject.AddComponent<Image>();
            buttonFill.sprite = UISprites.Fill(UITheme.RadiusSmall);
            buttonFill.type = Image.Type.Sliced;
            buttonFill.color = UITheme.Card;

            var buttonBorder = UIBuilder.Stretch(UIBuilder.Node(button, "Hairline"))
                                        .gameObject.AddComponent<Image>();
            buttonBorder.sprite = UISprites.Outline(UITheme.RadiusSmall, 1);
            buttonBorder.type = Image.Type.Sliced;
            buttonBorder.color = UITheme.Hairline;
            buttonBorder.raycastTarget = false;

            result.buttonLabel = UIBuilder.Label(button, "Label", UITheme.Label, UITheme.Ink,
                                                 TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch(result.buttonLabel.rectTransform);

            result.button = button.gameObject.AddComponent<UIButton>();
            result.button.background = buttonFill;
            result.button.border = buttonBorder;
            result.button.label = result.buttonLabel;
            result.button.SetRestColour(UITheme.Card);

            Facility target = facility;
            result.button.Clicked += () => Upgrade(target);

            return result;
        }

        void BuildFooter(Transform layer)
        {
            Text footer = UIBuilder.Label(layer, "Footer", UITheme.Label, UITheme.InkFaint,
                                          TextAnchor.LowerCenter);
            UIBuilder.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, UITheme.Margin), new Vector2(900f, 24f));
            footer.text = UITheme.Track("TAB  RETURN TO THE MOUNTAIN", 1);
        }

        static Sprite IconFor(Facility facility)
        {
            if (facility is LiftFacility) return UIIcons.Lift;
            if (facility is ParkFacility) return UIIcons.Park;
            if (facility is LodgeFacility) return UIIcons.Lodge;
            if (facility is PlacedBuilding) return UIIcons.Lodge;
            return UIIcons.Mountain;
        }

        // ---------------- running -------------------------------------------

        void Upgrade(Facility facility)
        {
            if (facility == null || ledger == null || !facility.CanUpgrade) return;

            float cost = facility.UpgradeCost;
            if (!ledger.Spend(LedgerLine.Construction, cost)) return;

            facility.SetLevel(facility.level + 1);
            Refresh();

            if (notifications != null)
            {
                notifications.Announce(facility.displayName + " upgraded",
                                       "Now level " + facility.level + ".  " + facility.LevelSummary);
            }
        }

        void Update()
        {
            if (!IsOpen) return;
            Refresh();
        }

        void Refresh()
        {
            if (rating != null)
            {
                _ratingValue.text = rating.Stars.ToString("0.0");
                _ratingStars.Set(rating.Stars);

                for (int i = 0; i < _factorValues.Count && i < rating.Factors.Count; i++)
                {
                    _factorValues[i].text = Mathf.RoundToInt(rating.Factors[i].value * 100f) + "%";

                    RectTransform bar = _factorBars[i].rectTransform;
                    float full = 430f - UITheme.Pad * 2f;
                    bar.sizeDelta = new Vector2(Mathf.Max(3f, full * rating.Factors[i].value), 3f);
                }
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].root != null && !_cards[i].root.gameObject.activeSelf) continue;
                RefreshCard(_cards[i]);
            }
        }

        void RefreshCard(Card card)
        {
            Facility facility = card.facility;
            if (facility == null) return;

            card.level.text = UITheme.Track("LEVEL " + facility.level + " OF " + facility.maxLevel);
            card.summary.text = facility.LevelSummary;
            card.upkeep.text = Ledger.Money(facility.DailyUpkeep) + " per day";

            float full = CardWidth - UITheme.Pad * 2f;
            card.qualityBar.rectTransform.sizeDelta =
                new Vector2(Mathf.Max(4f, full * facility.Quality), 4f);

            if (!facility.CanUpgrade)
            {
                card.buttonLabel.text = UITheme.Track("FULLY UPGRADED");
                card.button.interactable = false;
                return;
            }

            bool affordable = ledger != null && ledger.Cash >= facility.UpgradeCost;

            card.buttonLabel.text = UITheme.Track("UPGRADE   " + Ledger.Money(facility.UpgradeCost));
            card.button.interactable = affordable;
        }
    }
}
