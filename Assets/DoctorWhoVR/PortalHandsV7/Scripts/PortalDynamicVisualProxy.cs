using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalHandsV7
{
    /// <summary>
    /// Builds a render-only clone of a dynamic object and places that clone in
    /// the opposite portal's stencil-proxy space. The object stays visible
    /// through the doorway while moving, being held, or crossing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalDynamicVisualProxy : MonoBehaviour
    {
        private static readonly Quaternion HalfTurn =
            Quaternion.Euler(0f, 180f, 0f);

        [SerializeField] private bool _includeInactiveRenderers = true;
        [SerializeField, Min(1f)] private float _maximumPortalDistance = 40f;

        private GameObject _proxyRoot;
        private readonly List<Material> _runtimeMaterials =
            new List<Material>();

        private void Start()
        {
            BuildProxy();
        }

        private void LateUpdate()
        {
            UpdateProxyTransform();
        }

        private void OnDestroy()
        {
            if (_proxyRoot != null)
                Destroy(_proxyRoot);

            foreach (Material material in _runtimeMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
        }

        private void BuildProxy()
        {
            if (_proxyRoot != null)
                return;

            Shader proxyShader =
                Shader.Find(
                    "DoctorWhoVR/StencilPortalV6/ProxyLit");

            if (proxyShader == null)
            {
                Debug.LogError(
                    "[Portal Hands V7] ProxyLit shader was not found.",
                    this);
                return;
            }

            _proxyRoot =
                new GameObject(name + " Portal Visual Proxy");

            _proxyRoot.hideFlags =
                HideFlags.DontSave;

            Dictionary<Transform, Transform> map =
                new Dictionary<Transform, Transform>();

            Transform clonedRoot =
                CloneTransformHierarchy(
                    transform,
                    _proxyRoot.transform,
                    map);

            CopyRenderers(
                transform,
                map,
                proxyShader);

            clonedRoot.localPosition = Vector3.zero;
            clonedRoot.localRotation = Quaternion.identity;
            clonedRoot.localScale = Vector3.one;
        }

        private Transform CloneTransformHierarchy(
            Transform source,
            Transform parent,
            Dictionary<Transform, Transform> map)
        {
            GameObject cloneObject =
                new GameObject(source.name);

            Transform clone =
                cloneObject.transform;

            clone.SetParent(parent, false);
            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;

            map[source] = clone;

            for (int index = 0;
                 index < source.childCount;
                 ++index)
            {
                CloneTransformHierarchy(
                    source.GetChild(index),
                    clone,
                    map);
            }

            return clone;
        }

        private void CopyRenderers(
            Transform sourceRoot,
            Dictionary<Transform, Transform> map,
            Shader proxyShader)
        {
            MeshRenderer[] meshRenderers =
                sourceRoot.GetComponentsInChildren<MeshRenderer>(
                    _includeInactiveRenderers);

            foreach (MeshRenderer sourceRenderer in meshRenderers)
            {
                MeshFilter sourceFilter =
                    sourceRenderer.GetComponent<MeshFilter>();

                if (sourceFilter == null ||
                    sourceFilter.sharedMesh == null)
                {
                    continue;
                }

                Transform cloneTransform =
                    map[sourceRenderer.transform];

                MeshFilter cloneFilter =
                    cloneTransform.gameObject
                        .AddComponent<MeshFilter>();

                cloneFilter.sharedMesh =
                    sourceFilter.sharedMesh;

                MeshRenderer cloneRenderer =
                    cloneTransform.gameObject
                        .AddComponent<MeshRenderer>();

                CopyRendererSettings(
                    sourceRenderer,
                    cloneRenderer,
                    proxyShader);
            }

            SkinnedMeshRenderer[] skinnedRenderers =
                sourceRoot.GetComponentsInChildren<
                    SkinnedMeshRenderer>(
                    _includeInactiveRenderers);

            foreach (
                SkinnedMeshRenderer sourceRenderer in
                skinnedRenderers)
            {
                Transform cloneTransform =
                    map[sourceRenderer.transform];

                SkinnedMeshRenderer cloneRenderer =
                    cloneTransform.gameObject
                        .AddComponent<SkinnedMeshRenderer>();

                cloneRenderer.sharedMesh =
                    sourceRenderer.sharedMesh;

                cloneRenderer.rootBone =
                    sourceRenderer.rootBone != null &&
                    map.ContainsKey(sourceRenderer.rootBone)
                        ? map[sourceRenderer.rootBone]
                        : cloneTransform;

                Transform[] sourceBones =
                    sourceRenderer.bones;

                Transform[] cloneBones =
                    new Transform[sourceBones.Length];

                for (int index = 0;
                     index < sourceBones.Length;
                     ++index)
                {
                    Transform sourceBone =
                        sourceBones[index];

                    cloneBones[index] =
                        sourceBone != null &&
                        map.ContainsKey(sourceBone)
                            ? map[sourceBone]
                            : cloneTransform;
                }

                cloneRenderer.bones = cloneBones;
                cloneRenderer.localBounds =
                    sourceRenderer.localBounds;
                cloneRenderer.updateWhenOffscreen = true;

                CopyRendererSettings(
                    sourceRenderer,
                    cloneRenderer,
                    proxyShader);
            }
        }

        private void CopyRendererSettings(
            Renderer source,
            Renderer destination,
            Shader proxyShader)
        {
            Material[] sourceMaterials =
                source.sharedMaterials;

            Material[] proxyMaterials =
                new Material[
                    Mathf.Max(1, sourceMaterials.Length)];

            for (int index = 0;
                 index < proxyMaterials.Length;
                 ++index)
            {
                Material sourceMaterial =
                    sourceMaterials.Length > 0
                        ? sourceMaterials[
                            Mathf.Min(
                                index,
                                sourceMaterials.Length - 1)]
                        : null;

                Material proxyMaterial =
                    new Material(proxyShader);

                proxyMaterial.name =
                    name + " Proxy Material";

                Color color = Color.white;

                if (sourceMaterial != null)
                {
                    if (sourceMaterial.HasProperty("_BaseColor"))
                    {
                        color =
                            sourceMaterial.GetColor("_BaseColor");
                    }
                    else if (sourceMaterial.HasProperty("_Color"))
                    {
                        color =
                            sourceMaterial.GetColor("_Color");
                    }
                }

                proxyMaterial.SetColor(
                    "_BaseColor",
                    color);

                proxyMaterials[index] = proxyMaterial;
                _runtimeMaterials.Add(proxyMaterial);
            }

            destination.sharedMaterials =
                proxyMaterials;

            destination.shadowCastingMode =
                ShadowCastingMode.Off;

            destination.receiveShadows = false;
            destination.enabled = source.enabled;
        }

        private void UpdateProxyTransform()
        {
            if (_proxyRoot == null)
                return;

            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            if (portals.Length < 2)
            {
                _proxyRoot.SetActive(false);
                return;
            }

            StencilPortal currentPortal = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (StencilPortal portal in portals)
            {
                float distance =
                    Vector3.Distance(
                        transform.position,
                        portal.transform.position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    currentPortal = portal;
                }
            }

            if (currentPortal == null ||
                currentPortal.Target == null ||
                nearestDistance > _maximumPortalDistance)
            {
                _proxyRoot.SetActive(false);
                return;
            }

            StencilPortal viewPortal =
                currentPortal.Target;

            Matrix4x4 mapping =
                viewPortal.transform.localToWorldMatrix *
                Matrix4x4.Rotate(HalfTurn) *
                currentPortal.transform.worldToLocalMatrix *
                transform.localToWorldMatrix;

            Vector4 positionColumn =
                mapping.GetColumn(3);

            Vector3 position =
                new Vector3(
                    positionColumn.x,
                    positionColumn.y,
                    positionColumn.z);

            _proxyRoot.transform.SetPositionAndRotation(
                position,
                mapping.rotation);

            _proxyRoot.transform.localScale =
                mapping.lossyScale;

            _proxyRoot.SetActive(true);
        }
    }
}
