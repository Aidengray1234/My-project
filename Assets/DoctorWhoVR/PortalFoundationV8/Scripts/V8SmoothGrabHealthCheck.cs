using System.Collections;
using System.Collections.Generic;
using UltimateXR.Avatar;
using UltimateXR.Locomotion;
using UltimateXR.Manipulation;
using UnityEngine;
using UnityEngine.XR;

namespace DoctorWhoVR.PortalFoundationV8
{
    [DefaultExecutionOrder(2200)]
    public sealed class V8SmoothGrabHealthCheck : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.35f);

            List<InputDevice> devices =
                new List<InputDevice>();

            InputDevices.GetDevices(devices);

            List<string> controllerNames =
                new List<string>();

            foreach (InputDevice device in devices)
            {
                InputDeviceCharacteristics characteristics =
                    device.characteristics;

                if ((characteristics &
                     InputDeviceCharacteristics.Controller) != 0 ||
                    (characteristics &
                     InputDeviceCharacteristics.HeldInHand) != 0)
                {
                    controllerNames.Add(
                        device.name +
                        " [" +
                        characteristics +
                        "]");
                }
            }

            UxrAvatar[] avatars =
                FindObjectsOfType<UxrAvatar>(true);

            UxrGrabber[] grabbers =
                FindObjectsOfType<UxrGrabber>(true);

            UxrGrabbableObject[] grabbables =
                FindObjectsOfType<UxrGrabbableObject>(true);

            UxrLocomotion[] oldLocomotion =
                FindObjectsOfType<UxrLocomotion>(true);

            LineRenderer[] lines =
                FindObjectsOfType<LineRenderer>(true);

            V8DirectOpenXrSmoothLocomotion[] smooth =
                FindObjectsOfType<
                    V8DirectOpenXrSmoothLocomotion>(true);

            V8DirectOpenXrGripDriver[] gripDrivers =
                FindObjectsOfType<
                    V8DirectOpenXrGripDriver>(true);

            string controllers =
                controllerNames.Count > 0
                    ? string.Join(", ", controllerNames)
                    : "none";

            bool ready =
                avatars.Length == 1 &&
                smooth.Length == 1 &&
                gripDrivers.Length == 1 &&
                grabbers.Length >= 2 &&
                grabbables.Length >= 6 &&
                oldLocomotion.Length == 0 &&
                lines.Length == 0;

            string status =
                "controllers={" + controllers + "}, " +
                "avatars=" + avatars.Length + ", " +
                "direct smooth movement=" + smooth.Length + ", " +
                "direct grip drivers=" + gripDrivers.Length + ", " +
                "grabbers=" + grabbers.Length + ", " +
                "grabbables=" + grabbables.Length + ", " +
                "old/teleport locomotion=" +
                    oldLocomotion.Length + ", " +
                "line renderers=" + lines.Length;

            if (ready)
            {
                Debug.Log(
                    "[V8 Smooth/Grab Repair] Ready: " +
                    status);
            }
            else
            {
                Debug.LogError(
                    "[V8 Smooth/Grab Repair] Not ready: " +
                    status);
            }
        }
    }
}
