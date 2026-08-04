using UltimateXR.Avatar;
using UltimateXR.Core;
using UltimateXR.Manipulation;
using UnityEngine;
using UnityEngine.XR;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Direct OpenXR grip input for UltimateXR grabbing.
    ///
    /// This bypasses device-name matching problems while still using
    /// UltimateXR's real UxrGrabber, UxrGrabbableObject and UxrGrabManager
    /// manipulation system.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class V8DirectOpenXrGripDriver : MonoBehaviour
    {
        [SerializeField] private UxrAvatar _avatar;
        [SerializeField, Range(0.1f, 1f)] private float _pressThreshold = 0.62f;
        [SerializeField, Range(0f, 0.9f)] private float _releaseThreshold = 0.32f;
        [SerializeField, Min(0.05f)] private float _testObjectGrabDistance = 0.30f;

        private bool _leftPressed;
        private bool _rightPressed;

        private void Awake()
        {
            if (_avatar == null)
                _avatar = GetComponent<UxrAvatar>();

            if (_avatar == null)
                _avatar = GetComponentInParent<UxrAvatar>();
        }

        private void Start()
        {
            ConfigureSceneGrabbables();
        }

        private void Update()
        {
            if (_avatar == null ||
                UxrGrabManager.Instance == null)
            {
                return;
            }

            UxrGrabManager.Instance.IsGrabbingAllowed = true;

            UpdateHand(
                UxrHandSide.Left,
                XRNode.LeftHand,
                ref _leftPressed);

            UpdateHand(
                UxrHandSide.Right,
                XRNode.RightHand,
                ref _rightPressed);
        }

        private void UpdateHand(
            UxrHandSide handSide,
            XRNode node,
            ref bool wasPressed)
        {
            InputDevice device =
                InputDevices.GetDeviceAtXRNode(node);

            float gripValue =
                ReadGrip(device);

            bool gripButton =
                ReadGripButton(device);

            bool pressedNow =
                wasPressed
                    ? gripButton ||
                      gripValue > _releaseThreshold
                    : gripButton ||
                      gripValue >= _pressThreshold;

            if (pressedNow && !wasPressed)
            {
                UxrGrabManager.Instance.TryGrab(
                    _avatar,
                    handSide);
            }
            else if (!pressedNow && wasPressed)
            {
                ReleaseHand(handSide);
            }

            wasPressed = pressedNow;
        }

        private void ReleaseHand(UxrHandSide handSide)
        {
            UxrGrabber[] grabbers =
                _avatar.GetComponentsInChildren<
                    UxrGrabber>(true);

            foreach (UxrGrabber grabber in grabbers)
            {
                if (grabber == null ||
                    grabber.Side != handSide ||
                    grabber.GrabbedObject == null)
                {
                    continue;
                }

                UxrGrabbableObject heldObject =
                    grabber.GrabbedObject;

                UxrGrabManager.Instance.ReleaseObject(
                    grabber,
                    heldObject,
                    true);
            }
        }

        private void ConfigureSceneGrabbables()
        {
            UxrGrabbableObject[] grabbables =
                FindObjectsOfType<UxrGrabbableObject>(true);

            foreach (UxrGrabbableObject grabbable in grabbables)
            {
                if (grabbable == null)
                    continue;

                Rigidbody body =
                    grabbable.GetComponent<Rigidbody>();

                if (body != null)
                {
                    grabbable.RigidBodySource = body;
                    body.isKinematic = false;
                    body.useGravity = true;
                    body.interpolation =
                        RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode =
                        CollisionDetectionMode.ContinuousDynamic;
                }

                if (grabbable.GrabPointCount <= 0)
                    continue;

                UxrGrabPointInfo point =
                    grabbable.GetGrabPoint(0);

                point.BothHandsCompatible = true;
                point.UseDefaultGrabButtons = true;
                point.MaxDistanceGrab =
                    Mathf.Max(
                        point.MaxDistanceGrab,
                        _testObjectGrabDistance);

                // Keep the object's exact relative position/orientation
                // when grabbed instead of snapping it sideways or upside down.
                point.SnapMode =
                    UxrSnapToHandMode.DontSnap;
            }
        }

        private static float ReadGrip(
            InputDevice device)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(
                    CommonUsages.grip,
                    out float value))
            {
                return value;
            }

            return 0f;
        }

        private static bool ReadGripButton(
            InputDevice device)
        {
            return
                device.isValid &&
                device.TryGetFeatureValue(
                    CommonUsages.gripButton,
                    out bool value) &&
                value;
        }
    }
}
