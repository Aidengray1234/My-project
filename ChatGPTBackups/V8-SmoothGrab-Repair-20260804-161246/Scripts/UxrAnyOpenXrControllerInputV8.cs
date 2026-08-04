using System.Collections.Generic;
using UltimateXR.Core;
using UltimateXR.Devices;
using UltimateXR.Devices.Integrations;
using UnityEngine;
using UnityEngine.XR;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Generic Unity XR/OpenXR controller input for UltimateXR.
    ///
    /// UltimateXR 0.9.7 controller integrations compare exact device names.
    /// This fallback dynamically accepts any valid left/right held controller,
    /// including OpenXR, SteamVR OpenXR, Virtual Desktop, Link/Air Link,
    /// and Unity's XR Device Simulator devices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UxrAnyOpenXrControllerInputV8
        : UxrUnityXRControllerInput
    {
        public override string SDKDependency
        {
            get { return string.Empty; }
        }

        public override UxrControllerSetupType SetupType
        {
            get { return UxrControllerSetupType.Dual; }
        }

        public override bool IsHandednessSupported
        {
            get { return true; }
        }

        public override bool MainJoystickIsTouchpad
        {
            get { return false; }
        }

        public override bool ForceUseAlways
        {
            get { return true; }
        }

        public override IEnumerable<string> ControllerNames
        {
            get
            {
                List<InputDevice> devices =
                    new List<InputDevice>();

                InputDevices.GetDevices(devices);

                HashSet<string> emittedNames =
                    new HashSet<string>();

                foreach (InputDevice device in devices)
                {
                    if (!IsUsableController(device) ||
                        string.IsNullOrEmpty(device.name) ||
                        !emittedNames.Add(device.name))
                    {
                        continue;
                    }

                    yield return device.name;
                }
            }
        }

        public override bool HasControllerElements(
            UxrHandSide handSide,
            UxrControllerElements controllerElements)
        {
            UxrControllerElements available =
                UxrControllerElements.Joystick |
                UxrControllerElements.Grip |
                UxrControllerElements.Trigger |
                UxrControllerElements.Button1 |
                UxrControllerElements.Button2 |
                UxrControllerElements.Menu |
                UxrControllerElements.DPad;

            return
                (available & controllerElements) ==
                controllerElements;
        }

        private static bool IsUsableController(
            InputDevice device)
        {
            InputDeviceCharacteristics characteristics =
                device.characteristics;

            bool isController =
                (characteristics &
                 InputDeviceCharacteristics.Controller) != 0 ||
                (characteristics &
                 InputDeviceCharacteristics.HeldInHand) != 0;

            bool hasSide =
                (characteristics &
                 InputDeviceCharacteristics.Left) != 0 ||
                (characteristics &
                 InputDeviceCharacteristics.Right) != 0;

            return device.isValid && isController && hasSide;
        }
    }
}
