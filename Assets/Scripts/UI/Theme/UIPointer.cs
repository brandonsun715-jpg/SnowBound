using System.Collections.Generic;
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

        static readonly List<RectTransform> _blockers = new List<RectTransform>();

        /// <summary>
        /// Panels that swallow clicks. The world selection raycast asks this
        /// before firing, so clicking a button never also selects whatever
        /// happens to be behind it.
        /// </summary>
        public static void Block(RectTransform rect)
        {
            if (rect != null && !_blockers.Contains(rect)) _blockers.Add(rect);
        }

        public static void Unblock(RectTransform rect)
        {
            _blockers.Remove(rect);
        }

        public static bool OverInterface
        {
            get
            {
                for (int i = _blockers.Count - 1; i >= 0; i--)
                {
                    RectTransform rect = _blockers[i];
                    if (rect == null) { _blockers.RemoveAt(i); continue; }
                    if (!rect.gameObject.activeInHierarchy) continue;
                    if (Over(rect)) return true;
                }
                return false;
            }
        }

        public static bool Over(RectTransform rect)
        {
            if (rect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Position, null);
        }
    }
}
