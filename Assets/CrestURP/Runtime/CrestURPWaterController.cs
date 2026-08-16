// Crest Water 4 URP port layer for MusicProgram.
// The Crest core is MIT licensed. This integration code is project-owned.

using Crest;
using UnityEngine;

namespace MusicProgram.CrestURP
{
    /// <summary>
    /// Central visual control surface for the URP ocean and underwater renderer.
    /// Values are pushed as shader globals so the transparent ocean and the
    /// full-screen underwater pass remain identical in both XR eyes.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CrestURPWaterController : MonoBehaviour
    {
        public enum PhysicalCausticsQuality
        {
            Balanced = 0,
            High = 1,
        }

        public enum UnderwaterDebugView
        {
            None = 0,
            CausticGain = 1,
            WaveSlope = 2,
            WaterDepth = 3,
            DisplacementHeight = 4,
        }

        static readonly int WaterLevelId = Shader.PropertyToID("_CrestURPWaterLevel");
        static readonly int ShallowColorId = Shader.PropertyToID("_CrestURPShallowColor");
        static readonly int DeepColorId = Shader.PropertyToID("_CrestURPDeepColor");
        static readonly int AbsorptionId = Shader.PropertyToID("_CrestURPAbsorption");
        static readonly int ScatteringId = Shader.PropertyToID("_CrestURPScattering");
        static readonly int VisibilityId = Shader.PropertyToID("_CrestURPVisibility");
        static readonly int CausticsId = Shader.PropertyToID("_CrestURPCaustics");
        static readonly int CausticsColorId = Shader.PropertyToID("_CrestURPCausticsColor");
        static readonly int PhysicalCaustics0Id = Shader.PropertyToID("_CrestURPPhysicalCaustics0");
        static readonly int PhysicalCaustics1Id = Shader.PropertyToID("_CrestURPPhysicalCaustics1");
        static readonly int PhysicalCaustics2Id = Shader.PropertyToID("_CrestURPPhysicalCaustics2");
        static readonly int MeniscusId = Shader.PropertyToID("_CrestURPMeniscus");
        static readonly int GodRaysId = Shader.PropertyToID("_CrestURPGodRays");
        static readonly int MainLightDirectionId = Shader.PropertyToID("_CrestURPMainLightDirection");
        static readonly int MainLightColorId = Shader.PropertyToID("_CrestURPMainLightColor");
        static readonly int UnderwaterEnabledId = Shader.PropertyToID("_CrestURPUnderwaterEnabled");

        [Header("Water body")]
        [Tooltip("Crest Ocean Renderer. If empty, the active Crest ocean is used.")]
        public OceanRenderer ocean;
        [Tooltip("Material using Crest/URP/Ocean.")]
        public Material oceanMaterial;
        [Tooltip("Material using Crest/URP/Underwater. Crest live simulation resources are explicitly bound to it for URP full-screen passes.")]
        public Material underwaterMaterial;
        [Tooltip("Main directional light used for sun glitter, caustics and underwater shafts.")]
        public Light sun;

        [Header("Optical properties")]
        [ColorUsage(false, true)] public Color shallowColor = new(0.035f, 0.38f, 0.50f, 1f);
        [ColorUsage(false, true)] public Color deepColor = new(0.004f, 0.055f, 0.11f, 1f);
        [Tooltip("Beer-Lambert absorption coefficients for red, green and blue.")]
        public Vector3 absorption = new(0.12f, 0.045f, 0.022f);
        [Tooltip("In-scattered light tint under water.")]
        [ColorUsage(false, true)] public Color scatteringColor = new(0.015f, 0.15f, 0.19f, 1f);
        [UnityEngine.Range(4f, 120f)] public float visibilityDistance = 36f;

        [Header("Physical caustics")]
        [Tooltip("Trace sunlight through the live Crest surface using Snell refraction and Jacobian area compression.")]
        public bool physicalCaustics = true;
        public PhysicalCausticsQuality causticsQuality = PhysicalCausticsQuality.High;
        [UnityEngine.Range(0f, 5f)] public float causticsIntensity = 1.35f;
        [UnityEngine.Range(0.005f, 0.5f)] public float causticsDepthFalloff = 0.085f;
        [ColorUsage(false, true)] public Color causticsColor = new(0.65f, 0.92f, 0.84f, 1f);
        [Tooltip("Index of refraction. Fresh water is about 1.333; sea water is about 1.339.")]
        [UnityEngine.Range(1.01f, 1.6f)] public float waterRefractiveIndex = 1.339f;
        [Tooltip("World-space finite-difference radius. Larger values produce broader, more stable caustics.")]
        [UnityEngine.Range(0.04f, 2f)] public float causticsSampleRadius = 0.28f;
        [Tooltip("Minimum sampling radius in Crest texels. Prevents sub-texel gradients while automatically following the selected LOD.")]
        [UnityEngine.Range(0.5f, 4f)] public float causticsAdaptiveTexelRadius = 1.25f;
        [Tooltip("Back-project receiver points to the refracting source patch on the wave surface.")]
        [UnityEngine.Range(0f, 1.25f)] public float causticsInverseProjection = 1f;
        [Tooltip("Shapes the computed irradiance concentration without changing its location.")]
        [UnityEngine.Range(0.25f, 3f)] public float causticsFocalExponent = 0.72f;
        [Tooltip("Clamp for focal singularities to avoid unstable white pixels.")]
        [UnityEngine.Range(1.1f, 32f)] public float causticsMaximumGain = 9f;
        [Tooltip("Lower determinant limit for the surface-to-receiver light mapping.")]
        [UnityEngine.Range(0.01f, 0.5f)] public float causticsMinimumDeterminant = 0.055f;
        [UnityEngine.Range(0f, 4f)] public float causticsReceiverNormalPower = 0.8f;
        [UnityEngine.Range(0f, 2f)] public float causticsMinimumDepth = 0.08f;
        public UnderwaterDebugView underwaterDebugView;

        [Header("Waterline and volume")]
        [UnityEngine.Range(0.0005f, 0.08f)] public float waterlineWidth = 0.012f;
        [UnityEngine.Range(0.05f, 2f)] public float waterlineRange = 0.65f;
        [UnityEngine.Range(0f, 3f)] public float waterlineBrightness = 0.45f;
        [UnityEngine.Range(0f, 3f)] public float godRayIntensity = 0.65f;
        [UnityEngine.Range(1f, 64f)] public float godRayAnisotropy = 16f;
        [UnityEngine.Range(0f, 4f)] public float underwaterDistortion = 0.55f;
        public bool underwaterRendering = true;

        [Header("Surface")]
        [UnityEngine.Range(0f, 1f)] public float smoothness = 0.93f;
        [UnityEngine.Range(0f, 2f)] public float detailNormalStrength = 0.52f;
        [UnityEngine.Range(0f, 0.25f)] public float refractionStrength = 0.045f;
        [UnityEngine.Range(0f, 3f)] public float foamStrength = 1.25f;
        [UnityEngine.Range(0f, 1f)] public float foamThreshold = 0.42f;
        [UnityEngine.Range(0.1f, 12f)] public float shorelineFoamDepth = 2.2f;

        void Reset()
        {
            FindReferences();
            Apply();
        }

        void OnEnable()
        {
            FindReferences();
            Apply();
        }

        void OnValidate()
        {
            absorption.x = Mathf.Max(0f, absorption.x);
            absorption.y = Mathf.Max(0f, absorption.y);
            absorption.z = Mathf.Max(0f, absorption.z);
            Apply();
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(UnderwaterEnabledId, 0f);
        }

        void Update()
        {
            Apply();
        }

        void FindReferences()
        {
            if (ocean == null)
            {
                ocean = OceanRenderer.Instance != null
                    ? OceanRenderer.Instance
                    : FindFirstObjectByType<OceanRenderer>();
            }

            if (oceanMaterial == null && ocean != null)
            {
                oceanMaterial = ocean.OceanMaterial;
            }

            if (sun == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var candidate in lights)
                {
                    if (candidate.type == LightType.Directional && candidate.enabled)
                    {
                        sun = candidate;
                        break;
                    }
                }
            }
        }

        public void Apply()
        {
            var waterLevel = ocean != null ? ocean.transform.position.y : transform.position.y;
            Shader.SetGlobalFloat(WaterLevelId, waterLevel);
            Shader.SetGlobalColor(ShallowColorId, shallowColor);
            Shader.SetGlobalColor(DeepColorId, deepColor);
            Shader.SetGlobalVector(AbsorptionId, new Vector4(absorption.x, absorption.y, absorption.z, 0f));
            Shader.SetGlobalColor(ScatteringId, scatteringColor);
            Shader.SetGlobalFloat(VisibilityId, Mathf.Max(0.01f, visibilityDistance));
            Shader.SetGlobalVector(CausticsId, new Vector4(causticsIntensity, 0f, 0f, causticsDepthFalloff));
            Shader.SetGlobalColor(CausticsColorId, causticsColor);
            Shader.SetGlobalVector(PhysicalCaustics0Id, new Vector4(
                waterRefractiveIndex,
                causticsSampleRadius,
                causticsInverseProjection,
                physicalCaustics ? 1f : 0f));
            Shader.SetGlobalVector(PhysicalCaustics1Id, new Vector4(
                causticsFocalExponent,
                causticsMaximumGain,
                causticsMinimumDeterminant,
                (float)causticsQuality));
            Shader.SetGlobalVector(PhysicalCaustics2Id, new Vector4(
                causticsReceiverNormalPower,
                causticsMinimumDepth,
                (float)underwaterDebugView,
                causticsAdaptiveTexelRadius));
            Shader.SetGlobalVector(MeniscusId, new Vector4(waterlineWidth, waterlineRange, waterlineBrightness, underwaterDistortion));
            Shader.SetGlobalVector(GodRaysId, new Vector4(godRayIntensity, godRayAnisotropy, 0f, 0f));
            Shader.SetGlobalFloat(UnderwaterEnabledId, underwaterRendering ? 1f : 0f);

            if (ocean != null && underwaterMaterial != null)
            {
                ocean.BindCustomMaterialData(underwaterMaterial);
            }

            if (sun != null)
            {
                var direction = -sun.transform.forward;
                var color = sun.color.linear * sun.intensity;
                Shader.SetGlobalVector(MainLightDirectionId, new Vector4(direction.x, direction.y, direction.z, 0f));
                Shader.SetGlobalColor(MainLightColorId, color);
            }

            if (oceanMaterial == null)
            {
                return;
            }

            oceanMaterial.SetColor("_ShallowColor", shallowColor);
            oceanMaterial.SetColor("_DeepColor", deepColor);
            oceanMaterial.SetVector("_Absorption", absorption);
            oceanMaterial.SetFloat("_Smoothness", smoothness);
            oceanMaterial.SetFloat("_DetailNormalStrength", detailNormalStrength);
            oceanMaterial.SetFloat("_RefractionStrength", refractionStrength);
            oceanMaterial.SetFloat("_FoamStrength", foamStrength);
            oceanMaterial.SetFloat("_FoamThreshold", foamThreshold);
            oceanMaterial.SetFloat("_ShorelineFoamDepth", shorelineFoamDepth);
        }
    }
}
