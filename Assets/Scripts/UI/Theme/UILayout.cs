using UnityEngine;

namespace SnowBound.Hud
{
    /// <summary>
    /// Where things are allowed to be.
    ///
    /// Every canvas matches height, so the design is always 900 units tall and
    /// its width is 900 times the aspect ratio: 1200 on a 4:3 window, 1600 on
    /// 16:9, 2100 on an ultrawide. Anything laid out at a fixed distance from
    /// the left edge therefore has to fit inside the narrowest of those, which
    /// is the single rule that keeps the interface from overlapping itself on
    /// somebody else's monitor.
    ///
    /// So the screen is divided into bands, each panel is anchored to the edge
    /// it belongs to, and nothing is ever wider than SafeWidth.
    /// </summary>
    public static class UILayout
    {
        /// <summary>Design height. Fixed, because every canvas matches height.</summary>
        public const float Height = 900f;

        /// <summary>
        /// The narrowest design width worth supporting: a 4:3 window. Fixed
        /// layouts are sized against this, so they still fit when the window
        /// is square-ish and simply sit further apart when it is wide.
        /// </summary>
        public const float NarrowWidth = 1200f;

        public const float Margin = 26f;

        /// <summary>Widest a centred, fixed-width panel may be.</summary>
        public const float SafeWidth = NarrowWidth - Margin * 2f;

        // ---- bands -------------------------------------------------------

        /// <summary>The strip of figures across the top.</summary>
        public const float TopBarHeight = 76f;

        /// <summary>Everything below the top bar starts here.</summary>
        public const float UnderTopBar = Margin * 2f + TopBarHeight;

        /// <summary>
        /// The row of tabs along the bottom. This is always on screen in
        /// management mode: it is the only way into the tools, so hiding it
        /// until a tool is open would leave nothing to click.
        /// </summary>
        public const float TabBarHeight = 48f;

        /// <summary>Gap between the tab row and the panel it opens.</summary>
        public const float TabGap = 8f;

        /// <summary>The tool dock, which opens above the tabs.</summary>
        public const float DockHeight = 310f;

        /// <summary>Bottom of the dock panel, measured from the screen bottom.</summary>
        public const float DockBottom = Margin + TabBarHeight + TabGap;

        /// <summary>Everything above the dock stops here.</summary>
        public const float AboveDock = DockBottom + DockHeight + Margin * 0.5f;

        /// <summary>The inspector rail down the right.</summary>
        public const float RailWidth = 336f;

        /// <summary>Tallest the right-hand rail can be without meeting the dock.</summary>
        public const float RailHeight = Height - UnderTopBar - AboveDock;

        // ---- helpers ------------------------------------------------------

        /// <summary>Fit a row of n items of the given width into the safe width.</summary>
        public static float RowStride(int count, float itemWidth, float gap)
        {
            if (count <= 0) return itemWidth + gap;

            float wanted = count * (itemWidth + gap) - gap;
            if (wanted <= SafeWidth) return itemWidth + gap;

            // Too many to fit at full size, so shrink them rather than let the
            // row run off the side of the screen.
            return (SafeWidth + gap) / count;
        }

        /// <summary>Left edge of item i in a centred row, measured from the row's centre.</summary>
        public static float RowOffset(int index, int count, float stride)
        {
            float span = stride * count;
            return -span * 0.5f + stride * index;
        }
    }
}
