using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Marks a collider as snow you can carve. Nothing else: it exists so
    /// spray and tracks can tell the piste apart from a wooden deck, a lift
    /// tower or a rock.
    ///
    /// Put it on anything that should leave a track. Groomed pistes, snow
    /// ramps and the summit all want one; roofs and boulders do not.
    /// </summary>
    public class SnowSurface : MonoBehaviour
    {
    }
}
