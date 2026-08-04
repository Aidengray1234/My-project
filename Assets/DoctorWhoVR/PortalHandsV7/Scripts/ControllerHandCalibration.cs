using UnityEngine;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Stores a hand model's controller-relative placement in one editable
    /// component. Offsets can be tuned in Play mode and copied afterward.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerHandCalibration : MonoBehaviour
    {
        [SerializeField] private Vector3 _positionOffset =
            new Vector3(0f, -0.015f, 0.025f);

        [SerializeField] private Vector3 _rotationOffset =
            Vector3.zero;

        [SerializeField, Min(0.01f)] private float _uniformScale = 1f;

        public void Configure(
            Vector3 positionOffset,
            Vector3 rotationOffset,
            float uniformScale)
        {
            _positionOffset = positionOffset;
            _rotationOffset = rotationOffset;
            _uniformScale = uniformScale;

            Apply();
        }

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            transform.localPosition = _positionOffset;
            transform.localRotation =
                Quaternion.Euler(_rotationOffset);
            transform.localScale =
                Vector3.one * Mathf.Max(0.01f, _uniformScale);
        }
    }
}
