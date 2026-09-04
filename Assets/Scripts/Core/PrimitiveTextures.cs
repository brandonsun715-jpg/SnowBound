using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Textures drawn in code, so particles need no imported art either.
    /// </summary>
    public static class PrimitiveTextures
    {
        static Texture2D _softCircle;

        /// <summary>A soft round white blob. The snow particle sprite.</summary>
        public static Texture2D SoftCircle(int size = 64)
        {
            if (_softCircle != null) return _softCircle;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "SoftCircle";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    alpha *= alpha;   // soft edge rather than a hard disc
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _softCircle = tex;
            return tex;
        }
    }
}
