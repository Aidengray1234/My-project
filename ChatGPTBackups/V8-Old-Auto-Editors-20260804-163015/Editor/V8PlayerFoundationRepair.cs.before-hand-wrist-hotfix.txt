#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using UltimateXR.Avatar;
using UltimateXR.Devices;
using UltimateXR.Devices.Visualization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    /// <summary>
    /// Repairs the current V8 scene in place:
    /// 1. Replaces the hands-only avatar with UltimateXR's full-body Cyborg.
    /// 2. Removes every invalid built-in controller input integration.
    /// 3. Keeps only the generic OpenXR input/tracking bridge.
    /// 4. Removes controller model renderers and pointer visuals.
    /// 5. Creates stable whole-player body anchors.
    /// </summary>
    [InitializeOnLoad]
    public static class V8PlayerFoundationRepair
    {
        private const string V8ScenePath =
            "Assets/DoctorWhoVR/PortalFoundationV8/Scenes/" +
            "TARDISPortalFoundationV8.unity";

        private const string BackupFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8/SceneBackups";

        private const string BackupScenePath =
            BackupFolder +
            "/TARDISPortalFoundationV8_BeforePlayerFoundation.unity";

        private const string FullBodyAvatarPath =
            "Packages/com.vrmada.ultimatexr-unity/" +
            "Runtime/Prefabs/Avatars/CyborgAvatar_URP.prefab";

        static V8PlayerFoundationRepair()
        {
            EditorApplication.delayCall += AutoRepair;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Repair Errors and Build Full Player")]
        public static void RepairFromMenu()
        {
            ApplyRepair(true);
        }

        private static void AutoRepair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != V8ScenePath)
                return;

            UxrAvatar avatar =
                UnityEngine.Object.FindObjectOfType<UxrAvatar>(true);

            if (avatar == null)
                return;

            if (avatar.GetComponent<V8PlayerBodyFoundation>() != null)
                return;

            ApplyRepair(false);
        }

        private static void ApplyRepair(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Exit Play Mode",
                    "Exit Play Mode before repairing the player.",
                    "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != V8ScenePath)
            {
                if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(
                    V8ScenePath,
                    OpenSceneMode.Single);
            }

            GameObject fullBodyPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FullBodyAvatarPath);

            if (fullBodyPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Full-Body Avatar Missing",
                    "UltimateXR's CyborgAvatar_URP prefab was not found. " +
                    "Wait for Package Manager to finish importing UltimateXR.",
                    "OK");
                return;
            }

            EnsureFolder(BackupFolder);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    BackupScenePath) == null)
            {
                EditorSceneManager.SaveScene(
                    scene,
                    BackupScenePath,
                    true);
            }

            UxrAvatar oldAvatar =
                UnityEngine.Object.FindObjectOfType<UxrAvatar>(true);

            Vector3 oldPosition =
                oldAvatar != null
                    ? oldAvatar.transform.position
                    : new Vector3(0f, 0f, -3.2f);

            Quaternion oldRotation =
                oldAvatar != null
                    ? oldAvatar.transform.rotation
                    : Quaternion.identity;

            RemoveOldPlayerObjects(oldAvatar);

            GameObject playerObject =
                PrefabUtility.InstantiatePrefab(
                    fullBodyPrefab)
                as GameObject;

            if (playerObject == null)
            {
                Debug.LogError(
                    "[V8 Player Foundation] Could not instantiate " +
                    "CyborgAvatar_URP.");
                return;
            }

            PrefabUtility.UnpackPrefabInstance(
                playerObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            playerObject.name =
                "V8 Full Body Player";

            playerObject.transform.SetPositionAndRotation(
                oldPosition,
                oldRotation);

            UxrAvatar avatar =
                playerObject.GetComponent<UxrAvatar>();

            if (avatar == null)
            {
                avatar =
                    playerObject.GetComponentInChildren<UxrAvatar>(true);
            }

            if (avatar == null)
            {
                Debug.LogError(
                    "[V8 Player Foundation] Full-body prefab has no UxrAvatar.");
                UnityEngine.Object.DestroyImmediate(playerObject);
                return;
            }

            Camera cameraValue = avatar.CameraComponent;

            if (cameraValue != null)
                cameraValue.tag = "MainCamera";

            InstallGenericInputAndTracking(avatar);
            RemoveInvalidControllerIntegrations(avatar);
            RemoveControllerVisualsAndPointers(avatar);
            AddExistingV8RuntimeSystems(avatar);
            ConfigureBodyFoundation(avatar);

            RemoveMissingScripts();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, V8ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[V8 Player Foundation] Full-body player installed. " +
                "Invalid UltimateXR controller asset references were removed.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "V8 Full Player Foundation Ready",
                    "The current V8 scene now uses one full-body avatar.\n\n" +
                    "All built-in controller integrations that referenced " +
                    "prefab assets were removed. Only the generic OpenXR " +
                    "input/tracking bridge remains.",
                    "OK");
            }
        }

        private static void RemoveOldPlayerObjects(UxrAvatar oldAvatar)
        {
            HashSet<GameObject> delete =
                new HashSet<GameObject>();

            if (oldAvatar != null)
                delete.Add(oldAvatar.transform.root.gameObject);

            string[] exactNames =
            {
                "UltimateXR Player (V8 Full System)",
                "V8 Full Body Player",
                "XR Interaction Setup",
                "XR Origin (XR Rig)"
            };

            foreach (string objectName in exactNames)
            {
                GameObject found = GameObject.Find(objectName);

                if (found != null)
                    delete.Add(found.transform.root.gameObject);
            }

            foreach (GameObject target in delete)
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void InstallGenericInputAndTracking(UxrAvatar avatar)
        {
            UxrControllerTracking[] trackingComponents =
                avatar.GetComponentsInChildren<UxrControllerTracking>(true);

            Transform leftSensor = null;
            Transform rightSensor = null;
            GameObject host = avatar.gameObject;

            foreach (UxrControllerTracking tracking in trackingComponents)
            {
                if (tracking == null)
                    continue;

                SerializedObject serialized =
                    new SerializedObject(tracking);

                SerializedProperty leftProperty =
                    serialized.FindProperty("_leftHandSensor");

                SerializedProperty rightProperty =
                    serialized.FindProperty("_rightHandSensor");

                Transform candidateLeft =
                    leftProperty != null
                        ? leftProperty.objectReferenceValue as Transform
                        : null;

                Transform candidateRight =
                    rightProperty != null
                        ? rightProperty.objectReferenceValue as Transform
                        : null;

                if (candidateLeft != null &&
                    candidateRight != null)
                {
                    leftSensor = candidateLeft;
                    rightSensor = candidateRight;
                    host = tracking.gameObject;
                    break;
                }
            }

            UxrAnyOpenXrControllerInputV8 input =
                host.GetComponent<UxrAnyOpenXrControllerInputV8>();

            if (input == null)
            {
                input =
                    host.AddComponent<UxrAnyOpenXrControllerInputV8>();
            }

            UxrAnyOpenXrControllerTrackingV8 genericTracking =
                host.GetComponent<UxrAnyOpenXrControllerTrackingV8>();

            if (genericTracking == null)
            {
                genericTracking =
                    host.AddComponent<UxrAnyOpenXrControllerTrackingV8>();
            }

            SerializedObject serializedGeneric =
                new SerializedObject(genericTracking);

            SerializedProperty genericLeft =
                serializedGeneric.FindProperty("_leftHandSensor");

            SerializedProperty genericRight =
                serializedGeneric.FindProperty("_rightHandSensor");

            SerializedProperty updateLeft =
                serializedGeneric.FindProperty("_updateAvatarLeftHand");

            SerializedProperty updateRight =
                serializedGeneric.FindProperty("_updateAvatarRightHand");

            if (genericLeft != null)
                genericLeft.objectReferenceValue = leftSensor;

            if (genericRight != null)
                genericRight.objectReferenceValue = rightSensor;

            if (updateLeft != null)
                updateLeft.boolValue = true;

            if (updateRight != null)
                updateRight.boolValue = true;

            serializedGeneric.ApplyModifiedPropertiesWithoutUndo();

            input.enabled = true;
            genericTracking.enabled = true;
        }

        private static void RemoveInvalidControllerIntegrations(
            UxrAvatar avatar)
        {
            UxrControllerInput[] inputComponents =
                avatar.GetComponentsInChildren<UxrControllerInput>(true);

            foreach (UxrControllerInput input in inputComponents)
            {
                if (input == null ||
                    input is UxrAnyOpenXrControllerInputV8)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(input);
            }

            UxrControllerTracking[] trackingComponents =
                avatar.GetComponentsInChildren<UxrControllerTracking>(true);

            foreach (UxrControllerTracking tracking in trackingComponents)
            {
                if (tracking == null ||
                    tracking is UxrAnyOpenXrControllerTrackingV8)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(tracking);
            }
        }

        private static void RemoveControllerVisualsAndPointers(
            UxrAvatar avatar)
        {
            foreach (
                UxrController3DModel model in
                avatar.GetComponentsInChildren<UxrController3DModel>(true))
            {
                if (model != null)
                    UnityEngine.Object.DestroyImmediate(model);
            }

            foreach (
                LineRenderer line in
                avatar.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line != null)
                    UnityEngine.Object.DestroyImmediate(line);
            }

            foreach (
                TrailRenderer trail in
                avatar.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trail != null)
                    UnityEngine.Object.DestroyImmediate(trail);
            }

            MonoBehaviour[] behaviours =
                avatar.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string name =
                    behaviour.GetType().Name;

                if (name.IndexOf(
                        "LaserPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "Teleport",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "CameraWallFade",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void AddExistingV8RuntimeSystems(UxrAvatar avatar)
        {
            if (avatar.GetComponent<V8DirectOpenXrSmoothLocomotion>() == null)
            {
                avatar.gameObject.AddComponent<
                    V8DirectOpenXrSmoothLocomotion>();
            }

            if (avatar.GetComponent<
                    UltimateXrAvatarPortalTravellerV8>() == null)
            {
                avatar.gameObject.AddComponent<
                    UltimateXrAvatarPortalTravellerV8>();
            }

            // The old snap-grab driver is intentionally not re-added.
            V8DirectOpenXrGripDriver oldGrip =
                avatar.GetComponent<V8DirectOpenXrGripDriver>();

            if (oldGrip != null)
                UnityEngine.Object.DestroyImmediate(oldGrip);
        }

        private static void ConfigureBodyFoundation(UxrAvatar avatar)
        {
            Animator animator =
                avatar.GetComponentInChildren<Animator>(true);

            Transform head =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;

            Transform chest =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.Chest)
                    : null;

            if (chest == null && animator != null)
            {
                chest =
                    animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }

            Transform pelvis =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.Hips)
                    : null;

            Transform leftHand =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
                    : avatar.LeftHand;

            Transform rightHand =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                    : avatar.RightHand;

            Transform leftFoot =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.LeftFoot)
                    : null;

            Transform rightFoot =
                animator != null
                    ? animator.GetBoneTransform(HumanBodyBones.RightFoot)
                    : null;

            V8PlayerBodyFoundation foundation =
                avatar.GetComponent<V8PlayerBodyFoundation>();

            if (foundation == null)
            {
                foundation =
                    avatar.gameObject.AddComponent<
                        V8PlayerBodyFoundation>();
            }

            foundation.Configure(
                avatar,
                head,
                chest,
                pelvis,
                leftHand,
                rightHand,
                leftFoot,
                rightFoot);

            if (avatar.GetComponent<
                    V8FirstPersonBodyVisibility>() == null)
            {
                avatar.gameObject.AddComponent<
                    V8FirstPersonBodyVisibility>();
            }
        }

        private static void RemoveMissingScripts()
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

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath);

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
