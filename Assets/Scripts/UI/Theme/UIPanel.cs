using UnityEngine;

namespace SnowBound.Hud
{
    /// <summary>
    /// Shows and hides a piece of interface the expensive way: opacity, a
    /// short rise, and a hair of scale. Fast, smooth, controlled. Nothing
    /// bounces, nothing swings, nothing draws attention to the animation
    /// instead of the content.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        public float riseDistance = UITheme.RiseDistance;
        public float startScale = UITheme.StartScale;

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _restPosition;
        float _shown;
        bool _wantShown;
        bool _resting = true;

        public bool Visible { get { return _wantShown; } }

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
            _restPosition = _rect.anchoredPosition;
            Apply();
        }

        /// <summary>Call after moving the panel, so it rises back to the new place.</summary>
        public void MarkRestPosition()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _restPosition = _rect.anchoredPosition;
        }

        public void Show() { _wantShown = true; _resting = false; gameObject.SetActive(true); }

        public void Hide() { _wantShown = false; _resting = false; }

        public void ShowInstantly()
        {
            _wantShown = true;
            _shown = 1f;
            gameObject.SetActive(true);
            Apply();
        }

        public void HideInstantly()
        {
            _wantShown = false;
            _shown = 0f;
            Apply();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_resting) return;

            float target = _wantShown ? 1f : 0f;
            float speed = 1f / Mathf.Max(0.01f, _wantShown ? UITheme.FadeIn : UITheme.FadeOut);

            _shown = Mathf.MoveTowards(_shown, target, speed * Time.unscaledDeltaTime);
            Apply();

            if (!Mathf.Approximately(_shown, target)) return;

            _resting = true;
            if (!_wantShown) gameObject.SetActive(false);
        }

        void Apply()
        {
            if (_group == null) return;

            // Ease out, so it arrives softly rather than stopping dead.
            float eased = 1f - (1f - _shown) * (1f - _shown);

            _group.alpha = eased;
            _group.blocksRaycasts = _shown > 0.5f;

            if (_rect == null) return;
            _rect.anchoredPosition = _restPosition + new Vector2(0f, (1f - eased) * -riseDistance);
            _rect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
        }
    }
}
