using System.Collections.Generic;
using UnityEngine;
using UltimateXR.Manipulation;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Free UltimateXR grabbable objects preserve position, rotation, velocity,
    /// and spin when crossing. Objects being held wait for the avatar crossing
    /// so the framework grip stays stable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class UltimateXrRigidbodyTravellerV8 : MonoBehaviour
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
        private UxrGrabbableObject _grabbable;
        private float _nextTeleportTime;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _grabbable = GetComponent<UxrGrabbableObject>();
        }

        private void OnEnable()
        {
            ResetStates();
        }

        private void FixedUpdate()
        {
            bool isGrabbed =
                _grabbable != null &&
                UxrGrabManager.Instance.IsBeingGrabbed(_grabbable);

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

                if (!isGrabbed &&
                    Time.unscaledTime >= _nextTeleportTime &&
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
                            0.12f))
                    {
                        UltimateXrPortalTransferUtilityV8
                            .TeleportRigidbody(_body, portal);

                        MarkExternallyTeleported();
                        return;
                    }
                }

                state.PreviousPosition = currentPosition;
                state.PreviousSide = currentSide;
            }
        }

        public void MarkExternallyTeleported()
        {
            _nextTeleportTime =
                Time.unscaledTime + _cooldown;

            ResetStates();
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

            foreach (
                StencilPortal portal in
                FindObjectsOfType<StencilPortal>())
            {
                if (portal != null)
                    _states.Add(portal, CreateState(portal));
            }
        }
    }
}
