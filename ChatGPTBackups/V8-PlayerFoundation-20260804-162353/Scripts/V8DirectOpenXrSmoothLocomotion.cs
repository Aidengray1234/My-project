using UltimateXR.Avatar;
using UltimateXR.Core;
using UnityEngine;
using UnityEngine.XR;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Direct OpenXR smooth locomotion for the current V8 avatar.
    ///
    /// Left primary joystick:
    ///     Smooth movement relative to the headset.
    ///
    /// Right primary joystick:
    ///     Continuous smooth rotation.
    ///
    /// This component does not use teleport locomotion or a pointer.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class V8DirectOpenXrSmoothLocomotion : MonoBehaviour
    {
        [SerializeField] private UxrAvatar _avatar;

        [Header("Smooth Movement")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float _sprintSpeed = 4.1f;
        [SerializeField, Range(0f, 0.95f)] private float _deadZone = 0.16f;
        [SerializeField] private bool _useGripAsSprint;

        [Header("Smooth Turning")]
        [SerializeField, Min(1f)] private float _turnDegreesPerSecond = 120f;

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField, Min(0.05f)] private float _capsuleRadius = 0.24f;
        [SerializeField] private float _gravity = -9.81f;

        private float _verticalVelocity;

        private void Awake()
        {
            if (_avatar == null)
                _avatar = GetComponent<UxrAvatar>();

            if (_avatar == null)
                _avatar = GetComponentInParent<UxrAvatar>();
        }

        private void Update()
        {
            if (_avatar == null ||
                _avatar.CameraComponent == null ||
                UxrManager.Instance == null)
            {
                return;
            }

            InputDevice leftDevice =
                InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            InputDevice rightDevice =
                InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector2 leftAxis =
                ReadPrimaryAxis(leftDevice);

            Vector2 rightAxis =
                ReadPrimaryAxis(rightDevice);

            leftAxis = ApplyDeadZone(leftAxis);
            rightAxis = ApplyDeadZone(rightAxis);

            UpdateSmoothTurn(rightAxis.x);
            UpdateSmoothMovement(leftDevice, leftAxis);
            UpdateGravity();
        }

        private void UpdateSmoothTurn(float turnAxis)
        {
            if (Mathf.Abs(turnAxis) <= 0.001f)
                return;

            UxrManager.Instance.RotateAvatar(
                _avatar,
                turnAxis *
                _turnDegreesPerSecond *
                Time.deltaTime);
        }

        private void UpdateSmoothMovement(
            InputDevice leftDevice,
            Vector2 axis)
        {
            if (axis.sqrMagnitude <= 0.0001f)
                return;

            Transform cameraTransform =
                _avatar.CameraComponent.transform;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    cameraTransform.forward,
                    Vector3.up).normalized;

            Vector3 right =
                Vector3.ProjectOnPlane(
                    cameraTransform.right,
                    Vector3.up).normalized;

            Vector3 direction =
                forward * axis.y +
                right * axis.x;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            bool sprinting =
                _useGripAsSprint &&
                ReadGrip(leftDevice) >= 0.75f;

            float speed =
                sprinting ? _sprintSpeed : _walkSpeed;

            Vector3 requestedTranslation =
                direction *
                speed *
                Time.deltaTime;

            Vector3 acceptedTranslation =
                ResolveHorizontalCollision(
                    requestedTranslation);

            if (acceptedTranslation.sqrMagnitude > 0f)
            {
                UxrManager.Instance.TranslateAvatar(
                    _avatar,
                    acceptedTranslation);
            }
        }

        private Vector3 ResolveHorizontalCollision(
            Vector3 requestedTranslation)
        {
            float distance =
                requestedTranslation.magnitude;

            if (distance <= 0.00001f)
                return Vector3.zero;

            Vector3 floorPosition =
                _avatar.CameraFloorPosition;

            Vector3 cameraPosition =
                _avatar.CameraPosition;

            float standingHeight =
                Mathf.Max(
                    0.8f,
                    cameraPosition.y - floorPosition.y);

            float radius =
                Mathf.Min(
                    _capsuleRadius,
                    standingHeight * 0.45f);

            Vector3 bottom =
                floorPosition +
                Vector3.up * radius;

            Vector3 top =
                floorPosition +
                Vector3.up *
                Mathf.Max(
                    radius,
                    standingHeight - radius);

            Vector3 direction =
                requestedTranslation / distance;

            bool blocked =
                Physics.CapsuleCast(
                    bottom,
                    top,
                    radius,
                    direction,
                    out RaycastHit _,
                    distance + 0.015f,
                    _collisionMask,
                    QueryTriggerInteraction.Ignore);

            return blocked
                ? Vector3.zero
                : requestedTranslation;
        }

        private void UpdateGravity()
        {
            Vector3 floorPosition =
                _avatar.CameraFloorPosition;

            bool grounded =
                Physics.SphereCast(
                    floorPosition + Vector3.up * 0.16f,
                    0.12f,
                    Vector3.down,
                    out RaycastHit _,
                    0.24f,
                    _collisionMask,
                    QueryTriggerInteraction.Ignore);

            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -0.5f;
                return;
            }

            _verticalVelocity +=
                _gravity * Time.deltaTime;

            Vector3 verticalTranslation =
                Vector3.up *
                _verticalVelocity *
                Time.deltaTime;

            UxrManager.Instance.TranslateAvatar(
                _avatar,
                verticalTranslation);
        }

        private Vector2 ApplyDeadZone(Vector2 value)
        {
            float magnitude = value.magnitude;

            if (magnitude <= _deadZone)
                return Vector2.zero;

            float scaledMagnitude =
                Mathf.InverseLerp(
                    _deadZone,
                    1f,
                    Mathf.Min(1f, magnitude));

            return value.normalized * scaledMagnitude;
        }

        private static Vector2 ReadPrimaryAxis(
            InputDevice device)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(
                    CommonUsages.primary2DAxis,
                    out Vector2 axis))
            {
                return axis;
            }

            return Vector2.zero;
        }

        private static float ReadGrip(
            InputDevice device)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(
                    CommonUsages.grip,
                    out float grip))
            {
                return grip;
            }

            return 0f;
        }
    }
}
