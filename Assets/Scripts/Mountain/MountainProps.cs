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
        public int treeCount = 1800;
        [Tooltip("Tree line as a share of the summit. Read off the mountain rather\nthan typed in, so moving a peak moves the forest with it.")]
        [Range(0.2f, 1f)] public float treeLineShare = 0.60f;
        [Tooltip("Metres of thinning below the line. A forest that stops dead along\na contour is the giveaway that nobody planted it.")]
        public float treeLineFade = 60f;
        [Tooltip("Keep trees this far away from the edge of a run.")]
        public float pisteClearance = 8f;
        public float minTreeHeight = 6f;
        public float maxTreeHeight = 15f;
        public float maxTreeSlopeDeg = 45f;

        [Header("Rocks")]
        public int rockCount = 420;
        public float minRockSize = 1.5f;
        public float maxRockSize = 5f;

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

            SpawnTrees(container.transform);
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

                float h = mountain.SampleHeight(x, z);
                if (h > treeLine) continue;
                if (Vector3.Angle(mountain.SampleNormal(x, z), Vector3.up) > maxTreeSlopeDeg) continue;

                // Thin out towards the tree line. Squared, because a real one
                // goes from forest to scattered survivors quickly and then
                // takes a while to give up altogether.
                float density = Mathf.InverseLerp(treeLine, fadeFrom, h);
                if (density < 1f && _rnd.NextDouble() > density * density) continue;

                float height = Rand(minTreeHeight, maxTreeHeight);
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

                float sx = Rand(minRockSize, maxRockSize);
                float sy = sx * Rand(0.5f, 0.9f);
                float sz = sx * Rand(0.7f, 1.3f);

                Vector3 position = new Vector3(x, mountain.SampleHeight(x, z) - sy * 0.28f, z);
                Quaternion tilt = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
                var placement = Matrix4x4.TRS(position, tilt,
                                              new Vector3(sx * unit, sy * unit, sz * unit));

                if (modelled)
                {
                    HeroAssets.Piece rock = stone[_rnd.Next(stone.Length)];
                    rocks.Add(rock.vertices, rock.triangles, 0, placement, rock.uvs);
                }
                else
                {
                    rocks.Add(boulder.verts, boulder.tris, 0, placement);
                }

                // Snow settles on top, level, however the boulder is tipped.
                if (boulder.verts.Count > 0)
                {
                    var cap = Matrix4x4.TRS(position + Vector3.up * sy * 0.22f, Quaternion.identity,
                                            new Vector3(sx * 0.88f, sy * 0.55f, sz * 0.88f));
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

            rocks.Flush();
            settled.Flush();
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
