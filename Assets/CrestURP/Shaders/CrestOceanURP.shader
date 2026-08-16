// URP surface renderer for the MIT-licensed Crest Water 4 simulation core.
// Vertex displacement is derived from Crest's Ocean.shader and helper library.

Shader "Crest/URP/Ocean"
{
    Properties
    {
        [HDR] _ShallowColor("Shallow Color", Color) = (0.035, 0.38, 0.50, 0.82)
        [HDR] _DeepColor("Deep Color", Color) = (0.004, 0.055, 0.11, 0.96)
        _Absorption("Absorption", Vector) = (0.16, 0.055, 0.025, 0)
        _Smoothness("Smoothness", Range(0, 1)) = 0.93
        _DetailNormalStrength("Detail Normal Strength", Range(0, 2)) = 0.52
        _DetailNormalScale("Detail Normal Scale", Range(0.01, 4)) = 0.42
        _RefractionStrength("Refraction Strength", Range(0, 0.25)) = 0.045
        _FresnelPower("Fresnel Power", Range(1, 12)) = 5
        _Alpha("Surface Alpha", Range(0, 1)) = 0.68

        [Toggle(_FOAM_ON)] _Foam("Enable Crest Foam", Float) = 1
        [HDR] _FoamColor("Foam Color", Color) = (0.82, 0.95, 1, 1)
        _FoamStrength("Foam Strength", Range(0, 3)) = 1.25
        _FoamThreshold("Foam Threshold", Range(0, 1)) = 0.42
        _ShorelineFoamDepth("Shoreline Foam Depth", Range(0.1, 12)) = 2.2
        _ShorelineFoamStrength("Shoreline Foam Strength", Range(0, 3)) = 1.1

        [Toggle(_FLOW_ON)] _Flow("Enable Crest Flow", Float) = 0
        [Toggle(_CLIPSURFACE_ON)] _ClipSurface("Enable Crest Surface Clipping", Float) = 0
        [Toggle(_SHADOWS_ON)] _CrestShadows("Enable Crest Shadow Data", Float) = 0
        [Toggle(_UNDERWATER_ON)] _Underwater("Render Surface From Below", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-40"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "CrestURPForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _FOAM_ON
            #pragma shader_feature_local _FLOW_ON
            #pragma shader_feature_local _CLIPSURFACE_ON
            #pragma shader_feature_local _SHADOWS_ON
            #pragma shader_feature_local _UNDERWATER_ON
            #pragma multi_compile _ CREST_FLOATING_ORIGIN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #include "../../ThirdParty/CrestWater4/Shaders/ShaderLibrary/Common.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanGlobals.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanInputsDriven.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanHelpersNew.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanVertHelpers.hlsl"

            TEXTURE2D(_CrestURPPlanarReflectionTexture);
            SAMPLER(sampler_CrestURPPlanarReflectionTexture);
            float4x4 _CrestURPPlanarReflectionVP;
            float _CrestURPPlanarReflectionEnabled;
            float _CrestURPPlanarReflectionStrength;
            float _CrestURPPlanarReflectionDistortion;
            float _CrestURPPlanarReflectionMipStrength;
            float _CrestURPPlanarReflectionRendering;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _Absorption;
                half _Smoothness;
                half _DetailNormalStrength;
                half _DetailNormalScale;
                half _RefractionStrength;
                half _FresnelPower;
                half _Alpha;
                half4 _FoamColor;
                half _FoamStrength;
                half _FoamThreshold;
                half _ShorelineFoamDepth;
                half _ShorelineFoamStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 undisplacedWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half4 lodWeights : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 DetailNormal(float2 worldXZ, float time)
            {
                float scale = max(_DetailNormalScale, 0.001);
                float2 p0 = worldXZ * scale + float2(time * 0.17, time * 0.11);
                float2 p1 = mul(float2x2(0.8, -0.6, 0.6, 0.8), worldXZ) * (scale * 1.73)
                    + float2(-time * 0.13, time * 0.19);
                float2 d0 = float2(cos(p0.x * 2.1) + 0.55 * cos((p0.x + p0.y) * 3.7),
                                   -sin(p0.y * 2.4) + 0.45 * cos((p0.x - p0.y) * 3.1));
                float2 d1 = float2(cos(p1.x * 1.8) - 0.5 * sin((p1.x - p1.y) * 4.2),
                                   sin(p1.y * 2.25) + 0.4 * cos((p1.x + p1.y) * 3.6));
                return (d0 + d1) * 0.23 * _DetailNormalStrength;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                const CascadeParams cascade0 = _CrestCascadeData[_LD_SliceIndex];
                const CascadeParams cascade1 = _CrestCascadeData[_LD_SliceIndex + 1];
                const PerCascadeInstanceData instanceData = _CrestPerCascadeInstanceData[_LD_SliceIndex];

                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                float lodAlpha;
                SnapAndTransitionVertLayout(instanceData._meshScaleLerp, cascade0, instanceData._geoGridWidth, worldPosition, lodAlpha);
                output.undisplacedWS = worldPosition;

                const float weight0 = (1.0 - lodAlpha) * cascade0._weight;
                const float weight1 = (1.0 - weight0) * cascade1._weight;

                if (weight0 > 0.001)
                {
                    SampleDisplacements(_LD_TexArray_AnimatedWaves,
                        WorldToUV(output.undisplacedWS.xz, cascade0, _LD_SliceIndex), weight0, worldPosition);
                }
                if (weight1 > 0.001)
                {
                    SampleDisplacements(_LD_TexArray_AnimatedWaves,
                        WorldToUV(output.undisplacedWS.xz, cascade1, _LD_SliceIndex + 1), weight1, worldPosition);
                }

                output.positionWS = worldPosition;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.lodWeights = half4(lodAlpha, weight0, weight1, 0.0);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // The auxiliary camera must never see the ocean sheet itself.
                // Keeping the renderer enabled preserves Crest's simulation data.
                clip(0.5 - _CrestURPPlanarReflectionRendering);

                const CascadeParams cascade0 = _CrestCascadeData[_LD_SliceIndex];
                const CascadeParams cascade1 = _CrestCascadeData[_LD_SliceIndex + 1];
                const float weight0 = input.lodWeights.y;
                const float weight1 = input.lodWeights.z;

                float3 normalPosition = input.undisplacedWS;
                float2 normalXZ = 0.0;
                half pinch = 0.0;
                if (weight0 > 0.001)
                {
                    SampleDisplacementsNormals(_LD_TexArray_AnimatedWaves,
                        WorldToUV(input.undisplacedWS.xz, cascade0, _LD_SliceIndex), weight0,
                        cascade0._oneOverTextureRes, cascade0._texelWidth, normalPosition, normalXZ, pinch);
                }
                if (weight1 > 0.001)
                {
                    SampleDisplacementsNormals(_LD_TexArray_AnimatedWaves,
                        WorldToUV(input.undisplacedWS.xz, cascade1, _LD_SliceIndex + 1), weight1,
                        cascade1._oneOverTextureRes, cascade1._texelWidth, normalPosition, normalXZ, pinch);
                }

                normalXZ += DetailNormal(input.positionWS.xz, _Time.y);
                float3 normalWS = normalize(float3(normalXZ.x, 1.0, normalXZ.y));
                if (!isFrontFace)
                {
                    normalWS = -normalWS;
                }

                float3 viewDirection = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), _FresnelPower);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterEyeDepth = -TransformWorldToView(input.positionWS).z;
                float thickness = max(0.0, sceneEyeDepth - waterEyeDepth);

                float2 refractedUV = saturate(screenUV + normalWS.xz * _RefractionStrength * saturate(thickness * 0.08));
                half3 background = SampleSceneColor(refractedUV);
                half3 transmittance = exp(-max(_Absorption.xyz, 0.0001) * min(thickness, 80.0));
                half3 bodyColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(exp(-thickness * 0.11)));
                half3 color = background * transmittance + bodyColor * (1.0 - transmittance);

                float3 reflectionDirection = reflect(-viewDirection, normalWS);
                half perceptualRoughness = 1.0h - _Smoothness;
                half3 environment = GlossyEnvironmentReflection(reflectionDirection, perceptualRoughness, 1.0h);
                color = lerp(color, environment, saturate(0.18 + fresnel * 0.82));

                float4 reflectionPosition = mul(_CrestURPPlanarReflectionVP, float4(input.positionWS, 1.0));
                float2 reflectionUV = reflectionPosition.xy / max(abs(reflectionPosition.w), 0.0001) * 0.5 + 0.5;
                reflectionUV += normalWS.xz * _CrestURPPlanarReflectionDistortion;
                float reflectionValid = step(0.0001, reflectionPosition.w)
                    * step(0.0, reflectionUV.x) * step(reflectionUV.x, 1.0)
                    * step(0.0, reflectionUV.y) * step(reflectionUV.y, 1.0);
                half3 planarReflection = SAMPLE_TEXTURE2D_LOD(
                    _CrestURPPlanarReflectionTexture,
                    sampler_CrestURPPlanarReflectionTexture,
                    saturate(reflectionUV),
                    perceptualRoughness * _CrestURPPlanarReflectionMipStrength).rgb;
                half planarBlend = (half)(_CrestURPPlanarReflectionEnabled * _CrestURPPlanarReflectionStrength
                    * reflectionValid * (isFrontFace ? 1.0 : 0.0) * saturate(0.28 + fresnel * 0.72));
                color = lerp(color, planarReflection, planarBlend);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(32.0h, 320.0h, _Smoothness));
                color += mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation
                    * (specular * lerp(0.35h, 2.2h, fresnel) + ndl * _ShallowColor.rgb * 0.08h);

                if (!isFrontFace)
                {
                    // Avoid black back-facing sheets at the moving waterline while
                    // retaining the darker total-internal-reflection character.
                    color = lerp(color, _ShallowColor.rgb * 0.72h + environment * 0.28h, 0.34h);
                }

                half foam = 0.0h;
                #if _FOAM_ON
                if (weight0 > 0.001)
                {
                    SampleFoam(_LD_TexArray_Foam, WorldToUV(input.positionWS.xz, cascade0, _LD_SliceIndex), weight0, foam);
                }
                if (weight1 > 0.001)
                {
                    SampleFoam(_LD_TexArray_Foam, WorldToUV(input.positionWS.xz, cascade1, _LD_SliceIndex + 1), weight1, foam);
                }
                #endif

                half crestFoam = smoothstep(_FoamThreshold, 1.0h, foam * _FoamStrength + saturate(1.0h - normalWS.y) * 0.35h);
                half shorelineFoam = (1.0h - smoothstep(0.0h, _ShorelineFoamDepth, thickness)) * _ShorelineFoamStrength;
                half foamNoise = saturate(0.55h + 0.45h * sin(dot(input.positionWS.xz, float2(2.7, 3.1)) + _Time.y * 1.8));
                half foamMask = saturate(max(crestFoam, shorelineFoam * foamNoise));
                color = lerp(color, _FoamColor.rgb * (0.65h + 0.35h * ndl), foamMask);

                half alpha = saturate(_Alpha + fresnel * 0.18h + (1.0h - exp(-thickness * 0.08h)) * 0.12h + foamMask * 0.25h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
