using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Creates a direct interactor on the far side of a portal while the real
    /// controller hand is through the doorway. It uses the same controller
    /// grip input, allowing real far-side objects to be grabbed before the
    /// player's head/body crosses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalHandInteractorBridge : MonoBehaviour
    {
        private static readonly List<PortalHandInteractorBridge> Instances =
            new List<PortalHandInteractorBridge>();

        [SerializeField] private ActionBasedController _controller;
        [SerializeField] private XRDirectInteractor _normalDirectInteractor;
        [SerializeField] private Transform _handAnchor;
        [SerializeField, Min(0.02f)] private float _grabRadius = 0.09f;
        [SerializeField, Min(0.1f)] private float _activationDepth = 1.2f;

        private XRDirectInteractor _mappedInteractor;
        private Rigidbody _mappedRigidbody;
        private SphereCollider _mappedCollider;
        private StencilPortal _activePortal;
        private Transform _rigRoot;

        public XRDirectInteractor NormalInteractor
        {
            get { return _normalDirectInteractor; }
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
            XRDirectInteractor normalDirectInteractor,
            Transform handAnchor,
            Transform rigRoot)
        {
            _controller = controller;
            _normalDirectInteractor = normalDirectInteractor;
            _handAnchor = handAnchor;
            _rigRoot = rigRoot;

            EnsureMappedInteractor();
        }

        private void Awake()
        {
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
            SetMappedActive(false);
        }

        private void OnDestroy()
        {
            if (_mappedInteractor != null)
                Destroy(_mappedInteractor.gameObject);
        }

        private void Update()
        {
            if (_handAnchor == null ||
                _controller == null)
            {
                SetMappedActive(false);
                return;
            }

            StencilPortal portal =
                FindCrossedPortal(_handAnchor.position);

            if (portal == null ||
                portal.Target == null)
            {
                _activePortal = null;
                SetMappedActive(false);
                return;
            }

            _activePortal = portal;
            EnsureMappedInteractor();

            _mappedInteractor.transform.SetPositionAndRotation(
                portal.TransformPointToTarget(
                    _handAnchor.position),
                portal.TransformRotationToTarget(
                    _handAnchor.rotation));

            SetMappedActive(true);
        }

        private StencilPortal FindCrossedPortal(
            Vector3 handPosition)
        {
            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            StencilPortal best = null;
            float bestDistance = float.PositiveInfinity;

            foreach (StencilPortal portal in portals)
            {
                float signedDistance =
                    portal.SignedDistanceToPlane(handPosition);

                if (signedDistance > -0.015f ||
                    signedDistance < -_activationDepth ||
                    !portal.ContainsPoint(
                        handPosition,
                        0.24f,
                        0.24f))
                {
                    continue;
                }

                float absoluteDistance =
                    Mathf.Abs(signedDistance);

                if (absoluteDistance < bestDistance)
                {
                    bestDistance = absoluteDistance;
                    best = portal;
                }
            }

            return best;
        }

        private void EnsureMappedInteractor()
        {
            if (_mappedInteractor != null)
                return;

            GameObject mappedObject =
                new GameObject(
                    name + " Far-Side Direct Interactor");

            mappedObject.layer = gameObject.layer;

            PortalMappedInteractorMarker marker =
                mappedObject.AddComponent<
                    PortalMappedInteractorMarker>();

            marker.Owner = this;

            _mappedRigidbody =
                mappedObject.AddComponent<Rigidbody>();

            _mappedRigidbody.isKinematic = true;
            _mappedRigidbody.useGravity = false;
            _mappedRigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            _mappedCollider =
                mappedObject.AddComponent<SphereCollider>();

            _mappedCollider.isTrigger = true;
            _mappedCollider.radius = _grabRadius;

            _mappedInteractor =
                mappedObject.AddComponent<XRDirectInteractor>();

            if (_normalDirectInteractor != null)
            {
                _mappedInteractor.interactionManager =
                    _normalDirectInteractor.interactionManager;

                _mappedInteractor.interactionLayers =
                    _normalDirectInteractor.interactionLayers;
            }

            _mappedInteractor.xrController = _controller;
            mappedObject.SetActive(false);
        }

        private void SetMappedActive(bool active)
        {
            if (_mappedInteractor == null)
                return;

            GameObject mappedObject =
                _mappedInteractor.gameObject;

            if (mappedObject.activeSelf != active)
                mappedObject.SetActive(active);

            if (_normalDirectInteractor != null)
            {
                // Do not disable the normal interactor while it already holds
                // something. PortalPhysicsTraveller transfers it at crossing.
                bool canDisableNormal =
                    !_normalDirectInteractor.hasSelection;

                _normalDirectInteractor.enabled =
                    !active || !canDisableNormal;
            }
        }

        public bool TransferSelectionToMapped(
            XRGrabInteractable grab,
            StencilPortal portal)
        {
            if (grab == null ||
                portal == null ||
                _mappedInteractor == null ||
                _normalDirectInteractor == null ||
                _activePortal != portal)
            {
                return false;
            }

            XRInteractionManager manager =
                _normalDirectInteractor.interactionManager;

            if (manager == null)
                return false;

            manager.SelectExit(
                _normalDirectInteractor,
                grab);

            manager.SelectEnter(
                _mappedInteractor,
                grab);

            _normalDirectInteractor.enabled = false;
            return true;
        }

        public void TransferMappedSelectionsToNormal()
        {
            if (_mappedInteractor == null ||
                _normalDirectInteractor == null)
            {
                return;
            }

            XRInteractionManager manager =
                _mappedInteractor.interactionManager;

            if (manager == null)
                return;

            List<IXRSelectInteractable> selected =
                new List<IXRSelectInteractable>(
                    _mappedInteractor.interactablesSelected);

            foreach (IXRSelectInteractable interactable in selected)
            {
                manager.SelectExit(
                    _mappedInteractor,
                    interactable);

                manager.SelectEnter(
                    _normalDirectInteractor,
                    interactable);
            }

            _normalDirectInteractor.enabled = true;
            _activePortal = null;
            SetMappedActive(false);
        }

        public static PortalHandInteractorBridge
            FindForNormalInteractor(IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return null;

            foreach (PortalHandInteractorBridge bridge in Instances)
            {
                if (bridge != null &&
                    bridge._normalDirectInteractor == interactor)
                {
                    return bridge;
                }
            }

            return null;
        }

        public static IEnumerable<PortalHandInteractorBridge>
            GetForRig(Transform rigRoot)
        {
            foreach (PortalHandInteractorBridge bridge in Instances)
            {
                if (bridge == null)
                    continue;

                if (bridge._rigRoot == rigRoot ||
                    bridge.transform.IsChildOf(rigRoot))
                {
                    yield return bridge;
                }
            }
        }
    }
}
