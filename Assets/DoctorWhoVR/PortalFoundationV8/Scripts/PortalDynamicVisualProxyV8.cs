using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DoctorWhoVR.StencilPortalV6;

namespace DoctorWhoVR.PortalFoundationV8
{
    /// <summary>
    /// Creates a stencil-only visual clone for hands, held objects, rigidbodies,
    /// and future animated characters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalDynamicVisualProxyV8 : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maximumDistance = 18f;

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
                return;

            _proxyRoot =
                new GameObject(name + " V8 Portal Proxy");

            _proxyRoot.hideFlags = HideFlags.DontSave;

            Dictionary<Transform, Transform> map =
                new Dictionary<Transform, Transform>();

            CloneHierarchy(
                transform,
                _proxyRoot.transform,
                map);

            CopyRenderers(map, proxyShader);
        }

        private void CloneHierarchy(
            Transform source,
            Transform parent,
            Dictionary<Transform, Transform> map)
        {
            GameObject cloneObject =
                new GameObject(source.name);

            Transform clone = cloneObject.transform;

            clone.SetParent(parent, false);
            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;

            map[source] = clone;

            for (int index = 0;
                 index < source.childCount;
                 ++index)
            {
                CloneHierarchy(
                    source.GetChild(index),
                    clone,
                    map);
            }
        }

        private void CopyRenderers(
            Dictionary<Transform, Transform> map,
            Shader proxyShader)
        {
            foreach (
                MeshRenderer sourceRenderer in
                GetComponentsInChildren<MeshRenderer>(true))
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
                    cloneTransform.gameObject.AddComponent<MeshFilter>();

                cloneFilter.sharedMesh = sourceFilter.sharedMesh;

                MeshRenderer cloneRenderer =
                    cloneTransform.gameObject.AddComponent<MeshRenderer>();

                ConfigureRenderer(
                    sourceRenderer,
                    cloneRenderer,
                    proxyShader);
            }

            foreach (
                SkinnedMeshRenderer sourceRenderer in
                GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform cloneTransform =
                    map[sourceRenderer.transform];

                SkinnedMeshRenderer cloneRenderer =
                    cloneTransform.gameObject
                        .AddComponent<SkinnedMeshRenderer>();

                cloneRenderer.sharedMesh = sourceRenderer.sharedMesh;
                cloneRenderer.localBounds = sourceRenderer.localBounds;
                cloneRenderer.updateWhenOffscreen = true;

                Transform[] sourceBones = sourceRenderer.bones;
                Transform[] cloneBones =
                    new Transform[sourceBones.Length];

                for (int index = 0;
                     index < sourceBones.Length;
                     ++index)
                {
                    Transform sourceBone = sourceBones[index];

                    cloneBones[index] =
                        sourceBone != null &&
                        map.ContainsKey(sourceBone)
                            ? map[sourceBone]
                            : cloneTransform;
                }

                cloneRenderer.bones = cloneBones;

                if (sourceRenderer.rootBone != null &&
                    map.ContainsKey(sourceRenderer.rootBone))
                {
                    cloneRenderer.rootBone =
                        map[sourceRenderer.rootBone];
                }

                ConfigureRenderer(
                    sourceRenderer,
                    cloneRenderer,
                    proxyShader);
            }
        }

        private void ConfigureRenderer(
            Renderer source,
            Renderer destination,
            Shader proxyShader)
        {
            Material[] sourceMaterials = source.sharedMaterials;
            int count = Mathf.Max(1, sourceMaterials.Length);
            Material[] materials = new Material[count];

            for (int index = 0; index < count; ++index)
            {
                Material original =
                    sourceMaterials.Length > 0
                        ? sourceMaterials[
                            Mathf.Min(
                                index,
                                sourceMaterials.Length - 1)]
                        : null;

                Material proxy = new Material(proxyShader);

                Color color = Color.white;

                if (original != null)
                {
                    if (original.HasProperty("_BaseColor"))
                        color = original.GetColor("_BaseColor");
                    else if (original.HasProperty("_Color"))
                        color = original.GetColor("_Color");
                }

                proxy.SetColor("_BaseColor", color);
                materials[index] = proxy;
                _runtimeMaterials.Add(proxy);
            }

            destination.sharedMaterials = materials;
            destination.shadowCastingMode = ShadowCastingMode.Off;
            destination.receiveShadows = false;
        }

        private void UpdateProxyTransform()
        {
            if (_proxyRoot == null)
                return;

            StencilPortal[] portals =
                FindObjectsOfType<StencilPortal>();

            StencilPortal nearest = null;
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
                    nearest = portal;
                }
            }

            if (nearest == null ||
                nearest.Target == null ||
                nearestDistance > _maximumDistance)
            {
                _proxyRoot.SetActive(false);
                return;
            }

            Matrix4x4 mapped =
                nearest.TransformMatrixToTarget(
                    transform.localToWorldMatrix);

            Vector4 positionColumn = mapped.GetColumn(3);

            _proxyRoot.transform.SetPositionAndRotation(
                new Vector3(
                    positionColumn.x,
                    positionColumn.y,
                    positionColumn.z),
                mapped.rotation);

            _proxyRoot.transform.localScale = mapped.lossyScale;
            _proxyRoot.SetActive(true);
        }
    }
}
