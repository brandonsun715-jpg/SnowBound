using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Snowboard: one edge, ridden side-on. It turns slower and grips about
    /// half as hard as skis, so the board washes out into long drifting arcs
    /// instead of biting a tight line. Braking is a heel-side slide, which
    /// scrubs sideways much harder than a snowplough does.
    /// </summary>
    public class SnowboardMode : SnowLocomotionMode
    {
        public override LocomotionKind Kind => LocomotionKind.Snowboard;

        public SnowRideSettings settings = new SnowRideSettings
        {
            snowFriction = 1.7f,
            airDrag = 0.018f,
            tuckAirDrag = 0.011f,
            pushSpeed = 2.5f,

            lateralGrip = 6f,
            carveScrub = 0.50f,

            maxTurnRate = 110f,
            turnSpeedFalloff = 0.050f,
            airSteerFactor = 0.40f,

            brakeStrength = 14f,
            brakeGrip = 4f,

            jumpSpeed = 7f,
            landingGrace = 0.55f,

            groundStick = 5f,
            alignSpeed = 8f,
            bankAngle = 30f,
            bankFullSpeed = 12f,
            bodyYawOffset = 65f
        };

        protected override SnowRideSettings S => settings;
    }
}
