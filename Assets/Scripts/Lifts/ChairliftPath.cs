using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Lifts
{
    /// <summary>
    /// The closed loop the cable runs around: up one side of the line, across
    /// the top bullwheel, back down the other side, across the bottom one.
    ///
    /// Chairs are placed by a single number, how far around the loop they are,
    /// which is what makes "has a chair reached the loading point yet?" a
    /// one-line question instead of a geometry problem.
    /// </summary>
    public class ChairliftPath
    {
        readonly List<Vector3> _nodes = new List<Vector3>();
        readonly List<float> _distance = new List<float>();

        /// <summary>Distance all the way round the loop.</summary>
        public float Length { get; private set; }

        /// <summary>Distance from the bottom terminal to the top one.</summary>
        public float UpLength { get; private set; }

        public int Count { get { return _nodes.Count; } }
        public Vector3 Node(int i) { return _nodes[i]; }

        /// <summary>
        /// <paramref name="line"/> is the centre line of the lift, bottom
        /// first. The uphill and downhill cables are placed either side of it.
        /// </summary>
        public void Build(IList<Vector3> line, float trackSpacing)
        {
            _nodes.Clear();
            _distance.Clear();
            if (line == null || line.Count < 2) return;

            float half = trackSpacing * 0.5f;

            for (int i = 0; i < line.Count; i++)
                _nodes.Add(line[i] - Sideways(line, i) * half);

            int upNodes = _nodes.Count;

            for (int i = line.Count - 1; i >= 0; i--)
                _nodes.Add(line[i] + Sideways(line, i) * half);

            float total = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _distance.Add(total);
                total += Vector3.Distance(_nodes[i], _nodes[(i + 1) % _nodes.Count]);
            }

            Length = total;
            UpLength = _distance[upNodes - 1];
        }

        static Vector3 Sideways(IList<Vector3> line, int i)
        {
            Vector3 a = line[Mathf.Max(0, i - 1)];
            Vector3 b = line[Mathf.Min(line.Count - 1, i + 1)];

            Vector3 direction = b - a;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            return Vector3.Cross(Vector3.up, direction.normalized);
        }

        /// <summary>Position <paramref name="s"/> metres around the loop.</summary>
        public Vector3 Sample(float s, out Vector3 tangent)
        {
            tangent = Vector3.forward;
            if (_nodes.Count < 2) return Vector3.zero;

            s = Mathf.Repeat(s, Length);

            int index = 0;
            for (int k = _nodes.Count - 1; k >= 0; k--)
            {
                if (_distance[k] <= s) { index = k; break; }
            }

            Vector3 a = _nodes[index];
            Vector3 b = _nodes[(index + 1) % _nodes.Count];

            float span = Vector3.Distance(a, b);
            if (span < 0.0001f) return a;

            tangent = (b - a) / span;
            return Vector3.Lerp(a, b, (s - _distance[index]) / span);
        }

        public Vector3 Sample(float s)
        {
            Vector3 ignored;
            return Sample(s, out ignored);
        }

        /// <summary>
        /// Shortest signed distance from one point on the loop to another.
        /// Handles the wrap, so a chair just past the end still reads as close
        /// to a point just before it.
        /// </summary>
        public float Gap(float from, float to)
        {
            if (Length <= 0f) return 0f;
            return Mathf.Repeat(to - from + Length * 0.5f, Length) - Length * 0.5f;
        }
    }
}
