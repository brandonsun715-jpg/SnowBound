#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnowBound.EditorTools
{
    /// <summary>
    /// Import settings for the models and textures dropped into the project.
    ///
    /// Unity guesses when it first imports a file, and its guesses are wrong in
    /// two ways that matter here. A normal map imported as a colour texture is
    /// decoded through sRGB and comes out with the wrong slopes, so surfaces
    /// light incorrectly and nobody can see why. And a metallic or roughness
    /// map is data, not colour, so it must not be gamma corrected either.
    ///
    /// Doing it here rather than by hand means an asset dropped into the
    /// project is correct the first time it is imported, on any machine, with
    /// nobody having to remember.
    /// </summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        const string Folder = "/Resources/Models/";

        bool Ours { get { return assetPath.Replace("\\", "/").Contains(Folder); } }

        void OnPreprocessTexture()
        {
            if (!Ours) return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 2048;

            if (file.EndsWith("_normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                return;
            }

            if (file.EndsWith("_metallic") || file.EndsWith("_roughness") || file.EndsWith("_mask"))
            {
                // Data, not colour. And readable, because the two are combined
                // into the one map URP actually wants.
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
        }

        void OnPreprocessModel()
        {
            if (!Ours) return;

            var importer = (ModelImporter)assetImporter;

            // The scene builds its own materials from the maps beside the file,
            // so the ones inside it are only a source of confusion.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;

            // The forest and the rocks are welded into batched meshes rather
            // than spawned one by one, and welding needs to read the mesh.
            // Everything else stays unreadable, because a readable mesh is a
            // second copy of it in memory.
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.isReadable = file == "Trees" || file == "Rocks";
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = true;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // Collision comes from the placeholder the model replaces, which is
            // a handful of boxes rather than a third of a million triangles.
            importer.addCollider = false;
        }
    }

    /// <summary>
    /// Applies those settings to art that was already in the project.
    ///
    /// A postprocessor only runs when a file is imported. Anything dropped in
    /// before this script existed kept Unity's guesses, and there is no sign
    /// of it: a normal map imported as a colour texture is unpacked by URP
    /// from the wrong two channels, so the surface lights as if it were
    /// crumpled foil — which reads, on a large pale building, as a flat grey
    /// box. The fix is to reimport it, and nobody should have to know that.
    ///
    /// So on load, anything whose settings disagree with what the
    /// postprocessor would have given it is reimported once, which puts the
    /// postprocessor back in charge of it.
    /// </summary>
    [InitializeOnLoad]
    public static class ModelImportRepair
    {
        static ModelImportRepair()
        {
            EditorApplication.delayCall += Repair;
        }

        [MenuItem("SnowBound/Reimport Model Textures", false, 42)]
        static void Repair()
        {
            const string folder = "Assets/Resources/Models";
            if (!System.IO.Directory.Exists(folder)) return;

            int fixedUp = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (!Wrong(importer, path)) continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fixedUp++;
            }

            if (fixedUp > 0)
                Debug.Log("[SnowBound] Reimported " + fixedUp +
                          " model texture(s) with the wrong import settings.");
        }

        /// <summary>Does this file disagree with what the postprocessor asks for?</summary>
        static bool Wrong(TextureImporter importer, string path)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (file.EndsWith("_normal"))
                return importer.textureType != TextureImporterType.NormalMap;

            if (file.EndsWith("_metallic") || file.EndsWith("_roughness") || file.EndsWith("_mask"))
                return importer.sRGBTexture || !importer.isReadable;

            return false;
        }
    }
}
#endif
