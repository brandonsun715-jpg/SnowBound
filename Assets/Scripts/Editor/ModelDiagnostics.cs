#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.EditorTools
{
    /// <summary>
    /// Reports what the scatter is actually working with.
    ///
    /// A screenshot can say the forest looks wrong but not why, and the two
    /// halves of "wrong" need opposite fixes: either the geometry arrives
    /// broken, or it arrives fine and is placed badly. This asks both
    /// questions separately.
    ///
    /// First it reads each part the way MountainProps reads it and prints the
    /// size it came back as. A pine is about eleven metres tall in its own
    /// space; if it reports a tenth of that, nothing downstream can save it.
    ///
    /// Then it measures what actually ended up in the scene — every batched
    /// mesh, its chunk count and its world bounds. A forest chunk should be
    /// tens of metres tall. One that is a metre tall is being flattened after
    /// the geometry was read, which is a different bug in a different place.
    /// </summary>
    public static class ModelDiagnostics
    {
        [MenuItem("SnowBound/Diagnose Models", false, 43)]
        static void Diagnose()
        {
            var report = new StringBuilder();
            report.AppendLine("[SnowBound] Model diagnostics");
            report.AppendLine();

            report.AppendLine("As read, in the model's own space (metres):");
            Part(report, HeroAssets.Trees, "TreeA");
            Part(report, HeroAssets.Trees, "TreeB");
            Part(report, HeroAssets.Trees, "TreeC");
            Part(report, HeroAssets.Trees, "SnowA");
            Part(report, HeroAssets.Rocks, "RockA");
            Part(report, HeroAssets.Rocks, "RockSlab");
            Part(report, HeroAssets.Rocks, "RockSpire");
            Part(report, HeroAssets.Flora, "Fallen");
            Part(report, HeroAssets.Flora, "ShrubA");
            Part(report, HeroAssets.Props, "Bench");
            Part(report, HeroAssets.Props, "Lamp");

            report.AppendLine();
            report.AppendLine("As placed, in the scene (world metres):");

            var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None);
            int found = 0;

            foreach (MeshFilter filter in filters)
            {
                string name = filter.gameObject.name;

                // The batched scatters, which are the ones in question. Each
                // is named "<batch> <chunk number>" by MeshBatcher.
                if (!name.StartsWith("Forest") && !name.StartsWith("Rocks") &&
                    !name.StartsWith("RockSnow") && !name.StartsWith("Undergrowth") &&
                    !name.StartsWith("Dressing") && !name.StartsWith("TreeSnow"))
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds b = GetRenderBounds(filter);

                report.AppendLine(string.Format(
                    "  {0,-16} {1,7} verts   size {2,7:0.0} x {3,7:0.0} x {4,7:0.0}",
                    name, mesh.vertexCount, b.size.x, b.size.y, b.size.z));

                found++;
            }

            if (found == 0)
                report.AppendLine("  nothing batched found — enter Play mode first, " +
                                  "or the scatter never ran.");

            Debug.Log(report.ToString());
        }

        static Bounds GetRenderBounds(MeshFilter filter)
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            return renderer != null ? renderer.bounds : filter.sharedMesh.bounds;
        }

        static void Part(StringBuilder report, string folder, string part)
        {
            HeroAssets.Piece piece = HeroAssets.Geometry(folder, part);

            if (piece == null)
            {
                report.AppendLine(string.Format("  {0,-14} {1,-10} NOT READ — model missing, " +
                                                "or not readable", folder, part));
                return;
            }

            var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < piece.vertices.Count; i++)
            {
                low = Vector3.Min(low, piece.vertices[i]);
                high = Vector3.Max(high, piece.vertices[i]);
            }

            Vector3 size = high - low;

            report.AppendLine(string.Format(
                "  {0,-14} {1,-10} {2,6} verts  size {3,6:0.00} x {4,6:0.00} x {5,6:0.00}" +
                "   base y {6,6:0.00}   uvs {7}",
                folder, part, piece.vertices.Count, size.x, size.y, size.z, low.y,
                piece.uvs.Count == piece.vertices.Count ? "ok" : "MISSING"));
        }
    }
}
#endif
