using UnityEngine;
using SnowBound.Core;
using SnowBound.Buildings;
using SnowBound.Mountain;

namespace SnowBound.Resort
{
    /// <summary>
    /// The small things that make a base area a place: benches to sit on,
    /// bins beside them, lamps along the path, a sign at the top of every
    /// run, fencing where the ground falls away, and the heaps of pushed
    /// snow a groomer leaves at the edge of everything it has been over.
    ///
    /// Nothing here is scattered. A bench faces the lodge because that is
    /// where you sit and look; a bin stands beside a bench because that is
    /// where rubbish is; a fence goes where the ground drops away past the
    /// edge of the run, which is the only reason to put a fence anywhere.
    ///
    /// All of it is welded into batched meshes and none of it collides. It
    /// is scenery, and scenery you can walk into is a bug report.
    /// </summary>
    [ExecuteAlways]
    public class ResortDressing : MonoBehaviour
    {
        const string ContainerName = "GeneratedDressing";

        [Tooltip("Leave empty to find them in the scene.")]
        public MountainGenerator mountain;
        public LodgeBuilder lodge;

        [Header("Base area")]
        public bool dressLodge = true;

        [Header("Runs")]
        [Tooltip("A sign at the top of every run.")]
        public bool signRuns = true;
        [Tooltip("Fence the edge where the ground beyond it drops by more than this.")]
        public float fenceDrop = 3.2f;
        [Tooltip("How far out from the edge the drop is measured.")]
        public float fenceReach = 7f;
        public int maxFencePanels = 220;

        readonly GroundWatch _ground = new GroundWatch();

        void Start() { Build(); }
        void OnDisable() { _ground.Stop(); }

        void Update()
        {
            if (!Application.isPlaying) return;
            _ground.Tick();
        }

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
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (lodge == null) lodge = LodgeBuilder.Instance;
            if (mountain == null || !mountain.Ready) return;

            Material props = HeroAssets.Surface(HeroAssets.Props);
            if (props == null) return;

            Clear();

            var root = new GameObject(ContainerName);
            root.transform.SetParent(transform, false);

            var batch = new MeshBatcher(root.transform, "Dressing", new[] { props }, 60000, true);

            if (dressLodge) DressLodge(batch);
            if (signRuns) SignRuns(batch);
            Fence(batch);

            batch.Flush();

            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                tr.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            _ground.Follow(mountain, Build);
        }

        HeroAssets.Piece Piece(string part) { return HeroAssets.Geometry(HeroAssets.Props, part); }

        /// <summary>
        /// Stand one prop on the snow, turned to face a direction.
        ///
        /// `bed` is how much of the ground's own tilt the prop takes. A bench
        /// or a fence panel lies with the hill — level on a slope it has one
        /// end in the air and the other buried — while a lamp post and a sign
        /// are dug in upright whatever the ground under them is doing, so they
        /// pass nought.
        /// </summary>
        void Place(MeshBatcher batch, HeroAssets.Piece piece, Vector3 at, float yaw,
                   float scale = 1f, float bed = 0f)
        {
            if (piece == null) return;

            Quaternion lie = Quaternion.Euler(0f, yaw, 0f);

            if (bed > 0f)
            {
                Quaternion slope = Quaternion.FromToRotation(
                    Vector3.up, mountain.SampleNormal(at.x, at.z));
                lie = Quaternion.Slerp(Quaternion.identity, slope, Mathf.Clamp01(bed)) * lie;
            }

            var placement = Matrix4x4.TRS(
                new Vector3(at.x, mountain.SampleHeight(at.x, at.z) - 0.04f, at.z),
                lie, Vector3.one * scale);

            batch.Add(piece.vertices, piece.triangles, 0, placement, piece.uvs);
        }

        /// <summary>
        /// The furniture outside the lodge, laid out around its door rather
        /// than sprinkled near it.
        /// </summary>
        void DressLodge(MeshBatcher batch)
        {
            if (lodge == null) return;

            HeroAssets.Piece bench = Piece("Bench");
            HeroAssets.Piece bin = Piece("Bin");
            HeroAssets.Piece lamp = Piece("Lamp");
            HeroAssets.Piece pile = Piece("Pile");

            Vector3 door = lodge.EntrancePosition;
            float yaw = lodge.facingYaw;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            // Local coordinates: x across the front of the lodge, z out from
            // the door. Benches face back towards it.
            var seats = new[] { new Vector3(-5.5f, 0f, 4.5f), new Vector3(5.5f, 0f, 4.5f),
                                new Vector3(0f, 0f, 8.5f) };

            foreach (Vector3 seat in seats)
            {
                Place(batch, bench, door + turn * seat, yaw + (seat.z > 6f ? 0f : 180f), 1f, 0.7f);
                Place(batch, bin, door + turn * (seat + new Vector3(1.4f, 0f, 0.2f)), yaw, 1f, 0.7f);
            }

            var lamps = new[] { new Vector3(-8f, 0f, 2f), new Vector3(8f, 0f, 2f),
                                new Vector3(-8f, 0f, 11f), new Vector3(8f, 0f, 11f),
                                new Vector3(0f, 0f, 15f) };

            foreach (Vector3 post in lamps) Place(batch, lamp, door + turn * post, yaw);

            // Pushed snow at the edges of the pad, where a groomer would have
            // left it.
            var heaps = new[] { new Vector3(-15f, 0f, 7f), new Vector3(15f, 0f, 9f),
                                new Vector3(-11f, 0f, 17f) };

            for (int i = 0; i < heaps.Length; i++)
                Place(batch, pile, door + turn * heaps[i], yaw + i * 47f, 0.8f + i * 0.25f, 0.9f);
        }

        /// <summary>A board at the top of every run, facing the way down it.</summary>
        void SignRuns(MeshBatcher batch)
        {
            HeroAssets.Piece sign = Piece("Sign");
            if (sign == null) return;

            for (int i = 0; i < mountain.TrailCount; i++)
            {
                Trail trail = mountain.TrailAt(i);
                if (trail == null || trail.spine == null || trail.spine.Count < 3) continue;

                Vector3 top = trail.spine[0];
                Vector3 down = (trail.spine[2] - top);
                down.y = 0f;

                if (down.sqrMagnitude < 0.01f) continue;

                float yaw = Quaternion.LookRotation(down.normalized, Vector3.up).eulerAngles.y;
                Vector3 across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

                Place(batch, sign, top + across * (trail.halfWidth + 2.2f), yaw + 180f);
            }
        }

        /// <summary>
        /// Fencing where the edge of a run has something to fall off.
        ///
        /// Measured rather than decided: the ground is sampled a few metres
        /// out from each edge, and where it has dropped away a panel goes in.
        /// That puts fences along the cliff side of a traverse and nowhere
        /// along an open bowl, which is where they are in life.
        /// </summary>
        void Fence(MeshBatcher batch)
        {
            HeroAssets.Piece panel = Piece("Fence");
            if (panel == null) return;

            int placed = 0;

            for (int i = 0; i < mountain.TrailCount && placed < maxFencePanels; i++)
            {
                Trail trail = mountain.TrailAt(i);
                if (trail == null || trail.spine == null || trail.spine.Count < 4) continue;

                for (int s = 1; s < trail.spine.Count - 1 && placed < maxFencePanels; s++)
                {
                    Vector3 here = trail.spine[s];
                    Vector3 along = trail.spine[s + 1] - trail.spine[s - 1];
                    along.y = 0f;

                    if (along.sqrMagnitude < 0.01f) continue;

                    float yaw = Quaternion.LookRotation(along.normalized, Vector3.up).eulerAngles.y;
                    Vector3 across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

                    for (int side = -1; side <= 1 && placed < maxFencePanels; side += 2)
                    {
                        Vector3 edge = here + across * (side * (trail.halfWidth + 1.2f));
                        Vector3 beyond = edge + across * (side * fenceReach);

                        float lip = mountain.SampleHeight(edge.x, edge.z);
                        float out_ = mountain.SampleHeight(beyond.x, beyond.z);

                        if (lip - out_ < fenceDrop) continue;

                        Place(batch, panel, edge, yaw, 1f, 1f);
                        placed++;
                    }
                }
            }
        }
    }
}
