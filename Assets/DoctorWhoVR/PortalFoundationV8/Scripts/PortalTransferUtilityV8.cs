using UnityEngine;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    public static class PortalTransferUtilityV8
    {
        public static void TeleportTransform(
            Transform target,
            StencilPortal source,
            bool applyExitOffset = true)
        {
            if (target == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            Vector3 destinationPosition =
                source.TransformPointToTarget(target.position);

            if (applyExitOffset)
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
            bool applyExitOffset = true)
        {
            if (body == null ||
                source == null ||
                source.Target == null)
            {
                return;
            }

            Vector3 oldVelocity = body.velocity;
            Vector3 oldAngularVelocity = body.angularVelocity;

            Vector3 destinationPosition =
                source.TransformPointToTarget(body.position);

            if (applyExitOffset)
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
                source.TransformDirectionToTarget(oldVelocity);

            body.angularVelocity =
                source.TransformDirectionToTarget(oldAngularVelocity);

            body.WakeUp();
            Physics.SyncTransforms();
        }
    }
}
