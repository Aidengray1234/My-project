using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.V4
{
    /// <summary>
    /// Put this on the XR Origin root. It moves the complete tracked rig through
    /// a DoorwayPortal while preserving the headset's real-world offset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XRDoorwayTraveller : MonoBehaviour
    {
        internal static readonly List<XRDoorwayTraveller> ActiveTravellers =
            new List<XRDoorwayTraveller>();

        [SerializeField] private Transform _head;
        [SerializeField, Min(0.01f)] private float _teleportCooldown = 0.15f;

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

        public void TeleportThrough(DoorwayPortal source)
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

            // Recalculate after rotating the XR Origin so the user's tracked
            // room-scale head offset remains exactly where it should be.
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
