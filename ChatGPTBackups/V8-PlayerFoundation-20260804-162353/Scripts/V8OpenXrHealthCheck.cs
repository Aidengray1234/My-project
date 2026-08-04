using System.Collections;
using System.Collections.Generic;
using UltimateXR.Avatar;
using UltimateXR.Devices;
using UltimateXR.Locomotion;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Prints useful controller and cleanup information after XR initializes.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public sealed class V8OpenXrHealthCheck : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // XR devices can appear a few frames after scene startup.
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);

            List<InputDevice> devices =
                new List<InputDevice>();

            InputDevices.GetDevices(devices);

            List<string> controllerDescriptions =
                new List<string>();

            foreach (InputDevice device in devices)
            {
                InputDeviceCharacteristics characteristics =
                    device.characteristics;

                bool isController =
                    (characteristics &
                     InputDeviceCharacteristics.Controller) != 0 ||
                    (characteristics &
                     InputDeviceCharacteristics.HeldInHand) != 0;

                if (isController)
                {
                    controllerDescriptions.Add(
                        device.name +
                        " [" +
                        characteristics +
                        "]");
                }
            }

            UxrAvatar avatar =
                FindObjectOfType<UxrAvatar>();

            UxrAnyOpenXrControllerInputV8 input =
                FindObjectOfType<
                    UxrAnyOpenXrControllerInputV8>();

            UxrAnyOpenXrControllerTrackingV8 tracking =
                FindObjectOfType<
                    UxrAnyOpenXrControllerTrackingV8>();

            UxrSmoothLocomotion locomotion =
                FindObjectOfType<UxrSmoothLocomotion>();

            ActionBasedController[] oldControllers =
                FindObjectsOfType<ActionBasedController>(true);

            XRBaseInteractor[] oldInteractors =
                FindObjectsOfType<XRBaseInteractor>(true);

            LineRenderer[] lines =
                FindObjectsOfType<LineRenderer>(true);

            string connectedControllers =
                controllerDescriptions.Count > 0
                    ? string.Join(", ", controllerDescriptions)
                    : "none";

            bool leftEnabled =
                input != null &&
                input.IsControllerEnabled(
                    UltimateXR.Core.UxrHandSide.Left);

            bool rightEnabled =
                input != null &&
                input.IsControllerEnabled(
                    UltimateXR.Core.UxrHandSide.Right);

            bool ready =
                avatar != null &&
                input != null &&
                input.enabled &&
                tracking != null &&
                tracking.enabled &&
                locomotion != null &&
                locomotion.enabled &&
                leftEnabled &&
                rightEnabled &&
                oldControllers.Length == 0 &&
                oldInteractors.Length == 0 &&
                lines.Length == 0;

            string status =
                "controllers={" +
                connectedControllers +
                "}, generic input=" +
                (input != null && input.enabled) +
                ", left=" +
                leftEnabled +
                ", right=" +
                rightEnabled +
                ", tracking=" +
                (tracking != null && tracking.enabled) +
                ", locomotion=" +
                (locomotion != null && locomotion.enabled) +
                ", old XRI controllers=" +
                oldControllers.Length +
                ", old XRI interactors=" +
                oldInteractors.Length +
                ", line renderers=" +
                lines.Length;

            if (ready)
            {
                Debug.Log(
                    "[V8 OpenXR Fix] Ready: " + status);
            }
            else
            {
                Debug.LogError(
                    "[V8 OpenXR Fix] Not ready: " +
                    status);
            }
        }
    }
}
