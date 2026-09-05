using UnityEngine;
using SnowBound.Mountain;

namespace SnowBound.Player
{
    /// <summary>
    /// The net under the physics.
    ///
    /// Nothing here is a substitute for real collision — the terrain has a
    /// collider that matches its mesh exactly, and that is what stops the
    /// player. This exists for the cases collision cannot cover: the frame
    /// after the ground under your feet is sculpted away, a landing that
    /// resolves the wrong side of a face, a body that has been teleported by
    /// something else, or a run off the edge of the map.
    ///
    /// It is deliberately reluctant. A jump is not a failure, so it will not
    /// interrupt one; it acts only when the body is genuinely underneath the
    /// surface or genuinely outside the world, and then it puts it back on
    /// the snow at the same place rather than somewhere arbitrary.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class TerrainGuard : MonoBehaviour
    {
        public MountainGenerator mountain;
        public PlayerController player;

        [Header("Under the world")]
        [Tooltip("How far below the surface counts as being inside the mountain.")]
        public float buriedDepth = 1.6f;
        [Tooltip("How long it has to stay buried before this steps in.")]
        public float buriedGrace = 0.25f;

        [Header("Off the map")]
        [Tooltip("How far outside the map edge is allowed before it is a fall.")]
        public float edgeMargin = 4f;
        [Tooltip("Anything this far below the lowest ground is lost, whatever else is true.")]
        public float voidDepth = 120f;

        [Header("Reporting")]
        public bool logRecoveries = true;

        /// <summary>How many times this has had to step in. Useful while testing.</summary>
        public int Recoveries { get; private set; }

        float _buriedFor;
        float _lastRecovery = -99f;

        void Start()
        {
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (player == null) player = GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            if (mountain == null || player == null || !mountain.Ready) return;

            // A lift carries the body; it is meant to be off the ground.
            if (player.IsRiding) { _buriedFor = 0f; return; }

            Vector3 at = transform.position;

            if (Lost(at)) { Recover(at, "fell out of the world"); return; }

            bool outside = !mountain.InsideWorld(at.x, at.z, edgeMargin);
            if (outside) { Recover(at, "left the resort"); return; }

            float ground = mountain.SampleHeight(at.x, at.z);
            bool buried = at.y < ground - buriedDepth;

            if (!buried) { _buriedFor = 0f; return; }

            // Being briefly inside the surface is normal — a landing resolves
            // over a frame or two. Being inside it for a quarter of a second
            // is not.
            _buriedFor += Time.deltaTime;
            if (_buriedFor < buriedGrace) return;

            Recover(at, "ended up inside the mountain");
        }

        bool Lost(Vector3 at)
        {
            // The mountain never goes below zero, so this is unambiguous.
            return at.y < -voidDepth || float.IsNaN(at.y);
        }

        void Recover(Vector3 at, string why)
        {
            _buriedFor = 0f;

            // Do not fight with the recovery that just happened.
            if (Time.time - _lastRecovery < 0.5f) return;
            _lastRecovery = Time.time;
            Recoveries++;

            Vector3 safe = mountain.ClampToWorld(at, edgeMargin + 8f);
            if (float.IsNaN(safe.x) || float.IsNaN(safe.z)) safe = new Vector3(0f, 0f, 30f);

            safe.y = mountain.SampleHeight(safe.x, safe.z) + 1.2f;

            player.Teleport(safe);
            player.Velocity = Vector3.zero;

            if (logRecoveries)
                Debug.LogWarning("[TerrainGuard] Put the player back on the snow: " + why + ".", this);
        }
    }
}
