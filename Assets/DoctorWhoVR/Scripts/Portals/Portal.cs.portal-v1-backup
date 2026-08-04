using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DoctorWhoVR.Portals
{
    /// <summary>
    /// A two-way, high-speed-safe portal for URP and OpenXR.
    /// It renders separate left/right-eye textures and moves the XR Origin root
    /// when the tracked head crosses the portal plane.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Portal : MonoBehaviour
    {
        private sealed class TravellerState
        {
            public Vector3 PreviousHeadPosition;
            public float PreviousSide;
            public bool Initialized;
        }

        private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);
        private static bool s_IsRenderingPortal;

        [Header("Pair")]
        [SerializeField] private Portal _target;
        [SerializeField] private Renderer _surfaceRenderer;

        [Header("Opening")]
        [SerializeField, Min(0.1f)] private float _openingWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float _openingHeight = 3.4f;
        [SerializeField, Min(0f)] private float _exitOffset = 0.06f;
        [SerializeField, Min(0.001f)] private float _crossingEpsilon = 0.015f;

        [Header("Rendering")]
        [SerializeField, Range(256, 2048)] private int _perEyeResolution = 1024;
        [SerializeField, Min(1f)] private float _maximumRenderDistance = 80f;
        [SerializeField, Min(0f)] private float _nearClipOffset = 0.025f;
        [SerializeField] private int _portalSurfaceLayer = 30;

        private readonly Dictionary<PortalTraveller, TravellerState> _travellerStates =
            new Dictionary<PortalTraveller, TravellerState>();

        private readonly List<PortalTraveller> _removeBuffer = new List<PortalTraveller>();

        private Camera _portalCamera;
        private RenderTexture _leftEyeTexture;
        private RenderTexture _rightEyeTexture;
        private Material _runtimeMaterial;

        private static readonly int LeftTextureId = Shader.PropertyToID("_LeftTex");
        private static readonly int RightTextureId = Shader.PropertyToID("_RightTex");

        public Portal Target { get { return _target; } }
        public float ExitOffset { get { return _exitOffset; } }

        public void Configure(Portal target, Renderer surfaceRenderer, int portalSurfaceLayer)
        {
            _target = target;
            _surfaceRenderer = surfaceRenderer;
            _portalSurfaceLayer = portalSurfaceLayer;
        }

        private void Awake()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(_openingWidth, _openingHeight, 0.5f);

            EnsureRenderResources();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _travellerStates.Clear();
        }

        private void OnDestroy()
        {
            ReleaseRenderResources();
        }

        private void OnValidate()
        {
            var trigger = GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.size = new Vector3(_openingWidth, _openingHeight, 0.5f);
            }
        }

        private void LateUpdate()
        {
            TrackTravellerCrossings();
        }

        private void TrackTravellerCrossings()
        {
            _removeBuffer.Clear();

            // Update every active traveller, not only trigger contacts. This means a fast
            // player cannot skip the portal between physics frames.
            for (int i = 0; i < PortalTraveller.ActiveTravellers.Count; ++i)
            {
                PortalTraveller traveller = PortalTraveller.ActiveTravellers[i];
                if (traveller == null || !traveller.isActiveAndEnabled)
                    continue;

                Vector3 currentHeadPosition = traveller.Head.position;
                float currentSide = SignedDistanceToPlane(currentHeadPosition);

                TravellerState state;
                if (!_travellerStates.TryGetValue(traveller, out state))
                {
                    state = new TravellerState
                    {
                        PreviousHeadPosition = currentHeadPosition,
                        PreviousSide = currentSide,
                        Initialized = true
                    };
                    _travellerStates.Add(traveller, state);
                    continue;
                }

                if (traveller.CanTeleport &&
                    state.PreviousSide > _crossingEpsilon &&
                    currentSide <= -_crossingEpsilon)
                {
                    float denominator = state.PreviousSide - currentSide;
                    float t = denominator > 0.00001f
                        ? Mathf.Clamp01(state.PreviousSide / denominator)
                        : 1f;

                    Vector3 crossingPoint =
                        Vector3.Lerp(state.PreviousHeadPosition, currentHeadPosition, t);

                    if (IsInsideOpening(crossingPoint))
                    {
                        traveller.TeleportThrough(this);
                        currentHeadPosition = traveller.Head.position;
                        currentSide = SignedDistanceToPlane(currentHeadPosition);
                    }
                }

                state.PreviousHeadPosition = currentHeadPosition;
                state.PreviousSide = currentSide;
            }

            foreach (var pair in _travellerStates)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                    _removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; ++i)
                _travellerStates.Remove(_removeBuffer[i]);
        }

        private float SignedDistanceToPlane(Vector3 worldPoint)
        {
            return Vector3.Dot(transform.forward, worldPoint - transform.position);
        }

        private bool IsInsideOpening(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= _openingWidth * 0.5f &&
                   Mathf.Abs(local.y) <= _openingHeight * 0.5f;
        }

        public Vector3 TransformPointToTarget(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            local = HalfTurn * local;
            return _target.transform.TransformPoint(local);
        }

        public Vector3 TransformDirectionToTarget(Vector3 worldDirection)
        {
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            local = HalfTurn * local;
            return _target.transform.TransformDirection(local);
        }

        public Quaternion TransformRotationToTarget(Quaternion worldRotation)
        {
            return _target.transform.rotation *
                   HalfTurn *
                   Quaternion.Inverse(transform.rotation) *
                   worldRotation;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera sourceCamera)
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

            EnsureRenderResources();
            if (_portalCamera == null || _runtimeMaterial == null)
                return;

            s_IsRenderingPortal = true;
            try
            {
                CopyCameraSettings(sourceCamera);

                if (sourceCamera.stereoEnabled)
                {
                    RenderEye(context, sourceCamera, Camera.StereoscopicEye.Left, _leftEyeTexture);
                    RenderEye(context, sourceCamera, Camera.StereoscopicEye.Right, _rightEyeTexture);
                    _runtimeMaterial.SetTexture(LeftTextureId, _leftEyeTexture);
                    _runtimeMaterial.SetTexture(RightTextureId, _rightEyeTexture);
                }
                else
                {
                    RenderEye(context, sourceCamera, Camera.StereoscopicEye.Left, _leftEyeTexture);
                    _runtimeMaterial.SetTexture(LeftTextureId, _leftEyeTexture);
                    _runtimeMaterial.SetTexture(RightTextureId, _leftEyeTexture);
                }
            }
            finally
            {
                _portalCamera.targetTexture = null;
                s_IsRenderingPortal = false;
            }
        }

        private bool ShouldRender(Camera sourceCamera)
        {
            float sqrDistance =
                (sourceCamera.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > _maximumRenderDistance * _maximumRenderDistance)
                return false;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(sourceCamera);
            return GeometryUtility.TestPlanesAABB(planes, _surfaceRenderer.bounds);
        }

        private void CopyCameraSettings(Camera source)
        {
            _portalCamera.clearFlags = source.clearFlags;
            _portalCamera.backgroundColor = source.backgroundColor;
            _portalCamera.allowHDR = source.allowHDR;
            _portalCamera.allowMSAA = false;
            _portalCamera.useOcclusionCulling = source.useOcclusionCulling;
            _portalCamera.nearClipPlane = Mathf.Max(0.01f, source.nearClipPlane);
            _portalCamera.farClipPlane = source.farClipPlane;
            _portalCamera.cullingMask = source.cullingMask & ~(1 << _portalSurfaceLayer);
        }

        private void RenderEye(
            ScriptableRenderContext context,
            Camera sourceCamera,
            Camera.StereoscopicEye eye,
            RenderTexture destination)
        {
            Vector3 eyePosition;
            Quaternion eyeRotation;
            Matrix4x4 projection;

            if (sourceCamera.stereoEnabled)
            {
                Matrix4x4 eyeToWorld = sourceCamera.GetStereoViewMatrix(eye).inverse;
                eyePosition = eyeToWorld.GetColumn(3);
                eyeRotation = eyeToWorld.rotation;
                projection = sourceCamera.GetStereoProjectionMatrix(eye);
            }
            else
            {
                eyePosition = sourceCamera.transform.position;
                eyeRotation = sourceCamera.transform.rotation;
                projection = sourceCamera.projectionMatrix;
            }

            _portalCamera.transform.SetPositionAndRotation(
                TransformPointToTarget(eyePosition),
                TransformRotationToTarget(eyeRotation));

            _portalCamera.targetTexture = destination;
            _portalCamera.projectionMatrix = projection;

            Vector4 clipPlane = BuildCameraSpaceClipPlane();
            _portalCamera.projectionMatrix = _portalCamera.CalculateObliqueMatrix(clipPlane);

            UniversalRenderPipeline.RenderSingleCamera(context, _portalCamera);
        }

        private Vector4 BuildCameraSpaceClipPlane()
        {
            Vector3 planePosition =
                _target.transform.position + _target.transform.forward * _nearClipOffset;

            Vector3 portalToCamera = _portalCamera.transform.position - planePosition;
            float side = Mathf.Sign(Vector3.Dot(_target.transform.forward, portalToCamera));
            if (Mathf.Approximately(side, 0f))
                side = 1f;

            Vector3 planeNormal = _target.transform.forward * side;
            Matrix4x4 worldToCamera = _portalCamera.worldToCameraMatrix;
            Vector3 cameraSpacePosition = worldToCamera.MultiplyPoint(planePosition);
            Vector3 cameraSpaceNormal =
                worldToCamera.MultiplyVector(planeNormal).normalized;

            return new Vector4(
                cameraSpaceNormal.x,
                cameraSpaceNormal.y,
                cameraSpaceNormal.z,
                -Vector3.Dot(cameraSpacePosition, cameraSpaceNormal));
        }

        private void EnsureRenderResources()
        {
            if (_surfaceRenderer != null && _runtimeMaterial == null)
            {
                _runtimeMaterial = new Material(_surfaceRenderer.sharedMaterial)
                {
                    name = _surfaceRenderer.sharedMaterial.name + " (Runtime)"
                };
                _surfaceRenderer.material = _runtimeMaterial;
            }

            if (_portalCamera == null)
            {
                var cameraObject = new GameObject(name + " Portal Camera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                _portalCamera = cameraObject.AddComponent<Camera>();
                _portalCamera.enabled = false;
                _portalCamera.stereoTargetEye = StereoTargetEyeMask.None;

                var additionalData =
                    cameraObject.GetComponent<UniversalAdditionalCameraData>();
                if (additionalData == null)
                    additionalData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

                additionalData.renderPostProcessing = false;
                additionalData.requiresColorOption = CameraOverrideOption.Off;
                additionalData.requiresDepthOption = CameraOverrideOption.Off;
            }

            if (_leftEyeTexture == null ||
                _leftEyeTexture.width != _perEyeResolution ||
                _leftEyeTexture.height != _perEyeResolution)
            {
                ReleaseRenderTextures();
                _leftEyeTexture = CreateEyeTexture(name + " Left Eye");
                _rightEyeTexture = CreateEyeTexture(name + " Right Eye");
            }
        }

        private RenderTexture CreateEyeTexture(string textureName)
        {
            var texture = new RenderTexture(
                _perEyeResolution,
                _perEyeResolution,
                24,
                RenderTextureFormat.DefaultHDR)
            {
                name = textureName,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private void ReleaseRenderResources()
        {
            ReleaseRenderTextures();

            if (_portalCamera != null)
                Destroy(_portalCamera.gameObject);

            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
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
