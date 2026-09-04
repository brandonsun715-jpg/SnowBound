using UnityEngine;

namespace SnowBound.Player
{
    /// <summary>
    /// Boots on. Move relative to the camera, turn to face where you are
    /// going, jump, fall. No momentum worth speaking of — that is what
    /// makes skiing feel different later.
    /// </summary>
    public class WalkMode : LocomotionMode
    {
        public override LocomotionKind Kind => LocomotionKind.Walk;

        [Header("Speed (metres per second)")]
        public float walkSpeed = 4.5f;
        public float runSpeed = 7.5f;

        [Header("Feel")]
        public float groundAcceleration = 35f;
        public float airAcceleration = 8f;
        public float turnSpeed = 900f;
        [Tooltip("Metres. How high a standing jump goes.")]
        public float jumpHeight = 1.1f;

        public override void OnEnter()
        {
            // Snow modes tilt the body to the slope. Stand back up.
            Vector3 flat = Player.transform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                Player.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        public override void Tick(float dt)
        {
            Vector2 move = Player.Input.Move;
            Vector3 wish = Player.CameraForward * move.y + Player.CameraRight * move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float topSpeed = Player.Input.SprintHeld ? runSpeed : walkSpeed;

            Vector3 horizontal = new Vector3(Player.Velocity.x, 0f, Player.Velocity.z);
            float accel = Player.IsGrounded ? groundAcceleration : airAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, wish * topSpeed, accel * dt);

            float vertical = Player.Velocity.y;

            if (Player.IsGrounded && vertical <= 0f)
            {
                // A small downward bias keeps the feet pinned to slopes and
                // stops the controller ticking between grounded and airborne.
                vertical = -2f;

                if (Player.Input.JumpPressed)
                    vertical = Mathf.Sqrt(2f * jumpHeight * -Player.Gravity);
            }
            else
            {
                vertical += Player.Gravity * dt;
            }

            Player.Velocity = new Vector3(horizontal.x, vertical, horizontal.z);

            if (wish.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(wish, Vector3.up);
                Player.transform.rotation =
                    Quaternion.RotateTowards(Player.transform.rotation, want, turnSpeed * dt);
            }
        }
    }
}
