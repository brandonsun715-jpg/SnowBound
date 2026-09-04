using UnityEngine;
using UnityEngine.UI;

namespace SnowBound.Hud
{
    /// <summary>
    /// The plumbing every screen needs: a canvas that scales, text that can
    /// be read against snow, and flat panels. Kept in one place so the HUDs
    /// stay about what they say rather than how to build a RectTransform.
    /// </summary>
    public static class HudFactory
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { _font = null; }

                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch { _font = null; }
                }

                return _font;
            }
        }

        public static Canvas Canvas(Transform parent, string name, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static Text Label(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                 Vector2 offset, Vector2 size, TextAnchor align, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;

            // Snow is white, so the text needs something behind it.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                  Vector2 offset, Vector2 size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;

            return image;
        }
    }
}
