using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Game;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// The build menu: a row of building cards and a row of trail grades,
    /// along the bottom of the screen where they do not cover the mountain
    /// you are choosing a spot on.
    ///
    /// While something is being positioned the menu gets out of the way
    /// entirely and is replaced by one line: what it is, what it costs, and
    /// the three keys that matter.
    /// </summary>
    public class BuildPanel : MonoBehaviour
    {
        public BuildController builder;
        public TrailBuilder trails;
        public Ledger ledger;

        Canvas _canvas;
        UIPanel _menu;
        UIPanel _banner;

        Text _bannerTitle, _bannerHint, _bannerRefusal;

        class Card
        {
            public BuildingDefinition definition;
            public UIButton button;
            public Text price;
        }

        readonly List<Card> _cards = new List<Card>();

        void Start()
        {
            if (builder == null) builder = FindAnyObjectByType<BuildController>();
            if (trails == null) trails = FindAnyObjectByType<TrailBuilder>();
            if (ledger == null) ledger = Ledger.Instance;

            Build();
            _menu.HideInstantly();
            _banner.HideInstantly();
            _canvas.enabled = false;
        }

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        public void Open()
        {
            if (_canvas == null) return;
            _canvas.enabled = true;
            _menu.Show();
        }

        public void Close()
        {
            if (_canvas == null) return;

            if (builder != null) builder.Cancel();
            if (trails != null) trails.Cancel();

            _menu.Hide();
            _banner.Hide();
            _canvas.enabled = false;
        }

        // ---------------- building ---------------------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "BuildPanel", 12);

            RectTransform menu = UIBuilder.Place(UIBuilder.Node(_canvas.transform, "Menu"),
                                                 new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                 new Vector2(0f, UITheme.Margin + 92f),
                                                 new Vector2(1400f, 280f));
            menu.gameObject.AddComponent<CanvasGroup>();
            _menu = menu.gameObject.AddComponent<UIPanel>();

            BuildBuildingRow(menu);
            BuildTrailRow(menu);
            BuildBanner(_canvas.transform);
        }

        void BuildBuildingRow(Transform menu)
        {
            Text caption = UIBuilder.Label(menu, "Caption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(6f, 0f), new Vector2(400f, 18f));
            caption.text = UITheme.Track("BUILDINGS");

            IReadOnlyList<BuildingDefinition> catalogue = BuildingCatalogue.All;
            float width = 268f;

            for (int i = 0; i < catalogue.Count; i++)
            {
                BuildingDefinition definition = catalogue[i];
                _cards.Add(BuildCard(menu, definition, i * (width + 12f), width));
            }
        }

        Card BuildCard(Transform menu, BuildingDefinition definition, float x, float width)
        {
            RectTransform card = UIBuilder.Glass(menu, definition.name, new Vector2(0f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(x, -24f),
                                                 new Vector2(width, 168f), UITheme.Radius, UITheme.Card);
            UIPointer.Block(card);

            var topLeft = new Vector2(0f, 1f);

            UIBuilder.Icon(card, "Icon", UIIcons.Lodge, UITheme.Ice, topLeft, topLeft,
                           new Vector2(UITheme.Pad, -UITheme.Pad), 22f);

            Text name = UIBuilder.Label(card, "Name", UITheme.Heading, UITheme.Ink,
                                        TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(name.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad + 32f, -UITheme.Pad - 2f), new Vector2(210f, 26f));
            name.text = definition.name.ToUpperInvariant();

            Text first = UIBuilder.Label(card, "First", UITheme.Micro, UITheme.InkMuted,
                                         TextAnchor.UpperLeft);
            UIBuilder.Place(first.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 34f), new Vector2(230f, 18f));
            first.text = definition.firstEffect;

            Text second = UIBuilder.Label(card, "Second", UITheme.Micro, UITheme.InkFaint,
                                          TextAnchor.UpperLeft);
            UIBuilder.Place(second.rectTransform, topLeft, topLeft,
                            new Vector2(UITheme.Pad, -UITheme.Pad - 54f), new Vector2(230f, 18f));
            second.text = definition.secondEffect + "     " +
                          Ledger.Money(definition.dailyUpkeep) + " / day";

            RectTransform button = UIBuilder.Place(UIBuilder.Node(card, "Build"),
                                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                   new Vector2(0f, UITheme.Pad - 6f),
                                                   new Vector2(width - UITheme.Pad * 2f, 46f));

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

            Text price = UIBuilder.Label(button, "Price", UITheme.Label, UITheme.Ink,
                                         TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch(price.rectTransform);

            var control = button.gameObject.AddComponent<UIButton>();
            control.background = fill;
            control.border = border;
            control.label = price;
            control.SetRestColour(UITheme.Card);

            BuildingDefinition captured = definition;
            control.Clicked += () => { if (builder != null) builder.Begin(captured); };

            return new Card { definition = definition, button = control, price = price };
        }

        void BuildTrailRow(Transform menu)
        {
            Text caption = UIBuilder.Label(menu, "TrailCaption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(6f, -204f), new Vector2(400f, 18f));
            caption.text = UITheme.Track("CUT A NEW RUN");

            PisteGrade[] grades = { PisteGrade.Beginner, PisteGrade.Intermediate, PisteGrade.Advanced };

            for (int i = 0; i < grades.Length; i++)
            {
                PisteGrade grade = grades[i];

                RectTransform button = UIBuilder.Place(UIBuilder.Node(menu, "Trail" + grade),
                                                       new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                       new Vector2(i * 236f, -228f),
                                                       new Vector2(224f, 46f));
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

                UIBuilder.Solid(button, "Grade", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                new Vector2(14f, 0f), new Vector2(10f, 10f),
                                SkiHud.GradeColour(grade), 5);

                Text label = UIBuilder.Label(button, "Label", UITheme.Label, UITheme.Ink,
                                             TextAnchor.MiddleLeft, FontStyle.Bold);
                UIBuilder.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                new Vector2(34f, 0f), new Vector2(180f, 22f));
                label.text = UITheme.Track(SkiHud.GradeName(grade) + " RUN");

                var control = button.gameObject.AddComponent<UIButton>();
                control.background = fill;
                control.border = border;
                control.label = label;
                control.SetRestColour(UITheme.Card);

                PisteGrade captured = grade;
                control.Clicked += () => { if (trails != null) trails.Begin(captured); };
            }
        }

        void BuildBanner(Transform root)
        {
            RectTransform banner = UIBuilder.Glass(root, "PlacementBanner",
                                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                   new Vector2(0f, UITheme.Margin + 20f),
                                                   new Vector2(700f, 96f));
            banner.gameObject.AddComponent<CanvasGroup>();
            _banner = banner.gameObject.AddComponent<UIPanel>();

            _bannerTitle = UIBuilder.Label(banner, "Title", UITheme.Heading, UITheme.Ink,
                                           TextAnchor.UpperCenter, FontStyle.Bold);
            UIBuilder.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -14f), new Vector2(660f, 28f));

            _bannerRefusal = UIBuilder.Label(banner, "Refusal", UITheme.Micro, UITheme.Negative,
                                             TextAnchor.UpperCenter);
            UIBuilder.Place(_bannerRefusal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -44f), new Vector2(660f, 18f));

            _bannerHint = UIBuilder.Label(banner, "Hint", UITheme.Micro, UITheme.InkFaint,
                                          TextAnchor.LowerCenter);
            UIBuilder.Place(_bannerHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 14f), new Vector2(660f, 18f));
        }

        // ---------------- running -------------------------------------------------

        void Update()
        {
            if (_canvas == null || !_canvas.enabled) return;

            bool placingBuilding = builder != null && builder.Placing;
            bool cuttingTrail = trails != null && trails.Planning;
            bool busy = placingBuilding || cuttingTrail;

            // While positioning, the menu is in the way of the decision.
            if (busy) _menu.Hide(); else _menu.Show();
            if (busy) _banner.Show(); else _banner.Hide();

            if (placingBuilding) ShowBanner(builder.Pending.name, builder.Pending.cost,
                                            builder.ValidHere, builder.Refusal, true);
            else if (cuttingTrail) ShowBanner(SkiHud.GradeName(trails.Grade) + " RUN", trails.Cost,
                                              trails.ValidHere, trails.Refusal, false);

            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool affordable = ledger == null || ledger.Cash >= card.definition.cost;

                card.price.text = UITheme.Track("BUILD   " + Ledger.Money(card.definition.cost));
                card.button.interactable = affordable && !busy;
            }
        }

        void ShowBanner(string title, float cost, bool valid, string refusal, bool rotatable)
        {
            _bannerTitle.text = title.ToUpperInvariant() + "     " + Ledger.Money(cost);
            _bannerTitle.color = valid ? UITheme.Ink : UITheme.Negative;

            _bannerRefusal.text = valid ? string.Empty : (refusal == null ? string.Empty : refusal);

            _bannerHint.text = UITheme.Track(rotatable
                ? "CLICK  PLACE          R  ROTATE          ESC  CANCEL"
                : "CLICK  CUT THE RUN          ESC  CANCEL");
        }
    }
}
