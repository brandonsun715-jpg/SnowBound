using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Player
{
    /// <summary>
    /// The placeholder body: a rider built from Unity primitives, split into
    /// a body group and one group per bit of gear. Nothing here has a
    /// collider and nothing here drives gameplay, so swapping in a real
    /// rigged model later means deleting this component and dropping the
    /// model in as a child. No other script changes.
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

        Transform _body;
        Transform _skis;
        Transform _board;
        readonly Transform[] _legs = new Transform[2];

        // Remembered so the state survives a rebuild, whatever order the
        // player's components happen to start in.
        LocomotionKind _shownGear = LocomotionKind.Walk;
        float _bodyYaw;
        bool _seated;

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

            BuildBody(root.transform, jacketMat, trouserMat, helmetMat, gearMat);
            BuildSkis(root.transform, gearMat, helmetMat);
            BuildBoard(root.transform, gearMat);

            ShowGear(_shownGear);
            SetBodyYawOffset(_bodyYaw);
            SetSeated(_seated);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        void BuildBody(Transform root, Material jacketMat, Material trouserMat,
                       Material helmetMat, Material gearMat)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(root, false);
            _body = body.transform;

            for (int side = -1; side <= 1; side += 2)
            {
                _legs[(side + 1) / 2] = Part(_body, PrimitiveType.Capsule, "Leg",
                     new Vector3(side * 0.13f, 0.44f, 0f),
                     new Vector3(0.26f, 0.42f, 0.26f), trouserMat).transform;

                Part(_body, PrimitiveType.Capsule, "Arm",
                     new Vector3(side * 0.34f, 1.20f, 0f),
                     new Vector3(0.18f, 0.28f, 0.18f), jacketMat);
            }

            Part(_body, PrimitiveType.Capsule, "Torso",
                 new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.34f, 0.42f), jacketMat);

            Part(_body, PrimitiveType.Cube, "Backpack",
                 new Vector3(0f, 1.20f, -0.26f), new Vector3(0.40f, 0.46f, 0.20f), gearMat);

            Part(_body, PrimitiveType.Sphere, "Head",
                 new Vector3(0f, 1.66f, 0f), new Vector3(0.32f, 0.34f, 0.32f), helmetMat);

            Part(_body, PrimitiveType.Cube, "Goggles",
                 new Vector3(0f, 1.70f, 0.13f), new Vector3(0.28f, 0.09f, 0.08f), gearMat);
        }

        void BuildSkis(Transform root, Material gearMat, Material poleMat)
        {
            var skis = new GameObject("Skis");
            skis.transform.SetParent(root, false);
            _skis = skis.transform;

            for (int side = -1; side <= 1; side += 2)
            {
                Part(_skis, PrimitiveType.Cube, "Ski",
                     new Vector3(side * 0.15f, 0.03f, 0.20f),
                     new Vector3(0.12f, 0.05f, 1.75f), gearMat);

                Part(_skis, PrimitiveType.Cube, "Pole",
                     new Vector3(side * 0.44f, 0.62f, -0.08f),
                     new Vector3(0.045f, 1.25f, 0.045f), poleMat);
            }
        }

        void BuildBoard(Transform root, Material gearMat)
        {
            var board = new GameObject("Snowboard");
            board.transform.SetParent(root, false);
            _board = board.transform;

            Part(_board, PrimitiveType.Cube, "Board",
                 new Vector3(0f, 0.03f, 0.05f), new Vector3(0.34f, 0.05f, 1.55f), gearMat);
        }

        /// <summary>Swap what is strapped to the rider's feet.</summary>
        public void ShowGear(LocomotionKind kind)
        {
            _shownGear = kind;
            if (_skis != null) _skis.gameObject.SetActive(kind == LocomotionKind.Ski);
            if (_board != null) _board.gameObject.SetActive(kind == LocomotionKind.Snowboard);
        }

        /// <summary>
        /// Turn the body away from the direction of travel. A snowboarder
        /// rides side-on while the board still points down the hill.
        /// </summary>
        public void SetBodyYawOffset(float degrees)
        {
            _bodyYaw = degrees;
            if (_body != null) _body.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }

        /// <summary>Sit the rider down for the chairlift, or stand them back up.</summary>
        public void SetSeated(bool seated)
        {
            _seated = seated;

            if (_body != null)
                _body.localPosition = seated ? new Vector3(0f, -0.30f, 0.04f) : Vector3.zero;

            for (int i = 0; i < _legs.Length; i++)
            {
                if (_legs[i] == null) continue;
                float side = i == 0 ? -1f : 1f;
                _legs[i].localPosition = seated
                    ? new Vector3(side * 0.13f, 0.52f, 0.18f)
                    : new Vector3(side * 0.13f, 0.44f, 0f);
                _legs[i].localRotation = seated ? Quaternion.Euler(-68f, 0f, 0f) : Quaternion.identity;
            }
        }

        GameObject Part(Transform parent, PrimitiveType shape, string name,
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

            return go;
        }
    }
}
