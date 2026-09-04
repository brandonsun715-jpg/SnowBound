using UnityEngine;
using SnowBound.Weather;

namespace SnowBound.Player
{
    /// <summary>
    /// Shared physics for anything you slide down a mountain on.
    ///
    /// The model is deliberately not "walking, but faster". The rider has a
    /// heading (where the edges point) and a velocity (where they are actually
    /// going), and those two are allowed to disagree. That gap is what
    /// produces momentum, carving and drifting:
    ///
    ///   - gravity pulls you along the slope, so steeper means faster
    ///   - the edges scrub off sideways drift, fast for skis, slowly for a board
    ///   - steering turns the heading, and the velocity follows only as fast
    ///     as the edges can drag it round
    ///   - turning hard costs speed, so a straight line is always faster
    ///
    /// Skiing and snowboarding are the same code with different numbers.
    /// </summary>
    public abstract class SnowLocomotionMode : LocomotionMode
    {
        protected abstract SnowRideSettings S { get; }

        public override float BodyYawOffset => S.bodyYawOffset;

        float _yaw;
        float _airLock;
        bool _wasGrounded;
        WeatherSystem _weather;

        /// <summary>Deep snow drags more and holds an edge better than hardpack.</summary>
        float DragScale { get { return _weather != null ? _weather.DragMultiplier : 1f; } }
        float GripScale { get { return _weather != null ? _weather.GripMultiplier : 1f; } }

        /// <summary>True when we are actually riding, not mid-jump.</summary>
        bool Riding => Player.IsGrounded && _airLock <= 0f;

        public override void OnEnter()
        {
            _yaw = Player.transform.eulerAngles.y;
            _airLock = 0f;
            _wasGrounded = Player.IsGrounded;
            // Velocity is deliberately kept, so swapping gear never teleports
            // your momentum away.
        }

        public override void OnExit()
        {
            Player.LateralSlip = 0f;
        }

        public override void Tick(float dt)
        {
            if (_airLock > 0f) _airLock -= dt;
            if (_weather == null) _weather = WeatherSystem.Instance;

            Vector2 move = Player.Input.Move;
            float steer = move.x;
            bool tuck = move.y > 0.1f;
            bool brake = move.y < -0.1f || Player.Input.BrakeHeld;

            float speed = Player.Velocity.magnitude;
            Vector3 normal = Riding ? Player.GroundNormal : Vector3.up;

            Steer(steer, speed, dt);
            Vector3 headingFlat = new Vector3(Mathf.Sin(_yaw * Mathf.Deg2Rad), 0f,
                                              Mathf.Cos(_yaw * Mathf.Deg2Rad));

            if (Riding) Ride(headingFlat, normal, steer, tuck, brake, dt);
            else Fly(dt);

            _wasGrounded = Riding;
            Align(headingFlat, normal, steer, speed, dt);
        }

        void Steer(float steer, float speed, float dt)
        {
            // Slower turning the faster you go. This is what stops the rider
            // spinning on the spot at 80 km/h and forces you to plan a line.
            float rate = S.maxTurnRate / (1f + speed * S.turnSpeedFalloff);
            if (!Riding) rate *= S.airSteerFactor;
            _yaw += steer * rate * dt;
        }

        void Ride(Vector3 headingFlat, Vector3 normal, float steer, bool tuck, bool brake, float dt)
        {
            // A frame of reference lying flat on the snow.
            Vector3 heading = Vector3.ProjectOnPlane(headingFlat, normal);
            if (heading.sqrMagnitude < 0.0001f) heading = headingFlat;
            heading.Normalize();
            Vector3 side = Vector3.Cross(normal, heading);

            Vector3 planar = Vector3.ProjectOnPlane(Player.Velocity, normal);
            float forward = Vector3.Dot(planar, heading);
            float sideways = Vector3.Dot(planar, side);

            if (!_wasGrounded) ApplyLanding(ref forward, ref sideways);

            // Edges bite first, so the pull of gravity across the slope always
            // survives one frame. That leaves a believable trickle of sideslip
            // instead of an unnaturally perfect edge hold.
            float grip = S.lateralGrip * GripScale * (brake ? S.brakeGrip : 1f);
            sideways *= Mathf.Exp(-grip * dt);

            Vector3 slopePull = Vector3.ProjectOnPlane(new Vector3(0f, Player.Gravity, 0f), normal);
            forward += Vector3.Dot(slopePull, heading) * dt;
            sideways += Vector3.Dot(slopePull, side) * dt;

            float speed = Mathf.Sqrt(forward * forward + sideways * sideways);
            if (speed > 0.01f)
            {
                float drag = tuck ? S.tuckAirDrag : S.airDrag;
                float loss = (S.snowFriction * DragScale + drag * speed * speed) * dt;
                loss += Mathf.Abs(steer) * speed * S.carveScrub * dt;
                if (brake) loss += S.brakeStrength * dt;

                float keep = Mathf.Max(0f, speed - loss) / speed;
                forward *= keep;
                sideways *= keep;
            }

            // Skating, so the flats and the lift queue are not a dead end.
            if (tuck && forward < S.pushSpeed)
                forward = Mathf.MoveTowards(forward, S.pushSpeed, 6f * dt);

            Player.LateralSlip = sideways;

            Vector3 ride = heading * forward + side * sideways;

            if (Player.Input.JumpPressed)
            {
                Player.Velocity = ride + Vector3.up * S.jumpSpeed;
                // Without this the ground probe still sees snow next frame and
                // would flatten the jump before it started.
                _airLock = 0.2f;
                return;
            }

            Player.Velocity = ride;
            Player.GroundStick = -normal * S.groundStick;
        }

        void ApplyLanding(ref float forward, ref float sideways)
        {
            // Landing across your edges costs speed; landing straight does not.
            float total = Mathf.Sqrt(forward * forward + sideways * sideways);
            if (total < 0.01f) return;

            float straightness = Mathf.Abs(forward) / total;
            float keep = Mathf.Lerp(S.landingGrace, 1f, straightness);
            forward *= keep;
            sideways *= keep;
        }

        void Fly(float dt)
        {
            Vector3 v = Player.Velocity;
            v.y += Player.Gravity * dt;
            Player.Velocity = v;
            Player.LateralSlip = 0f;
        }

        void Align(Vector3 headingFlat, Vector3 normal, float steer, float speed, float dt)
        {
            Vector3 up = Riding ? Vector3.Slerp(Vector3.up, normal, 0.85f) : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(headingFlat, up);
            if (forward.sqrMagnitude < 0.0001f) return;

            Quaternion want = Quaternion.LookRotation(forward.normalized, up);
            float lean = -steer * S.bankAngle * Mathf.Clamp01(speed / S.bankFullSpeed);
            want *= Quaternion.Euler(0f, 0f, lean);

            Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, want,
                                                         1f - Mathf.Exp(-S.alignSpeed * dt));
        }
    }
}
