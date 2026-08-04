using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Rig-independent finger posing. Each finger bone computes its own curl
    /// axis from the real left/right skeleton geometry, so the left hand is
    /// never produced by reversing a right-hand rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdaptiveControllerHandPoseV8 : MonoBehaviour
    {
        private enum FingerGroup
        {
            Thumb,
            Index,
            Grip
        }

        private sealed class BonePose
        {
            public Transform Bone;
            public Quaternion OpenRotation;
            public Vector3 LocalCurlAxis;
            public float Angle;
            public FingerGroup Group;
        }

        [SerializeField] private ActionBasedController _controller;
        [SerializeField, Range(0.01f, 0.8f)] private float _smoothing = 0.20f;
        [SerializeField, Range(10f, 110f)] private float _proximalAngle = 54f;
        [SerializeField, Range(10f, 120f)] private float _middleAngle = 72f;
        [SerializeField, Range(10f, 120f)] private float _distalAngle = 78f;
        [SerializeField, Range(5f, 90f)] private float _thumbAngle = 42f;

        private readonly List<BonePose> _bones =
            new List<BonePose>();

        private float _grip;
        private float _trigger;

        public void Configure(ActionBasedController controller)
        {
            _controller = controller;
        }

        private void Awake()
        {
            RebuildBoneMap();
        }

        private void OnEnable()
        {
            if (_bones.Count == 0)
                RebuildBoneMap();
        }

        private void LateUpdate()
        {
            if (_controller == null)
                return;

            float targetGrip = ReadAction(_controller.selectAction);
            float targetTrigger = ReadAction(_controller.activateAction);

            float blend =
                1f - Mathf.Pow(
                    1f - Mathf.Clamp01(_smoothing),
                    Time.unscaledDeltaTime * 90f);

            _grip = Mathf.Lerp(_grip, targetGrip, blend);
            _trigger = Mathf.Lerp(_trigger, targetTrigger, blend);

            foreach (BonePose pose in _bones)
            {
                if (pose.Bone == null)
                    continue;

                float amount;

                switch (pose.Group)
                {
                    case FingerGroup.Index:
                        amount =
                            Mathf.Max(
                                _trigger,
                                _grip * 0.22f);
                        break;

                    case FingerGroup.Thumb:
                        amount =
                            Mathf.Max(
                                _grip * 0.85f,
                                _trigger * 0.25f);
                        break;

                    default:
                        amount = _grip;
                        break;
                }

                pose.Bone.localRotation =
                    pose.OpenRotation *
                    Quaternion.AngleAxis(
                        pose.Angle * amount,
                        pose.LocalCurlAxis);
            }
        }
        private static float ReadAction(
            UnityEngine.InputSystem.InputActionProperty property)
        {
            if (property.action == null ||
                !property.action.enabled)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                property.action.ReadValue<float>());
        }

        public void RebuildBoneMap()
        {
            _bones.Clear();

            Transform[] transforms =
                GetComponentsInChildren<Transform>(true);

            Transform wrist = FindNamed(transforms, "wrist");

            List<Transform> knuckles =
                new List<Transform>();

            foreach (Transform transformValue in transforms)
            {
                string lower =
                    transformValue.name.ToLowerInvariant();

                if ((lower.Contains("index") ||
                     lower.Contains("middle") ||
                     lower.Contains("ring") ||
                     lower.Contains("little") ||
                     lower.Contains("pinky")) &&
                    (lower.Contains("proximal") ||
                     lower.Contains("metacarpal")))
                {
                    knuckles.Add(transformValue);
                }
            }

            Vector3 palmCenter = transform.position;

            if (wrist != null && knuckles.Count > 0)
            {
                palmCenter = wrist.position;

                foreach (Transform knuckle in knuckles)
                    palmCenter += knuckle.position;

                palmCenter /= knuckles.Count + 1f;
            }

            foreach (Transform bone in transforms)
            {
                if (bone == transform)
                    continue;

                string lower = bone.name.ToLowerInvariant();

                FingerGroup? group = GetGroup(lower);

                if (!group.HasValue ||
                    lower.Contains("tip") ||
                    lower.Contains("wrist"))
                {
                    continue;
                }

                Transform child = FindFingerChild(bone, lower);

                if (child == null)
                    continue;

                Vector3 fingerDirection =
                    (child.position - bone.position).normalized;

                Vector3 towardPalm =
                    (palmCenter - child.position).normalized;

                Vector3 axisWorld =
                    Vector3.Cross(
                        fingerDirection,
                        towardPalm);

                if (axisWorld.sqrMagnitude < 0.0001f)
                {
                    axisWorld =
                        Vector3.Cross(
                            fingerDirection,
                            transform.up);
                }

                if (axisWorld.sqrMagnitude < 0.0001f)
                    continue;

                float angle =
                    group.Value == FingerGroup.Thumb
                        ? _thumbAngle
                        : GetJointAngle(lower);

                _bones.Add(
                    new BonePose
                    {
                        Bone = bone,
                        OpenRotation = bone.localRotation,
                        LocalCurlAxis =
                            bone.InverseTransformDirection(
                                axisWorld.normalized),
                        Angle = angle,
                        Group = group.Value
                    });
            }

            Debug.Log(
                "[Portal Foundation V8] Adaptive hand pose mapped " +
                _bones.Count +
                " finger bones on " +
                name +
                ".",
                this);
        }

        private float GetJointAngle(string lower)
        {
            if (lower.Contains("distal"))
                return _distalAngle;

            if (lower.Contains("intermediate") ||
                lower.Contains("middle"))
            {
                return _middleAngle;
            }

            return _proximalAngle;
        }

        private static FingerGroup? GetGroup(string lower)
        {
            if (lower.Contains("thumb"))
                return FingerGroup.Thumb;

            if (lower.Contains("index"))
                return FingerGroup.Index;

            if (lower.Contains("middle") ||
                lower.Contains("ring") ||
                lower.Contains("little") ||
                lower.Contains("pinky"))
            {
                return FingerGroup.Grip;
            }

            return null;
        }

        private static Transform FindNamed(
            Transform[] transforms,
            string text)
        {
            foreach (Transform transformValue in transforms)
            {
                if (transformValue.name
                    .IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return transformValue;
                }
            }

            return null;
        }

        private static Transform FindFingerChild(
            Transform bone,
            string boneLower)
        {
            for (int index = 0;
                 index < bone.childCount;
                 ++index)
            {
                Transform child = bone.GetChild(index);
                string childLower =
                    child.name.ToLowerInvariant();

                bool sameFinger =
                    (boneLower.Contains("thumb") &&
                     childLower.Contains("thumb")) ||
                    (boneLower.Contains("index") &&
                     childLower.Contains("index")) ||
                    (boneLower.Contains("middle") &&
                     childLower.Contains("middle")) ||
                    (boneLower.Contains("ring") &&
                     childLower.Contains("ring")) ||
                    ((boneLower.Contains("little") ||
                      boneLower.Contains("pinky")) &&
                     (childLower.Contains("little") ||
                      childLower.Contains("pinky")));

                if (sameFinger)
                    return child;
            }

            return bone.childCount > 0
                ? bone.GetChild(0)
                : null;
        }
    }
}
