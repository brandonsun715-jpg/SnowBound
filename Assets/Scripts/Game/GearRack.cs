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
            if (lodge == null) lodge = LodgeBuilder.Instance;
            if (lodge == null) return;

            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(Point, Quaternion.Euler(0f, lodge.facingYaw, 0f));

            Material timber = MaterialFactory.Create("RackTimber", new Color(0.30f, 0.21f, 0.14f), 0.06f);
            Material ski = MaterialFactory.Create("RackSki", new Color(0.20f, 0.62f, 0.85f), 0.35f);
            Material board = MaterialFactory.Create("RackBoard", new Color(0.85f, 0.62f, 0.16f), 0.35f);

            // A-frame rack.
            for (int side = -1; side <= 1; side += 2)
                Bar(root.transform, "RackPost", new Vector3(side * 1.6f, 0.75f, 0f),
                    new Vector3(0.12f, 1.5f, 0.12f), Quaternion.identity, timber);

            Bar(root.transform, "RackRail", new Vector3(0f, 1.42f, 0f),
                new Vector3(3.4f, 0.12f, 0.12f), Quaternion.identity, timber);

            // Spare gear leaning against it, so the rack reads at a glance.
            for (int i = 0; i < 3; i++)
            {
                float x = -1.1f + i * 0.55f;
                Bar(root.transform, "RackSki", new Vector3(x, 0.95f, -0.18f),
                    new Vector3(0.11f, 1.9f, 0.05f), Quaternion.Euler(9f, 0f, 4f), ski);
            }

            Bar(root.transform, "RackBoard", new Vector3(1.15f, 0.85f, -0.2f),
                new Vector3(0.32f, 1.6f, 0.05f), Quaternion.Euler(11f, 0f, -5f), board);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        void Bar(Transform parent, string name, Vector3 local, Vector3 scale,
                 Quaternion rotation, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Kill(go.GetComponent<Collider>());
        }

        void Update()
        {
            if (!Application.isPlaying) return;

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
