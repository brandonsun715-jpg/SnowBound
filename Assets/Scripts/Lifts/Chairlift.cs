using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Mountain;
using SnowBound.Player;

namespace SnowBound.Lifts
{
    /// <summary>
    /// A working fixed-grip chairlift: two terminals, towers between them,
    /// a cable loop, and chairs that never stop moving.
    ///
    /// It lays its own line out by asking the mountain where the piste is,
    /// so the lift always runs just inside the right-hand edge of the run
    /// however the terrain is retuned.
    ///
    /// Boarding is automatic. Stand in the loading area and the next free
    /// chair picks you up; the lift sets you down again at the top. That is
    /// deliberate: a prototype should not fail because you mistimed a
    /// button press.
    /// </summary>
    // Chairs must move before the rider reads the seat, or the body trails a
    // frame behind the chair and visibly judders.
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class Chairlift : MonoBehaviour
    {
        const string ContainerName = "GeneratedLift";

        [Tooltip("Leave empty to find them automatically.")]
        public MountainGenerator mountain;
        public PlayerController player;

        [Header("Line")]
        public float bottomZ = 45f;
        public float topZ = 402f;
        [Tooltip("Metres inside the right-hand edge of the piste.")]
        public float edgeInset = 6f;
        [Tooltip("Towers including both terminals.")]
        public int towerCount = 8;

        [Header("Heights")]
        public float towerHeight = 9f;
        [Tooltip("Cable height at the terminals. Low, so chairs arrive at sitting height.")]
        public float stationCableHeight = 3.4f;
        public float hangerLength = 2.1f;
        [Tooltip("Distance between the uphill and downhill cables.")]
        public float trackSpacing = 4.4f;

        [Header("Operation")]
        public float lineSpeed = 9f;
        [Tooltip("Metres between chairs. At 9 m/s, 26 m is a chair every three seconds.")]
        public float chairSpacing = 26f;
        [Tooltip("How close to the loading point you must stand.")]
        public float boardRadius = 5f;
        [Tooltip("How close a chair must be to the loading point to pick you up.")]
        public float catchWindow = 1.8f;
        public float boardLead = 6f;
        public float unloadLead = 9f;

        [Header("Seat")]
        [Tooltip("Where the rider sits relative to the seat. Negative Y puts the\nhips on the cushion and lets the legs dangle.")]
        public Vector3 seatOffset = new Vector3(0f, -0.55f, 0.02f);

        // ---------------------------------------------------------------

        static Chairlift _instance;

        public static Chairlift Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<Chairlift>();
                return _instance;
            }
        }

        /// <summary>Stand here to be picked up.</summary>
        public Vector3 BoardingPoint { get; private set; }
        public Vector3 UnloadPoint { get; private set; }
        public bool PlayerInLoadingArea { get; private set; }

        ChairliftPath _path;
        readonly List<ChairliftChair> _chairs = new List<ChairliftChair>();
        readonly List<ILiftPassenger> _queue = new List<ILiftPassenger>();
        float _travel;
        float _boardS;
        float _unloadS;

        void OnEnable() { _instance = this; }

        /// <summary>Join the queue. Idempotent, so callers can be careless.</summary>
        public void Register(ILiftPassenger passenger)
        {
            if (passenger == null || _queue.Contains(passenger)) return;
            _queue.Add(passenger);
        }

        public void Unregister(ILiftPassenger passenger)
        {
            _queue.Remove(passenger);
            ReleaseSeat(passenger);
        }

        /// <summary>
        /// Empty whatever chair this passenger is in. A guest that despawns
        /// mid-ride would otherwise leave a chair permanently occupied by a
        /// reference to something that no longer exists.
        /// </summary>
        public void ReleaseSeat(ILiftPassenger passenger)
        {
            if (passenger == null) return;

            foreach (ChairliftChair chair in _chairs)
            {
                if (chair != null && ReferenceEquals(chair.occupant, passenger)) chair.occupant = null;
            }
        }

        /// <summary>Where a passenger should stand to be picked up.</summary>
        public bool InLoadingArea(Vector3 position)
        {
            Vector3 offset = position - BoardingPoint;
            offset.y = 0f;
            return offset.magnitude <= boardRadius;
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
            _chairs.Clear();
            _path = null;

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
                Debug.LogError("[Chairlift] No MountainGenerator in the scene.", this);
                return;
            }

            Clear();

            List<Vector3> line = LayOutLine();

            _path = new ChairliftPath();
            _path.Build(line, trackSpacing);

            _boardS = boardLead;
            _unloadS = Mathf.Max(0f, _path.UpLength - unloadLead);
            BoardingPoint = _path.Sample(_boardS);
            UnloadPoint = _path.Sample(_unloadS);

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            Material steel = MaterialFactory.Create("LiftSteel", new Color(0.55f, 0.57f, 0.60f), 0.45f, 0.6f);
            Material cable = MaterialFactory.Create("LiftCable", new Color(0.18f, 0.19f, 0.21f), 0.35f);
            Material shell = MaterialFactory.Create("LiftShell", new Color(0.22f, 0.25f, 0.30f), 0.25f);
            // Slate, not red: the rider's jacket is red and the two were
            // blending into one another on the chair.
            Material chairMat = MaterialFactory.Create("LiftChair", new Color(0.22f, 0.28f, 0.38f), 0.25f);

            BuildTowers(root.transform, line, steel);
            BuildTerminals(root.transform, line, steel, shell);
            BuildCable(root.transform, cable);
            BuildChairs(root.transform, chairMat, steel);

            _travel = 0f;
            MoveChairs();

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        /// <summary>Tower tops, bottom terminal first, following the piste edge.</summary>
        List<Vector3> LayOutLine()
        {
            var line = new List<Vector3>();
            int count = Mathf.Max(2, towerCount);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float z = Mathf.Lerp(bottomZ, topZ, t);
                float x = mountain.PisteCenterX(z) + mountain.PisteHalfWidth(z) - edgeInset;
                float ground = mountain.SampleHeight(x, z);

                bool terminal = i == 0 || i == count - 1;
                line.Add(new Vector3(x, ground + (terminal ? stationCableHeight : towerHeight), z));
            }

            return line;
        }

        // ---------------- structure --------------------------------------

        void BuildTowers(Transform root, List<Vector3> line, Material steel)
        {
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 top = line[i];
                float ground = mountain.SampleHeight(top.x, top.z);
                float height = Mathf.Max(1f, top.y - ground);

                var mast = Piece(root, PrimitiveType.Cylinder, "LiftTower",
                                 new Vector3(top.x, ground + height * 0.5f, top.z),
                                 new Vector3(0.55f, height * 0.5f, 0.55f), steel, true);
                mast.transform.rotation = Quaternion.identity;

                Quaternion facing = Facing(line, i);

                var arm = Piece(root, PrimitiveType.Cube, "LiftCrossarm",
                                new Vector3(top.x, top.y + 0.25f, top.z),
                                new Vector3(trackSpacing + 1.6f, 0.28f, 0.36f), steel, false);
                arm.transform.rotation = facing;

                // Sheave trains, the little wheel packs the cable rides over.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 offset = facing * new Vector3(side * trackSpacing * 0.5f, 0f, 0f);
                    var sheave = Piece(root, PrimitiveType.Cube, "LiftSheave",
                                       top + offset + Vector3.up * 0.02f,
                                       new Vector3(0.9f, 0.34f, 0.3f), steel, false);
                    sheave.transform.rotation = facing;
                }
            }
        }

        void BuildTerminals(Transform root, List<Vector3> line, Material steel, Material shell)
        {
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : line.Count - 1;
                Vector3 node = line[i];
                Quaternion facing = Facing(line, i);
                float ground = mountain.SampleHeight(node.x, node.z);

                // The bullwheel the cable turns around: a big flat disc.
                Piece(root, PrimitiveType.Cylinder, "LiftBullwheel",
                      new Vector3(node.x, node.y, node.z),
                      new Vector3(trackSpacing, 0.14f, trackSpacing), steel, false);

                // Canopy, held clear of the loading lane so you can walk under.
                float canopyHeight = node.y + 2.4f;
                Piece(root, PrimitiveType.Cube, "LiftCanopy",
                      new Vector3(node.x, canopyHeight, node.z),
                      new Vector3(trackSpacing + 5f, 0.3f, 9f), shell, false)
                    .transform.rotation = facing;

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 offset = facing * new Vector3(sx * (trackSpacing * 0.5f + 2.2f), 0f, sz * 4f);
                        Vector3 postBase = new Vector3(node.x + offset.x, 0f, node.z + offset.z);
                        float postGround = mountain.SampleHeight(postBase.x, postBase.z);
                        float postHeight = Mathf.Max(1f, canopyHeight - postGround);

                        Piece(root, PrimitiveType.Cylinder, "LiftPost",
                              new Vector3(postBase.x, postGround + postHeight * 0.5f, postBase.z),
                              new Vector3(0.28f, postHeight * 0.5f, 0.28f), steel, true);
                    }
                }
            }
        }

        void BuildCable(Transform root, Material cableMat)
        {
            for (int i = 0; i < _path.Count; i++)
            {
                Vector3 a = _path.Node(i);
                Vector3 b = _path.Node((i + 1) % _path.Count);

                Vector3 span = b - a;
                float length = span.magnitude;
                if (length < 0.01f) continue;

                var piece = Piece(root, PrimitiveType.Cylinder, "LiftCable",
                                  a + span * 0.5f, new Vector3(0.09f, length * 0.5f, 0.09f),
                                  cableMat, false);
                piece.transform.rotation = Quaternion.FromToRotation(Vector3.up, span / length);
            }
        }

        void BuildChairs(Transform root, Material chairMat, Material steel)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt(_path.Length / Mathf.Max(4f, chairSpacing)));
            float spacing = _path.Length / count;

            var chairs = new GameObject("Chairs");
            chairs.transform.SetParent(root, false);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Chair");
                go.transform.SetParent(chairs.transform, false);

                var chair = go.AddComponent<ChairliftChair>();
                chair.loopOffset = i * spacing;

                float seatY = -hangerLength;

                Piece(go.transform, PrimitiveType.Cube, "Hanger",
                      new Vector3(0f, seatY * 0.5f, 0f),
                      new Vector3(0.13f, hangerLength, 0.13f), steel, false, true);

                Piece(go.transform, PrimitiveType.Cube, "Seat",
                      new Vector3(0f, seatY, 0.02f),
                      new Vector3(1.55f, 0.12f, 0.6f), chairMat, false, true);

                Piece(go.transform, PrimitiveType.Cube, "Backrest",
                      new Vector3(0f, seatY + 0.44f, -0.3f),
                      new Vector3(1.55f, 0.8f, 0.1f), chairMat, false, true);

                Piece(go.transform, PrimitiveType.Cube, "SafetyBar",
                      new Vector3(0f, seatY + 0.62f, 0.42f),
                      new Vector3(1.5f, 0.09f, 0.09f), steel, false, true);

                var seat = new GameObject("Seat Point");
                seat.transform.SetParent(go.transform, false);
                seat.transform.localPosition = new Vector3(0f, seatY + 0.06f, 0.02f);
                chair.seat = seat.transform;

                _chairs.Add(chair);
            }
        }

        /// <summary>Rotation whose local X runs across the line and local Z along it.</summary>
        Quaternion Facing(List<Vector3> line, int i)
        {
            Vector3 a = line[Mathf.Max(0, i - 1)];
            Vector3 b = line[Mathf.Min(line.Count - 1, i + 1)];
            Vector3 direction = b - a;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        GameObject Piece(Transform parent, PrimitiveType shape, string name,
                         Vector3 position, Vector3 scale, Material mat,
                         bool keepCollider, bool local = false)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);

            if (local) go.transform.localPosition = position;
            else go.transform.position = position;

            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            if (!keepCollider) Kill(go.GetComponent<Collider>());

            return go;
        }

        // ---------------- running ----------------------------------------

        void Update()
        {
            // Chairs are placed once at build time; only a running game moves them.
            if (!Application.isPlaying) return;
            if (_path == null || _chairs.Count == 0) return;

            _travel += lineSpeed * Time.deltaTime;
            if (_travel > _path.Length) _travel -= _path.Length;

            MoveChairs();
            RunStations();
        }

        void MoveChairs()
        {
            foreach (ChairliftChair chair in _chairs)
            {
                if (chair == null) continue;

                Vector3 tangent;
                Vector3 point = _path.Sample(chair.loopOffset + _travel, out tangent);

                tangent.y = 0f;
                chair.transform.position = point;
                if (tangent.sqrMagnitude > 0.0001f)
                    chair.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }

        void RunStations()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
                if (player != null) Register(player);
            }

            PlayerInLoadingArea = player != null && !player.IsRiding && player.IsGrounded &&
                                  InLoadingArea(player.transform.position);

            Unload();
            Board();
        }

        void Board()
        {
            foreach (ChairliftChair chair in _chairs)
            {
                if (chair == null || !chair.IsFree) continue;
                if (Mathf.Abs(_path.Gap(chair.loopOffset + _travel, _boardS)) > catchWindow) continue;

                ILiftPassenger next = NextInQueue();
                if (next == null) return;

                chair.occupant = next;
                chair.riderGear = next.Gear;
                next.BoardLift(chair.seat, seatOffset);

                if (ReferenceEquals(next, player)) PlayerInLoadingArea = false;
                return;
            }
        }

        ILiftPassenger NextInQueue()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                ILiftPassenger passenger = _queue[i];

                // Anything that has been destroyed since it queued.
                if (passenger == null || passenger.Transform == null)
                {
                    _queue.RemoveAt(i--);
                    continue;
                }

                if (!passenger.WaitingToBoard) continue;
                if (!InLoadingArea(passenger.Transform.position)) continue;

                return passenger;
            }

            return null;
        }

        void Unload()
        {
            foreach (ChairliftChair chair in _chairs)
            {
                if (chair == null || chair.occupant == null) continue;
                if (Mathf.Abs(_path.Gap(chair.loopOffset + _travel, _unloadS)) > catchWindow) continue;

                Unload(chair);
            }
        }

        void Unload(ChairliftChair chair)
        {
            Vector3 cable = _path.Sample(_unloadS);

            // Step off towards the middle of the run, not off the side of it.
            float centre = mountain.PisteCenterX(cable.z);
            float towards = Mathf.Sign(centre - cable.x);

            Vector3 spot = new Vector3(cable.x + towards * 5f, 0f, cable.z);
            spot.y = mountain.SampleHeight(spot.x, spot.z) + 0.4f;

            // Face the way the run goes, so you are already pointed downhill.
            Vector3 facing = mountain.PistePoint(spot.z - 30f) - spot;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.back;
            facing.Normalize();

            ILiftPassenger passenger = chair.occupant;
            chair.occupant = null;

            if (passenger != null && passenger.Transform != null)
                passenger.LeaveLift(spot, facing, facing * 3f);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
