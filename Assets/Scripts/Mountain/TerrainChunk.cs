using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// One square of the mountain: a mesh, the collider for that same mesh,
    /// and nothing else.
    ///
    /// The terrain is split up so that editing it costs the chunks you
    /// touched rather than the whole mountain. Rebuilding fifty thousand
    /// vertices and re-cooking their collision every frame of a brush stroke
    /// is what makes terrain tools feel broken; rebuilding a thousand does
    /// not.
    ///
    /// The renderer and the collider are handed the same mesh in the same
    /// call, which is the only way to guarantee that what you can see and
    /// what you can stand on agree.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(SnowSurface))]
    public class TerrainChunk : MonoBehaviour
    {
        public int x0, z0, x1, z1;   // inclusive vertex range in the height field

        Mesh _mesh;
        MeshFilter _filter;
        MeshCollider _collider;
        MeshRenderer _renderer;

        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int> _surface = new List<int>();

        /// <summary>
        /// One list per surface. Six is more than a terrain usually needs, but
        /// corduroy, powder, ice and bare rock are genuinely different
        /// materials and putting them on one shared texture is exactly what
        /// made the mountain read as a single white sheet.
        /// </summary>
        const int Surfaces = 6;

        readonly List<int>[] _faces = new List<int>[Surfaces];

        public static TerrainChunk Create(Transform parent, string name,
                                          int x0, int z0, int x1, int z1,
                                          Material[] surfaces)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.hideFlags = HideFlags.DontSaveInEditor;
            go.layer = parent.gameObject.layer;

            var chunk = go.AddComponent<TerrainChunk>();
            chunk.x0 = x0; chunk.z0 = z0; chunk.x1 = x1; chunk.z1 = z1;

            chunk._filter = go.GetComponent<MeshFilter>();
            chunk._collider = go.GetComponent<MeshCollider>();
            chunk._renderer = go.GetComponent<MeshRenderer>();
            chunk._renderer.sharedMaterials = surfaces;

            for (int i = 0; i < Surfaces; i++) chunk._faces[i] = new List<int>();

            return chunk;
        }

        public bool Touches(int ax0, int az0, int ax1, int az1)
        {
            return ax0 <= x1 && ax1 >= x0 && az0 <= z1 && az1 >= z0;
        }

        bool _colliderStale;

        /// <summary>
        /// Rebuild this chunk's geometry from the height field.
        ///
        /// Cooking collision is by far the most expensive part, so during a
        /// brush stroke it is skipped and the chunk is marked stale instead.
        /// Settle puts the collision back once the stroke ends, which is the
        /// only moment anything is going to stand on it.
        /// </summary>
        public void Rebuild(MountainGenerator mountain, bool cookCollision = true)
        {
            if (mountain == null || !mountain.Ready) return;

            int nx = x1 - x0 + 1;
            int nz = z1 - z0 + 1;
            if (nx < 2 || nz < 2) return;

            _verts.Clear(); _normals.Clear(); _uvs.Clear(); _surface.Clear();
            for (int i = 0; i < Surfaces; i++) _faces[i].Clear();

            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int gx = x0 + ix;
                    int gz = z0 + iz;

                    float x = mountain.GridX(gx);
                    float z = mountain.GridZ(gz);

                    _verts.Add(new Vector3(x, mountain.HeightAtIndex(gx, gz), z));

                    // Normals come from the height field rather than from this
                    // chunk's own triangles, so neighbouring chunks agree along
                    // their shared edge and the seam does not show.
                    _normals.Add(mountain.NormalAtIndex(gx, gz));

                    // World metres. The materials decide their own tiling from
                    // that, so "eight metres per tile of corduroy" is a number
                    // written once in one place and not divided into the mesh.
                    _uvs.Add(new Vector2(x, z));
                    _surface.Add(mountain.SurfaceAtIndex(gx, gz));
                }
            }

            for (int iz = 0; iz < nz - 1; iz++)
            {
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int i = iz * nx + ix;
                    Sort(mountain, i, i + nx, i + nx + 1);
                    Sort(mountain, i, i + nx + 1, i + 1);
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = gameObject.name + "Mesh" };
                _mesh.hideFlags = HideFlags.DontSave;
                _mesh.indexFormat = IndexFormat.UInt32;
                _mesh.MarkDynamic();
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.subMeshCount = Surfaces;
            for (int i = 0; i < Surfaces; i++) _mesh.SetTriangles(_faces[i], i);

            // Normal mapping needs tangents, and the terrain has real normal
            // maps on it now. Without these the snow is flat again.
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();

            _filter.sharedMesh = _mesh;

            if (!cookCollision) { _colliderStale = true; return; }

            Cook();
        }

        /// <summary>
        /// Give the collider the very same mesh the renderer has. Clearing it
        /// first forces Unity to re-cook rather than keep the stale shape,
        /// which is the classic way to end up walking through a hill that is
        /// visibly in front of you.
        /// </summary>
        void Cook()
        {
            _colliderStale = false;
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }

        /// <summary>Cook collision if a stroke left it behind. Cheap when it did not.</summary>
        public void Settle()
        {
            if (_colliderStale) Cook();
        }

        /// <summary>
        /// A triangle belongs to a run if any of its corners does, and a run is
        /// always snow however steep it is.
        /// </summary>
        void Sort(MountainGenerator mountain, int a, int b, int c)
        {
            int surface = Mathf.Max(_surface[a], Mathf.Max(_surface[b], _surface[c]));

            if (surface == 0)
            {
                // Off the runs, snow settles on gentle ground and slides off
                // steep ground, so a face past the rock angle is bare stone.
                Vector3 normal = Vector3.Cross(_verts[b] - _verts[a], _verts[c] - _verts[a]);

                if (normal.sqrMagnitude > 1e-10f &&
                    Vector3.Angle(normal.normalized, Vector3.up) > mountain.rockAngle)
                {
                    surface = 1;
                }
            }

            List<int> target = _faces[Mathf.Clamp(surface, 0, Surfaces - 1)];

            target.Add(a);
            target.Add(b);
            target.Add(c);
        }

        void OnDestroy()
        {
            if (_mesh == null) return;

            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }
}
