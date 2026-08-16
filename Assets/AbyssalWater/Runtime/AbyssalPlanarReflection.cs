using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MusicProgram.AbyssalWater
{
    /// <summary>
    /// Renders one center-eye planar reflection and shares it between both XR
    /// eyes. This avoids doubling the largest optional water cost in VR.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class AbyssalPlanarReflection : MonoBehaviour
    {
        public LayerMask reflectionLayers = ~0;
        [Range(0.1f, 1f)] public float resolutionScale = 0.5f;
        [Range(128, 2048)] public int maximumResolution = 1024;
        [Range(0.001f, 0.5f)] public float clipPlaneOffset = 0.06f;
        [Range(1, 8)] public int updateEveryFrames = 1;
        [Min(10f)] public float reflectionFarClip = 700f;
        public bool renderShadows = true;
        public bool generateRoughnessMipmaps = true;
        public bool useProfileQuality = true;

        Camera _sourceCamera;
        Camera _reflectionCamera;
        UniversalAdditionalCameraData _reflectionCameraData;
        RenderTexture _reflectionTexture;
        int _lastRenderedFrame = -1000;
        static bool s_RenderingReflection;

        void OnEnable()
        {
            _sourceCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            ReleaseResources();
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.PlanarReflectionEnabled, 0f);
        }

        void OnDestroy() => ReleaseResources();

        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (s_RenderingReflection || camera != _sourceCamera || !isActiveAndEnabled) return;
            var water = AbyssalWaterSystem.Active;
            if (water == null || water.profile == null || _sourceCamera == null) return;
            if (water.IsUnderwater(_sourceCamera.transform.position, -0.05f))
            {
                Shader.SetGlobalFloat(AbyssalWaterShaderIds.PlanarReflectionEnabled, 0f);
                return;
            }

            var frameInterval = useProfileQuality
                ? water.profile.quality == AbyssalWaterQuality.PcVrHigh ? 1 : 2
                : Mathf.Max(1, updateEveryFrames);
            if (_lastRenderedFrame >= 0 && Time.frameCount - _lastRenderedFrame < frameInterval) return;

            EnsureResources(water.profile);
            if (_reflectionCamera == null || _reflectionTexture == null) return;
            ConfigureReflectionCamera(water.waterLevel);

            var previousInvertCulling = GL.invertCulling;
            s_RenderingReflection = true;
            try
            {
                GL.invertCulling = !previousInvertCulling;
#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
#pragma warning restore CS0618
                if (generateRoughnessMipmaps && _reflectionTexture.useMipMap)
                    _reflectionTexture.GenerateMips();
                _lastRenderedFrame = Time.frameCount;
            }
            finally
            {
                GL.invertCulling = previousInvertCulling;
                s_RenderingReflection = false;
            }

            var gpuProjection = GL.GetGPUProjectionMatrix(_reflectionCamera.projectionMatrix, true);
            Shader.SetGlobalTexture(AbyssalWaterShaderIds.PlanarReflection, _reflectionTexture);
            Shader.SetGlobalMatrix(AbyssalWaterShaderIds.PlanarReflectionVp,
                gpuProjection * _reflectionCamera.worldToCameraMatrix);
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.PlanarReflectionEnabled, 1f);
        }

        void EnsureResources(AbyssalWaterProfile profile)
        {
            if (_reflectionCamera == null)
            {
                var go = new GameObject("Abyssal Planar Reflection Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _reflectionCamera = go.AddComponent<Camera>();
                _reflectionCamera.enabled = false;
                _reflectionCamera.cameraType = CameraType.Reflection;
                _reflectionCameraData = go.AddComponent<UniversalAdditionalCameraData>();
                _reflectionCameraData.renderPostProcessing = false;
                _reflectionCameraData.requiresDepthTexture = false;
                _reflectionCameraData.requiresColorTexture = false;
                _reflectionCameraData.allowXRRendering = false;
            }

            var profileScale = profile.quality switch
            {
                AbyssalWaterQuality.PcVrHigh => 0.5f,
                AbyssalWaterQuality.VrBalanced => 0.35f,
                _ => 0.25f
            };
            var scale = useProfileQuality ? profileScale : resolutionScale;
            var cap = useProfileQuality && profile.quality == AbyssalWaterQuality.QuestStandalone
                ? Mathf.Min(512, maximumResolution)
                : maximumResolution;
            var sourceWidth = _sourceCamera.targetTexture != null
                ? _sourceCamera.targetTexture.width
                : Mathf.Max(1, _sourceCamera.pixelWidth);
            var sourceHeight = _sourceCamera.targetTexture != null
                ? _sourceCamera.targetTexture.height
                : Mathf.Max(1, _sourceCamera.pixelHeight);
            var width = Mathf.Clamp(Mathf.RoundToInt(sourceWidth * scale), 128, cap);
            var height = Mathf.Clamp(Mathf.RoundToInt(sourceHeight * scale), 128, cap);
            if (_reflectionTexture != null && _reflectionTexture.width == width &&
                _reflectionTexture.height == height && _reflectionTexture.useMipMap == generateRoughnessMipmaps)
                return;

            ReleaseTexture();
            _reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "Abyssal Planar Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = generateRoughnessMipmaps,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear
            };
            _reflectionTexture.Create();
        }

        void ConfigureReflectionCamera(float level)
        {
            _reflectionCamera.CopyFrom(_sourceCamera);
            _reflectionCamera.enabled = false;
            _reflectionCamera.cameraType = CameraType.Reflection;
            _reflectionCamera.targetTexture = _reflectionTexture;
            _reflectionCamera.cullingMask = _sourceCamera.cullingMask & reflectionLayers & ~(1 << 4);
            _reflectionCamera.farClipPlane = Mathf.Min(_sourceCamera.farClipPlane, reflectionFarClip);
            _reflectionCamera.allowMSAA = false;
            _reflectionCamera.forceIntoRenderTexture = true;
            _reflectionCamera.useOcclusionCulling = false;
            _reflectionCameraData.renderPostProcessing = false;
            _reflectionCameraData.requiresDepthTexture = false;
            _reflectionCameraData.requiresColorTexture = false;
            _reflectionCameraData.renderShadows = renderShadows;
            _reflectionCameraData.allowXRRendering = false;

            var planePosition = new Vector3(0f, level, 0f);
            var planeNormal = Vector3.up;
            var plane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z,
                -Vector3.Dot(planeNormal, planePosition) - clipPlaneOffset);
            var reflection = CalculateReflectionMatrix(plane);
            _reflectionCamera.worldToCameraMatrix = _sourceCamera.worldToCameraMatrix * reflection;
            var clipPlane = CameraSpacePlane(_reflectionCamera, planePosition, planeNormal, 1f, clipPlaneOffset);
            _reflectionCamera.projectionMatrix = _reflectionCamera.CalculateObliqueMatrix(clipPlane);
            _reflectionCamera.cullingMatrix = _reflectionCamera.projectionMatrix * _reflectionCamera.worldToCameraMatrix;

            var reflectedPosition = reflection.MultiplyPoint(_sourceCamera.transform.position);
            var reflectedForward = reflection.MultiplyVector(_sourceCamera.transform.forward);
            var reflectedUp = reflection.MultiplyVector(_sourceCamera.transform.up);
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

        static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal,
            float sideSign, float offset)
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
            SafeDestroy(_reflectionCamera.gameObject);
            _reflectionCamera = null;
            _reflectionCameraData = null;
        }

        void ReleaseTexture()
        {
            if (_reflectionTexture == null) return;
            _reflectionTexture.Release();
            SafeDestroy(_reflectionTexture);
            _reflectionTexture = null;
        }

        static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
