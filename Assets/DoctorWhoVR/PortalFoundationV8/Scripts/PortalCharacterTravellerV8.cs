using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// NPC/character-ready portal adapter. Supports ordinary Transforms,
    /// CharacterController, Rigidbody, and NavMeshAgent roots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalCharacterTravellerV8 : MonoBehaviour
    {
        private sealed class PortalState
        {
            public Vector3 PreviousPosition;
            public float PreviousSide;
        }

        [SerializeField] private Transform _crossingPoint;
        [SerializeField, Min(0.02f)] private float _cooldown = 0.18f;
        [SerializeField] private UnityEvent _onPortalCrossed;

        private readonly Dictionary<StencilPortal, PortalState>
            _states =
                new Dictionary<StencilPortal, PortalState>();

        private CharacterController _controller;
        private Rigidbody _body;
        private NavMeshAgent _agent;
        private float _nextTeleportTime;

        private Transform CrossingPoint
        {
            get { return _crossingPoint != null ? _crossingPoint : transform; }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _body = GetComponent<Rigidbody>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            ResetStates();
        }

        private void LateUpdate()
        {
            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            foreach (StencilPortal portal in portals)
            {
                if (portal == null || portal.Target == null)
                    continue;

                PortalState state;

                if (!_states.TryGetValue(portal, out state))
                {
                    state = CreateState(portal);
                    _states.Add(portal, state);
                    continue;
                }

                Vector3 currentPosition = CrossingPoint.position;

                float currentSide =
                    portal.SignedDistanceToPlane(currentPosition);

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

                    Vector3 crossing =
                        Vector3.Lerp(
                            state.PreviousPosition,
                            currentPosition,
                            amount);

                    if (portal.ContainsPoint(
                            crossing,
                            0.15f,
                            0.15f))
                    {
                        TeleportCharacter(portal);
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

        private void TeleportCharacter(StencilPortal source)
        {
            Vector3 destination =
                source.TransformPointToTarget(transform.position) +
                source.Target.transform.forward *
                source.Target.ExitOffset;

            Quaternion rotation =
                source.TransformRotationToTarget(transform.rotation);

            Vector3 bodyVelocity =
                _body != null
                    ? _body.velocity
                    : Vector3.zero;

            Vector3 agentVelocity =
                _agent != null && _agent.enabled
                    ? _agent.velocity
                    : Vector3.zero;

            bool controllerEnabled =
                _controller != null && _controller.enabled;

            if (controllerEnabled)
                _controller.enabled = false;

            if (_agent != null && _agent.enabled)
            {
                _agent.Warp(destination);
                transform.rotation = rotation;
                _agent.velocity =
                    source.TransformDirectionToTarget(agentVelocity);
            }
            else
            {
                transform.SetPositionAndRotation(
                    destination,
                    rotation);
            }

            if (_body != null)
            {
                _body.position = destination;
                _body.rotation = rotation;
                _body.velocity =
                    source.TransformDirectionToTarget(bodyVelocity);
            }

            Physics.SyncTransforms();

            if (controllerEnabled)
                _controller.enabled = true;

            if (_onPortalCrossed != null)
                _onPortalCrossed.Invoke();
        }

        private PortalState CreateState(StencilPortal portal)
        {
            return new PortalState
            {
                PreviousPosition = CrossingPoint.position,
                PreviousSide =
                    portal.SignedDistanceToPlane(CrossingPoint.position)
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
