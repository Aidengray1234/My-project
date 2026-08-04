#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using DoctorWhoVR.StencilPortalV6;
using UltimateXR.Avatar;
using UltimateXR.Core;
using UltimateXR.Locomotion;
using UltimateXR.Manipulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    /// <summary>
    /// Converts the existing V8 scene in place. It does not rebuild from V6
    /// and does not create another versioned scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalFoundationV8FullUltimateXRUpgrade
    {
        private const string V8ScenePath =
            "Assets/DoctorWhoVR/PortalFoundationV8/Scenes/" +
            "TARDISPortalFoundationV8.unity";

        private const string BackupFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8/SceneBackups";

        private const string BackupScenePath =
            BackupFolder +
            "/TARDISPortalFoundationV8_BeforeFullUltimateXR.unity";

        private const string AvatarPrefabPath =
            "Packages/com.vrmada.ultimatexr-unity/" +
            "Runtime/Prefabs/Avatars/SmallHandsAvatar_URP.prefab";

        private const string MaterialFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8/Materials";

        static PortalFoundationV8FullUltimateXRUpgrade()
        {
            EditorApplication.delayCall +=
                AutoConvertOpenV8Scene;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Convert Current V8 to Full UltimateXR")]
        public static void ConvertFromMenu()
        {
            ConvertCurrentV8(true);
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Open Current V8 Scene")]
        public static void OpenV8Scene()
        {
            EditorSceneManager.OpenScene(
                V8ScenePath,
                OpenSceneMode.Single);
        }

        private static void AutoConvertOpenV8Scene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.path != V8ScenePath)
                return;

            if (UnityEngine.Object.FindObjectOfType<UxrAvatar>(true) != null)
                return;

            ConvertCurrentV8(false);
        }

        private static void ConvertCurrentV8(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Exit Play Mode",
                    "Exit Play Mode before converting V8.",
                    "OK");
                return;
            }

            GameObject avatarPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AvatarPrefabPath);

            if (avatarPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "UltimateXR Is Not Ready",
                    "Wait for Package Manager to finish importing " +
                    "com.vrmada.ultimatexr-unity v0.9.7.",
                    "OK");
                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.path != V8ScenePath)
            {
                if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene =
                    EditorSceneManager.OpenScene(
                        V8ScenePath,
                        OpenSceneMode.Single);
            }

            EnsureFolder(
                "Assets/DoctorWhoVR/PortalFoundationV8");

            EnsureFolder(BackupFolder);
            EnsureFolder(MaterialFolder);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    BackupScenePath) == null)
            {
                EditorSceneManager.SaveScene(
                    scene,
                    BackupScenePath,
                    true);
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            CaptureOldPlayerSpawn(
                out spawnPosition,
                out spawnRotation);

            RemoveOldInteractionSystems();
            RemoveMissingScriptsFromScene();

            GameObject avatarObject =
                PrefabUtility.InstantiatePrefab(
                    avatarPrefab)
                as GameObject;

            if (avatarObject == null)
            {
                Debug.LogError(
                    "[Portal Foundation V8] " +
                    "Could not instantiate SmallHandsAvatar_URP.");
                return;
            }

            avatarObject.name =
                "UltimateXR Player (V8 Full System)";

            avatarObject.transform.SetPositionAndRotation(
                spawnPosition,
                spawnRotation);

            UxrAvatar avatar =
                avatarObject.GetComponent<UxrAvatar>();

            if (avatar == null)
            {
                avatar =
                    avatarObject.GetComponentInChildren<UxrAvatar>(true);
            }

            if (avatar == null)
            {
                Debug.LogError(
                    "[Portal Foundation V8] " +
                    "The UltimateXR avatar prefab has no UxrAvatar component.");
                UnityEngine.Object.DestroyImmediate(avatarObject);
                return;
            }

            Camera avatarCamera =
                avatar.CameraComponent;

            if (avatarCamera != null)
                avatarCamera.tag = "MainCamera";

            DisableDarkeningComponents(avatarObject);

            UxrSmoothLocomotion locomotion =
                avatarObject.GetComponent<UxrSmoothLocomotion>();

            if (locomotion == null)
            {
                locomotion =
                    avatarObject.AddComponent<UxrSmoothLocomotion>();
            }

            locomotion.MetersPerSecondNormal = 2.5f;
            locomotion.MetersPerSecondSprint = 4.0f;
            locomotion.RotationDegreesPerSecondNormal = 120f;
            locomotion.RotationDegreesPerSecondSprint = 120f;
            locomotion.Gravity = -9.81f;

            UltimateXrAvatarPortalTravellerV8 portalTraveller =
                avatarObject.GetComponent<
                    UltimateXrAvatarPortalTravellerV8>();

            if (portalTraveller == null)
            {
                avatarObject.AddComponent<
                    UltimateXrAvatarPortalTravellerV8>();
            }

            foreach (
                UxrGrabber grabber in
                avatarObject.GetComponentsInChildren<UxrGrabber>(true))
            {
                if (grabber.GetComponent<
                        UltimateXrPortalVisualProxyV8>() == null)
                {
                    grabber.gameObject.AddComponent<
                        UltimateXrPortalVisualProxyV8>();
                }
            }

            EnsureUltimateXrManager();
            CreateUltimateXrGrabTestArea();
            CreateNpcReadyTest();
            CreateDiagnostics();

            RemoveMissingScriptsFromScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, V8ScenePath);

            EditorBuildSettings.scenes =
                new[]
                {
                    new EditorBuildSettingsScene(
                        V8ScenePath,
                        true)
                };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Portal Foundation V8] Current V8 scene converted " +
                "to the full UltimateXR avatar and manipulation system.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "V8 Full UltimateXR Conversion Complete",
                    "The existing V8 scene was converted in place.\n\n" +
                    "The old XR Interaction Setup, controllers, rays, " +
                    "custom hand scripts, direct interactors, and XRI " +
                    "grabbables were removed.\n\n" +
                    "A scene backup was created before conversion.",
                    "OK");
            }
        }

        private static void CaptureOldPlayerSpawn(
            out Vector3 position,
            out Quaternion rotation)
        {
            position = new Vector3(0f, 0f, -3.2f);
            rotation = Quaternion.identity;

            StencilPortalTraveller oldTraveller =
                UnityEngine.Object.FindObjectOfType<
                    StencilPortalTraveller>(true);

            if (oldTraveller != null)
            {
                position = oldTraveller.transform.position;
                rotation = oldTraveller.transform.rotation;
                return;
            }

            Camera oldCamera =
                UnityEngine.Object.FindObjectsOfType<Camera>(true)
                    .FirstOrDefault(
                        cameraValue =>
                            cameraValue.CompareTag("MainCamera"));

            if (oldCamera == null)
                return;

            position =
                new Vector3(
                    oldCamera.transform.position.x,
                    0f,
                    oldCamera.transform.position.z);

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    oldCamera.transform.forward,
                    Vector3.up);

            if (forward.sqrMagnitude > 0.001f)
            {
                rotation =
                    Quaternion.LookRotation(
                        forward.normalized,
                        Vector3.up);
            }
        }

        private static void RemoveOldInteractionSystems()
        {
            HashSet<GameObject> deleteRoots =
                new HashSet<GameObject>();

            foreach (
                ActionBasedController controller in
                UnityEngine.Object.FindObjectsOfType<
                    ActionBasedController>(true))
            {
                deleteRoots.Add(
                    controller.transform.root.gameObject);
            }

            foreach (
                XRInteractionManager manager in
                UnityEngine.Object.FindObjectsOfType<
                    XRInteractionManager>(true))
            {
                deleteRoots.Add(manager.transform.root.gameObject);
            }

            foreach (
                StencilPortalTraveller traveller in
                UnityEngine.Object.FindObjectsOfType<
                    StencilPortalTraveller>(true))
            {
                deleteRoots.Add(
                    traveller.transform.root.gameObject);
            }

            string[] exactRootNames =
            {
                "XR Interaction Setup",
                "V8 Portal Grab Test Area",
                "Portal Foundation V8 Diagnostics",
                "V8 NPC Ready Test",
                "UltimateXR Player (V8 Full System)",
                "UltimateXR Manager (V8)"
            };

            foreach (string rootName in exactRootNames)
            {
                GameObject rootObject =
                    GameObject.Find(rootName);

                if (rootObject != null)
                    deleteRoots.Add(rootObject);
            }

            foreach (GameObject target in deleteRoots)
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }

            foreach (
                XRGrabInteractable oldGrab in
                UnityEngine.Object.FindObjectsOfType<
                    XRGrabInteractable>(true))
            {
                if (oldGrab != null)
                    UnityEngine.Object.DestroyImmediate(oldGrab);
            }

            foreach (
                XRBaseInteractor oldInteractor in
                UnityEngine.Object.FindObjectsOfType<
                    XRBaseInteractor>(true))
            {
                if (oldInteractor != null)
                    UnityEngine.Object.DestroyImmediate(oldInteractor);
            }

            foreach (
                XRBaseController oldController in
                UnityEngine.Object.FindObjectsOfType<
                    XRBaseController>(true))
            {
                if (oldController != null)
                    UnityEngine.Object.DestroyImmediate(oldController);
            }

            foreach (
                AudioListener listener in
                UnityEngine.Object.FindObjectsOfType<AudioListener>(true))
            {
                UnityEngine.Object.DestroyImmediate(listener);
            }
        }

        private static void DisableDarkeningComponents(
            GameObject avatarObject)
        {
            MonoBehaviour[] behaviours =
                avatarObject.GetComponentsInChildren<
                    MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName =
                    behaviour.GetType().Name;

                if (typeName.IndexOf(
                        "CameraWallFade",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Vignette",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Tunneling",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void EnsureUltimateXrManager()
        {
            UxrManager manager =
                UnityEngine.Object.FindObjectOfType<UxrManager>(true);

            if (manager == null)
            {
                GameObject managerObject =
                    new GameObject("UltimateXR Manager (V8)");

                manager =
                    managerObject.AddComponent<UxrManager>();
            }

            manager.UsePrecaching = false;
        }

        private static void CreateUltimateXrGrabTestArea()
        {
            GameObject oldArea =
                GameObject.Find("V8 UltimateXR Grab Test Area");

            if (oldArea != null)
                UnityEngine.Object.DestroyImmediate(oldArea);

            GameObject root =
                new GameObject("V8 UltimateXR Grab Test Area");

            Material red =
                CreateMaterial(
                    MaterialFolder + "/UltimateXRRed.mat",
                    new Color(0.80f, 0.05f, 0.04f));

            Material blue =
                CreateMaterial(
                    MaterialFolder + "/UltimateXRBlue.mat",
                    new Color(0.04f, 0.22f, 0.82f));

            Material yellow =
                CreateMaterial(
                    MaterialFolder + "/UltimateXRYellow.mat",
                    new Color(0.95f, 0.58f, 0.05f));

            Material green =
                CreateMaterial(
                    MaterialFolder + "/UltimateXRGreen.mat",
                    new Color(0.04f, 0.72f, 0.12f));

            CreateTable(
                root.transform,
                "Exterior UltimateXR Test Table",
                new Vector3(-2.0f, 0.42f, 2.55f));

            CreateTable(
                root.transform,
                "Interior UltimateXR Test Table",
                new Vector3(2.0f, 0.42f, 27.45f));

            CreateGrabbable(
                root.transform,
                "UltimateXR Light Cube",
                PrimitiveType.Cube,
                new Vector3(-2.35f, 1.05f, 2.55f),
                Vector3.one * 0.36f,
                0.75f,
                red);

            CreateGrabbable(
                root.transform,
                "UltimateXR Throw Sphere",
                PrimitiveType.Sphere,
                new Vector3(-1.65f, 1.04f, 2.55f),
                Vector3.one * 0.40f,
                0.55f,
                blue);

            CreateGrabbable(
                root.transform,
                "UltimateXR Long Baton",
                PrimitiveType.Cylinder,
                new Vector3(-2.0f, 1.18f, 3.05f),
                new Vector3(0.10f, 0.72f, 0.10f),
                1.35f,
                yellow,
                Quaternion.Euler(0f, 0f, 90f));

            CreateGrabbable(
                root.transform,
                "UltimateXR Heavy Crate",
                PrimitiveType.Cube,
                new Vector3(1.65f, 1.08f, 27.45f),
                Vector3.one * 0.52f,
                7.5f,
                green);

            CreateGrabbable(
                root.transform,
                "UltimateXR Far Side Sphere",
                PrimitiveType.Sphere,
                new Vector3(2.35f, 1.04f, 27.45f),
                Vector3.one * 0.42f,
                0.65f,
                blue);

            CreateGrabbable(
                root.transform,
                "UltimateXR Portal Tool",
                PrimitiveType.Capsule,
                new Vector3(2.0f, 1.13f, 28.0f),
                new Vector3(0.13f, 0.42f, 0.13f),
                1.0f,
                yellow,
                Quaternion.Euler(0f, 0f, 90f));
        }

        private static void CreateTable(
            Transform parent,
            string objectName,
            Vector3 position)
        {
            Material tableMaterial =
                CreateMaterial(
                    MaterialFolder + "/UltimateXRTable.mat",
                    new Color(0.18f, 0.11f, 0.07f));

            GameObject top =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            top.name = objectName;
            top.transform.SetParent(parent, true);
            top.transform.position = position;
            top.transform.localScale =
                new Vector3(1.65f, 0.14f, 1.05f);

            top.GetComponent<Renderer>()
                .sharedMaterial = tableMaterial;

            Vector3[] legOffsets =
            {
                new Vector3(-0.68f, -0.47f, -0.38f),
                new Vector3(0.68f, -0.47f, -0.38f),
                new Vector3(-0.68f, -0.47f, 0.38f),
                new Vector3(0.68f, -0.47f, 0.38f)
            };

            foreach (Vector3 offset in legOffsets)
            {
                GameObject leg =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                leg.name = objectName + " Leg";
                leg.transform.SetParent(parent, true);
                leg.transform.position = position + offset;
                leg.transform.localScale =
                    new Vector3(0.12f, 0.82f, 0.12f);

                leg.GetComponent<Renderer>()
                    .sharedMaterial = tableMaterial;
            }
        }

        private static void CreateGrabbable(
            Transform parent,
            string objectName,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            float mass,
            Material material,
            Quaternion? rotation = null)
        {
            GameObject target =
                GameObject.CreatePrimitive(primitive);

            target.name = objectName;
            target.transform.SetParent(parent, true);
            target.transform.position = position;
            target.transform.rotation =
                rotation ?? Quaternion.identity;
            target.transform.localScale = scale;

            target.GetComponent<Renderer>()
                .sharedMaterial = material;

            Rigidbody body =
                target.AddComponent<Rigidbody>();

            body.mass = mass;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            target.AddComponent<UxrGrabbableObject>();

            target.AddComponent<
                UltimateXrRigidbodyTravellerV8>();

            target.AddComponent<
                UltimateXrPortalVisualProxyV8>();
        }

        private static void CreateNpcReadyTest()
        {
            GameObject oldNpc =
                GameObject.Find("V8 UltimateXR NPC Ready Test");

            if (oldNpc != null)
                UnityEngine.Object.DestroyImmediate(oldNpc);

            GameObject npc =
                GameObject.CreatePrimitive(
                    PrimitiveType.Capsule);

            npc.name =
                "V8 UltimateXR NPC Ready Test";

            npc.transform.position =
                new Vector3(3.1f, 1.0f, 1.2f);

            npc.GetComponent<Renderer>()
                .sharedMaterial =
                    CreateMaterial(
                        MaterialFolder + "/UltimateXRNpc.mat",
                        new Color(0.52f, 0.18f, 0.70f));

            Collider primitiveCollider =
                npc.GetComponent<Collider>();

            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    primitiveCollider);
            }

            CharacterController controller =
                npc.AddComponent<CharacterController>();

            controller.height = 2f;
            controller.radius = 0.35f;

            npc.AddComponent<PortalCharacterTravellerV8>();
            npc.AddComponent<UltimateXrPortalVisualProxyV8>();
        }

        private static void CreateDiagnostics()
        {
            GameObject oldDiagnostics =
                GameObject.Find(
                    "V8 Full UltimateXR Diagnostics");

            if (oldDiagnostics != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    oldDiagnostics);
            }

            GameObject diagnostics =
                new GameObject(
                    "V8 Full UltimateXR Diagnostics");

            diagnostics.AddComponent<
                UltimateXrPortalDiagnosticsV8>();
        }

        private static void RemoveMissingScriptsFromScene()
        {
            foreach (
                GameObject rootObject in
                SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (
                    Transform transformValue in
                    rootObject.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility
                        .RemoveMonoBehavioursWithMissingScript(
                            transformValue.gameObject);
                }
            }
        }

        private static Material CreateMaterial(
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

            Material material =
                new Material(shader);

            material.name =
                Path.GetFileNameWithoutExtension(path);

            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.30f);

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
