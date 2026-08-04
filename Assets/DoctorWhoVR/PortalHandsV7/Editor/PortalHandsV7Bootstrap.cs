#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalHandsV7;
using DoctorWhoVR.StencilPortalV6;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalHandsV7.Editor
{
    [InitializeOnLoad]
    public static class PortalHandsV7Bootstrap
    {
        private const string V6ScenePath =
            "Assets/DoctorWhoVR/StencilPortalV6/Scenes/" +
            "TARDISStencilPortalV6.unity";

        private const string RootFolder =
            "Assets/DoctorWhoVR/PortalHandsV7";

        private const string SceneFolder =
            RootFolder + "/Scenes";

        private const string MaterialFolder =
            RootFolder + "/Materials";

        private const string HandFolder =
            "Assets/DoctorWhoVR/HandModels/UnityXRHands";

        private const string V7ScenePath =
            SceneFolder + "/TARDISPortalHandsV7.unity";

        static PortalHandsV7Bootstrap()
        {
            EditorApplication.delayCall +=
                BuildAutomaticallyIfNeeded;
        }

        [MenuItem(
            "Doctor Who VR/Portal Hands V7/" +
            "Rebuild Hands and Object Test Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Portal Hands V7",
                    "This recreates the V7 hand, grabbing, and object portal test scene.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildScene(true);
        }

        [MenuItem(
            "Doctor Who VR/Portal Hands V7/" +
            "Open Hands and Object Test Scene")]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V7ScenePath) == null)
            {
                BuildScene(false);
                return;
            }

            EditorSceneManager.OpenScene(
                V7ScenePath,
                OpenSceneMode.Single);
        }

        [MenuItem(
            "Doctor Who VR/Portal Hands V7/" +
            "Reinstall Hand Models")]
        public static void ReinstallHands()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            InstallHandsInOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildAutomaticallyIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V7ScenePath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V6ScenePath) == null)
            {
                Debug.LogWarning(
                    "[Portal Hands V7] V6 scene was not found yet.");
                return;
            }

            BuildScene(false);
        }

        private static void BuildScene(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            Scene scene =
                EditorSceneManager.OpenScene(
                    V6ScenePath,
                    OpenSceneMode.Single);

            EditorSceneManager.SaveScene(
                scene,
                V7ScenePath);

            InstallHandsInOpenScene();
            EnsureGrabObjects();
            EnsureDiagnostics();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, V7ScenePath);

            EditorBuildSettings.scenes =
                new[]
                {
                    new EditorBuildSettingsScene(
                        V7ScenePath,
                        true)
                };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Portal Hands V7] V7 scene created successfully.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Portal Hands V7 Ready",
                    "Hands, direct grabbing, far-side grabbing, " +
                    "held-object portal transfer, and test objects " +
                    "were added.",
                    "OK");
            }
        }

        private static void InstallHandsInOpenScene()
        {
            GameObject leftModel =
                FindHandModelAsset(true);

            GameObject rightModel =
                FindHandModelAsset(false);

            if (leftModel == null ||
                rightModel == null)
            {
                Debug.LogError(
                    "[Portal Hands V7] LeftHand.fbx and RightHand.fbx " +
                    "were not found under " + HandFolder + ".");
                return;
            }

            ActionBasedController[] controllers =
                UnityEngine.Object.FindObjectsOfType<
                    ActionBasedController>(true);

            StencilPortalTraveller traveller =
                UnityEngine.Object.FindObjectOfType<
                    StencilPortalTraveller>(true);

            foreach (ActionBasedController controller in controllers)
            {
                string lower =
                    controller.name.ToLowerInvariant();

                bool isLeft =
                    lower.Contains("left");

                bool isRight =
                    lower.Contains("right");

                if (!isLeft && !isRight)
                    continue;

                RemoveOldHand(controller.transform);
                HideControllerVisuals(controller.transform);

                GameObject modelAsset =
                    isLeft ? leftModel : rightModel;

                GameObject hand =
                    PrefabUtility.InstantiatePrefab(
                        modelAsset,
                        controller.transform)
                    as GameObject;

                if (hand == null)
                    continue;

                hand.name =
                    isLeft
                        ? "Doctor Who Left Hand"
                        : "Doctor Who Right Hand";

                hand.transform.localPosition =
                    new Vector3(0f, -0.015f, 0.025f);

                hand.transform.localRotation =
                    Quaternion.identity;

                hand.transform.localScale =
                    Vector3.one;

                ApplyHandMaterial(hand, isLeft);

                ControllerHandCalibration calibration =
                    hand.GetComponent<ControllerHandCalibration>();

                if (calibration == null)
                {
                    calibration =
                        hand.AddComponent<
                            ControllerHandCalibration>();
                }

                calibration.Configure(
                    new Vector3(0f, -0.015f, 0.025f),
                    Vector3.zero,
                    1f);

                ControllerHandPoseDriver poseDriver =
                    hand.GetComponent<ControllerHandPoseDriver>();

                if (poseDriver == null)
                {
                    poseDriver =
                        hand.AddComponent<
                            ControllerHandPoseDriver>();
                }

                poseDriver.Configure(
                    controller,
                    isLeft);

                XRDirectInteractor directInteractor =
                    EnsureDirectInteractor(
                        controller);

                PortalHandInteractorBridge bridge =
                    controller.GetComponent<
                        PortalHandInteractorBridge>();

                if (bridge == null)
                {
                    bridge =
                        controller.gameObject.AddComponent<
                            PortalHandInteractorBridge>();
                }

                bridge.Configure(
                    controller,
                    directInteractor,
                    controller.transform,
                    traveller != null
                        ? traveller.transform
                        : controller.transform.root);
            }
        }

        private static GameObject FindHandModelAsset(
            bool left)
        {
            string desired =
                left ? "LeftHand" : "RightHand";

            string[] guids =
                AssetDatabase.FindAssets(
                    desired + " t:Model",
                    new[] { HandFolder });

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                GameObject model =
                    AssetDatabase.LoadAssetAtPath<
                        GameObject>(path);

                if (model != null)
                    return model;
            }

            return null;
        }

        private static void RemoveOldHand(
            Transform controller)
        {
            List<GameObject> remove =
                new List<GameObject>();

            for (int index = 0;
                 index < controller.childCount;
                 ++index)
            {
                Transform child =
                    controller.GetChild(index);

                if (child.name.StartsWith(
                        "Doctor Who ",
                        StringComparison.Ordinal))
                {
                    remove.Add(child.gameObject);
                }
            }

            foreach (GameObject oldHand in remove)
            {
                UnityEngine.Object.DestroyImmediate(oldHand);
            }
        }

        private static void HideControllerVisuals(
            Transform controller)
        {
            Renderer[] renderers =
                controller.GetComponentsInChildren<
                    Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform == controller)
                    continue;

                string lower =
                    renderer.name.ToLowerInvariant();

                if (lower.Contains("line") ||
                    lower.Contains("ray") ||
                    lower.Contains("reticle"))
                {
                    continue;
                }

                renderer.enabled = false;
            }
        }

        private static void ApplyHandMaterial(
            GameObject hand,
            bool left)
        {
            string path =
                MaterialFolder +
                (left
                    ? "/LeftHandSkin.mat"
                    : "/RightHandSkin.mat");

            Material material =
                AssetDatabase.LoadAssetAtPath<
                    Material>(path);

            if (material == null)
            {
                Shader shader =
                    Shader.Find(
                        "Universal Render Pipeline/Lit");

                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                material.name =
                    left
                        ? "LeftHandSkin"
                        : "RightHandSkin";

                Color skin =
                    new Color(0.67f, 0.42f, 0.30f);

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", skin);

                material.color = skin;

                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.28f);

                AssetDatabase.CreateAsset(
                    material,
                    path);
            }

            Renderer[] renderers =
                hand.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Material[] materials =
                    renderer.sharedMaterials;

                if (materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }

                for (int index = 0;
                     index < materials.Length;
                     ++index)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.enabled = true;
            }
        }

        private static XRDirectInteractor EnsureDirectInteractor(
            ActionBasedController controller)
        {
            XRDirectInteractor existing =
                controller.GetComponentInChildren<
                    XRDirectInteractor>(true);

            if (existing != null &&
                existing.GetComponent<
                    PortalMappedInteractorMarker>() == null)
            {
                return existing;
            }

            GameObject directObject =
                new GameObject("Direct Grab Interactor");

            directObject.layer =
                controller.gameObject.layer;

            directObject.transform.SetParent(
                controller.transform,
                false);

            directObject.transform.localPosition =
                new Vector3(0f, -0.01f, 0.06f);

            directObject.transform.localRotation =
                Quaternion.identity;

            Rigidbody body =
                directObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            SphereCollider trigger =
                directObject.AddComponent<SphereCollider>();

            trigger.isTrigger = true;
            trigger.radius = 0.09f;

            XRDirectInteractor direct =
                directObject.AddComponent<
                    XRDirectInteractor>();

            direct.xrController = controller;

            XRInteractionManager manager =
                UnityEngine.Object.FindObjectOfType<
                    XRInteractionManager>(true);

            if (manager != null)
                direct.interactionManager = manager;

            return direct;
        }

        private static void EnsureGrabObjects()
        {
            GameObject existing =
                GameObject.Find("Portal Hands V7 Test Objects");

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject root =
                new GameObject(
                    "Portal Hands V7 Test Objects");

            Material red =
                CreateLitMaterial(
                    MaterialFolder + "/GrabRed.mat",
                    new Color(0.82f, 0.06f, 0.04f));

            Material blue =
                CreateLitMaterial(
                    MaterialFolder + "/GrabBlue.mat",
                    new Color(0.04f, 0.20f, 0.85f));

            Material yellow =
                CreateLitMaterial(
                    MaterialFolder + "/GrabYellow.mat",
                    new Color(0.95f, 0.58f, 0.04f));

            Material green =
                CreateLitMaterial(
                    MaterialFolder + "/GrabGreen.mat",
                    new Color(0.05f, 0.75f, 0.14f));

            CreateTable(
                root.transform,
                "Exterior Grab Table",
                new Vector3(-2.0f, 0.42f, 2.55f));

            CreateTable(
                root.transform,
                "Interior Grab Table",
                new Vector3(2.0f, 0.42f, 27.45f));

            CreateGrabPrimitive(
                root.transform,
                "Portal Test Cube",
                PrimitiveType.Cube,
                new Vector3(-2.4f, 1.05f, 2.55f),
                new Vector3(0.38f, 0.38f, 0.38f),
                1.2f,
                red);

            CreateGrabPrimitive(
                root.transform,
                "Portal Test Sphere",
                PrimitiveType.Sphere,
                new Vector3(-1.65f, 1.03f, 2.55f),
                new Vector3(0.42f, 0.42f, 0.42f),
                0.7f,
                blue);

            CreateGrabPrimitive(
                root.transform,
                "Long Two-Hand Baton",
                PrimitiveType.Cylinder,
                new Vector3(-2.0f, 1.18f, 3.05f),
                new Vector3(0.10f, 0.72f, 0.10f),
                1.8f,
                yellow,
                Quaternion.Euler(0f, 0f, 90f));

            CreateGrabPrimitive(
                root.transform,
                "Heavy Portal Crate",
                PrimitiveType.Cube,
                new Vector3(1.65f, 1.08f, 27.45f),
                new Vector3(0.52f, 0.52f, 0.52f),
                8f,
                green);

            CreateGrabPrimitive(
                root.transform,
                "Far-Side Grab Sphere",
                PrimitiveType.Sphere,
                new Vector3(2.35f, 1.04f, 27.45f),
                new Vector3(0.44f, 0.44f, 0.44f),
                0.8f,
                blue);

            CreateGrabTool(
                root.transform,
                new Vector3(2.0f, 1.12f, 28.0f),
                yellow);
        }

        private static void CreateTable(
            Transform parent,
            string objectName,
            Vector3 position)
        {
            Material tableMaterial =
                CreateLitMaterial(
                    MaterialFolder + "/TestTable.mat",
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
                leg.transform.position =
                    position + offset;
                leg.transform.localScale =
                    new Vector3(0.12f, 0.82f, 0.12f);

                leg.GetComponent<Renderer>()
                    .sharedMaterial = tableMaterial;
            }
        }

        private static GameObject CreateGrabPrimitive(
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

            ConfigureGrabObject(target, mass);
            return target;
        }

        private static void CreateGrabTool(
            Transform parent,
            Vector3 position,
            Material material)
        {
            GameObject tool =
                new GameObject("Portal Grab Wrench");

            tool.transform.SetParent(parent, true);
            tool.transform.position = position;

            GameObject handle =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);

            handle.name = "Handle";
            handle.transform.SetParent(
                tool.transform,
                false);

            handle.transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);

            handle.transform.localScale =
                new Vector3(0.08f, 0.42f, 0.08f);

            handle.GetComponent<Renderer>()
                .sharedMaterial = material;

            GameObject head =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            head.name = "Wrench Head";
            head.transform.SetParent(
                tool.transform,
                false);

            head.transform.localPosition =
                new Vector3(0.48f, 0f, 0f);

            head.transform.localScale =
                new Vector3(0.20f, 0.28f, 0.10f);

            head.GetComponent<Renderer>()
                .sharedMaterial = material;

            ConfigureGrabObject(tool, 1.3f);
        }

        private static void ConfigureGrabObject(
            GameObject target,
            float mass)
        {
            Rigidbody body =
                target.GetComponent<Rigidbody>();

            if (body == null)
                body = target.AddComponent<Rigidbody>();

            body.mass = mass;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            XRGrabInteractable grab =
                target.GetComponent<XRGrabInteractable>();

            if (grab == null)
                grab = target.AddComponent<XRGrabInteractable>();

            grab.movementType =
                XRGrabInteractable.MovementType.VelocityTracking;

            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.throwOnDetach = true;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.attachEaseInTime = 0.06f;
            grab.useDynamicAttach = true;

            if (target.GetComponent<
                    PortalPhysicsTraveller>() == null)
            {
                target.AddComponent<
                    PortalPhysicsTraveller>();
            }

            if (target.GetComponent<
                    PortalDynamicVisualProxy>() == null)
            {
                target.AddComponent<
                    PortalDynamicVisualProxy>();
            }
        }

        private static void EnsureDiagnostics()
        {
            GameObject old =
                GameObject.Find(
                    "Portal Hands V7 Diagnostics");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject diagnostics =
                new GameObject(
                    "Portal Hands V7 Diagnostics");

            diagnostics.AddComponent<
                PortalInteractionDiagnostics>();
        }

        private static Material CreateLitMaterial(
            string path,
            Color color)
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<
                    Material>(path);

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
