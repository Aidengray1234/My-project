#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DoctorWhoVR.Portals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DoctorWhoVR.Editor
{
    /// <summary>
    /// Creates a clean Doctor Who VR portal prototype automatically.
    /// It keeps the XRI input/rig assets, removes the sample demo scene/content,
    /// removes the locomotion vignette object, and makes the portal scene the
    /// only enabled build scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalProjectBootstrap
    {
        private const string RootFolder = "Assets/DoctorWhoVR";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ScenePath = SceneFolder + "/TARDISPortalPrototype.unity";
        private const string PortalMaterialPath = MaterialFolder + "/PortalSurface.mat";
        private const int PreferredPortalLayer = 30;

        static PortalProjectBootstrap()
        {
            EditorApplication.delayCall += AutoBuildOnce;
        }

        [MenuItem("Doctor Who VR/Rebuild Clean Portal Prototype")]
        public static void RebuildFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Portal Prototype",
                "This rebuilds the generated portal prototype scene. Continue?",
                "Rebuild",
                "Cancel");

            if (confirmed)
                BuildProject(true);
        }

        private static void AutoBuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            BuildProject(false);
        }

        private static void BuildProject(bool showCompletionDialog)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            int portalLayer = EnsureLayer("PortalSurface", PreferredPortalLayer);
            Material portalMaterial = CreateOrLoadPortalMaterial();
            if (portalMaterial == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR] Portal shader was not ready. Unity will retry after the next script/shader refresh.");
                EditorApplication.delayCall += AutoBuildOnce;
                return;
            }

            Material exteriorMaterial = CreateOrLoadLitMaterial(
                MaterialFolder + "/ExteriorRoom.mat",
                new Color(0.12f, 0.18f, 0.24f));

            Material interiorMaterial = CreateOrLoadLitMaterial(
                MaterialFolder + "/InteriorRoom.mat",
                new Color(0.30f, 0.20f, 0.10f));

            Material trimMaterial = CreateOrLoadLitMaterial(
                MaterialFolder + "/PortalFrame.mat",
                new Color(0.08f, 0.16f, 0.32f),
                0.55f,
                0.25f);

            Material floorMaterial = CreateOrLoadLitMaterial(
                MaterialFolder + "/Floor.mat",
                new Color(0.16f, 0.16f, 0.17f),
                0.15f,
                0.35f);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            SetupLighting();

            GameObject setup = CreateXRSetup();
            if (setup == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR] Could not locate the Starter Assets 'XR Interaction Setup' prefab.");
                return;
            }

            RemoveVignetteObjects(setup);
            if (setup.GetComponent<ComfortVignetteDisabler>() == null)
                setup.AddComponent<ComfortVignetteDisabler>();

            GameObject xrOrigin = FindChildByName(setup.transform, "XR Origin (XR Rig)");
            Camera xrCamera = xrOrigin != null
                ? xrOrigin.GetComponentInChildren<Camera>(true)
                : setup.GetComponentInChildren<Camera>(true);

            if (xrOrigin == null || xrCamera == null)
            {
                Debug.LogError(
                    "[DoctorWhoVR] The XR setup prefab did not contain an XR Origin and camera.");
                return;
            }

            xrOrigin.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -3f),
                Quaternion.identity);

            xrCamera.tag = "MainCamera";

            PortalTraveller traveller =
                xrOrigin.GetComponent<PortalTraveller>();
            if (traveller == null)
                traveller = xrOrigin.AddComponent<PortalTraveller>();
            traveller.ConfigureHead(xrCamera.transform);

            CreateRoom(
                "Exterior Prototype",
                Vector3.zero,
                exteriorMaterial,
                floorMaterial,
                true);

            CreateRoom(
                "TARDIS Interior Prototype",
                new Vector3(0f, 0f, 30f),
                interiorMaterial,
                floorMaterial,
                false);

            Portal portalA = CreatePortal(
                "Exterior Portal",
                new Vector3(0f, 1.75f, 4.94f),
                Quaternion.Euler(0f, 180f, 0f),
                portalMaterial,
                trimMaterial,
                portalLayer);

            Portal portalB = CreatePortal(
                "Interior Portal",
                new Vector3(0f, 1.75f, 25.06f),
                Quaternion.identity,
                portalMaterial,
                trimMaterial,
                portalLayer);

            Renderer surfaceA =
                portalA.transform.Find("Portal Surface").GetComponent<Renderer>();
            Renderer surfaceB =
                portalB.transform.Find("Portal Surface").GetComponent<Renderer>();

            portalA.Configure(portalB, surfaceA, portalLayer);
            portalB.Configure(portalA, surfaceB, portalLayer);

            CreateSafetyFloor();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            RemoveStarterDemoContent();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = xrOrigin;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log(
                "[DoctorWhoVR] Clean portal prototype created. The tunneling vignette is disabled and sample demo content was removed.");

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "Portal Prototype Ready",
                    "The clean TARDIS portal scene was rebuilt successfully.",
                    "OK");
            }
        }

        private static GameObject CreateXRSetup()
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "XR Interaction Setup t:Prefab",
                new[] { "Assets" });

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.name == "XR Interaction Setup")
                {
                    var instance =
                        PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance != null)
                    {
                        instance.name = "XR Interaction Setup";
                        return instance;
                    }
                }
            }

            return null;
        }

        private static void RemoveVignetteObjects(GameObject root)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children.Reverse())
            {
                if (child == root.transform)
                    continue;

                if (child.name.Equals(
                        "TunnelingVignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals(
                        "Tunneling Vignette",
                        StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals(
                        "Locomotion Vignette",
                        StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static GameObject FindChildByName(
            Transform root,
            string objectName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }

            return null;
        }

        private static void CreateRoom(
            string roomName,
            Vector3 center,
            Material wallMaterial,
            Material floorMaterial,
            bool portalOnPositiveZ)
        {
            var room = new GameObject(roomName);
            room.transform.position = center;

            CreateCube(
                room.transform,
                "Floor",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(10f, 0.2f, 10f),
                floorMaterial);

            CreateCube(
                room.transform,
                "Ceiling",
                new Vector3(0f, 5.1f, 0f),
                new Vector3(10f, 0.2f, 10f),
                wallMaterial);

            CreateCube(
                room.transform,
                "Left Wall",
                new Vector3(-5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial);

            CreateCube(
                room.transform,
                "Right Wall",
                new Vector3(5.1f, 2.5f, 0f),
                new Vector3(0.2f, 5f, 10f),
                wallMaterial);

            float solidWallZ = portalOnPositiveZ ? -5.1f : 5.1f;
            CreateCube(
                room.transform,
                "Solid Wall",
                new Vector3(0f, 2.5f, solidWallZ),
                new Vector3(10f, 5f, 0.2f),
                wallMaterial);

            float portalWallZ = portalOnPositiveZ ? 5.1f : -5.1f;
            CreatePortalWall(
                room.transform,
                portalWallZ,
                wallMaterial);
        }

        private static void CreatePortalWall(
            Transform parent,
            float localZ,
            Material material)
        {
            const float openingWidth = 2.7f;
            const float openingHeight = 3.65f;
            const float roomWidth = 10f;
            const float roomHeight = 5f;

            float sideWidth = (roomWidth - openingWidth) * 0.5f;
            float sideOffset = openingWidth * 0.5f + sideWidth * 0.5f;

            CreateCube(
                parent,
                "Portal Wall Left",
                new Vector3(-sideOffset, roomHeight * 0.5f, localZ),
                new Vector3(sideWidth, roomHeight, 0.2f),
                material);

            CreateCube(
                parent,
                "Portal Wall Right",
                new Vector3(sideOffset, roomHeight * 0.5f, localZ),
                new Vector3(sideWidth, roomHeight, 0.2f),
                material);

            float topHeight = roomHeight - openingHeight;
            CreateCube(
                parent,
                "Portal Wall Top",
                new Vector3(
                    0f,
                    openingHeight + topHeight * 0.5f,
                    localZ),
                new Vector3(openingWidth, topHeight, 0.2f),
                material);
        }

        private static Portal CreatePortal(
            string portalName,
            Vector3 position,
            Quaternion rotation,
            Material surfaceMaterial,
            Material trimMaterial,
            int portalLayer)
        {
            var portalObject = new GameObject(portalName);
            portalObject.transform.SetPositionAndRotation(position, rotation);

            var trigger = portalObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 3.4f, 0.5f);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Portal Surface";
            surface.transform.SetParent(portalObject.transform, false);
            surface.transform.localPosition = Vector3.zero;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(2.4f, 3.4f, 1f);
            surface.layer = portalLayer;

            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.GetComponent<Renderer>().sharedMaterial = surfaceMaterial;

            CreateFramePiece(
                portalObject.transform,
                "Frame Left",
                new Vector3(-1.35f, 0f, 0.05f),
                new Vector3(0.25f, 3.8f, 0.25f),
                trimMaterial);

            CreateFramePiece(
                portalObject.transform,
                "Frame Right",
                new Vector3(1.35f, 0f, 0.05f),
                new Vector3(0.25f, 3.8f, 0.25f),
                trimMaterial);

            CreateFramePiece(
                portalObject.transform,
                "Frame Top",
                new Vector3(0f, 1.825f, 0.05f),
                new Vector3(2.95f, 0.25f, 0.25f),
                trimMaterial);

            return portalObject.AddComponent<Portal>();
        }

        private static void CreateFramePiece(
            Transform parent,
            string pieceName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = pieceName;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = localScale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateSafetyFloor()
        {
            GameObject safety = GameObject.CreatePrimitive(PrimitiveType.Cube);
            safety.name = "Portal Safety Floor";
            safety.transform.position = new Vector3(0f, -0.35f, 15f);
            safety.transform.localScale = new Vector3(14f, 0.5f, 42f);
            safety.GetComponent<Renderer>().enabled = false;
        }

        private static GameObject CreateCube(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.15f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.07f);

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            CreatePointLight(
                "Exterior Room Light",
                new Vector3(0f, 4f, 0f),
                new Color(0.55f, 0.75f, 1f));

            CreatePointLight(
                "Interior Room Light",
                new Vector3(0f, 4f, 30f),
                new Color(1f, 0.68f, 0.32f));
        }

        private static void CreatePointLight(
            string lightName,
            Vector3 position,
            Color color)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3.5f;
            light.color = color;
            light.shadows = LightShadows.Soft;
        }

        private static Material CreateOrLoadPortalMaterial()
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(PortalMaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("DoctorWhoVR/PortalSurface");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "PortalSurface"
            };
            AssetDatabase.CreateAsset(material, PortalMaterialPath);
            return material;
        }

        private static Material CreateOrLoadLitMaterial(
            string path,
            Color color,
            float metallic = 0f,
            float smoothness = 0.25f)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path),
                color = color
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void RemoveStarterDemoContent()
        {
            string[] demoSceneGuids = AssetDatabase.FindAssets(
                "DemoScene t:Scene",
                new[] { "Assets/Samples" });

            foreach (string guid in demoSceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf(
                        "XR Interaction Toolkit",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            const string demoAssetsPath =
                "Assets/Samples/XR Interaction Toolkit/2.5.2/Starter Assets/DemoSceneAssets";
            if (AssetDatabase.IsValidFolder(demoAssetsPath))
                AssetDatabase.DeleteAsset(demoAssetsPath);
        }

        private static int EnsureLayer(string layerName, int preferredIndex)
        {
            for (int i = 8; i <= 31; ++i)
            {
                if (LayerMask.LayerToName(i) == layerName)
                    return i;
            }

            UnityEngine.Object tagManagerAsset =
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset")[0];

            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");

            int chosenIndex = -1;
            if (preferredIndex >= 8 &&
                preferredIndex <= 31 &&
                string.IsNullOrEmpty(
                    layers.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                chosenIndex = preferredIndex;
            }
            else
            {
                for (int i = 8; i <= 31; ++i)
                {
                    if (string.IsNullOrEmpty(
                            layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        chosenIndex = i;
                        break;
                    }
                }
            }

            if (chosenIndex < 0)
                throw new InvalidOperationException(
                    "No free Unity layer is available for portal surfaces.");

            layers.GetArrayElementAtIndex(chosenIndex).stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return chosenIndex;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)
                ?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
