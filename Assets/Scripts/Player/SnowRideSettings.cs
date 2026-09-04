using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Every number that decides how a pair of skis or a snowboard feels.
    /// Skiing and snowboarding run the same physics and differ only here,
    /// which is what makes them comparable but distinct.
    /// </summary>
    [System.Serializable]
    public class SnowRideSettings
    {
        [Header("Speed")]
        [Tooltip("Constant drag from the snow, m/s^2. Higher = slower run-outs.")]
        public float snowFriction = 1.5f;
        [Tooltip("Air resistance. Sets the top speed together with the slope angle.")]
        public float airDrag = 0.020f;
        [Tooltip("Air resistance while holding W (tuck). Lower = faster.")]
        public float tuckAirDrag = 0.012f;
        [Tooltip("Speed you can skate up to on flat ground by holding W.")]
        public float pushSpeed = 3.5f;

        [Header("Edges")]
        [Tooltip("How fast sideways drift is scrubbed off. High = carves, low = slides.")]
        public float lateralGrip = 11f;
        [Tooltip("Speed lost per second of hard turning. Turning is a real choice.")]
        public float carveScrub = 0.35f;

        [Header("Turning")]
        [Tooltip("Degrees per second when standing still.")]
        public float maxTurnRate = 170f;
        [Tooltip("How much speed blunts the turn rate. Higher = wider arcs when fast.")]
        public float turnSpeedFalloff = 0.06f;
        [Tooltip("Fraction of the turn rate available in mid-air.")]
        public float airSteerFactor = 0.3f;

        [Header("Braking")]
        public float brakeStrength = 12f;
        [Tooltip("Grip multiplier while braking. This is the sideways scrub of a stop.")]
        public float brakeGrip = 3f;

        [Header("Air")]
        [Tooltip("Metres per second straight up. 6.5 is about a 0.85 m hop.")]
        public float jumpSpeed = 6.5f;
        [Tooltip("Speed kept after landing badly sideways. 1 = no penalty.")]
        public float landingGrace = 0.6f;

        [Header("Feel")]
        [Tooltip("Downward push that keeps the rider on the snow between bumps.")]
        public float groundStick = 5f;
        [Tooltip("How quickly the body swings to the new slope and lean.")]
        public float alignSpeed = 10f;
        [Tooltip("Degrees the rider leans into a turn.")]
        public float bankAngle = 22f;
        [Tooltip("Speed at which the lean reaches its full angle.")]
        public float bankFullSpeed = 14f;
        [Tooltip("Degrees the body is turned away from the direction of travel.")]
        public float bodyYawOffset = 0f;
    }
}
