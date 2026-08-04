using UnityEngine;

namespace DoctorWhoVR.StencilPortalV6
{
    /// <summary>
    /// Emits one clear error if a generated V6 portal scene is incomplete.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class StencilPortalDiagnostics : MonoBehaviour
    {
        private void Start()
        {
            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            PortalProxyTransform[] proxies =
                FindObjectsOfType<PortalProxyTransform>();

            StencilPortalTraveller[] travellers =
                FindObjectsOfType<StencilPortalTraveller>();

            if (portals.Length != 2 ||
                proxies.Length != 2 ||
                travellers.Length < 1)
            {
                Debug.LogError(
                    "[DoctorWhoVR Stencil Portal V6] " +
                    "Scene setup is incomplete. Expected two portals, " +
                    "two proxy rooms, and one XR traveller.");
                return;
            }

            Debug.Log(
                "[DoctorWhoVR Stencil Portal V6] " +
                "Direct-stereo stencil portal is active. " +
                "No portal RenderTextures or portal cameras are in use.");
        }
    }
}
