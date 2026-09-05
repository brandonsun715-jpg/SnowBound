using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SnowBound.Player
{
    /// <summary>
    /// The only place in the game that touches the keyboard and mouse.
    /// Everything else asks this component for clean values, so swapping to
    /// gamepads or rebindable controls later changes this file and nothing else.
    ///
    /// Works with either Unity input backend. Unity defines ENABLE_INPUT_SYSTEM
    /// and ENABLE_LEGACY_INPUT_MANAGER from Project Settings > Player >
    /// Active Input Handling, so this compiles whichever one is selected.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Movement and actions. Switched off while riding the chairlift.")]
        public bool enableInput = true;
        [Tooltip("Looking around. Stays on while riding, because the view is\nhalf the point of a chairlift.")]
        public bool enableLook = true;
        [Tooltip("Held by the interface while a screen is open. Kept separate from\nthe gameplay flags so the two can never fight over them.")]
        public bool suspended;

        /// <summary>x = left/right, y = forward/back. Length never exceeds 1.</summary>
        public Vector2 Move
        {
            get
            {
                if (!enableInput || suspended) return Vector2.zero;
                Vector2 v = ReadMove();
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Mouse movement, already normalised so both backends feel the same.</summary>
        public Vector2 Look => enableLook && !suspended ? ReadLook() : Vector2.zero;

        public float Zoom => enableLook && !suspended ? ReadZoom() : 0f;
        public bool JumpPressed => enableInput && !suspended && ReadJump();
        public bool SprintHeld => enableInput && !suspended && ReadSprint();
        public bool BrakeHeld => enableInput && !suspended && ReadBrake();

        /// <summary>1 = walk, 2 = ski, 3 = snowboard. -1 when nothing pressed.</summary>
        public int GearPressed => enableInput && !suspended ? ReadGear() : -1;

        /// <summary>V steps the weather on. Works while riding the lift too.</summary>
        public bool WeatherPressed => ReadWeather();

        /// <summary>Dismisses whatever is waiting on the player. Never gated,
        /// because it is what un-gates everything else.</summary>
        public bool ContinuePressed => ReadContinue();

        /// <summary>Opens and closes the resort dashboard. Never gated.</summary>
        public bool ManagementPressed => ReadManagement();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER

        static Vector2 ReadMove()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }

        static Vector2 ReadLook()
        {
            var m = Mouse.current;
            // 0.1 matches the scale the old Input Manager reported.
            return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
        }

        static float ReadZoom()
        {
            var m = Mouse.current;
            return m == null ? 0f : m.scroll.ReadValue().y * 0.0008f;
        }

        static bool ReadJump()
        {
            var k = Keyboard.current;
            return k != null && k.spaceKey.wasPressedThisFrame;
        }

        static bool ReadSprint()
        {
            var k = Keyboard.current;
            return k != null && k.leftShiftKey.isPressed;
        }

        static bool ReadBrake()
        {
            var k = Keyboard.current;
            return k != null && k.leftCtrlKey.isPressed;
        }

        static int ReadGear()
        {
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.digit1Key.wasPressedThisFrame) return 1;
            if (k.digit2Key.wasPressedThisFrame) return 2;
            if (k.digit3Key.wasPressedThisFrame) return 3;
            return -1;
        }

        static bool ReadWeather()
        {
            var k = Keyboard.current;
            return k != null && k.vKey.wasPressedThisFrame;
        }

        static bool ReadContinue()
        {
            var k = Keyboard.current;
            if (k == null) return false;
            return k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame;
        }

        static bool ReadManagement()
        {
            var k = Keyboard.current;
            return k != null && k.tabKey.wasPressedThisFrame;
        }

#else

        static Vector2 ReadMove()
        {
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        static Vector2 ReadLook()
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        static float ReadZoom() { return Input.GetAxis("Mouse ScrollWheel"); }
        static bool ReadJump() { return Input.GetKeyDown(KeyCode.Space); }
        static bool ReadSprint() { return Input.GetKey(KeyCode.LeftShift); }
        static bool ReadBrake() { return Input.GetKey(KeyCode.LeftControl); }

        static int ReadGear()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
            return -1;
        }

        static bool ReadWeather() { return Input.GetKeyDown(KeyCode.V); }

        static bool ReadContinue()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        }

        static bool ReadManagement() { return Input.GetKeyDown(KeyCode.Tab); }

#endif
    }
}
