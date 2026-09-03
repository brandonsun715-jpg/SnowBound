using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SnowBound.Core
{
    /// <summary>
    /// Tiny procedural mesh helpers. Everything in the prototype (trees,
    /// poles, lift towers) is built from tubes and cones so we never depend
    /// on external 3D assets.
    /// </summary>
    public static class PrimitiveMeshes
    {
        /// <summary>
        /// Appends a vertical tube to the given vertex/triangle lists.
        /// Set r1 = 0 to get a cone. Set r0 = r1 to get a cylinder.
        /// </summary>
        public static void AddTube(List<Vector3> verts, List<int> tris,
                                   Vector3 center, float y0, float y1,
                                   float r0, float r1, int segments,
                                   bool capBottom = true, bool capTop = true)
        {
            segments = Mathf.Max(3, segments);

            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 b0 = center + d0 * r0 + Vector3.up * y0;
                Vector3 b1 = center + d1 * r0 + Vector3.up * y0;
                Vector3 t1 = center + d1 * r1 + Vector3.up * y1;
                Vector3 t0 = center + d0 * r1 + Vector3.up * y1;

                int s = verts.Count;
                verts.Add(b0); verts.Add(b1); verts.Add(t1); verts.Add(t0);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);

                if (capBottom && r0 > 0.0001f)
                {
                    int c = verts.Count;
                    verts.Add(center + Vector3.up * y0); verts.Add(b0); verts.Add(b1);
                    tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
                }

                if (capTop && r1 > 0.0001f)
                {
                    int c = verts.Count;
                    verts.Add(center + Vector3.up * y1); verts.Add(t1); verts.Add(t0);
                    tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
                }
            }
        }

        /// <summary>
        /// Finishes a mesh. Pass one triangle list per sub-mesh; the renderer's
        /// material slots line up with the order you pass them in.
        /// </summary>
        public static Mesh BuildMesh(string name, List<Vector3> verts, params List<int>[] subMeshes)
        {
            var mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subMeshes.Length;
            for (int i = 0; i < subMeshes.Length; i++) mesh.SetTriangles(subMeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
