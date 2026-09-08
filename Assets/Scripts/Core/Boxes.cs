using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Boxes with real texture coordinates.
    ///
    /// Unity's cube primitive is one metre across with its UVs running nought
    /// to one on every face, so scaling it to a fifteen metre wall stretches a
    /// single tile of timber over fifteen metres and the material reads as a
    /// smear. Building the box at its real size instead, with coordinates in
    /// metres, gives every surface in the resort the same texel density —
    /// which is most of what makes a set of materials look like one world
    /// rather than a collection of separately-textured objects.
    ///
    /// Meshes are shared between boxes of the same size, so a lift line of
    /// forty identical crossarms is one mesh and one draw call.
    /// </summary>
    public static class Boxes
    {
        static readonly Dictionary<long, Mesh> _cache = new Dictionary<long, Mesh>();

        /// <summary>A box mesh of this size, centred on its own origin.</summary>
        public static Mesh Mesh(Vector3 size)
        {
            long key = Key(size);

            Mesh cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            PrimitiveMeshes.AddBox(verts, tris, Vector3.zero, size);

            Mesh mesh = PrimitiveMeshes.BuildMesh("Box", verts, tris);
            _cache[key] = mesh;

            return mesh;
        }

        /// <summary>
        /// A box in the world. Scale stays at one and the size lives in the
        /// mesh, which is the only way the texture coordinates can mean metres.
        /// </summary>
        public static GameObject Create(Transform parent, string name, Vector3 localPosition,
                                        Vector3 size, Material material, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            go.AddComponent<MeshFilter>().sharedMesh = Mesh(size);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (collider) go.AddComponent<BoxCollider>().size = size;

            return go;
        }

        /// <summary>Rounded to the millimetre, so near-identical boxes share a mesh.</summary>
        static long Key(Vector3 size)
        {
            long x = Mathf.RoundToInt(Mathf.Abs(size.x) * 1000f);
            long y = Mathf.RoundToInt(Mathf.Abs(size.y) * 1000f);
            long z = Mathf.RoundToInt(Mathf.Abs(size.z) * 1000f);

            return (x * 1000003L + y) * 1000003L + z;
        }
    }
}
