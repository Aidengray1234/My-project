using UnityEngine;

namespace DoctorWhoVR.StencilPortalV6
{
    /// <summary>
    /// Places a visual-only clone of the destination room directly behind the
    /// source doorway. Because the main XR camera renders this geometry, Unity
    /// automatically produces correct left/right-eye stereo.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PortalProxyTransform : MonoBehaviour
    {
        private static readonly Quaternion HalfTurn =
            Quaternion.Euler(0f, 180f, 0f);

        [SerializeField] private Transform _sourcePortal;
        [SerializeField] private Transform _targetPortal;
        [SerializeField] private Transform _targetContentRoot;

        public void Configure(
            Transform sourcePortal,
            Transform targetPortal,
            Transform targetContentRoot)
        {
            _sourcePortal = sourcePortal;
            _targetPortal = targetPortal;
            _targetContentRoot = targetContentRoot;

            UpdateTransform();
        }

        private void LateUpdate()
        {
            UpdateTransform();
        }

        private void UpdateTransform()
        {
            if (_sourcePortal == null ||
                _targetPortal == null ||
                _targetContentRoot == null)
            {
                return;
            }

            Vector3 targetLocalPosition =
                _targetPortal.InverseTransformPoint(
                    _targetContentRoot.position);

            targetLocalPosition =
                HalfTurn * targetLocalPosition;

            transform.position =
                _sourcePortal.TransformPoint(targetLocalPosition);

            Quaternion targetLocalRotation =
                Quaternion.Inverse(_targetPortal.rotation) *
                _targetContentRoot.rotation;

            transform.rotation =
                _sourcePortal.rotation *
                HalfTurn *
                targetLocalRotation;

            transform.localScale =
                _targetContentRoot.lossyScale;
        }
    }
}
