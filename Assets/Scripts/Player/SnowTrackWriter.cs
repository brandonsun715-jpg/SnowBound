using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

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

        [Header("Memory")]
        [Tooltip("Points per chunk of track mesh.")]
        public int pointsPerChunk = 240;
        [Tooltip("Chunks kept before the oldest is discarded.")]
        public int maxChunks = 14;

        Transform _container;
        Material _material;

        readonly List<GameObject> _chunks = new List<GameObject>();
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

            _material = MaterialFactory.Create("SnowTrack", new Color(0.74f, 0.79f, 0.89f), 0.05f);
        }

        void Update()
        {
            if (player == null) return;

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

            _chunks.Add(go);
            _verts.Clear();
            _tris.Clear();
            _points = 0;
            _hasLast = false;

            while (_chunks.Count > maxChunks)
            {
                GameObject oldest = _chunks[0];
                _chunks.RemoveAt(0);
                DestroyChunk(oldest);
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
            foreach (GameObject chunk in _chunks) DestroyChunk(chunk);
            _chunks.Clear();

            if (_container != null) Destroy(_container.gameObject);
        }
    }
}
