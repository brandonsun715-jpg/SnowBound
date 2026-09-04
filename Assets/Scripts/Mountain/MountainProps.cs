using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// Scatters pine trees, rocks and piste edge markers over the mountain so
    /// the scene reads as a real ski area instead of an empty white plane.
    /// Everything it makes lives under one child object called "GeneratedProps"
    /// and is never saved into the scene file.
    /// </summary>
    [ExecuteAlways]
    public class MountainProps : MonoBehaviour
    {
        const string ContainerName = "GeneratedProps";

        [Tooltip("Leave empty to use the MountainGenerator on this same object.")]
        public MountainGenerator mountain;

        [Header("Pine trees")]
        public int treeCount = 500;
        [Tooltip("No trees are placed above this height (metres).")]
        public float treeLine = 130f;
        [Tooltip("Keep trees this far away from the edge of the ski run.")]
        public float pisteClearance = 8f;
        public float minTreeHeight = 6f;
        public float maxTreeHeight = 15f;
        public float maxTreeSlopeDeg = 45f;

        [Header("Rocks")]
        public int rockCount = 140;
        public float minRockSize = 1.5f;
        public float maxRockSize = 5f;

        [Header("Piste edge markers")]
        public float markerSpacing = 25f;

        public int seed = 777;

        System.Random _rnd;

        void Start()
        {
            Build();
        }

        float Rand(float a, float b)
        {
            return a + (float)_rnd.NextDouble() * (b - a);
        }

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

            Material bark = MaterialFactory.Create("Bark", new Color(0.24f, 0.17f, 0.12f), 0.05f);
            Material needles = MaterialFactory.Create("Needles", new Color(0.10f, 0.24f, 0.16f), 0.05f);
            Material rockMat = MaterialFactory.Create("Rock", new Color(0.36f, 0.36f, 0.39f), 0.08f);
            Material orange = MaterialFactory.Create("MarkerOrange", new Color(0.95f, 0.42f, 0.05f), 0.1f);
            Material blue = MaterialFactory.Create("MarkerBlue", new Color(0.10f, 0.35f, 0.85f), 0.1f);

            Mesh pineMesh = BuildPineMesh();
            Mesh poleMesh = BuildPoleMesh();

            SpawnTrees(container.transform, pineMesh, bark, needles);
            SpawnRocks(container.transform, rockMat);
            SpawnMarkers(container.transform, poleMesh, orange, blue);

            // Keep the generated clutter out of the saved scene file.
            foreach (Transform tr in container.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        // ---------------- trees ------------------------------------------

        static Mesh BuildPineMesh()
        {
            var verts = new List<Vector3>();
            var trunk = new List<int>();
            var leaves = new List<int>();

            // Unit tree: 1.0 tall, scaled per instance.
            PrimitiveMeshes.AddTube(verts, trunk, Vector3.zero, 0f, 0.34f, 0.055f, 0.040f, 6);
            PrimitiveMeshes.AddTube(verts, leaves, Vector3.zero, 0.16f, 0.56f, 0.23f, 0f, 8);
            PrimitiveMeshes.AddTube(verts, leaves, Vector3.zero, 0.42f, 0.80f, 0.17f, 0f, 8);
            PrimitiveMeshes.AddTube(verts, leaves, Vector3.zero, 0.66f, 1.02f, 0.11f, 0f, 8);

            return PrimitiveMeshes.BuildMesh("Pine", verts, trunk, leaves);
        }

        void SpawnTrees(Transform parent, Mesh mesh, Material bark, Material needles)
        {
            float halfW = mountain.width * 0.5f;
            var mats = new[] { bark, needles };

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

                var go = new GameObject("Pine");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, h - 0.3f, z);
                go.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Rand(minTreeHeight, maxTreeHeight);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;

                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.06f;
                cc.height = 0.9f;
                cc.center = new Vector3(0f, 0.45f, 0f);

                placed++;
            }
        }

        // ---------------- rocks ------------------------------------------

        void SpawnRocks(Transform parent, Material rockMat)
        {
            float halfW = mountain.width * 0.5f;

            for (int i = 0; i < rockCount; i++)
            {
                float x = Rand(-halfW + 8f, halfW - 8f);
                float z = Rand(10f, mountain.length - 10f);

                // A few rocks may sit near the run, but never in the middle of it.
                if (mountain.IsOnPiste(x, z, -6f)) continue;

                float sx = Rand(minRockSize, maxRockSize);
                float sy = sx * Rand(0.5f, 0.9f);
                float sz = sx * Rand(0.7f, 1.3f);

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Rock";
                Kill(go.GetComponent<SphereCollider>());

                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, mountain.SampleHeight(x, z) - sy * 0.28f, z);
                go.transform.rotation = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
                go.transform.localScale = new Vector3(sx, sy, sz);
                go.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
        }

        // ---------------- piste markers ----------------------------------

        static Mesh BuildPoleMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            PrimitiveMeshes.AddTube(verts, tris, Vector3.zero, 0f, 1.9f, 0.07f, 0.05f, 6);
            return PrimitiveMeshes.BuildMesh("Pole", verts, tris);
        }

        void SpawnMarkers(Transform parent, Mesh mesh, Material left, Material right)
        {
            if (markerSpacing < 5f) markerSpacing = 5f;

            for (float z = 25f; z < mountain.length - 25f; z += markerSpacing)
            {
                float cx = mountain.PisteCenterX(z);
                float hw = mountain.PisteHalfWidth(z);
                MakeMarker(parent, mesh, left, cx - hw - 1.5f, z);
                MakeMarker(parent, mesh, right, cx + hw + 1.5f, z);
            }
        }

        void MakeMarker(Transform parent, Mesh mesh, Material mat, float x, float z)
        {
            var go = new GameObject("PisteMarker");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, mountain.SampleHeight(x, z) - 0.2f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
