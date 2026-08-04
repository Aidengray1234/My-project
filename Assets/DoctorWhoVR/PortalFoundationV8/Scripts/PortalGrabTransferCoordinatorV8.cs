using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    public static class PortalGrabTransferCoordinatorV8
    {
        public static void BeforePlayerTeleport(
            Transform rigRoot,
            StencilPortal source)
        {
            if (rigRoot == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            HashSet<Transform> moved =
                new HashSet<Transform>();

            XRGrabInteractable[] grabs =
                Object.FindObjectsOfType<XRGrabInteractable>();

            foreach (XRGrabInteractable grab in grabs)
            {
                if (grab == null || !grab.isSelected)
                    continue;

                IXRSelectInteractor selecting =
                    grab.firstInteractorSelecting;

                Component selectingComponent =
                    selecting as Component;

                if (selectingComponent == null)
                    continue;

                if (selectingComponent.GetComponent<
                        PortalMappedInteractorMarkerV8>() != null)
                {
                    // Already on the destination side.
                    continue;
                }

                if (!selectingComponent.transform.IsChildOf(rigRoot))
                    continue;

                Transform objectRoot = grab.transform;

                if (!moved.Add(objectRoot))
                    continue;

                Rigidbody body =
                    objectRoot.GetComponent<Rigidbody>();

                if (body != null)
                {
                    PortalTransferUtilityV8.TeleportRigidbody(
                        body,
                        source);
                }
                else
                {
                    PortalTransferUtilityV8.TeleportTransform(
                        objectRoot,
                        source);
                }
            }
        }

        public static void AfterPlayerTeleport(
            Transform rigRoot,
            StencilPortal source)
        {
            if (rigRoot == null)
                return;

            foreach (
                PortalReachBridgeV8 bridge in
                PortalReachBridgeV8.GetForRig(rigRoot))
            {
                bridge.TransferMappedSelectionsToNormal();
            }
        }
    }
}
