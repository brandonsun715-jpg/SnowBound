using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Hud
{
    /// <summary>
    /// A consistent line-icon set, rasterised from distance fields.
    ///
    /// Mixing icon styles is the fastest way to make an interface look
    /// assembled rather than designed, so every icon here is drawn by the
    /// same pen, at the same weight, on the same grid. Coverage is
    /// accumulated as a distance to the nearest stroke and then softened by
    /// exactly one pixel, which is what keeps them crisp at small sizes.
    /// </summary>
    public static class UIIcons
    {
        public const int Size = 64;

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Cash { get { return Get("cash", DrawCash); } }
        public static Sprite Guests { get { return Get("guests", DrawGuests); } }
        public static Sprite Star { get { return Get("star", p => DrawStar(p, true)); } }
        public static Sprite StarHollow { get { return Get("starHollow", p => DrawStar(p, false)); } }
        public static Sprite Sun { get { return Get("sun", DrawSun); } }
        public static Sprite Cloud { get { return Get("cloud", DrawCloud); } }
        public static Sprite Snow { get { return Get("snow", DrawSnow); } }
        public static Sprite Storm { get { return Get("storm", DrawStorm); } }
        public static Sprite Lift { get { return Get("lift", DrawLift); } }
        public static Sprite Lodge { get { return Get("lodge", DrawLodge); } }
        public static Sprite Park { get { return Get("park", DrawPark); } }
        public static Sprite Mountain { get { return Get("mountain", DrawMountain); } }
        public static Sprite ArrowUp { get { return Get("arrowUp", DrawArrowUp); } }
        public static Sprite Clock { get { return Get("clock", DrawClock); } }

        public static Sprite Weather(float storminess)
        {
            if (storminess > 0.68f) return Storm;
            if (storminess > 0.40f) return Snow;
            if (storminess > 0.22f) return Cloud;
            return Sun;
        }

        // ---------------- the pen -----------------------------------------

        /// <summary>Draws into a coverage buffer using shapes, not pixels.</summary>
        public class Pen
        {
            public readonly float[] coverage = new float[Size * Size];

            /// <summary>Everything is specified in a 0-1 square, y upwards.</summary>
            public void Line(Vector2 a, Vector2 b, float weight = 0.055f)
            {
                Stamp(p => SegmentDistance(p, a * Size, b * Size) - weight * Size * 0.5f);
            }

            public void Ring(Vector2 centre, float radius, float weight = 0.055f)
            {
                Stamp(p => Mathf.Abs(Vector2.Distance(p, centre * Size) - radius * Size)
                           - weight * Size * 0.5f);
            }

            public void Disc(Vector2 centre, float radius)
            {
                Stamp(p => Vector2.Distance(p, centre * Size) - radius * Size);
            }

            public void Polygon(Vector2[] points, bool filled, float weight = 0.055f)
            {
                Stamp(p =>
                {
                    float edge = float.MaxValue;
                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector2 a = points[i] * Size;
                        Vector2 b = points[(i + 1) % points.Length] * Size;
                        edge = Mathf.Min(edge, SegmentDistance(p, a, b));
                    }

                    if (!filled) return edge - weight * Size * 0.5f;
                    return Inside(points, p / Size) ? -edge : edge;
                });
            }

            void Stamp(System.Func<Vector2, float> distance)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float alpha = Mathf.Clamp01(0.5f - distance(p));
                        int i = y * Size + x;
                        if (alpha > coverage[i]) coverage[i] = alpha;
                    }
                }
            }

            static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float lengthSquared = ab.sqrMagnitude;
                if (lengthSquared < 0.0001f) return Vector2.Distance(p, a);

                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                return Vector2.Distance(p, a + ab * t);
            }

            static bool Inside(Vector2[] points, Vector2 p)
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    if ((points[i].y > p.y) == (points[j].y > p.y)) continue;

                    float crossing = (points[j].x - points[i].x) * (p.y - points[i].y)
                                     / (points[j].y - points[i].y) + points[i].x;
                    if (p.x < crossing) inside = !inside;
                }
                return inside;
            }
        }

        static Sprite Get(string key, System.Action<Pen> draw)
        {
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            var pen = new Pen();
            draw(pen);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 1f, 1f, pen.coverage[i]);

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f),
                                          100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;

            _cache[key] = sprite;
            return sprite;
        }

        // ---------------- the set -----------------------------------------

        static void DrawCash(Pen p)
        {
            p.Ring(new Vector2(0.5f, 0.5f), 0.33f);
            p.Line(new Vector2(0.5f, 0.20f), new Vector2(0.5f, 0.80f), 0.048f);
            p.Line(new Vector2(0.62f, 0.63f), new Vector2(0.38f, 0.63f), 0.048f);
            p.Line(new Vector2(0.38f, 0.63f), new Vector2(0.38f, 0.50f), 0.048f);
            p.Line(new Vector2(0.38f, 0.50f), new Vector2(0.62f, 0.50f), 0.048f);
            p.Line(new Vector2(0.62f, 0.50f), new Vector2(0.62f, 0.37f), 0.048f);
            p.Line(new Vector2(0.62f, 0.37f), new Vector2(0.38f, 0.37f), 0.048f);
        }

        static void DrawGuests(Pen p)
        {
            p.Ring(new Vector2(0.42f, 0.68f), 0.15f);
            p.Line(new Vector2(0.22f, 0.20f), new Vector2(0.26f, 0.40f));
            p.Line(new Vector2(0.26f, 0.40f), new Vector2(0.58f, 0.40f));
            p.Line(new Vector2(0.58f, 0.40f), new Vector2(0.62f, 0.20f));
            p.Ring(new Vector2(0.74f, 0.72f), 0.11f, 0.05f);
            p.Line(new Vector2(0.66f, 0.48f), new Vector2(0.86f, 0.48f), 0.05f);
            p.Line(new Vector2(0.86f, 0.48f), new Vector2(0.88f, 0.30f), 0.05f);
        }

        static void DrawStar(Pen p, bool filled)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i % 2 == 0) ? 0.40f : 0.17f;
                points[i] = new Vector2(0.5f + Mathf.Cos(angle) * radius,
                                        0.5f + Mathf.Sin(angle) * radius);
            }
            p.Polygon(points, filled, 0.05f);
        }

        static void DrawSun(Pen p)
        {
            p.Ring(new Vector2(0.5f, 0.5f), 0.21f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                p.Line(new Vector2(0.5f, 0.5f) + direction * 0.30f,
                       new Vector2(0.5f, 0.5f) + direction * 0.42f, 0.05f);
            }
        }

        static void DrawCloud(Pen p)
        {
            p.Ring(new Vector2(0.37f, 0.52f), 0.16f);
            p.Ring(new Vector2(0.58f, 0.57f), 0.20f);
            p.Line(new Vector2(0.28f, 0.38f), new Vector2(0.72f, 0.38f));
        }

        static void DrawSnow(Pen p)
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.36f;
                p.Line(new Vector2(0.5f, 0.5f) - direction, new Vector2(0.5f, 0.5f) + direction, 0.05f);

                var tip = new Vector2(0.5f, 0.5f) + direction;
                var arm = new Vector2(-direction.y, direction.x).normalized * 0.10f;
                p.Line(tip, tip - direction.normalized * 0.12f + arm, 0.045f);
                p.Line(tip, tip - direction.normalized * 0.12f - arm, 0.045f);
            }
        }

        static void DrawStorm(Pen p)
        {
            p.Ring(new Vector2(0.38f, 0.64f), 0.14f);
            p.Ring(new Vector2(0.60f, 0.68f), 0.17f);
            p.Line(new Vector2(0.28f, 0.53f), new Vector2(0.74f, 0.53f));
            p.Polygon(new[]
            {
                new Vector2(0.54f, 0.46f), new Vector2(0.42f, 0.24f),
                new Vector2(0.50f, 0.26f), new Vector2(0.44f, 0.08f),
                new Vector2(0.60f, 0.30f), new Vector2(0.51f, 0.29f)
            }, true);
        }

        static void DrawLift(Pen p)
        {
            p.Line(new Vector2(0.10f, 0.78f), new Vector2(0.90f, 0.58f), 0.045f);
            p.Line(new Vector2(0.34f, 0.70f), new Vector2(0.34f, 0.52f), 0.045f);
            p.Line(new Vector2(0.22f, 0.52f), new Vector2(0.46f, 0.52f), 0.05f);
            p.Line(new Vector2(0.22f, 0.52f), new Vector2(0.22f, 0.38f), 0.05f);
            p.Line(new Vector2(0.72f, 0.65f), new Vector2(0.72f, 0.14f), 0.05f);
            p.Line(new Vector2(0.58f, 0.14f), new Vector2(0.86f, 0.14f), 0.05f);
        }

        static void DrawLodge(Pen p)
        {
            p.Line(new Vector2(0.14f, 0.52f), new Vector2(0.50f, 0.82f));
            p.Line(new Vector2(0.50f, 0.82f), new Vector2(0.86f, 0.52f));
            p.Line(new Vector2(0.22f, 0.50f), new Vector2(0.22f, 0.18f), 0.05f);
            p.Line(new Vector2(0.78f, 0.50f), new Vector2(0.78f, 0.18f), 0.05f);
            p.Line(new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.18f), 0.05f);
            p.Line(new Vector2(0.44f, 0.18f), new Vector2(0.44f, 0.44f), 0.045f);
            p.Line(new Vector2(0.44f, 0.44f), new Vector2(0.58f, 0.44f), 0.045f);
            p.Line(new Vector2(0.58f, 0.44f), new Vector2(0.58f, 0.18f), 0.045f);
        }

        static void DrawPark(Pen p)
        {
            p.Line(new Vector2(0.08f, 0.24f), new Vector2(0.40f, 0.24f), 0.05f);
            p.Line(new Vector2(0.40f, 0.24f), new Vector2(0.56f, 0.62f), 0.05f);
            p.Line(new Vector2(0.56f, 0.62f), new Vector2(0.56f, 0.24f), 0.05f);
            p.Line(new Vector2(0.66f, 0.44f), new Vector2(0.92f, 0.44f), 0.05f);
            p.Line(new Vector2(0.70f, 0.44f), new Vector2(0.70f, 0.24f), 0.042f);
            p.Line(new Vector2(0.88f, 0.44f), new Vector2(0.88f, 0.24f), 0.042f);
        }

        static void DrawMountain(Pen p)
        {
            p.Line(new Vector2(0.06f, 0.26f), new Vector2(0.38f, 0.76f));
            p.Line(new Vector2(0.38f, 0.76f), new Vector2(0.58f, 0.46f));
            p.Line(new Vector2(0.58f, 0.46f), new Vector2(0.72f, 0.66f));
            p.Line(new Vector2(0.72f, 0.66f), new Vector2(0.94f, 0.26f));
            p.Line(new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.26f), 0.05f);
        }

        static void DrawArrowUp(Pen p)
        {
            p.Line(new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.82f));
            p.Line(new Vector2(0.24f, 0.58f), new Vector2(0.5f, 0.84f));
            p.Line(new Vector2(0.76f, 0.58f), new Vector2(0.5f, 0.84f));
        }

        static void DrawClock(Pen p)
        {
            p.Ring(new Vector2(0.5f, 0.5f), 0.36f);
            p.Line(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.72f), 0.05f);
            p.Line(new Vector2(0.5f, 0.5f), new Vector2(0.66f, 0.42f), 0.05f);
        }
    }
}
