using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Hud;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Game
{
    /// <summary>
    /// Cutting a new run.
    ///
    /// A piste is already data — a line, a width and a grade — and the terrain
    /// carves itself towards whichever run is nearest. So adding one is adding
    /// a definition and reshaping the mountain, not sculpting anything by
    /// hand. The preview is drawn from the same definition the real run will
    /// use, which is why what you see is what gets cut.
    ///
    /// Reshaping rebuilds the terrain mesh, the forest and the far range, so
    /// it takes a moment. That moment is the mountain being re-cut, and it is
    /// worth showing rather than hiding.
    /// </summary>
    public class TrailBuilder : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;
        public Ledger ledger;
        public ModeDirector modes;
        public SelectionController selection;
        public NotificationStack notifications;

        [Header("Cost")]
        public float baseCost = 16000f;
        public float costPerMetreOfWidth = 620f;

        [Header("Shape")]
        public float halfWidth = 19f;
        public float minimumGapToOtherRuns = 26f;

        static readonly string[] Names =
        {
            "Bergland", "Fox Hollow", "Silver Chute", "Timberline", "Whitebark",
            "Aspen Way", "Corrie", "Elkhorn", "Rimrock", "Snowdrift"
        };

        public bool Planning { get; private set; }
        public PisteGrade Grade { get; private set; }
        public bool ValidHere { get; private set; }
        public string Refusal { get; private set; }
        public float Cost { get { return baseCost + halfWidth * costPerMetreOfWidth; } }

        PisteDefinition _candidate;
        Transform _preview;
        Mesh _previewMesh;
        Material _valid, _invalid;
        MeshRenderer _previewRenderer;

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (modes == null) modes = ModeDirector.Instance;
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (notifications == null) notifications = FindAnyObjectByType<NotificationStack>();
        }

        public void Begin(PisteGrade grade)
        {
            Cancel();
            if (mountain == null) return;

            Grade = grade;
            Planning = true;

            _candidate = new PisteDefinition
            {
                name = Names[Random.Range(0, Names.Length)],
                grade = grade,
                anchorX = 10f,
                spreadX = 0f,
                snakeAmplitude = grade == PisteGrade.Advanced ? 18f : 24f,
                snakeFrequency = Random.Range(0.011f, 0.019f),
                snakePhase = Random.Range(0f, 6.2f),
                halfWidth = grade == PisteGrade.Beginner ? 26f
                          : grade == PisteGrade.Advanced ? 15f : halfWidth,
                baseExtraWidth = 14f,
                surfaceNoise = grade == PisteGrade.Advanced ? 2.6f
                             : grade == PisteGrade.Beginner ? 0.8f : 1.2f,
                hasRollers = grade != PisteGrade.Advanced
            };

            BuildPreview();

            if (selection != null) { selection.Clear(); selection.Active = false; }
        }

        public void Cancel()
        {
            Planning = false;
            _candidate = null;
            Refusal = null;

            if (_preview != null) Destroy(_preview.gameObject);
            _preview = null;

            if (selection != null && modes != null && modes.Mode == GameMode.Management)
                selection.Active = true;
        }

        void BuildPreview()
        {
            var go = new GameObject("TrailPreview");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _preview = go.transform;

            _previewMesh = new Mesh();
            _previewMesh.name = "TrailPreview";
            _previewMesh.hideFlags = HideFlags.DontSave;
            _previewMesh.MarkDynamic();

            go.AddComponent<MeshFilter>().sharedMesh = _previewMesh;

            _valid = MaterialFactory.CreateParticle("TrailValid",
                        new Color(0.47f, 0.82f, 0.59f, 0.5f), PrimitiveTextures.SoftCircle());
            _invalid = MaterialFactory.CreateParticle("TrailInvalid",
                        new Color(0.90f, 0.47f, 0.45f, 0.5f), PrimitiveTextures.SoftCircle());

            _previewRenderer = go.AddComponent<MeshRenderer>();
            _previewRenderer.sharedMaterial = _valid;
            _previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (!Planning) return;

            if (modes != null && modes.Mode != GameMode.Management) { Cancel(); return; }
            if (ManagementInput.CancelPressed) { Cancel(); return; }

            Vector3 point;
            if (!GroundUnderCursor(out point)) return;

            // The click decides where the run sits at mid mountain; the ends
            // stay anchored to the base area and the summit like every other.
            float spread = mountain.PisteSpread(point.z);
            if (spread > 0.15f) _candidate.spreadX = (point.x - _candidate.anchorX) / spread;

            ValidHere = Check();
            _previewRenderer.sharedMaterial = ValidHere ? _valid : _invalid;
            Redraw();

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

        bool Check()
        {
            if (ledger != null && ledger.Cash < Cost) { Refusal = "Not enough cash"; return false; }

            float half = mountain.width * 0.5f - 60f;

            for (float z = 120f; z < mountain.length - 60f; z += 40f)
            {
                float centre = mountain.CenterXFor(_candidate, z);

                if (Mathf.Abs(centre) > half) { Refusal = "Runs off the mountain"; return false; }

                for (int i = 0; i < mountain.PisteCount; i++)
                {
                    float gap = Mathf.Abs(centre - mountain.PisteCenterX(i, z))
                              - _candidate.halfWidth - mountain.PisteHalfWidth(i, z);

                    if (gap < minimumGapToOtherRuns)
                    {
                        Refusal = "Too close to " + mountain.pistes[i].name;
                        return false;
                    }
                }
            }

            Refusal = null;
            return true;
        }

        void Redraw()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            const int steps = 40;
            float from = 60f;
            float to = mountain.length - 40f;

            for (int i = 0; i <= steps; i++)
            {
                float z = Mathf.Lerp(from, to, i / (float)steps);
                float centre = mountain.CenterXFor(_candidate, z);
                float wide = _candidate.halfWidth;

                float leftY = mountain.SampleHeight(centre - wide, z) + 0.8f;
                float rightY = mountain.SampleHeight(centre + wide, z) + 0.8f;

                verts.Add(new Vector3(centre - wide, leftY, z));
                verts.Add(new Vector3(centre + wide, rightY, z));
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 3);
                tris.Add(a); tris.Add(a + 3); tris.Add(a + 1);
            }

            _previewMesh.Clear();
            _previewMesh.SetVertices(verts);
            _previewMesh.SetTriangles(tris, 0);
            _previewMesh.RecalculateNormals();
            _previewMesh.RecalculateBounds();
        }

        void Commit()
        {
            if (ledger != null && !ledger.Spend(LedgerLine.Construction, Cost)) return;

            PisteDefinition cut = _candidate;
            Cancel();

            mountain.AddPiste(cut);
            Reshape();

            if (notifications != null)
                notifications.Announce(cut.name + " is open",
                                       SkiHud.GradeName(cut.grade) + " run, cut into the mountain.");
        }

        /// <summary>Everything shaped by the terrain has to be shaped again.</summary>
        void Reshape()
        {
            var far = FindAnyObjectByType<FarRange>();
            if (far != null) far.Build();

            var props = FindAnyObjectByType<MountainProps>();
            if (props != null) props.Build();

            var park = FindAnyObjectByType<TerrainPark>();
            if (park != null) park.Build();

            var lift = FindAnyObjectByType<SnowBound.Lifts.Chairlift>();
            if (lift != null) lift.Build();

            var gates = FindAnyObjectByType<RunTimer>();
            if (gates != null) gates.Build();
        }
    }
}
