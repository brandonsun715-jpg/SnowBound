using System.Collections.Generic;
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
    [RequireComponent(typeof(SnowSurface))]
    public class MountainGenerator : MonoBehaviour
    {
        [Header("Size (metres)")]
        public float width = 470f;
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

        [Header("Runs")]
        public PisteDefinition[] pistes =
        {
            new PisteDefinition
            {
                name = "Larchway", grade = PisteGrade.Intermediate,
                anchorX = 10f, spreadX = 55f,
                snakeAmplitude = 24f, snakeFrequency = 0.013f, snakePhase = 0f,
                halfWidth = 24f, baseExtraWidth = 28f,
                surfaceNoise = 1.1f, hasRollers = true
            },
            new PisteDefinition
            {
                name = "Cornice", grade = PisteGrade.Advanced,
                anchorX = 10f, spreadX = -105f,
                snakeAmplitude = 20f, snakeFrequency = 0.017f, snakePhase = 1.7f,
                halfWidth = 16f, baseExtraWidth = 12f,
                surfaceNoise = 2.6f, hasRollers = false
            }
        };

        [Header("How the runs fan out")]
        [Tooltip("Below this the runs are merged into the base area.")]
        public float spreadStartZ = 60f;
        [Tooltip("Above this they are fully apart.")]
        public float spreadFullZ = 190f;
        [Tooltip("Where they start converging again towards the summit.")]
        public float mergeStartZ = 355f;
        [Tooltip("Above this they share the top station.")]
        public float mergeEndZ = 402f;

        [Header("Valley walls")]
        public float wallFalloff = 45f;
        public float wallHeight = 32f;
        public float rimStart = 180f;
        public float rimEnd = 225f;
        public float rimHeight = 70f;

        [Header("Rollers")]
        [Tooltip("Smooth bumps across the run. These are the jumps.")]
        public bool rollers = true;
        [Tooltip("Distances up the mountain, in metres, where a roller sits.")]
        public float[] rollerZ = { 128f, 214f, 300f };
        public float rollerHeight = 2.6f;
        [Tooltip("How long each roller is, front to back.")]
        public float rollerLength = 26f;

        [Header("Terrain noise")]
        public float noiseScale = 0.012f;
        [Tooltip("Bumpiness off the runs.")]
        public float offPisteNoise = 9f;
        public int seed = 12345;

        [Header("Look")]
        [Tooltip("Leave empty to auto-create a plain snow material at runtime.")]
        public Material snowMaterial;
        [Tooltip("Used on faces too steep to hold snow. Leave empty for a default.")]
        public Material rockMaterial;
        [Tooltip("Faces steeper than this show bare rock. The piste never does.")]
        [Range(20f, 75f)] public float rockAngle = 40f;

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
        Material _runtimeRock;
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

        public int PisteCount { get { return pistes != null ? pistes.Length : 0; } }

        /// <summary>
        /// 0 where the runs are merged, 1 where they are fully apart. Zero at
        /// the base and again at the summit, so one lift serves every run.
        /// </summary>
        public float PisteSpread(float z)
        {
            return Smooth01(spreadStartZ, spreadFullZ, z) * (1f - Smooth01(mergeStartZ, mergeEndZ, z));
        }

        /// <summary>Middle of run <paramref name="index"/> at this z.</summary>
        public float PisteCenterX(int index, float z)
        {
            if (index < 0 || index >= PisteCount) return 0f;

            PisteDefinition piste = pistes[index];
            float spread = PisteSpread(z);

            return piste.anchorX
                 + piste.spreadX * spread
                 + Mathf.Sin(z * piste.snakeFrequency + piste.snakePhase)
                   * piste.snakeAmplitude * spread;
        }

        /// <summary>Half width of run <paramref name="index"/> (wider near the base).</summary>
        public float PisteHalfWidth(int index, float z)
        {
            if (index < 0 || index >= PisteCount) return 20f;

            PisteDefinition piste = pistes[index];
            float k = Smooth01(0f, 90f, z);
            return piste.halfWidth + piste.baseExtraWidth * (1f - k);
        }

        /// <summary>
        /// The run this point belongs to: the one whose edge it is furthest
        /// inside, or least far outside.
        /// </summary>
        public int NearestPiste(float x, float z)
        {
            int best = 0;
            float bestScore = float.MaxValue;

            for (int i = 0; i < PisteCount; i++)
            {
                float score = Mathf.Abs(x - PisteCenterX(i, z)) - PisteHalfWidth(i, z);
                if (score < bestScore) { bestScore = score; best = i; }
            }

            return best;
        }

        /// <summary>Middle of the main run. Kept for systems that only need one.</summary>
        public float PisteCenterX(float z) { return PisteCenterX(0, z); }

        public float PisteHalfWidth(float z) { return PisteHalfWidth(0, z); }

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
        /// <summary>
        /// Smooth bumps down the middle of the run. They fade out towards the
        /// edges so the piste keeps its shape, and they are what turns the
        /// jump button into something worth pressing.
        /// </summary>
        float Rollers(float x, float z, int piste)
        {
            if (!rollers || rollerZ == null || rollerZ.Length == 0) return 0f;
            if (rollerLength < 1f || rollerHeight <= 0f) return 0f;

            if (piste < 0 || piste >= PisteCount || !pistes[piste].hasRollers) return 0f;

            float half = PisteHalfWidth(piste, z);
            float across = 1f - Smooth01(half * 0.2f, half * 1.1f,
                                         Mathf.Abs(x - PisteCenterX(piste, z)));
            if (across <= 0f) return 0f;

            float sum = 0f;
            float reach = rollerLength * 0.5f;

            for (int i = 0; i < rollerZ.Length; i++)
            {
                float t = (z - rollerZ[i]) / reach;
                if (t <= -1f || t >= 1f) continue;
                // Cosine bump: zero height and zero slope at both ends, so a
                // roller blends into the run instead of stepping out of it.
                sum += 0.5f * (1f + Mathf.Cos(t * Mathf.PI)) * rollerHeight;
            }

            return sum * across;
        }

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
            // Each run is carved into the mountain, and the ground between
            // two of them rises into a ridge of its own accord.
            int piste = NearestPiste(x, z);
            float halfWidth = PisteHalfWidth(piste, z);
            float d = Mathf.Abs(x - PisteCenterX(piste, z));
            float wall = Smooth01(halfWidth, halfWidth + wallFalloff, d);
            h += wall * wallHeight;

            // The rim belongs to the edge of the map, not to any one run.
            h += Smooth01(rimStart, rimEnd, Mathf.Abs(x)) * rimHeight;

            float groomed = piste < PisteCount ? pistes[piste].surfaceNoise : 1.1f;
            h += Fbm(x, z) * Mathf.Lerp(groomed, offPisteNoise, wall);

            h += Rollers(x, z, piste);

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

            for (int i = 0; i < PisteCount; i++)
            {
                if (Mathf.Abs(x - PisteCenterX(i, z)) <= PisteHalfWidth(i, z) + margin) return true;
            }

            return false;
        }

        /// <summary>Middle of a run at this z, on the ground.</summary>
        public Vector3 PistePoint(int index, float z)
        {
            float x = PisteCenterX(index, z);
            return new Vector3(x, SampleHeight(x, z), z);
        }

        public Vector3 PistePoint(float z) { return PistePoint(0, z); }

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
            var snow = new List<int>((nx - 1) * (nz - 1) * 6);
            var rock = new List<int>();

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

            for (int iz = 0; iz < nz - 1; iz++)
            {
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int i = iz * nx + ix;
                    Sort(verts, i, i + nx, i + nx + 1, snow, rock);
                    Sort(verts, i, i + nx + 1, i + 1, snow, rock);
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
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(snow, 0);
            _mesh.SetTriangles(rock, 1);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var mc = GetComponent<MeshCollider>();
            mc.sharedMesh = null;
            mc.sharedMesh = _mesh;

            Material snowMat = snowMaterial;
            if (snowMat == null)
            {
                if (_runtimeSnow == null)
                    _runtimeSnow = MaterialFactory.Create("SnowRuntime", new Color(0.93f, 0.95f, 1f), 0.32f);
                snowMat = _runtimeSnow;
            }

            Material rockMat = rockMaterial;
            if (rockMat == null)
            {
                if (_runtimeRock == null)
                    _runtimeRock = MaterialFactory.Create("RockRuntime", new Color(0.30f, 0.29f, 0.29f), 0.06f);
                rockMat = _runtimeRock;
            }

            GetComponent<MeshRenderer>().sharedMaterials = new[] { snowMat, rockMat };
        }

        /// <summary>
        /// Snow settles on gentle ground and slides off steep ground, so a
        /// face steeper than rockAngle is drawn as bare rock. The piste is
        /// always snow, whatever the slope says.
        /// </summary>
        void Sort(Vector3[] verts, int a, int b, int c, List<int> snow, List<int> rock)
        {
            List<int> target = snow;

            Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (normal.sqrMagnitude > 1e-10f &&
                Vector3.Angle(normal.normalized, Vector3.up) > rockAngle)
            {
                Vector3 centre = (verts[a] + verts[b] + verts[c]) / 3f;
                if (!IsOnPiste(centre.x, centre.z, 6f)) target = rock;
            }

            target.Add(a);
            target.Add(b);
            target.Add(c);
        }
    }
}
