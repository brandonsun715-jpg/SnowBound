using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// Builds the snowy mountain mesh and is the single source of truth for
    /// "how high is the ground at (x, z)?". Every other system (props, lodge,
    /// chairlift, player spawn) asks this component instead of raycasting.
    ///
    /// Layout: z = 0 is the bottom of the mountain (base area / lodge),
    /// z = length is the top (lift top station). x = 0 is the middle.
    /// Keep this GameObject at position (0,0,0), rotation 0, scale 1.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class MountainGenerator : MonoBehaviour
    {
        [Header("Size (metres)")]
        public float width = 320f;
        public float length = 420f;
        [Tooltip("Smaller = smoother terrain but a heavier mesh. 2 is a good default.")]
        public float cellSize = 2f;

        [Header("Fall line")]
        public float maxHeight = 120f;
        [Tooltip("1 = straight ramp. Above 1 = gentle at the bottom, steeper at the top.")]
        public float steepness = 1.5f;
        [Tooltip("Everything below this z is a flat pad for the base area / lodge.")]
        public float bottomPadZ = 35f;
        [Tooltip("Everything above this z is a flat pad for the top lift station.")]
        public float topPadZ = 395f;
        public float padFade = 30f;

        [Header("Piste (the ski run)")]
        public float pisteHalfWidth = 22f;
        [Tooltip("Extra width added at the very bottom so the base area is open.")]
        public float basePisteExtraWidth = 25f;
        [Tooltip("How far the run snakes left/right, in metres.")]
        public float pisteCurveAmplitude = 28f;
        public float pisteCurveFrequency = 0.012f;

        [Header("Valley walls")]
        public float wallFalloff = 45f;
        public float wallHeight = 32f;
        public float rimStart = 110f;
        public float rimEnd = 155f;
        public float rimHeight = 70f;

        [Header("Terrain noise")]
        public float noiseScale = 0.012f;
        [Tooltip("Bumpiness on the groomed run. Keep this small.")]
        public float pisteNoise = 1.2f;
        [Tooltip("Bumpiness off the run.")]
        public float offPisteNoise = 9f;
        public int seed = 12345;

        [Header("Look")]
        [Tooltip("Leave empty to auto-create a plain snow material at runtime.")]
        public Material snowMaterial;

        // ---------------------------------------------------------------

        static MountainGenerator _instance;

        /// <summary>Global access for other systems. Safe to call from Awake.</summary>
        public static MountainGenerator Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<MountainGenerator>();
                return _instance;
            }
        }

        Mesh _mesh;
        Material _runtimeSnow;
        bool _noiseReady;
        float _nOffX, _nOffZ;

        void Awake()
        {
            _instance = this;
            if (_mesh == null) Build();
        }

        void OnValidate()
        {
            cellSize = Mathf.Max(0.5f, cellSize);
            width = Mathf.Max(20f, width);
            length = Mathf.Max(20f, length);
        }

        // ---------------- height queries (usable without building) -------

        void EnsureNoise()
        {
            if (_noiseReady) return;
            var rnd = new System.Random(seed);
            _nOffX = 1000f + (float)rnd.NextDouble() * 5000f;
            _nOffZ = 1000f + (float)rnd.NextDouble() * 5000f;
            _noiseReady = true;
        }

        static float Smooth01(float a, float b, float v)
        {
            if (Mathf.Abs(b - a) < 0.0001f) return v >= b ? 1f : 0f;
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        float FallLine(float z)
        {
            float t = Mathf.Clamp01(z / length);
            return maxHeight * Mathf.Pow(t, steepness);
        }

        /// <summary>Middle of the ski run at this z. The run snakes gently.</summary>
        public float PisteCenterX(float z)
        {
            return Mathf.Sin(z * pisteCurveFrequency) * pisteCurveAmplitude;
        }

        /// <summary>Half width of the ski run at this z (wider near the base).</summary>
        public float PisteHalfWidth(float z)
        {
            float k = Smooth01(0f, 90f, z);
            return pisteHalfWidth + basePisteExtraWidth * (1f - k);
        }

        float Fbm(float x, float z)
        {
            float sum = 0f, amp = 1f, freq = noiseScale, norm = 0f;
            for (int i = 0; i < 3; i++)
            {
                float n = Mathf.PerlinNoise((x + _nOffX) * freq, (z + _nOffZ) * freq);
                sum += (n - 0.5f) * 2f * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.13f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ground height in world space at (x, z).</summary>
        public float SampleHeight(float x, float z)
        {
            EnsureNoise();

            float h = FallLine(z);

            // Flat pad at the bottom (base area) and at the top (lift station).
            float kBottom = Smooth01(bottomPadZ, bottomPadZ + padFade, z);
            h = Mathf.Lerp(FallLine(bottomPadZ), h, kBottom);
            float kTop = Smooth01(topPadZ - padFade, topPadZ, z);
            h = Mathf.Lerp(h, FallLine(topPadZ), kTop);

            // Valley walls either side of the run.
            float d = Mathf.Abs(x - PisteCenterX(z));
            float wall = Smooth01(PisteHalfWidth(z), PisteHalfWidth(z) + wallFalloff, d);
            h += wall * wallHeight;
            h += Smooth01(rimStart, rimEnd, d) * rimHeight;

            // Bumpy off-piste, near-smooth on-piste.
            h += Fbm(x, z) * Mathf.Lerp(pisteNoise, offPisteNoise, wall);

            // Berms so the player cannot slide off the front/back edge of the map.
            h += Smooth01(14f, 0f, z) * 22f;
            h += Smooth01(length - 8f, length, z) * 22f;

            return h;
        }

        public Vector3 SamplePoint(float x, float z)
        {
            return new Vector3(x, SampleHeight(x, z), z);
        }

        /// <summary>Up direction of the ground at (x, z).</summary>
        public Vector3 SampleNormal(float x, float z)
        {
            const float e = 1.5f;
            float hL = SampleHeight(x - e, z);
            float hR = SampleHeight(x + e, z);
            float hD = SampleHeight(x, z - e);
            float hU = SampleHeight(x, z + e);
            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        public bool IsOnPiste(float x, float z, float margin = 0f)
        {
            if (z < 0f || z > length) return false;
            return Mathf.Abs(x - PisteCenterX(z)) <= PisteHalfWidth(z) + margin;
        }

        /// <summary>Middle of the run at this z, on the ground.</summary>
        public Vector3 PistePoint(float z)
        {
            float x = PisteCenterX(z);
            return new Vector3(x, SampleHeight(x, z), z);
        }

        // ---------------- mesh building ----------------------------------

        [ContextMenu("Build Now")]
        public void Build()
        {
            if (transform.position != Vector3.zero ||
                transform.rotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
            {
                Debug.LogWarning("[MountainGenerator] Keep this GameObject at position (0,0,0), " +
                                 "rotation (0,0,0) and scale (1,1,1) or heights will not line up.", this);
            }

            EnsureNoise();

            int nx = Mathf.Max(2, Mathf.RoundToInt(width / cellSize) + 1);
            int nz = Mathf.Max(2, Mathf.RoundToInt(length / cellSize) + 1);

            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            var tris = new int[(nx - 1) * (nz - 1) * 6];

            float x0 = -width * 0.5f;

            for (int iz = 0; iz < nz; iz++)
            {
                float z = iz / (float)(nz - 1) * length;
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = x0 + ix / (float)(nx - 1) * width;
                    int i = iz * nx + ix;
                    verts[i] = new Vector3(x, SampleHeight(x, z), z);
                    uvs[i] = new Vector2(x / 12f, z / 12f);
                }
            }

            int t = 0;
            for (int iz = 0; iz < nz - 1; iz++)
            {
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int i = iz * nx + ix;
                    tris[t++] = i;
                    tris[t++] = i + nx;
                    tris[t++] = i + nx + 1;
                    tris[t++] = i;
                    tris[t++] = i + nx + 1;
                    tris[t++] = i + 1;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.name = "MountainMesh";
                _mesh.hideFlags = HideFlags.DontSave;
            }
            _mesh.Clear();
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var mc = GetComponent<MeshCollider>();
            mc.sharedMesh = null;
            mc.sharedMesh = _mesh;

            Material mat = snowMaterial;
            if (mat == null)
            {
                if (_runtimeSnow == null)
                    _runtimeSnow = MaterialFactory.Create("SnowRuntime", new Color(0.93f, 0.95f, 1f), 0.32f);
                mat = _runtimeSnow;
            }
            GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
