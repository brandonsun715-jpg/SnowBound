using UnityEngine;
using UnityEngine.UI;

namespace SnowBound.Hud
{
    /// <summary>
    /// Constructs interface out of the design system rather than out of
    /// RectTransforms. Every panel gets the same fill, the same hairline and
    /// the same sheen along its top edge, which is the difference between a
    /// set of screens and a set of unrelated rectangles.
    /// </summary>
    public static class UIBuilder
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
            // Designed against 1600x900 rather than 1080p, so the whole
            // interface is a fifth larger relative to the window. Text that
            // survives a small window is worth more than text that is
            // perfectly proportioned on a monitor nobody is using.
            scaler.referenceResolution = new Vector2(1600f, 900f);
            // Match height rather than width, so an ultrawide window gains
            // empty space at the sides instead of shrinking everything.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            return canvas;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        /// <summary>Anchor to one point and size explicitly. Predictable at any resolution.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
                                          Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>A pane of dark glass: fill, hairline border, and a lit top edge.</summary>
        public static RectTransform Glass(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                          Vector2 offset, Vector2 size,
                                          int radius = UITheme.Radius, Color? tint = null)
        {
            RectTransform root = Place(Node(parent, name), anchor, pivot, offset, size);

            var fill = root.gameObject.AddComponent<Image>();
            fill.sprite = UISprites.Fill(radius);
            fill.type = Image.Type.Sliced;
            fill.color = tint.HasValue ? tint.Value : UITheme.Glass;
            fill.raycastTarget = false;

            var border = Stretch(Node(root, "Hairline")).gameObject.AddComponent<Image>();
            border.sprite = UISprites.Outline(radius, 1);
            border.type = Image.Type.Sliced;
            border.color = UITheme.Hairline;
            border.raycastTarget = false;

            // The bright inside edge along the top. One line, and the panel
            // stops reading as a flat rectangle and starts reading as glass.
            RectTransform sheen = Node(root, "Sheen");
            sheen.anchorMin = new Vector2(0f, 1f);
            sheen.anchorMax = new Vector2(1f, 1f);
            sheen.pivot = new Vector2(0.5f, 1f);
            sheen.sizeDelta = new Vector2(-radius * 2f, 1.2f);
            sheen.anchoredPosition = new Vector2(0f, -1.4f);

            var sheenImage = sheen.gameObject.AddComponent<Image>();
            sheenImage.sprite = UISprites.Pixel;
            sheenImage.color = UITheme.Sheen;
            sheenImage.raycastTarget = false;

            return root;
        }

        public static Image Solid(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                  Vector2 offset, Vector2 size, Color colour, int radius = 0)
        {
            RectTransform rect = Place(Node(parent, name), anchor, pivot, offset, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = radius > 0 ? UISprites.Fill(radius) : UISprites.Pixel;
            image.type = radius > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;

            return image;
        }

        public static Text Label(Transform parent, string name, int size, Color colour,
                                 TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;

            return text;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Color colour,
                                 Vector2 anchor, Vector2 pivot, Vector2 offset, float size)
        {
            RectTransform rect = Place(Node(parent, name), anchor, pivot, offset,
                                       new Vector2(size, size));

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>A hairline rule, for separating groups inside a panel.</summary>
        public static Image Rule(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                 Vector2 offset, float width)
        {
            return Solid(parent, name, anchor, pivot, offset, new Vector2(width, 1f), UITheme.Hairline);
        }
    }
}
