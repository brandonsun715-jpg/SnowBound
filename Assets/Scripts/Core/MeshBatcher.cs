using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SnowBound.Core
{
    /// <summary>
    /// Collects many small pieces of geometry into a few large meshes.
    ///
    /// Five hundred trees as five hundred objects is five hundred draw calls;
    /// the same trees welded into nine meshes is nine. That difference is the
    /// whole reason this exists, and it matters most on the machines least
    /// able to absorb it.
    ///
    /// Each material gets its own sub-mesh slot, so one batch can still hold
    /// bark, three shades of needle and snow.
    /// </summary>
    public class MeshBatcher
    {
        readonly Transform _parent;
        readonly string _name;
        readonly Material[] _materials;
        readonly int _maxVertices;

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int>[] _slots;
        readonly bool _unwrapped;
        int _chunk;

        /// <summary>
        /// Set <paramref name="unwrapped"/> when the pieces come from models
        /// that carry their own texture coordinates. Everything generated in
        /// this project has none, and gets a world-space projection instead —
        /// but projecting over a model's own unwrap would throw its baked
        /// textures away.
        /// </summary>
        public MeshBatcher(Transform parent, string name, Material[] materials,
                           int maxVertices = 60000, bool unwrapped = false)
        {
            _parent = parent;
            _name = name;
            _materials = materials;
            _unwrapped = unwrapped;
            _maxVertices = Mathf.Max(3000, maxVertices);

            _slots = new List<int>[materials.Length];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = new List<int>();
        }

        /// <summary>Add one piece, placed by <paramref name="placement"/>.</summary>
        public void Add(List<Vector3> vertices, List<int> triangles, int slot, Matrix4x4 placement,
                        List<Vector2> uvs = null)
        {
            if (vertices == null || triangles == null) return;
            if (slot < 0 || slot >= _slots.Length) return;

            if (_vertices.Count + vertices.Count > _maxVertices) Flush();

            int start = _vertices.Count;
            for (int i = 0; i < vertices.Count; i++)
                _vertices.Add(placement.MultiplyPoint3x4(vertices[i]));

            if (_unwrapped)
                for (int i = 0; i < vertices.Count; i++)
                    _uvs.Add(uvs != null && i < uvs.Count ? uvs[i] : Vector2.zero);

            List<int> target = _slots[slot];
            for (int i = 0; i < triangles.Count; i++)
                target.Add(start + triangles[i]);
        }

        /// <summary>Write out whatever has been collected so far.</summary>
        public void Flush()
        {
            if (_vertices.Count == 0) return;

            var go = new GameObject(_name + " " + _chunk++);
            go.transform.SetParent(_parent, false);

            var mesh = new Mesh();
            mesh.name = _name + "Mesh";
            mesh.hideFlags = HideFlags.DontSave;
            mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            mesh.subMeshCount = _slots.Length;
            for (int i = 0; i < _slots.Length; i++) mesh.SetTriangles(_slots[i], i);

            // Batched geometry is welded from meshes that were never unwrapped,
            // so it gets the same world-space projection everything else does.
            // Without it a textured material on the forest is a flat colour.
            // Models brought their own, and projecting over those would throw
            // away the textures that were baked to them.
            if (_unwrapped && _uvs.Count == _vertices.Count) mesh.SetUVs(0, _uvs);
            else PrimitiveMeshes.ProjectUVs(mesh);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = _materials;

            _vertices.Clear();
            _uvs.Clear();
            for (int i = 0; i < _slots.Length; i++) _slots[i].Clear();
        }
    }
}
