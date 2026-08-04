using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8
{
    [DefaultExecutionOrder(1000)]
    public sealed class PortalFoundationDiagnosticsV8 : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;

            ActionBasedController[] controllers =
                FindObjectsOfType<ActionBasedController>(true);

            PortalReachBridgeV8[] reachBridges =
                FindObjectsOfType<PortalReachBridgeV8>(true);

            XRGrabInteractable[] grabs =
                FindObjectsOfType<XRGrabInteractable>(true);

            PortalRigidbodyTravellerV8[] objects =
                FindObjectsOfType<PortalRigidbodyTravellerV8>(true);

            AdaptiveControllerHandPoseV8[] hands =
                FindObjectsOfType<AdaptiveControllerHandPoseV8>(true);

            bool ready =
                controllers.Length >= 2 &&
                reachBridges.Length >= 2 &&
                hands.Length >= 2 &&
                grabs.Length >= 5 &&
                objects.Length >= 5;

            string counts =
                "controllers=" + controllers.Length +
                ", hands=" + hands.Length +
                ", reach bridges=" + reachBridges.Length +
                ", grabs=" + grabs.Length +
                ", portal objects=" + objects.Length;

            if (!ready)
            {
                Debug.LogError(
                    "[Portal Foundation V8] Setup incomplete: " +
                    counts +
                    ". Use Doctor Who VR > Portal Foundation V8 > " +
                    "Rebuild Clean V8 Scene.");
                yield break;
            }

            Debug.Log(
                "[Portal Foundation V8] Clean interaction foundation ready: " +
                counts +
                ". Player, held-object, free-rigidbody, mapped-hand, " +
                "CharacterController, and NavMeshAgent portal paths are active.");
        }
    }
}
