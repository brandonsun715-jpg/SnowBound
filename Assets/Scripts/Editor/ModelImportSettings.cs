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

            importer.isReadable = false;
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
}
#endif
