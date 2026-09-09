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
    /// its own bounds and fitting them inside the placeholder's is the only
    /// approach that does not need someone to type a magic number per asset.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class HeroModel : MonoBehaviour
    {
        const string ContainerName = "HeroModel";

        [Header("Asset")]
        [Tooltip("Path under a Resources folder, without the extension. The\nmaps are the ones named after it in the same folder.")]
        public string modelPath = "Models/Lodge/Lodge";

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
        Bounds _fittedTo;
        Vector3 _size;
        readonly List<Renderer> _hidden = new List<Renderer>();

        /// <summary>How big the model ended up, in metres. For the console line.</summary>
        public Vector3 Size { get { return _size; } }

        void Start() { Raise(); }

        float _checkedAt;

        void Update()
        {
            // The placeholder rebuilds itself whenever the terrain moves under
            // it, which brings its renderers back and can move them. Twice a
            // second is far cheaper than anyone can notice, and cheaper than
            // an event.
            if (!Loaded) return;
            if (Time.unscaledTime - _checkedAt < 0.5f) return;

            _checkedAt = Time.unscaledTime;

            Bounds now;
            if (Placeholder(out now) && Moved(now, _fittedTo) && _model != null)
                Fit(_model.gameObject);

            if (hidePlaceholder) HidePlaceholder();
        }

        static bool Moved(Bounds a, Bounds b)
        {
            return (a.center - b.center).sqrMagnitude > 0.01f ||
                   (a.size - b.size).sqrMagnitude > 0.01f;
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
            bool dressed = Dress(instance);

            if (hidePlaceholder) HidePlaceholder();

            Loaded = true;
            if (dressed) Problem = null;

            // Said out loud, because the two ways this goes wrong — the wrong
            // size and no textures — both look like "a grey box" and neither
            // says which. One line names both.
            Debug.Log("[HeroModel] " + name + ": " + modelPath +
                      " at " + _size.x.ToString("0.0") + " x " + _size.y.ToString("0.0") +
                      " x " + _size.z.ToString("0.0") + " m, " +
                      (dressed ? "textured" : "NO TEXTURES (untextured grey)"), this);
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
        /// Scale and stand the model where the placeholder is.
        ///
        /// The model is fitted to the placeholder rather than the other way
        /// round, because an asset arrives at whatever scale its author left
        /// it in and measuring is the only approach that does not need a magic
        /// number typed in per asset.
        ///
        /// It is fitted INSIDE the placeholder's box rather than stretched to
        /// match one edge of it: the smallest of the three ratios wins, so no
        /// axis can ever come out bigger than the building it replaces. The
        /// first version of this matched the widest of X and Z, and a model
        /// taller than it is wide — which this one is — came out as a thirty
        /// metre slab standing over the trees.
        ///
        /// Height is measured from the ground, not from the bottom of the
        /// placeholder's box. Placeholders sink a foundation several metres
        /// into the hill so the terrain cannot poke up through the floor, and
        /// counting that buried part as building makes the model too tall and
        /// then buries it by the same amount.
        /// </summary>
        void Fit(GameObject instance)
        {
            Bounds target;
            bool measured = Placeholder(out target);

            _fittedTo = target;

            instance.transform.localPosition = Vector3.zero;
            instance.transform.rotation = Facing() * Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one;

            Bounds model;
            if (!Measure(instance, out model)) return;

            float ground = measured ? GroundUnder(target) : GroundUnder(transform.position);

            Vector3 room = measured
                ? new Vector3(target.size.x, Mathf.Max(1f, target.max.y - ground), target.size.z)
                : Vector3.one * fallbackWidth;

            float scale = Mathf.Min(Fitting(room.x, model.size.x),
                          Mathf.Min(Fitting(room.y, model.size.y),
                                    Fitting(room.z, model.size.z)));

            if (scale <= 0f || scale > 1e6f) return;

            instance.transform.localScale = Vector3.one * (scale * fitScale);

            // Scale moved the box, so measure it again before standing it up.
            if (!Measure(instance, out model)) return;

            Vector3 stand = measured
                ? new Vector3(target.center.x, ground, target.center.z)
                : new Vector3(transform.position.x, ground, transform.position.z);

            instance.transform.position += new Vector3(stand.x - model.center.x,
                                                       stand.y - model.min.y - bed,
                                                       stand.z - model.center.z) + offset;

            _size = model.size;
        }

        /// <summary>How much this axis may be scaled by. Zero means it does not constrain.</summary>
        static float Fitting(float room, float has)
        {
            return has > 0.0001f && room > 0.0001f ? room / has : float.MaxValue;
        }

        /// <summary>
        /// Which way the placeholder faces. The building's own geometry is
        /// turned towards the run, and a hero model standing square to the
        /// world instead would have its back door on the piste.
        /// </summary>
        Quaternion Facing()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == ContainerName) continue;
                if (child.GetComponentInChildren<Renderer>(true) == null) continue;

                return child.rotation;
            }

            return transform.rotation;
        }

        /// <summary>Snow level under a footprint, so the model stands on the hill.</summary>
        float GroundUnder(Bounds target)
        {
            float ground = GroundUnder(target.center);

            // Never above the roof and never below the floor, whatever the
            // terrain says: a bad sample must not launch the building.
            return Mathf.Clamp(ground, target.min.y, target.max.y);
        }

        float GroundUnder(Vector3 at)
        {
            var mountain = SnowBound.Mountain.MountainGenerator.Instance;
            return mountain != null && mountain.Ready ? mountain.SampleHeight(at.x, at.z) : at.y;
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
        /// Put the model's own baked maps on it.
        ///
        /// The material comes from HeroAssets, which caches one per model:
        /// the maps behind it are two thousand pixels square and combining
        /// them is not something to do twice.
        /// </summary>
        bool Dress(GameObject instance)
        {
            Material material = HeroAssets.Surface(Folder, smoothnessScale, maskResolution);

            if (material == null)
            {
                Problem = "No textures beside Resources/" + modelPath;
                return false;
            }

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var set = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < set.Length; i++) set[i] = material;

                r.sharedMaterials = set;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            return true;
        }

        /// <summary>The folder the model and its maps share, which is what
        /// they are all named after.</summary>
        string Folder
        {
            get
            {
                string path = modelPath.Replace("\\", "/");
                int cut = path.LastIndexOf('/');
                return cut < 0 ? path : path.Substring(cut + 1);
            }
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
