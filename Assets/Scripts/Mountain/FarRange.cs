using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// The rest of the world: ridges and peaks beyond the boundary of the
    /// resort, so the mountain sits in a range rather than on a slab in a
    /// void.
    ///
    /// It is one coarse mesh with no colliders, because nothing will ever
    /// walk on it. Inside the playable rectangle it is pushed below the real
    /// terrain and simply hides underneath, which avoids cutting a hole in it
    /// and avoids any seam along the join. It stays flat for a cell or two
    /// past the boundary as well, so a triangle straddling the edge cannot
    /// poke up through the snow.
    ///
    /// Heights are anchored to the real mountain's own edge, so however the
    /// resort is retuned the far range still meets it.
    /// </summary>
    [ExecuteAlways]
    public class FarRange : MonoBehaviour
    {
        const string ContainerName = "GeneratedFarRange";

        public MountainGenerator mountain;

        [Header("Extent")]
        [Tooltip("How far out the world goes, in metres from the resort centre.")]
        public float extent = 1900f;
        [Tooltip("Metres per quad. Coarse: this is scenery, not terrain.")]
        public float cellSize = 44f;

        [Header("Shape")]
        [Tooltip("Distance over which the far peaks rise to full height.")]
        public float ridgeDistance = 620f;
        public float ridgeHeight = 420f;
        [Tooltip("How far the ground falls away outside the resort.")]
        public float valleyDrop = 46f;
        [Tooltip("How far under the real terrain the range hides inside the resort.")]
        public float sink = 6f;
        public float noiseScale = 0.0016f;
        public int seed = 4242;

        [Header("Look")]
        [Range(20f, 75f)] public float rockAngle = 38f;

        Mesh _mesh;
        Material _snow, _rock;
        float _offsetX, _offsetZ;

        void Start() { Build(); }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == ContainerName) Kill(c.gameObject);
            }
        }

        [ContextMenu("Build Now")]
        public void Build()
        {
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (mountain == null) return;

            Clear();

            var rnd = new System.Random(seed);
            _offsetX = 3000f + (float)rnd.NextDouble() * 4000f;
            _offsetZ = 3000f + (float)rnd.NextDouble() * 4000f;

            float centreZ = mountain.length * 0.5f;
            int steps = Mathf.Max(8, Mathf.RoundToInt(extent * 2f / Mathf.Max(8f, cellSize)));

            var verts = new Vector3[(steps + 1) * (steps + 1)];
            var snow = new List<int>();
            var rock = new List<int>();

            for (int iz = 0; iz <= steps; iz++)
            {
                float z = centreZ + Mathf.Lerp(-extent, extent, iz / (float)steps);
                for (int ix = 0; ix <= steps; ix++)
                {
                    float x = Mathf.Lerp(-extent, extent, ix / (float)steps);
                    verts[iz * (steps + 1) + ix] = new Vector3(x, Height(x, z), z);
                }
            }

            for (int iz = 0; iz < steps; iz++)
            {
                for (int ix = 0; ix < steps; ix++)
                {
                    int i = iz * (steps + 1) + ix;
                    Sort(verts, i, i + steps + 1, i + steps + 2, snow, rock);
                    Sort(verts, i, i + steps + 2, i + 1, snow, rock);
                }
            }

            var go = new GameObject(ContainerName);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            _mesh = new Mesh();
            _mesh.name = "FarRangeMesh";
            _mesh.hideFlags = HideFlags.DontSave;
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.vertices = verts;
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(snow, 0);
            _mesh.SetTriangles(rock, 1);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_snow == null)
                _snow = MaterialFactory.Create("FarSnow", new Color(0.88f, 0.91f, 0.97f), 0.24f);
            if (_rock == null)
                _rock = MaterialFactory.Create("FarRock", new Color(0.29f, 0.29f, 0.31f), 0.05f);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _snow, _rock };
            // Scenery casts nothing: the shadow map is better spent on the resort.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Height of the world outside the resort, anchored to the resort's
        /// own edge so the two always meet.
        /// </summary>
        float Height(float x, float z)
        {
            float half = mountain.width * 0.5f;

            float nearestX = Mathf.Clamp(x, -half, half);
            float nearestZ = Mathf.Clamp(z, 0f, mountain.length);
            float edge = mountain.SampleHeight(nearestX, nearestZ);

            float away = Vector2.Distance(new Vector2(x, z), new Vector2(nearestX, nearestZ));

            // Inside the resort it simply hides under the real thing.
            if (away < 0.01f) return edge - sink;

            // And it stays hidden for a couple of cells beyond the boundary,
            // so the triangles that straddle the edge stay below the snow.
            float clear = Mathf.Max(0f, away - cellSize * 1.5f);

            float t = Mathf.Clamp01(clear / Mathf.Max(1f, ridgeDistance));
            float eased = t * t * (3f - 2f * t);

            // Ridged noise: folding the absolute value gives sharp crests
            // instead of the rolling blobs plain Perlin produces.
            float ridge = 0f;
            float amplitude = 1f;
            float frequency = noiseScale;
            float normal = 0f;

            for (int i = 0; i < 4; i++)
            {
                float n = Mathf.PerlinNoise((x + _offsetX) * frequency, (z + _offsetZ) * frequency);
                ridge += (1f - Mathf.Abs(n * 2f - 1f)) * amplitude;
                normal += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.17f;
            }

            ridge = normal > 0f ? ridge / normal : 0f;
            ridge = Mathf.Pow(ridge, 2.1f);

            return edge - sink - eased * valleyDrop + ridge * ridgeHeight * eased;
        }

        void Sort(Vector3[] verts, int a, int b, int c, List<int> snow, List<int> rock)
        {
            List<int> target = snow;

            Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (normal.sqrMagnitude > 1e-10f &&
                Vector3.Angle(normal.normalized, Vector3.up) > rockAngle)
            {
                target = rock;
            }

            target.Add(a);
            target.Add(b);
            target.Add(c);
        }
    }
}
