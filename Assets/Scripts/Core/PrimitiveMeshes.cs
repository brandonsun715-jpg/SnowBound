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
        /// Appends a triangular prism: the classic gable roof shape. The
        /// triangle sits in the X/Y plane and is extruded along Z, so the
        /// ridge runs along Z and the gable ends face forward and back.
        /// Base sits at y = 0. Faces do not share vertices, so the roof keeps
        /// crisp edges instead of looking melted.
        /// </summary>
        public static void AddPrism(List<Vector3> verts, List<int> tris,
                                    Vector3 center, float halfWidth, float height, float length)
        {
            float hl = length * 0.5f;

            Vector3 a = center + new Vector3(-halfWidth, 0f, -hl);
            Vector3 b = center + new Vector3(halfWidth, 0f, -hl);
            Vector3 c = center + new Vector3(0f, height, -hl);
            Vector3 d = center + new Vector3(-halfWidth, 0f, hl);
            Vector3 e = center + new Vector3(halfWidth, 0f, hl);
            Vector3 f = center + new Vector3(0f, height, hl);

            AddTri(verts, tris, a, c, b);        // gable end, front
            AddTri(verts, tris, d, e, f);        // gable end, back
            AddQuad(verts, tris, a, d, f, c);    // left slope
            AddQuad(verts, tris, b, c, f, e);    // right slope
            AddQuad(verts, tris, a, b, e, d);    // underside
        }

        /// <summary>A flat annulus lying in the XZ plane, facing up.</summary>
        public static void AddRing(List<Vector3> verts, List<int> tris, Vector3 centre,
                                   float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(8, segments);

            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                AddQuad(verts, tris,
                        centre + d0 * innerRadius, centre + d0 * outerRadius,
                        centre + d1 * outerRadius, centre + d1 * innerRadius);
            }
        }

        public static void AddTri(List<Vector3> verts, List<int> tris,
                                  Vector3 p0, Vector3 p1, Vector3 p2)
        {
            int s = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        }

        public static void AddQuad(List<Vector3> verts, List<int> tris,
                                   Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int s = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        /// <summary>
        /// A box with its own vertices per face, so every face takes its own
        /// texture projection and the corners stay crisp.
        /// </summary>
        public static void AddBox(List<Vector3> verts, List<int> tris, Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;

            Vector3 a = centre + new Vector3(-h.x, -h.y, -h.z);
            Vector3 b = centre + new Vector3(h.x, -h.y, -h.z);
            Vector3 c = centre + new Vector3(h.x, -h.y, h.z);
            Vector3 d = centre + new Vector3(-h.x, -h.y, h.z);

            Vector3 e = centre + new Vector3(-h.x, h.y, -h.z);
            Vector3 f = centre + new Vector3(h.x, h.y, -h.z);
            Vector3 g = centre + new Vector3(h.x, h.y, h.z);
            Vector3 i = centre + new Vector3(-h.x, h.y, h.z);

            AddQuad(verts, tris, e, f, g, i);   // top
            AddQuad(verts, tris, d, c, b, a);   // bottom
            AddQuad(verts, tris, a, b, f, e);   // front
            AddQuad(verts, tris, c, d, i, g);   // back
            AddQuad(verts, tris, d, a, e, i);   // left
            AddQuad(verts, tris, b, c, g, f);   // right
        }

        /// <summary>
        /// Texture coordinates in metres, projected per triangle onto whichever
        /// axis that triangle faces.
        ///
        /// None of the geometry in this game is unwrapped, and a mesh with no
        /// UVs samples the same single texel everywhere — so a textured
        /// material on it looks like a flat colour, which is exactly what it
        /// was supposed to stop being. Projecting from world position is not a
        /// real unwrap, but on boxes, prisms, tubes and terrain it is
        /// indistinguishable from one, and it costs nothing to author.
        /// </summary>
        public static void ProjectUVs(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0) return;

            Vector3[] verts = mesh.vertices;
            var uvs = new Vector2[verts.Length];

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);

                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];

                    Vector3 n = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);

                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);

                    if (ay >= ax && ay >= az)
                    {
                        uvs[i0] = new Vector2(verts[i0].x, verts[i0].z);
                        uvs[i1] = new Vector2(verts[i1].x, verts[i1].z);
                        uvs[i2] = new Vector2(verts[i2].x, verts[i2].z);
                    }
                    else if (ax >= az)
                    {
                        uvs[i0] = new Vector2(verts[i0].z, verts[i0].y);
                        uvs[i1] = new Vector2(verts[i1].z, verts[i1].y);
                        uvs[i2] = new Vector2(verts[i2].z, verts[i2].y);
                    }
                    else
                    {
                        uvs[i0] = new Vector2(verts[i0].x, verts[i0].y);
                        uvs[i1] = new Vector2(verts[i1].x, verts[i1].y);
                        uvs[i2] = new Vector2(verts[i2].x, verts[i2].y);
                    }
                }
            }

            mesh.uv = uvs;
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
            ProjectUVs(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
