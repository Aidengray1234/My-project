#if UNITY_EDITOR
using DoctorWhoVR.RealPortalV5;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.RealPortalV5.Editor
{
    /// <summary>
    /// Permanently updates the generated V5 scene in the editor. The runtime
    /// guard also applies the same correction, so entering Play mode is safe
    /// even before the scene asset has been resaved.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalV5SurfaceEditorFix
    {
        private const string ScenePath =
            "Assets/DoctorWhoVR/RealPortalV5/Scenes/" +
            "TARDISRealPortalV5.unity";

        static PortalV5SurfaceEditorFix()
        {
            EditorApplication.delayCall += ApplyAutomatically;
        }

        [MenuItem(
            "Doctor Who VR/Real Portal V5/" +
            "Apply V5.1 Surface Fix")]
        public static void ApplyFromMenu()
        {
            ApplyToOpenScene(true);
        }

        private static void ApplyAutomatically()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.path != ScenePath)
                return;

            ApplyToOpenScene(false);
        }

        private static void ApplyToOpenScene(
            bool showDialog)
        {
            Scene scene =
                SceneManager.GetActiveScene();

            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            PortalSurfaceOrientationGuard.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                "[DoctorWhoVR Real Portal V5.1] " +
                "Portal surfaces now face the correct direction.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "V5.1 Surface Fix Applied",
                    "The portal surfaces were flipped toward their " +
                    "source rooms and the scene was saved.",
                    "OK");
            }
        }
    }
}
#endif
