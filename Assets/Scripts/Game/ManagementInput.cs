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
    ///
    /// The right mouse button does two jobs, which is fine because they are
    /// distinguishable: dragging it looks around, and pressing and releasing
    /// it without moving cancels whatever you were placing. The drag distance
    /// is what tells them apart, so this keeps a little state and refreshes it
    /// once a frame however many times it is asked.
    /// </summary>
    public static class ManagementInput
    {
        [Tooltip("Pixels of movement before a right click counts as a drag.")]
        const float DragThreshold = 6f;

        static int _frame = -1;
        static bool _rightHeld, _middleHeld;
        static float _dragged;
        static bool _cancelled;

        static void Pump()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;

            bool right = RawRightHeld;
            bool middle = RawMiddleHeld;

            _cancelled = false;

            if (right && !_rightHeld) _dragged = 0f;
            else if (right) _dragged += RawMouseDelta.magnitude;
            else if (_rightHeld) _cancelled = _dragged < DragThreshold;

            _rightHeld = right;
            _middleHeld = middle;
        }

        /// <summary>Hold either mouse button to look around.</summary>
        public static bool LookHeld
        {
            get { Pump(); return _rightHeld || _middleHeld; }
        }

        /// <summary>Right click, pressed and released without moving.</summary>
        public static bool CancelPressed
        {
            get { Pump(); return _cancelled; }
        }

        /// <summary>Kept for anything that still wants only the middle button.</summary>
        public static bool RotateHeld { get { Pump(); return _middleHeld; } }

        public static Vector2 MouseDelta { get { return RawMouseDelta; } }

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

        /// <summary>Q and E, or the scroll wheel: straight up and straight down.</summary>
        public static float Lift
        {
            get
            {
                var k = Keyboard.current;
                float keys = k == null ? 0f
                    : (k.eKey.isPressed ? 1f : 0f) - (k.qKey.isPressed ? 1f : 0f);

                return keys + Zoom * 90f;
            }
        }

        public static bool Faster
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.leftShiftKey.isPressed;
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

        static bool RawRightHeld
        {
            get { var m = Mouse.current; return m != null && m.rightButton.isPressed; }
        }

        static bool RawMiddleHeld
        {
            get { var m = Mouse.current; return m != null && m.middleButton.isPressed; }
        }

        static Vector2 RawMouseDelta
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

#else

        public static Vector2 Pan
        {
            get { return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); }
        }

        public static float Lift
        {
            get
            {
                float keys = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
                return keys + Zoom * 90f;
            }
        }

        public static bool Faster { get { return Input.GetKey(KeyCode.LeftShift); } }

        public static float Zoom { get { return Input.GetAxis("Mouse ScrollWheel"); } }

        static bool RawRightHeld { get { return Input.GetMouseButton(1); } }
        static bool RawMiddleHeld { get { return Input.GetMouseButton(2); } }

        static Vector2 RawMouseDelta
        {
            get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        }

        public static bool BackPressed { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool RotatePressed { get { return Input.GetKeyDown(KeyCode.R); } }

#endif
    }
}
