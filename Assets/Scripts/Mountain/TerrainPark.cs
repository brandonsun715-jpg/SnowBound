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
        public int trailIndex = 0;
        [Tooltip("Metres to one side of that run's centre line.")]
        public float lateralOffset = 13f;
        [Tooltip("Off until the resort actually has a park. A new resort has none.")]
        public bool built = false;
        [Tooltip("How far down the run the top kicker sits, 0 summit, 1 base.")]
        [Range(0.1f, 0.9f)] public float alongTrail = 0.45f;

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

        [Header("Rail")]
        public float railZ = 273f;
        [Tooltip("How far the bottom end of the rail sits below the top one.")]
        public float railDrop = 1.4f;

        readonly GroundWatch _ground = new GroundWatch();

        void Start() { Build(); }
        void OnDisable() { _ground.Stop(); }

        void Update()
        {
            if (!Application.isPlaying) return;
            _ground.Tick();
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
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (mountain == null) return;

            Clear();

            // A park is built on a run. Without one there is nowhere to put it,
            // and on a new resort there is no run yet.
            if (!built || Run == null) return;

            PlaceOnRun();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            Material rideOn = Surfaces.Groomed;
            Material shaded = Surfaces.Packed;
            Material steel = Surfaces.Painted("ParkSteel", new Color(0.28f, 0.30f, 0.34f));
            Material slick = Surfaces.Steel;

            BuildKickers(root.transform, rideOn, shaded);
            BuildBoxes(root.transform, steel, slick);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            // Kickers and boxes sit on the run's surface. Regroom, re-carve or
            // sculpt that run and they have to be rebuilt onto it.
            _ground.Follow(mountain, Build);
            _ground.Note(Anchor, Mathf.Max(60f, kickerSpacing * kickerCount), 8);
        }

        public Trail Run
        {
            get { return mountain != null ? mountain.TrailAt(trailIndex) : null; }
        }

        /// <summary>Where along its run the park ended up. The map marker uses this.</summary>
        public Vector3 Anchor
        {
            get
            {
                Trail run = Run;
                return run != null ? run.PointAt(alongTrail) : transform.position;
            }
        }

        /// <summary>
        /// Lay the park out along whichever run it belongs to, rather than at
        /// fixed distances up the mountain. A run the player drew can start
        /// and finish anywhere.
        /// </summary>
        void PlaceOnRun()
        {
            Trail run = Run;
            if (run == null) return;

            topKickerZ = run.PointAt(alongTrail).z;
            boxZ = run.PointAt(Mathf.Max(0.05f, alongTrail - 0.14f)).z;
        }

        /// <summary>
        /// Middle of the run at this distance up the mountain. A run is a line
        /// the player drew, so this reads the line rather than assuming the
        /// run goes straight down.
        /// </summary>
        float CentreX(float z)
        {
            Trail run = Run;
            if (run == null || run.spine == null || run.spine.Count == 0) return lateralOffset;

            float best = float.MaxValue;
            float x = 0f;

            for (int i = 0; i < run.spine.Count; i++)
            {
                float d = Mathf.Abs(run.spine[i].z - z);
                if (d >= best) continue;

                best = d;
                x = run.spine[i].x;
            }

            return x + lateralOffset;
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
            float length = height * lengthPerHeight;
            float midZ = startZ - length * 0.5f;
            float centreX = CentreX(midZ);

            // A park crew grooms a pad before they build on it, and so does
            // this: level the ground, then protect it, so nothing sculpts
            // the jump out from under itself later.
            var pad = new Vector3(centreX, 0f, midZ);
            mountain.FlattenPad(pad, length * 0.7f, length * 0.9f);
            mountain.Protect(pad, length * 0.7f, name);

            if (BuildModelledKicker(root, name, centreX, midZ, length, height)) return;

            BuildShapedKicker(root, startZ, height, name, rideOn, shaded);
        }

        /// <summary>
        /// The real jump, stood on the pad and pitched to lie along it.
        ///
        /// Its collider is its own mesh, so what you ride is exactly what
        /// you see — which is the one thing a jump cannot get wrong.
        /// </summary>
        bool BuildModelledKicker(Transform root, string name, float centreX, float midZ,
                                 float length, float height)
        {
            float scale = height / HeroAssets.KickerHeight;

            GameObject model = HeroAssets.SpawnPart(
                HeroAssets.Park, "Kicker", root, Vector3.zero, Quaternion.identity,
                new Vector3(1f, scale, scale));

            if (model == null) return false;

            // Pitched to the slope it stands on, so the approach meets the
            // snow instead of stepping up onto it.
            float uphill = mountain.SampleHeight(centreX, midZ + length * 0.5f);
            float downhill = mountain.SampleHeight(centreX, midZ - length * 0.5f);
            float pitch = -Mathf.Atan2(uphill - downhill, length) * Mathf.Rad2Deg;

            model.name = name;
            model.transform.SetPositionAndRotation(
                new Vector3(centreX, (uphill + downhill) * 0.5f, midZ),
                Quaternion.Euler(pitch, 0f, 0f));

            var filter = model.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                model.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

            // Built out of snow, so it keeps leaving tracks.
            model.AddComponent<SnowSurface>();

            return true;
        }

        void BuildShapedKicker(Transform root, float startZ, float height, string name,
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
            BuildJib(root, "Flat Box", "Box", boxZ, 4.5f, 0f, boxWidth, 0.22f, steel, slick);
            BuildJib(root, "Down Box", "Box", boxZ - 16f, -4.5f, downBoxDrop, boxWidth, 0.22f,
                     steel, slick);

            // A rail is not a box: it is a tube you have to balance on, and
            // a park without one is a park with nothing to learn on.
            BuildJib(root, "Down Rail", "Rail", railZ, 0.6f, railDrop, 0.16f, 0.14f,
                     steel, slick);
        }

        /// <summary>
        /// One jib feature: a box or a rail.
        ///
        /// The slab is the collider and the thing the game reasons about —
        /// where it is, how it is tilted, that it is slick rather than snow.
        /// The model is hung on it and only ever seen, so a missing model
        /// costs the look of the feature and nothing about riding it.
        /// </summary>
        void BuildJib(Transform root, string name, string part, float z, float sideways,
                      float drop, float width, float thickness, Material steel, Material slick)
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

            GameObject slab = Boxes.Create(root, name, Vector3.zero,
                                           new Vector3(width, thickness, span), slick, true);

            slab.transform.position = (top + bottom) * 0.5f;
            slab.transform.rotation = Quaternion.LookRotation(along / span, Vector3.up);

            // Steel and plastic do not hold you back the way snow does.
            slab.AddComponent<SlickSurface>();

            GameObject model = HeroAssets.SpawnPart(
                HeroAssets.Park, part, slab.transform, Vector3.zero, Quaternion.identity,
                new Vector3(1f, 1f, span / HeroAssets.BoxLength));

            if (model != null) HeroAssets.Hide(slab.transform, model);

            // Legs, so it stands on the snow instead of floating over it.
            Leg(root, steel, top, thickness);
            Leg(root, steel, bottom, thickness);
        }

        void Leg(Transform root, Material steel, Vector3 under, float thickness)
        {
            float ground = mountain.SampleHeight(under.x, under.z);
            float underside = under.y - thickness * 0.5f;
            float height = Mathf.Max(0.2f, underside - ground);

            GameObject model = HeroAssets.SpawnPart(
                HeroAssets.Park, "Leg", root, Vector3.zero, Quaternion.identity,
                new Vector3(1f, height, 1f));

            if (model != null)
            {
                // The leg is drawn hanging a metre from its own origin, so
                // it is hung from the underside and stretched to the snow.
                model.transform.position = new Vector3(under.x, underside, under.z);
                return;
            }

            GameObject leg = Boxes.Create(root, "BoxLeg", Vector3.zero,
                                          new Vector3(boxWidth * 0.75f, height, 0.16f), steel);

            leg.transform.position = new Vector3(under.x, ground + height * 0.5f, under.z);
        }
    }
}
