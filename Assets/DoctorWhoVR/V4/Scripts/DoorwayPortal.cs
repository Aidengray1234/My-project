using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.V4
{
    /// <summary>
    /// Stable non-rendering doorway portal. It continuously tracks the headset
    /// instead of relying only on trigger callbacks, so fast movement cannot
    /// skip the crossing plane.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DoorwayPortal : MonoBehaviour
    {
        private sealed class TravellerState
        {
            public Vector3 PreviousHeadPosition;
            public float PreviousSide;
        }

        private static readonly Quaternion HalfTurn =
            Quaternion.Euler(0f, 180f, 0f);

        [Header("Pair")]
        [SerializeField] private DoorwayPortal _target;

        [Header("Opening")]
        [SerializeField, Min(0.1f)] private float _openingWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float _openingHeight = 3.4f;
        [SerializeField, Min(0f)] private float _exitOffset = 0.18f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.02f;

        [Header("State")]
        [SerializeField] private bool _isOpen = true;
        [SerializeField] private GameObject _visualField;

        private readonly Dictionary<XRDoorwayTraveller, TravellerState>
            _travellerStates =
                new Dictionary<XRDoorwayTraveller, TravellerState>();

        private readonly List<XRDoorwayTraveller> _removeBuffer =
            new List<XRDoorwayTraveller>();

        public DoorwayPortal Target
        {
            get { return _target; }
        }

        public float ExitOffset
        {
            get { return _exitOffset; }
        }

        public bool IsOpen
        {
            get { return _isOpen; }
        }

        public void Configure(
            DoorwayPortal target,
            GameObject visualField)
        {
            _target = target;
            _visualField = visualField;
            ApplyState();
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            ApplyState();
        }

        private void Awake()
        {
            ConfigureTrigger();
            ApplyState();
        }

        private void OnValidate()
        {
            ConfigureTrigger();
            ApplyState();
        }

        private void OnDisable()
        {
            _travellerStates.Clear();
        }

        private void LateUpdate()
        {
            TrackTravellerCrossings();
        }

        private void ConfigureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();

            if (trigger == null)
                return;

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(
                _openingWidth,
                _openingHeight,
                0.65f);
        }

        private void ApplyState()
        {
            if (_visualField != null)
                _visualField.SetActive(_isOpen);
        }

        private void TrackTravellerCrossings()
        {
            _removeBuffer.Clear();

            for (int index = 0;
                 index < XRDoorwayTraveller.ActiveTravellers.Count;
                 ++index)
            {
                XRDoorwayTraveller traveller =
                    XRDoorwayTraveller.ActiveTravellers[index];

                if (traveller == null ||
                    !traveller.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 currentHeadPosition =
                    traveller.Head.position;

                float currentSide =
                    SignedDistanceToPlane(currentHeadPosition);

                TravellerState state;

                if (!_travellerStates.TryGetValue(
                        traveller,
                        out state))
                {
                    state = new TravellerState
                    {
                        PreviousHeadPosition =
                            currentHeadPosition,
                        PreviousSide = currentSide
                    };

                    _travellerStates.Add(traveller, state);
                    continue;
                }

                if (_isOpen &&
                    _target != null &&
                    _target._isOpen &&
                    traveller.CanTeleport &&
                    state.PreviousSide > _crossingEpsilon &&
                    currentSide <= -_crossingEpsilon)
                {
                    float denominator =
                        state.PreviousSide - currentSide;

                    float crossingAmount =
                        denominator > 0.00001f
                            ? Mathf.Clamp01(
                                state.PreviousSide /
                                denominator)
                            : 1f;

                    Vector3 crossingPoint =
                        Vector3.Lerp(
                            state.PreviousHeadPosition,
                            currentHeadPosition,
                            crossingAmount);

                    if (IsInsideOpening(crossingPoint))
                    {
                        traveller.TeleportThrough(this);

                        currentHeadPosition =
                            traveller.Head.position;

                        currentSide =
                            SignedDistanceToPlane(
                                currentHeadPosition);
                    }
                }

                state.PreviousHeadPosition =
                    currentHeadPosition;

                state.PreviousSide = currentSide;
            }

            foreach (
                KeyValuePair<XRDoorwayTraveller, TravellerState>
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
                _travellerStates.Remove(
                    _removeBuffer[index]);
            }
        }

        private float SignedDistanceToPlane(
            Vector3 worldPoint)
        {
            return Vector3.Dot(
                transform.forward,
                worldPoint - transform.position);
        }

        private bool IsInsideOpening(
            Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            return
                Mathf.Abs(localPoint.x) <=
                    _openingWidth * 0.5f &&
                Mathf.Abs(localPoint.y) <=
                    _openingHeight * 0.5f;
        }

        public Vector3 TransformPointToTarget(
            Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            localPoint = HalfTurn * localPoint;

            return _target.transform.TransformPoint(
                localPoint);
        }

        public Vector3 TransformDirectionToTarget(
            Vector3 worldDirection)
        {
            Vector3 localDirection =
                transform.InverseTransformDirection(
                    worldDirection);

            localDirection = HalfTurn * localDirection;

            return _target.transform.TransformDirection(
                localDirection);
        }

        public Quaternion TransformRotationToTarget(
            Quaternion worldRotation)
        {
            return
                _target.transform.rotation *
                HalfTurn *
                Quaternion.Inverse(transform.rotation) *
                worldRotation;
        }
    }
}
