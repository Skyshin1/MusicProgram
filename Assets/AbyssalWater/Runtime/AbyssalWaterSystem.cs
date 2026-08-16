using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MusicProgram.AbyssalWater
{
    /// <summary>
    /// Owns the camera-relative clipmap ocean, shared Gerstner spectrum and the
    /// near-field height-field simulation used by interactors.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AbyssalWaterSystem : MonoBehaviour
    {
        public static AbyssalWaterSystem Active { get; private set; }

        [Header("Required")]
        public AbyssalWaterProfile profile;
        public Material waterMaterial;
        public ComputeShader dynamicWaveCompute;

        [Header("Scene")]
        public Transform viewer;
        public float waterLevel;
        public bool followMainCamera = true;
        public bool renderInSceneView = true;

        [Header("Diagnostics")]
        public bool showLodBounds;
        public bool showDynamicWaveArea;

        readonly List<Transform> _lodTransforms = new List<Transform>();
        readonly List<Mesh> _lodMeshes = new List<Mesh>();
        readonly List<Vector4> _pendingImpulses = new List<Vector4>();

        RenderTexture _dynamicPrevious;
        RenderTexture _dynamicCurrent;
        RenderTexture _dynamicNext;
        ComputeBuffer _impulseBuffer;
        Vector4[] _impulseUpload = new Vector4[32];
        Vector2 _dynamicCenter;
        int _dynamicResolution;
        int _clearKernel = -1;
        int _stepKernel = -1;
        int _shiftKernel = -1;
        int _lastMeshHash;
        float _simulationAccumulator;
        float _waterTime;

        static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        static readonly int TimeStepId = Shader.PropertyToID("_TimeStep");
        static readonly int WaveCoefficientId = Shader.PropertyToID("_WaveCoefficient");
        static readonly int DampingId = Shader.PropertyToID("_Damping");
        static readonly int ImpulseCountId = Shader.PropertyToID("_ImpulseCount");
        static readonly int ImpulsesId = Shader.PropertyToID("_Impulses");
        static readonly int PreviousId = Shader.PropertyToID("_Previous");
        static readonly int CurrentId = Shader.PropertyToID("_Current");
        static readonly int NextId = Shader.PropertyToID("_Next");
        static readonly int ClearTargetId = Shader.PropertyToID("_ClearTarget");
        static readonly int ShiftSourceId = Shader.PropertyToID("_ShiftSource");
        static readonly int ShiftTargetId = Shader.PropertyToID("_ShiftTarget");
        static readonly int ShiftPixelsId = Shader.PropertyToID("_ShiftPixels");
        static readonly int LodDataId = Shader.PropertyToID("_AbyssalLodData");

        public float WaterTime => _waterTime;
        public bool DynamicWavesAvailable => _dynamicCurrent != null && _dynamicCurrent.IsCreated();

        void OnEnable()
        {
            Active = this;
            ResolveViewer();
            RebuildOcean();
            EnsureDynamicResources();
            PushGlobals();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            ReleaseGeneratedOcean();
            ReleaseDynamicResources();
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.PlanarReflectionEnabled, 0f);
            Shader.SetGlobalTexture(AbyssalWaterShaderIds.DynamicCurrent, Texture2D.blackTexture);
        }

        void OnDestroy()
        {
            ReleaseGeneratedOcean();
            ReleaseDynamicResources();
        }

        void OnValidate()
        {
            waterLevel = transform.position.y;
            _lastMeshHash = 0;
            if (isActiveAndEnabled)
            {
                RebuildOcean();
                EnsureDynamicResources();
                PushGlobals();
            }
        }

        void Update()
        {
            if (profile == null || waterMaterial == null) return;
            ResolveViewer();

            var expectedHash = CalculateMeshHash();
            if (_lodTransforms.Count != profile.lodLevels || expectedHash != _lastMeshHash)
                RebuildOcean();

            _waterTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            UpdateOceanPosition();
            EnsureDynamicResources();
            UpdateDynamicWaves();
            PushGlobals();
        }

        public void EnqueueImpulse(Vector3 worldPosition, float radius, float strength)
        {
            if (profile == null || !profile.enableDynamicWaves || radius <= 0f || Mathf.Approximately(strength, 0f))
                return;
            if (_pendingImpulses.Count >= Mathf.Clamp(profile.maximumImpulsesPerStep, 1, 64)) return;
            _pendingImpulses.Add(new Vector4(worldPosition.x, worldPosition.z, radius, strength));
        }

        public void SampleSurface(Vector3 worldPosition, out Vector3 displacedPosition,
            out Vector3 normal, out Vector3 velocity)
        {
            if (profile == null)
            {
                displacedPosition = new Vector3(worldPosition.x, waterLevel, worldPosition.z);
                normal = Vector3.up;
                velocity = Vector3.zero;
                return;
            }
            profile.SampleSurface(worldPosition, _waterTime, waterLevel,
                out displacedPosition, out normal, out velocity);
        }

        public float GetWaterHeight(Vector3 worldPosition)
        {
            SampleSurface(worldPosition, out var position, out _, out _);
            return position.y;
        }

        public bool IsUnderwater(Vector3 worldPosition, float tolerance = 0f)
            => worldPosition.y < GetWaterHeight(worldPosition) + tolerance;

        void ResolveViewer()
        {
            if (viewer != null || !followMainCamera) return;
            var camera = Camera.main;
            if (camera != null) viewer = camera.transform;
        }

        void UpdateOceanPosition()
        {
            var target = viewer != null ? viewer.position : transform.position;
            var finestCell = profile.baseLodSize / Mathf.Max(4, profile.verticesPerLevel);
            var snappedX = Mathf.Floor(target.x / finestCell) * finestCell;
            var snappedZ = Mathf.Floor(target.z / finestCell) * finestCell;
            for (var i = 0; i < _lodTransforms.Count; i++)
                _lodTransforms[i].position = new Vector3(snappedX, waterLevel - i * 0.012f, snappedZ);
        }

        void PushGlobals()
        {
            if (profile == null) return;
            profile.ApplyGlobals(_waterTime);
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.WaterLevel, waterLevel);
            Shader.SetGlobalVector(AbyssalWaterShaderIds.DynamicCenterSize,
                new Vector4(_dynamicCenter.x, _dynamicCenter.y,
                    Mathf.Max(1f, profile.dynamicWorldSize), profile.enableDynamicWaves ? 1f : 0f));
            Shader.SetGlobalVector(AbyssalWaterShaderIds.DynamicParameters,
                new Vector4(profile.dynamicDisplacement,
                    DynamicWavesAvailable ? 1f / _dynamicCurrent.width : 0f,
                    profile.contactFoamStrength, 0f));
            Shader.SetGlobalTexture(AbyssalWaterShaderIds.DynamicCurrent,
                DynamicWavesAvailable ? _dynamicCurrent : Texture2D.blackTexture);
            Shader.SetGlobalTexture(AbyssalWaterShaderIds.DynamicPrevious,
                _dynamicPrevious != null ? _dynamicPrevious : Texture2D.blackTexture);
        }

        void RebuildOcean()
        {
            ReleaseGeneratedOcean();
            if (profile == null || waterMaterial == null) return;

            var root = new GameObject("Abyssal Ocean LOD (Generated)");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(transform, false);

            var levels = Mathf.Clamp(profile.lodLevels, 2, 8);
            for (var level = 0; level < levels; level++)
            {
                var go = new GameObject($"LOD {level}");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(root.transform, false);
                go.layer = 4;

                var mesh = BuildLodMesh(level);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = waterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.sortingOrder = -level;
                var size = profile.baseLodSize * Mathf.Pow(2f, level);
                var segments = Mathf.Clamp(profile.verticesPerLevel, 16, 160);
                if ((segments & 1) != 0) segments++;
                var step = size / segments;
                var lodData = new MaterialPropertyBlock();
                lodData.SetVector(LodDataId, new Vector4(
                    level == 0 ? 0f : size * 0.25f,
                    size * 0.5f,
                    step * 2f,
                    step * 4f));
                renderer.SetPropertyBlock(lodData);

                _lodTransforms.Add(go.transform);
                _lodMeshes.Add(mesh);
            }

            _lastMeshHash = CalculateMeshHash();
            UpdateOceanPosition();
        }

        Mesh BuildLodMesh(int level)
        {
            var segments = Mathf.Clamp(profile.verticesPerLevel, 16, 160);
            if ((segments & 1) != 0) segments++;
            var size = profile.baseLodSize * Mathf.Pow(2f, level);
            var half = size * 0.5f;
            var innerHalf = level == 0 ? -1f : size * 0.25f;
            var step = size / segments;
            var rowLength = segments + 1;
            var vertices = new List<Vector3>(rowLength * rowLength);
            var uv = new List<Vector2>(rowLength * rowLength);
            var indices = new List<int>(segments * segments * 6);

            // One indexed grid per ring keeps the ocean compact and gives the
            // GPU shared vertices along every cell and LOD boundary.
            for (var z = 0; z <= segments; z++)
            {
                var vertexZ = -half + z * step;
                for (var x = 0; x <= segments; x++)
                {
                    var vertexX = -half + x * step;
                    vertices.Add(new Vector3(vertexX, 0f, vertexZ));
                    uv.Add(new Vector2(vertexX, vertexZ));
                }
            }

            for (var z = 0; z < segments; z++)
            {
                var z0 = -half + z * step;
                var z1 = z0 + step;
                for (var x = 0; x < segments; x++)
                {
                    var x0 = -half + x * step;
                    var x1 = x0 + step;
                    var centerX = (x0 + x1) * 0.5f;
                    var centerZ = (z0 + z1) * 0.5f;
                    // One-cell underlap lets the finer ring cover the coarse
                    // T-junction. Per-ring sorting and a tiny vertical bias avoid
                    // transparency z-fighting while keeping the seam watertight.
                    if (level > 0 && Mathf.Max(Mathf.Abs(centerX), Mathf.Abs(centerZ)) < innerHalf - step * 0.75f)
                        continue;

                    var start = z * rowLength + x;
                    indices.Add(start);
                    indices.Add(start + rowLength + 1);
                    indices.Add(start + 1);
                    indices.Add(start);
                    indices.Add(start + rowLength);
                    indices.Add(start + rowLength + 1);
                }
            }

            if (profile.skirtDepth > 0f)
            {
                var seamDepth = level == profile.lodLevels - 1
                    ? profile.skirtDepth
                    : Mathf.Min(profile.skirtDepth, Mathf.Max(0.2f, profile.waveHeight * 0.4f));
                AddOuterSkirt(vertices, uv, indices, half, step, seamDepth);
            }

            var mesh = new Mesh
            {
                name = $"Abyssal Water LOD {level}",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0, true);
            var horizontalMargin = Mathf.Max(2f, profile.waveHeight * profile.choppiness * 2f);
            var verticalMargin = Mathf.Max(2f,
                profile.waveHeight * 2f + profile.dynamicDisplacement + profile.skirtDepth);
            mesh.bounds = new Bounds(new Vector3(0f, -profile.skirtDepth * 0.5f, 0f),
                new Vector3(size + horizontalMargin * 2f,
                    verticalMargin * 2f + profile.skirtDepth,
                    size + horizontalMargin * 2f));
            return mesh;
        }

        static void AddOuterSkirt(List<Vector3> vertices, List<Vector2> uv, List<int> indices,
            float half, float step, float depth)
        {
            var segments = Mathf.Max(1, Mathf.RoundToInt(half * 2f / step));
            for (var edge = 0; edge < 4; edge++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var a = -half + i * step;
                    var b = a + step;
                    Vector3 p0;
                    Vector3 p1;
                    switch (edge)
                    {
                        case 0: p0 = new Vector3(a, 0f, -half); p1 = new Vector3(b, 0f, -half); break;
                        case 1: p0 = new Vector3(half, 0f, a); p1 = new Vector3(half, 0f, b); break;
                        case 2: p0 = new Vector3(-a, 0f, half); p1 = new Vector3(-b, 0f, half); break;
                        default: p0 = new Vector3(-half, 0f, -a); p1 = new Vector3(-half, 0f, -b); break;
                    }
                    var start = vertices.Count;
                    vertices.Add(p0);
                    vertices.Add(p1);
                    vertices.Add(p1 + Vector3.down * depth);
                    vertices.Add(p0 + Vector3.down * depth);
                    uv.Add(new Vector2(p0.x, p0.z));
                    uv.Add(new Vector2(p1.x, p1.z));
                    uv.Add(new Vector2(p1.x, p1.z));
                    uv.Add(new Vector2(p0.x, p0.z));
                    indices.Add(start);
                    indices.Add(start + 1);
                    indices.Add(start + 2);
                    indices.Add(start);
                    indices.Add(start + 2);
                    indices.Add(start + 3);
                }
            }
        }

        int CalculateMeshHash()
        {
            if (profile == null) return 0;
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + profile.lodLevels;
                hash = hash * 31 + profile.verticesPerLevel;
                hash = hash * 31 + profile.baseLodSize.GetHashCode();
                hash = hash * 31 + profile.skirtDepth.GetHashCode();
                hash = hash * 31 + (waterMaterial != null ? waterMaterial.GetInstanceID() : 0);
                return hash;
            }
        }

        void ReleaseGeneratedOcean()
        {
            if (_lodTransforms.Count > 0)
            {
                var root = _lodTransforms[0] != null ? _lodTransforms[0].parent : null;
                if (root != null) SafeDestroy(root.gameObject);
            }
            foreach (var mesh in _lodMeshes)
                if (mesh != null) SafeDestroy(mesh);
            _lodTransforms.Clear();
            _lodMeshes.Clear();
        }

        void EnsureDynamicResources()
        {
            if (profile == null || dynamicWaveCompute == null || !profile.enableDynamicWaves)
            {
                ReleaseDynamicResources();
                return;
            }

            var resolution = profile.EffectiveDynamicResolution;
            if (_dynamicCurrent != null && _dynamicResolution == resolution) return;
            ReleaseDynamicResources();

            _dynamicResolution = resolution;
            _dynamicPrevious = CreateDynamicTexture("Abyssal Dynamic Previous", resolution);
            _dynamicCurrent = CreateDynamicTexture("Abyssal Dynamic Current", resolution);
            _dynamicNext = CreateDynamicTexture("Abyssal Dynamic Next", resolution);
            _clearKernel = dynamicWaveCompute.FindKernel("Clear");
            _stepKernel = dynamicWaveCompute.FindKernel("Step");
            _shiftKernel = dynamicWaveCompute.FindKernel("Shift");
            _impulseUpload = new Vector4[Mathf.Clamp(profile.maximumImpulsesPerStep, 1, 64)];
            _impulseBuffer = new ComputeBuffer(_impulseUpload.Length, sizeof(float) * 4);
            ClearDynamicTexture(_dynamicPrevious);
            ClearDynamicTexture(_dynamicCurrent);
            ClearDynamicTexture(_dynamicNext);
            var target = viewer != null ? viewer.position : transform.position;
            _dynamicCenter = new Vector2(target.x, target.z);
        }

        RenderTexture CreateDynamicTexture(string textureName, int resolution)
        {
            var texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RHalf)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            texture.Create();
            return texture;
        }

        void ClearDynamicTexture(RenderTexture target)
        {
            if (target == null || dynamicWaveCompute == null || _clearKernel < 0) return;
            dynamicWaveCompute.SetInt(ResolutionId, target.width);
            dynamicWaveCompute.SetTexture(_clearKernel, ClearTargetId, target);
            Dispatch(_clearKernel, target.width);
        }

        void UpdateDynamicWaves()
        {
            if (!Application.isPlaying || !DynamicWavesAvailable || profile == null) return;
            RecenterDynamicSimulation();

            var frameDt = Mathf.Min(1f / 20f, Time.deltaTime);
            _simulationAccumulator += frameDt;
            var substeps = Mathf.Clamp(profile.dynamicSubsteps, 1, 4);
            var dt = frameDt / substeps;
            for (var i = 0; i < substeps; i++)
                StepDynamicSimulation(dt, i == 0);
            _simulationAccumulator = 0f;
            _pendingImpulses.Clear();
        }

        void RecenterDynamicSimulation()
        {
            if (viewer == null) return;
            var texelSize = profile.dynamicWorldSize / _dynamicResolution;
            var desired = new Vector2(
                Mathf.Round(viewer.position.x / texelSize) * texelSize,
                Mathf.Round(viewer.position.z / texelSize) * texelSize);
            var deltaPixels = Vector2Int.RoundToInt((desired - _dynamicCenter) / texelSize);
            if (deltaPixels == Vector2Int.zero) return;

            if (Mathf.Abs(deltaPixels.x) >= _dynamicResolution || Mathf.Abs(deltaPixels.y) >= _dynamicResolution)
            {
                ClearDynamicTexture(_dynamicPrevious);
                ClearDynamicTexture(_dynamicCurrent);
                _dynamicCenter = desired;
                return;
            }

            ShiftDynamicTexture(_dynamicCurrent, _dynamicNext, deltaPixels);
            ShiftDynamicTexture(_dynamicPrevious, _dynamicCurrent, deltaPixels);
            var oldPrevious = _dynamicPrevious;
            _dynamicPrevious = _dynamicCurrent;
            _dynamicCurrent = _dynamicNext;
            _dynamicNext = oldPrevious;
            _dynamicCenter = desired;
        }

        void ShiftDynamicTexture(RenderTexture source, RenderTexture target, Vector2Int pixels)
        {
            dynamicWaveCompute.SetInt(ResolutionId, _dynamicResolution);
            dynamicWaveCompute.SetInts(ShiftPixelsId, pixels.x, pixels.y);
            dynamicWaveCompute.SetTexture(_shiftKernel, ShiftSourceId, source);
            dynamicWaveCompute.SetTexture(_shiftKernel, ShiftTargetId, target);
            Dispatch(_shiftKernel, _dynamicResolution);
        }

        void StepDynamicSimulation(float dt, bool uploadImpulses)
        {
            var count = uploadImpulses ? Mathf.Min(_pendingImpulses.Count, _impulseUpload.Length) : 0;
            for (var i = 0; i < _impulseUpload.Length; i++) _impulseUpload[i] = Vector4.zero;
            for (var i = 0; i < count; i++)
            {
                var impulse = _pendingImpulses[i];
                var uv = (new Vector2(impulse.x, impulse.y) - _dynamicCenter) / profile.dynamicWorldSize + Vector2.one * 0.5f;
                _impulseUpload[i] = new Vector4(uv.x, uv.y,
                    impulse.z / profile.dynamicWorldSize, impulse.w);
            }
            _impulseBuffer.SetData(_impulseUpload);

            var texelWorld = profile.dynamicWorldSize / _dynamicResolution;
            var coefficient = Mathf.Min(0.49f,
                profile.dynamicWaveSpeed * profile.dynamicWaveSpeed * dt * dt / (texelWorld * texelWorld));
            dynamicWaveCompute.SetInt(ResolutionId, _dynamicResolution);
            dynamicWaveCompute.SetFloat(TimeStepId, dt);
            dynamicWaveCompute.SetFloat(WaveCoefficientId, coefficient);
            dynamicWaveCompute.SetFloat(DampingId, Mathf.Max(0f, profile.dynamicDamping));
            dynamicWaveCompute.SetInt(ImpulseCountId, count);
            dynamicWaveCompute.SetBuffer(_stepKernel, ImpulsesId, _impulseBuffer);
            dynamicWaveCompute.SetTexture(_stepKernel, PreviousId, _dynamicPrevious);
            dynamicWaveCompute.SetTexture(_stepKernel, CurrentId, _dynamicCurrent);
            dynamicWaveCompute.SetTexture(_stepKernel, NextId, _dynamicNext);
            Dispatch(_stepKernel, _dynamicResolution);

            var oldPrevious = _dynamicPrevious;
            _dynamicPrevious = _dynamicCurrent;
            _dynamicCurrent = _dynamicNext;
            _dynamicNext = oldPrevious;
        }

        void Dispatch(int kernel, int resolution)
        {
            var groups = Mathf.CeilToInt(resolution / 8f);
            dynamicWaveCompute.Dispatch(kernel, groups, groups, 1);
        }

        void ReleaseDynamicResources()
        {
            ReleaseTexture(ref _dynamicPrevious);
            ReleaseTexture(ref _dynamicCurrent);
            ReleaseTexture(ref _dynamicNext);
            _impulseBuffer?.Release();
            _impulseBuffer = null;
            _dynamicResolution = 0;
            _pendingImpulses.Clear();
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            SafeDestroy(texture);
            texture = null;
        }

        static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            if (showLodBounds)
            {
                Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.45f);
                for (var i = 0; i < profile.lodLevels; i++)
                {
                    var size = profile.baseLodSize * Mathf.Pow(2f, i);
                    Gizmos.DrawWireCube(new Vector3(transform.position.x, waterLevel, transform.position.z),
                        new Vector3(size, 0.02f, size));
                }
            }
            if (showDynamicWaveArea)
            {
                Gizmos.color = new Color(0.1f, 1f, 0.55f, 0.6f);
                Gizmos.DrawWireCube(new Vector3(_dynamicCenter.x, waterLevel, _dynamicCenter.y),
                    new Vector3(profile.dynamicWorldSize, 0.1f, profile.dynamicWorldSize));
            }
        }
    }
}
