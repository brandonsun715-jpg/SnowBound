using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Hud;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Game
{
    /// <summary>
    /// Designing a run.
    ///
    /// The player drops control points down the mountain and the run follows a
    /// smooth line through them. Everything shown while designing — the
    /// ribbon, the length, the grade, the difficulty it will be classified as
    /// — is measured off the ground the run would actually sit on, so the
    /// preview is a promise rather than a decoration.
    ///
    /// The run is not cut until it is confirmed, and it is refused rather than
    /// cut badly: a line that climbs, wanders off the map, crosses another run
    /// or cannot be paid for is rejected with the reason said out loud.
    /// </summary>
    public class TrailDesigner : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;
        public Ledger ledger;
        public ModeDirector modes;
        public SelectionController selection;
        public NotificationStack notifications;

        [Header("Cost")]
        public float baseCost = 4200f;
        [Tooltip("Per metre of run, per metre of width. Wide runs cost more to cut.")]
        public float costPerSquareMetre = 1.35f;

        [Header("Rules")]
        [Tooltip("Least gap between the edges of two runs.")]
        public float minimumGapToOtherRuns = 18f;
        [Tooltip("Shortest run worth cutting.")]
        public float minimumLength = 90f;
        public float pointSpacing = 22f;

        [Header("Preview")]
        public float markerSize = 4.5f;

        static readonly string[] Names =
        {
            "Bergland", "Fox Hollow", "Silver Chute", "Timberline", "Whitebark",
            "Aspen Way", "Corrie", "Elkhorn", "Rimrock", "Snowdrift",
            "Larchway", "Cornice", "Ptarmigan", "Meltwater", "Sundance"
        };

        // ---- what the interface reads ----

        public bool Designing { get; private set; }
        public Trail Draft { get; private set; }
        public bool ValidHere { get; private set; }
        public string Refusal { get; private set; }
        public int PointCount { get { return Draft != null ? Draft.points.Count : 0; } }

        public float Cost
        {
            get
            {
                if (Draft == null) return baseCost;
                return baseCost + Draft.length * Draft.halfWidth * 2f * costPerSquareMetre;
            }
        }

        Transform _preview;
        Mesh _ribbon;
        MeshRenderer _ribbonRenderer;
        Material _valid, _invalid;

        Transform _markers;
        Material _markerMaterial;
        readonly List<Transform> _dots = new List<Transform>();

        Vector3 _cursor;
        bool _cursorValid;

        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<int> _tris = new List<int>();

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (modes == null) modes = ModeDirector.Instance;
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
        }

        // ---------------- starting and stopping ---------------------------

        public void Begin(TrailGrade grade)
        {
            Cancel();
            if (mountain == null) return;

            Draft = new Trail { name = Names[Random.Range(0, Names.Length)] };
            Trail.ApplyGradeDefaults(Draft, grade);

            Designing = true;
            Refusal = "Click the top of the run";

            BuildPreview();

            if (selection != null) { selection.Clear(); selection.Active = false; }
        }

        public void Cancel()
        {
            Designing = false;
            Draft = null;
            Refusal = null;

            if (_preview != null) Destroy(_preview.gameObject);
            _preview = null;
            _dots.Clear();

            if (selection != null && modes != null && modes.Mode == GameMode.Management)
                selection.Active = true;
        }

        public void SetWidth(float halfWidth)
        {
            if (Draft == null) return;

            Draft.halfWidth = Mathf.Clamp(halfWidth, 5f, 34f);
            Remeasure();
        }

        public void SetSnow(SnowQuality quality)
        {
            if (Draft == null) return;
            Draft.snow = quality;
        }

        public void SetGroomed(bool groomed)
        {
            if (Draft == null) return;
            Draft.groomed = groomed;
        }

        public void Rename(string name)
        {
            if (Draft == null || string.IsNullOrEmpty(name)) return;
            Draft.name = name;
        }

        /// <summary>Take the last control point back off.</summary>
        public void Undo()
        {
            if (Draft == null || Draft.points.Count == 0) return;

            Draft.points.RemoveAt(Draft.points.Count - 1);
            Remeasure();
        }

        // ---------------- running -----------------------------------------

        void Update()
        {
            if (!Designing) return;

            if (modes != null && modes.Mode != GameMode.Management) { Cancel(); return; }

            if (ManagementInput.CancelPressed) { Undo(); return; }

            _cursorValid = GroundUnderCursor(out _cursor);

            Redraw();

            if (!UIPointer.Pressed || UIPointer.OverInterface || !_cursorValid) return;

            AddPoint(_cursor);
        }

        void AddPoint(Vector3 point)
        {
            if (Draft == null) return;

            var flat = new Vector2(point.x, point.z);

            // Two points on top of each other make a kink, not a corner.
            if (Draft.points.Count > 0 &&
                Vector2.Distance(Draft.points[Draft.points.Count - 1], flat) < pointSpacing)
            {
                return;
            }

            Draft.points.Add(flat);
            Remeasure();
        }

        /// <summary>
        /// Measure the proposed run against the ground it would sit on. This is
        /// the natural mountain, not the carved one, because the run has not
        /// been cut yet — but the carve follows this line, so the figures hold.
        /// </summary>
        void Remeasure()
        {
            if (Draft == null || mountain == null) return;

            Draft.Resample(mountain.NaturalHeight);
            Draft.Measure(mountain.NaturalHeight);

            // The grade is what the terrain says it is, not what was picked
            // off the menu. Drawing a gentle line and calling it a black run
            // does not make it one.
            Draft.grade = Trail.GradeFor(Draft.averageGrade, Draft.maxGrade, Draft.halfWidth);

            ValidHere = Check();
        }

        bool Check()
        {
            if (Draft == null) { Refusal = null; return false; }

            if (Draft.points.Count == 0) { Refusal = "Click the top of the run"; return false; }
            if (Draft.points.Count == 1) { Refusal = "Click again further down the mountain"; return false; }

            for (int i = 0; i < Draft.points.Count; i++)
            {
                Vector2 p = Draft.points[i];
                if (!mountain.InsideWorld(p.x, p.y, -12f)) { Refusal = "Runs off the mountain"; return false; }
            }

            if (Draft.length < minimumLength)
            {
                Refusal = "Too short to be a run";
                return false;
            }

            // A run that climbs is not a run. The terrain will be forced to
            // descend when it is cut, so a line that mostly goes uphill would
            // gouge the mountain apart rather than make a trail.
            if (Draft.drop < Draft.length * 0.04f)
            {
                Refusal = "That line does not go downhill";
                return false;
            }

            string clash = Clash();
            if (clash != null) { Refusal = clash; return false; }

            if (ledger != null && ledger.Cash < Cost)
            {
                Refusal = "Not enough cash";
                return false;
            }

            Refusal = null;
            return true;
        }

        string Clash()
        {
            var spine = Draft.spine;
            if (spine == null) return null;

            for (int s = 0; s < spine.Count; s += 2)
            {
                Vector3 p = spine[s];

                string reserved = mountain.ProtectedBy(p.x, p.z, Draft.halfWidth);
                if (reserved != null) return "Crosses the " + reserved.ToLowerInvariant();

                for (int i = 0; i < mountain.TrailCount; i++)
                {
                    Trail other = mountain.TrailAt(i);
                    if (other == null) continue;

                    float along;
                    float gap = other.DistanceTo(p.x, p.z, out along)
                              - other.halfWidth - Draft.halfWidth;

                    if (gap < minimumGapToOtherRuns) return "Too close to " + other.name;
                }
            }

            return null;
        }

        public void Confirm()
        {
            if (!Designing || !ValidHere || Draft == null) return;
            if (ledger != null && !ledger.Spend(LedgerLine.Construction, Cost)) return;

            Trail cut = Draft;
            Cancel();

            mountain.AddTrail(cut);
            Reshape();

            if (notifications != null)
            {
                notifications.Announce(cut.name + " is open",
                                       Trail.GradeName(cut.grade) + " run, "
                                       + Mathf.RoundToInt(cut.length) + " m, "
                                       + Mathf.RoundToInt(cut.drop) + " m vertical.");
            }
        }

        /// <summary>Everything shaped by the terrain has to be shaped again.</summary>
        public static void Reshape()
        {
            var props = FindAnyObjectByType<MountainProps>();
            if (props != null) props.Build();

            var far = FindAnyObjectByType<FarRange>();
            if (far != null) far.Build();

            var park = FindAnyObjectByType<TerrainPark>();
            if (park != null) park.Build();

            var lift = FindAnyObjectByType<SnowBound.Lifts.Chairlift>();
            if (lift != null) lift.Build();

            var gates = FindAnyObjectByType<RunTimer>();
            if (gates != null) gates.Build();
        }

        // ---------------- preview -------------------------------------------

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
            var go = new GameObject("TrailPreview");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _preview = go.transform;

            _ribbon = new Mesh { name = "TrailPreview" };
            _ribbon.hideFlags = HideFlags.DontSave;
            _ribbon.MarkDynamic();

            go.AddComponent<MeshFilter>().sharedMesh = _ribbon;

            _valid = MaterialFactory.CreateParticle("TrailValid",
                        new Color(0.47f, 0.82f, 0.59f, 0.55f), PrimitiveTextures.SoftCircle());
            _invalid = MaterialFactory.CreateParticle("TrailInvalid",
                        new Color(0.90f, 0.47f, 0.45f, 0.55f), PrimitiveTextures.SoftCircle());
            _markerMaterial = MaterialFactory.CreateEmissive("TrailPoint",
                        new Color(0.56f, 0.78f, 0.95f), new Color(0.42f, 0.68f, 0.95f) * 1.6f);

            _ribbonRenderer = go.AddComponent<MeshRenderer>();
            _ribbonRenderer.sharedMaterial = _valid;
            _ribbonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var markers = new GameObject("Points");
            markers.transform.SetParent(_preview, false);
            markers.hideFlags = HideFlags.DontSaveInEditor;
            _markers = markers.transform;
        }

        void Redraw()
        {
            if (_ribbon == null || Draft == null) return;

            _ribbonRenderer.sharedMaterial = ValidHere ? _valid : _invalid;

            // The line as it stands, plus wherever the cursor is now, so the
            // player can see the next segment before committing to it.
            var line = new List<Vector2>(Draft.points);
            if (_cursorValid && line.Count > 0) line.Add(new Vector2(_cursor.x, _cursor.z));

            Ribbon(line);
            Dots();
        }

        void Ribbon(List<Vector2> line)
        {
            _verts.Clear();
            _tris.Clear();

            if (line.Count >= 2)
            {
                var preview = new Trail { halfWidth = Draft.halfWidth, points = line };
                preview.Resample(mountain.NaturalHeight);

                List<Vector3> spine = preview.spine;

                for (int i = 0; i < spine.Count; i++)
                {
                    Vector3 forward = i < spine.Count - 1 ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1];
                    forward.y = 0f;

                    if (forward.sqrMagnitude < 0.0001f) forward = Vector3.back;
                    forward.Normalize();

                    Vector3 across = Vector3.Cross(Vector3.up, forward) * Draft.halfWidth;

                    Vector3 left = spine[i] - across;
                    Vector3 right = spine[i] + across;

                    left.y = mountain.SampleHeight(left.x, left.z) + 0.9f;
                    right.y = mountain.SampleHeight(right.x, right.z) + 0.9f;

                    _verts.Add(left);
                    _verts.Add(right);
                }

                for (int i = 0; i < spine.Count - 1; i++)
                {
                    int a = i * 2;
                    _tris.Add(a); _tris.Add(a + 2); _tris.Add(a + 3);
                    _tris.Add(a); _tris.Add(a + 3); _tris.Add(a + 1);
                }
            }

            _ribbon.Clear();
            if (_verts.Count == 0) return;

            _ribbon.SetVertices(_verts);
            _ribbon.SetTriangles(_tris, 0);
            _ribbon.RecalculateNormals();
            _ribbon.RecalculateBounds();
        }

        void Dots()
        {
            while (_dots.Count < Draft.points.Count)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Point";
                dot.transform.SetParent(_markers, false);
                dot.hideFlags = HideFlags.DontSaveInEditor;
                dot.GetComponent<MeshRenderer>().sharedMaterial = _markerMaterial;

                Destroy(dot.GetComponent<Collider>());
                _dots.Add(dot.transform);
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                bool used = i < Draft.points.Count;
                _dots[i].gameObject.SetActive(used);
                if (!used) continue;

                Vector2 p = Draft.points[i];
                _dots[i].position = new Vector3(p.x, mountain.SampleHeight(p.x, p.y) + 1.6f, p.y);
                _dots[i].localScale = Vector3.one * markerSize;
            }
        }
    }
}
