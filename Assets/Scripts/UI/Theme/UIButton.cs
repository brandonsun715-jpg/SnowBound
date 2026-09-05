using UnityEngine;
using UnityEngine.UI;

namespace SnowBound.Hud
{
    /// <summary>
    /// A card you can click. States are separated by a small shift in surface
    /// brightness rather than by an outline or a colour change, so a screen
    /// full of them still reads as one calm surface.
    /// </summary>
    public class UIButton : MonoBehaviour
    {
        public Image background;
        public Image border;
        public Text label;
        public bool interactable = true;

        /// <summary>Label colour while enabled. Set it to flag a price you cannot pay.</summary>
        [System.NonSerialized] public Color labelColour = UITheme.Ink;

        public event System.Action Clicked;

        public bool Hovered { get; private set; }

        RectTransform _rect;
        Color _restColour;
        CanvasGroup _group;
        bool _groupChecked;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (background != null) _restColour = background.color;
        }

        public void SetRestColour(Color colour)
        {
            _restColour = colour;
            if (background != null && !Hovered) background.color = colour;
        }

        void Update()
        {
            if (background == null || _rect == null) return;

            // A panel mid-fade is still in the hierarchy. It must not accept clicks.
            if (!_groupChecked)
            {
                _group = GetComponentInParent<CanvasGroup>();
                _groupChecked = true;
            }

            bool reachable = _group == null || _group.alpha > 0.9f;

            Hovered = interactable && reachable && UIPointer.Over(_rect);

            Color target = _restColour;
            Color edge = UITheme.Hairline;

            if (!interactable)
            {
                target = new Color(_restColour.r, _restColour.g, _restColour.b, _restColour.a * 0.55f);
            }
            else if (Hovered)
            {
                target = UIPointer.Held ? UITheme.CardActive : UITheme.CardHover;
                edge = UITheme.HairlineBright;
            }

            float dt = Time.unscaledDeltaTime;
            background.color = Color.Lerp(background.color, target, 1f - Mathf.Exp(-14f * dt));
            if (border != null) border.color = Color.Lerp(border.color, edge, 1f - Mathf.Exp(-14f * dt));

            if (label != null)
                label.color = Color.Lerp(label.color, interactable ? labelColour : UITheme.InkFaint,
                                         1f - Mathf.Exp(-14f * dt));

            if (Hovered && UIPointer.Pressed && Clicked != null) Clicked();
        }
    }
}
