using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.RealPortalV5
{
    /// <summary>
    /// V5.1 compatibility guard.
    ///
    /// Unity's built-in Quad faces local -Z. The V5 builder originally left
    /// the portal surface at identity rotation even though StereoPortal.forward
    /// points toward the room/viewer. This flips every existing portal surface
    /// so its front face points along the portal's +Z direction.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    public static class PortalSurfaceOrientationGuard
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            ApplyToScene(scene);
        }

        public static void ApplyToScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0;
                 rootIndex < roots.Length;
                 ++rootIndex)
            {
                StereoPortal[] portals =
                    roots[rootIndex]
                        .GetComponentsInChildren<StereoPortal>(true);

                for (int portalIndex = 0;
                     portalIndex < portals.Length;
                     ++portalIndex)
                {
                    StereoPortal portal = portals[portalIndex];

                    if (portal == null)
                        continue;

                    Transform surface =
                        portal.transform.Find("Portal Surface");

                    if (surface == null)
                        continue;

                    surface.localRotation =
                        Quaternion.Euler(0f, 180f, 0f);

                    // Keep the visible surface slightly toward the source room
                    // so it wins the depth test against the doorway threshold.
                    surface.localPosition =
                        new Vector3(0f, 0f, 0.025f);
                }
            }
        }
    }
}
