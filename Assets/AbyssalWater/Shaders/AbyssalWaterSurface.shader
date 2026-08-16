Shader "MusicProgram/Abyssal Water/Surface"
{
    Properties
    {
        [NoScaleOffset]_NormalMapA("Fine Normal", 2D) = "bump" {}
        [NoScaleOffset]_NormalMapB("Broad Normal", 2D) = "bump" {}
        [NoScaleOffset]_FoamNoise("Foam Noise", 2D) = "white" {}
        _NormalTiling("Normal Tiling (Fine, Broad)", Vector) = (0.12, 0.035, 0, 0)
        _NormalSpeeds("Normal Speeds", Vector) = (0.018, -0.012, -0.009, 0.014)
        _FineNormalStrength("Fine Normal Strength", Range(0, 2)) = 0.48
        _BroadNormalStrength("Broad Normal Strength", Range(0, 2)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
        }
        Pass
        {
            Name "AbyssalWaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "AbyssalWaterCommon.hlsl"

            TEXTURE2D(_NormalMapA);
            SAMPLER(sampler_NormalMapA);
            TEXTURE2D(_NormalMapB);
            SAMPLER(sampler_NormalMapB);
            TEXTURE2D(_FoamNoise);
            SAMPLER(sampler_FoamNoise);
            TEXTURE2D(_AbyssalPlanarReflectionTexture);
            SAMPLER(sampler_AbyssalPlanarReflectionTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _NormalTiling;
                float4 _NormalSpeeds;
                float _FineNormalStrength;
                float _BroadNormalStrength;
            CBUFFER_END

            float4x4 _AbyssalPlanarReflectionVP;
            float _AbyssalPlanarReflectionEnabled;
            float4 _AbyssalLodData; // inner radius, outer radius, inner/outer minimum wavelength

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 waveData : TEXCOORD2; // crest, jacobian, dynamic height, dynamic velocity
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float radialDistance = max(abs(input.positionOS.x), abs(input.positionOS.z));
                float lodBlend = saturate((radialDistance - _AbyssalLodData.x) /
                                          max(0.01, _AbyssalLodData.y - _AbyssalLodData.x));
                float minimumWavelength = lerp(_AbyssalLodData.z, _AbyssalLodData.w, lodBlend);
                AbyssalWaveSample wave = AbyssalEvaluateWavesFiltered(positionWS.xz, minimumWavelength);
                positionWS += wave.displacement;
                output.positionWS = positionWS;
                output.normalWS = wave.normalWS;
                output.waveData = float4(wave.crest, wave.jacobian, wave.dynamicHeight, wave.dynamicVelocity);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float3 AbyssalHash3(float2 value, float seed)
            {
                float3 hash = float3(
                    dot(value, float2(127.1, 311.7)),
                    dot(value, float2(269.5, 183.3)),
                    dot(value, float2(419.2, 371.9)));
                return frac(sin(hash + seed * float3(0.071, 0.113, 0.173)) * 43758.5453);
            }

            float2 AbyssalRotateQuarter(float2 value, float quadrant)
            {
                if (quadrant < 0.5) return value;
                if (quadrant < 1.5) return float2(-value.y, value.x);
                if (quadrant < 2.5) return -value;
                return float2(value.y, -value.x);
            }

            float2 AbyssalInverseRotateQuarter(float2 value, float quadrant)
            {
                if (quadrant < 0.5) return value;
                if (quadrant < 1.5) return float2(value.y, -value.x);
                if (quadrant < 2.5) return -value;
                return float2(-value.y, value.x);
            }

            float3 AbyssalSampleNormalCell(TEXTURE2D_PARAM(normalMap, normalSampler),
                                            float2 uv, float2 cell, float strength, float seed)
            {
                float3 random = AbyssalHash3(cell, seed);
                float quadrant = floor(random.z * 4.0);
                float2 local = uv - cell;
                float2 transformedUv = AbyssalRotateQuarter(local, quadrant) + random.xy * 7.31;
                float3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalMap, normalSampler, transformedUv), strength);
                normalTS.xy = AbyssalInverseRotateQuarter(normalTS.xy, quadrant);
                return normalTS;
            }

            float3 AbyssalSampleStochasticNormal(TEXTURE2D_PARAM(normalMap, normalSampler),
                                                  float2 uv, float strength, float seed)
            {
                float2 baseCell = floor(uv);
                float2 blend = frac(uv);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float3 normal00 = AbyssalSampleNormalCell(TEXTURE2D_ARGS(normalMap, normalSampler),
                    uv, baseCell, strength, seed);
                float3 normal10 = AbyssalSampleNormalCell(TEXTURE2D_ARGS(normalMap, normalSampler),
                    uv, baseCell + float2(1.0, 0.0), strength, seed);
                float3 normal01 = AbyssalSampleNormalCell(TEXTURE2D_ARGS(normalMap, normalSampler),
                    uv, baseCell + float2(0.0, 1.0), strength, seed);
                float3 normal11 = AbyssalSampleNormalCell(TEXTURE2D_ARGS(normalMap, normalSampler),
                    uv, baseCell + 1.0, strength, seed);
                return normalize(lerp(lerp(normal00, normal10, blend.x),
                                      lerp(normal01, normal11, blend.x), blend.y));
            }

            float3 SampleDetailNormal(float2 worldXZ)
            {
                float2 uvA = worldXZ * _NormalTiling.x + _AbyssalTime * _NormalSpeeds.xy;
                float2 uvB = worldXZ * _NormalTiling.y + _AbyssalTime * _NormalSpeeds.zw;
                float3 tangentA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMapA, sampler_NormalMapA, uvA), _FineNormalStrength);
                float3 tangentB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMapB, sampler_NormalMapB, uvB), _BroadNormalStrength);
                if (_AbyssalAntiTiling.x > 0.5 && _AbyssalAntiTiling.w > 0.001)
                {
                    float3 stochasticA = AbyssalSampleStochasticNormal(
                        TEXTURE2D_ARGS(_NormalMapA, sampler_NormalMapA), uvA,
                        _FineNormalStrength, _AbyssalAntiTilingSeed + 31.0);
                    float3 stochasticB = AbyssalSampleStochasticNormal(
                        TEXTURE2D_ARGS(_NormalMapB, sampler_NormalMapB), uvB,
                        _BroadNormalStrength, _AbyssalAntiTilingSeed + 79.0);
                    tangentA = normalize(lerp(tangentA, stochasticA, _AbyssalAntiTiling.w));
                    tangentB = normalize(lerp(tangentB, stochasticB, _AbyssalAntiTiling.w));
                }
                float3 worldA = float3(tangentA.x, tangentA.z, tangentA.y);
                float3 worldB = float3(tangentB.x, tangentB.z, tangentB.y);
                return normalize(worldA + worldB - float3(0, 1, 0));
            }

            float3 SampleEnvironment(float3 viewDirection, float3 normalWS, float roughness)
            {
                float3 reflected = reflect(-viewDirection, normalWS);
                float4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0,
                    reflected, roughness * 6.0);
                return DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);
            }

            float3 SamplePlanarReflection(float3 positionWS, float3 normalWS, float roughness, float3 fallback)
            {
                if (_AbyssalPlanarReflectionEnabled < 0.5) return fallback;
                float4 clip = mul(_AbyssalPlanarReflectionVP, float4(positionWS, 1.0));
                float2 uv = clip.xy / max(1e-4, clip.w) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
                uv += viewNormal.xy * _AbyssalOptics.y * 0.45;
                float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float valid = smoothstep(0.0, 0.075, edgeDistance);
                float mip = (1.0 - _AbyssalOptics.w) * 5.0;
                float3 planar = SAMPLE_TEXTURE2D_LOD(_AbyssalPlanarReflectionTexture,
                    sampler_AbyssalPlanarReflectionTexture, saturate(uv), mip).rgb;
                return lerp(fallback, planar, valid * _AbyssalOptics.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 viewDirection = SafeNormalize(GetCameraPositionWS() - input.positionWS);

                float3 detailNormal = SampleDetailNormal(input.positionWS.xz);
                float pixelFootprint = max(length(ddx(input.positionWS.xz)),
                                           length(ddy(input.positionWS.xz))) * 1.35;
                AbyssalWaveSample microWave = AbyssalEvaluateMicroWavesFiltered(
                    input.positionWS.xz, pixelFootprint);
                float3 microSlope = float3(-microWave.slope.x * _AbyssalSurface.x, 0.0,
                                           -microWave.slope.y * _AbyssalSurface.x);
                float3 detailSlope = detailNormal - float3(0.0, 1.0, 0.0);
                float3 normalWS = normalize(input.normalWS + microSlope + detailSlope * 0.48);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 refractedUV = screenUV + normalVS.xy * _AbyssalOptics.y;
                refractedUV = saturate(refractedUV);

                float rawDepth = SampleSceneDepth(refractedUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterEyeDepth = max(0.0, -TransformWorldToView(input.positionWS).z);
                float opticalLength = max(0.0, sceneEyeDepth - waterEyeDepth);
                float3 transmittance = AbyssalBeerLambert(opticalLength);
                float3 sceneColour = SampleSceneColor(refractedUV);
                float3 refracted = sceneColour * transmittance +
                    _AbyssalScattering.rgb * (1.0 - transmittance) * _AbyssalScattering.w;

                float3 sceneWS = ComputeWorldSpacePosition(refractedUV, rawDepth, UNITY_MATRIX_I_VP);
                float sceneDepthBelowWater = AbyssalWaterHeight(sceneWS.xz) - sceneWS.y;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #if UNITY_REVERSED_Z
                    float validReceiver = step(0.00001, rawDepth);
                #else
                    float validReceiver = step(rawDepth, 0.99999);
                #endif
                float3 caustic = AbyssalPhysicalCaustic(sceneWS.xz, sceneDepthBelowWater,
                                                        mainLight.direction) * validReceiver;
                refracted += caustic * _AbyssalCausticColor.rgb * mainLight.color * transmittance;

                float roughness = 1.0 - _AbyssalOptics.w;
                float3 environment = SampleEnvironment(viewDirection, normalWS, roughness);
                float3 reflection = SamplePlanarReflection(input.positionWS, normalWS, roughness, environment);
                float r0 = pow((_AbyssalOptics.x - 1.0) / (_AbyssalOptics.x + 1.0), 2.0);
                float fresnel = r0 + (1.0 - r0) * pow(1.0 - saturate(dot(normalWS, viewDirection)), 5.0);

                float3 halfVector = SafeNormalize(viewDirection + mainLight.direction);
                float specularPower = lerp(32.0, 512.0, _AbyssalOptics.w);
                float specular = pow(saturate(dot(normalWS, halfVector)), specularPower) *
                                 mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float backLight = pow(saturate(dot(viewDirection, -mainLight.direction)),
                                      _AbyssalSurface.z);
                float crestTransmission = backLight * input.waveData.x * _AbyssalSurface.y;

                float compression = saturate(1.0 - input.waveData.y);
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise,
                    input.positionWS.xz * 0.085 + _AbyssalTime * float2(0.018, -0.011)).r;
                float crestGate = smoothstep(_AbyssalFoam.y - _AbyssalFoam.z,
                                             _AbyssalFoam.y + _AbyssalFoam.z,
                                             input.waveData.x);
                float compressionGate = smoothstep(0.08, 0.42, compression);
                float breakup = smoothstep(0.42, 0.76, foamNoise + compression * 0.12);
                float crestFoam = crestGate * compressionGate * breakup;
                float shorelineFoam = 1.0 - smoothstep(0.0, max(0.01, _AbyssalFoam.w), opticalLength);
                shorelineFoam *= smoothstep(0.36, 0.8, foamNoise) * 0.7;
                float contactFoam = saturate(abs(input.waveData.w) * _AbyssalDynamicParameters.z +
                                             abs(input.waveData.z) * 0.35);
                float foam = saturate((crestFoam * lerp(0.55, 1.25, foamNoise) + shorelineFoam + contactFoam) *
                                      _AbyssalFoam.x);

                float3 colour = lerp(refracted, reflection, saturate(fresnel * _AbyssalOptics.z));
                colour += specular * mainLight.color * (0.2 + fresnel);
                colour += crestTransmission * _AbyssalCrestColor.rgb * mainLight.color;
                colour = lerp(colour, _AbyssalFoamColor.rgb, foam);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
