using System.Collections.Generic;
using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Every material in the resort, in one place.
    ///
    /// Built once and shared. Two objects wearing "the same wood" wear the
    /// literally same material, which is what makes a place look designed
    /// rather than assembled — and it is also what lets the whole resort batch
    /// into a handful of draw calls.
    ///
    /// Terrain UVs are world metres, so the tiling numbers below are read
    /// directly as "how many metres of mountain one tile of texture covers".
    /// </summary>
    public static class Surfaces
    {
        static Material _powder, _packed, _groomed, _icy, _wind, _mixed;
        static Material _rock, _cliff;
        static Material _timber, _darkTimber, _stone, _steel, _galvanised, _glass;
        static Material _bark, _spruce, _fir, _pine, _treeSnow;

        // ---------------- snow ---------------------------------------------

        /// <summary>Untracked snow away from the runs. Wind has been at it.</summary>
        public static Material OffPiste
        {
            get
            {
                if (_wind != null) return _wind;

                _wind = MaterialFactory.CreateSurface("SnowOffPiste", ProceduralTextures.Windblown(),
                                                      Color.white, 24f, 1.15f);
                return MaterialFactory.WithDetail(_wind, ProceduralTextures.Powder(), 2.2f, 0.35f, 0.7f);
            }
        }

        public static Material Powder
        {
            get
            {
                if (_powder != null) return _powder;

                _powder = MaterialFactory.CreateSurface("SnowPowder", ProceduralTextures.Powder(),
                                                        Color.white, 16f, 0.9f);
                return MaterialFactory.WithDetail(_powder, ProceduralTextures.Powder(), 1.4f, 0.4f, 0.9f);
            }
        }

        public static Material Packed
        {
            get
            {
                if (_packed != null) return _packed;

                _packed = MaterialFactory.CreateSurface("SnowPacked", ProceduralTextures.Packed(),
                                                        Color.white, 18f, 1f);
                return MaterialFactory.WithDetail(_packed, ProceduralTextures.Packed(), 1.8f, 0.3f, 0.6f);
            }
        }

        /// <summary>
        /// Corduroy. Tiled tight, because the ridges a groomer leaves are about
        /// fifteen centimetres apart and at any coarser tiling they stop
        /// reading as corduroy and start reading as stripes.
        /// </summary>
        public static Material Groomed
        {
            get
            {
                if (_groomed != null) return _groomed;

                _groomed = MaterialFactory.CreateSurface("SnowGroomed", ProceduralTextures.Groomed(),
                                                         Color.white, 8f, 1.25f);
                return MaterialFactory.WithDetail(_groomed, ProceduralTextures.Packed(), 1.6f, 0.25f, 0.5f);
            }
        }

        public static Material Icy
        {
            get
            {
                if (_icy != null) return _icy;

                _icy = MaterialFactory.CreateSurface("SnowIcy", ProceduralTextures.Icy(),
                                                     Color.white, 14f, 0.8f);
                return MaterialFactory.WithDetail(_icy, ProceduralTextures.Icy(), 2.4f, 0.25f, 0.5f);
            }
        }

        public static Material Mixed
        {
            get
            {
                if (_mixed != null) return _mixed;

                _mixed = MaterialFactory.CreateSurface("SnowMixed", ProceduralTextures.Mixed(),
                                                       Color.white, 20f, 1f);
                return MaterialFactory.WithDetail(_mixed, ProceduralTextures.Packed(), 2f, 0.3f, 0.6f);
            }
        }

        // ---------------- geology -------------------------------------------

        public static Material Rock
        {
            get
            {
                if (_rock != null) return _rock;

                _rock = MaterialFactory.CreateSurface("Rock", ProceduralTextures.Rock(),
                                                      Color.white, 9f, 1.5f);
                return MaterialFactory.WithDetail(_rock, ProceduralTextures.Rock(), 1.2f, 0.4f, 0.9f);
            }
        }

        /// <summary>The same stone, darker and tiled larger, for big faces.</summary>
        public static Material Cliff
        {
            get
            {
                if (_cliff != null) return _cliff;

                _cliff = MaterialFactory.CreateSurface("Cliff", ProceduralTextures.Rock(),
                                                       new Color(0.82f, 0.83f, 0.86f), 22f, 1.7f);
                return MaterialFactory.WithDetail(_cliff, ProceduralTextures.Rock(), 2.6f, 0.45f, 1f);
            }
        }

        public static Material Stone
        {
            get
            {
                if (_stone != null) return _stone;

                _stone = MaterialFactory.CreateSurface("StoneWall", ProceduralTextures.Rock(),
                                                       new Color(0.86f, 0.85f, 0.83f), 2.2f, 1.3f);
                return _stone;
            }
        }

        // ---------------- built things ---------------------------------------

        public static Material Timber
        {
            get
            {
                if (_timber != null) return _timber;

                _timber = MaterialFactory.CreateSurface("Timber",
                    ProceduralTextures.Wood("Larch", new Color(0.55f, 0.38f, 0.24f),
                                                     new Color(0.26f, 0.17f, 0.11f)),
                    Color.white, 3.2f, 1.2f);

                return _timber;
            }
        }

        public static Material DarkTimber
        {
            get
            {
                if (_darkTimber != null) return _darkTimber;

                _darkTimber = MaterialFactory.CreateSurface("DarkTimber",
                    ProceduralTextures.Wood("Stained", new Color(0.30f, 0.21f, 0.15f),
                                                       new Color(0.13f, 0.09f, 0.07f)),
                    Color.white, 2.8f, 1.3f);

                return _darkTimber;
            }
        }

        public static Material Steel
        {
            get
            {
                if (_steel != null) return _steel;

                _steel = MaterialFactory.CreateSurface("Steel", ProceduralTextures.Metal(),
                                                       Color.white, 1.6f, 0.9f);
                return _steel;
            }
        }

        /// <summary>Lift towers: galvanised, so paler and less shiny than steel.</summary>
        public static Material Galvanised
        {
            get
            {
                if (_galvanised != null) return _galvanised;

                _galvanised = MaterialFactory.CreateSurface("Galvanised", ProceduralTextures.Metal(),
                                                            new Color(0.88f, 0.90f, 0.94f), 2.4f, 0.7f);
                return _galvanised;
            }
        }

        // Named materials are cached, because the things that ask for them —
        // the piste markers, the gates, a guest's jacket — are rebuilt every
        // time the terrain moves, and a new material per rebuild is a leak.
        static readonly Dictionary<string, Material> _named = new Dictionary<string, Material>();

        public static Material Painted(string name, Color colour)
        {
            return Cached("Painted" + name,
                          () => MaterialFactory.CreateSurface("Painted" + name,
                                    ProceduralTextures.Painted(name, colour),
                                    Color.white, 1.8f, 0.8f));
        }

        public static Material Fabric(string name, Color colour)
        {
            return Cached("Fabric" + name,
                          () => MaterialFactory.CreateSurface("Fabric" + name,
                                    ProceduralTextures.Fabric(name, colour),
                                    Color.white, 0.9f, 1f));
        }

        static Material Cached(string key, System.Func<Material> make)
        {
            Material found;
            if (_named.TryGetValue(key, out found) && found != null) return found;

            found = make();
            _named[key] = found;

            return found;
        }

        /// <summary>Lodge glazing: warm inside, reflective outside.</summary>
        public static Material Glass
        {
            get
            {
                if (_glass != null) return _glass;

                _glass = MaterialFactory.CreateEmissive("Glass", new Color(0.30f, 0.34f, 0.40f),
                                                        new Color(1f, 0.80f, 0.46f) * 1.4f);
                if (_glass.HasProperty("_Smoothness")) _glass.SetFloat("_Smoothness", 0.92f);
                if (_glass.HasProperty("_Metallic")) _glass.SetFloat("_Metallic", 0.1f);

                return _glass;
            }
        }

        // ---------------- vegetation ------------------------------------------

        public static Material Bark
        {
            get
            {
                if (_bark != null) return _bark;

                _bark = MaterialFactory.CreateSurface("Bark", ProceduralTextures.Bark(),
                                                      Color.white, 1.4f, 1.4f);
                return _bark;
            }
        }

        public static Material Spruce
        {
            get
            {
                if (_spruce != null) return _spruce;

                _spruce = MaterialFactory.CreateSurface("Spruce",
                    ProceduralTextures.Needles("Spruce", new Color(0.10f, 0.24f, 0.17f)),
                    Color.white, 2.6f, 1.2f);

                return _spruce;
            }
        }

        public static Material Fir
        {
            get
            {
                if (_fir != null) return _fir;

                _fir = MaterialFactory.CreateSurface("Fir",
                    ProceduralTextures.Needles("Fir", new Color(0.14f, 0.29f, 0.20f)),
                    Color.white, 2.4f, 1.2f);

                return _fir;
            }
        }

        public static Material Pine
        {
            get
            {
                if (_pine != null) return _pine;

                _pine = MaterialFactory.CreateSurface("Pine",
                    ProceduralTextures.Needles("Pine", new Color(0.11f, 0.21f, 0.22f)),
                    Color.white, 2.8f, 1.2f);

                return _pine;
            }
        }

        /// <summary>The snow that sits on branches, roofs and boulders.</summary>
        public static Material Settled
        {
            get
            {
                if (_treeSnow != null) return _treeSnow;

                _treeSnow = MaterialFactory.CreateSurface("SettledSnow", ProceduralTextures.Powder(),
                                                          Color.white, 3.4f, 1f);
                return _treeSnow;
            }
        }
    }
}
