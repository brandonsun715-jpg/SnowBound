using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// The rest of the world.
    ///
    /// Three shells at increasing distance and decreasing detail: the country
    /// immediately beyond the resort, the range behind that, and the peaks on
    /// the horizon. Each is a coarser mesh than the one inside it, so the
    /// whole of a ten kilometre view costs about thirty thousand vertices —
    /// less than a single chunk of the playable terrain.
    ///
    /// Each shell sinks below the one inside it, which is what stops a coarse
    /// mesh poking up through a fine one along their shared ground. That is
    /// also how the innermost shell hides under the resort itself: no hole to
    /// cut, no seam to keep in sync.
    ///
    /// None of it has colliders and none of it casts shadows. It is scenery,
    /// and its entire job is to make the resort look like part of somewhere
    /// bigger instead of a slab in a void.
    /// </summary>
    [ExecuteAlways]
    public class FarRange : MonoBehaviour
    {
        const string ContainerName = "GeneratedFarRange";

        [System.Serializable]
        public class Shell
        {
            public string name = "Shell";
            [Tooltip("Half the width of this shell, in metres from the resort centre.")]
            public float extent = 1800f;
            [Tooltip("Metres per quad. Coarse: this is scenery, not terrain.")]
            public float cellSize = 30f;
            [Tooltip("Distance over which its peaks rise to full height.")]
            public float ridgeDistance = 700f;
            public float ridgeHeight = 380f;
            [Tooltip("How far it drops below whatever is inside it.")]
            public float sink = 12f;
            [Tooltip("Larger features further out, so the horizon reads as huge.")]
            public float noiseScale = 0.0016f;
            [Range(0f, 1f)] public float haze = 0f;
        }

        public MountainGenerator mountain;

        [Header("Shells, nearest first")]
        public Shell[] shells =
        {
            new Shell { name = "Near country", extent = 1900f, cellSize = 28f,
                        ridgeDistance = 620f, ridgeHeight = 300f, sink = 8f,
                        noiseScale = 0.0022f, haze = 0.10f },

            new Shell { name = "Range", extent = 3800f, cellSize = 70f,
                        ridgeDistance = 1500f, ridgeHeight = 620f, sink = 26f,
                        noiseScale = 0.0011f, haze = 0.34f },

            new Shell { name = "Horizon", extent = 7600f, cellSize = 165f,
                        ridgeDistance = 3200f, ridgeHeight = 980f, sink = 60f,
                        noiseScale = 0.00055f, haze = 0.62f }
        };

        [Header("Look")]
        [Range(20f, 75f)] public float rockAngle = 40f;
        public float valleyDrop = 60f;
        [Tooltip("How far under the real terrain the nearest shell hides.")]
        public float bed = 6f;
        public int seed = 4242;

        readonly List<Mesh> _meshes = new List<Mesh>();
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

            foreach (Mesh m in _meshes) Kill(m);
            _meshes.Clear();
        }

        [ContextMenu("Build Now")]
        public void Build()
        {
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (mountain == null || shells == null || shells.Length == 0) return;

            Clear();

            var rnd = new System.Random(seed);
            _offsetX = 3000f + (float)rnd.NextDouble() * 4000f;
            _offsetZ = 3000f + (float)rnd.NextDouble() * 4000f;

            Materials();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);
            root.hideFlags = HideFlags.DontSaveInEditor;

            // Furthest first, so the near shells are drawn over them.
            for (int i = shells.Length - 1; i >= 0; i--)
            {
                float inner = i > 0 ? shells[i - 1].extent : 0f;
                BuildShell(root.transform, shells[i], inner, i);
            }
        }

        void Materials()
        {
            if (_snow == null)
                _snow = MaterialFactory.CreateSurface("FarSnow", ProceduralTextures.Windblown(),
                                                      new Color(0.95f, 0.97f, 1f), 110f, 0.6f);

            if (_rock == null)
                _rock = MaterialFactory.CreateSurface("FarRock", ProceduralTextures.Rock(),
                                                      new Color(0.74f, 0.76f, 0.82f), 85f, 0.8f);
        }

        void BuildShell(Transform root, Shell shell, float inner, int index)
        {
            float centreZ = mountain.length * 0.5f;
            int steps = Mathf.Clamp(Mathf.RoundToInt(shell.extent * 2f / Mathf.Max(8f, shell.cellSize)), 8, 400);

            var verts = new Vector3[(steps + 1) * (steps + 1)];
            var snow = new List<int>();
            var rock = new List<int>();

            for (int iz = 0; iz <= steps; iz++)
            {
                float z = centreZ + Mathf.Lerp(-shell.extent, shell.extent, iz / (float)steps);

                for (int ix = 0; ix <= steps; ix++)
                {
                    float x = Mathf.Lerp(-shell.extent, shell.extent, ix / (float)steps);
                    verts[iz * (steps + 1) + ix] = new Vector3(x, Height(x, z, shell, inner), z);
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

            var mesh = new Mesh
            {
                name = "FarRange" + index,
                hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt32
            };

            mesh.vertices = verts;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(snow, 0);
            mesh.SetTriangles(rock, 1);

            PrimitiveMeshes.ProjectUVs(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            _meshes.Add(mesh);

            var go = new GameObject(shell.name);
            go.transform.SetParent(root, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _snow, _rock };

            // Scenery casts nothing: the shadow map is better spent on the resort.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Height of one shell. Anchored to the resort's own edge so the
        /// nearest one always meets it, sunk below whatever is inside it, and
        /// rising into ridges as it gets further away.
        /// </summary>
        float Height(float x, float z, Shell shell, float inner)
        {
            float half = mountain.width * 0.5f;

            float nearestX = Mathf.Clamp(x, -half, half);
            float nearestZ = Mathf.Clamp(z, 0f, mountain.length);
            float edge = mountain.SampleHeight(nearestX, nearestZ);

            float away = Vector2.Distance(new Vector2(x, z), new Vector2(nearestX, nearestZ));

            // Under the resort, and under everything closer in than this shell.
            float under = bed + shell.sink;
            if (away < 0.01f) return edge - under;

            // Stay hidden for a couple of its own cells past whatever it is
            // hiding behind, so the triangles that straddle the join cannot
            // poke up through the finer mesh in front of them.
            float clear = Mathf.Max(0f, away - Mathf.Max(shell.cellSize * 1.5f, inner - half));

            float t = Mathf.Clamp01(clear / Mathf.Max(1f, shell.ridgeDistance));
            float eased = t * t * (3f - 2f * t);

            float ridge = Ridged(x, z, shell.noiseScale);

            return edge - under - eased * valleyDrop + ridge * shell.ridgeHeight * eased;
        }

        /// <summary>
        /// Ridged noise: folding the absolute value gives sharp crests instead
        /// of the rolling blobs plain Perlin produces.
        /// </summary>
        float Ridged(float x, float z, float scale)
        {
            float sum = 0f, amplitude = 1f, frequency = scale, norm = 0f;

            for (int i = 0; i < 5; i++)
            {
                float n = Mathf.PerlinNoise((x + _offsetX) * frequency, (z + _offsetZ) * frequency);

                sum += (1f - Mathf.Abs(n * 2f - 1f)) * amplitude;
                norm += amplitude;

                amplitude *= 0.5f;
                frequency *= 2.17f;
            }

            float ridge = norm > 0f ? sum / norm : 0f;
            return Mathf.Pow(ridge, 2.1f);
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
