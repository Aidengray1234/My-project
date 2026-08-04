using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DoctorWhoVR.Portals
{
    /// <summary>
    /// Two-way doorway portal for URP and OpenXR.
    ///
    /// Rendering is performed once per visible eye. The portal surface samples
    /// the result by screen position, so it behaves like a window instead of
    /// stretching the complete camera image across the doorway mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Portal : MonoBehaviour
    {
        private sealed class TravellerState
        {
            public Vector3 PreviousHeadPosition;
            public float PreviousSide;
        }

        private static readonly Quaternion HalfTurn =
            Quaternion.Euler(0f, 180f, 0f);

        private static bool s_IsRenderingPortal;

        [Header("Pair")]
        [SerializeField] private Portal _target;
        [SerializeField] private Renderer _surfaceRenderer;

        [Header("Opening")]
        [SerializeField, Min(0.1f)] private float _openingWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float _openingHeight = 3.4f;
        [SerializeField, Min(0f)] private float _exitOffset = 0.10f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.02f;

        [Header("Rendering")]
        [SerializeField, Range(256, 2048)] private int _textureWidth = 1024;
        [SerializeField, Range(0.5f, 2f)] private float _textureAspect = 1f;
        [SerializeField, Min(1f)] private float _maximumRenderDistance = 60f;
        [SerializeField, Min(0.001f)] private float _nearClipOffset = 0.06f;
        [SerializeField] private int _portalSurfaceLayer = 30;
        [SerializeField] private bool _renderFromBackSide;

        private readonly Dictionary<PortalTraveller, TravellerState> _travellerStates =
            new Dictionary<PortalTraveller, TravellerState>();

        private readonly List<PortalTraveller> _removeBuffer =
            new List<PortalTraveller>();

        private Camera _portalCamera;
        private RenderTexture _leftEyeTexture;
        private RenderTexture _rightEyeTexture;
        private Material _runtimeMaterial;

        private int _currentTextureWidth;
        private int _currentTextureHeight;

        private static readonly int LeftTextureId =
            Shader.PropertyToID("_LeftTex");

        private static readonly int RightTextureId =
            Shader.PropertyToID("_RightTex");

        public Portal Target
        {
            get { return _target; }
        }

        public float ExitOffset
        {
            get { return _exitOffset; }
        }

        public void Configure(
            Portal target,
            Renderer surfaceRenderer,
            int portalSurfaceLayer)
        {
            _target = target;
            _surfaceRenderer = surfaceRenderer;
            _portalSurfaceLayer = portalSurfaceLayer;
        }

        private void Awake()
        {
            ConfigureTrigger();
            EnsureRenderResources(null);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering +=
                OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -=
                OnBeginCameraRendering;

            _travellerStates.Clear();
        }

        private void OnDestroy()
        {
            ReleaseRenderResources();
        }

        private void OnValidate()
        {
            ConfigureTrigger();
        }

        private void LateUpdate()
        {
            TrackTravellerCrossings();
        }

        private void ConfigureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null)
                return;

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(
                _openingWidth,
                _openingHeight,
                0.5f);
        }

        private void TrackTravellerCrossings()
        {
            _removeBuffer.Clear();

            for (int i = 0;
                 i < PortalTraveller.ActiveTravellers.Count;
                 ++i)
            {
                PortalTraveller traveller =
                    PortalTraveller.ActiveTravellers[i];

                if (traveller == null ||
                    !traveller.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 currentHeadPosition =
                    traveller.Head.position;

                float currentSide =
                    SignedDistanceToPlane(currentHeadPosition);

                TravellerState state;
                if (!_travellerStates.TryGetValue(
                        traveller,
                        out state))
                {
                    state = new TravellerState
                    {
                        PreviousHeadPosition =
                            currentHeadPosition,
                        PreviousSide = currentSide
                    };

                    _travellerStates.Add(traveller, state);
                    continue;
                }

                if (traveller.CanTeleport &&
                    state.PreviousSide > _crossingEpsilon &&
                    currentSide <= -_crossingEpsilon)
                {
                    float denominator =
                        state.PreviousSide - currentSide;

                    float amount =
                        denominator > 0.00001f
                            ? Mathf.Clamp01(
                                state.PreviousSide /
                                denominator)
                            : 1f;

                    Vector3 crossingPoint =
                        Vector3.Lerp(
                            state.PreviousHeadPosition,
                            currentHeadPosition,
                            amount);

                    if (IsInsideOpening(crossingPoint))
                    {
                        traveller.TeleportThrough(this);

                        currentHeadPosition =
                            traveller.Head.position;

                        currentSide =
                            SignedDistanceToPlane(
                                currentHeadPosition);
                    }
                }

                state.PreviousHeadPosition =
                    currentHeadPosition;

                state.PreviousSide = currentSide;
            }

            foreach (
                KeyValuePair<PortalTraveller, TravellerState>
                    pair in _travellerStates)
            {
                if (pair.Key == null ||
                    !pair.Key.isActiveAndEnabled)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            for (int i = 0;
                 i < _removeBuffer.Count;
                 ++i)
            {
                _travellerStates.Remove(
                    _removeBuffer[i]);
            }
        }

        private float SignedDistanceToPlane(
            Vector3 worldPoint)
        {
            return Vector3.Dot(
                transform.forward,
                worldPoint - transform.position);
        }

        private bool IsInsideOpening(
            Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            return
                Mathf.Abs(localPoint.x) <=
                    _openingWidth * 0.5f &&
                Mathf.Abs(localPoint.y) <=
                    _openingHeight * 0.5f;
        }

        public Vector3 TransformPointToTarget(
            Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            localPoint = HalfTurn * localPoint;

            return _target.transform.TransformPoint(
                localPoint);
        }

        public Vector3 TransformDirectionToTarget(
            Vector3 worldDirection)
        {
            Vector3 localDirection =
                transform.InverseTransformDirection(
                    worldDirection);

            localDirection =
                HalfTurn * localDirection;

            return _target.transform.TransformDirection(
                localDirection);
        }

        public Quaternion TransformRotationToTarget(
            Quaternion worldRotation)
        {
            return
                _target.transform.rotation *
                HalfTurn *
                Quaternion.Inverse(transform.rotation) *
                worldRotation;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera sourceCamera)
        {
            if (s_IsRenderingPortal ||
                _target == null ||
                _surfaceRenderer == null ||
                sourceCamera == null ||
                sourceCamera.cameraType !=
                    CameraType.Game ||
                !sourceCamera.CompareTag("MainCamera"))
            {
                return;
            }

            if (!ShouldRender(sourceCamera))
                return;

            EnsureRenderResources(sourceCamera);

            if (_portalCamera == null ||
                _runtimeMaterial == null ||
                _leftEyeTexture == null)
            {
                return;
            }

            s_IsRenderingPortal = true;

            try
            {
                CopyCameraSettings(sourceCamera);

                if (sourceCamera.stereoEnabled)
                {
                    RenderEye(
                        context,
                        sourceCamera,
                        Camera.StereoscopicEye.Left,
                        _leftEyeTexture);

                    RenderEye(
                        context,
                        sourceCamera,
                        Camera.StereoscopicEye.Right,
                        _rightEyeTexture);

                    _runtimeMaterial.SetTexture(
                        LeftTextureId,
                        _leftEyeTexture);

                    _runtimeMaterial.SetTexture(
                        RightTextureId,
                        _rightEyeTexture);
                }
                else
                {
                    RenderEye(
                        context,
                        sourceCamera,
                        Camera.StereoscopicEye.Left,
                        _leftEyeTexture);

                    _runtimeMaterial.SetTexture(
                        LeftTextureId,
                        _leftEyeTexture);

                    _runtimeMaterial.SetTexture(
                        RightTextureId,
                        _leftEyeTexture);
                }
            }
            finally
            {
                if (_portalCamera != null)
                {
                    _portalCamera.targetTexture = null;
                    _portalCamera.ResetCullingMatrix();
                }

                s_IsRenderingPortal = false;
            }
        }

        private bool ShouldRender(
            Camera sourceCamera)
        {
            Vector3 cameraToPortal =
                transform.position -
                sourceCamera.transform.position;

            if (cameraToPortal.sqrMagnitude >
                _maximumRenderDistance *
                _maximumRenderDistance)
            {
                return false;
            }

            if (!_renderFromBackSide)
            {
                float cameraSide =
                    SignedDistanceToPlane(
                        sourceCamera.transform.position);

                if (cameraSide < -0.03f)
                    return false;
            }

            Plane[] planes =
                GeometryUtility.CalculateFrustumPlanes(
                    sourceCamera);

            return GeometryUtility.TestPlanesAABB(
                planes,
                _surfaceRenderer.bounds);
        }

        private void CopyCameraSettings(
            Camera sourceCamera)
        {
            _portalCamera.clearFlags =
                sourceCamera.clearFlags;

            _portalCamera.backgroundColor =
                sourceCamera.backgroundColor;

            _portalCamera.allowHDR =
                sourceCamera.allowHDR;

            _portalCamera.allowMSAA = false;
            _portalCamera.useOcclusionCulling = false;

            _portalCamera.nearClipPlane =
                Mathf.Max(
                    0.01f,
                    sourceCamera.nearClipPlane);

            _portalCamera.farClipPlane =
                sourceCamera.farClipPlane;

            int safeLayer =
                Mathf.Clamp(
                    _portalSurfaceLayer,
                    0,
                    31);

            _portalCamera.cullingMask =
                sourceCamera.cullingMask &
                ~(1 << safeLayer);
        }

        private void RenderEye(
            ScriptableRenderContext context,
            Camera sourceCamera,
            Camera.StereoscopicEye eye,
            RenderTexture destination)
        {
            Vector3 sourceEyePosition;
            Quaternion sourceEyeRotation;
            Matrix4x4 sourceProjection;

            if (sourceCamera.stereoEnabled)
            {
                Matrix4x4 eyeToWorld =
                    sourceCamera
                        .GetStereoViewMatrix(eye)
                        .inverse;

                Vector4 positionColumn =
                    eyeToWorld.GetColumn(3);

                sourceEyePosition =
                    new Vector3(
                        positionColumn.x,
                        positionColumn.y,
                        positionColumn.z);

                sourceEyeRotation =
                    eyeToWorld.rotation;

                sourceProjection =
                    sourceCamera
                        .GetStereoProjectionMatrix(eye);
            }
            else
            {
                sourceEyePosition =
                    sourceCamera.transform.position;

                sourceEyeRotation =
                    sourceCamera.transform.rotation;

                sourceProjection =
                    sourceCamera.projectionMatrix;
            }

            _portalCamera.transform.SetPositionAndRotation(
                TransformPointToTarget(
                    sourceEyePosition),
                TransformRotationToTarget(
                    sourceEyeRotation));

            _portalCamera.targetTexture =
                destination;

            _portalCamera.projectionMatrix =
                sourceProjection;

            Vector4 clipPlane =
                BuildCameraSpaceClipPlane();

            _portalCamera.projectionMatrix =
                _portalCamera.CalculateObliqueMatrix(
                    clipPlane);

            _portalCamera.cullingMatrix =
                _portalCamera.projectionMatrix *
                _portalCamera.worldToCameraMatrix;

            UniversalRenderPipeline.RenderSingleCamera(
                context,
                _portalCamera);
        }

        private Vector4 BuildCameraSpaceClipPlane()
        {
            Vector3 destinationPlanePosition =
                _target.transform.position +
                _target.transform.forward *
                _nearClipOffset;

            Vector3 destinationPlaneNormal =
                _target.transform.forward;

            float cameraSide =
                Vector3.Dot(
                    destinationPlaneNormal,
                    _portalCamera.transform.position -
                    destinationPlanePosition);

            if (cameraSide < 0f)
                destinationPlaneNormal *= -1f;

            Matrix4x4 worldToCamera =
                _portalCamera.worldToCameraMatrix;

            Vector3 cameraSpacePosition =
                worldToCamera.MultiplyPoint(
                    destinationPlanePosition);

            Vector3 cameraSpaceNormal =
                worldToCamera
                    .MultiplyVector(
                        destinationPlaneNormal)
                    .normalized;

            return new Vector4(
                cameraSpaceNormal.x,
                cameraSpaceNormal.y,
                cameraSpaceNormal.z,
                -Vector3.Dot(
                    cameraSpacePosition,
                    cameraSpaceNormal));
        }

        private void EnsureRenderResources(
            Camera sourceCamera)
        {
            EnsureRuntimeMaterial();
            EnsurePortalCamera();

            int width = Mathf.Clamp(
                _textureWidth,
                256,
                2048);

            float aspect =
                sourceCamera != null &&
                sourceCamera.aspect > 0.1f
                    ? sourceCamera.aspect
                    : Mathf.Max(
                        0.5f,
                        _textureAspect);

            int height = Mathf.Clamp(
                Mathf.RoundToInt(width / aspect),
                256,
                2048);

            if (_leftEyeTexture == null ||
                _rightEyeTexture == null ||
                _currentTextureWidth != width ||
                _currentTextureHeight != height)
            {
                ReleaseRenderTextures();

                _currentTextureWidth = width;
                _currentTextureHeight = height;

                _leftEyeTexture =
                    CreateEyeTexture(
                        name + " Left Eye",
                        width,
                        height);

                _rightEyeTexture =
                    CreateEyeTexture(
                        name + " Right Eye",
                        width,
                        height);
            }
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null ||
                _surfaceRenderer == null ||
                _surfaceRenderer.sharedMaterial == null)
            {
                return;
            }

            _runtimeMaterial =
                new Material(
                    _surfaceRenderer.sharedMaterial);

            _runtimeMaterial.name =
                _surfaceRenderer
                    .sharedMaterial.name +
                " (Runtime " + name + ")";

            _surfaceRenderer.material =
                _runtimeMaterial;
        }

        private void EnsurePortalCamera()
        {
            if (_portalCamera != null)
                return;

            GameObject cameraObject =
                new GameObject(
                    name + " Portal Camera");

            cameraObject.hideFlags =
                HideFlags.HideAndDontSave;

            _portalCamera =
                cameraObject.AddComponent<Camera>();

            _portalCamera.enabled = false;

            _portalCamera.stereoTargetEye =
                StereoTargetEyeMask.None;

            UniversalAdditionalCameraData
                additionalData =
                    cameraObject.GetComponent<
                        UniversalAdditionalCameraData>();

            if (additionalData == null)
            {
                additionalData =
                    cameraObject.AddComponent<
                        UniversalAdditionalCameraData>();
            }

            additionalData.renderPostProcessing = false;

            additionalData.requiresColorOption =
                CameraOverrideOption.Off;

            additionalData.requiresDepthOption =
                CameraOverrideOption.Off;
        }

        private RenderTexture CreateEyeTexture(
            string textureName,
            int width,
            int height)
        {
            RenderTexture texture =
                new RenderTexture(
                    width,
                    height,
                    24,
                    RenderTextureFormat.DefaultHDR);

            texture.name = textureName;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.antiAliasing = 1;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Create();

            return texture;
        }

        private void ReleaseRenderResources()
        {
            ReleaseRenderTextures();

            if (_portalCamera != null)
            {
                Destroy(_portalCamera.gameObject);
                _portalCamera = null;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void ReleaseRenderTextures()
        {
            if (_leftEyeTexture != null)
            {
                _leftEyeTexture.Release();
                Destroy(_leftEyeTexture);
                _leftEyeTexture = null;
            }

            if (_rightEyeTexture != null)
            {
                _rightEyeTexture.Release();
                Destroy(_rightEyeTexture);
                _rightEyeTexture = null;
            }
        }
    }
}
