#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using DoctorWhoVR.StencilPortalV6;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    [InitializeOnLoad]
    public static class PortalFoundationV8Bootstrap
    {
        private const string V6ScenePath =
            "Assets/DoctorWhoVR/StencilPortalV6/Scenes/" +
            "TARDISStencilPortalV6.unity";

        private const string RootFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8";

        private const string SceneFolder =
            RootFolder + "/Scenes";

        private const string MaterialFolder =
            RootFolder + "/Materials";

        private const string V8ScenePath =
            SceneFolder + "/TARDISPortalFoundationV8.unity";

        private const string UltimateXrRoot =
            "Packages/com.vrmada.ultimatexr-unity";

        private const string LeftHandPrefabPath =
            UltimateXrRoot +
            "/Runtime/Prefabs/Internal/Hands/Controllers/" +
            "SmallControllerHandLeft.prefab";

        private const string RightHandPrefabPath =
            UltimateXrRoot +
            "/Runtime/Prefabs/Internal/Hands/Controllers/" +
            "SmallControllerHandRight.prefab";

        private static double _nextPollTime;

        static PortalFoundationV8Bootstrap()
        {
            EditorApplication.update += PollForAutomaticBuild;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Rebuild Clean V8 Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Portal Foundation V8",
                    "This rebuilds V8 from the working V6 stencil scene and " +
                    "removes all V7 interaction objects from the new scene.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildScene(true);
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Open Clean V8 Scene")]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V8ScenePath) == null)
            {
                BuildScene(false);
                return;
            }

            EditorSceneManager.OpenScene(
                V8ScenePath,
                OpenSceneMode.Single);
        }

        private static void PollForAutomaticBuild()
        {
            if (EditorApplication.timeSinceStartup < _nextPollTime)
                return;

            _nextPollTime =
                EditorApplication.timeSinceStartup + 2.0d;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V8ScenePath) != null)
            {
                EditorApplication.update -= PollForAutomaticBuild;
                return;
            }

            if (!HandAssetsReady() ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    V6ScenePath) == null)
            {
                return;
            }

            BuildScene(false);
            EditorApplication.update -= PollForAutomaticBuild;
        }

        private static bool HandAssetsReady()
        {
            return
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    LeftHandPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    RightHandPrefabPath) != null;
        }

        private static void BuildScene(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!HandAssetsReady())
            {
                EditorUtility.DisplayDialog(
                    "UltimateXR Is Still Importing",
                    "Wait for Package Manager and script compilation to finish, " +
                    "then run Rebuild Clean V8 Scene again.",
                    "OK");
                return;
            }

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
                V8ScenePath);

            RemoveV7ObjectsFromScene();
            InstallHandsAndInteractors();
            CreateGrabTestArea();
            CreateNpcReadyTest();
            CreateDiagnostics();

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
                "[Portal Foundation V8] Clean V8 scene created from V6.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Portal Foundation V8 Ready",
                    "V7 was not copied into this scene. V8 now contains " +
                    "separate left/right hands, one direct-grab setup per " +
                    "controller, portal object transfer, and an NPC-ready adapter.",
                    "OK");
            }
        }

        private static void RemoveV7ObjectsFromScene()
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string namespaceName =
                    behaviour.GetType().Namespace;

                if (!string.IsNullOrEmpty(namespaceName) &&
                    namespaceName.StartsWith(
                        "DoctorWhoVR.PortalHandsV7",
                        StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            Transform[] transforms =
                UnityEngine.Object.FindObjectsOfType<Transform>(true);

            List<GameObject> delete =
                new List<GameObject>();

            foreach (Transform transformValue in transforms)
            {
                string lower =
                    transformValue.name.ToLowerInvariant();

                if (lower.Contains("doctor who left hand") ||
                    lower.Contains("doctor who right hand") ||
                    lower.Contains("far-side direct interactor") ||
                    lower.Contains("direct grab interactor") ||
                    lower.Contains("portal hands v7"))
                {
                    delete.Add(transformValue.gameObject);
                }
            }

            foreach (GameObject target in delete.Distinct())
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void InstallHandsAndInteractors()
        {
            GameObject leftPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    LeftHandPrefabPath);

            GameObject rightPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    RightHandPrefabPath);

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

                bool isLeft = lower.Contains("left");
                bool isRight = lower.Contains("right");

                if (!isLeft && !isRight)
                    continue;

                RemoveExistingV8Hand(controller.transform);

                GameObject prefab =
                    isLeft ? leftPrefab : rightPrefab;

                GameObject hand =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        controller.transform)
                    as GameObject;

                if (hand == null)
                    continue;

                PrefabUtility.UnpackPrefabInstance(
                    hand,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                hand.name =
                    isLeft
                        ? "V8 UltimateXR Left Hand"
                        : "V8 UltimateXR Right Hand";

                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;
                hand.transform.localScale = Vector3.one;

                StripUltimateXrRuntimeComponents(hand);
                HideControllerModelRenderers(controller.transform, hand);

                AdaptiveControllerHandPoseV8 pose =
                    hand.AddComponent<
                        AdaptiveControllerHandPoseV8>();

                pose.Configure(controller);

                hand.AddComponent<
                    PortalDynamicVisualProxyV8>();

                XRDirectInteractor direct =
                    CreateCleanDirectInteractor(controller);

                PortalReachBridgeV8 bridge =
                    controller.GetComponent<PortalReachBridgeV8>();

                if (bridge == null)
                {
                    bridge =
                        controller.gameObject.AddComponent<
                            PortalReachBridgeV8>();
                }

                bridge.Configure(
                    controller,
                    direct,
                    controller.transform,
                    traveller != null
                        ? traveller.transform
                        : controller.transform.root);
            }
        }

        private static void StripUltimateXrRuntimeComponents(
            GameObject hand)
        {
            MonoBehaviour[] scripts =
                hand.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour script in scripts)
            {
                if (script != null)
                    UnityEngine.Object.DestroyImmediate(script);
            }

            Animator[] animators =
                hand.GetComponentsInChildren<Animator>(true);

            foreach (Animator animator in animators)
                UnityEngine.Object.DestroyImmediate(animator);

            Collider[] colliders =
                hand.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
                UnityEngine.Object.DestroyImmediate(collider);

            Rigidbody[] bodies =
                hand.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody body in bodies)
                UnityEngine.Object.DestroyImmediate(body);

            Transform[] transforms =
                hand.GetComponentsInChildren<Transform>(true);

            List<GameObject> helpers =
                new List<GameObject>();

            foreach (Transform transformValue in transforms)
            {
                string lower =
                    transformValue.name.ToLowerInvariant();

                if (lower.Contains("dummygrabber") ||
                    lower.Contains("uxrfingertip") ||
                    lower.Contains("laserpointer"))
                {
                    helpers.Add(transformValue.gameObject);
                }
            }

            foreach (GameObject helper in helpers.Distinct())
            {
                if (helper != null && helper != hand)
                    UnityEngine.Object.DestroyImmediate(helper);
            }
        }

        private static void HideControllerModelRenderers(
            Transform controller,
            GameObject hand)
        {
            Renderer[] renderers =
                controller.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform.IsChildOf(hand.transform))
                {
                    renderer.enabled = true;
                    continue;
                }

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

        private static void RemoveExistingV8Hand(
            Transform controller)
        {
            List<GameObject> delete =
                new List<GameObject>();

            for (int index = 0;
                 index < controller.childCount;
                 ++index)
            {
                Transform child = controller.GetChild(index);

                if (child.name.StartsWith(
                        "V8 UltimateXR ",
                        StringComparison.Ordinal))
                {
                    delete.Add(child.gameObject);
                }
            }

            foreach (GameObject target in delete)
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static XRDirectInteractor CreateCleanDirectInteractor(
            ActionBasedController controller)
        {
            XRDirectInteractor[] existing =
                controller.GetComponentsInChildren<
                    XRDirectInteractor>(true);

            foreach (XRDirectInteractor interactor in existing)
            {
                if (interactor.GetComponent<
                        PortalMappedInteractorMarkerV8>() == null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        interactor.gameObject);
                }
            }

            GameObject directObject =
                new GameObject("V8 Direct Grab Interactor");

            directObject.layer = controller.gameObject.layer;

            directObject.transform.SetParent(
                controller.transform,
                false);

            directObject.transform.localPosition =
                new Vector3(0f, -0.012f, 0.055f);

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
            trigger.radius = 0.085f;

            XRDirectInteractor direct =
                directObject.AddComponent<XRDirectInteractor>();

            direct.xrController = controller;

            XRInteractionManager manager =
                UnityEngine.Object.FindObjectOfType<
                    XRInteractionManager>(true);

            if (manager != null)
                direct.interactionManager = manager;

            return direct;
        }

        private static void CreateGrabTestArea()
        {
            GameObject old =
                GameObject.Find("V8 Portal Grab Test Area");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject root =
                new GameObject("V8 Portal Grab Test Area");

            Material red =
                CreateMaterial(
                    MaterialFolder + "/V8Red.mat",
                    new Color(0.80f, 0.05f, 0.04f));

            Material blue =
                CreateMaterial(
                    MaterialFolder + "/V8Blue.mat",
                    new Color(0.04f, 0.22f, 0.82f));

            Material yellow =
                CreateMaterial(
                    MaterialFolder + "/V8Yellow.mat",
                    new Color(0.95f, 0.58f, 0.05f));

            Material green =
                CreateMaterial(
                    MaterialFolder + "/V8Green.mat",
                    new Color(0.04f, 0.72f, 0.12f));

            CreateGrabObject(
                root.transform,
                "V8 Light Cube",
                PrimitiveType.Cube,
                new Vector3(-2.1f, 1.0f, 2.4f),
                new Vector3(0.34f, 0.34f, 0.34f),
                0.75f,
                red);

            CreateGrabObject(
                root.transform,
                "V8 Throw Sphere",
                PrimitiveType.Sphere,
                new Vector3(-1.45f, 1.0f, 2.4f),
                Vector3.one * 0.38f,
                0.55f,
                blue);

            CreateGrabObject(
                root.transform,
                "V8 Long Baton",
                PrimitiveType.Cylinder,
                new Vector3(-1.8f, 1.15f, 3.0f),
                new Vector3(0.09f, 0.66f, 0.09f),
                1.3f,
                yellow,
                Quaternion.Euler(0f, 0f, 90f));

            CreateGrabObject(
                root.transform,
                "V8 Heavy Crate",
                PrimitiveType.Cube,
                new Vector3(1.7f, 1.0f, 27.5f),
                Vector3.one * 0.52f,
                7.5f,
                green);

            CreateGrabObject(
                root.transform,
                "V8 Far Side Sphere",
                PrimitiveType.Sphere,
                new Vector3(2.35f, 1.0f, 27.5f),
                Vector3.one * 0.40f,
                0.65f,
                blue);

            CreateGrabObject(
                root.transform,
                "V8 Portal Tool",
                PrimitiveType.Capsule,
                new Vector3(2.0f, 1.15f, 28.1f),
                new Vector3(0.13f, 0.42f, 0.13f),
                1.0f,
                yellow,
                Quaternion.Euler(0f, 0f, 90f));
        }

        private static GameObject CreateGrabObject(
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

            XRGrabInteractable grab =
                target.AddComponent<XRGrabInteractable>();

            grab.movementType =
                XRGrabInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.throwOnDetach = true;
            grab.attachEaseInTime = 0.055f;

            target.AddComponent<
                PortalRigidbodyTravellerV8>();

            target.AddComponent<
                PortalDynamicVisualProxyV8>();

            return target;
        }

        private static void CreateNpcReadyTest()
        {
            GameObject old =
                GameObject.Find("V8 NPC Ready Test");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject npc =
                GameObject.CreatePrimitive(
                    PrimitiveType.Capsule);

            npc.name = "V8 NPC Ready Test";
            npc.transform.position =
                new Vector3(3.1f, 1.0f, 1.2f);

            npc.GetComponent<Renderer>()
                .sharedMaterial =
                    CreateMaterial(
                        MaterialFolder + "/V8Npc.mat",
                        new Color(0.52f, 0.18f, 0.70f));

            Collider oldCollider = npc.GetComponent<Collider>();

            if (oldCollider != null)
                UnityEngine.Object.DestroyImmediate(oldCollider);

            CharacterController controller =
                npc.AddComponent<CharacterController>();

            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0f, 0f);

            npc.AddComponent<
                PortalCharacterTravellerV8>();

            npc.AddComponent<
                PortalDynamicVisualProxyV8>();
        }

        private static void CreateDiagnostics()
        {
            GameObject old =
                GameObject.Find("Portal Foundation V8 Diagnostics");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject diagnostics =
                new GameObject(
                    "Portal Foundation V8 Diagnostics");

            diagnostics.AddComponent<
                PortalFoundationDiagnosticsV8>();
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
