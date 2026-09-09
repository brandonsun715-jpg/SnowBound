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
        public const float TreeHeight = 10.00f;    // trunk foot to crown
        public const float RockSize = 2.00f;       // the widest boulder, across
        public const float KickerHeight = 2.00f;   // snow to lip
        public const float BoxLength = 8.00f;      // a jib box or a rail

        public const string Chair = "ChairliftChair";
        public const string Tower = "ChairliftTower";
        public const string Station = "ChairliftStation";
        public const string Ski = "Ski";
        public const string Pole = "SkiPole";
        public const string Board = "Snowboard";
        public const string RiderSki = "RiderSki";
        public const string RiderBoard = "RiderBoard";
        public const string Trees = "Trees";
        public const string Rocks = "Rocks";
        public const string Flora = "Flora";
        public const string Park = "ParkFeatures";

        /// <summary>
        /// The longest side each model was drawn at, in metres.
        ///
        /// It is here to catch one specific disaster. An FBX carries its own
        /// idea of what a unit means, and a file that says centimetres while
        /// holding metres imports a hundred times too small — which does not
        /// look like an error, it looks like the model failing to appear at
        /// all. Measuring what actually arrived and comparing it with what
        /// was drawn turns that into a line in the console and a model the
        /// right size anyway.
        /// </summary>
        static float Drawn(string folder)
        {
            switch (folder)
            {
                case Chair: return 2.17f;
                case Tower: return 9.48f;
                case Station: return 13.60f;
                case Ski: return SkiLength;
                case Pole: return 1.25f;
                case Board: return BoardLength;
                case RiderSki: return 1.77f;
                case RiderBoard: return 1.77f;
                default: return 0f;
            }
        }

        /// <summary>What a spawned model is called, so it can be told apart
        /// from the placeholder it is standing in for.</summary>
        public const string Container = "HeroModel";

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> Surfaces = new Dictionary<string, Material>();
        static readonly HashSet<string> Reported = new HashSet<string>();
        static readonly Dictionary<string, Piece> Pieces = new Dictionary<string, Piece>();

        /// <summary>
        /// One model part's geometry, ready to be welded into a batch.
        ///
        /// Eighteen hundred trees cannot be eighteen hundred objects, so the
        /// forest is not spawned — it is copied, placed and welded. That
        /// needs the mesh itself rather than a prefab, and it needs the
        /// model's own texture coordinates to come with it.
        /// </summary>
        public class Piece
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector2> uvs = new List<Vector2>();

            public bool Valid { get { return vertices.Count > 0 && triangles.Count > 0; } }
        }

        /// <summary>
        /// The geometry of one named part of a model, or null if it is not
        /// there. Cached, because reading a mesh copies all of it.
        /// </summary>
        public static Piece Geometry(string folder, string part)
        {
            string key = folder + "/" + part;

            Piece piece;
            if (Pieces.TryGetValue(key, out piece)) return piece;

            piece = Read(folder, part);
            Pieces[key] = piece;

            return piece;
        }

        static Piece Read(string folder, string part)
        {
            GameObject prefab = Prefab(folder);
            if (prefab == null) return null;

            Transform found = Part(prefab.transform, part);
            if (found == null) return null;

            var filter = found.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;

            Mesh mesh = filter.sharedMesh;

            if (!mesh.isReadable)
            {
                if (Reported.Add(folder + "/" + part))
                    Debug.LogWarning("[HeroAssets] " + folder + "/" + part + " is not readable, so it " +
                                     "cannot be batched. Tick Read/Write on its import settings.");
                return null;
            }

            var piece = new Piece();
            piece.vertices.AddRange(mesh.vertices);
            piece.triangles.AddRange(mesh.triangles);
            piece.uvs.AddRange(mesh.uv);

            return piece.Valid ? piece : null;
        }

        /// <summary>Find one named object inside a model.</summary>
        public static Transform Part(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            return null;
        }

        /// <summary>
        /// Stand up one named part of a model on its own — a rail out of the
        /// park's file, a leg out of the same one — and throw the rest away.
        ///
        /// One file and one texture for a set of things that belong together
        /// costs less than one of each, and the parts are laid out side by
        /// side in it, so the part is brought back to the origin on its way
        /// out.
        /// </summary>
        public static GameObject SpawnPart(string folder, string part, Transform parent,
                                           Vector3 localPosition, Quaternion localRotation,
                                           Vector3 scale)
        {
            GameObject whole = Spawn(folder, parent, localPosition, localRotation, scale);
            if (whole == null) return null;

            Transform kept = Part(whole.transform, part);

            if (kept == null)
            {
                Kill(whole);
                return null;
            }

            // The whole model carries the scale, including whatever
            // correction it needed; the part carries its own on top.
            Vector3 own = kept.localScale;

            kept.SetParent(parent, false);
            kept.localPosition = localPosition;
            kept.localRotation = localRotation;
            kept.localScale = Vector3.Scale(own, whole.transform.localScale);
            kept.gameObject.name = Container;

            Kill(whole);
            return kept.gameObject;
        }

        static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

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
            instance.transform.localScale = Vector3.one;

            float correction = Correction(folder, instance);
            instance.transform.localScale = scale * correction;

            // Never leave a renderer without a material. A null material in
            // URP draws nothing at all, and an asset that silently fails to
            // appear is the hardest kind of problem to look at.
            Material material = Surface(folder) ??
                                MaterialFactory.Create(folder + "Untextured",
                                                       new Color(0.55f, 0.55f, 0.57f), 0.3f);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var set = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < set.Length; i++) set[i] = material;
                r.sharedMaterials = set;

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
        /// How far out the model arrived from the size it was drawn at, and
        /// what to multiply it by to put that right.
        ///
        /// Said out loud the first time, because a model that is a hundred
        /// times too small and a model that failed to load look exactly the
        /// same from where the player is standing.
        /// </summary>
        static float Correction(string folder, GameObject instance)
        {
            float drawn = Drawn(folder);
            float arrived = Longest(instance);

            if (drawn <= 0f || arrived <= 0.0001f) return 1f;

            float ratio = drawn / arrived;
            if (ratio < 1.25f && ratio > 0.8f) return 1f;

            if (Reported.Add(folder))
                Debug.LogWarning("[HeroAssets] " + folder + " imported at " +
                                 arrived.ToString("0.###") + " m but was drawn at " +
                                 drawn.ToString("0.###") + " m, so it is being scaled by " +
                                 ratio.ToString("0.###") + ". Its FBX unit scale is wrong.");

            return ratio;
        }

        /// <summary>The model's longest side, in its own space.</summary>
        static float Longest(GameObject instance)
        {
            Matrix4x4 into = instance.transform.worldToLocalMatrix;
            var bounds = new Bounds();
            bool any = false;

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 place = into * filter.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = place.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z));

                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }

            return any ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 0f;
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
