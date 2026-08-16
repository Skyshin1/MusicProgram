using Crest;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MusicProgram.CrestURP
{
    /// <summary>
    /// URP planar reflection renderer. A mono center-eye reflection is shared by
    /// both XR eyes to keep the cost suitable for VR.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CrestURPPlanarReflection : MonoBehaviour
    {
        static readonly int ReflectionTextureId = Shader.PropertyToID("_CrestURPPlanarReflectionTexture");
        static readonly int ReflectionVpId = Shader.PropertyToID("_CrestURPPlanarReflectionVP");
        static readonly int ReflectionEnabledId = Shader.PropertyToID("_CrestURPPlanarReflectionEnabled");
        static readonly int ReflectionStrengthId = Shader.PropertyToID("_CrestURPPlanarReflectionStrength");
        static readonly int ReflectionDistortionId = Shader.PropertyToID("_CrestURPPlanarReflectionDistortion");
        static readonly int ReflectionMipStrengthId = Shader.PropertyToID("_CrestURPPlanarReflectionMipStrength");
        static readonly int ReflectionRenderingId = Shader.PropertyToID("_CrestURPPlanarReflectionRendering");

        [Tooltip("Layers visible in the planar reflection.")]
        public LayerMask reflectionLayers = ~0;
        [UnityEngine.Range(0.1f, 1f)] public float resolutionScale = 0.5f;
        [UnityEngine.Range(128, 2048)] public int maximumResolution = 1024;
        [UnityEngine.Range(0.001f, 0.5f)] public float clipPlaneOffset = 0.06f;
        [UnityEngine.Range(0f, 1f)] public float reflectionStrength = 0.72f;
        [UnityEngine.Range(0f, 0.12f)] public float waveNormalDistortion = 0.024f;
        [UnityEngine.Range(0f, 8f)] public float roughnessMipStrength = 4f;
        public bool generateRoughnessMipmaps = true;
        [UnityEngine.Range(1, 8)] public int updateEveryFrames = 1;
        public float reflectionFarClip = 700f;
        public bool renderShadows = true;
        public bool renderBelowWater;

        Camera _sourceCamera;
        Camera _reflectionCamera;
        UniversalAdditionalCameraData _reflectionCameraData;
        RenderTexture _reflectionTexture;
        int _lastRenderedFrame = -1000;
        Vector3 _lastSourcePosition;
        Quaternion _lastSourceRotation;
        static bool s_RenderingReflection;

        /// <summary>True after URP has produced a valid reflection target at least once.</summary>
        public bool HasRenderedTexture => _reflectionTexture != null && _reflectionTexture.IsCreated() &&
                                          _lastRenderedFrame >= 0;

        void OnEnable()
        {
            _sourceCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            ReleaseResources();
            Shader.SetGlobalFloat(ReflectionEnabledId, 0f);
            Shader.SetGlobalFloat(ReflectionRenderingId, 0f);
        }

        void OnDestroy() => ReleaseResources();

        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (s_RenderingReflection || camera != _sourceCamera || !isActiveAndEnabled) return;
            var ocean = OceanRenderer.Instance;
            if (ocean == null || _sourceCamera == null) return;
            if (!renderBelowWater && _sourceCamera.transform.position.y < ocean.SeaLevel - 0.1f)
            {
                Shader.SetGlobalFloat(ReflectionEnabledId, 0f);
                return;
            }
            var poseChangedThisFrame = Time.frameCount == _lastRenderedFrame &&
                                       ((_sourceCamera.transform.position - _lastSourcePosition).sqrMagnitude > 0.000001f ||
                                        Quaternion.Angle(_sourceCamera.transform.rotation, _lastSourceRotation) > 0.01f);
            if (_lastRenderedFrame >= 0 &&
                Time.frameCount - _lastRenderedFrame < Mathf.Max(1, updateEveryFrames) &&
                !poseChangedThisFrame) return;

            EnsureResources();
            if (_reflectionCamera == null || _reflectionTexture == null) return;

            ConfigureReflectionCamera(ocean.SeaLevel);
            var previousInvertCulling = GL.invertCulling;
            s_RenderingReflection = true;
            Shader.SetGlobalFloat(ReflectionRenderingId, 1f);
            try
            {
                GL.invertCulling = !previousInvertCulling;
#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
#pragma warning restore CS0618
                if (generateRoughnessMipmaps && _reflectionTexture.useMipMap)
                {
                    _reflectionTexture.GenerateMips();
                }
                _lastRenderedFrame = Time.frameCount;
                _lastSourcePosition = _sourceCamera.transform.position;
                _lastSourceRotation = _sourceCamera.transform.rotation;
            }
            finally
            {
                GL.invertCulling = previousInvertCulling;
                Shader.SetGlobalFloat(ReflectionRenderingId, 0f);
                s_RenderingReflection = false;
            }

            var gpuProjection = GL.GetGPUProjectionMatrix(_reflectionCamera.projectionMatrix, true);
            Shader.SetGlobalTexture(ReflectionTextureId, _reflectionTexture);
            Shader.SetGlobalMatrix(ReflectionVpId, gpuProjection * _reflectionCamera.worldToCameraMatrix);
            Shader.SetGlobalFloat(ReflectionStrengthId, reflectionStrength);
            Shader.SetGlobalFloat(ReflectionDistortionId, waveNormalDistortion);
            Shader.SetGlobalFloat(ReflectionMipStrengthId, generateRoughnessMipmaps ? roughnessMipStrength : 0f);
            Shader.SetGlobalFloat(ReflectionEnabledId, 1f);
        }

        void EnsureResources()
        {
            if (_reflectionCamera == null)
            {
                var reflectionObject = new GameObject("Crest URP Planar Reflection Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _reflectionCamera = reflectionObject.AddComponent<Camera>();
                _reflectionCamera.enabled = false;
                _reflectionCamera.cameraType = CameraType.Reflection;
                _reflectionCameraData = reflectionObject.AddComponent<UniversalAdditionalCameraData>();
                _reflectionCameraData.renderPostProcessing = false;
                _reflectionCameraData.requiresDepthTexture = false;
                _reflectionCameraData.requiresColorTexture = false;
                _reflectionCameraData.renderShadows = renderShadows;
                _reflectionCameraData.allowXRRendering = false;
            }

            var sourceWidth = _sourceCamera.targetTexture != null
                ? _sourceCamera.targetTexture.width
                : Mathf.Max(1, _sourceCamera.pixelWidth);
            var sourceHeight = _sourceCamera.targetTexture != null
                ? _sourceCamera.targetTexture.height
                : Mathf.Max(1, _sourceCamera.pixelHeight);
            var width = Mathf.Clamp(Mathf.RoundToInt(sourceWidth * resolutionScale), 128, maximumResolution);
            var height = Mathf.Clamp(Mathf.RoundToInt(sourceHeight * resolutionScale), 128, maximumResolution);
            if (_reflectionTexture != null && _reflectionTexture.width == width && _reflectionTexture.height == height &&
                _reflectionTexture.useMipMap == generateRoughnessMipmaps) return;

            ReleaseTexture();
            _reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "Crest URP Planar Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = generateRoughnessMipmaps,
                autoGenerateMips = false
            };
            _reflectionTexture.Create();
        }

        void ConfigureReflectionCamera(float waterLevel)
        {
            _reflectionCamera.CopyFrom(_sourceCamera);
            _reflectionCamera.enabled = false;
            _reflectionCamera.cameraType = CameraType.Reflection;
            _reflectionCamera.targetTexture = _reflectionTexture;
            _reflectionCamera.cullingMask = _sourceCamera.cullingMask & reflectionLayers;
            _reflectionCamera.farClipPlane = Mathf.Min(_sourceCamera.farClipPlane, reflectionFarClip);
            _reflectionCamera.allowMSAA = false;
            _reflectionCamera.forceIntoRenderTexture = true;
            _reflectionCamera.useOcclusionCulling = false;
            _reflectionCameraData.renderPostProcessing = false;
            _reflectionCameraData.requiresDepthTexture = false;
            _reflectionCameraData.requiresColorTexture = false;
            _reflectionCameraData.renderShadows = renderShadows;
            _reflectionCameraData.allowXRRendering = false;

            var planePosition = new Vector3(0f, waterLevel, 0f);
            var planeNormal = Vector3.up;
            var reflectionPlane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z,
                -Vector3.Dot(planeNormal, planePosition) - clipPlaneOffset);
            var reflectionMatrix = CalculateReflectionMatrix(reflectionPlane);
            var reflectedPosition = reflectionMatrix.MultiplyPoint(_sourceCamera.transform.position);

            _reflectionCamera.worldToCameraMatrix = _sourceCamera.worldToCameraMatrix * reflectionMatrix;
            var clipPlane = CameraSpacePlane(_reflectionCamera, planePosition, planeNormal, 1f, clipPlaneOffset);
            _reflectionCamera.projectionMatrix = _reflectionCamera.CalculateObliqueMatrix(clipPlane);
            _reflectionCamera.cullingMatrix = _reflectionCamera.projectionMatrix * _reflectionCamera.worldToCameraMatrix;

            var reflectedForward = reflectionMatrix.MultiplyVector(_sourceCamera.transform.forward);
            var reflectedUp = reflectionMatrix.MultiplyVector(_sourceCamera.transform.up);
            _reflectionCamera.transform.SetPositionAndRotation(reflectedPosition,
                Quaternion.LookRotation(reflectedForward, reflectedUp));
        }

        static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1f - 2f * plane.x * plane.x;
            matrix.m01 = -2f * plane.x * plane.y;
            matrix.m02 = -2f * plane.x * plane.z;
            matrix.m03 = -2f * plane.w * plane.x;
            matrix.m10 = -2f * plane.y * plane.x;
            matrix.m11 = 1f - 2f * plane.y * plane.y;
            matrix.m12 = -2f * plane.y * plane.z;
            matrix.m13 = -2f * plane.w * plane.y;
            matrix.m20 = -2f * plane.z * plane.x;
            matrix.m21 = -2f * plane.z * plane.y;
            matrix.m22 = 1f - 2f * plane.z * plane.z;
            matrix.m23 = -2f * plane.w * plane.z;
            return matrix;
        }

        static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign, float offset)
        {
            var offsetPosition = position + normal * offset;
            var matrix = camera.worldToCameraMatrix;
            var cameraPosition = matrix.MultiplyPoint(offsetPosition);
            var cameraNormal = matrix.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
                -Vector3.Dot(cameraPosition, cameraNormal));
        }

        void ReleaseResources()
        {
            ReleaseTexture();
            if (_reflectionCamera == null) return;
            if (Application.isPlaying) Destroy(_reflectionCamera.gameObject);
            else DestroyImmediate(_reflectionCamera.gameObject);
            _reflectionCamera = null;
            _reflectionCameraData = null;
        }

        void ReleaseTexture()
        {
            if (_reflectionTexture == null) return;
            _reflectionTexture.Release();
            if (Application.isPlaying) Destroy(_reflectionTexture);
            else DestroyImmediate(_reflectionTexture);
            _reflectionTexture = null;
        }
    }
}
