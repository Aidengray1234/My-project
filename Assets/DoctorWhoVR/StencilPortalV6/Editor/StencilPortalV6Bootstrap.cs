#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.StencilPortalV6;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.StencilPortalV6.Editor
{
    /// <summary>
    /// Creates a clean direct-stereo stencil portal scene. No portal camera,
    /// RenderTexture, stereo matrix override, or oblique projection is used.
    /// </summary>
    [InitializeOnLoad]
    public static class StencilPortalV6Bootstrap
    {
        private const string RootFolder =
            "Assets/DoctorWhoVR/StencilPortalV6";

        private const string SceneFolder =
            RootFolder + "/Scenes";

        private const string MaterialFolder =
            RootFolder + "/Materials";

        private const string ScenePath =
            SceneFolder + "/TARDISStencilPortalV6.unity";

        private const string MaskMaterialPath =
            MaterialFolder + "/PortalMask.mat";

        private const string BackCoverMaterialPath =
            MaterialFolder + "/PortalBackCover.mat";

        static StencilPortalV6Bootstrap()
        {
            EditorApplication.delayCall +=
                BuildAutomaticallyIfNeeded;
        }

        [MenuItem(
            "Doctor Who VR/Stencil Portal V6/" +
            "Rebuild Direct-Stereo Portal Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Stencil Portal V6",
                    "This recreates the direct-stereo stencil portal scene. Continue?",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildScene(true);
        }

        [MenuItem(
            "Doctor Who VR/Stencil Portal V6/" +
            "Open Direct-Stereo Portal Scene")]
        public static void OpenSceneFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildScene(false);
                return;
            }

            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
        }

        private static void BuildAutomaticallyIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            BuildScene(false);
        }

        private static void BuildScene(bool showCompletionDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            Material maskMaterial =
                CreateOrLoadMaterial(
                    MaskMaterialPath,
                    "DoctorWhoVR/StencilPortalV6/PortalMask");

            Material backCoverMaterial =
                CreateOrLoadMaterial(
                    BackCoverMaterialPath,
                    "DoctorWhoVR/StencilPortalV6/BackCover");

            if (maskMaterial == null ||
                backCoverMaterial == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR Stencil Portal V6] " +
                    "Shaders are not ready. Unity will retry after import.");

                EditorApplication.delayCall +=
                    BuildAutomaticallyIfNeeded;

                return;
            }

            Material exteriorActual =
                CreateLitMaterial(
                    MaterialFolder + "/ExteriorActual.mat",
                    new Color(0.08f, 0.13f, 0.20f));

            Material interiorActual =
                CreateLitMaterial(
                    MaterialFolder + "/InteriorActual.mat",
                    new Color(0.30f, 0.16f, 0.07f));

            Material actualFloor =
                CreateLitMaterial(
                    MaterialFolder + "/ActualFloor.mat",
                    new Color(0.11f, 0.12f, 0.14f));

            Material frameMaterial =
                CreateLitMaterial(
                    MaterialFolder + "/PortalFrame.mat",
                    new Color(0.02f, 0.08f, 0.26f));

            Material exteriorProxy =
                CreateProxyMaterial(
                    MaterialFolder + "/ExteriorProxy.mat",
                    new Color(0.08f, 0.13f, 0.20f));

            Material interiorProxy =
                CreateProxyMaterial(
                    MaterialFolder + "/InteriorProxy.mat",
                    new Color(0.30f, 0.16f, 0.07f));

            Material proxyFloor =
                CreateProxyMaterial(
                    MaterialFolder + "/ProxyFloor.mat",
                    new Color(0.11f, 0.12f, 0.14f));

            Material greenProxy =
                CreateProxyMaterial(
                    MaterialFolder + "/GreenProxy.mat",
                    new Color(0.04f, 0.72f, 0.10f));

            Material redProxy =
                CreateProxyMaterial(
                    MaterialFolder + "/RedProxy.mat",
                    new Color(0.78f, 0.04f, 0.03f));

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            SetupLighting();

            GameObject xrSetup = CreateXRSetup();

            if (xrSetup == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR Stencil Portal V6] " +
                    "Could not locate XR Interaction Setup.");

                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(xrSetup))
            {
                PrefabUtility.UnpackPrefabInstance(
                    xrSetup,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            RemoveComfortVignettes(xrSetup);

            GameObject xrOrigin =
                FindChildByName(
                    xrSetup.transform,
                    "XR Origin (XR Rig)");

            Camera xrCamera =
                xrOrigin != null
                    ? xrOrigin.GetComponentInChildren<Camera>(true)
                    : xrSetup.GetComponentInChildren<Camera>(true);

            if (xrOrigin == null || xrCamera == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR Stencil Portal V6] " +
                    "XR Origin or camera was not found.");

                return;
            }

            xrOrigin.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -3.2f),
                Quaternion.identity);

            xrCamera.tag = "MainCamera";

            StencilPortalTraveller traveller =
                xrOrigin.GetComponent<StencilPortalTraveller>();

            if (traveller == null)
            {
                traveller =
                    xrOrigin.AddComponent<StencilPortalTraveller>();
            }

            traveller.ConfigureHead(xrCamera.transform);

            GameObject exteriorRoom =
                CreateRoom(
                    "Exterior Actual Room",
                    Vector3.zero,
                    exteriorActual,
                    actualFloor,
                    true);

            GameObject interiorRoom =
                CreateRoom(
                    "TARDIS Interior Actual Room",
                    new Vector3(0f, 0f, 30f),
                    interiorActual,
                    actualFloor,
                    false);

            GameObject exteriorRedCube =
                CreateTestCube(
                    exteriorRoom.transform,
                    "Exterior Red Landmark",
                    new Vector3(-2.0f, 0.8f, 1.7f),
                    new Vector3(1.2f, 1.6f, 1.2f),
                    CreateLitMaterial(
                        MaterialFolder + "/RedActual.mat",
                        new Color(0.78f, 0.04f, 0.03f)));

            GameObject interiorGreenCube =
                CreateTestCube(
                    interiorRoom.transform,
                    "Interior Green Landmark",
                    new Vector3(1.8f, 1.0f, -0.3f),
                    new Vector3(1.2f, 2.0f, 1.2f),
                    CreateLitMaterial(
                        MaterialFolder + "/GreenActual.mat",
                        new Color(0.04f, 0.72f, 0.10f)));

            StencilPortal exteriorPortal =
                CreatePortal(
                    "Exterior Stencil Portal",
                    new Vector3(0f, 1.75f, 4.96f),
                    Quaternion.Euler(0f, 180f, 0f),
                    maskMaterial,
                    backCoverMaterial,
                    frameMaterial);

            StencilPortal interiorPortal =
                CreatePortal(
                    "Interior Stencil Portal",
                    new Vector3(0f, 1.75f, 25.04f),
                    Quaternion.identity,
                    maskMaterial,
                    backCoverMaterial,
                    frameMaterial);

            exteriorPortal.Configure(interiorPortal);
            interiorPortal.Configure(exteriorPortal);

            GameObject interiorProxyRoom =
                CloneRoomAsProxy(
                    interiorRoom,
                    "Interior Proxy Through Exterior Door",
                    interiorProxy,
                    proxyFloor,
                    greenProxy,
                    "Interior Green Landmark");

            PortalProxyTransform interiorProxyTransform =
                interiorProxyRoom.AddComponent<PortalProxyTransform>();

            interiorProxyTransform.Configure(
                exteriorPortal.transform,
                interiorPortal.transform,
                interiorRoom.transform);

            GameObject exteriorProxyRoom =
                CloneRoomAsProxy(
                    exteriorRoom,
                    "Exterior Proxy Through Interior Door",
                    exteriorProxy,
                    proxyFloor,
                    redProxy,
                    "Exterior Red Landmark");

            PortalProxyTransform exteriorProxyTransform =
                exteriorProxyRoom.AddComponent<PortalProxyTransform>();

            exteriorProxyTransform.Configure(
                interiorPortal.transform,
                exteriorPortal.transform,
                exteriorRoom.transform);

            CreateSafetyFloor();

            GameObject diagnosticsObject =
                new GameObject("Stencil Portal V6 Diagnostics");

            diagnosticsObject.AddComponent<StencilPortalDiagnostics>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes =
                new[]
                {
                    new EditorBuildSettingsScene(
                        ScenePath,
                        true)
                };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[DoctorWhoVR Stencil Portal V6] " +
                "Direct-stereo portal scene created. " +
                "The XR camera renders destination proxy geometry directly.");

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "Stencil Portal V6 Ready",
                    "The direct-stereo portal scene was rebuilt.\n\n" +
                    "This version uses no portal camera or RenderTexture.",
                    "OK");
            }
        }

        private static GameObject CreateXRSetup()
        {
            string[] prefabGuids =
                AssetDatabase.FindAssets(
                    "XR Interaction Setup t:Prefab",
                    new[] { "Assets" });

            foreach (string guid in prefabGuids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null ||
                    prefab.name != "XR Interaction Setup")
                {
                    continue;
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(prefab)
                    as GameObject;

                if (instance != null)
                {
                    instance.name = "XR Interaction Setup";
                    return instance;
                }
            }

            return null;
        }

        private static void RemoveComfortVignettes(GameObject root)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in transforms.Reverse())
            {
                if (child == null ||
                    child == root.transform)
                {
                    continue;
                }

                if (child.name.Equals(
                        "TunnelingVignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals(
                        "Tunneling Vignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals(
                        "Locomotion Vignette",
                        StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.DestroyImmediate(
                        child.gameObject);
                }
            }
        }

        private static GameObject FindChildByName(
            Transform root,
            string objectName)
        {
            foreach (
                Transform child in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }

            return null;
        }

        private static GameObject CreateRoom(
            string roomName,
            Vector3 center,
            Material wallMaterial,
            Material floorMaterial,
            bool doorwayOnPositiveZ)
        {
            GameObject room = new GameObject(roomName);
            room.transform.position = center;

            CreateCube(
                room.transform,
                "Floor",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(10f, 0.2f, 10f),
                floorMaterial);

            CreateCube(
                room.transform,
                "Ceiling",
                new Vector3(0f, 5.1f, 0f),
                new Vector3(10f, 0.2f, 10f),
                wallMaterial);

            CreateCube(
                room.transform,
                "Left Wall",
                new Vector3(-5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial);

            CreateCube(
                room.transform,
                "Right Wall",
                new Vector3(5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial);

            float solidWallZ =
                doorwayOnPositiveZ ? -5.1f : 5.1f;

            CreateCube(
                room.transform,
                "Solid Wall",
                new Vector3(0f, 2.5f, solidWallZ),
                new Vector3(10f, 5f, 0.2f),
                wallMaterial);

            float doorwayWallZ =
                doorwayOnPositiveZ ? 5.1f : -5.1f;

            CreateDoorwayWall(
                room.transform,
                doorwayWallZ,
                wallMaterial);

            return room;
        }

        private static void CreateDoorwayWall(
            Transform parent,
            float localZ,
            Material material)
        {
            const float openingWidth = 2.7f;
            const float openingHeight = 3.65f;
            const float roomWidth = 10f;
            const float roomHeight = 5f;

            float sideWidth =
                (roomWidth - openingWidth) * 0.5f;

            float sideOffset =
                openingWidth * 0.5f +
                sideWidth * 0.5f;

            CreateCube(
                parent,
                "Doorway Wall Left",
                new Vector3(
                    -sideOffset,
                    roomHeight * 0.5f,
                    localZ),
                new Vector3(
                    sideWidth,
                    roomHeight,
                    0.2f),
                material);

            CreateCube(
                parent,
                "Doorway Wall Right",
                new Vector3(
                    sideOffset,
                    roomHeight * 0.5f,
                    localZ),
                new Vector3(
                    sideWidth,
                    roomHeight,
                    0.2f),
                material);

            float topHeight =
                roomHeight - openingHeight;

            CreateCube(
                parent,
                "Doorway Wall Top",
                new Vector3(
                    0f,
                    openingHeight +
                    topHeight * 0.5f,
                    localZ),
                new Vector3(
                    openingWidth,
                    topHeight,
                    0.2f),
                material);
        }

        private static StencilPortal CreatePortal(
            string objectName,
            Vector3 position,
            Quaternion rotation,
            Material maskMaterial,
            Material backCoverMaterial,
            Material frameMaterial)
        {
            GameObject portalObject =
                new GameObject(objectName);

            portalObject.transform.SetPositionAndRotation(
                position,
                rotation);

            BoxCollider trigger =
                portalObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
            trigger.size =
                new Vector3(2.4f, 3.4f, 0.7f);

            GameObject mask =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad);

            mask.name = "Portal Stencil Mask";
            mask.transform.SetParent(
                portalObject.transform,
                false);

            mask.transform.localPosition =
                new Vector3(0f, 0f, 0.02f);

            // Unity Quad faces local -Z. Rotate it so the front points along
            // the portal object's +Z/source-room direction.
            mask.transform.localRotation =
                Quaternion.Euler(0f, 180f, 0f);

            mask.transform.localScale =
                new Vector3(2.4f, 3.4f, 1f);

            Collider maskCollider =
                mask.GetComponent<Collider>();

            if (maskCollider != null)
                UnityEngine.Object.DestroyImmediate(maskCollider);

            MeshRenderer maskRenderer =
                mask.GetComponent<MeshRenderer>();

            Material[] maskMaterials =
                new Material[]
                {
                    maskMaterial,
                    backCoverMaterial
                };

            maskRenderer.sharedMaterials = maskMaterials;

            CreateFramePiece(
                portalObject.transform,
                "Frame Left",
                new Vector3(-1.35f, 0f, 0.06f),
                new Vector3(0.25f, 3.8f, 0.25f),
                frameMaterial);

            CreateFramePiece(
                portalObject.transform,
                "Frame Right",
                new Vector3(1.35f, 0f, 0.06f),
                new Vector3(0.25f, 3.8f, 0.25f),
                frameMaterial);

            CreateFramePiece(
                portalObject.transform,
                "Frame Top",
                new Vector3(0f, 1.825f, 0.06f),
                new Vector3(2.95f, 0.25f, 0.25f),
                frameMaterial);

            CreateFramePiece(
                portalObject.transform,
                "Threshold",
                new Vector3(0f, -1.72f, 0.06f),
                new Vector3(2.7f, 0.12f, 0.45f),
                frameMaterial);

            return portalObject.AddComponent<StencilPortal>();
        }

        private static GameObject CloneRoomAsProxy(
            GameObject sourceRoom,
            string cloneName,
            Material wallProxyMaterial,
            Material floorProxyMaterial,
            Material landmarkProxyMaterial,
            string landmarkName)
        {
            GameObject proxy =
                UnityEngine.Object.Instantiate(sourceRoom);

            proxy.name = cloneName;

            Collider[] colliders =
                proxy.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer[] renderers =
                proxy.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.gameObject.name == "Floor")
                {
                    renderer.sharedMaterial = floorProxyMaterial;
                }
                else if (renderer.gameObject.name == landmarkName)
                {
                    renderer.sharedMaterial = landmarkProxyMaterial;
                }
                else
                {
                    renderer.sharedMaterial = wallProxyMaterial;
                }

                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                renderer.receiveShadows = false;
            }

            return proxy;
        }

        private static GameObject CreateTestCube(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            return CreateCube(
                parent,
                objectName,
                localPosition,
                localScale,
                material);
        }

        private static GameObject CreateCube(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject cube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            cube.GetComponent<Renderer>()
                .sharedMaterial = material;

            return cube;
        }

        private static void CreateFramePiece(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            CreateCube(
                parent,
                objectName,
                localPosition,
                localScale,
                material);
        }

        private static void CreateSafetyFloor()
        {
            GameObject safety =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            safety.name = "Invisible Safety Floor";
            safety.transform.position =
                new Vector3(0f, -0.48f, 15f);
            safety.transform.localScale =
                new Vector3(16f, 0.7f, 42f);
            safety.GetComponent<Renderer>().enabled = false;
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode =
                AmbientMode.Trilight;

            RenderSettings.ambientSkyColor =
                new Color(0.24f, 0.29f, 0.39f);

            RenderSettings.ambientEquatorColor =
                new Color(0.11f, 0.12f, 0.16f);

            RenderSettings.ambientGroundColor =
                new Color(0.04f, 0.04f, 0.05f);

            GameObject directionalObject =
                new GameObject("Directional Light");

            Light directional =
                directionalObject.AddComponent<Light>();

            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            directional.shadows = LightShadows.Soft;

            directionalObject.transform.rotation =
                Quaternion.Euler(45f, -35f, 0f);

            CreatePointLight(
                "Exterior Light",
                new Vector3(0f, 4f, 0f),
                new Color(0.45f, 0.70f, 1f));

            CreatePointLight(
                "Interior Light",
                new Vector3(0f, 4f, 30f),
                new Color(1f, 0.58f, 0.25f));
        }

        private static void CreatePointLight(
            string objectName,
            Vector3 position,
            Color color)
        {
            GameObject lightObject =
                new GameObject(objectName);

            lightObject.transform.position = position;

            Light pointLight =
                lightObject.AddComponent<Light>();

            pointLight.type = LightType.Point;
            pointLight.range = 12f;
            pointLight.intensity = 3.2f;
            pointLight.color = color;
            pointLight.shadows = LightShadows.Soft;
        }

        private static Material CreateOrLoadMaterial(
            string path,
            string shaderName)
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Shader shader = Shader.Find(shaderName);

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateProxyMaterial(
            string path,
            Color color)
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "DoctorWhoVR/StencilPortalV6/ProxyLit");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateLitMaterial(
            string path,
            Color color)
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.32f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath);

            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace('\\', '/');
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(
                parent,
                Path.GetFileName(folderPath));
        }
    }
}
#endif
