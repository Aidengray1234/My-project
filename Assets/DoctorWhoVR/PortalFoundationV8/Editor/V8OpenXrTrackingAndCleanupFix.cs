#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using UltimateXR.Avatar;
using UltimateXR.CameraUtils;
using UltimateXR.Devices;
using UltimateXR.Locomotion;
using UltimateXR.UI.UnityInputModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    /// <summary>
    /// Repairs the current V8 scene in place:
    /// - keeps the full UltimateXR avatar,
    /// - adds generic OpenXR input/tracking,
    /// - copies UltimateXR's calibrated hand sensors,
    /// - removes every surviving XRI controller/ray/line visual,
    /// - enables smooth movement and smooth rotation.
    /// </summary>
    [InitializeOnLoad]
    public static class V8OpenXrTrackingAndCleanupFix
    {
        private const string V8ScenePath =
            "Assets/DoctorWhoVR/PortalFoundationV8/Scenes/" +
            "TARDISPortalFoundationV8.unity";

        private const string BackupFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8/SceneBackups";

        private const string BackupScenePath =
            BackupFolder +
            "/TARDISPortalFoundationV8_BeforeOpenXRTrackingFix.unity";

        static V8OpenXrTrackingAndCleanupFix()
        {
            EditorApplication.delayCall +=
                AutoFixCurrentV8;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Fix Tracking Movement and Old Pointers")]
        public static void FixFromMenu()
        {
            ApplyFix(true);
        }

        private static void AutoFixCurrentV8()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != V8ScenePath)
                return;

            UxrAvatar avatar =
                UnityEngine.Object.FindObjectOfType<
                    UxrAvatar>(true);

            if (avatar == null)
                return;

            UxrAnyOpenXrControllerInputV8 existing =
                avatar.GetComponentInChildren<
                    UxrAnyOpenXrControllerInputV8>(true);

            if (existing != null)
                return;

            ApplyFix(false);
        }

        private static void ApplyFix(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Exit Play Mode",
                    "Exit Play Mode before applying the V8 fix.",
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

                scene =
                    EditorSceneManager.OpenScene(
                        V8ScenePath,
                        OpenSceneMode.Single);
            }

            UxrAvatar avatar =
                UnityEngine.Object.FindObjectOfType<
                    UxrAvatar>(true);

            if (avatar == null)
            {
                EditorUtility.DisplayDialog(
                    "UltimateXR Avatar Missing",
                    "Run the current V8 Full UltimateXR conversion first.",
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

            RemoveOldXriAndPointerObjects(avatar);
            InstallGenericOpenXrBridge(avatar);
            ConfigureAvatarAndLocomotion(avatar);
            DisableUltimateXrPointersAndFades(avatar);
            CreateHealthCheck();

            RemoveMissingScripts();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, V8ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[V8 OpenXR Fix] Applied generic controller " +
                "tracking/input and removed old pointer visuals.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "V8 Tracking Fix Applied",
                    "The existing V8 scene was fixed in place.\n\n" +
                    "Generic OpenXR input/tracking was added, calibrated " +
                    "UltimateXR sensors were copied, smooth locomotion was " +
                    "enabled, and old XRI controllers/rays/lines were removed.",
                    "OK");
            }
        }

        private static void InstallGenericOpenXrBridge(
            UxrAvatar avatar)
        {
            UxrControllerTracking[] existingTracking =
                avatar.GetComponentsInChildren<
                    UxrControllerTracking>(true);

            UxrControllerInput[] existingInput =
                avatar.GetComponentsInChildren<
                    UxrControllerInput>(true);

            Transform leftSensor = null;
            Transform rightSensor = null;
            bool updateLeft = true;
            bool updateRight = true;

            GameObject host = null;

            foreach (
                UxrControllerTracking tracking in
                existingTracking)
            {
                if (tracking == null)
                    continue;

                SerializedObject serializedTracking =
                    new SerializedObject(tracking);

                SerializedProperty leftProperty =
                    serializedTracking.FindProperty(
                        "_leftHandSensor");

                SerializedProperty rightProperty =
                    serializedTracking.FindProperty(
                        "_rightHandSensor");

                Transform possibleLeft =
                    leftProperty != null
                        ? leftProperty.objectReferenceValue
                            as Transform
                        : null;

                Transform possibleRight =
                    rightProperty != null
                        ? rightProperty.objectReferenceValue
                            as Transform
                        : null;

                if (possibleLeft != null &&
                    possibleRight != null)
                {
                    leftSensor = possibleLeft;
                    rightSensor = possibleRight;
                    host = tracking.gameObject;

                    SerializedProperty updateLeftProperty =
                        serializedTracking.FindProperty(
                            "_updateAvatarLeftHand");

                    SerializedProperty updateRightProperty =
                        serializedTracking.FindProperty(
                            "_updateAvatarRightHand");

                    if (updateLeftProperty != null)
                        updateLeft =
                            updateLeftProperty.boolValue;

                    if (updateRightProperty != null)
                        updateRight =
                            updateRightProperty.boolValue;

                    break;
                }
            }

            if (host == null)
            {
                UxrControllerInput firstInput =
                    existingInput.FirstOrDefault(
                        value => value != null);

                host =
                    firstInput != null
                        ? firstInput.gameObject
                        : avatar.gameObject;
            }

            foreach (
                UxrControllerInput input in
                existingInput)
            {
                if (input != null &&
                    !(input is UxrAnyOpenXrControllerInputV8))
                {
                    input.enabled = false;
                }
            }

            foreach (
                UxrControllerTracking tracking in
                existingTracking)
            {
                if (tracking != null &&
                    !(tracking is
                        UxrAnyOpenXrControllerTrackingV8))
                {
                    tracking.enabled = false;
                }
            }

            UxrAnyOpenXrControllerInputV8 genericInput =
                host.GetComponent<
                    UxrAnyOpenXrControllerInputV8>();

            if (genericInput == null)
            {
                genericInput =
                    host.AddComponent<
                        UxrAnyOpenXrControllerInputV8>();
            }

            genericInput.enabled = true;

            UxrAnyOpenXrControllerTrackingV8 genericTracking =
                host.GetComponent<
                    UxrAnyOpenXrControllerTrackingV8>();

            if (genericTracking == null)
            {
                genericTracking =
                    host.AddComponent<
                        UxrAnyOpenXrControllerTrackingV8>();
            }

            SerializedObject serializedGenericTracking =
                new SerializedObject(genericTracking);

            SerializedProperty genericLeft =
                serializedGenericTracking.FindProperty(
                    "_leftHandSensor");

            SerializedProperty genericRight =
                serializedGenericTracking.FindProperty(
                    "_rightHandSensor");

            SerializedProperty genericUpdateLeft =
                serializedGenericTracking.FindProperty(
                    "_updateAvatarLeftHand");

            SerializedProperty genericUpdateRight =
                serializedGenericTracking.FindProperty(
                    "_updateAvatarRightHand");

            SerializedProperty smoothPosition =
                serializedGenericTracking.FindProperty(
                    "_smoothPosition");

            SerializedProperty smoothRotation =
                serializedGenericTracking.FindProperty(
                    "_smoothRotation");

            if (genericLeft != null)
                genericLeft.objectReferenceValue = leftSensor;

            if (genericRight != null)
                genericRight.objectReferenceValue = rightSensor;

            if (genericUpdateLeft != null)
                genericUpdateLeft.boolValue = updateLeft;

            if (genericUpdateRight != null)
                genericUpdateRight.boolValue = updateRight;

            if (smoothPosition != null)
                smoothPosition.floatValue = 0.08f;

            if (smoothRotation != null)
                smoothRotation.floatValue = 0.06f;

            serializedGenericTracking.ApplyModifiedPropertiesWithoutUndo();

            if (leftSensor == null ||
                rightSensor == null)
            {
                Debug.LogError(
                    "[V8 OpenXR Fix] Could not copy UltimateXR's " +
                    "left/right calibrated controller sensor transforms. " +
                    "The avatar prefab may be incomplete.",
                    avatar);
            }
        }

        private static void ConfigureAvatarAndLocomotion(
            UxrAvatar avatar)
        {
            avatar.RenderMode =
                UxrAvatarRenderModes.Avatar;

            avatar.ShowControllerHands = false;

            UxrSmoothLocomotion locomotion =
                avatar.GetComponentInChildren<
                    UxrSmoothLocomotion>(true);

            if (locomotion == null)
            {
                locomotion =
                    avatar.gameObject.AddComponent<
                        UxrSmoothLocomotion>();
            }

            locomotion.enabled = true;
            locomotion.MetersPerSecondNormal = 2.5f;
            locomotion.MetersPerSecondSprint = 4.0f;
            locomotion.RotationDegreesPerSecondNormal = 120f;
            locomotion.RotationDegreesPerSecondSprint = 120f;
            locomotion.Gravity = -9.81f;
        }

        private static void RemoveOldXriAndPointerObjects(
            UxrAvatar avatar)
        {
            HashSet<GameObject> deleteObjects =
                new HashSet<GameObject>();

            foreach (
                MonoBehaviour behaviour in
                UnityEngine.Object.FindObjectsOfType<
                    MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                string namespaceName =
                    behaviour.GetType().Namespace;

                if (!string.IsNullOrEmpty(namespaceName) &&
                    namespaceName.StartsWith(
                        "UnityEngine.XR.Interaction.Toolkit",
                        StringComparison.Ordinal))
                {
                    GameObject objectValue =
                        behaviour.gameObject;

                    if (!objectValue.transform.IsChildOf(
                            avatar.transform))
                    {
                        deleteObjects.Add(objectValue);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(
                            behaviour);
                    }
                }
            }

            foreach (
                LineRenderer line in
                UnityEngine.Object.FindObjectsOfType<
                    LineRenderer>(true))
            {
                if (line != null)
                    deleteObjects.Add(line.gameObject);
            }

            Transform[] allTransforms =
                UnityEngine.Object.FindObjectsOfType<
                    Transform>(true);

            string[] unwantedNameParts =
            {
                "xr interaction setup",
                "xr origin",
                "left controller",
                "right controller",
                "lefthand controller",
                "righthand controller",
                "controller model",
                "ray interactor",
                "line visual",
                "interactor line",
                "teleport ray",
                "reticle visual"
            };

            foreach (Transform transformValue in allTransforms)
            {
                if (transformValue == null ||
                    transformValue.IsChildOf(avatar.transform))
                {
                    continue;
                }

                string lower =
                    transformValue.name.ToLowerInvariant();

                if (unwantedNameParts.Any(
                        part => lower.Contains(part)))
                {
                    deleteObjects.Add(
                        transformValue.gameObject);
                }
            }

            foreach (GameObject target in deleteObjects)
            {
                if (target != null &&
                    !target.transform.IsChildOf(avatar.transform))
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void DisableUltimateXrPointersAndFades(
            UxrAvatar avatar)
        {
            MonoBehaviour[] behaviours =
                avatar.GetComponentsInChildren<
                    MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName =
                    behaviour.GetType().Name;

                bool disable =
                    typeName.IndexOf(
                        "LaserPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "CameraWallFade",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Vignette",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Tunneling",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                if (disable)
                    behaviour.enabled = false;
            }

            foreach (
                LineRenderer line in
                avatar.GetComponentsInChildren<
                    LineRenderer>(true))
            {
                UnityEngine.Object.DestroyImmediate(line);
            }
        }

        private static void CreateHealthCheck()
        {
            GameObject old =
                GameObject.Find("V8 OpenXR Health Check");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject health =
                new GameObject("V8 OpenXR Health Check");

            health.AddComponent<V8OpenXrHealthCheck>();
        }

        private static void RemoveMissingScripts()
        {
            foreach (
                GameObject rootObject in
                SceneManager.GetActiveScene()
                    .GetRootGameObjects())
            {
                foreach (
                    Transform transformValue in
                    rootObject.GetComponentsInChildren<
                        Transform>(true))
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
