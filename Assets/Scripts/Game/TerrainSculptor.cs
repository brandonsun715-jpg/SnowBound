using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Hud;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Game
{
    /// <summary>
    /// Shaping the mountain.
    ///
    /// A round brush, five tools, and a ring drawn on the snow that follows
    /// the ground so the player can see exactly what the stroke will reach.
    /// The terrain rebuilds only the chunks the brush touched, which is what
    /// keeps a stroke smooth rather than a slideshow.
    ///
    /// Ground the resort depends on — the lodge's footings, a lift station —
    /// is refused rather than quietly ruined, and the ring says so instead of
    /// nothing happening for no visible reason.
    /// </summary>
    public class TerrainSculptor : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;
        public ModeDirector modes;
        public SelectionController selection;
        public Ledger ledger;

        [Header("Brush")]
        public TerrainTool tool = TerrainTool.Raise;
        public float radius = 26f;
        public float strength = 0.55f;
        public float minRadius = 8f;
        public float maxRadius = 90f;

        [Header("Snow painting")]
        public SnowQuality paintQuality = SnowQuality.Packed;
        public bool paintGroomed = true;

        [Header("Cost")]
        [Tooltip("Cost per second of shaping. Zero means the mountain is free to move.")]
        public float costPerSecond = 0f;

        [Header("Ring")]
        public int ringSegments = 72;
        public float ringLift = 0.5f;

        public bool Active { get; private set; }
        public bool Painting { get; set; }
        public string Refusal { get; private set; }
        public bool CanApplyHere { get; private set; }

        Transform _ring;
        LineRenderer _line;
        Material _ringOk, _ringBlocked;
        Vector3 _cursor;
        bool _cursorValid;

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (modes == null) modes = ModeDirector.Instance;
            if (selection == null) selection = FindAnyObjectByType<SelectionController>();
            if (ledger == null) ledger = Ledger.Instance;
        }

        // ---------------- turning it on and off ---------------------------

        public void Begin(TerrainTool which)
        {
            tool = which;
            Painting = false;

            if (Active) return;
            Active = true;

            BuildRing();

            if (selection != null) { selection.Clear(); selection.Active = false; }
        }

        /// <summary>Snow painting is a brush too, but it edits a run, not the ground.</summary>
        public void BeginPainting(SnowQuality quality, bool groomed)
        {
            Begin(TerrainTool.Smooth);

            Painting = true;
            paintQuality = quality;
            paintGroomed = groomed;
        }

        public void End()
        {
            Active = false;
            Painting = false;
            Refusal = null;

            if (_ring != null) Destroy(_ring.gameObject);
            _ring = null;

            if (_stroking)
            {
                _stroking = false;
                if (mountain != null) mountain.SettleColliders();
                TrailDesigner.Reshape();
            }

            if (selection != null && modes != null && modes.Mode == GameMode.Management)
                selection.Active = true;
        }

        public void SetRadius(float value) { radius = Mathf.Clamp(value, minRadius, maxRadius); }
        public void SetStrength(float value) { strength = Mathf.Clamp(value, 0.1f, 1f); }

        // ---------------- running --------------------------------------------

        void Update()
        {
            if (!Active) return;

            if (modes != null && modes.Mode != GameMode.Management) { End(); return; }
            if (mountain == null || !mountain.Ready) return;

            _cursorValid = GroundUnderCursor(out _cursor);

            Preview();

            bool holding = UIPointer.Held && !UIPointer.OverInterface && _cursorValid;

            if (holding)
            {
                if (Painting) PaintOnce();
                else Carve();

                _stroking = true;
                return;
            }

            // The trees, the lift and the markers all sit on ground that has
            // just moved, but re-placing nine hundred trees every frame of a
            // brush stroke would make the brush unusable. They are put back
            // once, when the stroke ends.
            if (!_stroking) return;

            _stroking = false;
            mountain.SettleColliders();
            TrailDesigner.Reshape();
        }

        bool _stroking;

        void Carve()
        {
            if (costPerSecond > 0f && ledger != null &&
                !ledger.Spend(LedgerLine.Construction, costPerSecond * Time.deltaTime))
            {
                Refusal = "Not enough cash";
                return;
            }

            Refusal = mountain.Sculpt(_cursor, radius, strength, tool, Time.deltaTime);
        }

        float _lastPaint = -1f;

        void PaintOnce()
        {
            // Painting is a state change, not a continuous push, so once per
            // press is plenty and once per frame would thrash the rebuild.
            if (Time.time - _lastPaint < 0.4f) return;
            _lastPaint = Time.time;

            Refusal = mountain.PaintSnow(_cursor, paintQuality, paintGroomed);
        }

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

        // ---------------- the ring ---------------------------------------------

        void BuildRing()
        {
            var go = new GameObject("BrushRing");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _ring = go.transform;

            _ringOk = MaterialFactory.CreateParticle("BrushOk",
                        new Color(0.56f, 0.78f, 0.95f, 0.95f), PrimitiveTextures.SoftCircle());
            _ringBlocked = MaterialFactory.CreateParticle("BrushBlocked",
                        new Color(0.90f, 0.47f, 0.45f, 0.95f), PrimitiveTextures.SoftCircle());

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.widthMultiplier = 0.9f;
            _line.numCapVertices = 2;
            _line.positionCount = Mathf.Max(12, ringSegments);
            _line.sharedMaterial = _ringOk;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }

        void Preview()
        {
            if (_line == null) return;

            _line.enabled = _cursorValid;
            if (!_cursorValid) { CanApplyHere = false; return; }

            string reserved = mountain.ProtectedBy(_cursor.x, _cursor.z, radius * 0.55f);
            bool inside = mountain.InsideWorld(_cursor.x, _cursor.z, -radius * 0.25f);

            CanApplyHere = reserved == null && inside;
            if (!CanApplyHere) Refusal = reserved ?? "Outside the resort";
            else if (Refusal == "Outside the resort") Refusal = null;

            _line.sharedMaterial = CanApplyHere ? _ringOk : _ringBlocked;
            _line.widthMultiplier = Mathf.Max(0.5f, radius * 0.035f);

            int n = _line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;

                float x = _cursor.x + Mathf.Cos(a) * radius;
                float z = _cursor.z + Mathf.Sin(a) * radius;

                // The ring lies on the snow, so it reads as an area of ground
                // rather than a flat disc floating over a slope.
                _line.SetPosition(i, new Vector3(x, mountain.SampleHeight(x, z) + ringLift, z));
            }
        }

        public static string ToolName(TerrainTool tool)
        {
            switch (tool)
            {
                case TerrainTool.Raise: return "RAISE";
                case TerrainTool.Lower: return "LOWER";
                case TerrainTool.Smooth: return "SMOOTH";
                case TerrainTool.Flatten: return "FLATTEN";
                default: return "SCULPT SLOPE";
            }
        }
    }
}
