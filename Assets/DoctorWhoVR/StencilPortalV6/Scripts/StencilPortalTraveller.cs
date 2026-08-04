using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.StencilPortalV6
{
    /// <summary>
    /// Original V6 XR-Origin traveller. Full-UltimateXR V8 does not use this,
    /// but it remains for compatibility with the separate V6 test scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StencilPortalTraveller : MonoBehaviour
    {
        internal static readonly List<StencilPortalTraveller> ActiveTravellers =
            new List<StencilPortalTraveller>();

        [SerializeField] private Transform _head;
        [SerializeField, Min(0.01f)] private float _teleportCooldown = 0.16f;

        private CharacterController _characterController;
        private Rigidbody _rigidbody;
        private float _nextTeleportTime;

        public Transform Head
        {
            get
            {
                if (_head == null && Camera.main != null)
                    _head = Camera.main.transform;

                return _head != null ? _head : transform;
            }
        }

        public bool CanTeleport
        {
            get
            {
                return isActiveAndEnabled &&
                       Time.unscaledTime >= _nextTeleportTime;
            }
        }

        public void ConfigureHead(Transform head)
        {
            _head = head;
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (_characterController == null)
            {
                _characterController =
                    GetComponentInChildren<CharacterController>(true);
            }

            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (!ActiveTravellers.Contains(this))
                ActiveTravellers.Add(this);
        }

        private void OnDisable()
        {
            ActiveTravellers.Remove(this);
        }

        public void TeleportThrough(StencilPortal source)
        {
            if (source == null ||
                source.Target == null ||
                !CanTeleport)
            {
                return;
            }

            Transform head = Head;

            Vector3 destinationHeadPosition =
                source.TransformPointToTarget(head.position);

            Quaternion destinationRootRotation =
                source.TransformRotationToTarget(transform.rotation);

            bool controllerWasEnabled =
                _characterController != null &&
                _characterController.enabled;

            if (controllerWasEnabled)
                _characterController.enabled = false;

            Vector3 oldVelocity =
                _rigidbody != null
                    ? _rigidbody.velocity
                    : Vector3.zero;

            Vector3 oldAngularVelocity =
                _rigidbody != null
                    ? _rigidbody.angularVelocity
                    : Vector3.zero;

            transform.rotation = destinationRootRotation;

            Vector3 rotatedHeadOffset =
                head.position - transform.position;

            transform.position =
                destinationHeadPosition -
                rotatedHeadOffset +
                source.Target.transform.forward *
                source.Target.ExitOffset;

            if (_rigidbody != null)
            {
                _rigidbody.position = transform.position;
                _rigidbody.rotation = transform.rotation;

                _rigidbody.velocity =
                    source.TransformDirectionToTarget(oldVelocity);

                _rigidbody.angularVelocity =
                    source.TransformDirectionToTarget(oldAngularVelocity);
            }

            Physics.SyncTransforms();

            if (controllerWasEnabled)
                _characterController.enabled = true;

            _nextTeleportTime =
                Time.unscaledTime + _teleportCooldown;
        }
    }
}
