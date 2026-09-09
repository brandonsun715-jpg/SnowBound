using UnityEngine;
using SnowBound.Core;
using SnowBound.Buildings;
using SnowBound.Player;

namespace SnowBound.Game
{
    /// <summary>
    /// The rack outside the lodge. Gear can only be swapped standing here,
    /// which is the rule that turns a pile of systems into a loop: you have
    /// to come back to the lodge to change what is on your feet.
    ///
    /// Set PlayerController.allowGearKeysAnywhere while testing to ignore it.
    /// </summary>
    [ExecuteAlways]
    public class GearRack : MonoBehaviour
    {
        const string ContainerName = "GeneratedRack";

        public LodgeBuilder lodge;
        public PlayerController player;

        [Tooltip("How close you have to stand to change gear.")]
        public float radius = 7f;
        [Tooltip("Metres to the side of the lodge door.")]
        public float sideOffset = 5.5f;

        public bool PlayerInRange { get; private set; }

        public Vector3 Point
        {
            get
            {
                if (lodge == null) lodge = LodgeBuilder.Instance;
                if (lodge == null) return transform.position;

                Vector3 across = Quaternion.Euler(0f, lodge.facingYaw, 0f) * Vector3.right;
                return lodge.EntrancePosition + across * sideOffset;
            }
        }

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
            if (lodge == null) lodge = LodgeBuilder.Instance;
            if (lodge == null) return;

            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(Point, Quaternion.Euler(0f, lodge.facingYaw, 0f));

            Material timber = Surfaces.DarkTimber;
            Material ski = Surfaces.Painted("Ski", new Color(0.20f, 0.62f, 0.85f));
            Material board = Surfaces.Painted("Board", new Color(0.85f, 0.62f, 0.16f));

            // A-frame rack.
            for (int side = -1; side <= 1; side += 2)
                Bar(root.transform, "RackPost", new Vector3(side * 1.6f, 0.75f, 0f),
                    new Vector3(0.12f, 1.5f, 0.12f), Quaternion.identity, timber);

            Bar(root.transform, "RackRail", new Vector3(0f, 1.42f, 0f),
                new Vector3(3.4f, 0.12f, 0.12f), Quaternion.identity, timber);

            // Spare gear leaning against it, so the rack reads at a glance.
            // Stood on their tails against the rail: a ski is tipped back
            // until it is nearly upright, which puts its own centre a little
            // under half its length off the ground.
            for (int i = 0; i < 3; i++)
            {
                float x = -1.1f + i * 0.55f;
                var lean = Quaternion.Euler(-80f, 0f, 4f + i * 2f);

                if (HeroAssets.Spawn(HeroAssets.Ski, root.transform,
                                     new Vector3(x, 0.86f, -0.18f), lean) == null)
                    Bar(root.transform, "RackSki", new Vector3(x, 0.95f, -0.18f),
                        new Vector3(0.11f, 1.9f, 0.05f), Quaternion.Euler(9f, 0f, 4f), ski);
            }

            if (HeroAssets.Spawn(HeroAssets.Board, root.transform,
                                 new Vector3(1.15f, 0.78f, -0.20f),
                                 Quaternion.Euler(-79f, 0f, -6f)) == null)
                Bar(root.transform, "RackBoard", new Vector3(1.15f, 0.85f, -0.2f),
                    new Vector3(0.32f, 1.6f, 0.05f), Quaternion.Euler(11f, 0f, -5f), board);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            // It stands on the lodge's pad, and the pad is only where it is
            // until something sculpts it.
            _ground.Follow(SnowBound.Mountain.MountainGenerator.Instance, Build);
            _ground.Note(Point, 8f, 4);
        }

        void Bar(Transform parent, string name, Vector3 local, Vector3 scale,
                 Quaternion rotation, Material mat)
        {
            GameObject go = Boxes.Create(parent, name, local, scale, mat);
            go.transform.localRotation = rotation;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            _ground.Tick();

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null || player.IsRiding) { PlayerInRange = false; return; }

            Vector3 offset = player.transform.position - Point;
            offset.y = 0f;
            PlayerInRange = offset.magnitude <= radius;
            if (!PlayerInRange) return;

            int gear = player.Input.GearPressed;
            if (gear > 0) player.SetMode((LocomotionKind)gear);
        }
    }
}
