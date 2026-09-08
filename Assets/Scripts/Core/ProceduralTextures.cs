using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>The three maps that make a surface look like a material.</summary>
    public struct SurfaceMaps
    {
        public Texture2D albedo;
        public Texture2D normal;
        /// <summary>Metallic in red, smoothness in alpha. What URP's Lit shader wants.</summary>
        public Texture2D mask;

        public bool Valid { get { return albedo != null; } }
    }

    /// <summary>
    /// Every surface in the game, written as arithmetic.
    ///
    /// The project imports no textures, so until now every material was a flat
    /// colour — which is the single reason the whole thing read as a prototype
    /// however good the geometry got. Real snow is not one shade of white: it
    /// has grain, it has wind on it, a groomer leaves corduroy in it, ice is
    /// smoother than powder and reflects differently. None of that is
    /// expensive to compute; it just has to actually be there.
    ///
    /// Each surface is built in one pass: a height field, a colour, a metallic
    /// value and a smoothness value per texel, with the normal map derived from
    /// the height afterwards by central differences. One pass over the pixels
    /// rather than three, and the normal is guaranteed to agree with the albedo
    /// because they came out of the same function.
    ///
    /// Everything is cached by name. A surface is generated once for the life
    /// of the game and shared by every object that wears it.
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>What one texel of a surface is.</summary>
        public struct Texel
        {
            public float height;      // drives the normal map
            public Color colour;
            public float metallic;
            public float smoothness;
        }

        public delegate Texel Shade(float u, float v);

        static readonly Dictionary<string, SurfaceMaps> _cache = new Dictionary<string, SurfaceMaps>();

        // ---------------- the builder -------------------------------------

        /// <summary>
        /// Run a shading function over a square and bake the result into three
        /// textures. Tiling is seamless because every noise call wraps.
        /// </summary>
        public static SurfaceMaps Build(string name, int size, float normalStrength, Shade shade)
        {
            SurfaceMaps cached;
            if (_cache.TryGetValue(name, out cached) && cached.albedo != null) return cached;

            size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(size), 32, 1024);

            var height = new float[size * size];
            var albedo = new Color32[size * size];
            var mask = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;

                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;

                    Texel t = shade(x / (float)size, v);

                    height[i] = t.height;
                    albedo[i] = t.colour;

                    mask[i] = new Color32((byte)(Mathf.Clamp01(t.metallic) * 255f), 0, 0,
                                          (byte)(Mathf.Clamp01(t.smoothness) * 255f));
                }
            }

            var maps = new SurfaceMaps
            {
                albedo = Bake(name + "_A", size, albedo, true),
                normal = Normals(name + "_N", size, height, normalStrength),
                mask = Bake(name + "_M", size, mask, false)
            };

            _cache[name] = maps;
            return maps;
        }

        static Texture2D Bake(string name, int size, Color32[] pixels, bool srgb)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, !srgb);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.hideFlags = HideFlags.DontSave;

            tex.SetPixels32(pixels);
            tex.Apply(true, false);

            return tex;
        }

        /// <summary>
        /// A normal map from the height field.
        ///
        /// URP unpacks normals as x from red times alpha and y from green, so
        /// alpha is left at one and the vector goes straight into red and
        /// green. Sampling with wrap keeps the map seamless at the edges.
        /// </summary>
        static Texture2D Normals(string name, int size, float[] height, float strength)
        {
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                int up = ((y + 1) % size) * size;
                int down = ((y - 1 + size) % size) * size;
                int here = y * size;

                for (int x = 0; x < size; x++)
                {
                    int right = (x + 1) % size;
                    int left = (x - 1 + size) % size;

                    float dx = (height[here + left] - height[here + right]) * strength;
                    float dy = (height[down + x] - height[up + x]) * strength;

                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    pixels[here + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f),
                                                   (byte)((n.y * 0.5f + 0.5f) * 255f),
                                                   (byte)((n.z * 0.5f + 0.5f) * 255f),
                                                   255);
                }
            }

            return Bake(name, size, pixels, false);
        }

        // ---------------- noise -------------------------------------------

        /// <summary>
        /// Value noise on a wrapping lattice. Perlin from Unity does not tile,
        /// and a texture with a visible seam is worse than no texture.
        /// </summary>
        static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;

            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);

            float fx = x - x0;
            float fy = y - y0;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Lattice(x0, y0, period, seed);
            float b = Lattice(x0 + 1, y0, period, seed);
            float c = Lattice(x0, y0 + 1, period, seed);
            float d = Lattice(x0 + 1, y0 + 1, period, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            int h = x * 374761393 + y * 668265263 + seed * 1442695040;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);

            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// <summary>Several octaves of the above. Still tiles.</summary>
        static float Fbm(float u, float v, int period, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int p = period;

            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(u, v, p, seed + i * 71) * amp;
                norm += amp;

                amp *= gain;
                p *= 2;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Folded noise. Gives creases rather than blobs.</summary>
        static float Ridged(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int p = period;

            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Noise(u, v, p, seed + i * 131) * 2f - 1f);
                sum += n * n * amp;
                norm += amp;

                amp *= 0.5f;
                p *= 2;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        static Color Tint(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

        // ---------------- snow ---------------------------------------------

        static readonly Color SnowLit = new Color(0.965f, 0.975f, 1.000f);
        static readonly Color SnowShade = new Color(0.780f, 0.835f, 0.930f);
        static readonly Color SnowBlue = new Color(0.700f, 0.790f, 0.910f);

        /// <summary>
        /// Fresh powder. Fine grain, a scatter of bright facets where a crystal
        /// happens to catch the light, and almost no gloss: powder is the least
        /// reflective snow there is.
        /// </summary>
        public static SurfaceMaps Powder()
        {
            return Build("SnowPowder", 512, 2.2f, (u, v) =>
            {
                float grain = Fbm(u, v, 96, 3, 11);
                float drift = Fbm(u, v, 8, 3, 23);
                float sparkle = Noise(u, v, 256, 41);

                float h = drift * 0.7f + grain * 0.3f;

                Color c = Tint(SnowShade, SnowLit, drift * 0.55f + grain * 0.45f);
                if (sparkle > 0.93f) c += new Color(0.05f, 0.05f, 0.05f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.06f + sparkle * 0.14f
                };
            });
        }

        /// <summary>
        /// Packed piste. Firmer, with the faint striations skis leave in it and
        /// enough gloss to catch the sun.
        /// </summary>
        public static SurfaceMaps Packed()
        {
            return Build("SnowPacked", 512, 1.5f, (u, v) =>
            {
                float grain = Fbm(u, v, 128, 3, 7);
                float swell = Fbm(u, v, 10, 3, 19);

                // Faint tracks running down the fall line.
                float tracks = Mathf.Sin((u + Fbm(u, v, 6, 2, 33) * 0.25f) * Mathf.PI * 60f);
                tracks = tracks * tracks * 0.12f;

                float h = swell * 0.55f + grain * 0.3f + tracks;

                Color c = Tint(SnowShade, SnowLit, swell * 0.5f + grain * 0.4f + 0.1f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.22f + grain * 0.12f
                };
            });
        }

        /// <summary>
        /// Groomed corduroy. This is the one detail that says "ski resort" at a
        /// glance: even ridges left by a winch cat, running down the fall line,
        /// with the crests catching light and the troughs holding shadow.
        /// </summary>
        public static SurfaceMaps Groomed()
        {
            return Build("SnowGroomed", 512, 2.6f, (u, v) =>
            {
                // The cat wanders a little, so the lines are not ruler straight.
                float wander = Fbm(u, v, 4, 2, 61) * 0.06f;
                float rib = Mathf.Cos((u + wander) * Mathf.PI * 2f * 28f);

                float corduroy = (rib * 0.5f + 0.5f);
                corduroy = Mathf.Pow(corduroy, 1.4f);

                float grain = Fbm(u, v, 160, 2, 5);
                float swell = Fbm(u, v, 12, 2, 17);

                float h = corduroy * 0.55f + swell * 0.35f + grain * 0.1f;

                Color c = Tint(SnowShade, SnowLit, corduroy * 0.45f + swell * 0.35f + 0.2f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    // The crests are polished by the tiller, the troughs are not.
                    smoothness = 0.20f + corduroy * 0.22f
                };
            });
        }

        /// <summary>Wind slab: sastrugi, dunes and scoured patches.</summary>
        public static SurfaceMaps Windblown()
        {
            return Build("SnowWind", 512, 2.4f, (u, v) =>
            {
                // Dunes running across the prevailing wind.
                float along = u * 0.82f + v * 0.57f;
                float across = -u * 0.57f + v * 0.82f;

                float dune = Mathf.Sin(across * Mathf.PI * 2f * 9f + Fbm(u, v, 8, 2, 91) * 5f);
                dune = Mathf.Pow(dune * 0.5f + 0.5f, 2.2f);

                float scour = Fbm(along, across, 14, 3, 77);
                float grain = Fbm(u, v, 120, 2, 13);

                float h = dune * 0.6f + scour * 0.3f + grain * 0.1f;

                Color c = Tint(SnowBlue, SnowLit, dune * 0.5f + scour * 0.4f + 0.15f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.18f + dune * 0.3f
                };
            });
        }

        /// <summary>
        /// Boilerplate ice. Smooth, blue, and it reflects — which is exactly
        /// what makes it read as unpleasant to ski before you have touched it.
        /// </summary>
        public static SurfaceMaps Icy()
        {
            return Build("SnowIce", 512, 1.2f, (u, v) =>
            {
                float sheet = Fbm(u, v, 6, 3, 3);
                float cracks = Ridged(u, v, 18, 3, 29);
                float chatter = Fbm(u, v, 90, 2, 47);

                float h = sheet * 0.5f + Mathf.Pow(cracks, 3f) * 0.4f + chatter * 0.1f;

                Color c = Tint(SnowBlue, SnowLit, sheet * 0.5f + 0.25f);
                c = Tint(c, new Color(0.62f, 0.74f, 0.88f), Mathf.Pow(cracks, 4f) * 0.8f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.62f + sheet * 0.24f
                };
            });
        }

        /// <summary>Old snow gone patchy: part packed, part ice, part slush.</summary>
        public static SurfaceMaps Mixed()
        {
            return Build("SnowMixed", 512, 1.8f, (u, v) =>
            {
                float patch = Fbm(u, v, 7, 3, 55);
                float grain = Fbm(u, v, 110, 3, 9);
                float dirt = Fbm(u, v, 20, 2, 67);

                float h = patch * 0.6f + grain * 0.4f;

                Color c = Tint(SnowShade, SnowLit, patch * 0.5f + grain * 0.35f);
                c = Tint(c, new Color(0.74f, 0.75f, 0.74f), Mathf.Max(0f, dirt - 0.62f) * 1.4f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.16f + patch * 0.34f
                };
            });
        }

        // ---------------- everything else ------------------------------------

        /// <summary>Alpine rock: bedded, cracked, and lichened in the hollows.</summary>
        public static SurfaceMaps Rock()
        {
            return Build("Rock", 512, 3.4f, (u, v) =>
            {
                float beds = Fbm(u, v * 3.2f, 6, 3, 101);
                float cracks = Ridged(u, v, 12, 4, 113);
                float grit = Fbm(u, v, 140, 3, 127);

                float h = beds * 0.45f + Mathf.Pow(cracks, 2.4f) * 0.45f + grit * 0.1f;

                Color c = Tint(new Color(0.22f, 0.215f, 0.225f),
                               new Color(0.46f, 0.44f, 0.42f), beds * 0.7f + grit * 0.3f);

                // Cracks hold shadow and damp.
                c = Tint(c, new Color(0.13f, 0.13f, 0.14f), Mathf.Pow(cracks, 5f));

                // A little green where water sits.
                float lichen = Mathf.Max(0f, Fbm(u, v, 26, 2, 139) - 0.60f) * 2.2f;
                c = Tint(c, new Color(0.34f, 0.37f, 0.28f), lichen * 0.55f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.04f + grit * 0.10f
                };
            });
        }

        /// <summary>Sawn timber cladding: boards, grain, knots.</summary>
        public static SurfaceMaps Wood(string name, Color light, Color dark)
        {
            return Build("Wood" + name, 512, 2.0f, (u, v) =>
            {
                // Boards running horizontally, with a gap between each.
                float boards = v * 14f;
                float board = Mathf.Floor(boards);
                float within = boards - board;

                float gap = Mathf.Min(within, 1f - within);
                float seam = 1f - Mathf.Clamp01(gap / 0.045f);

                // Each board is cut from a different part of the log.
                float offset = Lattice((int)board, 0, 14, 211);

                float grain = Fbm(u * 2f + offset, v * 26f, 24, 3, 149);
                float rings = Mathf.Abs(Mathf.Sin((grain * 3f + u * 5f) * Mathf.PI * 3f));

                float knot = Mathf.Max(0f, Fbm(u, v * 3f, 9, 2, 173 + (int)board) - 0.72f) * 3.5f;

                float h = rings * 0.25f + grain * 0.2f - seam * 0.8f - knot * 0.3f;

                Color c = Tint(dark, light, rings * 0.45f + grain * 0.55f);
                c = Tint(c, dark * 0.55f, seam);
                c = Tint(c, dark * 0.6f, Mathf.Clamp01(knot));

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.14f + rings * 0.12f
                };
            });
        }

        /// <summary>Galvanised steel: brushed, a little pitted, properly metal.</summary>
        public static SurfaceMaps Metal()
        {
            return Build("Metal", 256, 1.2f, (u, v) =>
            {
                float brush = Fbm(u * 18f, v, 64, 2, 181);
                float pits = Mathf.Max(0f, Fbm(u, v, 70, 2, 191) - 0.66f) * 3f;
                float grime = Fbm(u, v, 10, 3, 197);

                float h = brush * 0.4f - pits * 0.5f;

                Color c = Tint(new Color(0.44f, 0.46f, 0.50f),
                               new Color(0.62f, 0.64f, 0.68f), brush * 0.6f + grime * 0.4f);
                c = Tint(c, new Color(0.30f, 0.28f, 0.26f), pits * 0.5f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0.85f - pits * 0.4f,
                    smoothness = 0.52f + brush * 0.2f - pits * 0.3f
                };
            });
        }

        /// <summary>Painted steel, for lift chairs and signage.</summary>
        public static SurfaceMaps Painted(string name, Color colour)
        {
            return Build("Paint" + name, 256, 1.0f, (u, v) =>
            {
                float orange = Fbm(u, v, 80, 2, 199);
                float wear = Mathf.Max(0f, Fbm(u, v, 16, 3, 211) - 0.68f) * 3f;

                Color c = Tint(colour, colour * 1.12f, orange);
                c = Tint(c, new Color(0.42f, 0.40f, 0.38f), Mathf.Clamp01(wear) * 0.6f);

                return new Texel
                {
                    height = orange * 0.25f - wear * 0.3f,
                    colour = c,
                    metallic = wear * 0.5f,
                    smoothness = 0.44f - wear * 0.3f
                };
            });
        }

        /// <summary>Technical outerwear: woven, matte, with a faint sheen.</summary>
        public static SurfaceMaps Fabric(string name, Color colour)
        {
            return Build("Fabric" + name, 256, 1.4f, (u, v) =>
            {
                float weave = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 90f)) * Mathf.Abs(Mathf.Sin(v * Mathf.PI * 90f));
                float fold = Fbm(u, v, 9, 3, 223);

                Color c = Tint(colour * 0.86f, colour, fold * 0.7f + weave * 0.3f);

                return new Texel
                {
                    height = weave * 0.4f + fold * 0.6f,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.16f + weave * 0.12f
                };
            });
        }

        /// <summary>Bark: deeply furrowed, the way a mature conifer is.</summary>
        public static SurfaceMaps Bark()
        {
            return Build("Bark", 256, 3.0f, (u, v) =>
            {
                float furrow = Ridged(u * 3f, v * 0.6f, 10, 3, 233);
                float plates = Fbm(u * 2f, v, 20, 3, 241);

                float h = Mathf.Pow(furrow, 1.6f) * 0.7f + plates * 0.3f;

                Color c = Tint(new Color(0.135f, 0.100f, 0.075f),
                               new Color(0.330f, 0.250f, 0.185f), plates * 0.6f + furrow * 0.4f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.03f + plates * 0.06f
                };
            });
        }

        /// <summary>Conifer needles seen as a mass, not as individual leaves.</summary>
        public static SurfaceMaps Needles(string name, Color colour)
        {
            return Build("Needles" + name, 256, 2.6f, (u, v) =>
            {
                float clumps = Fbm(u, v, 22, 3, 251);
                float sprays = Ridged(u * 1.6f, v * 2.4f, 40, 2, 257);

                float h = clumps * 0.5f + sprays * 0.5f;

                Color c = Tint(colour * 0.62f, colour * 1.18f, clumps * 0.55f + sprays * 0.45f);

                return new Texel
                {
                    height = h,
                    colour = c,
                    metallic = 0f,
                    smoothness = 0.10f + sprays * 0.10f
                };
            });
        }

        /// <summary>A soft round blob, for particles.</summary>
        public static Texture2D SoftCircle(int size = 64) { return PrimitiveTextures.SoftCircle(size); }
    }
}
