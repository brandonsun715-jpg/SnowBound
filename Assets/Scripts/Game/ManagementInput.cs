using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SnowBound.Game
{
    /// <summary>
    /// Camera input for management mode. Kept apart from PlayerInputReader on
    /// purpose: the two modes read the same keys to mean different things, and
    /// one file trying to serve both is how you end up panning the map while
    /// skiing.
    /// </summary>
    public static class ManagementInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER

        public static Vector2 Pan
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;

                float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                        - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                        - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);

                return new Vector2(x, y);
            }
        }

        public static float Zoom
        {
            get
            {
                var m = Mouse.current;
                return m == null ? 0f : m.scroll.ReadValue().y * 0.0008f;
            }
        }

        public static bool RotateHeld
        {
            get
            {
                var m = Mouse.current;
                return m != null && m.middleButton.isPressed;
            }
        }

        public static Vector2 MouseDelta
        {
            get
            {
                var m = Mouse.current;
                return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
            }
        }

        public static bool BackPressed
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.escapeKey.wasPressedThisFrame;
            }
        }

        public static bool RotatePressed
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.rKey.wasPressedThisFrame;
            }
        }

        /// <summary>
        /// Right mouse. Escape is deliberately not here: one press should undo
        /// one thing, and HudDirector is the only place that knows the order.
        /// </summary>
        public static bool CancelPressed
        {
            get
            {
                var m = Mouse.current;
                return m != null && m.rightButton.wasPressedThisFrame;
            }
        }

#else

        public static Vector2 Pan
        {
            get { return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); }
        }

        public static float Zoom { get { return Input.GetAxis("Mouse ScrollWheel"); } }
        public static bool RotateHeld { get { return Input.GetMouseButton(2); } }

        public static Vector2 MouseDelta
        {
            get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        }

        public static bool BackPressed { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool RotatePressed { get { return Input.GetKeyDown(KeyCode.R); } }

        public static bool CancelPressed { get { return Input.GetMouseButtonDown(1); } }

#endif
    }
}
