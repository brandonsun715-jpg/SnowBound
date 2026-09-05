using UnityEngine;
using SnowBound.Player;

namespace SnowBound.Lifts
{
    /// <summary>
    /// One chair. It carries no logic of its own: the Chairlift moves it and
    /// decides who is sitting in it. All it knows is where it is on the loop
    /// and who is aboard.
    /// </summary>
    public class ChairliftChair : MonoBehaviour
    {
        [Tooltip("Where a rider is placed. Set by the lift when it builds.")]
        public Transform seat;

        [Tooltip("This chair's fixed place in the queue, in metres round the loop.")]
        public float loopOffset;

        [Tooltip("A surface lift tows its rider along the snow instead of carrying them.")]
        public bool towed;

        public ILiftPassenger occupant;

        [Tooltip("What the rider had on their feet when they got on.")]
        public LocomotionKind riderGear = LocomotionKind.Ski;

        public bool IsFree { get { return occupant == null; } }
    }
}
