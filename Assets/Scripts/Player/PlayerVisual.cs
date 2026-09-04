using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Player
{
    /// <summary>
    /// The placeholder body: a skier built from Unity primitives. It is only
    /// ever a child of the player object and carries no colliders, so
    /// replacing it with a real rigged model later means deleting this
    /// component and dropping the model in. Nothing else changes.
    /// </summary>
    [ExecuteAlways]
    public class PlayerVisual : MonoBehaviour
    {
        const string ContainerName = "GeneratedBody";

        [Header("Colours")]
        public Color jacket = new Color(0.85f, 0.26f, 0.14f);
        public Color trousers = new Color(0.13f, 0.16f, 0.26f);
        public Color helmet = new Color(0.12f, 0.13f, 0.16f);
        public Color gear = new Color(0.20f, 0.62f, 0.85f);

        Transform _skis;
        Transform _board;

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
            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            Material jacketMat = MaterialFactory.Create("PlayerJacket", jacket, 0.15f);
            Material trouserMat = MaterialFactory.Create("PlayerTrousers", trousers, 0.15f);
            Material helmetMat = MaterialFactory.Create("PlayerHelmet", helmet, 0.35f);
            Material gearMat = MaterialFactory.Create("PlayerGear", gear, 0.35f);

            // Legs.
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root.transform, PrimitiveType.Capsule, "Leg",
                     new Vector3(side * 0.13f, 0.44f, 0f),
                     new Vector3(0.26f, 0.42f, 0.26f), trouserMat);
            }

            Part(root.transform, PrimitiveType.Capsule, "Torso",
                 new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.34f, 0.42f), jacketMat);

            // Arms.
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root.transform, PrimitiveType.Capsule, "Arm",
                     new Vector3(side * 0.34f, 1.20f, 0f),
                     new Vector3(0.18f, 0.28f, 0.18f), jacketMat);
            }

            Part(root.transform, PrimitiveType.Cube, "Backpack",
                 new Vector3(0f, 1.20f, -0.26f), new Vector3(0.40f, 0.46f, 0.20f), gearMat);

            Part(root.transform, PrimitiveType.Sphere, "Head",
                 new Vector3(0f, 1.66f, 0f), new Vector3(0.32f, 0.34f, 0.32f), helmetMat);

            Part(root.transform, PrimitiveType.Cube, "Goggles",
                 new Vector3(0f, 1.70f, 0.13f), new Vector3(0.28f, 0.09f, 0.08f), gearMat);

            // Gear, hidden until the player puts it on.
            var skis = new GameObject("Skis");
            skis.transform.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(skis.transform, PrimitiveType.Cube, "Ski",
                     new Vector3(side * 0.15f, 0.03f, 0.20f),
                     new Vector3(0.12f, 0.05f, 1.75f), gearMat);
            }
            _skis = skis.transform;

            var board = new GameObject("Snowboard");
            board.transform.SetParent(root.transform, false);
            Part(board.transform, PrimitiveType.Cube, "Board",
                 new Vector3(0f, 0.03f, 0.05f), new Vector3(0.34f, 0.05f, 1.55f), gearMat);
            _board = board.transform;

            ShowGear(LocomotionKind.Walk);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        /// <summary>Swap what is strapped to the player's feet.</summary>
        public void ShowGear(LocomotionKind kind)
        {
            if (_skis == null || _board == null) return;
            _skis.gameObject.SetActive(kind == LocomotionKind.Ski);
            _board.gameObject.SetActive(kind == LocomotionKind.Snowboard);
        }

        void Part(Transform parent, PrimitiveType shape, string name,
                  Vector3 localPosition, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // The CharacterController capsule is the only collider the player needs.
            Kill(go.GetComponent<Collider>());
        }
    }
}
