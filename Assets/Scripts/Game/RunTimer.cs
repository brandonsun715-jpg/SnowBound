using UnityEngine;
using SnowBound.Core;
using SnowBound.Mountain;
using SnowBound.Player;

namespace SnowBound.Game
{
    /// <summary>
    /// Start and finish areas, and the clock between them.
    ///
    /// Crossing is measured against a line up the mountain rather than with
    /// trigger volumes, so it cannot be missed at speed however wide the run
    /// gets. The timer arms itself when you are above the start gate, which
    /// means stepping off the lift and setting off always counts.
    /// </summary>
    [ExecuteAlways]
    public class RunTimer : MonoBehaviour
    {
        const string ContainerName = "GeneratedGates";

        public MountainGenerator mountain;
        public PlayerController player;

        [Header("Course")]
        [Tooltip("Which run is timed. The gates go on that run's own ends.")]
        public int trailIndex = 0;

        /// <summary>Distance up the mountain of the start gate, read off the run.</summary>
        public float startZ { get; private set; }
        /// <summary>Distance up the mountain of the finish gate.</summary>
        public float finishZ { get; private set; }

        /// <summary>False until the resort has a run to time.</summary>
        public bool HasCourse { get; private set; }

        public bool Running { get; private set; }
        public float Elapsed { get; private set; }
        public float LastTime { get; private set; } = -1f;
        public float BestTime { get; private set; } = -1f;

        bool _armed;

        readonly SnowBound.Mountain.GroundWatch _ground = new SnowBound.Mountain.GroundWatch();

        void Start() { Build(); }
        void OnDisable() { _ground.Stop(); }

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

            Running = false;
            _armed = false;

            // No run, no course. A new resort has nothing to time.
            Trail run = mountain.TrailAt(trailIndex);
            HasCourse = run != null && run.spine != null && run.spine.Count >= 2;
            if (!HasCourse) return;

            Vector3 top = run.PointAt(0.05f);
            Vector3 bottom = run.PointAt(0.95f);

            startZ = top.z;
            finishZ = bottom.z;

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            Material post = Surfaces.Galvanised;
            Material startBanner = Surfaces.Fabric("StartBanner", new Color(0.13f, 0.55f, 0.30f));
            Material finishBanner = Surfaces.Fabric("FinishBanner", new Color(0.80f, 0.16f, 0.16f));

            Gate(root.transform, top, run.halfWidth, post, startBanner);
            Gate(root.transform, bottom, run.halfWidth, post, finishBanner);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            // The gate posts are cut to the ground under each one. Regroom or
            // re-carve the run and they need cutting again.
            _ground.Follow(mountain, Build);
            _ground.Note(Rect.MinMaxRect(Mathf.Min(top.x, bottom.x) - run.halfWidth - 4f,
                                         Mathf.Min(top.z, bottom.z) - 4f,
                                         Mathf.Max(top.x, bottom.x) + run.halfWidth + 4f,
                                         Mathf.Max(top.z, bottom.z) + 4f), 6);
        }

        void Gate(Transform parent, Vector3 at, float halfWidth, Material postMat, Material bannerMat)
        {
            float z = at.z;
            float centre = at.x;
            float half = Mathf.Max(4f, halfWidth * 0.9f);
            const float height = 5.5f;

            float leftGround = mountain.SampleHeight(centre - half, z);
            float rightGround = mountain.SampleHeight(centre + half, z);
            float top = Mathf.Max(leftGround, rightGround) + height;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = centre + side * half;
                float ground = mountain.SampleHeight(x, z);
                float postHeight = top - ground;

                Piece(parent, "GatePost", new Vector3(x, ground + postHeight * 0.5f, z),
                      new Vector3(0.30f, postHeight, 0.30f), postMat);
            }

            Piece(parent, "GateBanner", new Vector3(centre, top - 0.7f, z),
                  new Vector3(half * 2f, 1.4f, 0.18f), bannerMat);
        }

        void Piece(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = Boxes.Create(parent, name, Vector3.zero, scale, mat);
            go.transform.position = position;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            _ground.Tick();

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null || !HasCourse) return;

            // The lift carries you back up through both gates. That is not a run.
            if (player.IsRiding)
            {
                Running = false;
                _armed = false;
                return;
            }

            float z = player.transform.position.z;

            if (!Running)
            {
                if (z > startZ) { _armed = true; return; }

                if (_armed && player.IsRidingSnow)
                {
                    Running = true;
                    Elapsed = 0f;
                    _armed = false;
                }
                return;
            }

            Elapsed += Time.deltaTime;

            if (z <= finishZ)
            {
                Running = false;
                LastTime = Elapsed;
                if (BestTime < 0f || Elapsed < BestTime) BestTime = Elapsed;
            }
        }

        /// <summary>m:ss.hh, or a dash when there is no time yet.</summary>
        public static string Format(float seconds)
        {
            if (seconds < 0f) return "--:--";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return string.Format("{0}:{1:00.00}", minutes, rest);
        }
    }
}
