using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Mountain;

namespace SnowBound.Buildings
{
    /// <summary>
    /// Builds the base-area lodge out of Unity primitives and one procedural
    /// roof mesh. It asks MountainGenerator how high the ground is under its
    /// footprint, then sits on a stone plinth deep enough to hide the bumps.
    ///
    /// EntrancePosition is the spot on the deck where the player will spawn
    /// and later swap between boots, skis and snowboard.
    ///
    /// Keep this GameObject at (0,0,0); move the lodge with Position X / Z.
    /// </summary>
    [ExecuteAlways]
    public class LodgeBuilder : MonoBehaviour
    {
        const string ContainerName = "GeneratedLodge";

        [Tooltip("Leave empty to find the MountainGenerator automatically.")]
        public MountainGenerator mountain;

        [Header("Placement (world metres)")]
        public float positionX = -22f;
        public float positionZ = 27f;
        [Tooltip("Degrees around Y. Turns the front of the lodge towards the ski run.")]
        public float facingYaw = 70f;

        [Header("Building")]
        public float width = 22f;
        public float depth = 13f;
        public float wallHeight = 7.5f;
        public float roofHeight = 5f;
        public float roofOverhang = 1.6f;
        [Tooltip("How far the stone base lifts the building above the snow.")]
        public float plinthHeight = 1.4f;

        [Header("Deck")]
        public float deckDepth = 6f;
        public float deckHeight = 1f;
        public float rampLength = 3.6f;

        // ---------------------------------------------------------------

        static LodgeBuilder _instance;

        public static LodgeBuilder Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<LodgeBuilder>();
                return _instance;
            }
        }

        Transform _entrance;

        /// <summary>Front of the lodge, on the deck. Player spawn / gear swap.</summary>
        public Vector3 EntrancePosition
        {
            get
            {
                if (_entrance != null) return _entrance.position;

                // Same spot, worked out without needing the lodge to be built yet.
                var m = mountain != null ? mountain : MountainGenerator.Instance;
                Vector3 offset = Quaternion.Euler(0f, facingYaw, 0f) *
                                 new Vector3(0f, 0f, depth * 0.5f + deckDepth * 0.5f);
                float x = positionX + offset.x;
                float z = positionZ + offset.z;
                float y = m != null ? m.SampleHeight(x, z) + deckHeight : deckHeight;
                return new Vector3(x, y, z);
            }
        }

        void OnEnable() { _instance = this; }

        void Start() { Build(); }

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
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (mountain == null)
            {
                Debug.LogError("[LodgeBuilder] No MountainGenerator in the scene.", this);
                return;
            }

            Clear();

            Quaternion rot = Quaternion.Euler(0f, facingYaw, 0f);

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(positionX, LowestGround(rot), positionZ);
            root.transform.localRotation = rot;

            Material stone = MaterialFactory.Create("LodgeStone", new Color(0.34f, 0.33f, 0.32f), 0.08f);
            Material wood = MaterialFactory.Create("LodgeWood", new Color(0.44f, 0.29f, 0.19f), 0.06f);
            Material darkWood = MaterialFactory.Create("LodgeTimber", new Color(0.18f, 0.12f, 0.08f), 0.06f);
            Material deckWood = MaterialFactory.Create("LodgeDeck", new Color(0.36f, 0.25f, 0.17f), 0.06f);
            Material roofMat = MaterialFactory.Create("LodgeRoof", new Color(0.16f, 0.15f, 0.18f), 0.10f);
            Material snowMat = MaterialFactory.Create("LodgeRoofSnow", new Color(0.95f, 0.96f, 1f), 0.30f);
            Material glass = MaterialFactory.CreateEmissive("LodgeWindow",
                                 new Color(0.95f, 0.75f, 0.42f), new Color(1f, 0.72f, 0.32f) * 2.2f);
            Material lampGlow = MaterialFactory.CreateEmissive("LodgeLamp",
                                 new Color(1f, 0.90f, 0.70f), new Color(1f, 0.85f, 0.60f) * 3f);

            BuildShell(root.transform, stone, wood, darkWood, roofMat, snowMat);
            BuildFrontage(root.transform, darkWood, glass);
            BuildDeck(root.transform, deckWood, darkWood, lampGlow);

            var entrance = new GameObject("EntrancePoint");
            entrance.transform.SetParent(root.transform, false);
            entrance.transform.localPosition = new Vector3(0f, deckHeight + 0.1f, depth * 0.5f + deckDepth * 0.5f);
            _entrance = entrance.transform;

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        /// <summary>
        /// Lowest ground under the whole footprint. Building off the lowest
        /// point means the terrain never pokes up through the floor.
        /// </summary>
        float LowestGround(Quaternion rot)
        {
            float min = float.MaxValue;
            float halfW = width * 0.5f + 1f;
            float back = -(depth * 0.5f + 1f);
            float front = depth * 0.5f + deckDepth + rampLength;

            for (int ix = 0; ix <= 4; ix++)
            {
                float lx = Mathf.Lerp(-halfW, halfW, ix / 4f);
                for (int iz = 0; iz <= 4; iz++)
                {
                    float lz = Mathf.Lerp(back, front, iz / 4f);
                    Vector3 w = rot * new Vector3(lx, 0f, lz);
                    float h = mountain.SampleHeight(positionX + w.x, positionZ + w.z);
                    if (h < min) min = h;
                }
            }

            return min;
        }

        // ---------------- parts ------------------------------------------

        void BuildShell(Transform root, Material stone, Material wood,
                        Material darkWood, Material roofMat, Material snowMat)
        {
            float foundationHeight = 8f + plinthHeight;
            Box(root, "Foundation",
                new Vector3(0f, (-8f + plinthHeight) * 0.5f, 0f),
                new Vector3(width + 1.2f, foundationHeight, depth + 1.2f), stone, true);

            Box(root, "Walls",
                new Vector3(0f, plinthHeight + wallHeight * 0.5f, 0f),
                new Vector3(width, wallHeight, depth), wood, true);

            // Timber band between the two storeys.
            Box(root, "Trim",
                new Vector3(0f, plinthHeight + wallHeight * 0.52f, 0f),
                new Vector3(width + 0.35f, 0.45f, depth + 0.35f), darkWood, false);

            float roofBase = plinthHeight + wallHeight;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            PrimitiveMeshes.AddPrism(verts, tris, Vector3.zero,
                                     depth * 0.5f + roofOverhang, roofHeight, width + roofOverhang * 2f);
            Mesh roofMesh = PrimitiveMeshes.BuildMesh("LodgeRoofMesh", verts, tris);

            // The prism's ridge runs along its own Z, so turn it a quarter turn
            // to lie along the long axis of the building.
            var roof = new GameObject("Roof");
            roof.transform.SetParent(root, false);
            roof.transform.localPosition = new Vector3(0f, roofBase, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
            roof.AddComponent<MeshCollider>().sharedMesh = roofMesh;

            var roofSnow = new GameObject("RoofSnow");
            roofSnow.transform.SetParent(root, false);
            roofSnow.transform.localPosition = new Vector3(0f, roofBase + 0.18f, 0f);
            roofSnow.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            roofSnow.transform.localScale = new Vector3(1.015f, 1f, 1.01f);
            roofSnow.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            roofSnow.AddComponent<MeshRenderer>().sharedMaterial = snowMat;

            Box(root, "Chimney",
                new Vector3(-width * 0.30f, roofBase + roofHeight * 0.55f, depth * 0.15f),
                new Vector3(1.5f, roofHeight + 2.4f, 1.5f), stone, false);
        }

        void BuildFrontage(Transform root, Material darkWood, Material glass)
        {
            float face = depth * 0.5f;

            Box(root, "Door",
                new Vector3(0f, plinthHeight + 1.25f, face + 0.06f),
                new Vector3(2.4f, 2.5f, 0.12f), darkWood, false);

            const int perRow = 4;
            for (int row = 0; row < 2; row++)
            {
                float y = plinthHeight + (row == 0 ? 2.0f : 5.3f);
                for (int i = 0; i < perRow; i++)
                {
                    float x = Mathf.Lerp(-width * 0.36f, width * 0.36f, i / (float)(perRow - 1));
                    Box(root, "WindowFrame", new Vector3(x, y, face + 0.05f),
                        new Vector3(2.4f, 1.8f, 0.10f), darkWood, false);
                    Box(root, "Window", new Vector3(x, y, face + 0.07f),
                        new Vector3(2.1f, 1.5f, 0.14f), glass, false);
                }
            }
        }

        void BuildDeck(Transform root, Material deckWood, Material darkWood, Material lampGlow)
        {
            float deckCenterZ = depth * 0.5f + deckDepth * 0.5f;
            float deckWidth = width * 0.85f;

            Box(root, "Deck",
                new Vector3(0f, (-8f + deckHeight) * 0.5f, deckCenterZ),
                new Vector3(deckWidth, 8f + deckHeight, deckDepth), deckWood, true);

            // Side railings only; the front stays open so you can walk out.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * deckWidth * 0.47f;

                for (int i = 0; i < 4; i++)
                {
                    float z = Mathf.Lerp(depth * 0.5f + 0.3f, depth * 0.5f + deckDepth - 0.2f, i / 3f);
                    Box(root, "RailPost", new Vector3(x, deckHeight + 0.5f, z),
                        new Vector3(0.14f, 1f, 0.14f), darkWood, false);
                }

                Box(root, "RailBar", new Vector3(x, deckHeight + 0.97f, deckCenterZ),
                    new Vector3(0.16f, 0.12f, deckDepth - 0.4f), darkWood, false);
            }

            // Ramp down to the snow so the player can walk up onto the deck.
            float slope = Mathf.Sqrt(rampLength * rampLength + deckHeight * deckHeight);
            var ramp = Box(root, "Ramp", Vector3.zero, new Vector3(5f, 0.35f, slope), deckWood, true);
            ramp.transform.localRotation = Quaternion.Euler(Mathf.Atan2(deckHeight, rampLength) * Mathf.Rad2Deg, 0f, 0f);
            ramp.transform.localPosition = new Vector3(0f, deckHeight * 0.5f - 0.08f,
                                                      depth * 0.5f + deckDepth + rampLength * 0.5f);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * width * 0.44f;
                float z = depth * 0.5f + deckDepth + 1.2f;
                Box(root, "LampPost", new Vector3(x, 2f, z), new Vector3(0.22f, 4f, 0.22f), darkWood, false);
                Box(root, "LampGlow", new Vector3(x, 4.15f, z), new Vector3(0.7f, 0.7f, 0.7f), lampGlow, false);
            }
        }

        GameObject Box(Transform parent, string name, Vector3 localCenter, Vector3 size,
                       Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Thin decorative pieces would only snag the player.
            if (!keepCollider) Kill(go.GetComponent<BoxCollider>());

            return go;
        }
    }
}
