using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>Which way a terrain brush pushes.</summary>
    public enum TerrainTool { Raise, Lower, Smooth, Flatten, Slope }

    /// <summary>
    /// The mountain: a height field, the chunks that draw and collide with it,
    /// and the runs cut into it.
    ///
    /// The important idea is that there is exactly one surface. The height
    /// field is the truth; the chunk meshes are built from it and their
    /// colliders are those same meshes; every query — where is the ground,
    /// how steep is it, where does this run go — reads the same array by
    /// bilinear interpolation, which is precisely what the triangles do. So
    /// what you see, what you stand on and what the game believes cannot
    /// drift apart.
    ///
    /// The field is built in layers: the natural mountain underneath, then
    /// whatever the player has sculpted, then the runs carved into the
    /// result. Sculpting edits its own layer, so cutting a new run never
    /// throws away the shaping that was done before it.
    ///
    /// Layout: z = 0 is the base area, z = length is the summit. x = 0 is the
    /// middle. Keep this GameObject at the origin, unrotated and unscaled.
    /// </summary>
    [ExecuteAlways]
    public class MountainGenerator : MonoBehaviour
    {
        const string ContainerName = "GeneratedTerrain";

        [Header("Size (metres)")]
        public float width = 620f;
        public float length = 560f;
        [Tooltip("Metres between height samples. Smaller is smoother and heavier.")]
        public float cellSize = 2.5f;
        [Tooltip("Height samples per chunk edge. Chunks are what get rebuilt when you sculpt.")]
        public int chunkCells = 24;

        [Header("Fall line")]
        public float maxHeight = 210f;
        [Tooltip("1 = straight ramp. Above 1 = gentle at the bottom, steeper at the top.")]
        public float steepness = 1.55f;
        [Tooltip("Everything below this z is a flat pad for the base area and the lodge.")]
        public float bottomPadZ = 46f;
        [Tooltip("Everything above this z is a flat shoulder at the summit.")]
        public float topPadZ = 528f;
        public float padFade = 34f;

        [Header("Shape")]
        [Tooltip("How far the ground rises towards the edges of the map.")]
        public float rimStart = 205f;
        public float rimEnd = 300f;
        public float rimHeight = 96f;
        [Tooltip("Bowls and shoulders across the mountain. This is the natural relief.")]
        public float reliefHeight = 34f;
        public float reliefScale = 0.0042f;

        [Header("Terrain noise")]
        public float noiseScale = 0.011f;
        [Tooltip("Bumpiness of the untouched mountain.")]
        public float roughness = 7.5f;
        public int seed = 12345;

        [Header("Runs")]
        [Tooltip("How far outside a run its bank reaches.")]
        public float trailFalloff = 26f;
        [Tooltip("Least grade a cut run is allowed to have, so no run ever runs uphill.")]
        public float minimumTrailGrade = 0.035f;
        public float rollerSpacing = 78f;
        public float rollerHeight = 2.4f;
        public float rollerLength = 26f;

        [Header("Look")]
        public Material snowMaterial;
        public Material rockMaterial;
        public Material groomedMaterial;
        public Material powderMaterial;
        [Tooltip("Faces steeper than this show bare rock. A run never does.")]
        [Range(20f, 75f)] public float rockAngle = 42f;

        /// <summary>The runs the player has cut. Empty on a new resort.</summary>
        [System.NonSerialized] public List<Trail> trails = new List<Trail>();

        // ---------------------------------------------------------------

        static MountainGenerator _instance;

        public static MountainGenerator Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<MountainGenerator>();
                return _instance;
            }
        }

        /// <summary>Raised after the surface changes, with the world rect that moved.</summary>
        public event System.Action<Rect> TerrainChanged;

        int _nx, _nz;
        float _x0, _cellX, _cellZ;
        float[] _h;         // the final surface
        float[] _sculpt;    // what the player has pushed around
        byte[] _trailAt;    // 0 = open mountain, otherwise trail index + 1

        Transform _container;
        readonly List<TerrainChunk> _chunks = new List<TerrainChunk>();

        Material _runtimeSnow, _runtimeRock, _runtimeGroomed, _runtimePowder;
        bool _noiseReady;
        float _nOffX, _nOffZ, _rOffX, _rOffZ;

        readonly List<Protection> _protections = new List<Protection>();

        struct Protection
        {
            public Vector2 centre;
            public float radius;
            public string what;
        }

        // ---------------- lifecycle --------------------------------------

        void Awake()
        {
            _instance = this;
            if (_h == null) Regenerate();
        }

        void OnEnable() { _instance = this; }

        void OnValidate()
        {
            cellSize = Mathf.Max(1f, cellSize);
            width = Mathf.Max(60f, width);
            length = Mathf.Max(60f, length);
            chunkCells = Mathf.Clamp(chunkCells, 8, 64);
        }

        // ---------------- the grid ---------------------------------------

        public int GridCountX { get { return _nx; } }
        public int GridCountZ { get { return _nz; } }
        public bool Ready { get { return _h != null; } }

        public float GridX(int ix) { return _x0 + ix * _cellX; }
        public float GridZ(int iz) { return iz * _cellZ; }

        public int IndexX(float x) { return Mathf.Clamp(Mathf.RoundToInt((x - _x0) / _cellX), 0, _nx - 1); }
        public int IndexZ(float z) { return Mathf.Clamp(Mathf.RoundToInt(z / _cellZ), 0, _nz - 1); }

        public float HeightAtIndex(int ix, int iz)
        {
            if (_h == null) return 0f;
            ix = Mathf.Clamp(ix, 0, _nx - 1);
            iz = Mathf.Clamp(iz, 0, _nz - 1);
            return _h[iz * _nx + ix];
        }

        public Vector3 NormalAtIndex(int ix, int iz)
        {
            float hL = HeightAtIndex(ix - 1, iz);
            float hR = HeightAtIndex(ix + 1, iz);
            float hD = HeightAtIndex(ix, iz - 1);
            float hU = HeightAtIndex(ix, iz + 1);

            return new Vector3(hL - hR, 2f * Mathf.Max(_cellX, _cellZ), hD - hU).normalized;
        }

        /// <summary>Which surface a vertex belongs to: 0 snow, 2 groomed run, 3 loose run.</summary>
        public int SurfaceAtIndex(int ix, int iz)
        {
            Trail trail = TrailAtIndex(ix, iz);
            if (trail == null) return 0;
            return trail.groomed ? 2 : 3;
        }

        public Trail TrailAtIndex(int ix, int iz)
        {
            if (_trailAt == null) return null;

            ix = Mathf.Clamp(ix, 0, _nx - 1);
            iz = Mathf.Clamp(iz, 0, _nz - 1);

            int id = _trailAt[iz * _nx + ix];
            if (id == 0 || id - 1 >= trails.Count) return null;

            return trails[id - 1];
        }

        // ---------------- height queries ---------------------------------

        void EnsureNoise()
        {
            if (_noiseReady) return;

            var rnd = new System.Random(seed);
            _nOffX = 1000f + (float)rnd.NextDouble() * 5000f;
            _nOffZ = 1000f + (float)rnd.NextDouble() * 5000f;
            _rOffX = 7000f + (float)rnd.NextDouble() * 5000f;
            _rOffZ = 7000f + (float)rnd.NextDouble() * 5000f;
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

        float Fbm(float x, float z, float scale, int octaves)
        {
            float sum = 0f, amp = 1f, freq = scale, norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise((x + _nOffX) * freq, (z + _nOffZ) * freq);
                sum += (n - 0.5f) * 2f * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.13f;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>
        /// The mountain as nature left it: a fall line, a flat pad at either
        /// end, walls at the edges of the map, and relief in between. No runs,
        /// because on a new resort there are none.
        /// </summary>
        public float BaseHeight(float x, float z)
        {
            EnsureNoise();

            float h = FallLine(z);

            float kBottom = Smooth01(bottomPadZ, bottomPadZ + padFade, z);
            h = Mathf.Lerp(FallLine(bottomPadZ), h, kBottom);

            float kTop = Smooth01(topPadZ - padFade, topPadZ, z);
            h = Mathf.Lerp(h, FallLine(topPadZ), kTop);

            // Bowls, spurs and shoulders. This is what gives the undeveloped
            // mountain somewhere obvious to put a run and somewhere obviously
            // hard to.
            float relief = Mathf.PerlinNoise((x + _rOffX) * reliefScale, (z + _rOffZ) * reliefScale);
            float shoulder = Mathf.PerlinNoise((x + _rOffX) * reliefScale * 2.7f,
                                               (z + _rOffZ) * reliefScale * 2.7f);
            h += ((relief - 0.45f) * 1.7f + (shoulder - 0.5f) * 0.6f) * reliefHeight
                 * Smooth01(bottomPadZ, bottomPadZ + padFade * 2f, z);

            // The rim: the map has sides, and they climb.
            h += Smooth01(rimStart, rimEnd, Mathf.Abs(x)) * rimHeight;

            h += Fbm(x, z, noiseScale, 3) * roughness;

            // Berms at the front and back edge so nothing slides off the map.
            h += Smooth01(16f, 0f, z) * 26f;
            h += Smooth01(length - 10f, length, z) * 26f;

            return h;
        }

        float SculptAt(float x, float z)
        {
            if (_sculpt == null) return 0f;
            return Bilinear(_sculpt, x, z);
        }

        /// <summary>The mountain plus the player's shaping, before any run is cut.</summary>
        public float NaturalHeight(float x, float z)
        {
            return BaseHeight(x, z) + SculptAt(x, z);
        }

        float Bilinear(float[] field, float x, float z)
        {
            if (field == null) return 0f;

            float fx = Mathf.Clamp((x - _x0) / _cellX, 0f, _nx - 1.0001f);
            float fz = Mathf.Clamp(z / _cellZ, 0f, _nz - 1.0001f);

            int ix = (int)fx;
            int iz = (int)fz;
            float tx = fx - ix;
            float tz = fz - iz;

            int ix1 = Mathf.Min(ix + 1, _nx - 1);
            int iz1 = Mathf.Min(iz + 1, _nz - 1);

            float a = field[iz * _nx + ix];
            float b = field[iz * _nx + ix1];
            float c = field[iz1 * _nx + ix];
            float d = field[iz1 * _nx + ix1];

            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>
        /// Ground height at (x, z). This is the same bilinear interpolation the
        /// triangles perform, so it is the actual surface and not an
        /// approximation of it.
        /// </summary>
        public float SampleHeight(float x, float z)
        {
            if (_h == null) return BaseHeight(x, z);
            return Bilinear(_h, x, z);
        }

        public Vector3 SamplePoint(float x, float z)
        {
            return new Vector3(x, SampleHeight(x, z), z);
        }

        public Vector3 SampleNormal(float x, float z)
        {
            float e = Mathf.Max(_cellX, _cellZ);
            float hL = SampleHeight(x - e, z);
            float hR = SampleHeight(x + e, z);
            float hD = SampleHeight(x, z - e);
            float hU = SampleHeight(x, z + e);

            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        public float SlopeDegrees(float x, float z)
        {
            return Vector3.Angle(SampleNormal(x, z), Vector3.up);
        }

        // ---------------- the map's edges ---------------------------------

        public Rect WorldBounds
        {
            get { return Rect.MinMaxRect(-width * 0.5f, 0f, width * 0.5f, length); }
        }

        public bool InsideWorld(float x, float z, float margin = 0f)
        {
            return x > -width * 0.5f - margin && x < width * 0.5f + margin
                && z > -margin && z < length + margin;
        }

        public Vector3 ClampToWorld(Vector3 point, float margin = 6f)
        {
            point.x = Mathf.Clamp(point.x, -width * 0.5f + margin, width * 0.5f - margin);
            point.z = Mathf.Clamp(point.z, margin, length - margin);
            return point;
        }

        // ---------------- trails -------------------------------------------

        public int TrailCount { get { return trails != null ? trails.Count : 0; } }

        public Trail TrailAt(int index)
        {
            if (trails == null || index < 0 || index >= trails.Count) return null;
            return trails[index];
        }

        public int IndexOf(Trail trail) { return trails == null ? -1 : trails.IndexOf(trail); }

        /// <summary>The run nearest this point, or -1 if the mountain is bare.</summary>
        public int NearestTrail(float x, float z)
        {
            if (trails == null || trails.Count == 0) return -1;

            int best = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < trails.Count; i++)
            {
                float along;
                float score = trails[i].DistanceTo(x, z, out along) - trails[i].halfWidth;
                if (score < bestScore) { bestScore = score; best = i; }
            }

            return best;
        }

        /// <summary>Is this point on the groomed part of any run?</summary>
        public bool OnAnyTrail(float x, float z, float margin = 0f)
        {
            if (trails == null) return false;

            for (int i = 0; i < trails.Count; i++)
            {
                float along;
                if (trails[i].DistanceTo(x, z, out along) <= trails[i].halfWidth + margin) return true;
            }

            return false;
        }

        /// <summary>The run under this point, or null out on the open mountain.</summary>
        public Trail TrailUnder(float x, float z, float margin = 0f)
        {
            if (trails == null) return null;

            Trail best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < trails.Count; i++)
            {
                float along;
                float d = trails[i].DistanceTo(x, z, out along);
                if (d > trails[i].halfWidth + margin || d >= bestDistance) continue;

                bestDistance = d;
                best = trails[i];
            }

            return best;
        }

        public void AddTrail(Trail trail)
        {
            if (trail == null || !trail.Valid) return;
            if (trails == null) trails = new List<Trail>();

            trails.Add(trail);
            Regenerate();
        }

        public void RemoveTrail(Trail trail)
        {
            if (trails == null || !trails.Remove(trail)) return;
            Regenerate();
        }

        // ---------------- protected ground ---------------------------------

        /// <summary>
        /// Ground that must not move: the lodge's footings, a lift station.
        /// Sculpting inside one of these is refused rather than allowed to
        /// quietly leave a building floating in the air.
        /// </summary>
        public void Protect(Vector3 centre, float radius, string what)
        {
            for (int i = 0; i < _protections.Count; i++)
            {
                if (_protections[i].what != what) continue;

                _protections[i] = new Protection
                {
                    centre = new Vector2(centre.x, centre.z),
                    radius = radius,
                    what = what
                };
                return;
            }

            _protections.Add(new Protection
            {
                centre = new Vector2(centre.x, centre.z),
                radius = radius,
                what = what
            });
        }

        public void Unprotect(string what)
        {
            for (int i = _protections.Count - 1; i >= 0; i--)
                if (_protections[i].what == what) _protections.RemoveAt(i);
        }

        /// <summary>What is protecting this point, or null if the ground is free.</summary>
        public string ProtectedBy(float x, float z, float radius)
        {
            var point = new Vector2(x, z);

            for (int i = 0; i < _protections.Count; i++)
            {
                if (Vector2.Distance(point, _protections[i].centre) < _protections[i].radius + radius)
                    return _protections[i].what;
            }

            return null;
        }

        // ---------------- sculpting ------------------------------------------

        /// <summary>
        /// Push the ground about with a round brush. Returns what blocked the
        /// stroke, or null if it went through.
        ///
        /// The edit is written to the sculpt layer rather than to the surface,
        /// so the runs cut into the mountain are re-carved on top of the new
        /// shape instead of being flattened by it.
        /// </summary>
        public string Sculpt(Vector3 centre, float radius, float strength, TerrainTool tool, float dt)
        {
            if (_h == null) return "The mountain is not built yet";

            string blocked = ProtectedBy(centre.x, centre.z, radius * 0.55f);
            if (blocked != null) return blocked;

            if (!InsideWorld(centre.x, centre.z, -radius * 0.25f)) return "Outside the resort";

            int x0 = IndexX(centre.x - radius);
            int x1 = IndexX(centre.x + radius);
            int z0 = IndexZ(centre.z - radius);
            int z1 = IndexZ(centre.z + radius);

            float amount = strength * dt;
            float average = SampleHeight(centre.x, centre.z);

            for (int iz = z0; iz <= z1; iz++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    float x = GridX(ix);
                    float z = GridZ(iz);

                    float d = Vector2.Distance(new Vector2(x, z), new Vector2(centre.x, centre.z));
                    if (d > radius) continue;

                    // Soft edge, so a stroke leaves a hill and not a plateau.
                    float falloff = 1f - Smooth01(radius * 0.25f, radius, d);
                    if (falloff <= 0f) continue;

                    int i = iz * _nx + ix;
                    float here = _h[i];
                    float want;

                    switch (tool)
                    {
                        case TerrainTool.Raise:
                            want = here + 24f * amount;
                            break;

                        case TerrainTool.Lower:
                            want = here - 24f * amount;
                            break;

                        case TerrainTool.Smooth:
                            want = Mathf.Lerp(here, Neighbourhood(ix, iz), Mathf.Clamp01(amount * 5f));
                            break;

                        case TerrainTool.Flatten:
                            want = Mathf.Lerp(here, average, Mathf.Clamp01(amount * 4f));
                            break;

                        default:
                            // Sculpt slope: pull the ground onto a clean plane
                            // through the brush centre, tilted down the fall line.
                            want = Mathf.Lerp(here, average + SlopePlane(centre, x, z),
                                              Mathf.Clamp01(amount * 4f));
                            break;
                    }

                    _sculpt[i] += (want - here) * falloff;
                }
            }

            RecomputeRegion(x0 - 2, z0 - 2, x1 + 2, z1 + 2);

            // Collision is re-cooked when the stroke finishes, not on every
            // frame of it. Nothing is standing on ground being sculpted.
            RebuildRegion(x0 - 2, z0 - 2, x1 + 2, z1 + 2, false);

            // Shaping the ground under a run changes what that run is, so the
            // figures and the grade are read again rather than left stale.
            MeasureTrails();

            RaiseChanged(centre, radius + trailFalloff);
            return null;
        }

        float SlopePlane(Vector3 centre, float x, float z)
        {
            // The natural fall line at the brush centre, extended across the
            // brush. Sculpting to it gives a run you can actually ski.
            float ahead = NaturalHeight(centre.x, centre.z + 12f);
            float behind = NaturalHeight(centre.x, centre.z - 12f);
            float slope = (ahead - behind) / 24f;

            return (z - centre.z) * slope + (x - centre.x) * 0f;
        }

        float Neighbourhood(int ix, int iz)
        {
            float sum = 0f;
            int n = 0;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    sum += HeightAtIndex(ix + dx, iz + dz);
                    n++;
                }
            }

            return n > 0 ? sum / n : HeightAtIndex(ix, iz);
        }

        /// <summary>Paint the snow on whatever run the brush is over.</summary>
        public string PaintSnow(Vector3 centre, SnowQuality quality, bool? groomed)
        {
            Trail trail = TrailUnder(centre.x, centre.z, 4f);
            if (trail == null) return "Not on a run";

            trail.snow = quality;
            if (groomed.HasValue) trail.groomed = groomed.Value;

            Regenerate();
            return null;
        }

        // ---------------- building the field ---------------------------------

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            if (transform.position != Vector3.zero ||
                transform.rotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
            {
                Debug.LogWarning("[MountainGenerator] Keep this GameObject at the origin, " +
                                 "unrotated and unscaled, or heights will not line up.", this);
            }

            EnsureNoise();
            ClearLegacyMesh();
            Allocate();

            RecomputeRegion(0, 0, _nx - 1, _nz - 1);
            BuildChunks();

            for (int i = 0; i < _chunks.Count; i++) _chunks[i].Rebuild(this);

            MeasureTrails();
            RaiseChanged(Vector3.zero, Mathf.Max(width, length));
        }

        /// <summary>
        /// The terrain used to be one mesh on this object. A scene saved before
        /// the split still carries it, and it would sit there rendering and
        /// colliding with a shape that no longer exists.
        /// </summary>
        void ClearLegacyMesh()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = null;

            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = null;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        void Allocate()
        {
            int nx = Mathf.Max(4, Mathf.RoundToInt(width / cellSize) + 1);
            int nz = Mathf.Max(4, Mathf.RoundToInt(length / cellSize) + 1);

            bool resized = _h == null || nx != _nx || nz != _nz;

            _nx = nx;
            _nz = nz;
            _x0 = -width * 0.5f;
            _cellX = width / (_nx - 1);
            _cellZ = length / (_nz - 1);

            if (!resized) return;

            _h = new float[_nx * _nz];
            _sculpt = new float[_nx * _nz];
            _trailAt = new byte[_nx * _nz];

            // The chunk layout depends on the grid, so it has to go too.
            ClearChunks();
        }

        /// <summary>
        /// Rewrite a rectangle of the surface: nature, plus the player's
        /// shaping, with the runs carved back into it.
        /// </summary>
        void RecomputeRegion(int x0, int z0, int x1, int z1)
        {
            x0 = Mathf.Clamp(x0, 0, _nx - 1);
            x1 = Mathf.Clamp(x1, 0, _nx - 1);
            z0 = Mathf.Clamp(z0, 0, _nz - 1);
            z1 = Mathf.Clamp(z1, 0, _nz - 1);

            for (int iz = z0; iz <= z1; iz++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    int i = iz * _nx + ix;
                    _h[i] = BaseHeight(GridX(ix), GridZ(iz)) + _sculpt[i];
                    _trailAt[i] = 0;
                }
            }

            if (trails == null) return;

            for (int t = 0; t < trails.Count && t < 254; t++) CarveTrail(t, x0, z0, x1, z1);
        }

        /// <summary>
        /// Cut a run into the field.
        ///
        /// The centre line is first hung on the natural mountain, smoothed
        /// along its length and then forced to descend, so a run always skis
        /// and never climbs back uphill. The corridor is pulled onto that
        /// profile and blended back out to the mountain over the bank, which
        /// is what produces a cut bench on a sidehill without modelling one.
        /// </summary>
        void CarveTrail(int index, int rx0, int rz0, int rx1, int rz1)
        {
            Trail trail = trails[index];
            if (trail == null || !trail.Valid) return;

            // A run nowhere near the rectangle being rebuilt costs nothing.
            float reachGuess = trail.halfWidth + trailFalloff;
            if (trail.spine != null && trail.spine.Count >= 2 &&
                (trail.bounds.xMin - reachGuess > GridX(rx1) ||
                 trail.bounds.xMax + reachGuess < GridX(rx0) ||
                 trail.bounds.yMin - reachGuess > GridZ(rz1) ||
                 trail.bounds.yMax + reachGuess < GridZ(rz0)))
            {
                return;
            }

            trail.Resample(NaturalHeight);
            if (trail.spine.Count < 2) return;

            Profile(trail);

            float reach = trail.halfWidth + trailFalloff;

            // Only the vertices the run can possibly reach, and only inside the
            // rectangle being recomputed.
            int tx0 = Mathf.Max(rx0, IndexX(trail.bounds.xMin - reach));
            int tx1 = Mathf.Min(rx1, IndexX(trail.bounds.xMax + reach));
            int tz0 = Mathf.Max(rz0, IndexZ(trail.bounds.yMin - reach));
            int tz1 = Mathf.Min(rz1, IndexZ(trail.bounds.yMax + reach));

            if (tx0 > tx1 || tz0 > tz1) return;

            float noise = trail.surfaceNoise * (trail.groomed ? 0.45f : 1f);

            for (int iz = tz0; iz <= tz1; iz++)
            {
                for (int ix = tx0; ix <= tx1; ix++)
                {
                    float x = GridX(ix);
                    float z = GridZ(iz);

                    float along;
                    float d = trail.DistanceTo(x, z, out along);
                    if (d > reach) continue;

                    int i = iz * _nx + ix;
                    float natural = _h[i];

                    Vector3 centre = trail.PointAt(along);
                    float surface = centre.y;

                    // Lean slightly towards the mountain near the edges so the
                    // run does not read as a flat shelf stuck on a hillside.
                    float inside = Mathf.Clamp01(d / Mathf.Max(1f, trail.halfWidth));
                    surface = Mathf.Lerp(surface, natural, 0.22f * inside * inside);

                    if (d <= trail.halfWidth)
                    {
                        float edge = 1f - Smooth01(trail.halfWidth * 0.55f, trail.halfWidth, d);
                        surface += Fbm(x, z, noiseScale * 2.3f, 2) * noise * edge;

                        if (trail.hasRollers) surface += Roller(trail, along) * edge;

                        _trailAt[i] = (byte)(index + 1);
                        _h[i] = surface;
                        continue;
                    }

                    // The bank: back out to the mountain over the falloff.
                    float k = Smooth01(trail.halfWidth, reach, d);
                    _h[i] = Mathf.Lerp(surface, natural, k);
                }
            }
        }

        /// <summary>
        /// Smooth the run's own fall line and make sure it only ever goes down.
        /// Writes the result back into the spine, so the spine is the snow.
        /// </summary>
        void Profile(Trail trail)
        {
            var spine = trail.spine;
            int n = spine.Count;

            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = spine[i].y;

            // Three smoothing passes: enough to take the noise out of the line
            // without turning a steep run into a gentle one.
            var work = new float[n];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = Mathf.Max(0, i - 2);
                    int b = Mathf.Min(n - 1, i + 2);

                    float sum = 0f;
                    for (int j = a; j <= b; j++) sum += y[j];
                    work[i] = sum / (b - a + 1);
                }

                System.Array.Copy(work, y, n);
            }

            // Force a descent. Without this a player can draw a line across a
            // spur and produce a run with an uphill section in the middle,
            // which is not a run.
            float step = Trail.SpineSpacing * minimumTrailGrade;
            for (int i = 1; i < n; i++) y[i] = Mathf.Min(y[i], y[i - 1] - step);

            for (int i = 0; i < n; i++)
            {
                Vector3 p = spine[i];
                spine[i] = new Vector3(p.x, y[i], p.z);
            }
        }

        float Roller(Trail trail, float along)
        {
            if (trail.length < rollerSpacing * 1.5f) return 0f;

            float travelled = along * trail.length;
            float phase = travelled % rollerSpacing;

            float half = rollerLength * 0.5f;
            if (phase > rollerLength) return 0f;

            float t = (phase - half) / half;
            return 0.5f * (1f + Mathf.Cos(t * Mathf.PI)) * rollerHeight;
        }

        void MeasureTrails()
        {
            if (trails == null) return;

            for (int i = 0; i < trails.Count; i++)
            {
                Trail trail = trails[i];
                trail.Resample(SampleHeight);
                trail.Measure(SampleHeight);
            }
        }

        // ---------------- chunks ----------------------------------------------

        void ClearChunks()
        {
            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                if (_chunks[i] == null) continue;

                if (Application.isPlaying) Destroy(_chunks[i].gameObject);
                else DestroyImmediate(_chunks[i].gameObject);
            }

            _chunks.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != ContainerName) continue;

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            _container = null;
        }

        void BuildChunks()
        {
            if (_chunks.Count > 0 && _container != null) return;

            ClearChunks();

            var container = new GameObject(ContainerName);
            container.transform.SetParent(transform, false);
            container.hideFlags = HideFlags.DontSaveInEditor;
            container.layer = gameObject.layer;
            _container = container.transform;

            Material snow = Resolve(ref _runtimeSnow, snowMaterial,
                                    "SnowRuntime", new Color(0.93f, 0.95f, 1f), 0.30f);
            Material rock = Resolve(ref _runtimeRock, rockMaterial,
                                    "RockRuntime", new Color(0.30f, 0.29f, 0.29f), 0.06f);
            Material groomed = Resolve(ref _runtimeGroomed, groomedMaterial,
                                       "GroomedRuntime", new Color(0.97f, 0.98f, 1f), 0.44f);
            Material powder = Resolve(ref _runtimePowder, powderMaterial,
                                      "PowderRuntime", new Color(0.90f, 0.93f, 1f), 0.16f);

            int cells = Mathf.Max(8, chunkCells);

            for (int z0 = 0; z0 < _nz - 1; z0 += cells)
            {
                for (int x0 = 0; x0 < _nx - 1; x0 += cells)
                {
                    int x1 = Mathf.Min(x0 + cells, _nx - 1);
                    int z1 = Mathf.Min(z0 + cells, _nz - 1);

                    _chunks.Add(TerrainChunk.Create(_container, "Chunk " + x0 + "_" + z0,
                                                    x0, z0, x1, z1, snow, rock, groomed, powder));
                }
            }
        }

        Material Resolve(ref Material cache, Material assigned, string name, Color colour, float smooth)
        {
            if (assigned != null) return assigned;
            if (cache == null) cache = MaterialFactory.Create(name, colour, smooth);
            return cache;
        }

        void RebuildRegion(int x0, int z0, int x1, int z1, bool cookCollision = true)
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i] == null || !_chunks[i].Touches(x0, z0, x1, z1)) continue;
                _chunks[i].Rebuild(this, cookCollision);
            }
        }

        /// <summary>
        /// Put the collision back on anything a brush stroke left behind. Call
        /// it when the stroke ends; it costs nothing when nothing is stale.
        /// </summary>
        public void SettleColliders()
        {
            for (int i = 0; i < _chunks.Count; i++)
                if (_chunks[i] != null) _chunks[i].Settle();
        }

        void RaiseChanged(Vector3 centre, float radius)
        {
            if (TerrainChanged == null) return;

            TerrainChanged(Rect.MinMaxRect(centre.x - radius, centre.z - radius,
                                           centre.x + radius, centre.z + radius));
        }
    }
}
