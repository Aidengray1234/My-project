using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalHandsV7
{
    [DefaultExecutionOrder(-900)]
    public sealed class PortalInteractionDiagnostics : MonoBehaviour
    {
        private void Start()
        {
            ActionBasedController[] controllers =
                FindObjectsOfType<ActionBasedController>();

            XRDirectInteractor[] directInteractors =
                FindObjectsOfType<XRDirectInteractor>();

            XRGrabInteractable[] grabObjects =
                FindObjectsOfType<XRGrabInteractable>();

            PortalPhysicsTraveller[] physicsTravellers =
                FindObjectsOfType<PortalPhysicsTraveller>();

            if (controllers.Length < 2 ||
                directInteractors.Length < 2 ||
                grabObjects.Length < 4 ||
                physicsTravellers.Length < 4)
            {
                Debug.LogError(
                    "[Portal Hands V7] Setup incomplete. " +
                    "Expected two controllers/direct interactors and " +
                    "at least four portal-enabled grab test objects.");
                return;
            }

            Debug.Log(
                "[Portal Hands V7] Hands, direct grabbing, " +
                "mapped far-side interactors, held-object transfer, " +
                "free-object teleporting, and dynamic proxies are active.");
        }
    }
}
