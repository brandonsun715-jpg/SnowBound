using UnityEngine;

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
    }
}
