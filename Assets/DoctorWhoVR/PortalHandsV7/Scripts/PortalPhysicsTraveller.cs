using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Teleports free rigidbodies through portals with velocity preserved.
    /// Held objects transfer from the normal hand to the mapped far-side hand
    /// when they cross before the player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PortalPhysicsTraveller : MonoBehaviour
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
            InitializeStates();
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
                    state = new PortalState
                    {
                        PreviousPosition = transform.position,
                        PreviousSide =
                            portal.SignedDistanceToPlane(
                                transform.position)
                    };

                    _states.Add(portal, state);
                    continue;
                }

                Vector3 currentPosition =
                    transform.position;

                float currentSide =
                    portal.SignedDistanceToPlane(
                        currentPosition);

                if (Time.unscaledTime >= _nextTeleportTime &&
                    state.PreviousSide > 0.015f &&
                    currentSide <= -0.015f)
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
                            0.12f))
                    {
                        if (TryTeleport(portal))
                        {
                            _nextTeleportTime =
                                Time.unscaledTime + _cooldown;

                            InitializeStates();
                            return;
                        }
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

                PortalMappedInteractorMarker marker = null;

                Component selectingComponent =
                    selecting as Component;

                if (selectingComponent != null)
                {
                    marker =
                        selectingComponent.GetComponent<
                            PortalMappedInteractorMarker>();
                }

                if (marker != null)
                {
                    // It was already teleported and is held by a mapped hand.
                    return false;
                }

                PortalHandInteractorBridge bridge =
                    PortalHandInteractorBridge
                        .FindForNormalInteractor(selecting);

                if (bridge == null ||
                    bridge.ActivePortal != portal)
                {
                    return false;
                }

                XRInteractionManager manager =
                    bridge.NormalInteractor != null
                        ? bridge.NormalInteractor.interactionManager
                        : null;

                if (manager == null)
                    return false;

                manager.SelectExit(
                    bridge.NormalInteractor,
                    _grab);

                PortalHeldObjectBridge.TeleportTransformAndBody(
                    transform,
                    portal);

                manager.SelectEnter(
                    bridge.MappedInteractor,
                    _grab);

                return true;
            }

            PortalHeldObjectBridge.TeleportTransformAndBody(
                transform,
                portal);

            return true;
        }

        private void InitializeStates()
        {
            _states.Clear();

            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            foreach (StencilPortal portal in portals)
            {
                if (portal == null)
                    continue;

                _states.Add(
                    portal,
                    new PortalState
                    {
                        PreviousPosition = transform.position,
                        PreviousSide =
                            portal.SignedDistanceToPlane(
                                transform.position)
                    });
            }
        }
    }
}
