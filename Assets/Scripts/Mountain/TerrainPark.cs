using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Mountain
{
    /// <summary>
    /// A snow park built onto one of the runs: a line of kickers of
    /// increasing size, with boxes above them to warm up on.
    ///
    /// The ramps are shaped in world space against the terrain underneath, so
    /// they sit on the snow however it undulates, and they are marked as snow
    /// themselves so tracks and spray carry on across them. The boxes are
    /// marked slick instead, because steel does not drag the way snow does.
    /// </summary>
    [ExecuteAlways]
    public class TerrainPark : MonoBehaviour
    {
        const string ContainerName = "GeneratedPark";

        public MountainGenerator mountain;

        [Header("Where")]
        [Tooltip("Which run the park is built on.")]
        public int pisteIndex = 0;
        [Tooltip("Metres to one side of that run's centre line.")]
        public float lateralOffset = 13f;

        [Header("Kickers")]
        public int kickerCount = 3;
        [Tooltip("Distance up the mountain of the top kicker. They descend from there.")]
        public float topKickerZ = 252f;
        public float kickerSpacing = 56f;
        public float smallestHeight = 1.5f;
        public float largestHeight = 2.9f;
        [Tooltip("Ramp length as a multiple of its height. Lower is steeper.")]
        public float lengthPerHeight = 2.6f;
        public float kickerWidth = 6f;
        [Tooltip("How sharply the ramp steepens towards the lip. 1 is a wedge.")]
        public float lipShape = 2.6f;

        [Header("Boxes")]
        public float boxZ = 305f;
        public float boxLength = 8f;
        public float boxWidth = 0.95f;
        [Tooltip("Height of the riding surface above the snow.")]
        public float boxHeight = 0.5f;
        [Tooltip("Extra drop across the length of the down box.")]
        public float downBoxDrop = 1.1f;

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
            if (mountain == null) return;

            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            Material rideOn = MaterialFactory.Create("ParkSnow", new Color(0.96f, 0.97f, 1f), 0.34f);
            Material shaded = MaterialFactory.Create("ParkSnowShade", new Color(0.72f, 0.78f, 0.90f), 0.22f);
            Material steel = MaterialFactory.Create("ParkSteel", new Color(0.30f, 0.32f, 0.36f), 0.45f, 0.5f);
            Material slick = MaterialFactory.Create("ParkBoxTop", new Color(0.66f, 0.69f, 0.74f), 0.68f, 0.3f);

            BuildKickers(root.transform, rideOn, shaded);
            BuildBoxes(root.transform, steel, slick);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        float CentreX(float z)
        {
            return mountain.PisteCenterX(pisteIndex, z) + lateralOffset;
        }

        // ---------------- kickers -----------------------------------------

        void BuildKickers(Transform root, Material rideOn, Material shaded)
        {
            for (int i = 0; i < Mathf.Max(0, kickerCount); i++)
            {
                float share = kickerCount > 1 ? i / (float)(kickerCount - 1) : 0f;

                // Smallest at the top, so you build up to the big one.
                float height = Mathf.Lerp(smallestHeight, largestHeight, share);
                float z = topKickerZ - i * kickerSpacing;

                BuildKicker(root, z, height, "Kicker " + (i + 1), rideOn, shaded);
            }
        }

        void BuildKicker(Transform root, float startZ, float height, string name,
                         Material rideOn, Material shaded)
        {
            const int rows = 14;
            const float skirt = 1.4f;

            float length = height * lengthPerHeight;
            float halfWidth = kickerWidth * 0.5f;

            var verts = new List<Vector3>();
            var top = new List<int>();
            var sides = new List<int>();

            var topLeft = new Vector3[rows + 1];
            var topRight = new Vector3[rows + 1];
            var footLeft = new Vector3[rows + 1];
            var footRight = new Vector3[rows + 1];

            for (int i = 0; i <= rows; i++)
            {
                float t = i / (float)rows;
                float z = startZ - t * length;           // downhill is falling z
                float rise = height * Mathf.Pow(t, lipShape);

                float centre = CentreX(z);
                float xl = centre - halfWidth;
                float xr = centre + halfWidth;

                float groundLeft = mountain.SampleHeight(xl, z);
                float groundRight = mountain.SampleHeight(xr, z);

                topLeft[i] = new Vector3(xl, groundLeft + rise, z);
                topRight[i] = new Vector3(xr, groundRight + rise, z);
                footLeft[i] = new Vector3(xl, groundLeft - skirt, z);
                footRight[i] = new Vector3(xr, groundRight - skirt, z);
            }

            for (int i = 0; i < rows; i++)
            {
                PrimitiveMeshes.AddQuad(verts, top, topLeft[i], topRight[i], topRight[i + 1], topLeft[i + 1]);
                PrimitiveMeshes.AddQuad(verts, sides, footLeft[i], topLeft[i], topLeft[i + 1], footLeft[i + 1]);
                PrimitiveMeshes.AddQuad(verts, sides, footRight[i], footRight[i + 1], topRight[i + 1], topRight[i]);
            }

            // The lip: a clean vertical face at the end of the ramp.
            PrimitiveMeshes.AddQuad(verts, sides,
                topLeft[rows], topRight[rows], footRight[rows], footLeft[rows]);

            Mesh mesh = PrimitiveMeshes.BuildMesh(name + "Mesh", verts, top, sides);

            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { rideOn, shaded };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Built out of snow, so it keeps leaving tracks.
            go.AddComponent<SnowSurface>();
        }

        // ---------------- boxes -------------------------------------------

        void BuildBoxes(Transform root, Material steel, Material slick)
        {
            BuildBox(root, "Flat Box", boxZ, 4.5f, 0f, steel, slick);
            BuildBox(root, "Down Box", boxZ - 16f, -4.5f, downBoxDrop, steel, slick);
        }

        void BuildBox(Transform root, string name, float z, float sideways, float drop,
                      Material steel, Material slick)
        {
            float half = boxLength * 0.5f;

            float xTop = CentreX(z + half) + sideways;
            float xBottom = CentreX(z - half) + sideways;

            var top = new Vector3(xTop, mountain.SampleHeight(xTop, z + half) + boxHeight, z + half);
            var bottom = new Vector3(xBottom,
                                     mountain.SampleHeight(xBottom, z - half) + boxHeight - drop,
                                     z - half);

            Vector3 along = bottom - top;
            float span = along.magnitude;
            if (span < 0.1f) return;

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(root, false);
            slab.transform.position = (top + bottom) * 0.5f;
            slab.transform.rotation = Quaternion.LookRotation(along / span, Vector3.up);
            slab.transform.localScale = new Vector3(boxWidth, 0.22f, span);
            slab.GetComponent<MeshRenderer>().sharedMaterial = slick;

            // Steel and plastic do not hold you back the way snow does.
            slab.AddComponent<SlickSurface>();

            // Legs, so it stands on the snow instead of floating over it.
            Leg(root, steel, top);
            Leg(root, steel, bottom);
        }

        void Leg(Transform root, Material steel, Vector3 under)
        {
            float ground = mountain.SampleHeight(under.x, under.z);
            float height = Mathf.Max(0.2f, under.y - ground);

            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "BoxLeg";
            leg.transform.SetParent(root, false);
            leg.transform.position = new Vector3(under.x, ground + height * 0.5f, under.z);
            leg.transform.localScale = new Vector3(boxWidth * 0.75f, height, 0.16f);
            leg.GetComponent<MeshRenderer>().sharedMaterial = steel;

            Kill(leg.GetComponent<Collider>());
        }
    }
}
