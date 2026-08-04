using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.RealPortalV5
{
    /// <summary>
    /// Moves the entire XR Origin through a linked portal while preserving the
    /// player's tracked room-scale head offset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VRPortalTraveller : MonoBehaviour
    {
        internal static readonly List<VRPortalTraveller> ActiveTravellers =
            new List<VRPortalTraveller>();

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

        public void TeleportThrough(StereoPortal source)
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

            Vector3 linearVelocity =
                _rigidbody != null
                    ? _rigidbody.velocity
                    : Vector3.zero;

            Vector3 angularVelocity =
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
                    source.TransformDirectionToTarget(linearVelocity);

                _rigidbody.angularVelocity =
                    source.TransformDirectionToTarget(angularVelocity);
            }

            Physics.SyncTransforms();

            if (controllerWasEnabled)
                _characterController.enabled = true;

            _nextTeleportTime =
                Time.unscaledTime + _teleportCooldown;
        }
    }
}
