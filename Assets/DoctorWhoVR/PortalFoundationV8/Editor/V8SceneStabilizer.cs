#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using UltimateXR.Avatar;
using UltimateXR.Devices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    /// <summary>
    /// Stabilizes the existing V8 scene only.
    ///
    /// It never saves package prefabs and never rebuilds the project from an
    /// older scene. All work is restricted to TARDISPortalFoundationV8.unity.
    /// </summary>
    [InitializeOnLoad]
    public static class V8SceneStabilizer
    {
        private const string V8ScenePath =
            "Assets/DoctorWhoVR/PortalFoundationV8/Scenes/" +
            "TARDISPortalFoundationV8.unity";

        private static bool _isRunning;

        static V8SceneStabilizer()
        {
            EditorApplication.delayCall += AutoRunWhenV8IsOpen;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Stabilize Current V8 Scene")]
        public static void StabilizeFromMenu()
        {
            Stabilize(true);
        }

        private static void AutoRunWhenV8IsOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();

            // Critical guard: never edit or save a package prefab stage.
            if (!activeScene.IsValid() ||
                activeScene.path != V8ScenePath)
            {
                return;
            }

            Stabilize(false);
        }

        private static void Stabilize(bool showDialog)
        {
            if (_isRunning ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _isRunning = true;

            try
            {
                Scene scene = SceneManager.GetActiveScene();

                if (!scene.IsValid() ||
                    scene.path != V8ScenePath)
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

                // Refuse to continue if Unity did not open the actual scene.
                if (!scene.IsValid() ||
                    scene.path != V8ScenePath ||
                    scene.path.StartsWith(
                        "Packages/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(
                        "[V8 Stabilizer] Refusing to save because the " +
                        "active stage is not the V8 scene.");
                    return;
                }

                RemoveStaleDiagnostics();
                RemoveOldPointerVisuals();
                RemoveOldSnapGrabDriver();
                EnsureCurrentAvatarRuntime();
                RemoveMissingScripts(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[V8 Stabilizer] Current V8 scene stabilized. " +
                    "Old auto-repair diagnostics and pointer visuals " +
                    "were removed.");

                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "V8 Scene Stabilized",
                        "The current V8 scene was cleaned without " +
                        "touching any package prefab.\n\n" +
                        "Old diagnostics, stale health checks, pointer " +
                        "lines and the snap-grab driver were removed.",
                        "OK");
                }
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static void RemoveStaleDiagnostics()
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsOfType<
                    MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                string namespaceName = type.Namespace ?? string.Empty;
                string typeName = type.Name;

                bool doctorWhoDiagnostic =
                    namespaceName.StartsWith(
                        "DoctorWhoVR",
                        StringComparison.Ordinal) &&
                    (typeName.IndexOf(
                         "Diagnostic",
                         StringComparison.OrdinalIgnoreCase) >= 0 ||
                     typeName.IndexOf(
                         "HealthCheck",
                         StringComparison.OrdinalIgnoreCase) >= 0);

                if (doctorWhoDiagnostic)
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            Transform[] transforms =
                UnityEngine.Object.FindObjectsOfType<Transform>(true);

            HashSet<GameObject> deleteObjects =
                new HashSet<GameObject>();

            foreach (Transform transformValue in transforms)
            {
                if (transformValue == null)
                    continue;

                string lower =
                    transformValue.name.ToLowerInvariant();

                if (lower.Contains("diagnostic") ||
                    lower.Contains("health check"))
                {
                    deleteObjects.Add(transformValue.gameObject);
                }
            }

            foreach (GameObject target in deleteObjects)
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void RemoveOldPointerVisuals()
        {
            foreach (
                LineRenderer line in
                UnityEngine.Object.FindObjectsOfType<
                    LineRenderer>(true))
            {
                if (line != null)
                    UnityEngine.Object.DestroyImmediate(line);
            }

            foreach (
                TrailRenderer trail in
                UnityEngine.Object.FindObjectsOfType<
                    TrailRenderer>(true))
            {
                if (trail != null)
                    UnityEngine.Object.DestroyImmediate(trail);
            }

            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsOfType<
                    MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;

                bool isPointerOrTeleportVisual =
                    typeName.IndexOf(
                        "LaserPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "RayPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "TeleportTarget",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "TeleportLocomotion",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "LineVisual",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                if (isPointerOrTeleportVisual)
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            Transform[] transforms =
                UnityEngine.Object.FindObjectsOfType<Transform>(true);

            string[] pointerNameParts =
            {
                "teleport ray",
                "teleport target",
                "ray interactor",
                "laser pointer",
                "line visual",
                "reticle visual",
                "pointer visual"
            };

            HashSet<GameObject> deleteObjects =
                new HashSet<GameObject>();

            foreach (Transform transformValue in transforms)
            {
                if (transformValue == null)
                    continue;

                string lower =
                    transformValue.name.ToLowerInvariant();

                if (pointerNameParts.Any(
                        namePart => lower.Contains(namePart)))
                {
                    deleteObjects.Add(transformValue.gameObject);
                }
            }

            foreach (GameObject target in deleteObjects)
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void RemoveOldSnapGrabDriver()
        {
            V8DirectOpenXrGripDriver[] drivers =
                UnityEngine.Object.FindObjectsOfType<
                    V8DirectOpenXrGripDriver>(true);

            foreach (V8DirectOpenXrGripDriver driver in drivers)
            {
                if (driver != null)
                    UnityEngine.Object.DestroyImmediate(driver);
            }
        }

        private static void EnsureCurrentAvatarRuntime()
        {
            UxrAvatar[] avatars =
                UnityEngine.Object.FindObjectsOfType<
                    UxrAvatar>(true);

            if (avatars.Length == 0)
            {
                Debug.LogWarning(
                    "[V8 Stabilizer] No UltimateXR avatar was found. " +
                    "The scene was cleaned, but player tracking cannot " +
                    "be verified until an avatar exists.");
                return;
            }

            // Prefer the full-body V8 player if duplicates survived.
            UxrAvatar avatar =
                avatars.FirstOrDefault(
                    value =>
                        value != null &&
                        value.transform.root.name.IndexOf(
                            "Full Body",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                ?? avatars[0];

            foreach (UxrAvatar duplicate in avatars)
            {
                if (duplicate != null &&
                    duplicate != avatar)
                {
                    UnityEngine.Object.DestroyImmediate(
                        duplicate.transform.root.gameObject);
                }
            }

            if (avatar.CameraComponent != null)
                avatar.CameraComponent.tag = "MainCamera";

            if (avatar.GetComponent<
                    V8DirectOpenXrSmoothLocomotion>() == null)
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

            UxrAnyOpenXrControllerInputV8 input =
                avatar.GetComponentInChildren<
                    UxrAnyOpenXrControllerInputV8>(true);

            UxrAnyOpenXrControllerTrackingV8 tracking =
                avatar.GetComponentInChildren<
                    UxrAnyOpenXrControllerTrackingV8>(true);

            GameObject host =
                tracking != null
                    ? tracking.gameObject
                    : input != null
                        ? input.gameObject
                        : avatar.gameObject;

            if (input == null)
            {
                input =
                    host.AddComponent<
                        UxrAnyOpenXrControllerInputV8>();
            }

            if (tracking == null)
            {
                tracking =
                    host.AddComponent<
                        UxrAnyOpenXrControllerTrackingV8>();

                SerializedObject serializedTracking =
                    new SerializedObject(tracking);

                SerializedProperty leftSensor =
                    serializedTracking.FindProperty(
                        "_leftHandSensor");

                SerializedProperty rightSensor =
                    serializedTracking.FindProperty(
                        "_rightHandSensor");

                SerializedProperty updateLeft =
                    serializedTracking.FindProperty(
                        "_updateAvatarLeftHand");

                SerializedProperty updateRight =
                    serializedTracking.FindProperty(
                        "_updateAvatarRightHand");

                if (leftSensor != null)
                {
                    leftSensor.objectReferenceValue =
                        avatar.LeftHand != null
                            ? avatar.LeftHand.Wrist
                            : null;
                }

                if (rightSensor != null)
                {
                    rightSensor.objectReferenceValue =
                        avatar.RightHand != null
                            ? avatar.RightHand.Wrist
                            : null;
                }

                if (updateLeft != null)
                    updateLeft.boolValue = true;

                if (updateRight != null)
                    updateRight.boolValue = true;

                serializedTracking
                    .ApplyModifiedPropertiesWithoutUndo();
            }

            input.enabled = true;
            tracking.enabled = true;
        }

        private static void RemoveMissingScripts(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                Transform[] transforms =
                    rootObject.GetComponentsInChildren<
                        Transform>(true);

                foreach (Transform transformValue in transforms)
                {
                    GameObjectUtility
                        .RemoveMonoBehavioursWithMissingScript(
                            transformValue.gameObject);
                }
            }
        }
    }
}
#endif
