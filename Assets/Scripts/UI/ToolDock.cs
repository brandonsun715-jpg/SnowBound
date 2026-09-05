using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SnowBound.Game;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Hud
{
    /// <summary>
    /// Everything the owner does to the mountain, in one dock along the bottom
    /// of the screen.
    ///
    /// The row of tabs is always on screen in management mode, because it is
    /// the only way into the tools: hiding it until a tool was open would
    /// leave the player with nothing to click and no idea where anything is.
    /// The panel it opens sits above it.
    ///
    /// One panel, one page at a time. That is not a stylistic choice: it is the
    /// reason two tool panels can never end up on top of each other, whatever
    /// order the player clicks things in. The dock owns its pages, so opening
    /// one closes the last, and closing it stands down whichever tool was live.
    ///
    /// Everything is a fixed width that fits a four-by-three window, anchored
    /// to the bottom centre, so on a wider screen it sits in more space rather
    /// than stretching or spilling.
    /// </summary>
    public class ToolDock : MonoBehaviour
    {
        public enum Page { None, Build, Terrain, Trails, Lifts, Resort }

        public BuildController builder;
        public TerrainSculptor sculptor;
        public TrailDesigner trails;
        public LiftPlacer lifts;
        public Ledger ledger;
        public MountainGenerator mountain;
        public ManagementScreen overview;

        Canvas _canvas;
        UIPanel _panel;      // the page that is open
        UIPanel _bar;        // the row of tabs, always up in management mode
        RectTransform _dock;
        Text _status;

        Page _page = Page.None;

        readonly Dictionary<Page, RectTransform> _pages = new Dictionary<Page, RectTransform>();
        readonly Dictionary<Page, UIButton> _tabs = new Dictionary<Page, UIButton>();

        class Card
        {
            public UIButton button;
            public Text price;
            public float cost;
        }

        readonly List<Card> _buildCards = new List<Card>();
        readonly List<Card> _liftCards = new List<Card>();
        readonly Dictionary<TerrainTool, UIButton> _toolButtons = new Dictionary<TerrainTool, UIButton>();
        readonly Dictionary<SnowQuality, UIButton> _snowButtons = new Dictionary<SnowQuality, UIButton>();

        Text _sizeValue, _strengthValue;
        Image _sizeBar, _strengthBar;
        Text _widthValue;
        Image _widthBar;
        Text _trailName, _trailGrade, _trailStats;
        UIButton _confirmTrail, _groomToggle;
        readonly Dictionary<TrailGrade, UIButton> _gradeButtons = new Dictionary<TrailGrade, UIButton>();

        const float MeterWidth = 210f;

        void Start()
        {
            if (builder == null) builder = FindAnyObjectByType<BuildController>();
            if (sculptor == null) sculptor = FindAnyObjectByType<TerrainSculptor>();
            if (trails == null) trails = FindAnyObjectByType<TrailDesigner>();
            if (lifts == null) lifts = FindAnyObjectByType<LiftPlacer>();
            if (ledger == null) ledger = Ledger.Instance;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (overview == null) overview = FindAnyObjectByType<ManagementScreen>();

            Build();

            _panel.HideInstantly();
            _bar.HideInstantly();
            _canvas.enabled = false;
        }

        /// <summary>Show or hide the tabs. Called when the mode changes.</summary>
        public void SetVisible(bool visible)
        {
            if (_canvas == null) return;

            if (!visible) { Close(); _bar.Hide(); return; }

            _canvas.enabled = true;
            _bar.Show();
        }

        // ---------------- opening and closing -------------------------------

        /// <summary>A page is open, as opposed to just the tabs being up.</summary>
        public bool PageOpen { get { return _page != Page.None; } }

        /// <summary>Kept for the escape ladder: is there anything to close?</summary>
        public bool IsOpen { get { return PageOpen; } }

        public Page Current { get { return _page; } }

        public void Open(Page page)
        {
            if (_canvas == null) return;

            // The resort overview is a screen of its own rather than a page in
            // the dock, but it belongs in the same row: one place for
            // everything the owner can open.
            if (page == Page.Resort)
            {
                Close();
                if (overview == null) return;

                if (overview.IsOpen) overview.Close(); else overview.Open();
                return;
            }

            if (overview != null) overview.Close();

            if (_page == page && PageOpen) { Close(); return; }

            StandDown();

            _page = page;
            _canvas.enabled = true;
            _panel.Show();

            foreach (var pair in _pages) pair.Value.gameObject.SetActive(pair.Key == page);

            if (page == Page.Terrain && sculptor != null) sculptor.Begin(sculptor.tool);
        }

        /// <summary>Close whatever page is open. The tabs stay.</summary>
        public void Close()
        {
            if (_canvas == null) return;

            StandDown();

            _page = Page.None;
            _panel.Hide();
        }

        /// <summary>Put down whatever tool is in hand, without closing the dock.</summary>
        void StandDown()
        {
            if (builder != null) builder.Cancel();
            if (trails != null) trails.Cancel();
            if (lifts != null) lifts.Cancel();
            if (sculptor != null) sculptor.End();
        }

        /// <summary>True while a tool is waiting for a click on the mountain.</summary>
        public bool Busy
        {
            get
            {
                return (builder != null && builder.Placing)
                    || (trails != null && trails.Designing)
                    || (lifts != null && lifts.Placing)
                    || (sculptor != null && sculptor.Active);
            }
        }

        // ---------------- building the dock ----------------------------------

        void Build()
        {
            _canvas = UIBuilder.Canvas(transform, "ToolDock", 12);

            BuildTabs();

            _dock = UIBuilder.Glass(_canvas.transform, "Dock",
                                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                    new Vector2(0f, UILayout.DockBottom),
                                    new Vector2(UILayout.SafeWidth, UILayout.DockHeight));
            UIPointer.Block(_dock);

            _dock.gameObject.AddComponent<CanvasGroup>();
            _panel = _dock.gameObject.AddComponent<UIPanel>();

            _pages[Page.Build] = BuildPage("BuildPage", BuildBuildingRow);
            _pages[Page.Terrain] = BuildPage("TerrainPage", BuildTerrainTools);
            _pages[Page.Trails] = BuildPage("TrailPage", BuildTrailTools);
            _pages[Page.Lifts] = BuildPage("LiftPage", BuildLiftRow);

            _status = UIBuilder.Label(_dock, "Status", UITheme.Micro, UITheme.InkFaint,
                                      TextAnchor.LowerCenter);
            UIBuilder.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 10f), new Vector2(UILayout.SafeWidth - 40f, 18f));

            foreach (var pair in _pages) pair.Value.gameObject.SetActive(false);
        }

        /// <summary>
        /// The row of tabs. It lives outside the panel it opens, because a
        /// menu you can only reach by already being in the menu is not a menu.
        /// </summary>
        void BuildTabs()
        {
            var names = new[]
            {
                new KeyValuePair<Page, string>(Page.Build, "BUILD"),
                new KeyValuePair<Page, string>(Page.Terrain, "TERRAIN"),
                new KeyValuePair<Page, string>(Page.Trails, "TRAILS"),
                new KeyValuePair<Page, string>(Page.Lifts, "LIFTS"),
                new KeyValuePair<Page, string>(Page.Resort, "RESORT")
            };

            const float tabWidth = 158f;
            const float gap = 6f;

            float width = names.Length * (tabWidth + gap) - gap + UITheme.Pad;

            RectTransform bar = UIBuilder.Glass(_canvas.transform, "Tabs",
                                                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                new Vector2(0f, UILayout.Margin),
                                                new Vector2(width, UILayout.TabBarHeight),
                                                UITheme.RadiusSmall);
            UIPointer.Block(bar);

            bar.gameObject.AddComponent<CanvasGroup>();
            _bar = bar.gameObject.AddComponent<UIPanel>();
            _bar.riseDistance = 8f;

            float left = UITheme.Pad * 0.5f;

            for (int i = 0; i < names.Length; i++)
            {
                UIButton tab = Chip(bar, names[i].Value, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(left + i * (tabWidth + gap), 0f),
                                    new Vector2(tabWidth, UILayout.TabBarHeight - 12f));

                Page page = names[i].Key;
                tab.Clicked += () => Open(page);

                _tabs[page] = tab;
            }
        }

        /// <summary>
        /// Light up the tab that has the next obvious thing to do on it, so a
        /// new resort is not five unlabelled choices with no order to them.
        /// </summary>
        float _liftCheckedAt = -99f;
        bool _hasLift;

        Page Suggested
        {
            get
            {
                if (mountain != null && mountain.TrailCount == 0) return Page.Trails;

                if (Time.time - _liftCheckedAt > 0.5f)
                {
                    _liftCheckedAt = Time.time;
                    _hasLift = FindAnyObjectByType<Chairlift>() != null;
                }

                return _hasLift ? Page.None : Page.Lifts;
            }
        }

        RectTransform BuildPage(string name, System.Action<RectTransform> fill)
        {
            RectTransform page = UIBuilder.Node(_dock, name);
            page.anchorMin = new Vector2(0f, 0f);
            page.anchorMax = new Vector2(1f, 1f);
            page.pivot = new Vector2(0.5f, 0.5f);
            page.offsetMin = new Vector2(UITheme.Pad, 30f);
            page.offsetMax = new Vector2(-UITheme.Pad, -58f);

            fill(page);
            return page;
        }

        // ---------------- build page --------------------------------------------

        void BuildBuildingRow(RectTransform page)
        {
            IReadOnlyList<BuildingDefinition> catalogue = BuildingCatalogue.All;

            const float cardWidth = 214f;
            float stride = UILayout.RowStride(catalogue.Count, cardWidth, 10f);

            for (int i = 0; i < catalogue.Count; i++)
            {
                BuildingDefinition definition = catalogue[i];

                RectTransform card = CardFrame(page, definition.name,
                                               UILayout.RowOffset(i, catalogue.Count, stride),
                                               stride - 10f);

                Head(card, definition.name, UIIcons.Lodge);
                Line(card, definition.firstEffect, -40f, UITheme.InkMuted);
                Line(card, definition.secondEffect, -60f, UITheme.InkFaint);
                Line(card, Ledger.Money(definition.dailyUpkeep) + " / day upkeep", -80f, UITheme.InkFaint);

                Card entry = Action(card, "BUILD   " + Ledger.Money(definition.cost), definition.cost);

                BuildingDefinition captured = definition;
                entry.button.Clicked += () =>
                {
                    StandDown();
                    if (builder != null) builder.Begin(captured);
                };

                _buildCards.Add(entry);
            }
        }

        // ---------------- terrain page --------------------------------------------

        void BuildTerrainTools(RectTransform page)
        {
            Caption(page, "SHAPE THE MOUNTAIN", 0f);

            var tools = new[]
            {
                TerrainTool.Raise, TerrainTool.Lower, TerrainTool.Smooth,
                TerrainTool.Flatten, TerrainTool.Slope
            };

            const float buttonWidth = 132f;

            for (int i = 0; i < tools.Length; i++)
            {
                TerrainTool tool = tools[i];

                UIButton button = Chip(page, TerrainSculptor.ToolName(tool),
                                       new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(i * (buttonWidth + 8f), -22f),
                                       new Vector2(buttonWidth, 40f));

                button.Clicked += () =>
                {
                    if (sculptor == null) return;

                    sculptor.Painting = false;
                    sculptor.Begin(tool);
                };

                _toolButtons[tool] = button;
            }

            // Size and strength, as steps rather than a drag. A meter you nudge
            // is exact; a meter you drag is a fight with the mouse.
            _sizeBar = Meter(page, "SIZE", 0f, -74f, out _sizeValue,
                             () => Nudge(-1, 0), () => Nudge(1, 0));

            _strengthBar = Meter(page, "STRENGTH", MeterWidth + 150f, -74f, out _strengthValue,
                                 () => Nudge(0, -1), () => Nudge(0, 1));

            Caption(page, "PAINT THE SNOW", -152f);

            var qualities = new[]
            {
                SnowQuality.Packed, SnowQuality.Powder, SnowQuality.FreshPowder,
                SnowQuality.Icy, SnowQuality.Mixed
            };

            for (int i = 0; i < qualities.Length; i++)
            {
                SnowQuality quality = qualities[i];

                UIButton button = Chip(page, Trail.SnowName(quality),
                                       new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(i * 140f, -172f), new Vector2(132f, 36f));

                button.Clicked += () =>
                {
                    if (sculptor != null) sculptor.BeginPainting(quality, sculptor.paintGroomed);
                };

                _snowButtons[quality] = button;
            }

            UIButton groom = Chip(page, "GROOMED", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(-142f, -172f), new Vector2(132f, 36f));
            groom.Clicked += () =>
            {
                if (sculptor != null) sculptor.BeginPainting(sculptor.paintQuality, true);
            };

            UIButton rough = Chip(page, "UNGROOMED", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(0f, -172f), new Vector2(132f, 36f));
            rough.Clicked += () =>
            {
                if (sculptor != null) sculptor.BeginPainting(sculptor.paintQuality, false);
            };
        }

        void Nudge(int size, int strength)
        {
            if (sculptor == null) return;

            if (size != 0) sculptor.SetRadius(sculptor.radius + size * 6f);
            if (strength != 0) sculptor.SetStrength(sculptor.strength + strength * 0.1f);
        }

        // ---------------- trail page --------------------------------------------

        void BuildTrailTools(RectTransform page)
        {
            Caption(page, "CUT A NEW RUN", 0f);

            var grades = new[] { TrailGrade.Green, TrailGrade.Blue, TrailGrade.Black, TrailGrade.DoubleBlack };

            for (int i = 0; i < grades.Length; i++)
            {
                TrailGrade grade = grades[i];

                UIButton button = Chip(page, Trail.GradeName(grade),
                                       new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(i * 168f, -22f), new Vector2(160f, 40f));

                UIBuilder.Solid(button.transform, "Pip", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                new Vector2(12f, 0f), new Vector2(9f, 9f), SkiHud.GradeColour(grade), 5);

                button.Clicked += () =>
                {
                    StandDown();
                    if (trails != null) trails.Begin(grade);
                };

                _gradeButtons[grade] = button;
            }

            _widthBar = Meter(page, "WIDTH", 0f, -74f, out _widthValue,
                              () => Widen(-1), () => Widen(1));

            _trailName = UIBuilder.Label(page, "Name", UITheme.Heading, UITheme.Ink,
                                         TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(_trailName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(MeterWidth + 150f, -74f), new Vector2(330f, 26f));

            _trailGrade = UIBuilder.Label(page, "Grade", UITheme.Micro, UITheme.Ice,
                                          TextAnchor.UpperLeft);
            UIBuilder.Place(_trailGrade.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(MeterWidth + 150f, -100f), new Vector2(330f, 18f));

            _trailStats = UIBuilder.Label(page, "Stats", UITheme.Label, UITheme.InkMuted,
                                          TextAnchor.UpperLeft);
            UIBuilder.Place(_trailStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(MeterWidth + 150f, -126f), new Vector2(520f, 66f));

            _confirmTrail = Chip(page, "CONFIRM RUN", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                 new Vector2(0f, -22f), new Vector2(190f, 40f));
            _confirmTrail.Clicked += () => { if (trails != null) trails.Confirm(); };

            UIButton undo = Chip(page, "UNDO POINT", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                 new Vector2(-198f, -22f), new Vector2(180f, 40f));
            undo.Clicked += () => { if (trails != null) trails.Undo(); };

            _groomToggle = Chip(page, "GROOMED", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                new Vector2(0f, -70f), new Vector2(190f, 36f));
            _groomToggle.Clicked += () =>
            {
                if (trails == null || trails.Draft == null) return;
                trails.SetGroomed(!trails.Draft.groomed);
            };

            // The park is built onto a run, so it belongs with the runs.
            _park = Chip(page, "TERRAIN PARK   " + Ledger.Money(parkCost),
                         new Vector2(1f, 1f), new Vector2(1f, 1f),
                         new Vector2(0f, -114f), new Vector2(190f, 36f));
            _park.Clicked += BuildPark;
        }

        [Header("Terrain park")]
        public float parkCost = 18000f;

        UIButton _park;

        /// <summary>
        /// Raise the snow park on the first run. It is a facility like any
        /// other once it exists: it costs money to run and it counts towards
        /// the rating and the variety of the resort.
        /// </summary>
        void BuildPark()
        {
            var park = FindAnyObjectByType<TerrainPark>();
            if (park == null || park.built) return;
            if (mountain == null || mountain.TrailCount == 0) return;

            if (ledger != null && !ledger.Spend(LedgerLine.Construction, parkCost)) return;

            park.trailIndex = 0;
            park.built = true;
            park.Build();

            var traffic = FindAnyObjectByType<ResortTraffic>();
            if (traffic != null) traffic.Rescan();

            var notes = FindAnyObjectByType<NotificationStack>();
            if (notes != null)
                notes.Announce("Terrain park open", "Kickers and boxes on "
                                                    + mountain.TrailAt(0).name + ".");
        }

        void Widen(int step)
        {
            if (trails == null || trails.Draft == null) return;
            trails.SetWidth(trails.Draft.halfWidth + step * 2f);
        }

        // ---------------- lift page ---------------------------------------------

        void BuildLiftRow(RectTransform page)
        {
            IReadOnlyList<LiftDefinition> catalogue = LiftCatalogue.All;

            const float cardWidth = 262f;
            float stride = UILayout.RowStride(catalogue.Count, cardWidth, 12f);

            for (int i = 0; i < catalogue.Count; i++)
            {
                LiftDefinition definition = catalogue[i];

                RectTransform card = CardFrame(page, definition.name,
                                               UILayout.RowOffset(i, catalogue.Count, stride),
                                               stride - 12f);

                Head(card, definition.name, UIIcons.Lift);
                Line(card, definition.GuestsPerHour.ToString("N0") + " guests / hour", -40f, UITheme.InkMuted);
                Line(card, "Speed  " + definition.speedWord
                         + "     Comfort  " + definition.comfortWord, -60f, UITheme.InkFaint);
                Line(card, Ledger.Money(definition.dailyUpkeep) + " / day     max "
                         + Mathf.RoundToInt(definition.maxLength) + " m", -80f, UITheme.InkFaint);

                Card entry = Action(card, "BUILD   " + Ledger.Money(definition.cost), definition.cost);

                LiftDefinition captured = definition;
                entry.button.Clicked += () =>
                {
                    StandDown();
                    if (lifts != null) lifts.Begin(captured);
                };

                _liftCards.Add(entry);
            }
        }

        // ---------------- running -------------------------------------------------

        void Update()
        {
            if (_canvas == null || !_canvas.enabled) return;

            Page suggested = Suggested;

            foreach (var pair in _tabs)
            {
                Color rest = pair.Key == _page ? UITheme.CardActive
                           : pair.Key == suggested ? UITheme.CardHover
                           : UITheme.Card;

                pair.Value.SetRestColour(rest);
                pair.Value.labelColour = pair.Key == suggested ? UITheme.Ice : UITheme.Ink;
            }

            if (!PageOpen) { _status.text = string.Empty; return; }

            float cash = ledger != null ? ledger.Cash : 0f;

            for (int i = 0; i < _buildCards.Count; i++) Afford(_buildCards[i], cash);
            for (int i = 0; i < _liftCards.Count; i++) Afford(_liftCards[i], cash);

            switch (_page)
            {
                case Page.Terrain: UpdateTerrain(); break;
                case Page.Trails: UpdateTrails(); break;
                default: _status.text = UITheme.Track(Hint()); break;
            }
        }

        void Afford(Card card, float cash)
        {
            if (card == null || card.button == null) return;

            bool affordable = cash >= card.cost;

            card.button.interactable = affordable;
            card.button.labelColour = affordable ? UITheme.Ink : UITheme.Negative;
        }

        string Hint()
        {
            if (builder != null && builder.Placing)
            {
                return builder.Refusal != null
                    ? builder.Refusal.ToUpperInvariant()
                    : "CLICK  PLACE          R  ROTATE          ESC  CANCEL";
            }

            if (lifts != null && lifts.Placing)
            {
                if (lifts.Refusal != null) return lifts.Refusal.ToUpperInvariant();

                return "LENGTH  " + Mathf.RoundToInt(lifts.Length) + " M"
                     + "          RISE  " + Mathf.RoundToInt(lifts.Rise) + " M"
                     + "          RIDE  " + Mathf.RoundToInt(lifts.RideSeconds) + " S"
                     + "          CLICK  BUILD          ESC  CANCEL";
            }

            return "PICK SOMETHING TO BUILD";
        }

        void UpdateTerrain()
        {
            if (sculptor == null) return;

            foreach (var pair in _toolButtons)
                pair.Value.SetRestColour(!sculptor.Painting && sculptor.tool == pair.Key
                                         ? UITheme.CardActive : UITheme.Card);

            foreach (var pair in _snowButtons)
                pair.Value.SetRestColour(sculptor.Painting && sculptor.paintQuality == pair.Key
                                         ? UITheme.CardActive : UITheme.Card);

            _sizeValue.text = Mathf.RoundToInt(sculptor.radius) + " m";
            _sizeBar.rectTransform.sizeDelta =
                new Vector2(Mathf.Max(4f, MeterWidth * Mathf.InverseLerp(sculptor.minRadius,
                                                                        sculptor.maxRadius,
                                                                        sculptor.radius)), 4f);

            _strengthValue.text = Mathf.RoundToInt(sculptor.strength * 100f) + "%";
            _strengthBar.rectTransform.sizeDelta =
                new Vector2(Mathf.Max(4f, MeterWidth * sculptor.strength), 4f);

            _status.text = UITheme.Track(sculptor.Refusal != null
                ? sculptor.Refusal.ToUpperInvariant()
                : (sculptor.Painting
                    ? "CLICK A RUN TO CHANGE ITS SNOW          ESC  DONE"
                    : "HOLD THE MOUSE ON THE MOUNTAIN TO SHAPE IT          ESC  DONE"));
        }

        void UpdateTrails()
        {
            if (trails == null) return;

            bool designing = trails.Designing && trails.Draft != null;

            _confirmTrail.interactable = designing && trails.ValidHere;
            _groomToggle.interactable = designing;

            var park = FindAnyObjectByType<TerrainPark>();
            _park.interactable = park != null && !park.built
                              && mountain != null && mountain.TrailCount > 0
                              && (ledger == null || ledger.Cash >= parkCost);
            _park.label.text = UITheme.Track(park != null && park.built
                ? "PARK BUILT"
                : "TERRAIN PARK   " + Ledger.Money(parkCost));

            if (!designing)
            {
                _trailName.text = "NO RUN IN DESIGN";
                _trailGrade.text = string.Empty;
                _trailStats.text = string.Empty;
                _widthValue.text = "-";
                _widthBar.rectTransform.sizeDelta = new Vector2(4f, 4f);

                _status.text = UITheme.Track("PICK A DIFFICULTY, THEN CLICK DOWN THE MOUNTAIN");
                return;
            }

            Trail draft = trails.Draft;

            _trailName.text = draft.name.ToUpperInvariant();

            // The grade is whatever the terrain turned out to be, which is not
            // always what was asked for. Saying so is the honest thing.
            _trailGrade.text = UITheme.Track(Trail.GradeName(draft.grade) + "  ·  "
                                             + Ledger.Money(trails.Cost));
            _trailGrade.color = SkiHud.GradeColour(draft.grade);

            _trailStats.text = "Length " + Mathf.RoundToInt(draft.length) + " m"
                             + "     Vertical " + Mathf.RoundToInt(draft.drop) + " m\n"
                             + "Average grade " + Mathf.RoundToInt(draft.averageGrade * 100f) + "%"
                             + "     Max grade " + Mathf.RoundToInt(draft.maxGrade * 100f) + "%"
                             + "     " + (draft.groomed ? "Groomed" : "Ungroomed");

            _widthValue.text = Mathf.RoundToInt(draft.halfWidth * 2f) + " m";
            _widthBar.rectTransform.sizeDelta =
                new Vector2(Mathf.Max(4f, MeterWidth * Mathf.InverseLerp(10f, 68f, draft.halfWidth * 2f)), 4f);

            _groomToggle.label.text = UITheme.Track(draft.groomed ? "GROOMED" : "UNGROOMED");

            foreach (var pair in _gradeButtons)
                pair.Value.SetRestColour(draft.grade == pair.Key ? UITheme.CardActive : UITheme.Card);

            _status.text = UITheme.Track(trails.Refusal != null
                ? trails.Refusal.ToUpperInvariant()
                : "CLICK TO ADD A POINT          RIGHT CLICK  UNDO          ESC  CANCEL");
        }

        // ---------------- pieces ---------------------------------------------------

        void Caption(Transform page, string text, float y)
        {
            Text caption = UIBuilder.Label(page, "Caption" + text, UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(2f, y), new Vector2(420f, 18f));
            caption.text = UITheme.Track(text);
        }

        RectTransform CardFrame(Transform page, string name, float x, float width)
        {
            RectTransform card = UIBuilder.Glass(page, name, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                                                 new Vector2(x, 0f), new Vector2(width, 152f),
                                                 UITheme.RadiusSmall, UITheme.Card);
            return card;
        }

        void Head(Transform card, string name, Sprite icon)
        {
            var topLeft = new Vector2(0f, 1f);

            UIBuilder.Icon(card, "Icon", icon, UITheme.Ice, topLeft, topLeft,
                           new Vector2(14f, -12f), 18f);

            Text label = UIBuilder.Label(card, "Name", UITheme.Label, UITheme.Ink,
                                         TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Place(label.rectTransform, topLeft, topLeft,
                            new Vector2(40f, -12f), new Vector2(190f, 22f));
            label.text = name.ToUpperInvariant();
        }

        void Line(Transform card, string text, float y, Color colour)
        {
            Text line = UIBuilder.Label(card, "Line" + y, UITheme.Micro, colour, TextAnchor.UpperLeft);
            UIBuilder.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, y), new Vector2(220f, 18f));
            line.text = text;
        }

        Card Action(RectTransform card, string label, float cost)
        {
            UIButton button = Chip(card, label, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(0f, 12f),
                                   new Vector2(card.sizeDelta.x - 24f, 36f));

            return new Card { button = button, price = button.label, cost = cost };
        }

        /// <summary>
        /// The one button shape used everywhere in the dock: a filled rounded
        /// rectangle with a hairline and a tracked label. Everything being the
        /// same shape is most of what makes an interface look designed.
        /// </summary>
        UIButton Chip(Transform parent, string text, Vector2 anchor, Vector2 pivot,
                      Vector2 offset, Vector2 size)
        {
            RectTransform rect = UIBuilder.Place(UIBuilder.Node(parent, text), anchor, pivot, offset, size);

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
            UIBuilder.Stretch(label.rectTransform, 6f);
            label.text = UITheme.Track(text);

            var button = rect.gameObject.AddComponent<UIButton>();
            button.background = fill;
            button.border = border;
            button.label = label;
            button.SetRestColour(UITheme.Card);

            return button;
        }

        /// <summary>A labelled bar with a minus and a plus. Exact, and never a drag.</summary>
        Image Meter(Transform page, string name, float x, float y, out Text value,
                    System.Action less, System.Action more)
        {
            Text caption = UIBuilder.Label(page, name + "Caption", UITheme.Micro, UITheme.InkFaint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(x, y), new Vector2(200f, 18f));
            caption.text = UITheme.Track(name);

            value = UIBuilder.Label(page, name + "Value", UITheme.Label, UITheme.Ink,
                                    TextAnchor.UpperRight, FontStyle.Bold);
            UIBuilder.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            new Vector2(x + MeterWidth, y - 2f), new Vector2(110f, 20f));

            UIBuilder.Solid(page, name + "Track", new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(x, y - 24f), new Vector2(MeterWidth, 4f),
                            new Color(1f, 1f, 1f, 0.08f));

            Image bar = UIBuilder.Solid(page, name + "Bar", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(x, y - 24f), new Vector2(20f, 4f), UITheme.Ice);
            bar.rectTransform.pivot = new Vector2(0f, 1f);

            UIButton minus = Chip(page, "-", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                  new Vector2(x, y - 38f), new Vector2(40f, 30f));
            minus.Clicked += () => less();

            UIButton plus = Chip(page, "+", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                 new Vector2(x + 48f, y - 38f), new Vector2(40f, 30f));
            plus.Clicked += () => more();

            return bar;
        }
    }
}
