using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Controller-driven pose animation for the Unity XR Hands sample meshes.
    /// It also supports similarly named third-party hand rigs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerHandPoseDriver : MonoBehaviour
    {
        private sealed class BonePose
        {
            public Transform Bone;
            public Quaternion OpenRotation;
            public float Weight;
        }

        [SerializeField] private ActionBasedController _controller;
        [SerializeField] private bool _isLeftHand;
        [SerializeField, Range(0f, 1f)] private float _poseSmoothing = 0.22f;
        [SerializeField] private Vector3 _curlAxis = Vector3.right;
        [SerializeField, Range(0f, 110f)] private float _fingerCurlAngle = 72f;
        [SerializeField, Range(0f, 90f)] private float _thumbCurlAngle = 44f;

        private readonly List<BonePose> _indexBones = new List<BonePose>();
        private readonly List<BonePose> _gripBones = new List<BonePose>();
        private readonly List<BonePose> _thumbBones = new List<BonePose>();

        private float _smoothedGrip;
        private float _smoothedTrigger;

        public void Configure(
            ActionBasedController controller,
            bool isLeftHand)
        {
            _controller = controller;
            _isLeftHand = isLeftHand;
        }

        private void Awake()
        {
            CacheBones();
        }

        private void OnEnable()
        {
            if (_indexBones.Count == 0 &&
                _gripBones.Count == 0 &&
                _thumbBones.Count == 0)
            {
                CacheBones();
            }
        }

        private void LateUpdate()
        {
            if (_controller == null)
                return;

            float grip = 0f;
            float trigger = 0f;

            if (_controller.selectAction.action != null &&
                _controller.selectAction.action.enabled)
            {
                grip =
                    _controller.selectAction.action.ReadValue<float>();
            }

            if (_controller.activateAction.action != null &&
                _controller.activateAction.action.enabled)
            {
                trigger =
                    _controller.activateAction.action.ReadValue<float>();
            }

            float smoothing =
                1f - Mathf.Pow(
                    1f - Mathf.Clamp01(_poseSmoothing),
                    Time.unscaledDeltaTime * 90f);

            _smoothedGrip =
                Mathf.Lerp(_smoothedGrip, grip, smoothing);

            _smoothedTrigger =
                Mathf.Lerp(_smoothedTrigger, trigger, smoothing);

            ApplyBones(
                _indexBones,
                Mathf.Max(_smoothedTrigger, _smoothedGrip * 0.35f),
                _fingerCurlAngle);

            ApplyBones(
                _gripBones,
                _smoothedGrip,
                _fingerCurlAngle);

            ApplyBones(
                _thumbBones,
                Mathf.Max(_smoothedGrip, _smoothedTrigger * 0.35f),
                _thumbCurlAngle);
        }

        private void CacheBones()
        {
            _indexBones.Clear();
            _gripBones.Clear();
            _thumbBones.Clear();

            Transform[] bones =
                GetComponentsInChildren<Transform>(true);

            foreach (Transform bone in bones)
            {
                if (bone == transform)
                    continue;

                string lower =
                    bone.name.ToLowerInvariant();

                if (IsTipOrMetacarpal(lower))
                    continue;

                float weight =
                    lower.Contains("distal") ? 1f :
                    lower.Contains("intermediate") ? 0.82f :
                    lower.Contains("proximal") ? 0.62f :
                    0.55f;

                BonePose pose =
                    new BonePose
                    {
                        Bone = bone,
                        OpenRotation = bone.localRotation,
                        Weight = weight
                    };

                if (lower.Contains("thumb"))
                {
                    _thumbBones.Add(pose);
                }
                else if (lower.Contains("index"))
                {
                    _indexBones.Add(pose);
                }
                else if (lower.Contains("middle") ||
                         lower.Contains("ring") ||
                         lower.Contains("little") ||
                         lower.Contains("pinky"))
                {
                    _gripBones.Add(pose);
                }
            }
        }

        private static bool IsTipOrMetacarpal(string lower)
        {
            return
                lower.Contains("tip") ||
                lower.Contains("metacarpal") ||
                lower.Contains("wrist");
        }

        private void ApplyBones(
            List<BonePose> bones,
            float amount,
            float maximumAngle)
        {
            Vector3 axis =
                _curlAxis.sqrMagnitude > 0.001f
                    ? _curlAxis.normalized
                    : Vector3.right;

            float mirror =
                _isLeftHand ? -1f : 1f;

            foreach (BonePose pose in bones)
            {
                if (pose.Bone == null)
                    continue;

                Quaternion curl =
                    Quaternion.AngleAxis(
                        maximumAngle *
                        pose.Weight *
                        amount *
                        mirror,
                        axis);

                pose.Bone.localRotation =
                    pose.OpenRotation * curl;
            }
        }
    }
}
