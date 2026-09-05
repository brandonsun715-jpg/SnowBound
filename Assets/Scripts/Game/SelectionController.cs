using UnityEngine;
using SnowBound.Buildings;
using SnowBound.Core;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Resort;
using SnowBound.Hud;

namespace SnowBound.Game
{
    public enum SelectionKind { None, Facility, Trail, Guest, Ground }

    /// <summary>What the player has clicked on. Plain data, read by the inspector.</summary>
    public class Selection
    {
        public SelectionKind kind = SelectionKind.None;
        public Facility facility;
        public int trailIndex = -1;
        public Trail trail;
        public Guest guest;
        public Vector3 anchor;
        public float radius = 12f;
    }

    /// <summary>
    /// Clicking things on the mountain.
    ///
    /// Nothing needs a "selectable" component: what a thing is, is already
    /// written down. A Facility is a facility, a Guest is a guest, and a hit
    /// on the terrain is whichever run that point belongs to. Buildings added
    /// later become clickable the moment they get a Facility, with no extra
    /// wiring.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        public Camera view;
        public MountainGenerator mountain;

        [Header("Highlight")]
        public float ringWidth = 1.1f;
        public float ringLift = 0.35f;

        public Selection Current { get; private set; }
        public event System.Action<Selection> SelectionChanged;

        Transform _ring;
        Material _ringMaterial;

        void Start()
        {
            if (view == null) view = Camera.main;
            if (mountain == null) mountain = MountainGenerator.Instance;

            BuildRing();
            ShowRing(false);
        }

        void BuildRing()
        {
            var go = new GameObject("SelectionRing");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _ring = go.transform;

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            PrimitiveMeshes.AddRing(verts, tris, Vector3.zero, 1f - ringWidth / 24f, 1f, 72);

            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.BuildMesh("SelectionRing", verts, tris);

            _ringMaterial = MaterialFactory.CreateEmissive("SelectionRing",
                                                           new Color(0.55f, 0.78f, 0.95f),
                                                           new Color(0.40f, 0.70f, 0.95f) * 2.2f);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _ringMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (_ring != null) Destroy(_ring.gameObject);
        }

        /// <summary>Only management mode selects things; skiing has other concerns.</summary>
        public bool Active { get; set; }

        void Update()
        {
            if (!Active)
            {
                if (Current != null) Clear();
                return;
            }

            TrackRing();

            if (!UIPointer.Pressed || UIPointer.OverInterface) return;
            Pick();
        }

        void Pick()
        {
            if (view == null) view = Camera.main;
            if (view == null) return;

            Ray ray = view.ScreenPointToRay(UIPointer.Position);

            RaycastHit hit;
            // Guests are triggers, so triggers count here and nowhere else.
            if (!Physics.Raycast(ray, out hit, 3000f, ~0, QueryTriggerInteraction.Collide))
            {
                Clear();
                return;
            }

            var guest = hit.collider.GetComponentInParent<Guest>();
            if (guest != null)
            {
                Set(new Selection
                {
                    kind = SelectionKind.Guest,
                    guest = guest,
                    anchor = guest.transform.position,
                    radius = 1.6f
                });
                return;
            }

            var facility = hit.collider.GetComponentInParent<Facility>();
            if (facility != null)
            {
                Set(new Selection
                {
                    kind = SelectionKind.Facility,
                    facility = facility,
                    anchor = AnchorFor(facility, hit.point),
                    radius = RadiusFor(facility)
                });
                return;
            }

            // Anything else is the mountain itself: either a run, or open ground.
            if (mountain == null) { Clear(); return; }

            Trail trail = mountain.TrailUnder(hit.point.x, hit.point.z, 6f);

            if (trail == null)
            {
                Set(new Selection
                {
                    kind = SelectionKind.Ground,
                    anchor = hit.point,
                    radius = 9f
                });
                return;
            }

            float along;
            trail.DistanceTo(hit.point.x, hit.point.z, out along);

            Set(new Selection
            {
                kind = SelectionKind.Trail,
                trailIndex = mountain.IndexOf(trail),
                trail = trail,
                anchor = trail.PointAt(along),
                radius = Mathf.Max(10f, trail.halfWidth * 0.9f)
            });
        }

        Vector3 AnchorFor(Facility facility, Vector3 fallback)
        {
            var lift = facility.GetComponent<Chairlift>();
            if (lift != null) return lift.BoardingPoint;

            var lodge = facility.GetComponent<LodgeBuilder>();
            if (lodge != null) return lodge.EntrancePosition;

            var park = facility.GetComponent<TerrainPark>();
            if (park != null && mountain != null) return park.Anchor;

            return fallback;
        }

        static float RadiusFor(Facility facility)
        {
            if (facility.GetComponent<Chairlift>() != null) return 9f;
            if (facility.GetComponent<TerrainPark>() != null) return 16f;
            return 14f;
        }

        void Set(Selection selection)
        {
            Current = selection;
            ShowRing(true);
            TrackRing();

            if (SelectionChanged != null) SelectionChanged(Current);
        }

        public void Clear()
        {
            if (Current == null) return;

            Current = null;
            ShowRing(false);

            if (SelectionChanged != null) SelectionChanged(null);
        }

        void TrackRing()
        {
            if (_ring == null || Current == null) return;

            // Guests move, so the ring follows rather than being placed once.
            if (Current.kind == SelectionKind.Guest)
            {
                if (Current.guest == null) { Clear(); return; }
                Current.anchor = Current.guest.transform.position;
            }

            Vector3 at = Current.anchor;
            if (mountain != null) at.y = mountain.SampleHeight(at.x, at.z);

            _ring.position = at + Vector3.up * ringLift;
            _ring.localScale = new Vector3(Current.radius, 1f, Current.radius);
        }

        void ShowRing(bool visible)
        {
            if (_ring != null) _ring.gameObject.SetActive(visible);
        }
    }
}
