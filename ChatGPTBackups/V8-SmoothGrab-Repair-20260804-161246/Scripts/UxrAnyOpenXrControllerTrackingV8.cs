using System;
using UltimateXR.Devices.Integrations;
using UnityEngine;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Uses Unity XR node tracking for the generic OpenXR input fallback.
    /// UltimateXR's existing calibrated left/right sensor transforms are
    /// assigned by the V8 editor fixer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UxrAnyOpenXrControllerTrackingV8
        : UxrUnityXRControllerTracking
    {
        public override Type RelatedControllerInputType
        {
            get
            {
                return typeof(UxrAnyOpenXrControllerInputV8);
            }
        }

        public override string SDKDependency
        {
            get { return string.Empty; }
        }
    }
}
