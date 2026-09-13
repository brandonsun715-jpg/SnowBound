using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// Scatters pine trees, rocks and piste edge markers over the mountain so
    /// the scene reads as a real ski area instead of an empty white plane.
    ///
    /// Everything visible is welded into a handful of batched meshes rather
    /// than one object per tree: a forest of five hundred separate renderers
    /// costs five hundred draw calls, and that is the difference between a
    /// smooth frame rate and a bad one. Colliders stay separate and cheap,
    /// because physics wants them individually.
    ///
    /// Nothing here is saved into the scene file.
    /// </summary>
    [ExecuteAlways]
    public class MountainProps : MonoBehaviour
    {
        const string ContainerName = "GeneratedProps";

        [Tooltip("Leave empty to use the MountainGenerator on this same object.")]
        public MountainGenerator mountain;

        [Header("Pine trees")]
        public int treeCount = 8200;
        [Tooltip("Tree line as a share of the summit. Read off the mountain rather\nthan typed in, so moving a peak moves the forest with it.")]
        [Range(0.2f, 1f)] public float treeLineShare = 0.60f;
        [Tooltip("Metres of thinning below the line. A forest that stops dead along\na contour is the giveaway that nobody planted it.")]
        public float treeLineFade = 60f;
        [Tooltip("Keep trees this far away from the edge of a run.")]
        public float pisteClearance = 8f;
        public float minTreeHeight = 6f;
        public float maxTreeHeight = 15f;
        public float maxTreeSlopeDeg = 45f;

        [Tooltip("How tightly the forest clumps into stands. A forest scattered\nevenly is a plantation; a real one is stands with clearings between them.")]
        public float standScale = 0.0025f;

        [Header("Undergrowth")]
        [Tooltip("Scrub, bushes, fallen trees and stumps. Nothing here collides.")]
        public int undergrowthCount = 5000;

        [Header("Rocks")]
        public int rockCount = 4200;
        public float minRockSize = 1.5f;
        public float maxRockSize = 5f;

        [Header("Cliffs")]
        [Tooltip("Bands of bedded rock on the steep ground. A band is several\nslabs laid along the contour, not one boulder made big.")]
        public int cliffBands = 150;
        [Tooltip("How steep the ground has to be before the rock breaks through.")]
        public float minCliffSlopeDeg = 32f;
        [Tooltip("Width of a slab in the band, in metres.")]
        public float cliffSize = 7f;

        [Header("Piste edge markers")]
        public float markerSpacing = 25f;

        public int seed = 777;

        System.Random _rnd;
        readonly GroundWatch _ground = new GroundWatch();

        /// <summary>One self-contained lump of geometry, ready to be batched.</summary>
        class Piece
        {
            public readonly List<Vector3> verts = new List<Vector3>();
            public readonly List<int> tris = new List<int>();
        }

        /// <summary>
        /// One placed piece of cliff. Planned before anything is planted, so
        /// the forest and the boulders can be told to keep off it.
        /// </summary>
        class Cliff
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float yaw;
            public float height;
            public float reach;
            public bool tooth;
        }

        readonly List<Cliff> _cliffs = new List<Cliff>();

        void Start() { Build(); }
        void OnDisable() { _ground.Stop(); }

        void Update()
        {
            if (!Application.isPlaying) return;
            _ground.Tick();
        }

        float Rand(float a, float b) { return a + (float)_rnd.NextDouble() * (b - a); }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == ContainerName) Kill(c.gameObject);
            }
        }

        [ContextMenu("Build Now")]
        public void Build()
        {
            if (mountain == null) mountain = GetComponent<MountainGenerator>();
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (mountain == null)
            {
                Debug.LogError("[MountainProps] No MountainGenerator found. " +
                               "Put this component on the same GameObject as MountainGenerator.", this);
                return;
            }

            Clear();
            _rnd = new System.Random(seed);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(transform, false);

            // Rock first, everything else around it.
            PlanCliffs();
            IndexCliffs();

            SpawnTrees(container.transform);
            SpawnUndergrowth(container.transform);
            SpawnRocks(container.transform);
            SpawnMarkers(container.transform);

            // Keep the generated clutter out of the saved scene file.
            foreach (Transform tr in container.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            // Every trunk was planted at the height the ground was. Carve a run
            // under the forest and half of it is standing on air, so scatter
            // again when the ground moves. A wide grid, because the change that
            // matters here is usually a new trail rather than one brush stroke.
            _ground.Follow(mountain, Build);
            _ground.quiet = 0.75f;
            _ground.Note(Rect.MinMaxRect(-mountain.width * 0.5f, 0f,
                                         mountain.width * 0.5f, mountain.length), 24);
        }

        // ---------------- trees ------------------------------------------

        /// <summary>
        /// A pine as three separate lumps: trunk, needles, and the snow lying
        /// on top of each tier of branches.
        /// </summary>
        static void BuildPine(out Piece trunk, out Piece needles, out Piece snow)
        {
            trunk = new Piece();
            needles = new Piece();
            snow = new Piece();

            PrimitiveMeshes.AddTube(trunk.verts, trunk.tris, Vector3.zero, 0f, 0.34f, 0.055f, 0.040f, 6);

            // Three tiers, each with a cap of settled snow on its upper third.
            AddTier(needles, snow, 0.16f, 0.56f, 0.23f);
            AddTier(needles, snow, 0.42f, 0.80f, 0.17f);
            AddTier(needles, snow, 0.66f, 1.02f, 0.11f);
        }

        static void AddTier(Piece needles, Piece snow, float bottom, float top, float radius)
        {
            PrimitiveMeshes.AddTube(needles.verts, needles.tris, Vector3.zero, bottom, top, radius, 0f, 8);

            const float share = 0.36f;
            float capBottom = Mathf.Lerp(top, bottom, share);
            float capRadius = radius * share * 1.12f;
            PrimitiveMeshes.AddTube(snow.verts, snow.tris, Vector3.zero,
                                    capBottom, top + 0.012f, capRadius, 0f, 8);
        }

        void SpawnTrees(Transform parent)
        {
            // Three modelled species if they are in the project, and the
            // stacked cones this started as if they are not.
            var wood = new HeroAssets.Piece[3];
            var caps = new HeroAssets.Piece[3];
            bool modelled = true;

            for (int i = 0; i < 3; i++)
            {
                string tag = i == 0 ? "A" : i == 1 ? "B" : "C";
                wood[i] = HeroAssets.Geometry(HeroAssets.Trees, "Tree" + tag);
                caps[i] = HeroAssets.Geometry(HeroAssets.Trees, "Snow" + tag);
                modelled &= wood[i] != null;
            }

            Material forest = modelled ? HeroAssets.Surface(HeroAssets.Trees) : null;
            modelled &= forest != null;

            Piece trunk = null, needles = null, snow = null;
            if (!modelled) BuildPine(out trunk, out needles, out snow);

            // A modelled tree carries its own bark, needles and snow in one
            // baked texture, so the whole forest is one material. The
            // fallback needs five: bark, three shades of needle and snow.
            MeshBatcher batch = modelled
                ? new MeshBatcher(parent, "Forest", new[] { forest }, 60000, true)
                : new MeshBatcher(parent, "Forest",
                    new[] { Surfaces.Bark, Surfaces.Spruce, Surfaces.Fir, Surfaces.Pine,
                            Surfaces.Settled });

            var colliders = new GameObject("TreeColliders");
            colliders.transform.SetParent(parent, false);

            float halfW = mountain.width * 0.5f;

            // The line the forest stops at, and the band it thins out over.
            float treeLine = Mathf.Max(20f, mountain.Summit * treeLineShare);
            float fadeFrom = treeLine - Mathf.Max(1f, treeLineFade);

            // A modelled tree is drawn ten metres tall; the old one was drawn
            // one, and both are scaled to the height this tree wants.
            float unit = modelled ? 1f / HeroAssets.TreeHeight : 1f;

            int placed = 0;
            int guard = 0;
            int guardLimit = Mathf.Max(1000, treeCount * 40);

            while (placed < treeCount && guard < guardLimit)
            {
                guard++;

                float x = Rand(-halfW + 12f, halfW - 12f);
                float z = Rand(12f, mountain.length - 12f);

                if (mountain.OnAnyTrail(x, z, pisteClearance)) continue;
                if (NearCliff(x, z, 1.5f)) continue;

                float h = mountain.SampleHeight(x, z);
                if (h > treeLine) continue;
                if (Vector3.Angle(mountain.SampleNormal(x, z), Vector3.up) > maxTreeSlopeDeg) continue;

                // Thin out towards the tree line. Squared, because a real one
                // goes from forest to scattered survivors quickly and then
                // takes a while to give up altogether.
                float density = Mathf.InverseLerp(treeLine, fadeFrom, h);
                if (density < 1f && _rnd.NextDouble() > density * density) continue;

                // And clump. Trees grow where other trees already are, so a
                // forest is stands with clearings between them rather than an
                // even scatter — which is the difference between a forest and
                // a plantation, and it reads from a kilometre away.
                if (_rnd.NextDouble() > Stand(x, z)) continue;

                // Shorter with altitude: the same species runs out of season
                // before it runs out of ground.
                float altitude = Mathf.InverseLerp(0f, Mathf.Max(1f, treeLine), h);
                float height = Rand(minTreeHeight, maxTreeHeight) * Mathf.Lerp(1.05f, 0.62f, altitude);
                float girth = Rand(0.82f, 1.2f);
                var placement = Matrix4x4.TRS(
                    new Vector3(x, h - 0.3f, z),
                    Quaternion.Euler(0f, Rand(0f, 360f), 0f),
                    new Vector3(height * girth * unit, height * unit, height * girth * unit));

                if (modelled)
                {
                    int species = _rnd.Next(wood.Length);

                    batch.Add(wood[species].vertices, wood[species].triangles, 0, placement,
                              wood[species].uvs);

                    if (caps[species] != null)
                        batch.Add(caps[species].vertices, caps[species].triangles, 0, placement,
                                  caps[species].uvs);
                }
                else
                {
                    batch.Add(trunk.verts, trunk.tris, 0, placement);
                    batch.Add(needles.verts, needles.tris, 1 + _rnd.Next(3), placement);
                    batch.Add(snow.verts, snow.tris, 4, placement);
                }

                var hit = new GameObject("TreeCollider");
                hit.transform.SetParent(colliders.transform, false);
                hit.transform.position = new Vector3(x, h - 0.3f, z);
                hit.transform.localScale = new Vector3(height * girth, height, height * girth);

                var capsule = hit.AddComponent<CapsuleCollider>();
                capsule.radius = 0.06f;
                capsule.height = 0.9f;
                capsule.center = new Vector3(0f, 0.45f, 0f);

                placed++;
            }

            batch.Flush();
        }

        /// <summary>
        /// How much forest belongs at a point, ignoring altitude: two
        /// octaves of noise, one for the stands and one for the gaps inside
        /// them.
        /// </summary>
        float Stand(float x, float z)
        {
            float stand = Mathf.PerlinNoise((x + 500f) * standScale, (z + 900f) * standScale);
            float gaps = Mathf.PerlinNoise((x - 200f) * standScale * 3.1f,
                                           (z + 300f) * standScale * 3.1f);

            return Mathf.Clamp01(stand * 1.35f - 0.20f + (gaps - 0.5f) * 0.30f);
        }

        // ---------------- undergrowth -------------------------------------

        /// <summary>
        /// What grows between the trees: scrub at the tree line, bare bushes
        /// in the clearings, and blown-down trunks and stumps inside the
        /// stands where they fell.
        ///
        /// None of it collides. It is there to break up the ground between
        /// the trees, which is the difference between a forest and a set of
        /// trees standing on a white plane.
        /// </summary>
        void SpawnUndergrowth(Transform parent)
        {
            var scrub = HeroAssets.Geometry(HeroAssets.Flora, "ShrubA");
            var scrubSnow = HeroAssets.Geometry(HeroAssets.Flora, "SnowShrubA");
            var bush = HeroAssets.Geometry(HeroAssets.Flora, "ShrubB");
            var fallen = HeroAssets.Geometry(HeroAssets.Flora, "Fallen");
            var fallenSnow = HeroAssets.Geometry(HeroAssets.Flora, "SnowFallen");
            var stump = HeroAssets.Geometry(HeroAssets.Flora, "Stump");

            Material flora = HeroAssets.Surface(HeroAssets.Flora);
            if (scrub == null || bush == null || fallen == null || stump == null || flora == null)
                return;

            var batch = new MeshBatcher(parent, "Undergrowth", new[] { flora }, 60000, true);

            float halfW = mountain.width * 0.5f;
            float treeLine = Mathf.Max(20f, mountain.Summit * treeLineShare);

            int guard = 0;
            int placed = 0;
            int guardLimit = Mathf.Max(1000, undergrowthCount * 30);

            while (placed < undergrowthCount && guard < guardLimit)
            {
                guard++;

                float x = Rand(-halfW + 10f, halfW - 10f);
                float z = Rand(10f, mountain.length - 10f);

                if (mountain.OnAnyTrail(x, z, pisteClearance * 0.6f)) continue;
                if (NearCliff(x, z, 0.5f)) continue;

                float h = mountain.SampleHeight(x, z);
                float slope = Vector3.Angle(mountain.SampleNormal(x, z), Vector3.up);
                if (slope > 42f) continue;

                float altitude = h / Mathf.Max(1f, treeLine);
                float stand = Stand(x, z);

                HeroAssets.Piece piece;
                HeroAssets.Piece snow = null;
                float size;

                if (altitude > 0.86f)
                {
                    // Above the trees: scrub, and only where it is sheltered.
                    if (altitude > 1.35f || _rnd.NextDouble() > 0.55) continue;
                    piece = scrub;
                    snow = scrubSnow;
                    size = Rand(0.7f, 1.3f);
                }
                else if (stand > 0.62f)
                {
                    // Inside a stand: what fell, and what was cut.
                    bool log = _rnd.NextDouble() < 0.45;
                    piece = log ? fallen : stump;
                    snow = log ? fallenSnow : null;
                    size = Rand(0.8f, 1.25f);
                }
                else if (stand > 0.18f)
                {
                    piece = _rnd.NextDouble() < 0.65 ? bush : scrub;
                    snow = piece == scrub ? scrubSnow : null;
                    size = Rand(0.8f, 1.4f);
                }
                else
                {
                    continue;
                }

                var placement = Matrix4x4.TRS(
                    new Vector3(x, h - 0.06f, z),
                    Quaternion.Euler(Rand(-5f, 5f), Rand(0f, 360f), Rand(-5f, 5f)),
                    new Vector3(size, size * Rand(0.85f, 1.15f), size));

                batch.Add(piece.vertices, piece.triangles, 0, placement, piece.uvs);
                if (snow != null)
                    batch.Add(snow.vertices, snow.triangles, 0, placement, snow.uvs);

                placed++;
            }

            batch.Flush();
        }

        // ---------------- rocks ------------------------------------------

        void SpawnRocks(Transform parent)
        {
            // Three modelled boulders if they are there, and Unity's sphere
            // if they are not.
            var stone = new HeroAssets.Piece[3];
            bool modelled = true;

            for (int i = 0; i < 3; i++)
            {
                stone[i] = HeroAssets.Geometry(HeroAssets.Rocks,
                                               "Rock" + (i == 0 ? "A" : i == 1 ? "B" : "C"));
                modelled &= stone[i] != null;
            }

            Material granite = modelled ? HeroAssets.Surface(HeroAssets.Rocks) : null;
            modelled &= granite != null;

            Mesh sphere = BorrowPrimitiveMesh(PrimitiveType.Sphere);
            if (sphere == null && !modelled) return;

            var boulder = new Piece();
            if (sphere != null)
            {
                boulder.verts.AddRange(sphere.vertices);
                boulder.tris.AddRange(sphere.triangles);
            }

            // Two batches, not two sub-meshes: the rock brought its own
            // texture coordinates and the snow on top of it has none, and
            // one mesh cannot be unwrapped and projected at the same time.
            var rocks = modelled
                ? new MeshBatcher(parent, "Rocks", new[] { granite }, 60000, true)
                : new MeshBatcher(parent, "Rocks", new[] { Surfaces.Rock });

            var settled = new MeshBatcher(parent, "RockSnow", new[] { Surfaces.Settled });

            var colliders = new GameObject("RockColliders");
            colliders.transform.SetParent(parent, false);

            float halfW = mountain.width * 0.5f;
            float unit = modelled ? 1f / HeroAssets.RockSize : 1f;

            for (int i = 0; i < rockCount; i++)
            {
                float x = Rand(-halfW + 8f, halfW - 8f);
                float z = Rand(10f, mountain.length - 10f);

                // Strictly off-piste: keeps the run clean and keeps rocks out
                // of the base area where the lodge stands.
                if (mountain.OnAnyTrail(x, z, 2f)) continue;
                if (NearCliff(x, z, 1f)) continue;

                float h = mountain.SampleHeight(x, z);

                // Rock shows through where the mountain is steep, high, or
                // has been scoured — not evenly over the whole map. Boulders
                // sit low where they rolled to; scree lies high where it
                // broke off.
                float slope = Vector3.Angle(mountain.SampleNormal(x, z), Vector3.up);
                float altitude = Mathf.InverseLerp(0f, Mathf.Max(1f, mountain.Summit), h);
                float field = Mathf.PerlinNoise((x + 1300f) * 0.0075f, (z - 700f) * 0.0075f);

                float chance = Mathf.Clamp01(field * 1.5f - 0.35f)
                             + Mathf.InverseLerp(24f, 46f, slope) * 0.55f
                             + altitude * 0.45f;

                if (_rnd.NextDouble() > chance) continue;

                // Big at the bottom, scree at the top.
                float scale = Mathf.Lerp(1.0f, 0.34f, altitude) * Rand(0.7f, 1.25f);
                float sx = Mathf.Clamp(maxRockSize * scale, minRockSize * 0.4f, maxRockSize);
                float sy = sx * Rand(0.5f, 0.9f);
                float sz = sx * Rand(0.7f, 1.3f);

                Vector3 position = new Vector3(x, h - sy * 0.28f, z);
                Quaternion tilt = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
                var placement = Matrix4x4.TRS(position, tilt,
                                              new Vector3(sx * unit, sy * unit, sz * unit));

                HeroAssets.Piece rock = modelled ? stone[_rnd.Next(stone.Length)] : null;

                if (rock != null) rocks.Add(rock.vertices, rock.triangles, 0, placement, rock.uvs);
                else rocks.Add(boulder.verts, boulder.tris, 0, placement);

                // Snow settles on top, level, however the boulder is tipped —
                // and it is the same shape as the rock under it, so it sits on
                // the facets instead of bulging off them.
                var cap = Matrix4x4.TRS(position + Vector3.up * sy * 0.22f, Quaternion.identity,
                                        new Vector3(sx * 0.88f, sy * 0.55f, sz * 0.88f));

                if (rock != null)
                {
                    var capped = Matrix4x4.TRS(position + Vector3.up * sy * 0.22f, Quaternion.identity,
                                               new Vector3(sx * 0.88f * unit, sy * 0.55f * unit,
                                                           sz * 0.88f * unit));
                    settled.Add(rock.vertices, rock.triangles, 0, capped);
                }
                else if (boulder.verts.Count > 0)
                {
                    settled.Add(boulder.verts, boulder.tris, 0, cap);
                }

                var hit = new GameObject("RockCollider");
                hit.transform.SetParent(colliders.transform, false);
                hit.transform.SetPositionAndRotation(position, tilt);
                hit.transform.localScale = new Vector3(sx, sy, sz);

                var collider = hit.AddComponent<MeshCollider>();
                collider.sharedMesh = sphere;
                collider.convex = true;
            }

            SpawnCliffs(rocks, settled, colliders.transform, unit, modelled);

            rocks.Flush();
            settled.Flush();
        }

        // ---------------- cliffs -----------------------------------------

        /// <summary>
        /// Bands of bedded rock on the steep ground.
        ///
        /// A cliff is not a big boulder. What reads as a cliff is a line of
        /// flat-topped slabs following one contour — the bed that broke —
        /// with the odd tooth left standing above it, all of it half buried
        /// so the rock comes out of the mountain instead of sitting on it.
        ///
        /// It shares the boulders' batches, so a whole map of cliffs is not
        /// one extra draw call.
        /// </summary>
        void PlanCliffs()
        {
            _cliffs.Clear();
            if (cliffBands <= 0) return;

            HeroAssets.Piece slab = HeroAssets.Geometry(HeroAssets.Rocks, "RockSlab");
            HeroAssets.Piece spire = HeroAssets.Geometry(HeroAssets.Rocks, "RockSpire");
            if (slab == null || spire == null) return;

            float halfW = mountain.width * 0.5f;

            // The same one number the boulders use: the set was drawn two
            // metres across, so a metre of world is half of it.
            float unit = 1f / HeroAssets.RockSize;

            for (int band = 0; band < cliffBands; band++)
            {
                // Find somewhere steep. Give up on a band rather than settle
                // for flat ground: a cliff in a meadow is worse than no cliff.
                float x = 0f, z = 0f;
                Vector3 normal = Vector3.up;
                bool found = false;

                for (int attempt = 0; attempt < 40 && !found; attempt++)
                {
                    x = Rand(-halfW + 20f, halfW - 20f);
                    z = Rand(30f, mountain.length - 30f);

                    if (mountain.OnAnyTrail(x, z, cliffSize)) continue;

                    normal = mountain.SampleNormal(x, z);
                    found = Vector3.Angle(normal, Vector3.up) >= minCliffSlopeDeg;
                }

                if (!found) continue;

                // Along the contour, not down the fall line. Rock breaks along
                // the bed, and the bed is level.
                Vector3 fall = Vector3.ProjectOnPlane(-normal, Vector3.up);
                if (fall.sqrMagnitude < 0.0001f) continue;

                Vector3 contour = Vector3.Cross(Vector3.up, fall.normalized).normalized;

                int pieces = _rnd.Next(3, 7);
                float step = cliffSize * Rand(0.62f, 0.82f);
                float start = -(pieces - 1) * 0.5f * step;

                for (int i = 0; i < pieces; i++)
                {
                    float along = start + i * step;

                    // The band wanders off its line a little, so it is a band
                    // and not a wall.
                    Vector3 drift = fall.normalized * Rand(-cliffSize * 0.35f, cliffSize * 0.35f);
                    float px = x + contour.x * along + drift.x;
                    float pz = z + contour.z * along + drift.z;

                    // Wider than it looks: the run's fencing stands a couple
                    // of metres out from the edge, and a slab through a fence
                    // panel is worse than a gap in the band.
                    if (mountain.OnAnyTrail(px, pz, 6f)) continue;

                    Vector3 groundNormal = mountain.SampleNormal(px, pz);
                    if (Vector3.Angle(groundNormal, Vector3.up) < minCliffSlopeDeg * 0.7f) continue;

                    float h = mountain.SampleHeight(px, pz);
                    bool tooth = _rnd.NextDouble() < 0.22;

                    HeroAssets.Piece piece = tooth ? spire : slab;

                    float size = cliffSize * Rand(0.72f, 1.30f) * (tooth ? 0.65f : 1f);
                    var scale = new Vector3(size * Rand(0.9f, 1.15f), size * Rand(0.85f, 1.2f),
                                            size * Rand(0.9f, 1.15f));

                    // Bedded into the slope: the slab lies with the hill, and
                    // most of its depth is under the surface.
                    Quaternion bed = Quaternion.Slerp(Quaternion.identity,
                                                      Quaternion.FromToRotation(Vector3.up, groundNormal),
                                                      Rand(0.55f, 0.95f));

                    float yaw = Mathf.Atan2(-contour.z, contour.x) * Mathf.Rad2Deg + Rand(-16f, 16f);
                    Quaternion rotation = bed * Quaternion.Euler(Rand(-8f, 8f), yaw, Rand(-8f, 8f));

                    // Half buried, measured off the model rather than guessed:
                    // a slab is sunk most of its thickness, a tooth only its
                    // foot, because a tooth is what is left standing.
                    Vector3 extent = LocalSize(piece);
                    float height = extent.y * scale.y * unit;
                    float sink = height * (tooth ? Rand(0.16f, 0.28f) : Rand(0.30f, 0.52f));
                    var position = new Vector3(px, h - sink, pz);

                    _cliffs.Add(new Cliff
                    {
                        position = position,
                        rotation = rotation,
                        scale = scale,
                        yaw = yaw,
                        height = height,
                        tooth = tooth,
                        reach = Mathf.Max(extent.x * scale.x, extent.z * scale.z) * unit * 0.5f,
                    });
                }
            }
        }

        /// <summary>Write the planned cliffs into the boulders' batches.</summary>
        void SpawnCliffs(MeshBatcher rocks, MeshBatcher settled, Transform colliders,
                         float unit, bool modelled)
        {
            if (!modelled || _cliffs.Count == 0) return;

            HeroAssets.Piece slab = HeroAssets.Geometry(HeroAssets.Rocks, "RockSlab");
            HeroAssets.Piece spire = HeroAssets.Geometry(HeroAssets.Rocks, "RockSpire");
            if (slab == null || spire == null) return;

            Mesh slabHull = HeroAssets.Shape(HeroAssets.Rocks, "RockSlab");
            Mesh spireHull = HeroAssets.Shape(HeroAssets.Rocks, "RockSpire");

            foreach (Cliff cliff in _cliffs)
            {
                HeroAssets.Piece piece = cliff.tooth ? spire : slab;
                Mesh hull = cliff.tooth ? spireHull : slabHull;

                var placement = Matrix4x4.TRS(cliff.position, cliff.rotation, cliff.scale * unit);
                rocks.Add(piece.vertices, piece.triangles, 0, placement, piece.uvs);

                // Snow lies on the top of a slab, level, but only on the ones
                // that are lying down — nothing settles on a tooth.
                if (!cliff.tooth && Vector3.Angle(cliff.rotation * Vector3.up, Vector3.up) < 34f)
                {
                    var capped = Matrix4x4.TRS(cliff.position + Vector3.up * cliff.height * 0.26f,
                                               Quaternion.Euler(0f, cliff.yaw, 0f),
                                               new Vector3(cliff.scale.x * 0.9f, cliff.scale.y * 0.55f,
                                                           cliff.scale.z * 0.9f) * unit);
                    settled.Add(piece.vertices, piece.triangles, 0, capped);
                }

                if (hull == null) continue;

                var hit = new GameObject(cliff.tooth ? "CliffTooth" : "CliffSlab");
                hit.transform.SetParent(colliders, false);
                hit.transform.SetPositionAndRotation(cliff.position, cliff.rotation);
                hit.transform.localScale = cliff.scale * unit;

                var collider = hit.AddComponent<MeshCollider>();
                collider.sharedMesh = hull;
                collider.convex = true;
            }
        }

        // Cliffs bucketed by where they are, so asking "is there one here" does
        // not mean asking every one of them.
        //
        // It used to. Three scatters ask this question once per candidate, and
        // on a map five times the size that was a third of a million questions
        // against seven hundred cliffs — two hundred million distance checks
        // for the forest alone, every time the mountain was rebuilt. Rebuilds
        // happen when the ground is sculpted and when the quality dial moves,
        // so that cost was paid in front of the player.
        const float CliffCell = 32f;

        readonly Dictionary<long, List<Cliff>> _cliffGrid = new Dictionary<long, List<Cliff>>();

        static long CliffKey(float x, float z)
        {
            long cx = (long)Mathf.Floor(x / CliffCell);
            long cz = (long)Mathf.Floor(z / CliffCell);

            return (cx << 32) ^ (cz & 0xffffffffL);
        }

        void IndexCliffs()
        {
            _cliffGrid.Clear();

            foreach (Cliff cliff in _cliffs)
            {
                long key = CliffKey(cliff.position.x, cliff.position.z);

                List<Cliff> bucket;
                if (!_cliffGrid.TryGetValue(key, out bucket))
                    _cliffGrid[key] = bucket = new List<Cliff>();

                bucket.Add(cliff);
            }
        }

        /// <summary>
        /// True where a cliff already is. Nothing else is planted there: a
        /// pine growing out of the middle of a slab is the single loudest way
        /// to say none of this was placed by anybody.
        /// </summary>
        bool NearCliff(float x, float z, float clearance)
        {
            if (_cliffGrid.Count == 0) return false;

            // A cliff reaches a few metres and the clearance a couple more, so
            // the neighbouring cells are as far as this can possibly matter.
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    List<Cliff> bucket;
                    if (!_cliffGrid.TryGetValue(
                            CliffKey(x + ox * CliffCell, z + oz * CliffCell), out bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Cliff cliff = bucket[i];
                        float dx = x - cliff.position.x;
                        float dz = z - cliff.position.z;
                        float reach = cliff.reach + clearance;

                        if (dx * dx + dz * dz < reach * reach) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>How big one piece of geometry is in its own space.</summary>
        static Vector3 LocalSize(HeroAssets.Piece piece)
        {
            var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < piece.vertices.Count; i++)
            {
                low = Vector3.Min(low, piece.vertices[i]);
                high = Vector3.Max(high, piece.vertices[i]);
            }

            Vector3 size = high - low;

            return size.x > 0f ? size : Vector3.one;
        }

        /// <summary>
        /// Unity's built-in meshes are only reachable through a primitive, so
        /// make one, take its mesh, and throw the object away.
        /// </summary>
        static Mesh BorrowPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            temp.SetActive(false);   // Destroy is deferred in play mode
            Kill(temp);
            return mesh;
        }

        // ---------------- piste markers ----------------------------------

        void SpawnMarkers(Transform parent)
        {
            var pole = new Piece();
            PrimitiveMeshes.AddTube(pole.verts, pole.tris, Vector3.zero, 0f, 1.9f, 0.07f, 0.05f, 6);

            // Outer edge orange as they are on a real mountain; inner edge in
            // the run's own grade colour, so you can read which run you are on.
            Material orange = Surfaces.Painted("MarkerOrange", new Color(0.95f, 0.42f, 0.05f));
            Material green = Surfaces.Painted("MarkerGreen", new Color(0.10f, 0.62f, 0.28f));
            Material blue = Surfaces.Painted("MarkerBlue", new Color(0.10f, 0.35f, 0.85f));
            Material red = Surfaces.Painted("MarkerRed", new Color(0.82f, 0.11f, 0.13f));

            var batch = new MeshBatcher(parent, "PisteMarkers", new[] { orange, green, blue, red });

            if (markerSpacing < 5f) markerSpacing = 5f;

            // Markers follow the run's own centre line, so a run that snakes
            // across the mountain is marked along the line the player drew
            // rather than along a straight guess at it.
            for (int i = 0; i < mountain.TrailCount; i++)
            {
                Trail trail = mountain.TrailAt(i);
                if (trail == null || trail.spine == null || trail.spine.Count < 2) continue;

                int gradeSlot = GradeSlot(trail.grade);
                int steps = Mathf.Max(2, Mathf.RoundToInt(trail.length / markerSpacing));

                for (int s = 1; s < steps; s++)
                {
                    float along = s / (float)steps;

                    Vector3 here = trail.PointAt(along);
                    Vector3 ahead = trail.PointAt(Mathf.Min(1f, along + 0.01f));

                    Vector3 forward = ahead - here;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.001f) continue;

                    Vector3 across = Vector3.Cross(Vector3.up, forward.normalized);
                    float half = trail.halfWidth + 1.5f;

                    Marker(batch, gradeSlot, here.x - across.x * half, here.z - across.z * half, pole);
                    Marker(batch, 0, here.x + across.x * half, here.z + across.z * half, pole);
                }
            }

            batch.Flush();
        }

        static int GradeSlot(TrailGrade grade)
        {
            switch (grade)
            {
                case TrailGrade.Green: return 1;
                case TrailGrade.Blue: return 2;
                default: return 3;
            }
        }

        void Marker(MeshBatcher batch, int slot, float x, float z, Piece pole)
        {
            var placement = Matrix4x4.TRS(new Vector3(x, mountain.SampleHeight(x, z) - 0.2f, z),
                                          Quaternion.identity, Vector3.one);
            batch.Add(pole.verts, pole.tris, slot, placement);
        }
    }
}
