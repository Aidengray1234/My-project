#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DoctorWhoVR.RealPortalV5;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;

namespace DoctorWhoVR.RealPortalV5.Editor
{
    /// <summary>
    /// Builds a clean real-portal test scene and switches OpenXR to Multi Pass
    /// for maximum compatibility with custom per-eye portal rendering.
    /// </summary>
    [InitializeOnLoad]
    public static class RealPortalV5Bootstrap
    {
        private const string RootFolder =
            "Assets/DoctorWhoVR/RealPortalV5";

        private const string SceneFolder =
            RootFolder + "/Scenes";

        private const string MaterialFolder =
            RootFolder + "/Materials";

        private const string ScenePath =
            SceneFolder + "/TARDISRealPortalV5.unity";

        private const string PortalMaterialPath =
            MaterialFolder + "/StereoPortalSurface.mat";

        private const int PortalLayer = 30;

        static RealPortalV5Bootstrap()
        {
            EditorApplication.delayCall += BuildAutomaticallyIfNeeded;
        }

        [MenuItem("Doctor Who VR/Real Portal V5/Rebuild Real Portal Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Real Portal V5",
                    "This recreates the real stereo portal test scene. Continue?",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildScene(true);
        }

        [MenuItem("Doctor Who VR/Real Portal V5/Open Real Portal Scene")]
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

            ConfigureOpenXR();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            BuildScene(false);
        }

        private static void ConfigureOpenXR()
        {
            OpenXRSettings settings =
                OpenXRSettings.ActiveBuildTargetInstance;

            if (settings == null)
                return;

            if (settings.renderMode !=
                OpenXRSettings.RenderMode.MultiPass)
            {
                settings.renderMode =
                    OpenXRSettings.RenderMode.MultiPass;

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[DoctorWhoVR Real Portal V5] " +
                    "OpenXR Render Mode changed to Multi Pass " +
                    "for reliable custom stereo portal rendering.");
            }
        }

        private static void BuildScene(bool showCompletionDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ConfigureOpenXR();

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            int portalLayer =
                EnsureLayer("RealPortalSurface", PortalLayer);

            Material portalMaterial =
                CreateOrLoadPortalMaterial();

            if (portalMaterial == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR Real Portal V5] " +
                    "Portal shader is not ready. Unity will retry.");

                EditorApplication.delayCall +=
                    BuildAutomaticallyIfNeeded;

                return;
            }

            Material exteriorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/ExteriorRoom.mat",
                    new Color(0.08f, 0.13f, 0.20f),
                    0.03f,
                    0.30f);

            Material interiorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/InteriorRoom.mat",
                    new Color(0.31f, 0.17f, 0.07f),
                    0.04f,
                    0.30f);

            Material floorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/Floor.mat",
                    new Color(0.11f, 0.12f, 0.14f),
                    0.03f,
                    0.35f);

            Material frameMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/PortalFrame.mat",
                    new Color(0.02f, 0.08f, 0.25f),
                    0.38f,
                    0.58f);

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            SetupLighting();

            GameObject xrSetup = CreateXRSetup();

            if (xrSetup == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR Real Portal V5] " +
                    "XR Interaction Setup prefab was not found.");

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
                    "[DoctorWhoVR Real Portal V5] " +
                    "XR Origin or Main Camera was not found.");

                return;
            }

            xrOrigin.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -3.2f),
                Quaternion.identity);

            xrCamera.tag = "MainCamera";

            VRPortalTraveller traveller =
                xrOrigin.GetComponent<VRPortalTraveller>();

            if (traveller == null)
            {
                traveller =
                    xrOrigin.AddComponent<VRPortalTraveller>();
            }

            traveller.ConfigureHead(xrCamera.transform);

            CreateRoom(
                "Exterior Room",
                Vector3.zero,
                exteriorMaterial,
                floorMaterial,
                true);

            CreateRoom(
                "TARDIS Interior Room",
                new Vector3(0f, 0f, 30f),
                interiorMaterial,
                floorMaterial,
                false);

            StereoPortal exteriorPortal =
                CreatePortal(
                    "Exterior Real Portal",
                    new Vector3(0f, 1.75f, 4.96f),
                    Quaternion.Euler(0f, 180f, 0f),
                    portalMaterial,
                    frameMaterial,
                    portalLayer);

            StereoPortal interiorPortal =
                CreatePortal(
                    "Interior Real Portal",
                    new Vector3(0f, 1.75f, 25.04f),
                    Quaternion.identity,
                    portalMaterial,
                    frameMaterial,
                    portalLayer);

            Renderer exteriorSurface =
                exteriorPortal.transform
                    .Find("Portal Surface")
                    .GetComponent<Renderer>();

            Renderer interiorSurface =
                interiorPortal.transform
                    .Find("Portal Surface")
                    .GetComponent<Renderer>();

            exteriorPortal.Configure(
                interiorPortal,
                exteriorSurface,
                portalLayer);

            interiorPortal.Configure(
                exteriorPortal,
                interiorSurface,
                portalLayer);

            CreateSafetyFloor();
            CreateTestObjects();

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
                "[DoctorWhoVR Real Portal V5] " +
                "Real stereo portal scene created.");

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "Real Portal V5 Ready",
                    "The real stereo portal test scene was rebuilt.\n\n" +
                    "OpenXR is set to Multi Pass. Exit and reopen Unity " +
                    "before the first headset test if the old render mode " +
                    "was already running.",
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
                if (child == null || child == root.transform)
                    continue;

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

        private static void CreateRoom(
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
                floorMaterial,
                0);

            CreateCube(
                room.transform,
                "Ceiling",
                new Vector3(0f, 5.1f, 0f),
                new Vector3(10f, 0.2f, 10f),
                wallMaterial,
                0);

            CreateCube(
                room.transform,
                "Left Wall",
                new Vector3(-5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial,
                0);

            CreateCube(
                room.transform,
                "Right Wall",
                new Vector3(5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial,
                0);

            float solidWallZ =
                doorwayOnPositiveZ ? -5.1f : 5.1f;

            CreateCube(
                room.transform,
                "Solid Wall",
                new Vector3(0f, 2.5f, solidWallZ),
                new Vector3(10f, 5f, 0.2f),
                wallMaterial,
                0);

            float doorwayWallZ =
                doorwayOnPositiveZ ? 5.1f : -5.1f;

            CreateDoorwayWall(
                room.transform,
                doorwayWallZ,
                wallMaterial);
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
                material,
                0);

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
                material,
                0);

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
                material,
                0);
        }

        private static StereoPortal CreatePortal(
            string objectName,
            Vector3 position,
            Quaternion rotation,
            Material portalMaterial,
            Material frameMaterial,
            int portalLayer)
        {
            GameObject portalObject =
                new GameObject(objectName);

            portalObject.layer = portalLayer;

            portalObject.transform.SetPositionAndRotation(
                position,
                rotation);

            BoxCollider trigger =
                portalObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
            trigger.size =
                new Vector3(2.4f, 3.4f, 0.65f);

            GameObject surface =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad);

            surface.name = "Portal Surface";
            surface.layer = portalLayer;

            surface.transform.SetParent(
                portalObject.transform,
                false);

            surface.transform.localPosition =
                new Vector3(0f, 0f, 0.02f);

            surface.transform.localRotation =
                Quaternion.identity;

            surface.transform.localScale =
                new Vector3(2.4f, 3.4f, 1f);

            Collider surfaceCollider =
                surface.GetComponent<Collider>();

            if (surfaceCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    surfaceCollider);
            }

            surface.GetComponent<Renderer>()
                .sharedMaterial = portalMaterial;

            CreateFramePiece(
                portalObject.transform,
                "Frame Left",
                new Vector3(-1.35f, 0f, 0.06f),
                new Vector3(0.25f, 3.8f, 0.25f),
                frameMaterial,
                portalLayer);

            CreateFramePiece(
                portalObject.transform,
                "Frame Right",
                new Vector3(1.35f, 0f, 0.06f),
                new Vector3(0.25f, 3.8f, 0.25f),
                frameMaterial,
                portalLayer);

            CreateFramePiece(
                portalObject.transform,
                "Frame Top",
                new Vector3(0f, 1.825f, 0.06f),
                new Vector3(2.95f, 0.25f, 0.25f),
                frameMaterial,
                portalLayer);

            CreateFramePiece(
                portalObject.transform,
                "Threshold",
                new Vector3(0f, -1.72f, 0.06f),
                new Vector3(2.7f, 0.12f, 0.45f),
                frameMaterial,
                portalLayer);

            return portalObject.AddComponent<StereoPortal>();
        }

        private static void CreateFramePiece(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer)
        {
            GameObject piece =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            piece.name = objectName;
            piece.layer = layer;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = localScale;

            piece.GetComponent<Renderer>()
                .sharedMaterial = material;
        }

        private static GameObject CreateCube(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer)
        {
            GameObject cube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            cube.name = objectName;
            cube.layer = layer;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            cube.GetComponent<Renderer>()
                .sharedMaterial = material;

            return cube;
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

        private static void CreateTestObjects()
        {
            Material red =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/RedTest.mat",
                    new Color(0.75f, 0.05f, 0.04f),
                    0.05f,
                    0.3f);

            Material green =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/GreenTest.mat",
                    new Color(0.04f, 0.65f, 0.12f),
                    0.05f,
                    0.3f);

            GameObject redCube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            redCube.name = "Exterior Red Cube";
            redCube.transform.position =
                new Vector3(-2.1f, 0.75f, 1.5f);
            redCube.transform.localScale =
                new Vector3(1.1f, 1.5f, 1.1f);
            redCube.GetComponent<Renderer>()
                .sharedMaterial = red;

            GameObject greenCube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            greenCube.name = "Interior Green Cube";
            greenCube.transform.position =
                new Vector3(1.8f, 1.0f, 29.5f);
            greenCube.transform.localScale =
                new Vector3(1.2f, 2.0f, 1.2f);
            greenCube.GetComponent<Renderer>()
                .sharedMaterial = green;
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode =
                AmbientMode.Trilight;

            RenderSettings.ambientSkyColor =
                new Color(0.26f, 0.31f, 0.42f);

            RenderSettings.ambientEquatorColor =
                new Color(0.12f, 0.13f, 0.17f);

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

        private static Material CreateOrLoadPortalMaterial()
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(
                    PortalMaterialPath);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "DoctorWhoVR/RealPortalV5/StereoPortalSurface");

            if (shader == null)
                return null;

            Material material =
                new Material(shader);

            material.name = "StereoPortalSurface";

            AssetDatabase.CreateAsset(
                material,
                PortalMaterialPath);

            return material;
        }

        private static Material CreateOrLoadLitMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
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
            material.name =
                Path.GetFileNameWithoutExtension(path);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int EnsureLayer(
            string layerName,
            int preferredIndex)
        {
            for (int index = 8; index <= 31; ++index)
            {
                if (LayerMask.LayerToName(index) == layerName)
                    return index;
            }

            UnityEngine.Object tagManagerAsset =
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset")[0];

            SerializedObject tagManager =
                new SerializedObject(tagManagerAsset);

            SerializedProperty layers =
                tagManager.FindProperty("layers");

            int selectedIndex = -1;

            if (preferredIndex >= 8 &&
                preferredIndex <= 31 &&
                string.IsNullOrEmpty(
                    layers.GetArrayElementAtIndex(
                        preferredIndex).stringValue))
            {
                selectedIndex = preferredIndex;
            }
            else
            {
                for (int index = 8; index <= 31; ++index)
                {
                    if (string.IsNullOrEmpty(
                            layers.GetArrayElementAtIndex(
                                index).stringValue))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            if (selectedIndex < 0)
            {
                throw new InvalidOperationException(
                    "No free Unity layer is available.");
            }

            layers.GetArrayElementAtIndex(selectedIndex)
                .stringValue = layerName;

            tagManager.ApplyModifiedProperties();
            return selectedIndex;
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
