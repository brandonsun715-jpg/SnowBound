using UnityEngine;
using UnityEngine.Rendering;

namespace SnowBound.Core
{
    /// <summary>
    /// Creates plain coloured materials at runtime so the prototype needs
    /// zero imported art. Later we just drag real Material assets into the
    /// public Material slots on the generator components and these are unused.
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
