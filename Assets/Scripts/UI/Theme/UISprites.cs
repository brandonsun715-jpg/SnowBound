using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Hud
{
    /// <summary>
    /// Rounded panels and hairline borders, drawn in code.
    ///
    /// Unity's default UI is square because a rounded corner needs a sprite,
    /// and a sprite normally means an imported image. Rasterising the shape
    /// from its distance field instead gives clean anti-aliased corners at
    /// any radius, and nine-slicing them means one small texture stretches to
    /// any panel without ever going soft.
    /// </summary>
    public static class UISprites
    {
        static readonly Dictionary<int, Sprite> _fills = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> _outlines = new Dictionary<int, Sprite>();
        static Sprite _pixel;

        /// <summary>A plain white pixel, for rules, bars and flat fills.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;

                var tex = New(2, 2);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();

                _pixel = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f,
                                       0, SpriteMeshType.FullRect);
                _pixel.hideFlags = HideFlags.DontSave;
                return _pixel;
            }
        }

        /// <summary>A solid rounded rectangle, nine-sliced on its corners.</summary>
        public static Sprite Fill(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 64);

            Sprite cached;
            if (_fills.TryGetValue(radius, out cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var tex = New(size, size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBox(x, y, size, radius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Slice(tex, size, radius);
            _fills[radius] = sprite;
            return sprite;
        }

        /// <summary>A rounded rectangle drawn as a thin ring: the hairline border.</summary>
        public static Sprite Outline(int radius, int thickness = 1)
        {
            radius = Mathf.Clamp(radius, 1, 64);
            thickness = Mathf.Clamp(thickness, 1, 8);

            int key = radius * 16 + thickness;

            Sprite cached;
            if (_outlines.TryGetValue(key, out cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var tex = New(size, size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBox(x, y, size, radius);

                    // Inside the outer edge, outside the inner one.
                    float outer = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f + d + thickness);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, outer * inner);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Slice(tex, size, radius);
            _outlines[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Signed distance to a rounded box: negative inside, zero on the
        /// edge. One pixel of that distance is exactly one pixel of
        /// anti-aliasing, which is why the corners come out clean.
        /// </summary>
        static float RoundedBox(int x, int y, int size, int radius)
        {
            float half = size * 0.5f;
            float px = x + 0.5f - half;
            float py = y + 0.5f - half;

            float inner = half - radius;
            float qx = Mathf.Max(Mathf.Abs(px) - inner, 0f);
            float qy = Mathf.Max(Mathf.Abs(py) - inner, 0f);

            return Mathf.Sqrt(qx * qx + qy * qy) - radius;
        }

        static Sprite Slice(Texture2D tex, int size, int radius)
        {
            var border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                          100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static Texture2D New(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }
    }
}
