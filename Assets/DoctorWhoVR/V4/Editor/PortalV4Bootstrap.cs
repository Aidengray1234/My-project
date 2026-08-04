#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DoctorWhoVR.V4;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.V4.Editor
{
    /// <summary>
    /// One-click builder for the stable Portal V4 doorway prototype.
    /// The older live-render portal scenes remain untouched but are not used.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalV4Bootstrap
    {
        private const string RootFolder =
            "Assets/DoctorWhoVR/V4";

        private const string SceneFolder =
            RootFolder + "/Scenes";

        private const string MaterialFolder =
            RootFolder + "/Materials";

        private const string ScenePath =
            SceneFolder + "/TARDISDoorwayV4.unity";

        private const string FieldMaterialPath =
            MaterialFolder + "/DoorwayField.mat";

        static PortalV4Bootstrap()
        {
            EditorApplication.delayCall += BuildAutomaticallyIfNeeded;
        }

        [MenuItem("Doctor Who VR/Portal V4/Rebuild Stable Doorway Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Portal V4",
                    "This recreates the stable doorway test scene. Continue?",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildScene(true);
        }

        [MenuItem("Doctor Who VR/Portal V4/Open Stable Doorway Scene")]
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

            Material fieldMaterial = CreateOrLoadFieldMaterial();

            if (fieldMaterial == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR V4] DoorwayField shader is not ready. " +
                    "Unity will retry after the next import.");

                EditorApplication.delayCall +=
                    BuildAutomaticallyIfNeeded;

                return;
            }

            Material exteriorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/ExteriorRoom.mat",
                    new Color(0.08f, 0.13f, 0.20f),
                    0.05f,
                    0.35f);

            Material interiorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/InteriorRoom.mat",
                    new Color(0.28f, 0.16f, 0.08f),
                    0.05f,
                    0.28f);

            Material floorMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/Floor.mat",
                    new Color(0.12f, 0.13f, 0.15f),
                    0.05f,
                    0.38f);

            Material frameMaterial =
                CreateOrLoadLitMaterial(
                    MaterialFolder + "/DoorwayFrame.mat",
                    new Color(0.03f, 0.10f, 0.28f),
                    0.35f,
                    0.55f);

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            SetupLighting();

            GameObject xrSetup = CreateXRSetup();

            if (xrSetup == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR V4] Could not locate the " +
                    "'XR Interaction Setup' Starter Assets prefab.");

                return;
            }

            // Unpacking makes this scene independent of the sample prefab and
            // allows the comfort vignette objects to be removed safely.
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
                    "[DoctorWhoVR V4] XR Origin or camera was not found.");

                return;
            }

            xrOrigin.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -3.1f),
                Quaternion.identity);

            xrCamera.tag = "MainCamera";

            XRDoorwayTraveller traveller =
                xrOrigin.GetComponent<XRDoorwayTraveller>();

            if (traveller == null)
            {
                traveller =
                    xrOrigin.AddComponent<XRDoorwayTraveller>();
            }

            traveller.ConfigureHead(xrCamera.transform);

            CreateRoom(
                "Exterior Test Room",
                Vector3.zero,
                exteriorMaterial,
                floorMaterial,
                true);

            CreateRoom(
                "TARDIS Interior Test Room",
                new Vector3(0f, 0f, 30f),
                interiorMaterial,
                floorMaterial,
                false);

            DoorwayPortal exteriorPortal =
                CreateDoorwayPortal(
                    "Exterior TARDIS Doorway",
                    new Vector3(0f, 1.75f, 4.96f),
                    Quaternion.Euler(0f, 180f, 0f),
                    fieldMaterial,
                    frameMaterial);

            DoorwayPortal interiorPortal =
                CreateDoorwayPortal(
                    "Interior TARDIS Doorway",
                    new Vector3(0f, 1.75f, 25.04f),
                    Quaternion.identity,
                    fieldMaterial,
                    frameMaterial);

            GameObject exteriorField =
                exteriorPortal.transform
                    .Find("Doorway Field")
                    .gameObject;

            GameObject interiorField =
                interiorPortal.transform
                    .Find("Doorway Field")
                    .gameObject;

            exteriorPortal.Configure(
                interiorPortal,
                exteriorField);

            interiorPortal.Configure(
                exteriorPortal,
                interiorField);

            CreateSafetyFloor();
            CreateInstructions();

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
                "[DoctorWhoVR V4] Stable doorway scene created. " +
                "Live portal rendering is disabled for this milestone.");

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "Portal V4 Ready",
                    "The stable teleport doorway scene was rebuilt.",
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
                    instance.name =
                        "XR Interaction Setup";

                    return instance;
                }
            }

            return null;
        }

        private static void RemoveComfortVignettes(
            GameObject root)
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

                string childName = child.name;

                if (childName.Equals(
                        "TunnelingVignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    childName.Equals(
                        "Tunneling Vignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    childName.Equals(
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
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in transforms)
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
            bool portalOnPositiveZ)
        {
            GameObject room =
                new GameObject(roomName);

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
                portalOnPositiveZ ? -5.1f : 5.1f;

            CreateCube(
                room.transform,
                "Solid Wall",
                new Vector3(0f, 2.5f, solidWallZ),
                new Vector3(10f, 5f, 0.2f),
                wallMaterial);

            float doorwayWallZ =
                portalOnPositiveZ ? 5.1f : -5.1f;

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

        private static DoorwayPortal CreateDoorwayPortal(
            string portalName,
            Vector3 position,
            Quaternion rotation,
            Material fieldMaterial,
            Material frameMaterial)
        {
            GameObject portalObject =
                new GameObject(portalName);

            portalObject.transform.SetPositionAndRotation(
                position,
                rotation);

            BoxCollider trigger =
                portalObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
            trigger.size =
                new Vector3(2.4f, 3.4f, 0.65f);

            GameObject field =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad);

            field.name = "Doorway Field";
            field.transform.SetParent(
                portalObject.transform,
                false);

            field.transform.localPosition =
                new Vector3(0f, 0f, 0.035f);

            field.transform.localRotation =
                Quaternion.identity;

            field.transform.localScale =
                new Vector3(2.4f, 3.4f, 1f);

            Collider fieldCollider =
                field.GetComponent<Collider>();

            if (fieldCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    fieldCollider);
            }

            field.GetComponent<Renderer>()
                .sharedMaterial = fieldMaterial;

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

            return portalObject.AddComponent<DoorwayPortal>();
        }

        private static void CreateFramePiece(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject piece =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            piece.name = objectName;
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

        private static void CreateSafetyFloor()
        {
            GameObject safetyFloor =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            safetyFloor.name =
                "Invisible Safety Floor";

            safetyFloor.transform.position =
                new Vector3(0f, -0.48f, 15f);

            safetyFloor.transform.localScale =
                new Vector3(16f, 0.7f, 42f);

            safetyFloor.GetComponent<Renderer>()
                .enabled = false;
        }

        private static void CreateInstructions()
        {
            GameObject instructionsObject =
                new GameObject("Portal V4 Instructions");

            TextMesh instructions =
                instructionsObject.AddComponent<TextMesh>();

            instructions.text =
                "PORTAL V4\n" +
                "Walk through the blue field.\n" +
                "This milestone tests stable VR crossing.";

            instructions.fontSize = 48;
            instructions.characterSize = 0.06f;
            instructions.anchor = TextAnchor.MiddleCenter;
            instructions.alignment = TextAlignment.Center;
            instructions.color = Color.white;

            instructionsObject.transform.position =
                new Vector3(0f, 2.2f, -4.65f);

            instructionsObject.transform.rotation =
                Quaternion.identity;
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode =
                AmbientMode.Trilight;

            RenderSettings.ambientSkyColor =
                new Color(0.25f, 0.30f, 0.40f);

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
                "Exterior Room Light",
                new Vector3(0f, 4f, 0f),
                new Color(0.40f, 0.65f, 1f));

            CreatePointLight(
                "Interior Room Light",
                new Vector3(0f, 4f, 30f),
                new Color(1f, 0.55f, 0.22f));
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

        private static Material CreateOrLoadFieldMaterial()
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(
                    FieldMaterialPath);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "DoctorWhoVR/V4/DoorwayField");

            if (shader == null)
                return null;

            Material material =
                new Material(shader);

            material.name = "DoorwayField";

            AssetDatabase.CreateAsset(
                material,
                FieldMaterialPath);

            return material;
        }

        private static Material CreateOrLoadLitMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            Material material =
                new Material(shader);

            material.name =
                Path.GetFileNameWithoutExtension(path);

            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(
                material,
                path);

            return material;
        }

        private static void EnsureFolder(
            string folderPath)
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

            string folderName =
                Path.GetFileName(folderPath);

            AssetDatabase.CreateFolder(
                parent,
                folderName);
        }
    }
}
#endif
