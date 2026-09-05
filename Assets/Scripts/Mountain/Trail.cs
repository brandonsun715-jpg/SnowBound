using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Mountain
{
    /// <summary>
    /// How hard a run is. The category is a consequence of the terrain, not a
    /// label pasted on it: a trail is measured after it is cut and told what
    /// it turned out to be.
    /// </summary>
    public enum TrailGrade { Green, Blue, Black, DoubleBlack }

    /// <summary>What the snow on a run is like today.</summary>
    public enum SnowQuality { Packed, Powder, FreshPowder, Icy, Mixed }

    /// <summary>
    /// A run the player designed: a line down the mountain, a width, and the
    /// conditions on it.
    ///
    /// The centre line is a list of control points with a Catmull-Rom spline
    /// through them, resampled once into a dense spine. Everything else —
    /// carving the terrain, asking whether a point is on the run, guests
    /// following it, the length and grade figures — reads that spine, so
    /// there is one definition of where the run goes.
    /// </summary>
    [System.Serializable]
    public class Trail
    {
        public string name = "New Trail";
        public TrailGrade grade = TrailGrade.Blue;

        /// <summary>Control points, in world XZ. Ordered summit first, base last.</summary>
        public List<Vector2> points = new List<Vector2>();

        [Tooltip("Half the skiable width, in metres.")]
        public float halfWidth = 16f;

        public SnowQuality snow = SnowQuality.Packed;
        public bool groomed = true;
        public bool open = true;

        [Tooltip("How rough the surface of the run is left. Grade sets this.")]
        public float surfaceNoise = 1.1f;

        [Tooltip("Rollers along the run. Beginner and intermediate runs get them.")]
        public bool hasRollers = true;

        // ---- measured off the terrain once the run is cut ----

        public float length;          // metres along the snow
        public float drop;            // metres of vertical
        public float averageGrade;    // 0..1, rise over run
        public float maxGrade;        // 0..1, steepest stretch
        public float guestsToday;

        /// <summary>Dense centre line, filled by Resample. World space, y from the terrain.</summary>
        [System.NonSerialized] public List<Vector3> spine = new List<Vector3>();

        [System.NonSerialized] public Rect bounds;

        public const float SpineSpacing = 6f;

        // ---------------- shape presets ----------------------------------

        /// <summary>
        /// What a grade means in the terrain, rather than in the legend. The
        /// designer starts a run at these numbers; the player can then change
        /// the width and the line, and the run is re-measured and may end up
        /// classified as something else entirely.
        /// </summary>
        public static void ApplyGradeDefaults(Trail trail, TrailGrade grade)
        {
            trail.grade = grade;

            switch (grade)
            {
                case TrailGrade.Green:
                    trail.halfWidth = 26f;
                    trail.surfaceNoise = 0.35f;
                    trail.hasRollers = true;
                    trail.snow = SnowQuality.Packed;
                    trail.groomed = true;
                    break;

                case TrailGrade.Blue:
                    trail.halfWidth = 18f;
                    trail.surfaceNoise = 1.1f;
                    trail.hasRollers = true;
                    trail.snow = SnowQuality.Packed;
                    trail.groomed = true;
                    break;

                case TrailGrade.Black:
                    trail.halfWidth = 11f;
                    trail.surfaceNoise = 2.4f;
                    trail.hasRollers = false;
                    trail.snow = SnowQuality.Powder;
                    trail.groomed = false;
                    break;

                default:
                    trail.halfWidth = 7.5f;
                    trail.surfaceNoise = 3.6f;
                    trail.hasRollers = false;
                    trail.snow = SnowQuality.FreshPowder;
                    trail.groomed = false;
                    break;
            }
        }

        /// <summary>
        /// The grade the terrain says this is. Steepness decides most of it and
        /// width narrows the verdict, because a 30% pitch two metres wide is
        /// not the same run as a 30% pitch fifty metres wide.
        /// </summary>
        public static TrailGrade GradeFor(float averageGrade, float maxGrade, float halfWidth)
        {
            float pitch = Mathf.Max(averageGrade, maxGrade * 0.75f);
            float tight = Mathf.Clamp01((20f - halfWidth) / 16f);

            float score = pitch + tight * 0.09f;

            if (score < 0.17f) return TrailGrade.Green;
            if (score < 0.28f) return TrailGrade.Blue;
            if (score < 0.42f) return TrailGrade.Black;
            return TrailGrade.DoubleBlack;
        }

        public static string GradeName(TrailGrade grade)
        {
            switch (grade)
            {
                case TrailGrade.Green: return "GREEN";
                case TrailGrade.Blue: return "BLUE";
                case TrailGrade.Black: return "BLACK";
                default: return "DOUBLE BLACK";
            }
        }

        public static string SnowName(SnowQuality snow)
        {
            switch (snow)
            {
                case SnowQuality.Packed: return "PACKED";
                case SnowQuality.Powder: return "POWDER";
                case SnowQuality.FreshPowder: return "FRESH POWDER";
                case SnowQuality.Icy: return "ICY";
                default: return "MIXED";
            }
        }

        /// <summary>
        /// How the snow rides. Above 1 is quicker and looser underfoot, below
        /// 1 is slower and grippier. Grooming firms whatever is there.
        /// </summary>
        public float Grip
        {
            get
            {
                float grip;
                switch (snow)
                {
                    case SnowQuality.Icy: grip = 0.55f; break;
                    case SnowQuality.Packed: grip = 1f; break;
                    case SnowQuality.Powder: grip = 1.25f; break;
                    case SnowQuality.FreshPowder: grip = 1.5f; break;
                    default: grip = 0.9f; break;
                }

                return groomed ? Mathf.Lerp(grip, 1f, 0.45f) : grip;
            }
        }

        /// <summary>Drag from the snow itself. Deep snow is slow, ice is not.</summary>
        public float Drag
        {
            get
            {
                float drag;
                switch (snow)
                {
                    case SnowQuality.Icy: drag = 0.6f; break;
                    case SnowQuality.Packed: drag = 1f; break;
                    case SnowQuality.Powder: drag = 1.35f; break;
                    case SnowQuality.FreshPowder: drag = 1.75f; break;
                    default: drag = 1.1f; break;
                }

                return groomed ? drag * 0.85f : drag;
            }
        }

        /// <summary>0 to 1. How much guests enjoy the state this run is in.</summary>
        public float Appeal
        {
            get
            {
                float condition;
                switch (snow)
                {
                    case SnowQuality.FreshPowder: condition = 1f; break;
                    case SnowQuality.Powder: condition = 0.88f; break;
                    case SnowQuality.Packed: condition = 0.74f; break;
                    case SnowQuality.Mixed: condition = 0.52f; break;
                    default: condition = 0.3f; break;
                }

                if (groomed) condition = Mathf.Lerp(condition, 0.86f, 0.5f);
                if (!open) condition *= 0.2f;

                return Mathf.Clamp01(condition);
            }
        }

        // ---------------- geometry ---------------------------------------

        public bool Valid { get { return points != null && points.Count >= 2; } }

        /// <summary>
        /// Turn the control points into a dense centre line and hang the
        /// terrain heights on it. Call after any change to the points, and
        /// again after the terrain under them moves.
        /// </summary>
        public void Resample(System.Func<float, float, float> heightAt)
        {
            if (spine == null) spine = new List<Vector3>();
            spine.Clear();

            if (!Valid) { bounds = new Rect(); return; }

            // Catmull-Rom needs a point either side, so mirror the ends.
            int n = points.Count;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Mathf.Min(n - 1, i + 2)];

                if (i == 0) p0 = p1 + (p1 - p2);
                if (i == n - 2) p3 = p2 + (p2 - p1);

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / SpineSpacing));

                for (int s = 0; s < steps; s++)
                {
                    Vector2 flat = CatmullRom(p0, p1, p2, p3, s / (float)steps);
                    float y = heightAt != null ? heightAt(flat.x, flat.y) : 0f;
                    spine.Add(new Vector3(flat.x, y, flat.y));

                    if (flat.x < minX) minX = flat.x;
                    if (flat.x > maxX) maxX = flat.x;
                    if (flat.y < minZ) minZ = flat.y;
                    if (flat.y > maxZ) maxZ = flat.y;
                }
            }

            Vector2 last = points[n - 1];
            spine.Add(new Vector3(last.x, heightAt != null ? heightAt(last.x, last.y) : 0f, last.y));

            minX = Mathf.Min(minX, last.x); maxX = Mathf.Max(maxX, last.x);
            minZ = Mathf.Min(minZ, last.y); maxZ = Mathf.Max(maxZ, last.y);

            bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * ((2f * p1)
                         + (-p0 + p2) * t
                         + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                         + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Distance from the centre line, in metres. Negative is not returned:
        /// zero means dead centre. <paramref name="along"/> comes back as 0 at
        /// the summit end and 1 at the base end.
        /// </summary>
        public float DistanceTo(float x, float z, out float along)
        {
            along = 0f;
            if (spine == null || spine.Count < 2) return float.MaxValue;

            var target = new Vector2(x, z);
            float best = float.MaxValue;
            int bestIndex = 0;
            float bestT = 0f;

            for (int i = 0; i < spine.Count - 1; i++)
            {
                var a = new Vector2(spine[i].x, spine[i].z);
                var b = new Vector2(spine[i + 1].x, spine[i + 1].z);

                Vector2 ab = b - a;
                float lengthSq = ab.sqrMagnitude;

                float t = lengthSq < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(target - a, ab) / lengthSq);
                float d = Vector2.Distance(target, a + ab * t);

                if (d >= best) continue;

                best = d;
                bestIndex = i;
                bestT = t;
            }

            along = (bestIndex + bestT) / (spine.Count - 1);
            return best;
        }

        /// <summary>Point on the centre line, 0 at the summit end, 1 at the base.</summary>
        public Vector3 PointAt(float along)
        {
            if (spine == null || spine.Count == 0) return Vector3.zero;
            if (spine.Count == 1) return spine[0];

            float f = Mathf.Clamp01(along) * (spine.Count - 1);
            int i = Mathf.Min(spine.Count - 2, Mathf.FloorToInt(f));

            return Vector3.Lerp(spine[i], spine[i + 1], f - i);
        }

        public Vector3 Top { get { return PointAt(0f); } }
        public Vector3 Bottom { get { return PointAt(1f); } }

        /// <summary>
        /// Walk the finished run and write down what it actually turned out to
        /// be. The numbers are read off the terrain, so shaping the mountain
        /// under a run changes its grade.
        /// </summary>
        public void Measure(System.Func<float, float, float> heightAt)
        {
            length = 0f;
            drop = 0f;
            averageGrade = 0f;
            maxGrade = 0f;

            if (spine == null || spine.Count < 2) return;

            // Refresh the heights first: the ground may have moved.
            if (heightAt != null)
            {
                for (int i = 0; i < spine.Count; i++)
                {
                    Vector3 p = spine[i];
                    spine[i] = new Vector3(p.x, heightAt(p.x, p.z), p.z);
                }
            }

            float flat = 0f;
            float steepest = 0f;

            // Max grade over a stretch rather than between two neighbouring
            // samples, so one bump does not make a green run read as expert.
            const float window = 24f;
            int span = Mathf.Max(1, Mathf.RoundToInt(window / SpineSpacing));

            for (int i = 0; i < spine.Count - 1; i++)
            {
                Vector3 a = spine[i];
                Vector3 b = spine[i + 1];

                length += Vector3.Distance(a, b);
                flat += Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

                int j = Mathf.Min(spine.Count - 1, i + span);
                Vector3 c = spine[j];

                float run = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(c.x, c.z));
                if (run < 1f) continue;

                float rise = Mathf.Max(0f, a.y - c.y);
                steepest = Mathf.Max(steepest, rise / run);
            }

            drop = Mathf.Max(0f, spine[0].y - spine[spine.Count - 1].y);
            averageGrade = flat > 1f ? drop / flat : 0f;
            maxGrade = steepest;
        }

        public Trail Clone()
        {
            var copy = new Trail
            {
                name = name,
                grade = grade,
                halfWidth = halfWidth,
                snow = snow,
                groomed = groomed,
                open = open,
                surfaceNoise = surfaceNoise,
                hasRollers = hasRollers,
                points = new List<Vector2>(points)
            };

            return copy;
        }
    }
}
