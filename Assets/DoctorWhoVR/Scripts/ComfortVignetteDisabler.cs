using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR
{
    /// <summary>
    /// Permanently disables the XR Starter Assets tunneling/comfort vignette.
    /// The project uses full-screen locomotion without the dark border effect.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ComfortVignetteDisabler : MonoBehaviour
    {
        private static ComfortVignetteDisabler s_Instance;
        private int _framesToRescan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (s_Instance != null)
                return;

            var host = new GameObject("Comfort Vignette Disabler");
            DontDestroyOnLoad(host);
            s_Instance = host.AddComponent<ComfortVignetteDisabler>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            DisableAllVignettes();
            _framesToRescan = 4;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void LateUpdate()
        {
            // Starter Asset prefabs can finish enabling one or two frames after a scene load.
            if (_framesToRescan <= 0)
                return;

            --_framesToRescan;
            DisableAllVignettes();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DisableAllVignettes();
            _framesToRescan = 4;
        }

        public static void DisableAllVignettes()
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var candidate in transforms)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                    continue;

                string objectName = candidate.name;
                if (objectName.Equals("TunnelingVignette", StringComparison.OrdinalIgnoreCase) ||
                    objectName.Equals("Tunneling Vignette", StringComparison.OrdinalIgnoreCase) ||
                    objectName.Equals("Locomotion Vignette", StringComparison.OrdinalIgnoreCase))
                {
                    candidate.gameObject.SetActive(false);
                }
            }

            var behaviours = Resources.FindObjectsOfTypeAll<Behaviour>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                    continue;

                string typeName = behaviour.GetType().Name;
                if (typeName.IndexOf("TunnelingVignette", StringComparison.OrdinalIgnoreCase) >= 0)
                    behaviour.enabled = false;
            }
        }
    }
}
