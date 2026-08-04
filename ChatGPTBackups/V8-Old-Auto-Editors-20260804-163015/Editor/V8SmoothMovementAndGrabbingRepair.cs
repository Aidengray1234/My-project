#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorWhoVR.PortalFoundationV8;
using UltimateXR.Avatar;
using UltimateXR.Devices.Visualization;
using UltimateXR.Locomotion;
using UltimateXR.Manipulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.PortalFoundationV8.Editor
{
    /// <summary>
    /// Repairs the current V8 scene in place.
    ///
    /// Removes teleport/pointer locomotion completely and installs direct
    /// OpenXR smooth movement, smooth turning and grip-driven grabbing.
    /// </summary>
    [InitializeOnLoad]
    public static class V8SmoothMovementAndGrabbingRepair
    {
        private const string V8ScenePath =
            "Assets/DoctorWhoVR/PortalFoundationV8/Scenes/" +
            "TARDISPortalFoundationV8.unity";

        private const string BackupFolder =
            "Assets/DoctorWhoVR/PortalFoundationV8/SceneBackups";

        private const string BackupScenePath =
            BackupFolder +
            "/TARDISPortalFoundationV8_BeforeSmoothGrabRepair.unity";

        static V8SmoothMovementAndGrabbingRepair()
        {
            EditorApplication.delayCall +=
                AutoRepairCurrentV8;
        }

        [MenuItem(
            "Doctor Who VR/Portal Foundation V8/" +
            "Repair Smooth Movement and Grabbing")]
        public static void RepairFromMenu()
        {
            ApplyRepair(true);
        }

        private static void AutoRepairCurrentV8()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.path != V8ScenePath)
                return;

            UxrAvatar avatar =
                UnityEngine.Object.FindObjectOfType<
                    UxrAvatar>(true);

            if (avatar == null)
                return;

            V8DirectOpenXrSmoothLocomotion existing =
                avatar.GetComponent<
                    V8DirectOpenXrSmoothLocomotion>();

            if (existing != null)
                return;

            ApplyRepair(false);
        }

        private static void ApplyRepair(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Exit Play Mode",
                    "Exit Play Mode before repairing V8.",
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

            UxrAvatar avatar =
                UnityEngine.Object.FindObjectOfType<
                    UxrAvatar>(true);

            if (avatar == null)
            {
                EditorUtility.DisplayDialog(
                    "UltimateXR Avatar Missing",
                    "The current V8 scene does not contain the " +
                    "UltimateXR avatar.",
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

            RemoveAllTeleportAndOldLocomotion();
            RemovePointerAndControllerVisuals(avatar);
            InstallDirectSmoothMovementAndGrip(avatar);
            ConfigureGrabbableObjects();
            CreateHealthCheck();
            RemoveMissingScripts();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, V8ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[V8 Smooth/Grab Repair] Applied to the current V8 scene.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "V8 Smooth Movement and Grabbing Repaired",
                    "Teleport locomotion and pointer visuals were removed.\n\n" +
                    "Left stick now performs direct smooth walking, right " +
                    "stick performs direct smooth turning, and controller " +
                    "grip directly drives UltimateXR grabbing.",
                    "OK");
            }
        }

        private static void RemoveAllTeleportAndOldLocomotion()
        {
            UxrLocomotion[] locomotionComponents =
                UnityEngine.Object.FindObjectsOfType<
                    UxrLocomotion>(true);

            foreach (UxrLocomotion locomotion in locomotionComponents)
            {
                if (locomotion != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        locomotion);
                }
            }

            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsOfType<
                    MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName =
                    behaviour.GetType().Name;

                bool isTeleportOrPointer =
                    typeName.IndexOf(
                        "TeleportLocomotion",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "TeleportTarget",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "LaserPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "RayPointer",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                if (isTeleportOrPointer)
                {
                    UnityEngine.Object.DestroyImmediate(
                        behaviour);
                }
            }
        }

        private static void RemovePointerAndControllerVisuals(
            UxrAvatar avatar)
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
                if (trail != null &&
                    trail.transform.IsChildOf(avatar.transform))
                {
                    UnityEngine.Object.DestroyImmediate(trail);
                }
            }

            foreach (
                UxrController3DModel model in
                UnityEngine.Object.FindObjectsOfType<
                    UxrController3DModel>(true))
            {
                if (model != null)
                    UnityEngine.Object.DestroyImmediate(model);
            }

            // The actual hand meshes are SkinnedMeshRenderers.
            // Any ordinary MeshRenderer beneath the avatar is a controller,
            // ray body, debug shape or leftover pointer visual.
            foreach (
                MeshRenderer renderer in
                avatar.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.enabled = false;
            }

            Transform[] transforms =
                avatar.GetComponentsInChildren<Transform>(true);

            string[] visualNameParts =
            {
                "laser",
                "pointer",
                "reticle",
                "teleport",
                "controller model",
                "ray visual",
                "line visual"
            };

            foreach (Transform transformValue in transforms)
            {
                if (transformValue == null ||
                    transformValue == avatar.transform)
                {
                    continue;
                }

                string lower =
                    transformValue.name.ToLowerInvariant();

                if (visualNameParts.Any(
                        value => lower.Contains(value)))
                {
                    transformValue.gameObject.SetActive(false);
                }
            }
        }

        private static void InstallDirectSmoothMovementAndGrip(
            UxrAvatar avatar)
        {
            V8DirectOpenXrSmoothLocomotion smooth =
                avatar.GetComponent<
                    V8DirectOpenXrSmoothLocomotion>();

            if (smooth == null)
            {
                smooth =
                    avatar.gameObject.AddComponent<
                        V8DirectOpenXrSmoothLocomotion>();
            }

            smooth.enabled = true;

            V8DirectOpenXrGripDriver grip =
                avatar.GetComponent<
                    V8DirectOpenXrGripDriver>();

            if (grip == null)
            {
                grip =
                    avatar.gameObject.AddComponent<
                        V8DirectOpenXrGripDriver>();
            }

            grip.enabled = true;
        }

        private static void ConfigureGrabbableObjects()
        {
            UxrGrabbableObject[] grabbables =
                UnityEngine.Object.FindObjectsOfType<
                    UxrGrabbableObject>(true);

            foreach (UxrGrabbableObject grabbable in grabbables)
            {
                if (grabbable == null)
                    continue;

                Rigidbody body =
                    grabbable.GetComponent<Rigidbody>();

                if (body != null)
                {
                    grabbable.RigidBodySource = body;
                    body.isKinematic = false;
                    body.useGravity = true;
                    body.interpolation =
                        RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode =
                        CollisionDetectionMode.ContinuousDynamic;
                }

                if (grabbable.GrabPointCount <= 0)
                    continue;

                UxrGrabPointInfo point =
                    grabbable.GetGrabPoint(0);

                point.BothHandsCompatible = true;
                point.UseDefaultGrabButtons = true;
                point.MaxDistanceGrab =
                    Mathf.Max(
                        point.MaxDistanceGrab,
                        0.30f);

                point.SnapMode =
                    UxrSnapToHandMode.DontSnap;
            }
        }

        private static void CreateHealthCheck()
        {
            GameObject old =
                GameObject.Find(
                    "V8 Smooth Grab Health Check");

            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            GameObject health =
                new GameObject(
                    "V8 Smooth Grab Health Check");

            health.AddComponent<
                V8SmoothGrabHealthCheck>();
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
