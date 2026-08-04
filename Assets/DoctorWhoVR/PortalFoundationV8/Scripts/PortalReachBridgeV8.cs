using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Maps one direct-grab interactor through the doorway when the physical
    /// controller hand crosses before the player's head.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalReachBridgeV8 : MonoBehaviour
    {
        private static readonly List<PortalReachBridgeV8> Instances =
            new List<PortalReachBridgeV8>();

        [SerializeField] private ActionBasedController _controller;
        [SerializeField] private XRDirectInteractor _normalInteractor;
        [SerializeField] private Transform _handAnchor;
        [SerializeField] private Transform _rigRoot;
        [SerializeField, Min(0.02f)] private float _grabRadius = 0.085f;
        [SerializeField, Min(0.1f)] private float _activationDepth = 1.35f;

        private XRDirectInteractor _mappedInteractor;
        private StencilPortal _activePortal;

        public XRDirectInteractor NormalInteractor
        {
            get { return _normalInteractor; }
        }

        public XRDirectInteractor MappedInteractor
        {
            get { return _mappedInteractor; }
        }

        public StencilPortal ActivePortal
        {
            get { return _activePortal; }
        }

        public void Configure(
            ActionBasedController controller,
            XRDirectInteractor normalInteractor,
            Transform handAnchor,
            Transform rigRoot)
        {
            _controller = controller;
            _normalInteractor = normalInteractor;
            _handAnchor = handAnchor;
            _rigRoot = rigRoot;

            EnsureMappedInteractor();
        }

        private void Awake()
        {
            if (_controller == null)
                _controller = GetComponent<ActionBasedController>();

            if (_normalInteractor == null)
            {
                XRDirectInteractor[] interactors =
                    GetComponentsInChildren<XRDirectInteractor>(true);

                foreach (XRDirectInteractor candidate in interactors)
                {
                    if (candidate != null &&
                        candidate.GetComponent<
                            PortalMappedInteractorMarkerV8>() == null)
                    {
                        _normalInteractor = candidate;
                        break;
                    }
                }
            }

            if (_handAnchor == null)
                _handAnchor = transform;

            if (_rigRoot == null)
            {
                StencilPortalTraveller traveller =
                    GetComponentInParent<StencilPortalTraveller>();

                if (traveller != null)
                    _rigRoot = traveller.transform;
            }

            EnsureMappedInteractor();
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            DeactivateMappedInteractor();
        }

        private void OnDestroy()
        {
            if (_mappedInteractor != null)
                Destroy(_mappedInteractor.gameObject);
        }

        private void Update()
        {
            if (_controller == null ||
                _handAnchor == null)
            {
                DeactivateMappedInteractor();
                return;
            }

            StencilPortal portal =
                FindCrossedPortal(_handAnchor.position);

            if (portal == null || portal.Target == null)
            {
                DeactivateMappedInteractor();
                return;
            }

            _activePortal = portal;
            EnsureMappedInteractor();

            if (_mappedInteractor == null)
                return;

            _mappedInteractor.transform.SetPositionAndRotation(
                portal.TransformPointToTarget(_handAnchor.position),
                portal.TransformRotationToTarget(_handAnchor.rotation));

            if (!_mappedInteractor.gameObject.activeSelf)
                _mappedInteractor.gameObject.SetActive(true);

            if (_normalInteractor != null &&
                !_normalInteractor.hasSelection)
            {
                _normalInteractor.enabled = false;
            }
        }

        private StencilPortal FindCrossedPortal(Vector3 handPosition)
        {
            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            StencilPortal nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (StencilPortal portal in portals)
            {
                float side =
                    portal.SignedDistanceToPlane(handPosition);

                if (side > -0.012f ||
                    side < -_activationDepth ||
                    !portal.ContainsPoint(
                        handPosition,
                        0.22f,
                        0.22f))
                {
                    continue;
                }

                float distance = Mathf.Abs(side);

                if (distance < nearestDistance)
                {
                    nearest = portal;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void EnsureMappedInteractor()
        {
            if (_mappedInteractor != null ||
                _controller == null)
            {
                return;
            }

            GameObject mappedObject =
                new GameObject(
                    name + " V8 Far-Side Direct Interactor");

            mappedObject.SetActive(false);
            mappedObject.layer = gameObject.layer;
            mappedObject.transform.SetParent(
                _controller.transform,
                false);

            PortalMappedInteractorMarkerV8 marker =
                mappedObject.AddComponent<
                    PortalMappedInteractorMarkerV8>();

            marker.Owner = this;

            Rigidbody body =
                mappedObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            SphereCollider trigger =
                mappedObject.AddComponent<SphereCollider>();

            trigger.isTrigger = true;
            trigger.radius = _grabRadius;

            _mappedInteractor =
                mappedObject.AddComponent<XRDirectInteractor>();

            _mappedInteractor.xrController = _controller;

            if (_normalInteractor != null)
            {
                _mappedInteractor.interactionManager =
                    _normalInteractor.interactionManager;

                _mappedInteractor.interactionLayers =
                    _normalInteractor.interactionLayers;
            }
        }

        private void DeactivateMappedInteractor()
        {
            _activePortal = null;

            if (_mappedInteractor != null &&
                _mappedInteractor.gameObject.activeSelf)
            {
                _mappedInteractor.gameObject.SetActive(false);
            }

            if (_normalInteractor != null)
                _normalInteractor.enabled = true;
        }

        public bool TransferNormalSelectionToMapped(
            XRGrabInteractable grab,
            StencilPortal portal)
        {
            if (grab == null ||
                portal == null ||
                _normalInteractor == null ||
                _mappedInteractor == null ||
                !ReferenceEquals(_activePortal, portal))
            {
                return false;
            }

            XRInteractionManager manager =
                _normalInteractor.interactionManager;

            if (manager == null)
                return false;

            manager.SelectExit(
                (IXRSelectInteractor)_normalInteractor,
                (IXRSelectInteractable)grab);

            manager.SelectEnter(
                (IXRSelectInteractor)_mappedInteractor,
                (IXRSelectInteractable)grab);

            _normalInteractor.enabled = false;
            return true;
        }

        public void TransferMappedSelectionsToNormal()
        {
            if (_mappedInteractor == null ||
                _normalInteractor == null)
            {
                return;
            }

            XRInteractionManager manager =
                _mappedInteractor.interactionManager;

            if (manager == null)
                return;

            List<IXRSelectInteractable> selections =
                new List<IXRSelectInteractable>(
                    _mappedInteractor.interactablesSelected);

            foreach (IXRSelectInteractable selection in selections)
            {
                manager.SelectExit(
                    (IXRSelectInteractor)_mappedInteractor,
                    selection);

                manager.SelectEnter(
                    (IXRSelectInteractor)_normalInteractor,
                    selection);
            }

            DeactivateMappedInteractor();
        }

        public static PortalReachBridgeV8
            FindForNormalInteractor(IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return null;

            foreach (PortalReachBridgeV8 bridge in Instances)
            {
                if (bridge != null &&
                    ReferenceEquals(
                        bridge._normalInteractor,
                        interactor))
                {
                    return bridge;
                }
            }

            return null;
        }

        public static IEnumerable<PortalReachBridgeV8>
            GetForRig(Transform rigRoot)
        {
            foreach (PortalReachBridgeV8 bridge in Instances)
            {
                if (bridge == null)
                    continue;

                if (ReferenceEquals(bridge._rigRoot, rigRoot) ||
                    bridge.transform.IsChildOf(rigRoot))
                {
                    yield return bridge;
                }
            }
        }
    }
}
