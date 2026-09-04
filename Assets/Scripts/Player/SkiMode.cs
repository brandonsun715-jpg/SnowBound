using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Skis: sharp, quick and grippy. Two edges pointing the same way hold a
    /// line well, so turns are tight, the rider goes roughly where the tips
    /// point, and a snowplough stops you hard.
    /// </summary>
    public class SkiMode : SnowLocomotionMode
    {
        public override LocomotionKind Kind => LocomotionKind.Ski;

        public SnowRideSettings settings = new SnowRideSettings
        {
            snowFriction = 1.5f,
            airDrag = 0.020f,
            tuckAirDrag = 0.012f,
            pushSpeed = 3.5f,

            lateralGrip = 11f,
            carveScrub = 0.35f,

            maxTurnRate = 170f,
            turnSpeedFalloff = 0.060f,
            airSteerFactor = 0.30f,

            brakeStrength = 12f,
            brakeGrip = 3f,

            jumpSpeed = 6.5f,
            landingGrace = 0.60f,

            groundStick = 5f,
            alignSpeed = 10f,
            bankAngle = 22f,
            bankFullSpeed = 14f,
            bodyYawOffset = 0f
        };

        protected override SnowRideSettings S => settings;
    }
}
