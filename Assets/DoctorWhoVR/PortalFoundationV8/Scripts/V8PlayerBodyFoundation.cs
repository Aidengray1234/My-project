using UltimateXR.Avatar;
using UnityEngine;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Central reference point for the whole player, not only the hands.
    /// Future physical grabbing, clothing, regeneration, damage, inventory,
    /// portal slicing and multiplayer replication can use these anchors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V8PlayerBodyFoundation : MonoBehaviour
    {
        [SerializeField] private UxrAvatar _avatar;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _chest;
        [SerializeField] private Transform _pelvis;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Transform _leftFoot;
        [SerializeField] private Transform _rightFoot;

        public UxrAvatar Avatar => _avatar;
        public Transform Head => _head;
        public Transform Chest => _chest;
        public Transform Pelvis => _pelvis;
        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;
        public Transform LeftFoot => _leftFoot;
        public Transform RightFoot => _rightFoot;

        public void Configure(
            UxrAvatar avatar,
            Transform head,
            Transform chest,
            Transform pelvis,
            Transform leftHand,
            Transform rightHand,
            Transform leftFoot,
            Transform rightFoot)
        {
            _avatar = avatar;
            _head = head;
            _chest = chest;
            _pelvis = pelvis;
            _leftHand = leftHand;
            _rightHand = rightHand;
            _leftFoot = leftFoot;
            _rightFoot = rightFoot;
        }

        private void Awake()
        {
            if (_avatar == null)
                _avatar = GetComponent<UxrAvatar>();
        }
    }
}
