using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(Renderer))]
    public sealed class SonicSurfaceRipple : MonoBehaviour
    {
        private struct Ripple
        {
            public bool Active;
            public bool LocalOrigin;
            public Vector3 Origin;
            public float StartTime;
            public float Strength;
            public float Speed;
            public float Width;
            public Color Color;
        }

        private static readonly int OriginsId = Shader.PropertyToID("_SonicRippleOrigins");
        private static readonly int DataId = Shader.PropertyToID("_SonicRippleData");
        private static readonly int ColorsId = Shader.PropertyToID("_SonicRippleColors");
        private static readonly int CountId = Shader.PropertyToID("_SonicRippleCount");

        [SerializeField] private Material rippleMaterial;
        [SerializeField, Range(1f, 10f)] private float nearbyRange = 5f;
        [SerializeField, Range(0.5f, 5f)] private float propagationSpeed = 1.8f;
        [SerializeField, Range(0.03f, 0.5f)] private float ringWidth = 0.12f;
        [SerializeField, Range(0.5f, 4f)] private float lifetime = 2.4f;
        [SerializeField, Range(1f, 1.03f)] private float shellScale = 1.004f;

        private readonly Ripple[] ripples = new Ripple[4];
        private readonly Vector4[] origins = new Vector4[4];
        private readonly Vector4[] data = new Vector4[4];
        private readonly Vector4[] colors = new Vector4[4];
        private int nextRipple;
        private Renderer shellRenderer;
        private MaterialPropertyBlock propertyBlock;
        private SonicAudioBus subscribedBus;

        public void Configure(Material material)
        {
            rippleMaterial = material;
        }

        private void Awake()
        {
            CreateShell();
        }

        private void OnEnable()
        {
            EnsureSubscription();
        }

        private void OnDisable()
        {
            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = null;
        }

        private void Update()
        {
            EnsureSubscription();
            UpdateProperties();
        }

        private void EnsureSubscription()
        {
            SonicAudioBus current = SonicAudioBus.Instance;
            if (current == subscribedBus)
                return;
            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = current;
            if (subscribedBus != null)
                subscribedBus.SoundEventReported += OnSoundEvent;
        }

        private void OnSoundEvent(SonicSoundEvent soundEvent)
        {
            bool ownSound = soundEvent.Involves(transform);
            float distance = Vector3.Distance(transform.position, soundEvent.Position);
            if (!ownSound && distance > nearbyRange)
                return;

            float distanceGain = ownSound
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, distance / nearbyRange);
            float strength = soundEvent.Strength * distanceGain;
            if (strength < 0.015f)
                return;

            ripples[nextRipple] = new Ripple
            {
                Active = true,
                LocalOrigin = ownSound,
                Origin = ownSound
                    ? transform.InverseTransformPoint(soundEvent.Position)
                    : soundEvent.Position,
                StartTime = Time.time,
                Strength = strength,
                Speed = propagationSpeed * Mathf.Lerp(0.85f, 1.25f, soundEvent.Bands.z),
                Width = ringWidth * Mathf.Lerp(1.25f, 0.75f, soundEvent.Bands.z),
                Color = SurfaceColor(soundEvent.Surface)
            };
            nextRipple = (nextRipple + 1) % ripples.Length;
        }

        private void CreateShell()
        {
            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            if (sourceFilter.sharedMesh == null || rippleMaterial == null)
                return;

            GameObject shell = new GameObject("Sonic Surface Ripple");
            shell.transform.SetParent(transform, false);
            shell.transform.localScale = Vector3.one * shellScale;
            MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
            shellFilter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rippleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            shellRenderer = renderer;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void UpdateProperties()
        {
            if (shellRenderer == null)
                return;

            int count = 0;
            float now = Time.time;
            for (int i = 0; i < ripples.Length; i++)
            {
                Ripple ripple = ripples[i];
                if (!ripple.Active)
                    continue;
                float age = now - ripple.StartTime;
                if (age > lifetime)
                {
                    ripple.Active = false;
                    ripples[i] = ripple;
                    continue;
                }

                Vector3 worldOrigin = ripple.LocalOrigin
                    ? transform.TransformPoint(ripple.Origin)
                    : ripple.Origin;
                origins[count] = new Vector4(
                    worldOrigin.x,
                    worldOrigin.y,
                    worldOrigin.z,
                    ripple.StartTime);
                data[count] = new Vector4(
                    ripple.Strength,
                    ripple.Speed,
                    ripple.Width,
                    1f);
                colors[count] = ripple.Color;
                count++;
            }

            shellRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetInt(CountId, count);
            propertyBlock.SetVectorArray(OriginsId, origins);
            propertyBlock.SetVectorArray(DataId, data);
            propertyBlock.SetVectorArray(ColorsId, colors);
            shellRenderer.SetPropertyBlock(propertyBlock);
        }

        private static Color SurfaceColor(SonicSurfaceType surface)
        {
            switch (surface)
            {
                case SonicSurfaceType.Wood:
                    return new Color(1f, 0.42f, 0.08f, 1f);
                case SonicSurfaceType.Metal:
                    return new Color(0.15f, 0.72f, 1f, 1f);
                case SonicSurfaceType.Glass:
                    return new Color(0.12f, 1f, 0.92f, 1f);
                case SonicSurfaceType.Stone:
                    return new Color(0.72f, 0.38f, 1f, 1f);
                default:
                    return new Color(0.35f, 1f, 0.22f, 1f);
            }
        }
    }
}
