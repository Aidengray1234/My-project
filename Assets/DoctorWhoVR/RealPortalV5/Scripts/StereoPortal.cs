using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace DoctorWhoVR.RealPortalV5
{
    /// <summary>
    /// A non-recursive, two-way stereo VR portal for Unity 2021.3, URP and
    /// OpenXR. Each eye receives its own render texture. Camera orientation is
    /// never extracted from an XR matrix; the exact view matrix is transformed
    /// directly through the linked portals.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StereoPortal : MonoBehaviour
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
        [SerializeField] private StereoPortal _target;
        [SerializeField] private Renderer _surfaceRenderer;

        [Header("Opening")]
        [SerializeField, Min(0.1f)] private float _openingWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float _openingHeight = 3.4f;
        [SerializeField, Min(0f)] private float _exitOffset = 0.18f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.02f;

        [Header("Rendering")]
        [SerializeField, Range(0.35f, 1.25f)] private float _resolutionScale = 0.75f;
        [SerializeField, Min(1f)] private float _maximumRenderDistance = 55f;
        [SerializeField, Min(0.001f)] private float _clipPlaneOffset = 0.055f;
        [SerializeField] private int _portalLayer = 30;
        [SerializeField] private bool _renderBackFace;

        private readonly Dictionary<VRPortalTraveller, TravellerState>
            _travellerStates =
                new Dictionary<VRPortalTraveller, TravellerState>();

        private readonly List<VRPortalTraveller> _removeBuffer =
            new List<VRPortalTraveller>();

        private Camera _portalCamera;
        private RenderTexture _leftTexture;
        private RenderTexture _rightTexture;
        private Material _runtimeMaterial;

        private int _textureWidth;
        private int _textureHeight;

        private static readonly int LeftTextureId =
            Shader.PropertyToID("_LeftTex");

        private static readonly int RightTextureId =
            Shader.PropertyToID("_RightTex");

        public StereoPortal Target
        {
            get { return _target; }
        }

        public float ExitOffset
        {
            get { return _exitOffset; }
        }

        public void Configure(
            StereoPortal target,
            Renderer surfaceRenderer,
            int portalLayer)
        {
            _target = target;
            _surfaceRenderer = surfaceRenderer;
            _portalLayer = portalLayer;
        }

        private void Awake()
        {
            ConfigureTrigger();
            EnsureResources(null);
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
            ReleaseResources();
        }

        private void OnValidate()
        {
            ConfigureTrigger();
        }

        private void LateUpdate()
        {
            TrackCrossings();
        }

        private void ConfigureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();

            if (trigger == null)
                return;

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;

            trigger.size =
                new Vector3(
                    _openingWidth,
                    _openingHeight,
                    0.65f);
        }

        private Matrix4x4 SourceToTargetMatrix()
        {
            if (_target == null)
                return Matrix4x4.identity;

            return
                _target.transform.localToWorldMatrix *
                HalfTurnMatrix *
                transform.worldToLocalMatrix;
        }

        public Vector3 TransformPointToTarget(Vector3 worldPoint)
        {
            return SourceToTargetMatrix().MultiplyPoint3x4(worldPoint);
        }

        public Vector3 TransformDirectionToTarget(Vector3 worldDirection)
        {
            return SourceToTargetMatrix().MultiplyVector(worldDirection);
        }

        public Quaternion TransformRotationToTarget(Quaternion worldRotation)
        {
            return
                _target.transform.rotation *
                HalfTurnRotation *
                Quaternion.Inverse(transform.rotation) *
                worldRotation;
        }

        private void TrackCrossings()
        {
            _removeBuffer.Clear();

            for (int index = 0;
                 index < VRPortalTraveller.ActiveTravellers.Count;
                 ++index)
            {
                VRPortalTraveller traveller =
                    VRPortalTraveller.ActiveTravellers[index];

                if (traveller == null ||
                    !traveller.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 headPosition = traveller.Head.position;
                float currentSide = SignedDistance(headPosition);

                TravellerState state;

                if (!_travellerStates.TryGetValue(
                        traveller,
                        out state))
                {
                    state = new TravellerState
                    {
                        PreviousHeadPosition = headPosition,
                        PreviousSide = currentSide
                    };

                    _travellerStates.Add(traveller, state);
                    continue;
                }

                if (_target != null &&
                    traveller.CanTeleport &&
                    state.PreviousSide > _crossingEpsilon &&
                    currentSide <= -_crossingEpsilon)
                {
                    float denominator =
                        state.PreviousSide - currentSide;

                    float amount =
                        denominator > 0.00001f
                            ? Mathf.Clamp01(
                                state.PreviousSide / denominator)
                            : 1f;

                    Vector3 crossingPoint =
                        Vector3.Lerp(
                            state.PreviousHeadPosition,
                            headPosition,
                            amount);

                    if (InsideOpening(crossingPoint))
                    {
                        traveller.TeleportThrough(this);

                        headPosition = traveller.Head.position;
                        currentSide = SignedDistance(headPosition);
                    }
                }

                state.PreviousHeadPosition = headPosition;
                state.PreviousSide = currentSide;
            }

            foreach (
                KeyValuePair<VRPortalTraveller, TravellerState>
                    pair in _travellerStates)
            {
                if (pair.Key == null ||
                    !pair.Key.isActiveAndEnabled)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            for (int index = 0;
                 index < _removeBuffer.Count;
                 ++index)
            {
                _travellerStates.Remove(_removeBuffer[index]);
            }
        }

        private float SignedDistance(Vector3 worldPoint)
        {
            return Vector3.Dot(
                transform.forward,
                worldPoint - transform.position);
        }

        private bool InsideOpening(Vector3 worldPoint)
        {
            Vector3 localPoint =
                transform.InverseTransformPoint(worldPoint);

            return
                Mathf.Abs(localPoint.x) <= _openingWidth * 0.5f &&
                Mathf.Abs(localPoint.y) <= _openingHeight * 0.5f;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera sourceCamera)
        {
            if (s_IsRenderingPortal ||
                sourceCamera == null ||
                sourceCamera.cameraType != CameraType.Game ||
                !sourceCamera.CompareTag("MainCamera") ||
                _target == null ||
                _surfaceRenderer == null)
            {
                return;
            }

            if (!ShouldRender(sourceCamera))
                return;

            EnsureResources(sourceCamera);

            if (_portalCamera == null ||
                _runtimeMaterial == null ||
                _leftTexture == null ||
                _rightTexture == null)
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
                        _leftTexture);

                    RenderEye(
                        context,
                        sourceCamera,
                        Camera.StereoscopicEye.Right,
                        _rightTexture);
                }
                else
                {
                    RenderMono(
                        context,
                        sourceCamera,
                        _leftTexture);

                    Graphics.Blit(_leftTexture, _rightTexture);
                }

                _runtimeMaterial.SetTexture(
                    LeftTextureId,
                    _leftTexture);

                _runtimeMaterial.SetTexture(
                    RightTextureId,
                    _rightTexture);
            }
            finally
            {
                ResetPortalCameraMatrices();
                s_IsRenderingPortal = false;
            }
        }

        private bool ShouldRender(Camera sourceCamera)
        {
            float distanceSquared =
                (sourceCamera.transform.position - transform.position)
                .sqrMagnitude;

            if (distanceSquared >
                _maximumRenderDistance * _maximumRenderDistance)
            {
                return false;
            }

            if (!_renderBackFace &&
                SignedDistance(sourceCamera.transform.position) < -0.02f)
            {
                return false;
            }

            Plane[] planes =
                GeometryUtility.CalculateFrustumPlanes(sourceCamera);

            return GeometryUtility.TestPlanesAABB(
                planes,
                _surfaceRenderer.bounds);
        }

        private void RenderMono(
            ScriptableRenderContext context,
            Camera sourceCamera,
            RenderTexture destination)
        {
            Matrix4x4 sourceView =
                sourceCamera.worldToCameraMatrix;

            Matrix4x4 sourceProjection =
                sourceCamera.projectionMatrix;

            RenderMappedView(
                context,
                sourceCamera,
                sourceView,
                sourceProjection,
                sourceCamera.transform.position,
                sourceCamera.transform.rotation,
                destination);
        }

        private void RenderEye(
            ScriptableRenderContext context,
            Camera sourceCamera,
            Camera.StereoscopicEye eye,
            RenderTexture destination)
        {
            Matrix4x4 sourceView =
                sourceCamera.GetStereoViewMatrix(eye);

            Matrix4x4 sourceProjection =
                sourceCamera.GetStereoProjectionMatrix(eye);

            Matrix4x4 eyeToWorld = sourceView.inverse;
            Vector4 eyePositionColumn = eyeToWorld.GetColumn(3);

            Vector3 sourceEyePosition =
                new Vector3(
                    eyePositionColumn.x,
                    eyePositionColumn.y,
                    eyePositionColumn.z);

            RenderMappedView(
                context,
                sourceCamera,
                sourceView,
                sourceProjection,
                sourceEyePosition,
                sourceCamera.transform.rotation,
                destination);
        }

        private void RenderMappedView(
            ScriptableRenderContext context,
            Camera sourceCamera,
            Matrix4x4 sourceView,
            Matrix4x4 sourceProjection,
            Vector3 sourceEyePosition,
            Quaternion sourceHeadRotation,
            RenderTexture destination)
        {
            Matrix4x4 sourceToTarget =
                SourceToTargetMatrix();

            Matrix4x4 targetToSource =
                sourceToTarget.inverse;

            // Exact portal view transformation:
            // Vportal = Vsource * inverse(MsourceToTarget)
            Matrix4x4 portalView =
                sourceView * targetToSource;

            Vector3 portalEyePosition =
                sourceToTarget.MultiplyPoint3x4(sourceEyePosition);

            Quaternion portalRotation =
                TransformRotationToTarget(sourceHeadRotation);

            _portalCamera.transform.SetPositionAndRotation(
                portalEyePosition,
                portalRotation);

            _portalCamera.worldToCameraMatrix = portalView;
            _portalCamera.projectionMatrix = sourceProjection;
            _portalCamera.targetTexture = destination;

            Vector4 clipPlaneCameraSpace =
                BuildClipPlaneCameraSpace(portalView);

            _portalCamera.projectionMatrix =
                _portalCamera.CalculateObliqueMatrix(
                    clipPlaneCameraSpace);

            UniversalRenderPipeline.RenderSingleCamera(
                context,
                _portalCamera);
        }

        private Vector4 BuildClipPlaneCameraSpace(
            Matrix4x4 portalView)
        {
            Vector3 planePoint =
                _target.transform.position +
                _target.transform.forward *
                _clipPlaneOffset;

            // Match the established portal-camera technique: remove everything
            // behind the destination doorway by facing the plane away from the
            // visible destination room.
            Vector3 planeNormal =
                -_target.transform.forward;

            Vector4 worldPlane =
                new Vector4(
                    planeNormal.x,
                    planeNormal.y,
                    planeNormal.z,
                    -Vector3.Dot(planeNormal, planePoint));

            return
                Matrix4x4.Transpose(portalView.inverse) *
                worldPlane;
        }

        private void CopyCameraSettings(Camera sourceCamera)
        {
            _portalCamera.clearFlags = sourceCamera.clearFlags;
            _portalCamera.backgroundColor = sourceCamera.backgroundColor;
            _portalCamera.allowHDR = sourceCamera.allowHDR;
            _portalCamera.allowMSAA = false;
            _portalCamera.useOcclusionCulling = false;
            _portalCamera.nearClipPlane =
                Mathf.Max(0.01f, sourceCamera.nearClipPlane);
            _portalCamera.farClipPlane = sourceCamera.farClipPlane;

            int safeLayer = Mathf.Clamp(_portalLayer, 0, 31);

            _portalCamera.cullingMask =
                sourceCamera.cullingMask &
                ~(1 << safeLayer);
        }

        private void EnsureResources(Camera sourceCamera)
        {
            EnsureMaterial();
            EnsureCamera();

            int baseWidth =
                XRSettings.eyeTextureWidth > 0
                    ? XRSettings.eyeTextureWidth
                    : (sourceCamera != null
                        ? Mathf.Max(512, sourceCamera.pixelWidth)
                        : 1024);

            int baseHeight =
                XRSettings.eyeTextureHeight > 0
                    ? XRSettings.eyeTextureHeight
                    : (sourceCamera != null
                        ? Mathf.Max(512, sourceCamera.pixelHeight)
                        : 1024);

            int width =
                Mathf.Clamp(
                    Mathf.RoundToInt(baseWidth * _resolutionScale),
                    512,
                    2048);

            int height =
                Mathf.Clamp(
                    Mathf.RoundToInt(baseHeight * _resolutionScale),
                    512,
                    2048);

            if (_leftTexture == null ||
                _rightTexture == null ||
                width != _textureWidth ||
                height != _textureHeight)
            {
                ReleaseTextures();

                _textureWidth = width;
                _textureHeight = height;

                _leftTexture =
                    CreateEyeTexture(
                        name + " Left Portal Eye",
                        width,
                        height);

                _rightTexture =
                    CreateEyeTexture(
                        name + " Right Portal Eye",
                        width,
                        height);
            }
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null ||
                _surfaceRenderer == null ||
                _surfaceRenderer.sharedMaterial == null)
            {
                return;
            }

            _runtimeMaterial =
                new Material(_surfaceRenderer.sharedMaterial);

            _runtimeMaterial.name =
                _surfaceRenderer.sharedMaterial.name +
                " (Runtime " + name + ")";

            _surfaceRenderer.material = _runtimeMaterial;
        }

        private void EnsureCamera()
        {
            if (_portalCamera != null)
                return;

            GameObject cameraObject =
                new GameObject(name + " Render Camera");

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
            texture.dimension =
                UnityEngine.Rendering.TextureDimension.Tex2D;
            texture.vrUsage = VRTextureUsage.None;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.antiAliasing = 1;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Create();

            return texture;
        }

        private void ResetPortalCameraMatrices()
        {
            if (_portalCamera == null)
                return;

            _portalCamera.targetTexture = null;
            _portalCamera.ResetWorldToCameraMatrix();
            _portalCamera.ResetProjectionMatrix();
            _portalCamera.ResetCullingMatrix();
        }

        private void ReleaseResources()
        {
            ReleaseTextures();

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

        private void ReleaseTextures()
        {
            if (_leftTexture != null)
            {
                _leftTexture.Release();
                Destroy(_leftTexture);
                _leftTexture = null;
            }

            if (_rightTexture != null)
            {
                _rightTexture.Release();
                Destroy(_rightTexture);
                _rightTexture = null;
            }
        }
    }
}
