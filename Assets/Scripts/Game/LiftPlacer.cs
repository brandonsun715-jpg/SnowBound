using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Hud;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Game
{
    /// <summary>
    /// Buying and siting a lift.
    ///
    /// Two clicks: the bottom station, then the top. Between them the line is
    /// drawn on the mountain with the figures that decide whether it is a good
    /// line — how far, how much vertical, how long the ride, what it costs to
    /// build and to run.
    ///
    /// A lift is refused rather than built badly. Each type has a reach and a
    /// gradient it can manage, which is what makes choosing between them a
    /// decision about the mountain rather than a decision about money.
    /// </summary>
    public class LiftPlacer : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;
        public Ledger ledger;
        public ModeDirector modes;
        public SelectionController selection;
        public NotificationStack notifications;

        [Header("Rules")]
        [Tooltip("Shortest line worth building.")]
        public float minimumLength = 80f;
        [Tooltip("Least gap between two lift lines.")]
        public float minimumGapToOtherLifts = 40f;
        [Tooltip("Ground reserved at each station, so nothing else is built on it.")]
        public float stationRadius = 16f;

        // ---- what the interface reads ----

        public bool Placing { get; private set; }
        public LiftDefinition Pending { get; private set; }
        public bool HasBottom { get; private set; }
        public Vector3 Bottom { get; private set; }
        public Vector3 Top { get; private set; }
        public bool ValidHere { get; private set; }
        public string Refusal { get; private set; }

        public float Length { get { return Vector2.Distance(Flat(Bottom), Flat(Top)); } }

        public float Rise { get { return Mathf.Max(0f, Top.y - Bottom.y); } }

        public float RideSeconds
        {
            get { return Pending != null && Pending.lineSpeed > 0.1f ? Length / Pending.lineSpeed : 0f; }
        }

        public float Grade { get { return Length > 1f ? Rise / Length : 0f; } }

        Transform _preview;
        LineRenderer _line;
        Material _ok, _bad;
        readonly List<Transform> _pins = new List<Transform>();
        Material _pinMaterial;

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (modes == null) modes = ModeDirector.Instance;
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
        }

        // ---------------- starting and stopping -----------------------------

        public void Begin(LiftDefinition definition)
        {
            Cancel();
            if (definition == null || mountain == null) return;

            Pending = definition;
            Placing = true;
            HasBottom = false;
            Refusal = "Click the bottom station";

            BuildPreview();

            if (selection != null) { selection.Clear(); selection.Active = false; }
        }

        public void Cancel()
        {
            Placing = false;
            Pending = null;
            HasBottom = false;
            Refusal = null;

            if (_preview != null) Destroy(_preview.gameObject);
            _preview = null;
            _pins.Clear();
        }

        /// <summary>Take the bottom station back off, or drop out entirely.</summary>
        public void Undo()
        {
            if (!Placing) return;

            if (HasBottom) { HasBottom = false; Refusal = "Click the bottom station"; return; }
            Cancel();
        }

        // ---------------- running -------------------------------------------

        void Update()
        {
            if (!Placing) return;

            if (modes != null && modes.Mode != GameMode.Management) { Cancel(); return; }
            if (ManagementInput.CancelPressed) { Undo(); return; }

            Vector3 point;
            if (!GroundUnderCursor(out point)) return;

            if (!HasBottom) Bottom = point;
            else Top = point;

            ValidHere = Check();
            Redraw(point);

            if (!UIPointer.Pressed || UIPointer.OverInterface) return;

            if (!HasBottom)
            {
                // The bottom station only has to be somewhere a lift can stand.
                if (!StationOk(point)) return;

                Bottom = point;
                HasBottom = true;
                Refusal = "Click the top station";
                return;
            }

            if (ValidHere) Confirm();
        }

        bool StationOk(Vector3 point)
        {
            if (!mountain.InsideWorld(point.x, point.z, -stationRadius))
            {
                Refusal = "Outside the resort";
                return false;
            }

            if (mountain.SlopeDegrees(point.x, point.z) > 26f)
            {
                Refusal = "A station needs flatter ground";
                return false;
            }

            return true;
        }

        bool Check()
        {
            if (Pending == null) { Refusal = null; return false; }
            if (!HasBottom) { Refusal = "Click the bottom station"; return false; }

            if (!StationOk(Top)) return false;

            if (Length < minimumLength) { Refusal = "Too short to be worth building"; return false; }

            if (Length > Pending.maxLength)
            {
                Refusal = "Too long for a " + Pending.name.ToLowerInvariant()
                        + "  (max " + Mathf.RoundToInt(Pending.maxLength) + " m)";
                return false;
            }

            if (Top.y <= Bottom.y + 4f)
            {
                Refusal = "The top station has to be uphill";
                return false;
            }

            if (Grade > Pending.maxGrade)
            {
                Refusal = "Too steep for a " + Pending.name.ToLowerInvariant()
                        + "  (max " + Mathf.RoundToInt(Pending.maxGrade * 100f) + "%)";
                return false;
            }

            string clash = Clash();
            if (clash != null) { Refusal = clash; return false; }

            if (ledger != null && ledger.Cash < Pending.cost) { Refusal = "Not enough cash"; return false; }

            Refusal = null;
            return true;
        }

        string Clash()
        {
            foreach (Chairlift other in FindObjectsByType<Chairlift>(FindObjectsSortMode.None))
            {
                if (other == null) continue;

                float gap = Mathf.Min(Vector2.Distance(other.bottomStation, Flat(Bottom)),
                                      Vector2.Distance(other.topStation, Flat(Top)));

                if (gap < minimumGapToOtherLifts) return "Too close to the " + other.name.ToLowerInvariant();
            }

            string reserved = mountain.ProtectedBy(Bottom.x, Bottom.z, 2f);
            if (reserved != null && reserved != "Lodge") return "Bottom station is on the " + reserved.ToLowerInvariant();

            reserved = mountain.ProtectedBy(Top.x, Top.z, 2f);
            if (reserved != null) return "Top station is on the " + reserved.ToLowerInvariant();

            return null;
        }

        void Confirm()
        {
            if (ledger != null && !ledger.Spend(LedgerLine.Construction, Pending.cost)) return;

            var go = new GameObject(Pending.name);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.hideFlags = HideFlags.DontSaveInEditor;

            var rig = go.AddComponent<Chairlift>();
            rig.mountain = mountain;
            rig.bottomStation = Flat(Bottom);
            rig.topStation = Flat(Top);
            rig.Configure(Pending);

            var facility = go.AddComponent<LiftFacility>();
            facility.Adopt(rig, Pending);

            // Both stations become ground the player may not sculpt away.
            mountain.Protect(Bottom, stationRadius, Pending.name + " bottom station");
            mountain.Protect(Top, stationRadius, Pending.name + " top station");

            LiftDefinition bought = Pending;
            Cancel();

            var traffic = FindAnyObjectByType<ResortTraffic>();
            if (traffic != null) traffic.Rescan();

            if (notifications != null)
            {
                notifications.Announce(bought.name + " is running",
                                       bought.GuestsPerHour.ToString("N0") + " guests an hour, "
                                       + Mathf.RoundToInt(rig.VerticalRise) + " m of vertical.");
            }

            if (selection != null) selection.Active = true;
        }

        // ---------------- preview ---------------------------------------------

        static Vector2 Flat(Vector3 v) { return new Vector2(v.x, v.z); }

        bool GroundUnderCursor(out Vector3 point)
        {
            point = Vector3.zero;
            if (view == null) view = Camera.main;
            if (view == null) return false;

            Ray ray = view.ScreenPointToRay(UIPointer.Position);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 6000f, ~0, QueryTriggerInteraction.Ignore)) return false;

            point = hit.point;
            return true;
        }

        void BuildPreview()
        {
            var go = new GameObject("LiftPreview");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _preview = go.transform;

            _ok = MaterialFactory.CreateParticle("LiftOk",
                    new Color(0.47f, 0.82f, 0.59f, 0.95f), PrimitiveTextures.SoftCircle());
            _bad = MaterialFactory.CreateParticle("LiftBad",
                    new Color(0.90f, 0.47f, 0.45f, 0.95f), PrimitiveTextures.SoftCircle());
            _pinMaterial = MaterialFactory.CreateEmissive("LiftPin",
                    new Color(0.56f, 0.78f, 0.95f), new Color(0.42f, 0.68f, 0.95f) * 1.6f);

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = 1.4f;
            _line.numCapVertices = 2;
            _line.positionCount = 24;
            _line.sharedMaterial = _ok;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            for (int i = 0; i < 2; i++)
            {
                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = "Station";
                pin.transform.SetParent(_preview, false);
                pin.hideFlags = HideFlags.DontSaveInEditor;
                pin.transform.localScale = new Vector3(2.4f, 6f, 2.4f);
                pin.GetComponent<MeshRenderer>().sharedMaterial = _pinMaterial;

                Destroy(pin.GetComponent<Collider>());
                _pins.Add(pin.transform);
            }
        }

        void Redraw(Vector3 cursor)
        {
            if (_line == null) return;

            Vector3 from = HasBottom ? Bottom : cursor;
            Vector3 to = HasBottom ? cursor : cursor;

            _line.enabled = HasBottom;
            _line.sharedMaterial = ValidHere ? _ok : _bad;

            if (HasBottom)
            {
                int n = _line.positionCount;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    Vector3 flat = Vector3.Lerp(from, to, t);

                    // The cable hangs above the ground it crosses, so a line
                    // that dives into a ridge is visibly a bad line.
                    float ground = mountain.SampleHeight(flat.x, flat.z);
                    float cable = Mathf.Lerp(from.y, to.y, t) + 8f;

                    _line.SetPosition(i, new Vector3(flat.x, Mathf.Max(ground + 2f, cable), flat.z));
                }
            }

            _pins[0].gameObject.SetActive(true);
            _pins[0].position = new Vector3(from.x, mountain.SampleHeight(from.x, from.z) + 3f, from.z);

            _pins[1].gameObject.SetActive(HasBottom);
            if (HasBottom)
                _pins[1].position = new Vector3(to.x, mountain.SampleHeight(to.x, to.z) + 3f, to.z);
        }
    }
}
