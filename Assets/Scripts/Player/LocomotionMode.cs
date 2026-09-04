using UnityEngine;

namespace SnowBound.Player
{
    public enum LocomotionKind
    {
        Walk = 1,
        Ski = 2,
        Snowboard = 3
    }

    /// <summary>
    /// One way of moving. Walking is the only one so far; skiing and
    /// snowboarding are separate components that plug into the same slot,
    /// which is why they can feel completely different instead of being
    /// a walk controller wearing a hat.
    ///
    /// PlayerController owns the shared parts (gravity, ground probe, applying
    /// velocity). A mode only decides how velocity and facing should change.
    /// </summary>
    public abstract class LocomotionMode : MonoBehaviour
    {
        protected PlayerController Player { get; private set; }

        public abstract LocomotionKind Kind { get; }

        public void Bind(PlayerController player) { Player = player; }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }

        /// <summary>Called once per frame while this mode is active.</summary>
        public abstract void Tick(float dt);
    }
}
