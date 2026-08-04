using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.StencilPortalV6
{
    /// <summary>
    /// Handles reliable two-way XR crossing. The visible portal itself is
    /// rendered by stencil-masked proxy geometry, not by RenderTextures.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StencilPortal : MonoBehaviour
    {
        private sealed class TravellerState
        {
            public Vector3 PreviousHeadPosition;
            public float PreviousSide;
        }

        private static readonly Quaternion HalfTurn =
            Quaternion.Euler(0f, 180f, 0f);

        [SerializeField] private StencilPortal _target;

        [Header("Opening")]
        [SerializeField, Min(0.1f)] private float _openingWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float _openingHeight = 3.4f;
        [SerializeField, Min(0f)] private float _exitOffset = 0.18f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.02f;

        private readonly Dictionary<StencilPortalTraveller, TravellerState>
            _travellerStates =
                new Dictionary<StencilPortalTraveller, TravellerState>();

        private readonly List<StencilPortalTraveller> _removeBuffer =
            new List<StencilPortalTraveller>();

        public StencilPortal Target
        {
            get { return _target; }
        }

        public float ExitOffset
        {
            get { return _exitOffset; }
        }

        public void Configure(StencilPortal target)
        {
            _target = target;
        }

        private void Awake()
        {
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            ConfigureTrigger();
        }

        private void OnDisable()
        {
            _travellerStates.Clear();
        }

        private void LateUpdate()
        {
            TrackCrossings();
        }

        private void ConfigureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();

            if (trigger == null)
                return;

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size =
                new Vector3(
                    _openingWidth,
                    _openingHeight,
                    0.7f);
        }

        private void TrackCrossings()
        {
            _removeBuffer.Clear();

            for (int index = 0;
                 index < StencilPortalTraveller.ActiveTravellers.Count;
                 ++index)
            {
                StencilPortalTraveller traveller =
                    StencilPortalTraveller.ActiveTravellers[index];

                if (traveller == null ||
                    !traveller.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 currentHeadPosition =
                    traveller.Head.position;

                float currentSide =
                    SignedDistance(currentHeadPosition);

                TravellerState state;

                if (!_travellerStates.TryGetValue(
                        traveller,
                        out state))
                {
                    state = new TravellerState
                    {
                        PreviousHeadPosition = currentHeadPosition,
                        PreviousSide = currentSide
                    };

                    _travellerStates.Add(traveller, state);
                    continue;
                }

                if (_target != null &&
                    traveller.CanTeleport &&
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
                            state.PreviousHeadPosition,
                            currentHeadPosition,
                            amount);

                    if (IsInsideOpening(crossingPoint))
                    {
                        traveller.TeleportThrough(this);

                        currentHeadPosition =
                            traveller.Head.position;

                        currentSide =
                            SignedDistance(currentHeadPosition);
                    }
                }

                state.PreviousHeadPosition = currentHeadPosition;
                state.PreviousSide = currentSide;
            }

            foreach (
                KeyValuePair<StencilPortalTraveller, TravellerState>
                    pair in _travellerStates)
            {
                if (pair.Key == null ||
                    !pair.Key.isActiveAndEnabled)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            for (int index = 0;
                 index < _removeBuffer.Count;
                 ++index)
            {
                _travellerStates.Remove(_removeBuffer[index]);
            }
        }

        private float SignedDistance(Vector3 worldPoint)
        {
            return Vector3.Dot(
                transform.forward,
                worldPoint - transform.position);
        }

        private bool IsInsideOpening(Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            return
                Mathf.Abs(localPoint.x) <= _openingWidth * 0.5f &&
                Mathf.Abs(localPoint.y) <= _openingHeight * 0.5f;
        }

        public Vector3 TransformPointToTarget(Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            localPoint = HalfTurn * localPoint;

            return _target.transform.TransformPoint(localPoint);
        }

        public Vector3 TransformDirectionToTarget(Vector3 worldDirection)
        {
            Vector3 localDirection =
                transform.InverseTransformDirection(worldDirection);

            localDirection = HalfTurn * localDirection;

            return _target.transform.TransformDirection(localDirection);
        }

        public Quaternion TransformRotationToTarget(Quaternion worldRotation)
        {
            return
                _target.transform.rotation *
                HalfTurn *
                Quaternion.Inverse(transform.rotation) *
                worldRotation;
        }
    }
}
