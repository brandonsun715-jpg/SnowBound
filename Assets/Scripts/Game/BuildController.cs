using UnityEngine;
using SnowBound.Buildings;
using SnowBound.Hud;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Game
{
    /// <summary>
    /// Putting a building down.
    ///
    /// The ghost is the real building wearing a translucent skin, not a
    /// stand-in, so what you position is exactly what you get. It sits itself
    /// on the lowest corner of its own footprint, the same way the lodge does,
    /// which is why it never floats or buries itself on a slope.
    ///
    /// Placement is refused rather than allowed and regretted: too steep, too
    /// close to something else, out on the piste, or out in the wilderness all
    /// read as invalid before any money changes hands.
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;
        public Ledger ledger;
        public ModeDirector modes;
        public SelectionController selection;
        public NotificationStack notifications;
        public ResortTraffic traffic;

        [Header("Rules")]
        [Tooltip("Steepest ground a building will stand on.")]
        public float maxSlope = 15f;
        [Tooltip("Buildings must be at least this far outside the groomed run.")]
        public float clearOfPiste = 4f;
        [Tooltip("And no further than this from one, or they are in the wilderness.")]
        public float nearPiste = 70f;
        [Tooltip("The base area counts as somewhere worth building, run or not.")]
        public float nearBase = 110f;
        public float spacing = 6f;
        public float rotateStep = 45f;

        public bool Placing { get { return _ghost != null; } }
        public BuildingDefinition Pending { get; private set; }
        public bool ValidHere { get; private set; }
        public string Refusal { get; private set; }

        PlacedBuilding _ghost;
        float _yaw;

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (modes == null) modes = ModeDirector.Instance;
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
        }

        public void Begin(BuildingDefinition definition)
        {
            Cancel();
            if (definition == null || mountain == null) return;

            Pending = definition;
            _yaw = 0f;

            var go = new GameObject("Building - " + definition.name);
            go.hideFlags = HideFlags.DontSaveInEditor;

            _ghost = go.AddComponent<PlacedBuilding>();
            _ghost.Define(definition, mountain);
            _ghost.Raise();

            // Selecting things while placing one is only ever an accident.
            if (selection != null) { selection.Clear(); selection.Active = false; }
        }

        public void Cancel()
        {
            if (_ghost != null) Destroy(_ghost.gameObject);

            _ghost = null;
            Pending = null;
            Refusal = null;

            if (selection != null && modes != null && modes.Mode == GameMode.Management)
                selection.Active = true;
        }

        void Update()
        {
            if (_ghost == null) return;

            if (modes != null && modes.Mode != GameMode.Management) { Cancel(); return; }

            if (ManagementInput.RotatePressed) _yaw += rotateStep;
            if (ManagementInput.CancelPressed) { Cancel(); return; }

            Vector3 point;
            if (!GroundUnderCursor(out point)) return;

            _ghost.MoveTo(point, _yaw);
            ValidHere = Check(point);
            _ghost.SetGhost(true, ValidHere);

            if (!UIPointer.Pressed || UIPointer.OverInterface) return;
            if (ValidHere) Commit();
        }

        bool GroundUnderCursor(out Vector3 point)
        {
            point = Vector3.zero;
            if (view == null) view = Camera.main;
            if (view == null) return false;

            Ray ray = view.ScreenPointToRay(UIPointer.Position);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 4000f, ~0, QueryTriggerInteraction.Ignore)) return false;

            point = hit.point;
            return true;
        }

        bool Check(Vector3 point)
        {
            if (mountain == null || Pending == null) { Refusal = null; return false; }

            if (ledger != null && ledger.Cash < Pending.cost)
            {
                Refusal = "Not enough cash";
                return false;
            }

            if (_ghost.SlopeUnder(point) > maxSlope)
            {
                Refusal = "Ground is too steep";
                return false;
            }

            float half = Mathf.Max(Pending.footprint.x, Pending.footprint.y) * 0.5f;

            if (mountain.OnAnyTrail(point.x, point.z, clearOfPiste - half))
            {
                Refusal = "That would block the run";
                return false;
            }

            string reserved = mountain.ProtectedBy(point.x, point.z, half);
            if (reserved != null)
            {
                Refusal = "Too close to the " + reserved.ToLowerInvariant();
                return false;
            }

            if (!NearARun(point, half))
            {
                Refusal = "Too far from the runs";
                return false;
            }

            for (int i = 0; i < PlacedBuilding.All.Count; i++)
            {
                PlacedBuilding other = PlacedBuilding.All[i];
                if (other == null || other == _ghost) continue;

                float theirs = Mathf.Max(other.definition.footprint.x, other.definition.footprint.y) * 0.5f;
                Vector3 gap = other.transform.position - point;
                gap.y = 0f;

                if (gap.magnitude < half + theirs + spacing)
                {
                    Refusal = "Too close to " + other.displayName;
                    return false;
                }
            }

            Refusal = null;
            return true;
        }

        /// <summary>
        /// Somewhere a guest would actually walk past: beside a run, or in the
        /// base area. On a new resort there are no runs at all, so the base
        /// area is the only place worth putting anything, which is exactly the
        /// decision the player should be making first.
        /// </summary>
        bool NearARun(Vector3 point, float half)
        {
            var lodge = SnowBound.Buildings.LodgeBuilder.Instance;
            if (lodge != null)
            {
                Vector3 gap = lodge.EntrancePosition - point;
                gap.y = 0f;
                if (gap.magnitude - half < nearBase) return true;
            }

            for (int i = 0; i < mountain.TrailCount; i++)
            {
                Trail run = mountain.TrailAt(i);
                if (run == null) continue;

                float along;
                float distance = run.DistanceTo(point.x, point.z, out along) - run.halfWidth;

                if (distance - half < nearPiste) return true;
            }

            return false;
        }

        void Commit()
        {
            if (ledger != null && !ledger.Spend(LedgerLine.Construction, Pending.cost)) return;

            _ghost.SetGhost(false, true);
            _ghost.name = Pending.name;

            // It is a facility now, so it starts costing money to run.
            if (traffic != null) traffic.Rescan();

            if (notifications != null)
                notifications.Announce(Pending.name + " opened", Pending.firstEffect);

            _ghost = null;
            Pending = null;

            if (selection != null) selection.Active = true;
        }
    }
}
