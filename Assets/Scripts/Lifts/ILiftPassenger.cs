using UnityEngine;
using SnowBound.Player;

namespace SnowBound.Lifts
{
    /// <summary>
    /// Anything that can sit on a chair. The lift does not care whether it is
    /// carrying the player or a guest, which is the whole point: one lift, one
    /// queue, one set of rules, and the crowd riding it is the same crowd the
    /// player queues behind.
    /// </summary>
    public interface ILiftPassenger
    {
        Transform Transform { get; }

        /// <summary>What they have on their feet, so they get off with it still on.</summary>
        LocomotionKind Gear { get; }

        /// <summary>True when they are stood in the loading area wanting a chair.</summary>
        bool WaitingToBoard { get; }

        void BoardLift(Transform seat, Vector3 seatOffset);

        void LeaveLift(Vector3 position, Vector3 facing, Vector3 velocity);
    }
}
