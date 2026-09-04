using UnityEngine;
using UnityEngine.UI;
using SnowBound.Player;
using SnowBound.Lifts;
using SnowBound.Game;

namespace SnowBound.Hud
{
    /// <summary>
    /// The prototype heads-up display: what is on your feet, how fast you are
    /// going, the clock, and one line telling you what to do next.
    ///
    /// It only ever reads state from the other systems. Nothing here decides
    /// anything, so replacing it with a designed UI later touches this file
    /// alone.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        const string ContainerName = "GeneratedHud";

        public PlayerController player;
        public Chairlift lift;
        public GearRack rack;
        public RunTimer timer;

        Text _status;
        Text _clock;
        Text _prompt;

        void Start()
        {
            Build();
        }

        void Build()
        {
            var canvasObject = new GameObject(ContainerName);
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = LoadFont();
            if (font == null)
                Debug.LogWarning("[GameHud] No built-in font found; the HUD will be blank.", this);

            _status = Label(canvasObject.transform, "Status", font,
                            new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(36f, -30f), TextAnchor.UpperLeft, 34);

            _clock = Label(canvasObject.transform, "Clock", font,
                           new Vector2(1f, 1f), new Vector2(1f, 1f),
                           new Vector2(-36f, -30f), TextAnchor.UpperRight, 34);

            _prompt = Label(canvasObject.transform, "Prompt", font,
                            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 74f), TextAnchor.LowerCenter, 30);

            canvasObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        static Font LoadFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { font = null; }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { font = null; }
            }

            return font;
        }

        static Text Label(Transform parent, string name, Font font, Vector2 anchor,
                          Vector2 pivot, Vector2 offset, TextAnchor align, int size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(1000f, 220f);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // Snow is white, so the text needs something behind it.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (lift == null) lift = Chairlift.Instance;
            if (rack == null) rack = FindAnyObjectByType<GearRack>();
            if (timer == null) timer = FindAnyObjectByType<RunTimer>();

            if (player == null || _status == null) return;

            _status.text = GearName(player.CurrentKind) + "\n" +
                           Mathf.RoundToInt(player.Speed * 3.6f) + " km/h";

            _clock.text = ClockText();
            _prompt.text = PromptText();
        }

        static string GearName(LocomotionKind kind)
        {
            switch (kind)
            {
                case LocomotionKind.Ski: return "Skis";
                case LocomotionKind.Snowboard: return "Snowboard";
                default: return "On foot";
            }
        }

        string ClockText()
        {
            if (timer == null) return string.Empty;

            if (timer.Running)
                return "Run  " + RunTimer.Format(timer.Elapsed);

            string best = "Best  " + RunTimer.Format(timer.BestTime);
            if (timer.LastTime < 0f) return best;

            return "Last  " + RunTimer.Format(timer.LastTime) + "\n" + best;
        }

        string PromptText()
        {
            if (player.IsRiding) return "Riding up. Enjoy the view.";

            if (lift != null && lift.PlayerInLoadingArea)
                return "Wait here. The next chair will pick you up.";

            if (rack != null && rack.PlayerInRange)
                return "1  Boots        2  Skis        3  Snowboard";

            if (!player.IsRidingSnow)
                return "Collect your gear from the rack outside the lodge.";

            if (timer != null && timer.Running)
                return string.Empty;

            return "Skate to the chairlift and ride to the summit.";
        }
    }
}
