using System.Collections.Generic;
using UnityEngine;

namespace DoctorWhoVR.Portals
{
    /// <summary>
    /// Place this on the XR Origin root. It moves the complete rig, including
    /// the tracked head and hands, through a portal in one operation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalTraveller : MonoBehaviour
    {
        internal static readonly List<PortalTraveller> ActiveTravellers = new List<PortalTraveller>();

        [SerializeField] private Transform _head;
        [SerializeField, Min(0.01f)] private float _teleportCooldown = 0.12f;

        private CharacterController _characterController;
        private Rigidbody _rigidbody;
        private float _nextAllowedTeleportTime;

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
            get { return isActiveAndEnabled && Time.unscaledTime >= _nextAllowedTeleportTime; }
        }

        public void ConfigureHead(Transform head)
        {
            _head = head;
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
                _characterController = GetComponentInChildren<CharacterController>(true);

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

        public void TeleportThrough(Portal source)
        {
            if (source == null || source.Target == null || !CanTeleport)
                return;

            Transform head = Head;
            Vector3 targetHeadPosition = source.TransformPointToTarget(head.position);
            Quaternion targetRootRotation = source.TransformRotationToTarget(transform.rotation);

            bool controllerWasEnabled = _characterController != null && _characterController.enabled;
            if (controllerWasEnabled)
                _characterController.enabled = false;

            Vector3 oldVelocity = _rigidbody != null ? _rigidbody.velocity : Vector3.zero;
            Vector3 oldAngularVelocity = _rigidbody != null ? _rigidbody.angularVelocity : Vector3.zero;

            transform.rotation = targetRootRotation;

            // Recalculate this after rotating the root so tracked head offset is preserved.
            Vector3 rotatedHeadOffset = head.position - transform.position;
            transform.position = targetHeadPosition - rotatedHeadOffset +
                                 source.Target.transform.forward * source.ExitOffset;

            if (_rigidbody != null)
            {
                _rigidbody.position = transform.position;
                _rigidbody.rotation = transform.rotation;
                _rigidbody.velocity = source.TransformDirectionToTarget(oldVelocity);
                _rigidbody.angularVelocity = source.TransformDirectionToTarget(oldAngularVelocity);
            }

            Physics.SyncTransforms();

            if (controllerWasEnabled)
                _characterController.enabled = true;

            _nextAllowedTeleportTime = Time.unscaledTime + _teleportCooldown;
        }
    }
}
