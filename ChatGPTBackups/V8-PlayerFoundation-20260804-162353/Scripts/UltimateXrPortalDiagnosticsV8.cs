using System.Collections;
using UnityEngine;
using UltimateXR.Avatar;
using UltimateXR.Manipulation;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8
{
    [DefaultExecutionOrder(1000)]
    public sealed class UltimateXrPortalDiagnosticsV8 : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;

            UxrAvatar[] avatars =
                FindObjectsOfType<UxrAvatar>(true);

            UxrGrabber[] grabbers =
                FindObjectsOfType<UxrGrabber>(true);

            UxrGrabbableObject[] grabbables =
                FindObjectsOfType<UxrGrabbableObject>(true);

            ActionBasedController[] oldControllers =
                FindObjectsOfType<ActionBasedController>(true);

            XRDirectInteractor[] oldDirectInteractors =
                FindObjectsOfType<XRDirectInteractor>(true);

            XRGrabInteractable[] oldGrabbables =
                FindObjectsOfType<XRGrabInteractable>(true);

            bool ready =
                avatars.Length == 1 &&
                grabbers.Length >= 2 &&
                grabbables.Length >= 6 &&
                oldControllers.Length == 0 &&
                oldDirectInteractors.Length == 0 &&
                oldGrabbables.Length == 0;

            string counts =
                "UltimateXR avatars=" + avatars.Length +
                ", grabbers=" + grabbers.Length +
                ", grabbables=" + grabbables.Length +
                ", old XRI controllers=" + oldControllers.Length +
                ", old XRI direct interactors=" +
                    oldDirectInteractors.Length +
                ", old XRI grabbables=" + oldGrabbables.Length;

            if (!ready)
            {
                Debug.LogError(
                    "[Portal Foundation V8 Full UltimateXR] " +
                    "Conversion is incomplete: " +
                    counts +
                    ". Run Doctor Who VR > Portal Foundation V8 > " +
                    "Convert Current V8 to Full UltimateXR.");
                yield break;
            }

            Debug.Log(
                "[Portal Foundation V8 Full UltimateXR] Ready: " +
                counts +
                ". The old XRI player/grab system is not active.");
        }
    }
}
