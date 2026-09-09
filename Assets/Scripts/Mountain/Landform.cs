using UnityEngine;

namespace SnowBound.Mountain
{
    /// <summary>The kinds of thing a mountain is made of.</summary>
    public enum LandformKind
    {
        /// <summary>A summit. Radial, steepening towards the top.</summary>
        Peak,
        /// <summary>A ridge or arête between two points.</summary>
        Ridge,
        /// <summary>A bowl or cirque: scooped out, with a rim around it.</summary>
        Bowl,
        /// <summary>A gully, chute or couloir cut down the hill.</summary>
        Gully,
        /// <summary>A shelf. Everything inside it is pulled towards one level.</summary>
        Bench,
        /// <summary>A cliff band: a step in the hillside along a line.</summary>
        Cliff,
        /// <summary>A shoulder running off a peak, losing height as it goes.</summary>
        Spur
    }

    /// <summary>
    /// One deliberate feature of the mountain.
    ///
    /// Noise alone makes terrain that is everywhere the same: however many
    /// octaves it has, every part of it has the same statistics as every other
    /// part, so there is nowhere to recognise and nothing to plan around. A
    /// real ski area is a composition — this summit, that bowl, the ridge
    /// between them, the chute off the back — and those are places, with names,
    /// that a player learns.
    ///
    /// So the mountain is authored as a list of these and the noise is only
    /// the texture on top. Each one is applied in order to the height under it,
    /// which is what lets a bench flatten what a peak raised and a cliff cut
    /// across both.
    /// </summary>
    [System.Serializable]
    public class Landform
    {
        public string name = "Feature";
        public LandformKind kind = LandformKind.Peak;

        [Tooltip("Where it is, in world XZ.")]
        public Vector2 at;
        [Tooltip("The far end, for the kinds that run between two points.")]
        public Vector2 to;

        [Tooltip("How far its influence reaches, in metres.")]
        public float radius = 120f;
        [Tooltip("Metres it raises, lowers or steps. Negative flips it.")]
        public float strength = 60f;
        [Tooltip("Above one it comes to a point; below one it is a dome.")]
        public float sharpness = 1.6f;

        [Tooltip("For a bench: the height it flattens towards. Zero means read it off the middle.")]
        public float level;

        /// <summary>
        /// The level a bench actually settles at, worked out once by the
        /// generator from the features above it in the list. Without this a
        /// bench would have to flatten towards a number typed in by hand, and
        /// that number would be wrong the moment a peak beside it moved.
        /// </summary>
        [System.NonSerialized] public float resolvedLevel;

        /// <summary>Everything that reads the mountain in one direction.</summary>
        public Vector2 Along
        {
            get
            {
                Vector2 run = to - at;
                return run.sqrMagnitude < 0.001f ? Vector2.up : run.normalized;
            }
        }

        // ---------------- evaluation ----------------------------------------

        /// <summary>
        /// Apply this feature to the ground under it. Takes the height so far
        /// and returns the height after, because not every landform is
        /// additive: a bench flattens, and a cliff steps.
        /// </summary>
        public float Apply(float h, float x, float z, System.Func<float, float, float> baseAt)
        {
            switch (kind)
            {
                case LandformKind.Peak: return h + Peak(x, z);
                case LandformKind.Ridge: return h + Ridge(x, z, false);
                case LandformKind.Spur: return h + Ridge(x, z, true);
                case LandformKind.Bowl: return h + Bowl(x, z);
                case LandformKind.Gully: return h + Gully(x, z);
                case LandformKind.Bench: return Bench(h, x, z, baseAt);
                default: return Cliff(h, x, z);
            }
        }

        float Falloff(float distance)
        {
            float t = Mathf.Clamp01(distance / Mathf.Max(1f, radius));
            float k = 1f - t;

            // Smoothstep at the edge so a feature meets the mountain rather
            // than sitting on it with a visible seam.
            return k * k * (3f - 2f * k);
        }

        float Peak(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), at);
            if (d > radius) return 0f;

            return Mathf.Pow(Falloff(d), sharpness) * strength;
        }

        /// <summary>Distance from the line this feature runs along, and how far down it.</summary>
        float ToSegment(float x, float z, out float along)
        {
            Vector2 a = at, b = to;
            Vector2 run = b - a;

            float length = run.sqrMagnitude;

            along = length < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, run) / length);
            return Vector2.Distance(new Vector2(x, z), a + run * along);
        }

        float Ridge(float x, float z, bool taper)
        {
            float along;
            float d = ToSegment(x, z, out along);
            if (d > radius) return 0f;

            float crest = Mathf.Pow(Falloff(d), sharpness);

            // A spur loses height as it runs away from the peak it comes off.
            if (taper) crest *= 1f - along * 0.85f;

            // Ends taper too, so a ridge does not stop dead.
            crest *= Mathf.SmoothStep(0f, 1f, Mathf.Min(along, 1f - along) * 6f + 0.15f);

            return crest * strength;
        }

        /// <summary>
        /// A cirque: scooped out in the middle with a rim around the edge,
        /// which is the shape a glacier leaves and the reason bowls ski well.
        /// </summary>
        float Bowl(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), at);
            if (d > radius) return 0f;

            float t = d / radius;

            float scoop = -(1f - t * t) * strength;
            float rim = Mathf.Exp(-Mathf.Pow((t - 0.86f) / 0.13f, 2f)) * strength * 0.55f;

            return scoop + rim;
        }

        /// <summary>A cut down the hill, with the shoulders left standing either side.</summary>
        float Gully(float x, float z)
        {
            float along;
            float d = ToSegment(x, z, out along);
            if (d > radius) return 0f;

            float t = d / radius;

            // Cut in the middle, shoulders raised at the edges.
            float cut = -Mathf.Pow(1f - t, sharpness) * strength;
            float shoulder = Mathf.Exp(-Mathf.Pow((t - 0.82f) / 0.16f, 2f)) * strength * 0.35f;

            // Deepest in the middle of its run, fading at both ends.
            float run = Mathf.SmoothStep(0f, 1f, Mathf.Min(along, 1f - along) * 4.5f);

            return (cut + shoulder) * run;
        }

        /// <summary>
        /// A shelf. Everything inside is pulled towards one level, which is how
        /// a mid station, a lift terminal or a natural terrace reads.
        /// </summary>
        float Bench(float h, float x, float z, System.Func<float, float, float> baseAt)
        {
            float d = Vector2.Distance(new Vector2(x, z), at);
            if (d > radius) return h;

            float flat = Mathf.Abs(level) > 0.001f ? level : resolvedLevel;
            if (Mathf.Abs(flat) < 0.001f && baseAt != null) flat = baseAt(at.x, at.y);
            if (Mathf.Abs(flat) < 0.001f) return h;

            // Strength reads as how completely it flattens, 0 to 1.
            float pull = Falloff(d) * Mathf.Clamp01(strength);

            return Mathf.Lerp(h, flat, pull);
        }

        /// <summary>
        /// A step in the hillside along a line: the uphill side stays, the
        /// downhill side drops away, and the transition is short enough to
        /// read as rock rather than as a steeper slope.
        /// </summary>
        float Cliff(float h, float x, float z)
        {
            Vector2 along = Along;
            var across = new Vector2(-along.y, along.x);

            Vector2 offset = new Vector2(x, z) - at;

            float side = Vector2.Dot(offset, across);
            float down = Vector2.Dot(offset, along);

            // Only along the length of the band.
            float span = Vector2.Distance(at, to);
            if (down < 0f || down > span) return h;

            float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(down, span - down) / Mathf.Max(1f, radius));

            // Short transition across the face. sharpness sets how short.
            float band = Mathf.Max(2f, radius / Mathf.Max(1f, sharpness));
            float step = Mathf.SmoothStep(-1f, 1f, side / band);

            return h - step * strength * ends;
        }
    }
}
