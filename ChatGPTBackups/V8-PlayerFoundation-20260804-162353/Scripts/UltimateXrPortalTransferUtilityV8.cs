using UnityEngine;
using UltimateXR.Manipulation;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    public static class UltimateXrPortalTransferUtilityV8
    {
        public static void TeleportTransform(
            Transform target,
            StencilPortal source,
            bool addExitOffset = true)
        {
            if (target == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            Vector3 destinationPosition =
                source.TransformPointToTarget(target.position);

            if (addExitOffset)
            {
                destinationPosition +=
                    source.Target.transform.forward *
                    source.Target.ExitOffset;
            }

            target.SetPositionAndRotation(
                destinationPosition,
                source.TransformRotationToTarget(target.rotation));
        }

        public static void TeleportRigidbody(
            Rigidbody body,
            StencilPortal source,
            bool addExitOffset = true)
        {
            if (body == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            Vector3 velocity = body.velocity;
            Vector3 angularVelocity = body.angularVelocity;

            Vector3 destinationPosition =
                source.TransformPointToTarget(body.position);

            if (addExitOffset)
            {
                destinationPosition +=
                    source.Target.transform.forward *
                    source.Target.ExitOffset;
            }

            Quaternion destinationRotation =
                source.TransformRotationToTarget(body.rotation);

            body.position = destinationPosition;
            body.rotation = destinationRotation;

            body.velocity =
                source.TransformDirectionToTarget(velocity);

            body.angularVelocity =
                source.TransformDirectionToTarget(angularVelocity);

            body.WakeUp();
            Physics.SyncTransforms();
        }

        public static void TeleportGrabbable(
            UxrGrabbableObject grabbable,
            StencilPortal source)
        {
            if (grabbable == null)
                return;

            Rigidbody body =
                grabbable.GetComponent<Rigidbody>();

            if (body != null)
            {
                TeleportRigidbody(body, source);
            }
            else
            {
                TeleportTransform(grabbable.transform, source);
            }

            UltimateXrRigidbodyTravellerV8 traveller =
                grabbable.GetComponent<
                    UltimateXrRigidbodyTravellerV8>();

            if (traveller != null)
                traveller.MarkExternallyTeleported();
        }
    }
}
