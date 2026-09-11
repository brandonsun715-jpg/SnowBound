using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Weather;

namespace SnowBound.Player
{
    /// <summary>
    /// Draws the lines the rider leaves in the snow.
    ///
    /// It records a point every so many metres, raycasts down to find the
    /// real surface, and stitches a ribbon of triangles just above it. Skis
    /// leave two thin lines, a board leaves one wide one.
    ///
    /// Ribbons are written into fixed-size chunks and the oldest chunk is
    /// thrown away once there are too many, so a long run costs a bounded
    /// amount of memory. When real snow deformation replaces this, only this
    /// file changes.
    /// </summary>
    public class SnowTrackWriter : MonoBehaviour
    {
        [Tooltip("Leave empty to use the PlayerController on this object.")]
        public PlayerController player;

        [Header("Shape")]
        [Tooltip("Metres between recorded points. Smaller follows turns more closely.")]
        public float segmentLength = 0.55f;
        public float skiTrackWidth = 0.13f;
        [Tooltip("Distance between the two ski lines.")]
        public float skiSpacing = 0.16f;
        public float boardTrackWidth = 0.34f;
        [Tooltip("Metres above the snow, so the ribbon never z-fights the terrain.")]
        public float surfaceOffset = 0.06f;

        [Header("When to draw")]
        public float minSpeed = 1.5f;
        public LayerMask groundMask = ~0;

        [Header("Filling in")]
        [Tooltip("Minutes of steady snowfall before a track has filled in and gone.\nNothing fills in while it is not snowing.")]
        public float fillMinutes = 1.4f;
        [Tooltip("The colour a fresh track is cut in, and the colour it ends up\nwhen new snow has covered it. It fills in rather than fading out, which\nis both what happens and what an opaque material can do.")]
        public Color cutColour = new Color(0.80f, 0.84f, 0.92f);
        public Color filledColour = new Color(0.95f, 0.97f, 1.00f);

        [Header("Memory")]
        [Tooltip("Points per chunk of track mesh.")]
        public int pointsPerChunk = 240;
        [Tooltip("Chunks kept before the oldest is discarded.")]
        public int maxChunks = 14;

        Transform _container;
        Material _material;

        /// <summary>One written ribbon and how far the weather has buried it.</summary>
        class Chunk
        {
            public GameObject go;
            public MeshRenderer renderer;
            public float filled;
        }

        readonly List<Chunk> _chunks = new List<Chunk>();
        WeatherSystem _weather;

        // Built in Start, not here. A field initializer runs inside the
        // MonoBehaviour's constructor, and Unity builds components on a
        // thread where most of its own types cannot be created yet — a
        // MaterialPropertyBlock among them. Written here it throws before
        // the component has begun to exist.
        MaterialPropertyBlock _block;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<int> _tris = new List<int>();

        Mesh _mesh;
        int _points;
        int _ribbons;
        LocomotionKind _chunkKind;

        bool _hasLast;
        Vector3 _lastPoint;

        void Start()
        {
            if (player == null) player = GetComponent<PlayerController>();

            var container = new GameObject("SnowTracks");
            container.hideFlags = HideFlags.DontSaveInEditor;
            _container = container.transform;

            _material = MaterialFactory.CreateSurface("SnowTrack", ProceduralTextures.Packed(),
                                                      cutColour, 2.4f, 1.4f);

            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (player == null) return;

            FillIn();

            bool drawing = player.IsRidingSnow && player.OnSnow && player.Speed >= minSpeed;
            if (!drawing)
            {
                _hasLast = false;
                return;
            }

            if (player.CurrentKind != _chunkKind) StartChunk(player.CurrentKind);

            Vector3 here = transform.position;
            if (_hasLast)
            {
                float step = Vector3.Distance(here, _lastPoint);
                if (step < segmentLength) return;
                // A teleport must not draw a stripe across the mountain.
                if (step > 6f) { _hasLast = false; }
            }

            AddPoint(here);
        }

        void StartChunk(LocomotionKind kind)
        {
            _chunkKind = kind;
            _ribbons = kind == LocomotionKind.Ski ? 2 : 1;

            var go = new GameObject("TrackChunk");
            go.transform.SetParent(_container, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            _mesh = new Mesh();
            _mesh.name = "TrackMesh";
            _mesh.hideFlags = HideFlags.DontSave;
            _mesh.MarkDynamic();

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _chunks.Add(new Chunk { go = go, renderer = renderer });
            _verts.Clear();
            _tris.Clear();
            _points = 0;
            _hasLast = false;

            while (_chunks.Count > maxChunks)
            {
                Chunk oldest = _chunks[0];
                _chunks.RemoveAt(0);
                DestroyChunk(oldest.go);
            }
        }

        /// <summary>
        /// New snow covers old tracks.
        ///
        /// A track is a groove, not a stain: what removes it is snow landing
        /// in it, so it fills back up to the colour of the field around it
        /// rather than fading away to nothing — which is also the only thing
        /// an opaque material can honestly do. Nothing happens at all while
        /// it is not snowing, so a clear day keeps every line you cut.
        ///
        /// The chunk being written is left alone: it would fill in while it
        /// was still being drawn.
        /// </summary>
        void FillIn()
        {
            if (_chunks.Count == 0) return;

            if (_block == null) _block = new MaterialPropertyBlock();
            if (_weather == null) _weather = WeatherSystem.Instance;
            float falling = _weather != null ? _weather.Snowfall : 0f;
            if (falling <= 0.001f) return;

            float rate = falling * Time.deltaTime / Mathf.Max(1f, fillMinutes * 60f);

            for (int i = 0; i < _chunks.Count - 1; i++)
            {
                Chunk chunk = _chunks[i];
                if (chunk.renderer == null) continue;

                chunk.filled = Mathf.Min(1f, chunk.filled + rate);

                _block.SetColor("_BaseColor", Color.Lerp(cutColour, filledColour, chunk.filled));
                _block.SetColor("_Color", Color.Lerp(cutColour, filledColour, chunk.filled));
                chunk.renderer.SetPropertyBlock(_block);

                // Buried is gone. Keeping it costs a draw call to render snow
                // on top of snow.
                if (chunk.filled >= 1f)
                {
                    DestroyChunk(chunk.go);
                    _chunks.RemoveAt(i);
                    i--;
                }
            }
        }

        void AddPoint(Vector3 position)
        {
            if (_mesh == null) StartChunk(player.CurrentKind);

            // Find the real snow surface rather than trusting the feet.
            Vector3 surface;
            Vector3 normal;
            if (Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit,
                                2.5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                surface = hit.point;
                normal = hit.normal;
            }
            else
            {
                surface = position;
                normal = Vector3.up;
            }

            Vector3 travel = _hasLast ? surface - _lastPoint : transform.forward;
            travel = Vector3.ProjectOnPlane(travel, normal);
            if (travel.sqrMagnitude < 0.0001f) travel = Vector3.ProjectOnPlane(transform.forward, normal);
            if (travel.sqrMagnitude < 0.0001f) return;
            travel.Normalize();

            Vector3 right = Vector3.Cross(normal, travel).normalized;
            Vector3 lift = normal * surfaceOffset;

            float halfWidth = (_ribbons == 2 ? skiTrackWidth : boardTrackWidth) * 0.5f;

            int first = _verts.Count;
            for (int r = 0; r < _ribbons; r++)
            {
                float offset = _ribbons == 2 ? (r == 0 ? -skiSpacing : skiSpacing) : 0f;
                Vector3 centre = surface + right * offset + lift;
                _verts.Add(centre - right * halfWidth);
                _verts.Add(centre + right * halfWidth);
            }

            if (_hasLast)
            {
                int stride = _ribbons * 2;
                int previous = first - stride;

                for (int r = 0; r < _ribbons; r++)
                {
                    int a0 = previous + r * 2;
                    int a1 = a0 + 1;
                    int b0 = first + r * 2;
                    int b1 = b0 + 1;

                    _tris.Add(a0); _tris.Add(b0); _tris.Add(b1);
                    _tris.Add(a0); _tris.Add(b1); _tris.Add(a1);
                }
            }

            _lastPoint = surface;
            _hasLast = true;
            _points++;

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_points >= pointsPerChunk)
            {
                // Carry the last point into the next chunk so the ribbon joins up.
                Vector3 carry = _lastPoint;
                StartChunk(_chunkKind);
                AddPoint(carry);
            }
        }

        /// <summary>Destroying the object alone would leak its mesh.</summary>
        static void DestroyChunk(GameObject chunk)
        {
            if (chunk == null) return;

            var filter = chunk.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);

            Destroy(chunk);
        }

        void OnDestroy()
        {
            foreach (Chunk chunk in _chunks) DestroyChunk(chunk.go);
            _chunks.Clear();

            if (_container != null) Destroy(_container.gameObject);
        }
    }
}
