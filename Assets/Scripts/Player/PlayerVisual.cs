using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Player
{
    /// <summary>
    /// The rider: a modelled skier and a modelled snowboarder, and a body
    /// built from primitives if neither is in the project.
    ///
    /// Nothing here has a collider and nothing here drives gameplay. What it
    /// does own is the pose, and there are only two poses the game asks for:
    /// turn the shoulders across the board, and sit down on the chairlift.
    /// Both are done by turning named parts of the model, which is why the
    /// riders are exported as a rig of fifteen parts rather than one mesh.
    ///
    /// A skier and a snowboarder are two models rather than one with
    /// different gear, because they do not stand the same way. A boarder's
    /// feet are strapped fore and aft to a board and their shoulders are
    /// turned across it; a skier's are side by side and square. That is the
    /// difference you read at a glance from the chairlift.
    /// </summary>
    [ExecuteAlways]
    public class PlayerVisual : MonoBehaviour
    {
        const string ContainerName = "GeneratedBody";

        [Header("Colours")]
        [Tooltip("Used by the fallback body only. The models carry their own\nkit, baked into their textures.")]
        public Color jacket = new Color(0.85f, 0.26f, 0.14f);
        public Color trousers = new Color(0.13f, 0.16f, 0.26f);
        public Color helmet = new Color(0.12f, 0.13f, 0.16f);
        public Color gear = new Color(0.20f, 0.62f, 0.85f);

        [Header("Standing on gear")]
        [Tooltip("How far the boots sit above the snow once they are in a\nbinding. Skis carry the rider higher than a board does.")]
        public float skiLift = 0.05f;
        public float boardLift = 0.03f;
        [Tooltip("Metres either side of the middle for a skier's feet.")]
        public float trackWidth = 0.14f;

        [Header("Riding")]
        [Tooltip("Degrees of lean per metre a second of sideways slip. A rider\nleans into a turn against the force throwing them out of it.")]
        public float leanPerSlip = 2.4f;
        public float maxLean = 28f;
        [Tooltip("Speed at which the rider is fully folded up over their skis.")]
        public float fastSpeed = 22f;
        [Tooltip("Metres the hips drop at full speed.")]
        public float crouchDepth = 0.16f;
        [Tooltip("How quickly the stance follows the ride. Lower is heavier.")]
        public float stanceResponse = 7f;

        Rider _skier;
        Rider _boarder;
        Rider _plain;
        Rider _shown;

        Transform _skis;
        Transform _board;
        readonly Transform[] _poles = new Transform[2];

        // Remembered so the state survives a rebuild, whatever order the
        // player's components happen to start in.
        LocomotionKind _shownGear = LocomotionKind.Walk;
        float _bodyYaw;
        bool _seated;

        float _lean;
        float _crouch;
        float _tuck;

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

            _skier = _boarder = _plain = _shown = null;
            _skis = _board = null;
            _poles[0] = _poles[1] = null;
        }

        [ContextMenu("Build Now")]
        public void Build()
        {
            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            _skier = Model(HeroAssets.RiderSki, skiLift);
            _boarder = Model(HeroAssets.RiderBoard, boardLift);

            // Only if a model is missing. The boxes are still a rider: they
            // are built round the same joints, so everything below poses
            // them without knowing which it has.
            if (_skier == null || _boarder == null) _plain = Primitives(root.transform);

            BuildSkis(root.transform);
            BuildBoard(root.transform);

            ShowGear(_shownGear);
            SetBodyYawOffset(_bodyYaw);
            SetSeated(_seated);

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        Rider Model(string folder, float lift)
        {
            Transform root = transform.Find(ContainerName);
            GameObject go = HeroAssets.Spawn(folder, root, new Vector3(0f, lift, 0f),
                                             Quaternion.identity);

            return go == null ? null : Rider.Rig(go.transform);
        }

        /// <summary>Which rider is on their feet for this gear.</summary>
        Rider For(LocomotionKind kind)
        {
            if (kind == LocomotionKind.Snowboard) return _boarder ?? _plain;
            return _skier ?? _plain;
        }

        /// <summary>Swap what is strapped to the rider's feet, and who is wearing it.</summary>
        public void ShowGear(LocomotionKind kind)
        {
            _shownGear = kind;

            Rider wanted = For(kind);

            foreach (Rider rider in new[] { _skier, _boarder, _plain })
                if (rider != null) rider.Show(rider == wanted);

            _shown = wanted;

            if (_skis != null) _skis.gameObject.SetActive(kind == LocomotionKind.Ski);
            if (_board != null) _board.gameObject.SetActive(kind == LocomotionKind.Snowboard);

            // The poles are held in the rider's hands, so they hang off the
            // model rather than off the ski group and have to be switched
            // separately.
            foreach (Transform pole in _poles)
                if (pole != null) pole.gameObject.SetActive(kind == LocomotionKind.Ski);

            SetBodyYawOffset(_bodyYaw);
            SetSeated(_seated);
        }

        /// <summary>
        /// Turn the rider's shoulders away from the direction of travel.
        ///
        /// The shoulders, not the whole body: a snowboarder's feet are
        /// strapped to the board and cannot turn with them. That is what
        /// makes the pose read as riding rather than as standing sideways.
        /// </summary>
        public void SetBodyYawOffset(float degrees)
        {
            _bodyYaw = degrees;
            if (_shown != null) _shown.Yaw(degrees);
        }

        /// <summary>
        /// Sit the rider down for the chairlift, or stand them back up.
        ///
        /// The gear goes with the feet. A board left standing on the snow
        /// under a rider who is now two metres above it is the single most
        /// obvious thing that can go wrong on a lift.
        /// </summary>
        public void SetSeated(bool seated)
        {
            _seated = seated;
            if (_shown != null) _shown.Seat(seated);

            foreach (Transform gear in new[] { _skis, _board })
            {
                if (gear == null) continue;

                gear.localPosition = seated ? new Vector3(0f, -0.28f, 0.30f) : Vector3.zero;
                gear.localRotation = seated ? Quaternion.Euler(-16f, 0f, 0f) : Quaternion.identity;
            }
        }

        /// <summary>
        /// Stand the rider the way the ride is actually going.
        ///
        /// Everything here is read off what the body is doing rather than
        /// off the keyboard — the same rule the spray and the audio follow —
        /// so a rider washing out sideways leans and folds whether or not
        /// anybody is holding a key down.
        ///
        /// Three numbers do all of it. Lean is the sideways slip: a rider
        /// leans into a turn against the force throwing them out of it, and
        /// that lean is the single thing that makes riding read as riding.
        /// Crouch is speed: fast is low. Tuck is being in the air, where the
        /// legs come up.
        /// </summary>
        public void SetRide(float speed, float slip, bool grounded, bool onSnow)
        {
            // Sitting on a chairlift is a pose of its own and outranks this
            // one. The controller does not call here while riding, but a
            // rider frozen half in a chair is not worth the risk.
            if (!Application.isPlaying || _shown == null || _seated) return;

            float lean = 0f;
            float crouch = 0f;
            float tuck = 0f;

            if (onSnow && !_seated)
            {
                lean = Mathf.Clamp(-slip * leanPerSlip, -maxLean, maxLean);
                crouch = Mathf.Clamp01(speed / Mathf.Max(1f, fastSpeed));
                tuck = grounded ? 0f : 1f;
            }

            float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, stanceResponse) * Time.deltaTime);

            _lean = Mathf.Lerp(_lean, lean, k);
            _crouch = Mathf.Lerp(_crouch, crouch, k);
            _tuck = Mathf.Lerp(_tuck, tuck, k);

            _shown.Ride(_lean, _crouch * crouchDepth, _crouch, _tuck);
        }

        /// <summary>
        /// Skis, and poles put in the rider's hands.
        ///
        /// The models are built at true size, tip forward and base on the
        /// snow, so they need positioning and no scaling at all.
        /// </summary>
        void BuildSkis(Transform root)
        {
            var skis = new GameObject("Skis");
            skis.transform.SetParent(root, false);
            _skis = skis.transform;

            Material gearMat = Surfaces.Painted("PlayerGear", gear);
            Material poleMat = Surfaces.Painted("PlayerHelmet", helmet);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var at = new Vector3(side * trackWidth, 0f, 0f);

                if (HeroAssets.Spawn(HeroAssets.Ski, _skis, at, Quaternion.identity) == null)
                    Part(_skis, PrimitiveType.Cube, "Ski",
                         new Vector3(side * trackWidth, 0.03f, 0.20f),
                         new Vector3(0.12f, 0.05f, 1.75f), gearMat);

                _poles[i] = Pole(side, poleMat);
            }
        }

        /// <summary>
        /// A pole hung off the hand that holds it: the grip lands in the
        /// fist and the shaft rakes back, so it swings with the arm instead
        /// of floating beside the rider.
        /// </summary>
        Transform Pole(float side, Material poleMat)
        {
            Rider holder = _skier ?? _plain;
            Transform hand = holder == null ? null : holder.Hand(side);

            if (hand == null)
            {
                GameObject box = Part(_skis, PrimitiveType.Cube, "Pole",
                                      new Vector3(side * 0.44f, 0.62f, -0.08f),
                                      new Vector3(0.045f, 1.25f, 0.045f), poleMat);
                return box.transform;
            }

            Quaternion rake = Quaternion.Euler(26f, 0f, side * 6f);
            Vector3 grip = new Vector3(0f, -0.01f, 0.05f) +
                           rake * new Vector3(0f, -(HeroAssets.PoleLength - 0.07f), 0f);

            GameObject pole = HeroAssets.Spawn(HeroAssets.Pole, hand, grip, rake);

            if (pole != null) return pole.transform;

            return Part(hand, PrimitiveType.Cube, "Pole", new Vector3(0f, -0.55f, 0.05f),
                        new Vector3(0.045f, 1.25f, 0.045f), poleMat).transform;
        }

        void BuildBoard(Transform root)
        {
            var board = new GameObject("Snowboard");
            board.transform.SetParent(root, false);
            _board = board.transform;

            if (HeroAssets.Spawn(HeroAssets.Board, _board, new Vector3(0f, 0f, 0.02f),
                                 Quaternion.identity) != null) return;

            Part(_board, PrimitiveType.Cube, "Board",
                 new Vector3(0f, 0.03f, 0.05f), new Vector3(0.34f, 0.05f, 1.55f),
                 Surfaces.Painted("PlayerGear", gear));
        }

        /// <summary>
        /// The fallback body, built round the same joints the models are.
        ///
        /// Each leg hangs off a hip rather than being a capsule sitting in
        /// space, so the seated pose that bends a model's knee bends this
        /// one too and there is only one piece of posing code.
        /// </summary>
        Rider Primitives(Transform root)
        {
            var body = new GameObject("PlainRider");
            body.transform.SetParent(root, false);

            Material jacketMat = Surfaces.Fabric("PlayerJacket", jacket);
            Material trouserMat = Surfaces.Fabric("PlayerTrousers", trousers);
            Material helmetMat = Surfaces.Painted("PlayerHelmet", helmet);
            Material gearMat = Surfaces.Painted("PlayerGear", gear);

            var hips = new GameObject("Hips");
            hips.transform.SetParent(body.transform, false);

            var torso = new GameObject("Torso");
            torso.transform.SetParent(hips.transform, false);

            var rider = new Rider { root = body.transform, hips = hips.transform,
                                    torso = torso.transform };

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                var thigh = new GameObject(i == 0 ? "ThighLeft" : "ThighRight");
                thigh.transform.SetParent(hips.transform, false);
                thigh.transform.localPosition = new Vector3(side * 0.13f, 0.88f, 0f);

                Part(thigh.transform, PrimitiveType.Capsule, "Leg",
                     new Vector3(0f, -0.44f, 0f), new Vector3(0.26f, 0.42f, 0.26f), trouserMat);

                rider.thighs[i] = thigh.transform;

                var hand = new GameObject(i == 0 ? "HandLeft" : "HandRight");
                hand.transform.SetParent(torso.transform, false);
                hand.transform.localPosition = new Vector3(side * 0.34f, 1.06f, 0f);
                rider.hands[i] = hand.transform;

                Part(hand.transform, PrimitiveType.Capsule, "Arm",
                     new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.28f, 0.18f), jacketMat);
            }

            Part(torso.transform, PrimitiveType.Capsule, "Torso",
                 new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.34f, 0.42f), jacketMat);

            Part(torso.transform, PrimitiveType.Cube, "Backpack",
                 new Vector3(0f, 1.20f, -0.26f), new Vector3(0.40f, 0.46f, 0.20f), gearMat);

            Part(torso.transform, PrimitiveType.Sphere, "Head",
                 new Vector3(0f, 1.66f, 0f), new Vector3(0.32f, 0.34f, 0.32f), helmetMat);

            Part(torso.transform, PrimitiveType.Cube, "Goggles",
                 new Vector3(0f, 1.70f, 0.13f), new Vector3(0.28f, 0.09f, 0.08f), gearMat);

            rider.Remember();
            return rider;
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

        /// <summary>
        /// One rider and the two things the game asks of them.
        ///
        /// It holds the parts by the joint they turn about and remembers
        /// where they started, because standing up again is putting them
        /// back rather than working out a second set of numbers.
        /// </summary>
        class Rider
        {
            public Transform root;
            public Transform hips;
            public Transform torso;
            public readonly Transform[] thighs = new Transform[2];
            public readonly Transform[] shins = new Transform[2];
            public readonly Transform[] hands = new Transform[2];

            Vector3 _hipsHome;
            readonly Vector3[] _thighHome = new Vector3[2];

            /// <summary>Pick a rig out of a model by the names its parts were exported with.</summary>
            public static Rider Rig(Transform model)
            {
                var rider = new Rider
                {
                    root = model,
                    hips = Find(model, "Hips"),
                    torso = Find(model, "Torso")
                };

                for (int i = 0; i < 2; i++)
                {
                    string side = i == 0 ? "Left" : "Right";
                    rider.thighs[i] = Find(model, "Thigh" + side);
                    rider.shins[i] = Find(model, "Shin" + side);
                    rider.hands[i] = Find(model, "Hand" + side);
                }

                rider.Remember();
                return rider;
            }

            static Transform Find(Transform root, string name)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;

                return null;
            }

            public void Remember()
            {
                if (hips != null) _hipsHome = hips.localPosition;

                for (int i = 0; i < 2; i++)
                    if (thighs[i] != null) _thighHome[i] = thighs[i].localPosition;
            }

            public Transform Hand(float side) { return hands[side < 0f ? 0 : 1]; }

            public void Show(bool on)
            {
                if (root != null) root.gameObject.SetActive(on);
            }

            float _yaw;

            public void Yaw(float degrees)
            {
                _yaw = degrees;
                if (torso != null) torso.localRotation = Quaternion.Euler(0f, degrees, 0f);
            }

            /// <summary>
            /// Lean, fold and tuck. The torso keeps whatever yaw the gear
            /// asked for and rolls on top of it, so a snowboarder's shoulders
            /// stay across the board while they lean into a turn.
            /// </summary>
            public void Ride(float lean, float drop, float fold, float tuck)
            {
                if (torso != null)
                    torso.localRotation = Quaternion.Euler(fold * 9f, _yaw, lean);

                if (hips != null)
                    hips.localPosition = _hipsHome - new Vector3(0f, drop, 0f);

                float knee = fold * 16f + tuck * 26f;

                for (int i = 0; i < 2; i++)
                {
                    if (thighs[i] != null)
                        thighs[i].localRotation = Quaternion.Euler(-knee, 0f, lean * 0.25f);

                    if (shins[i] != null)
                        shins[i].localRotation = Quaternion.Euler(knee * 1.35f, 0f, 0f);
                }
            }

            /// <summary>
            /// Sit down: drop the hips onto the seat, bring the feet
            /// together, swing the thighs forward and let the shins hang.
            ///
            /// Bringing the feet together matters for the snowboarder,
            /// whose feet are half a metre apart along the board and would
            /// otherwise straddle the chair.
            /// </summary>
            public void Seat(bool seated)
            {
                // Sitting down overrides the riding stance, and standing back
                // up hands it back.
                if (hips != null)
                    hips.localPosition = seated
                        ? _hipsHome + new Vector3(0f, -0.30f, 0.04f)
                        : _hipsHome;

                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;

                    if (thighs[i] != null)
                    {
                        thighs[i].localPosition = seated
                            ? new Vector3(side * 0.13f, _thighHome[i].y, 0f)
                            : _thighHome[i];

                        thighs[i].localRotation = seated
                            ? Quaternion.Euler(-70f, 0f, 0f) : Quaternion.identity;
                    }

                    if (shins[i] != null)
                        shins[i].localRotation = seated
                            ? Quaternion.Euler(62f, 0f, 0f) : Quaternion.identity;
                }
            }
        }
    }
}
