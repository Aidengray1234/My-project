using System.Collections.Generic;
using UnityEngine;
using UltimateXR.Avatar;
using UltimateXR.Core;
using UltimateXR.Manipulation;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Portal crossing for the complete UltimateXR local avatar. The framework
    /// moves the avatar, camera, correctly oriented hands, and controller input
    /// as one system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UltimateXrAvatarPortalTravellerV8 : MonoBehaviour
    {
        private sealed class PortalState
        {
            public Vector3 PreviousCameraPosition;
            public float PreviousSide;
        }

        [SerializeField] private UxrAvatar _avatar;
        [SerializeField, Min(0.02f)] private float _cooldown = 0.18f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.018f;

        private readonly Dictionary<StencilPortal, PortalState>
            _states =
                new Dictionary<StencilPortal, PortalState>();

        private float _nextTeleportTime;

        private void Awake()
        {
            if (_avatar == null)
                _avatar = GetComponent<UxrAvatar>();

            if (_avatar == null)
                _avatar = GetComponentInParent<UxrAvatar>();
        }

        private void OnEnable()
        {
            ResetStates();
        }

        private void LateUpdate()
        {
            if (_avatar == null ||
                _avatar.CameraComponent == null)
            {
                return;
            }

            Vector3 cameraPosition =
                _avatar.CameraComponent.transform.position;

            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            foreach (StencilPortal portal in portals)
            {
                if (portal == null ||
                    portal.Target == null)
                {
                    continue;
                }

                PortalState state;

                if (!_states.TryGetValue(portal, out state))
                {
                    state = CreateState(portal, cameraPosition);
                    _states.Add(portal, state);
                    continue;
                }

                float currentSide =
                    portal.SignedDistanceToPlane(cameraPosition);

                if (Time.unscaledTime >= _nextTeleportTime &&
                    state.PreviousSide > _crossingEpsilon &&
                    currentSide <= -_crossingEpsilon)
                {
                    float denominator =
                        state.PreviousSide - currentSide;

                    float amount =
                        denominator > 0.00001f
                            ? Mathf.Clamp01(
                                state.PreviousSide / denominator)
                            : 1f;

                    Vector3 crossingPoint =
                        Vector3.Lerp(
                            state.PreviousCameraPosition,
                            cameraPosition,
                            amount);

                    if (portal.ContainsPoint(crossingPoint))
                    {
                        TeleportAvatar(portal);
                        _nextTeleportTime =
                            Time.unscaledTime + _cooldown;

                        ResetStates();
                        return;
                    }
                }

                state.PreviousCameraPosition = cameraPosition;
                state.PreviousSide = currentSide;
            }
        }

        private void TeleportAvatar(StencilPortal source)
        {
            HashSet<UxrGrabbableObject> movedObjects =
                new HashSet<UxrGrabbableObject>();

            UxrGrabber[] grabbers =
                _avatar.GetComponentsInChildren<UxrGrabber>(true);

            foreach (UxrGrabber grabber in grabbers)
            {
                if (grabber == null ||
                    grabber.GrabbedObject == null ||
                    !movedObjects.Add(grabber.GrabbedObject))
                {
                    continue;
                }

                UltimateXrPortalTransferUtilityV8.TeleportGrabbable(
                    grabber.GrabbedObject,
                    source);
            }

            Vector3 destinationFloor =
                source.TransformPointToTarget(
                    _avatar.CameraFloorPosition) +
                source.Target.transform.forward *
                source.Target.ExitOffset;

            Vector3 destinationForward =
                source.TransformDirectionToTarget(
                    _avatar.ProjectedCameraForward);

            destinationForward =
                Vector3.ProjectOnPlane(
                    destinationForward,
                    Vector3.up);

            if (destinationForward.sqrMagnitude < 0.0001f)
                destinationForward = source.Target.transform.forward;

            UxrManager.Instance.MoveAvatarTo(
                _avatar,
                destinationFloor,
                destinationForward.normalized,
                true);
        }

        private PortalState CreateState(
            StencilPortal portal,
            Vector3 cameraPosition)
        {
            return new PortalState
            {
                PreviousCameraPosition = cameraPosition,
                PreviousSide =
                    portal.SignedDistanceToPlane(cameraPosition)
            };
        }

        private void ResetStates()
        {
            _states.Clear();

            if (_avatar == null ||
                _avatar.CameraComponent == null)
            {
                return;
            }

            Vector3 cameraPosition =
                _avatar.CameraComponent.transform.position;

            foreach (
                StencilPortal portal in
                FindObjectsOfType<StencilPortal>())
            {
                if (portal != null)
                {
                    _states.Add(
                        portal,
                        CreateState(portal, cameraPosition));
                }
            }
        }
    }
}
