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
        [Tooltip("Distance up the mountain of the start gate.")]
        public float startZ = 380f;
        [Tooltip("Distance up the mountain of the finish gate.")]
        public float finishZ = 60f;

        public bool Running { get; private set; }
        public float Elapsed { get; private set; }
        public float LastTime { get; private set; } = -1f;
        public float BestTime { get; private set; } = -1f;

        bool _armed;

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

            Material post = MaterialFactory.Create("GatePost", new Color(0.22f, 0.23f, 0.26f), 0.2f);
            Material startBanner = MaterialFactory.Create("StartBanner", new Color(0.13f, 0.55f, 0.30f), 0.2f);
            Material finishBanner = MaterialFactory.Create("FinishBanner", new Color(0.80f, 0.16f, 0.16f), 0.2f);

            Gate(root.transform, startZ, post, startBanner);
            Gate(root.transform, finishZ, post, finishBanner);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        void Gate(Transform parent, float z, Material postMat, Material bannerMat)
        {
            float centre = mountain.PisteCenterX(z);
            float half = mountain.PisteHalfWidth(z) * 0.6f;
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Kill(go.GetComponent<Collider>());
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

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
