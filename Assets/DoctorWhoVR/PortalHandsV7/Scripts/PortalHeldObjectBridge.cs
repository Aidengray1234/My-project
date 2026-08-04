using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Keeps held objects aligned during whole-rig teleport and transfers
    /// far-side mapped-hand grabs back to the real controller afterward.
    /// </summary>
    public static class PortalHeldObjectBridge
    {
        public static void BeforeRigTeleport(
            StencilPortalTraveller traveller,
            StencilPortal source)
        {
            if (traveller == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            HashSet<Transform> movedRoots =
                new HashSet<Transform>();

            XRGrabInteractable[] grabs =
                Object.FindObjectsOfType<XRGrabInteractable>();

            foreach (XRGrabInteractable grab in grabs)
            {
                if (grab == null || !grab.isSelected)
                    continue;

                IXRSelectInteractor selecting =
                    grab.firstInteractorSelecting;

                if (selecting == null)
                    continue;

                Component selectingComponent =
                    selecting as Component;

                if (selectingComponent == null)
                    continue;

                if (selectingComponent.GetComponent<
                        PortalMappedInteractorMarker>() != null)
                {
                    // This object is already physically in the destination.
                    continue;
                }

                if (!selectingComponent.transform.IsChildOf(
                        traveller.transform))
                {
                    continue;
                }

                Transform root = grab.transform;

                if (!movedRoots.Add(root))
                    continue;

                TeleportTransformAndBody(
                    root,
                    source);
            }
        }

        public static void AfterRigTeleport(
            StencilPortalTraveller traveller,
            StencilPortal source)
        {
            if (traveller == null)
                return;

            foreach (
                PortalHandInteractorBridge bridge in
                PortalHandInteractorBridge.GetForRig(
                    traveller.transform))
            {
                bridge.TransferMappedSelectionsToNormal();
            }
        }

        public static void TeleportTransformAndBody(
            Transform target,
            StencilPortal source)
        {
            if (target == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            Rigidbody body =
                target.GetComponent<Rigidbody>();

            Vector3 velocity =
                body != null
                    ? body.velocity
                    : Vector3.zero;

            Vector3 angularVelocity =
                body != null
                    ? body.angularVelocity
                    : Vector3.zero;

            Vector3 destinationPosition =
                source.TransformPointToTarget(
                    target.position) +
                source.Target.transform.forward *
                source.Target.ExitOffset;

            Quaternion destinationRotation =
                source.TransformRotationToTarget(
                    target.rotation);

            target.SetPositionAndRotation(
                destinationPosition,
                destinationRotation);

            if (body != null)
            {
                body.position = destinationPosition;
                body.rotation = destinationRotation;

                body.velocity =
                    source.TransformDirectionToTarget(
                        velocity);

                body.angularVelocity =
                    source.TransformDirectionToTarget(
                        angularVelocity);

                body.WakeUp();
            }
        }
    }
}
