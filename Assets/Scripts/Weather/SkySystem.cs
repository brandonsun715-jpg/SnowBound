using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Weather
{
    /// <summary>
    /// The sky, and the weather in it.
    ///
    /// Two layers. Underneath is Unity's atmospheric scattering sky, driven
    /// from the sun's own direction, which is what produces a real gradient
    /// and a real sunset rather than a painted one: thicken the atmosphere as
    /// the sun drops and the reddening happens because that is what scattering
    /// does.
    ///
    /// Over the top is a cloud dome — a hemisphere wearing a generated cloud
    /// texture, drifting on the wind, thickening into overcast as the weather
    /// turns. It moves, so the sky is never the same twice, and it follows the
    /// camera so it can never be reached.
    /// </summary>
    [ExecuteAlways]
    public class SkySystem : MonoBehaviour
    {
        public WeatherSystem weather;
        public LightingDirector lighting;

        [Header("Atmosphere")]
        [Tooltip("Thickness with the sun high. Higher means more blue overhead.")]
        public float clearThickness = 0.72f;
        [Tooltip("Thickness with the sun on the horizon. This is the sunset.")]
        public float lowSunThickness = 2.1f;
        public float stormThickness = 0.5f;
        public float exposure = 1.15f;

        [Header("Clouds")]
        public float domeRadius = 3600f;
        [Tooltip("How far the cloud sheet is above the camera, relative to the radius.")]
        public float domeFlatten = 0.34f;
        public int cloudTexture = 512;
        public float cloudTiling = 3.2f;
        [Tooltip("Metres a minute the sheet drifts at, per metre a second of wind.")]
        public float driftPerWind = 26f;
        [Range(0f, 1f)] public float clearCover = 0.10f;
        [Range(0f, 1f)] public float stormCover = 0.96f;

        Material _sky;
        Transform _dome;
        Material _cloud;
        Vector2 _drift;

        void OnEnable()
        {
            Build();
        }

        void OnDisable()
        {
            if (_dome == null) return;

            if (Application.isPlaying) Destroy(_dome.gameObject);
            else DestroyImmediate(_dome.gameObject);

            _dome = null;
        }

        void Build()
        {
            if (weather == null) weather = WeatherSystem.Instance;
            if (lighting == null) lighting = LightingDirector.Instance;

            if (_sky == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader != null)
                {
                    _sky = new Material(shader) { name = "SnowboundSky", hideFlags = HideFlags.DontSave };
                    _sky.SetFloat("_SunDisk", 2f);            // high quality disc
                    _sky.SetFloat("_SunSize", 0.035f);
                    _sky.SetFloat("_SunSizeConvergence", 6f);
                    RenderSettings.skybox = _sky;
                }
            }

            if (_dome != null) return;

            var go = new GameObject("CloudDome");
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSaveInEditor;
            _dome = go.transform;

            Texture2D clouds = CloudTexture();

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

            _cloud = unlit != null
                ? new Material(unlit) { name = "Clouds", hideFlags = HideFlags.DontSave }
                : MaterialFactory.CreateParticle("Clouds", Color.white, clouds);

            if (_cloud.HasProperty("_BaseMap")) _cloud.SetTexture("_BaseMap", clouds);
            if (_cloud.HasProperty("_MainTex")) _cloud.SetTexture("_MainTex", clouds);

            _cloud.SetTextureScale(_cloud.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex",
                                   new Vector2(cloudTiling, cloudTiling));

            // Transparent, unlit, never writes depth, drawn before everything.
            if (_cloud.HasProperty("_Surface")) _cloud.SetFloat("_Surface", 1f);
            if (_cloud.HasProperty("_Blend")) _cloud.SetFloat("_Blend", 0f);
            if (_cloud.HasProperty("_SrcBlend")) _cloud.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_cloud.HasProperty("_DstBlend")) _cloud.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_cloud.HasProperty("_ZWrite")) _cloud.SetFloat("_ZWrite", 0f);

            _cloud.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _cloud.renderQueue = 2000;

            go.AddComponent<MeshFilter>().sharedMesh = Dome();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _cloud;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        void LateUpdate()
        {
            if (_sky == null || _dome == null) Build();
            if (lighting == null) lighting = LightingDirector.Instance;

            float storm = weather != null ? Mathf.Clamp01(weather.storminess) : 0.15f;
            float height = lighting != null ? lighting.SunHeight : 0.6f;
            float golden = lighting != null ? lighting.GoldenHour : 0f;

            Atmosphere(storm, height, golden);
            Clouds(storm);
        }

        void Atmosphere(float storm, float height, float golden)
        {
            if (_sky == null) return;

            // The whole sunset comes from this line: more air in the way as the
            // sun drops, so more of the blue end is scattered out of it.
            float thickness = Mathf.Lerp(clearThickness, lowSunThickness, golden);
            thickness = Mathf.Lerp(thickness, stormThickness, storm * 0.8f);

            _sky.SetFloat("_AtmosphereThickness", thickness);
            _sky.SetFloat("_Exposure", exposure * Mathf.Lerp(1f, 0.55f, storm) * Mathf.Lerp(0.5f, 1f, height + 0.2f));

            Color tint = lighting != null
                ? Color.Lerp(lighting.Zenith, Color.white, 0.35f)
                : new Color(0.5f, 0.6f, 0.8f);

            _sky.SetColor("_SkyTint", Color.Lerp(tint, new Color(0.62f, 0.64f, 0.68f), storm));

            // Below the horizon is snow, not grass.
            _sky.SetColor("_GroundColor", Color.Lerp(new Color(0.68f, 0.72f, 0.80f),
                                                     new Color(0.58f, 0.60f, 0.64f), storm));

            // The disc swells and softens as it sinks.
            _sky.SetFloat("_SunSize", Mathf.Lerp(0.028f, 0.055f, golden) * Mathf.Lerp(1f, 0.2f, storm));
        }

        void Clouds(float storm)
        {
            if (_dome == null || _cloud == null) return;

            Camera view = Camera.main;
            if (view != null) _dome.position = new Vector3(view.transform.position.x, 0f,
                                                           view.transform.position.z);

            Vector3 wind = weather != null ? weather.Wind : Vector3.right;
            float speed = wind.magnitude;

            if (Application.isPlaying)
            {
                Vector2 direction = speed > 0.01f
                    ? new Vector2(wind.x, wind.z).normalized
                    : Vector2.right;

                _drift += direction * (speed * driftPerWind * 0.00002f) * Time.deltaTime * 60f;
            }

            string map = _cloud.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            _cloud.SetTextureOffset(map, _drift);

            float cover = Mathf.Lerp(clearCover, stormCover, storm);

            Color lit = lighting != null
                ? Color.Lerp(lighting.Horizon * 1.35f, lighting.SunColour, lighting.GoldenHour * 0.5f)
                : Color.white;

            // Overcast is flat and grey; broken cloud catches the sun.
            Color tint = Color.Lerp(lit, new Color(0.62f, 0.64f, 0.68f), storm * 0.7f);
            tint.a = cover;

            if (_cloud.HasProperty("_BaseColor")) _cloud.SetColor("_BaseColor", tint);
            if (_cloud.HasProperty("_Color")) _cloud.SetColor("_Color", tint);
        }

        // ---------------- geometry and texture -------------------------------

        Texture2D _clouds;

        /// <summary>
        /// Cloud cover as one tiling texture: billows in the alpha, brighter
        /// where the sheet is thin. Layered noise, because a cloud has
        /// structure at every scale at once.
        /// </summary>
        Texture2D CloudTexture()
        {
            if (_clouds != null) return _clouds;

            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(cloudTexture), 64, 1024);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;

                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;

                    float billow = Fbm(u, v, 4, 5, 7);
                    float wisps = Fbm(u * 2.2f, v * 2.2f, 9, 3, 31);

                    // Push it towards either sky or cloud, so there are edges.
                    float a = Mathf.Clamp01((billow * 0.75f + wisps * 0.25f - 0.34f) * 2.6f);
                    a = a * a * (3f - 2f * a);

                    float bright = 0.72f + billow * 0.28f;

                    pixels[y * size + x] = new Color(bright, bright, bright, a);
                }
            }

            _clouds = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "CloudSheet",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };

            _clouds.SetPixels32(pixels);
            _clouds.Apply(true, false);

            return _clouds;
        }

        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int p = period;

            for (int i = 0; i < octaves; i++)
            {
                sum += Wrapped(u, v, p, seed + i * 53) * amp;
                norm += amp;

                amp *= 0.55f;
                p *= 2;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        static float Wrapped(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;

            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(x0, y0, period, seed);
            float b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed);
            float d = Hash(x0 + 1, y0 + 1, period, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            int h = x * 374761393 + y * 668265263 + seed * 1442695040;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);

            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        Mesh _domeMesh;

        /// <summary>
        /// A flattened hemisphere seen from inside. Flattened because a real
        /// cloud sheet is a layer, not a ball, and a spherical one puts cloud
        /// directly overhead at the wrong scale.
        /// </summary>
        Mesh Dome()
        {
            if (_domeMesh != null) return _domeMesh;

            const int rings = 12;
            const int segments = 48;

            var verts = new Vector3[(rings + 1) * (segments + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[rings * segments * 6];

            for (int r = 0; r <= rings; r++)
            {
                // Bunch the rings towards the horizon, where the sheet is seen
                // edge on and needs the resolution.
                float t = r / (float)rings;
                float polar = Mathf.Pow(t, 0.65f) * Mathf.PI * 0.5f;

                float radius = Mathf.Sin(polar);
                float up = Mathf.Cos(polar) * domeFlatten;

                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    int i = r * (segments + 1) + s;

                    verts[i] = new Vector3(Mathf.Cos(a) * radius, up, Mathf.Sin(a) * radius) * domeRadius;

                    // Flat projection, so the sheet reads as a plane overhead.
                    uvs[i] = new Vector2(verts[i].x, verts[i].z) / (domeRadius * 2f) + new Vector2(0.5f, 0.5f);
                }
            }

            int k = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + segments + 1;

                    // Wound inwards: this is only ever seen from the inside.
                    tris[k++] = a; tris[k++] = a + 1; tris[k++] = b;
                    tris[k++] = a + 1; tris[k++] = b + 1; tris[k++] = b;
                }
            }

            _domeMesh = new Mesh { name = "CloudDome", hideFlags = HideFlags.DontSave };
            _domeMesh.vertices = verts;
            _domeMesh.uv = uvs;
            _domeMesh.triangles = tris;
            _domeMesh.RecalculateNormals();
            _domeMesh.bounds = new Bounds(Vector3.zero, Vector3.one * domeRadius * 4f);

            return _domeMesh;
        }
    }
}
