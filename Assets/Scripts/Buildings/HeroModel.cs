using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Buildings
{
    /// <summary>
    /// Puts a real model where a procedural placeholder was.
    ///
    /// The placeholder is not thrown away. It keeps its colliders, its
    /// entrance point and everything else the game reads off it — only its
    /// renderers are switched off, and the model is stood in exactly the same
    /// place at exactly the same size. So swapping a hero asset in cannot
    /// break where the player spawns, where guests walk, what the terrain
    /// protects, or what anything collides with.
    ///
    /// The model is fitted to the placeholder rather than the other way round.
    /// An asset arrives at whatever scale its author left it in, and measuring
    /// its own bounds and scaling to the footprint we asked for is the only
    /// approach that does not need someone to type a magic number per asset.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class HeroModel : MonoBehaviour
    {
        const string ContainerName = "HeroModel";

        [Header("Asset")]
        [Tooltip("Path under a Resources folder, without the extension.")]
        public string modelPath = "Models/Lodge/Lodge";
        [Tooltip("Textures beside it. Leave a name empty to skip that map.")]
        public string albedoPath = "Models/Lodge/Lodge_Albedo";
        public string normalPath = "Models/Lodge/Lodge_Normal";
        public string metallicPath = "Models/Lodge/Lodge_Metallic";
        public string roughnessPath = "Models/Lodge/Lodge_Roughness";

        [Header("Fit")]
        [Tooltip("Scale relative to the placeholder it replaces. One means the same size.")]
        public float fitScale = 1f;
        [Tooltip("Degrees to spin it so its front faces the same way as the placeholder.")]
        public float yaw = 0f;
        public Vector3 offset = Vector3.zero;
        [Tooltip("Sink it slightly so its base never floats above the snow.")]
        public float bed = 0.25f;
        [Tooltip("Used only if the placeholder has no renderers to measure.")]
        public float fallbackWidth = 30f;

        [Header("Placeholder")]
        [Tooltip("Turn the procedural geometry's renderers off once the model is up.")]
        public bool hidePlaceholder = true;

        [Header("Quality")]
        [Tooltip("Size the combined metallic and smoothness map is built at.")]
        public int maskResolution = 1024;
        public float smoothnessScale = 1f;

        /// <summary>True once a real model is standing here.</summary>
        public bool Loaded { get; private set; }

        /// <summary>Why it is not, if it is not. Shown in the console once.</summary>
        public string Problem { get; private set; }

        Transform _model;
        readonly List<Renderer> _hidden = new List<Renderer>();

        void Start() { Raise(); }

        float _checkedAt;

        void Update()
        {
            // The placeholder rebuilds itself whenever the terrain moves under
            // it, which brings its renderers back. Twice a second is far
            // cheaper than anyone can notice and cheaper than an event.
            if (!Loaded || !hidePlaceholder) return;
            if (Time.unscaledTime - _checkedAt < 0.5f) return;

            _checkedAt = Time.unscaledTime;
            HidePlaceholder();
        }

        [ContextMenu("Raise")]
        public void Raise()
        {
            Clear();

            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null)
            {
                Problem = "No model at Resources/" + modelPath;
                return;
            }

            GameObject instance = Instantiate(prefab, transform);
            instance.name = ContainerName;
            instance.hideFlags = HideFlags.DontSaveInEditor;
            _model = instance.transform;

            Fit(instance);
            Dress(instance);

            if (hidePlaceholder) HidePlaceholder();

            Loaded = true;
            Problem = null;
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            Loaded = false;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != ContainerName) continue;

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            _model = null;

            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
        }

        /// <summary>
        /// Scale and stand the model on top of the placeholder it replaces.
        ///
        /// The placeholder's own renderers are measured, so the model inherits
        /// its footprint, its position and its ground level without anybody
        /// typing a number. An asset arrives at whatever scale its author left
        /// it in; this is the only fitting approach that does not need a magic
        /// constant per asset.
        /// </summary>
        void Fit(GameObject instance)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one;

            Bounds model;
            if (!Measure(instance, out model)) return;

            Bounds target;
            bool measured = Placeholder(out target);

            float wanted = measured
                ? Mathf.Max(target.size.x, target.size.z)
                : fallbackWidth;

            float widest = Mathf.Max(model.size.x, model.size.z);
            if (widest < 0.0001f || wanted < 0.0001f) return;

            instance.transform.localScale = Vector3.one * (wanted / widest * fitScale);

            // Rotation and scale both move the box, so measure it again.
            if (!Measure(instance, out model)) return;

            Vector3 stand = measured
                ? new Vector3(target.center.x, target.min.y, target.center.z)
                : transform.position;

            var shift = new Vector3(stand.x - model.center.x,
                                    stand.y - model.min.y - bed,
                                    stand.z - model.center.z);

            instance.transform.position += shift + offset;
        }

        /// <summary>The bounds of the geometry this model is standing in for.</summary>
        bool Placeholder(out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (_model != null && r.transform.IsChildOf(_model)) continue;

                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return any;
        }

        static bool Measure(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds();

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        /// <summary>
        /// Build the material from the maps that shipped beside the model.
        ///
        /// URP wants metallic in red and smoothness in alpha of one texture,
        /// and the asset arrives with metallic and roughness as two. They are
        /// combined here, at a smaller size, because that map carries no fine
        /// detail worth two thousand pixels and combining at full size costs a
        /// visible pause on load.
        /// </summary>
        void Dress(GameObject instance)
        {
            var albedo = Load(albedoPath);
            if (albedo == null) return;

            var maps = new SurfaceMaps
            {
                albedo = albedo,
                normal = Load(normalPath),
                mask = CombineMask()
            };

            // One tile across the whole model: it is properly unwrapped, unlike
            // everything this project generates for itself.
            Material material = MaterialFactory.CreateSurface(name + "Model", maps, Color.white, 1f, 1f);
            if (material == null) return;

            // The model is properly unwrapped, so one tile across the whole of
            // it rather than the metre-based tiling everything generated uses.
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", Vector2.one);
            else if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", Vector2.one);

            if (material.HasProperty("_BumpMap")) material.SetTextureScale("_BumpMap", Vector2.one);
            if (material.HasProperty("_MetallicGlossMap")) material.SetTextureScale("_MetallicGlossMap", Vector2.one);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var set = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < set.Length; i++) set[i] = material;

                r.sharedMaterials = set;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        Texture2D Load(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
        }

        Texture2D _mask;

        Texture2D CombineMask()
        {
            if (_mask != null) return _mask;

            Texture2D metallic = Load(metallicPath);
            Texture2D roughness = Load(roughnessPath);

            if (metallic == null && roughness == null) return null;

            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(maskResolution), 64, 2048);

            Color[] metal = Sample(metallic, size);
            Color[] rough = Sample(roughness, size);

            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                float m = metal != null ? metal[i].r : 0f;

                // Smoothness is the opposite of roughness. Getting this the
                // wrong way round makes wood shiny and metal matte, which is
                // the most common reason an imported asset looks like plastic.
                float s = rough != null ? 1f - rough[i].r : 0.35f;

                pixels[i] = new Color32((byte)(Mathf.Clamp01(m) * 255f), 0, 0,
                                        (byte)(Mathf.Clamp01(s * smoothnessScale) * 255f));
            }

            _mask = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = name + "Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            _mask.SetPixels32(pixels);
            _mask.Apply(true, false);

            return _mask;
        }

        /// <summary>Read a texture down to a square of the given size.</summary>
        static Color[] Sample(Texture2D source, int size)
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

        void HidePlaceholder()
        {
            _hidden.Clear();

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (_model != null && r.transform.IsChildOf(_model)) continue;
                if (!r.enabled) continue;

                r.enabled = false;
                _hidden.Add(r);
            }
        }
    }
}
