using UnityEngine;

namespace SnowBound.Resort
{
    /// <summary>What the place is called. Read by the interface, nothing else.</summary>
    public class ResortIdentity : MonoBehaviour
    {
        public string resortName = "Snowbound";
        public string mountainName = "Larch Peak";

        public static ResortIdentity Instance
        {
            get { return FindAnyObjectByType<ResortIdentity>(); }
        }
    }
}
