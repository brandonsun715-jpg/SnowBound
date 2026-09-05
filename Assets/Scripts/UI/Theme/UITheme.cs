using UnityEngine;

namespace SnowBound.Hud
{
    /// <summary>
    /// The one place the interface's identity is defined: premium alpine
    /// resort seen through dark glass. Every panel, label and icon in the
    /// game reads its colours, sizes and spacing from here, which is what
    /// stops the UI drifting into a pile of unrelated canvases.
    ///
    /// The world is white and bright, so surfaces are dark and translucent
    /// and text sits on top of them rather than on the snow. Blue is an
    /// accent and never a background: the mountain supplies the colour.
    /// </summary>
    public static class UITheme
    {
        // ---- surfaces, darkest to lightest -------------------------------

        /// <summary>Full-screen dim behind a modal.</summary>
        public static readonly Color Scrim = new Color(0.02f, 0.03f, 0.05f, 0.66f);
        /// <summary>The standard glass panel.</summary>
        public static readonly Color Glass = new Color(0.055f, 0.072f, 0.100f, 0.82f);
        /// <summary>A card sitting on glass.</summary>
        public static readonly Color Card = new Color(0.094f, 0.116f, 0.150f, 0.90f);
        /// <summary>A card under the cursor.</summary>
        public static readonly Color CardHover = new Color(0.135f, 0.163f, 0.205f, 0.94f);
        /// <summary>A card being pressed or held.</summary>
        public static readonly Color CardActive = new Color(0.170f, 0.205f, 0.255f, 0.96f);

        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color HairlineBright = new Color(1f, 1f, 1f, 0.20f);
        /// <summary>The bright inside edge along the top of a pane of glass.</summary>
        public static readonly Color Sheen = new Color(1f, 1f, 1f, 0.13f);

        // ---- type --------------------------------------------------------

        public static readonly Color Ink = new Color(0.960f, 0.972f, 0.990f, 1f);
        public static readonly Color InkMuted = new Color(0.640f, 0.690f, 0.760f, 1f);
        public static readonly Color InkFaint = new Color(0.450f, 0.495f, 0.560f, 1f);

        // ---- accents -----------------------------------------------------

        public static readonly Color Ice = new Color(0.560f, 0.780f, 0.950f, 1f);
        public static readonly Color Positive = new Color(0.470f, 0.820f, 0.590f, 1f);
        public static readonly Color Warning = new Color(0.930f, 0.750f, 0.420f, 1f);
        public static readonly Color Negative = new Color(0.900f, 0.470f, 0.450f, 1f);

        // ---- trail grades, muted rather than saturated --------------------

        public static readonly Color GradeGreen = new Color(0.42f, 0.74f, 0.48f, 1f);
        public static readonly Color GradeBlue = new Color(0.40f, 0.66f, 0.92f, 1f);
        public static readonly Color GradeRed = new Color(0.88f, 0.44f, 0.42f, 1f);
        public static readonly Color GradeBlack = new Color(0.72f, 0.74f, 0.80f, 1f);

        // ---- type scale --------------------------------------------------

        public const int Display = 76;
        public const int Hero = 48;
        public const int Title = 32;
        public const int Heading = 23;
        public const int Body = 19;
        public const int Label = 14;
        public const int Micro = 12;

        // ---- rhythm ------------------------------------------------------

        /// <summary>Distance from the edge of the screen to anything.</summary>
        public const float Margin = 40f;
        /// <summary>Inside a panel.</summary>
        public const float Pad = 22f;
        /// <summary>Between related things.</summary>
        public const float Gap = 12f;
        /// <summary>Between groups.</summary>
        public const float Group = 26f;

        public const int Radius = 14;
        public const int RadiusSmall = 9;

        // ---- motion ------------------------------------------------------

        /// <summary>Panels open fast and settle. Nothing bounces.</summary>
        public const float FadeIn = 0.18f;
        public const float FadeOut = 0.14f;
        public const float RiseDistance = 14f;
        public const float StartScale = 0.985f;

        /// <summary>Frame-rate independent ease used everywhere.</summary>
        public static float Approach(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }

        /// <summary>
        /// Letter spacing, which uGUI text has no property for. Thin spaces
        /// between characters is the oldest trick there is and it is what
        /// makes a plain sans-serif read as a considered heading.
        /// </summary>
        public static string Track(string text, int amount = 1)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string spacer = new string(' ', Mathf.Max(1, amount));
            var built = new System.Text.StringBuilder(text.Length * 2);

            for (int i = 0; i < text.Length; i++)
            {
                built.Append(text[i]);
                if (i < text.Length - 1) built.Append(spacer);
            }

            return built.ToString();
        }
    }
}
