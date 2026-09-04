using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Marks a collider as something that barely slows you down: the top of a
    /// park box, a rail, ice. Snow drags; steel and plastic do not.
    ///
    /// Sits alongside SnowSurface rather than replacing it, because the two
    /// answer different questions. SnowSurface is "does this leave a track?"
    /// and this is "how much does it hold you back?".
    /// </summary>
    public class SlickSurface : MonoBehaviour
    {
        [Tooltip("Multiplier on snow friction. 1 is normal snow, 0 is frictionless.")]
        [Range(0f, 1f)] public float frictionScale = 0.15f;
    }
}
