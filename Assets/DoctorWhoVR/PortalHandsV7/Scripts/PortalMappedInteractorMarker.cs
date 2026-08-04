using UnityEngine;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Marker used to distinguish the far-side mapped hand interactor from
    /// the normal controller interactor during whole-rig portal traversal.
    /// </summary>
    public sealed class PortalMappedInteractorMarker : MonoBehaviour
    {
        public PortalHandInteractorBridge Owner;
    }
}
