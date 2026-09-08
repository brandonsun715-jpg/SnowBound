using UnityEngine;
using UnityEngine.Rendering;

namespace SnowBound.Core
{
    /// <summary>
    /// Materials, built at runtime, from textures that are also built at
    /// runtime. The project imports no art, so everything here is arithmetic —
    /// but it is proper physically based arithmetic: a base map, a normal map
    /// and a metallic-smoothness map, which is what separates a surface that
    /// reads as a material from a surface that reads as a coloured polygon.
    ///
    /// The flat-colour calls are still here and still work. They are for the
    /// things too small or too far away to be worth a texture.
    /// </summary>
    public static class MaterialFactory
    {
        static Shader _lit;

        static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                if (_lit == null) _lit = Shader.Find("Diffuse");
                return _lit;
            }
        }

        public static Material Create(string name, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            var m = new Material(Lit);
            m.name = name;

            // URP Lit property names.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            // Built-in Standard property names (fallback).
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        /// <summary>A material that glows, for warm lodge windows and lamps.</summary>
        public static Material CreateEmissive(string name, Color color, Color emission)
        {
            var m = Create(name, color, 0.1f);

            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return m;
        }

        /// <summary>
        /// A textured, physically based surface.
        ///
        /// URP wants smoothness in the alpha of the metallic map and multiplies
        /// it by the material's own smoothness, so that is set to one and the
        /// map is left to say what it means. The normal map is unpacked from
        /// red and green, which is how ProceduralTextures writes it.
        /// </summary>
        public static Material CreateSurface(string name, SurfaceMaps maps, Color tint,
                                             float metresPerTile, float normalStrength = 1f)
        {
            var m = new Material(Lit);
            m.name = name;
            m.hideFlags = HideFlags.DontSave;

            if (!maps.Valid) return Create(name, tint);

            float tiling = metresPerTile > 0.01f ? 1f / metresPerTile : 1f;

            Set(m, "_BaseMap", "_MainTex", maps.albedo);
            m.SetTextureScale(m.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex",
                              new Vector2(tiling, tiling));

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);

            if (maps.normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", maps.normal);
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));

                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", normalStrength);
                m.EnableKeyword("_NORMALMAP");
            }

            if (maps.mask != null && m.HasProperty("_MetallicGlossMap"))
            {
                m.SetTexture("_MetallicGlossMap", maps.mask);
                m.SetTextureScale("_MetallicGlossMap", new Vector2(tiling, tiling));
                m.EnableKeyword("_METALLICSPECGLOSSMAP");

                // The map carries the real values; these are the multipliers.
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 1f);
                if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 0f);
            }

            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);

            return m;
        }

        /// <summary>
        /// Add a second, much finer set of maps on top. Terrain is tiled at
        /// tens of metres, which looks right from the chairlift and looks like
        /// a blurred photograph from a metre away; a detail map at centimetre
        /// scale is what holds the surface together up close.
        /// </summary>
        public static Material WithDetail(Material m, SurfaceMaps detail, float metresPerTile,
                                          float albedoStrength = 0.5f, float normalStrength = 0.8f)
        {
            if (m == null || !detail.Valid) return m;
            if (!m.HasProperty("_DetailAlbedoMap")) return m;

            float tiling = metresPerTile > 0.01f ? 1f / metresPerTile : 1f;

            m.SetTexture("_DetailAlbedoMap", detail.albedo);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling));

            if (m.HasProperty("_DetailAlbedoMapScale")) m.SetFloat("_DetailAlbedoMapScale", albedoStrength);

            if (detail.normal != null && m.HasProperty("_DetailNormalMap"))
            {
                m.SetTexture("_DetailNormalMap", detail.normal);
                m.SetTextureScale("_DetailNormalMap", new Vector2(tiling, tiling));

                if (m.HasProperty("_DetailNormalMapScale"))
                    m.SetFloat("_DetailNormalMapScale", normalStrength);
            }

            m.EnableKeyword("_DETAIL_MULX2");
            return m;
        }

        static void Set(Material m, string urp, string standard, Texture texture)
        {
            if (texture == null) return;

            if (m.HasProperty(urp)) m.SetTexture(urp, texture);
            else if (m.HasProperty(standard)) m.SetTexture(standard, texture);
        }

        /// <summary>A soft additive-free transparent material for particles.</summary>
        public static Material CreateParticle(string name, Color tint, Texture2D texture)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");

            var m = new Material(sh);
            m.name = name;

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);

            // Force straight alpha blending. Harmless on shaders that are
            // already transparent, such as the Sprites/Default fallback.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;

            m.hideFlags = HideFlags.DontSave;
            return m;
        }
    }
}
