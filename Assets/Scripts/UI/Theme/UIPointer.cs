using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SnowBound.Hud
{
    /// <summary>
    /// Where the mouse is and whether it was clicked.
    ///
    /// Deliberately not Unity's EventSystem. That needs an input module
    /// matched to whichever input backend the project was set up with, and
    /// picking the wrong one leaves every button in the game silently dead.
    /// Hit testing rectangles ourselves is a dozen lines and works on both.
    /// </summary>
    public static class UIPointer
    {
        public static Vector2 Position
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var mouse = Mouse.current;
                return mouse == null ? Vector2.zero : mouse.position.ReadValue();
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool Pressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool Held
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static bool Over(RectTransform rect)
        {
            if (rect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Position, null);
        }
    }
}
