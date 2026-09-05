#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SnowBound.EditorTools
{
    /// <summary>
    /// Sets the Game view to 1920x1080 at 1x.
    ///
    /// The single most common reason a Unity game looks like a low-resolution
    /// mess is the Scale slider at the top of the Game view. Anything above
    /// 1x renders at a fraction of the window and magnifies the result, and
    /// on a Retina Mac "Low Resolution Aspect Ratios" halves it again. Neither
    /// has anything to do with the game, and neither can be set from a normal
    /// API — so this reaches into the editor's own Game view through
    /// reflection and sets them.
    ///
    /// Every step is optional and reported. If Unity has moved something and
    /// a step cannot be done, it says exactly which switch to flick by hand
    /// rather than failing silently.
    /// </summary>
    public static class GameViewFixer
    {
        const int Width = 1920;
        const int Height = 1080;

        [MenuItem("SnowBound/Fix Game View (1080p at 1x)", false, 41)]
        public static void Fix()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Manual("this version of Unity does not expose its Game view");
                return;
            }

            EditorWindow view = EditorWindow.GetWindow(gameViewType, false, "Game", true);
            if (view == null)
            {
                Manual("the Game view would not open");
                return;
            }

            bool sized = SelectFixedResolution(gameViewType, view);
            bool zoomed = ResetZoom(gameViewType, view);
            bool sharp = TurnOffLowResolution();

            view.Repaint();

            if (sized && zoomed && sharp)
            {
                Debug.Log("[SnowBound] Game view set to " + Width + "x" + Height +
                          " at 1x, low-resolution aspect ratios off.");
                return;
            }

            Manual((sized ? "" : "the resolution, ") +
                   (zoomed ? "" : "the Scale slider, ") +
                   (sharp ? "" : "low-resolution aspect ratios, ") + "could not be set");
        }

        static void Manual(string what)
        {
            Debug.LogWarning(
                "[SnowBound] Could not set the Game view automatically (" + what + ").\n" +
                "Set it by hand, at the top of the Game window:\n" +
                "  1. The aspect dropdown on the left: choose 1920x1080, or Free Aspect.\n" +
                "  2. The Scale slider: drag it all the way left, to 1x.\n" +
                "  3. The three-dot menu on the right: untick Low Resolution Aspect Ratios.\n" +
                "The Scale slider is the one that matters. Above 1x the game renders at a " +
                "fraction of the resolution and is magnified, which is what looks pixelated.");
        }

        // ---------------- the pieces ----------------------------------------

        /// <summary>Add a fixed 1920x1080 entry if it is missing, then pick it.</summary>
        static bool SelectFixedResolution(Type gameViewType, EditorWindow view)
        {
            try
            {
                Assembly editor = typeof(Editor).Assembly;

                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                Type singleton = editor.GetType("UnityEditor.ScriptableSingleton`1");
                Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType");

                if (sizesType == null || singleton == null || sizeType == null || sizeKind == null)
                    return false;

                object sizes = singleton.MakeGenericType(sizesType)
                                        .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                                        .GetValue(null, null);

                object group = sizesType.GetProperty("currentGroup").GetValue(sizes, null);
                Type groupType = group.GetType();

                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                int found = -1;

                for (int i = 0; i < total; i++)
                {
                    object size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });

                    int w = (int)sizeType.GetProperty("width").GetValue(size, null);
                    int h = (int)sizeType.GetProperty("height").GetValue(size, null);

                    if (w != Width || h != Height) continue;

                    found = i;
                    break;
                }

                if (found < 0)
                {
                    object fixedKind = Enum.Parse(sizeKind, "FixedResolution");

                    ConstructorInfo make = sizeType.GetConstructor(
                        new[] { sizeKind, typeof(int), typeof(int), typeof(string) });

                    if (make == null) return false;

                    object entry = make.Invoke(new[] { fixedKind, (object)Width, Height, "SnowBound 1080p" });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { entry });

                    found = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }

                MethodInfo select = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (select == null) return false;

                select.Invoke(view, new object[] { found, null });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Drag the Scale slider back to 1x. This is the one that matters.</summary>
        static bool ResetZoom(Type gameViewType, EditorWindow view)
        {
            try
            {
                // Newer editors keep it behind a property; older ones expose the
                // zoom area directly. Try both before giving up.
                PropertyInfo scale = gameViewType.GetProperty("defaultScale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                MethodInfo snap = gameViewType.GetMethod("SnapZoom",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (snap != null)
                {
                    float one = scale != null ? (float)scale.GetValue(view, null) : 1f;
                    snap.Invoke(view, new object[] { one });
                    return true;
                }

                FieldInfo zoomField = gameViewType.GetField("m_ZoomArea",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (zoomField == null) return false;

                object zoom = zoomField.GetValue(view);
                if (zoom == null) return false;

                PropertyInfo zoomScale = zoom.GetType().GetProperty("scale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (zoomScale == null || !zoomScale.CanWrite) return false;

                zoomScale.SetValue(zoom, Vector2.one, null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The Retina halving. Off means the window renders at its real size.</summary>
        static bool TurnOffLowResolution()
        {
            try
            {
                Type settings = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                if (settings == null) return false;

                // It lives on EditorPrefs in every version that has it.
                EditorPrefs.SetBool("GameView.LowResAspectRatios", false);

                PropertyInfo flag = typeof(EditorApplication).GetProperty("useLowResolutionAspectRatios",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (flag != null && flag.CanWrite) flag.SetValue(null, false, null);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif
