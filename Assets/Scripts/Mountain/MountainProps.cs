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
        public int treeCount = 900;
        [Tooltip("No trees are placed above this height (metres).")]
        public float treeLine = 130f;
        [Tooltip("Keep trees this far away from the edge of the ski run.")]
        public float pisteClearance = 8f;
        public float minTreeHeight = 6f;
        public float maxTreeHeight = 15f;
        public float maxTreeSlopeDeg = 45f;

        [Header("Rocks")]
        public int rockCount = 210;
        public float minRockSize = 1.5f;
        public float maxRockSize = 5f;

        [Header("Piste edge markers")]
        public float markerSpacing = 25f;

        public int seed = 777;

        System.Random _rnd;

        /// <summary>One self-contained lump of geometry, ready to be batched.</summary>
        class Piece
        {
            public readonly List<Vector3> verts = new List<Vector3>();
            public readonly List<int> tris = new List<int>();
        }

        void Start() { Build(); }

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
            Piece trunk, needles, snow;
            BuildPine(out trunk, out needles, out snow);

            Material bark = MaterialFactory.Create("Bark", new Color(0.24f, 0.17f, 0.12f), 0.05f);
            Material snowMat = MaterialFactory.Create("TreeSnow", new Color(0.95f, 0.96f, 1f), 0.28f);
            var needleShades = new[]
            {
                MaterialFactory.Create("NeedlesA", new Color(0.09f, 0.23f, 0.15f), 0.05f),
                MaterialFactory.Create("NeedlesB", new Color(0.13f, 0.28f, 0.19f), 0.05f),
                MaterialFactory.Create("NeedlesC", new Color(0.10f, 0.21f, 0.21f), 0.05f)
            };

            var batch = new MeshBatcher(parent, "Forest",
                new[] { bark, needleShades[0], needleShades[1], needleShades[2], snowMat });

            var colliders = new GameObject("TreeColliders");
            colliders.transform.SetParent(parent, false);

            float halfW = mountain.width * 0.5f;
            int placed = 0;
            int guard = 0;
            int guardLimit = Mathf.Max(1000, treeCount * 40);

            while (placed < treeCount && guard < guardLimit)
            {
                guard++;

                float x = Rand(-halfW + 12f, halfW - 12f);
                float z = Rand(12f, mountain.length - 12f);

                if (mountain.IsOnPiste(x, z, pisteClearance)) continue;

                float h = mountain.SampleHeight(x, z);
                if (h > treeLine) continue;
                if (Vector3.Angle(mountain.SampleNormal(x, z), Vector3.up) > maxTreeSlopeDeg) continue;

                float height = Rand(minTreeHeight, maxTreeHeight);
                float girth = Rand(0.82f, 1.2f);
                var placement = Matrix4x4.TRS(
                    new Vector3(x, h - 0.3f, z),
                    Quaternion.Euler(0f, Rand(0f, 360f), 0f),
                    new Vector3(height * girth, height, height * girth));

                batch.Add(trunk.verts, trunk.tris, 0, placement);
                batch.Add(needles.verts, needles.tris, 1 + _rnd.Next(needleShades.Length), placement);
                batch.Add(snow.verts, snow.tris, 4, placement);

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
            Mesh sphere = BorrowPrimitiveMesh(PrimitiveType.Sphere);
            if (sphere == null) return;

            var boulder = new Piece();
            boulder.verts.AddRange(sphere.vertices);
            boulder.tris.AddRange(sphere.triangles);

            Material rockMat = MaterialFactory.Create("Rock", new Color(0.34f, 0.34f, 0.36f), 0.08f);
            Material capMat = MaterialFactory.Create("RockSnow", new Color(0.94f, 0.95f, 1f), 0.30f);

            var batch = new MeshBatcher(parent, "Rocks", new[] { rockMat, capMat });

            var colliders = new GameObject("RockColliders");
            colliders.transform.SetParent(parent, false);

            float halfW = mountain.width * 0.5f;

            for (int i = 0; i < rockCount; i++)
            {
                float x = Rand(-halfW + 8f, halfW - 8f);
                float z = Rand(10f, mountain.length - 10f);

                // Strictly off-piste: keeps the run clean and keeps rocks out
                // of the base area where the lodge stands.
                if (mountain.IsOnPiste(x, z, 2f)) continue;

                float sx = Rand(minRockSize, maxRockSize);
                float sy = sx * Rand(0.5f, 0.9f);
                float sz = sx * Rand(0.7f, 1.3f);

                Vector3 position = new Vector3(x, mountain.SampleHeight(x, z) - sy * 0.28f, z);
                Quaternion tilt = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
                var placement = Matrix4x4.TRS(position, tilt, new Vector3(sx, sy, sz));

                batch.Add(boulder.verts, boulder.tris, 0, placement);

                // Snow settles on top, level, however the boulder is tipped.
                var cap = Matrix4x4.TRS(position + Vector3.up * sy * 0.22f, Quaternion.identity,
                                        new Vector3(sx * 0.88f, sy * 0.55f, sz * 0.88f));
                batch.Add(boulder.verts, boulder.tris, 1, cap);

                var hit = new GameObject("RockCollider");
                hit.transform.SetParent(colliders.transform, false);
                hit.transform.SetPositionAndRotation(position, tilt);
                hit.transform.localScale = new Vector3(sx, sy, sz);

                var collider = hit.AddComponent<MeshCollider>();
                collider.sharedMesh = sphere;
                collider.convex = true;
            }

            batch.Flush();
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
            Material orange = MaterialFactory.Create("MarkerOrange", new Color(0.95f, 0.42f, 0.05f), 0.1f);
            Material green = MaterialFactory.Create("MarkerGreen", new Color(0.10f, 0.62f, 0.28f), 0.1f);
            Material blue = MaterialFactory.Create("MarkerBlue", new Color(0.10f, 0.35f, 0.85f), 0.1f);
            Material red = MaterialFactory.Create("MarkerRed", new Color(0.82f, 0.11f, 0.13f), 0.1f);

            var batch = new MeshBatcher(parent, "PisteMarkers", new[] { orange, green, blue, red });

            if (markerSpacing < 5f) markerSpacing = 5f;

            for (int i = 0; i < mountain.PisteCount; i++)
            {
                int gradeSlot = GradeSlot(mountain.pistes[i].grade);

                for (float z = 25f; z < mountain.length - 25f; z += markerSpacing)
                {
                    // Near the base and the summit the runs lie on top of one
                    // another, so only the first one is marked there.
                    if (i > 0 && mountain.PisteSpread(z) < 0.15f) continue;

                    float centre = mountain.PisteCenterX(i, z);
                    float half = mountain.PisteHalfWidth(i, z);

                    Marker(batch, gradeSlot, centre - half - 1.5f, z, pole);
                    Marker(batch, 0, centre + half + 1.5f, z, pole);
                }
            }

            batch.Flush();
        }

        static int GradeSlot(PisteGrade grade)
        {
            switch (grade)
            {
                case PisteGrade.Beginner: return 1;
                case PisteGrade.Advanced: return 3;
                default: return 2;
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
