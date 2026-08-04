using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Seamlessly moves free or grabbed rigidbodies across a portal.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PortalRigidbodyTravellerV8 : MonoBehaviour
    {
        private sealed class PortalState
        {
            public Vector3 PreviousPosition;
            public float PreviousSide;
        }

        [SerializeField, Min(0.02f)] private float _cooldown = 0.16f;

        private readonly Dictionary<StencilPortal, PortalState>
            _states =
                new Dictionary<StencilPortal, PortalState>();

        private Rigidbody _body;
        private XRGrabInteractable _grab;
        private float _nextTeleportTime;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            ResetStates();
        }

        private void FixedUpdate()
        {
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
                    state = CreateState(portal);
                    _states.Add(portal, state);
                    continue;
                }

                Vector3 currentPosition = transform.position;

                float currentSide =
                    portal.SignedDistanceToPlane(currentPosition);

                if (Time.unscaledTime >= _nextTeleportTime &&
                    state.PreviousSide > 0.012f &&
                    currentSide <= -0.012f)
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
                            state.PreviousPosition,
                            currentPosition,
                            amount);

                    if (portal.ContainsPoint(
                            crossingPoint,
                            0.12f,
                            0.12f) &&
                        TryTeleport(portal))
                    {
                        _nextTeleportTime =
                            Time.unscaledTime + _cooldown;

                        ResetStates();
                        return;
                    }
                }

                state.PreviousPosition = currentPosition;
                state.PreviousSide = currentSide;
            }
        }

        private bool TryTeleport(StencilPortal portal)
        {
            if (_grab != null && _grab.isSelected)
            {
                IXRSelectInteractor selecting =
                    _grab.firstInteractorSelecting;

                Component selectingComponent =
                    selecting as Component;

                if (selectingComponent != null &&
                    selectingComponent.GetComponent<
                        PortalMappedInteractorMarkerV8>() != null)
                {
                    return false;
                }

                PortalReachBridgeV8 bridge =
                    PortalReachBridgeV8
                        .FindForNormalInteractor(selecting);

                if (bridge == null ||
                    !ReferenceEquals(
                        bridge.ActivePortal,
                        portal))
                {
                    return false;
                }

                XRInteractionManager manager =
                    bridge.NormalInteractor != null
                        ? bridge.NormalInteractor.interactionManager
                        : null;

                if (manager == null ||
                    bridge.MappedInteractor == null)
                {
                    return false;
                }

                manager.SelectExit(
                    (IXRSelectInteractor)bridge.NormalInteractor,
                    (IXRSelectInteractable)_grab);

                PortalTransferUtilityV8.TeleportRigidbody(
                    _body,
                    portal);

                manager.SelectEnter(
                    (IXRSelectInteractor)bridge.MappedInteractor,
                    (IXRSelectInteractable)_grab);

                return true;
            }

            PortalTransferUtilityV8.TeleportRigidbody(
                _body,
                portal);

            return true;
        }

        private PortalState CreateState(StencilPortal portal)
        {
            return new PortalState
            {
                PreviousPosition = transform.position,
                PreviousSide =
                    portal.SignedDistanceToPlane(transform.position)
            };
        }

        private void ResetStates()
        {
            _states.Clear();

            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            foreach (StencilPortal portal in portals)
            {
                if (portal != null)
                    _states.Add(portal, CreateState(portal));
            }
        }
    }
}
