using System;
using UltimateXR.Avatar;
using UnityEngine;
using UnityEngine.Rendering;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Keeps the full player body visible while hiding only local head/face
    /// geometry that would otherwise surround the headset camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V8FirstPersonBodyVisibility : MonoBehaviour
    {
        [SerializeField] private UxrAvatar _avatar;

        private static readonly string[] HeadNameParts =
        {
            "head",
            "face",
            "eye",
            "hair",
            "helmet",
            "visor"
        };

        private void Awake()
        {
            if (_avatar == null)
                _avatar = GetComponent<UxrAvatar>();

            if (_avatar == null)
                _avatar = GetComponentInParent<UxrAvatar>();

            ConfigureRenderers();
        }

        private void ConfigureRenderers()
        {
            if (_avatar == null)
                return;

            Renderer[] renderers =
                _avatar.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                string lower =
                    renderer.name.ToLowerInvariant();

                bool isHeadRenderer = false;

                foreach (string namePart in HeadNameParts)
                {
                    if (lower.Contains(namePart))
                    {
                        isHeadRenderer = true;
                        break;
                    }
                }

                if (!isHeadRenderer)
                {
                    renderer.enabled = true;
                    continue;
                }

                // Head geometry remains available for portal proxies, mirrors,
                // shadows and remote-player views but is not drawn normally
                // around the local camera.
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }
    }
}
