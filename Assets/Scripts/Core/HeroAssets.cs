using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// The real models, loaded once and shared.
    ///
    /// Everything in the resort is still built from procedural geometry, and
    /// still is what the game reasons about: colliders, entrances, boarding
    /// points and footprints all come off the placeholder. A hero model is
    /// only a set of renderers standing in exactly the same place, so a
    /// missing or broken asset costs the look of the thing and nothing else.
    ///
    /// A model is a folder under Resources/Models holding an FBX and its four
    /// maps, all named after the folder. They are built by tools/modelgen, in
    /// Blender, from scripts that live in this repository — so a model is
    /// something anybody can change and rebuild rather than a binary nobody
    /// can open.
    ///
    /// One material per model, one mask per model, both cached: a lift has
    /// dozens of chairs on it and each one building its own copy of a
    /// two-thousand pixel texture is how a scene load turns into a stall.
    /// </summary>
    public static class HeroAssets
    {
        /// <summary>
        /// What the models were drawn at, in metres. The game scales them to
        /// whatever the lift or the rider actually uses, and these are the
        /// numbers it scales from. They are the same numbers the generator
        /// builds to: change one in tools/modelgen and change it here.
        /// </summary>
        public const float ChairDrop = 2.10f;      // grip to the top of the seat
        public const float TowerHeight = 9.00f;    // ground to the cable
        public const float StationCable = 3.40f;   // ground to the cable
        public const float SkiLength = 1.72f;
        public const float PoleLength = 1.20f;
        public const float BoardLength = 1.55f;

        public const string Chair = "ChairliftChair";
        public const string Tower = "ChairliftTower";
        public const string Station = "ChairliftStation";
        public const string Ski = "Ski";
        public const string Pole = "SkiPole";
        public const string Board = "Snowboard";
        public const string RiderSki = "RiderSki";
        public const string RiderBoard = "RiderBoard";

        /// <summary>What a spawned model is called, so it can be told apart
        /// from the placeholder it is standing in for.</summary>
        public const string Container = "HeroModel";

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> Surfaces = new Dictionary<string, Material>();

        /// <summary>The imported model, or null if it is not in the project.</summary>
        public static GameObject Prefab(string folder)
        {
            GameObject prefab;
            if (Models.TryGetValue(folder, out prefab) && prefab != null) return prefab;

            prefab = Resources.Load<GameObject>("Models/" + folder + "/" + folder);
            Models[folder] = prefab;

            return prefab;
        }

        public static bool Has(string folder) { return Prefab(folder) != null; }

        /// <summary>
        /// The model's material: its baked base, normal and mask maps on the
        /// same lit shader everything else in the resort uses.
        ///
        /// The maps are unwrapped, so they are laid across the model once
        /// rather than tiled by the metre like the generated surfaces.
        /// </summary>
        public static Material Surface(string folder, float smoothnessScale = 1f,
                                       int maskResolution = 1024)
        {
            Material material;
            if (Surfaces.TryGetValue(folder, out material) && material != null) return material;

            string path = "Models/" + folder + "/" + folder;
            var albedo = Resources.Load<Texture2D>(path + "_Albedo");
            if (albedo == null)
            {
                Surfaces[folder] = null;
                return null;
            }

            var maps = new SurfaceMaps
            {
                albedo = albedo,
                normal = Resources.Load<Texture2D>(path + "_Normal"),
                mask = Mask(folder, path, maskResolution, smoothnessScale)
            };

            material = MaterialFactory.CreateSurface(folder + "Surface", maps, Color.white, 1f, 1f);

            foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap" })
                if (material.HasProperty(property)) material.SetTextureScale(property, Vector2.one);

            Surfaces[folder] = material;
            return material;
        }

        /// <summary>
        /// URP wants metallic in red and smoothness in alpha of one texture,
        /// and the bake writes metallic and roughness as two. They are
        /// combined here, at a smaller size, because that map carries no fine
        /// detail worth two thousand pixels.
        ///
        /// Smoothness is one minus roughness. Getting that the wrong way round
        /// makes paint shiny and steel matte, which is the single most common
        /// reason an imported asset looks like plastic.
        /// </summary>
        public static Texture2D Mask(string folder, string path, int resolution, float smoothnessScale)
        {
            var metallic = Resources.Load<Texture2D>(path + "_Metallic");
            var roughness = Resources.Load<Texture2D>(path + "_Roughness");

            if (metallic == null && roughness == null) return null;

            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 64, 2048);

            Color[] metal = Read(metallic, size);
            Color[] rough = Read(roughness, size);

            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                float m = metal != null ? metal[i].r : 0f;
                float s = rough != null ? 1f - rough[i].r : 0.4f;

                pixels[i] = new Color32((byte)(Mathf.Clamp01(m) * 255f), 0, 0,
                                        (byte)(Mathf.Clamp01(s * smoothnessScale) * 255f));
            }

            var mask = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = folder + "Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            mask.SetPixels32(pixels);
            mask.Apply(true, false);

            return mask;
        }

        static Color[] Read(Texture2D source, int size)
        {
            if (source == null) return null;

            var pixels = new Color[size * size];

            try
            {
                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, v);
                }
            }
            catch (UnityException)
            {
                // Not marked readable. The import settings ask for it, but a
                // project that has not reimported yet should not throw.
                return null;
            }

            return pixels;
        }

        /// <summary>
        /// Stand a model somewhere, at a size, wearing its own material.
        ///
        /// Scale is uniform and comes from one measured number — a tower's
        /// height, a chair's drop, a ski's length — because a model squashed
        /// on one axis to fill a box stops looking like the thing it is.
        /// Returns null if the model is not in the project, and the caller
        /// keeps its placeholder.
        /// </summary>
        public static GameObject Spawn(string folder, Transform parent, Vector3 localPosition,
                                       Quaternion localRotation, Vector3 scale)
        {
            GameObject prefab = Prefab(folder);
            if (prefab == null || scale.x <= 0.0001f || scale.y <= 0.0001f) return null;

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = Container;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = scale;

            Material material = Surface(folder);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null)
                {
                    var set = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < set.Length; i++) set[i] = material;
                    r.sharedMaterials = set;
                }

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            return instance;
        }

        public static GameObject Spawn(string folder, Transform parent, Vector3 localPosition,
                                       Quaternion localRotation, float scale = 1f)
        {
            return Spawn(folder, parent, localPosition, localRotation, Vector3.one * scale);
        }

        public static GameObject Spawn(string folder, Transform parent, float scale = 1f)
        {
            return Spawn(folder, parent, Vector3.zero, Quaternion.identity, Vector3.one * scale);
        }

        /// <summary>
        /// Switch the placeholder's renderers off, leaving everything else
        /// about it alone. Colliders, boarding points and the geometry other
        /// systems measure all stay exactly where they were.
        /// </summary>
        public static void Hide(Transform placeholder, GameObject model)
        {
            if (placeholder == null) return;

            foreach (Renderer r in placeholder.GetComponentsInChildren<Renderer>(true))
            {
                if (model != null && r.transform.IsChildOf(model.transform)) continue;
                r.enabled = false;
            }
        }
    }
}
