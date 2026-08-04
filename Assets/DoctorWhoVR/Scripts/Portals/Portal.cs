using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DoctorWhoVR.Portals
{
    /// <summary>
    /// Stable two-way doorway portal for URP/OpenXR.
    ///
    /// Portal V3 deliberately uses one center-eye render for both headset eyes.
    /// This removes XR eye-matrix roll/aim errors first. Once the doorway is
    /// fully stable, true stereo rendering can be added without changing
    /// crossing or teleport behavior.
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

        private static readonly Matrix4x4 HalfTurnMatrix =
            Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

        private static readonly Quaternion HalfTurnRotation =
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
        [SerializeField, Min(1f)] private float _maximumRenderDistance = 60f;
        [SerializeField, Min(0.001f)] private float _nearClipOffset = 0.08f;
        [SerializeField] private int _portalSurfaceLayer = 30;
        [SerializeField] private bool _renderFromBackSide;

        private readonly Dictionary<PortalTraveller, TravellerState>
            _travellerStates =
                new Dictionary<PortalTraveller, TravellerState>();

        private readonly List<PortalTraveller> _removeBuffer =
            new List<PortalTraveller>();

        private Camera _portalCamera;
        private RenderTexture _portalTexture;
        private Material _runtimeMaterial;

        private int _currentTextureWidth;
        private int _currentTextureHeight;

        private static readonly int PortalTextureId =
            Shader.PropertyToID("_PortalTex");

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
            EnsureRenderResources(null, Matrix4x4.identity);
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

        private Matrix4x4 GetSourceToTargetMatrix()
        {
            if (_target == null)
                return Matrix4x4.identity;

            return
                _target.transform.localToWorldMatrix *
                HalfTurnMatrix *
                transform.worldToLocalMatrix;
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
            return GetSourceToTargetMatrix()
                .MultiplyPoint3x4(worldPoint);
        }

        public Vector3 TransformDirectionToTarget(
            Vector3 worldDirection)
        {
            return GetSourceToTargetMatrix()
                .MultiplyVector(worldDirection);
        }

        public Quaternion TransformRotationToTarget(
            Quaternion worldRotation)
        {
            return
                _target.transform.rotation *
                HalfTurnRotation *
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
                sourceCamera.cameraType != CameraType.Game ||
                !sourceCamera.CompareTag("MainCamera"))
            {
                return;
            }

            if (!ShouldRender(sourceCamera))
                return;

            Matrix4x4 sourceProjection =
                BuildStableProjection(sourceCamera);

            EnsureRenderResources(
                sourceCamera,
                sourceProjection);

            if (_portalCamera == null ||
                _runtimeMaterial == null ||
                _portalTexture == null)
            {
                return;
            }

            s_IsRenderingPortal = true;

            try
            {
                CopyCameraSettings(sourceCamera);
                PositionPortalCamera(sourceCamera);

                _portalCamera.targetTexture =
                    _portalTexture;

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

                _runtimeMaterial.SetTexture(
                    PortalTextureId,
                    _portalTexture);
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

        private Matrix4x4 BuildStableProjection(
            Camera sourceCamera)
        {
            if (!sourceCamera.stereoEnabled)
                return sourceCamera.projectionMatrix;

            Matrix4x4 left =
                sourceCamera.GetStereoProjectionMatrix(
                    Camera.StereoscopicEye.Left);

            Matrix4x4 right =
                sourceCamera.GetStereoProjectionMatrix(
                    Camera.StereoscopicEye.Right);

            Matrix4x4 center = left;

            // Remove the per-eye horizontal shift. The portal is rendered from
            // the tracked head center, so the projection must also be centered.
            center.m00 = (left.m00 + right.m00) * 0.5f;
            center.m11 = (left.m11 + right.m11) * 0.5f;
            center.m02 = 0f;
            center.m12 = (left.m12 + right.m12) * 0.5f;

            return center;
        }

        private void PositionPortalCamera(
            Camera sourceCamera)
        {
            Matrix4x4 mappedCameraMatrix =
                GetSourceToTargetMatrix() *
                sourceCamera.transform.localToWorldMatrix;

            Vector4 positionColumn =
                mappedCameraMatrix.GetColumn(3);

            Vector3 position =
                new Vector3(
                    positionColumn.x,
                    positionColumn.y,
                    positionColumn.z);

            Vector4 forwardColumn =
                mappedCameraMatrix.GetColumn(2);

            Vector4 upColumn =
                mappedCameraMatrix.GetColumn(1);

            Vector3 forward =
                new Vector3(
                    forwardColumn.x,
                    forwardColumn.y,
                    forwardColumn.z).normalized;

            Vector3 up =
                new Vector3(
                    upColumn.x,
                    upColumn.y,
                    upColumn.z).normalized;

            if (forward.sqrMagnitude < 0.99f)
                forward = TransformDirectionToTarget(
                    sourceCamera.transform.forward)
                    .normalized;

            if (up.sqrMagnitude < 0.99f)
                up = TransformDirectionToTarget(
                    sourceCamera.transform.up)
                    .normalized;

            Quaternion rotation =
                Quaternion.LookRotation(forward, up);

            _portalCamera.transform.SetPositionAndRotation(
                position,
                rotation);
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
            Camera sourceCamera,
            Matrix4x4 projection)
        {
            EnsureRuntimeMaterial();
            EnsurePortalCamera();

            int width = Mathf.Clamp(
                _textureWidth,
                256,
                2048);

            float aspect = 1f;

            if (Mathf.Abs(projection.m00) > 0.0001f &&
                Mathf.Abs(projection.m11) > 0.0001f)
            {
                aspect =
                    Mathf.Abs(
                        projection.m11 /
                        projection.m00);
            }
            else if (sourceCamera != null &&
                     sourceCamera.aspect > 0.1f)
            {
                aspect = sourceCamera.aspect;
            }

            aspect = Mathf.Clamp(aspect, 0.5f, 2f);

            int height = Mathf.Clamp(
                Mathf.RoundToInt(width / aspect),
                256,
                2048);

            if (_portalTexture == null ||
                _currentTextureWidth != width ||
                _currentTextureHeight != height)
            {
                ReleaseRenderTexture();

                _currentTextureWidth = width;
                _currentTextureHeight = height;

                _portalTexture =
                    CreatePortalTexture(
                        name + " Portal View",
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
                _surfaceRenderer.sharedMaterial.name +
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

            UniversalAdditionalCameraData additionalData =
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

        private RenderTexture CreatePortalTexture(
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
            ReleaseRenderTexture();

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

        private void ReleaseRenderTexture()
        {
            if (_portalTexture == null)
                return;

            _portalTexture.Release();
            Destroy(_portalTexture);
            _portalTexture = null;
        }
    }
}
