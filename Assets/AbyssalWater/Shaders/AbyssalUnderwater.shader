Shader "Hidden/MusicProgram/Abyssal Water/Underwater"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Underwater Waterline Composite"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "AbyssalWaterCommon.hlsl"

            float IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return step(rawDepth, 0.00001);
                #else
                    return step(0.99999, rawDepth);
                #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 cameraWS = GetCameraPositionWS();
                float cameraHeight = AbyssalWaterHeight(cameraWS.xz);
                float signedCameraDistance = cameraWS.y - cameraHeight;
                float waterlineThickness = max(0.001, _AbyssalUnderwater.y);
                float underwaterAmount = 1.0 - smoothstep(-waterlineThickness, waterlineThickness,
                                                          signedCameraDistance);
                if (underwaterAmount <= 0.0001) return original;

                float rawDepth = SampleSceneDepth(uv);
                float sky = IsSkyDepth(rawDepth);
                float3 sceneWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 toScene = sceneWS - cameraWS;
                float sceneDistance = length(toScene);
                float3 rayDirection = sceneDistance > 1e-4 ? toScene / sceneDistance :
                                      normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(uv * 2.0 - 1.0, 1.0)));
                if (sky > 0.5)
                {
                    sceneDistance = _AbyssalAbsorption.w;
                    sceneWS = cameraWS + rayDirection * sceneDistance;
                }

                float waterPath = min(sceneDistance, _AbyssalAbsorption.w);
                float surfaceDistance = waterPath;
                float crossesSurface = 0.0;
                if (rayDirection.y > -0.12)
                {
                    surfaceDistance = AbyssalSolveRayToSurface(cameraWS, rayDirection, waterPath);
                    crossesSurface = step(surfaceDistance + 0.01, waterPath);
                    waterPath = min(waterPath, surfaceDistance);
                }

                float3 samplePoint = cameraWS + rayDirection * waterPath;
                AbyssalWaveSample surfaceWave = AbyssalEvaluateWaves(samplePoint.xz);
                float3 surfaceNormal = surfaceWave.normalWS;
                float tangentAmount = abs(dot(rayDirection, surfaceNormal));
                float waterline = crossesSurface *
                    (1.0 - smoothstep(0.0, waterlineThickness * 4.0 + fwidth(tangentAmount), tangentAmount));
                float meniscusRange = max(waterlineThickness * 2.0, _AbyssalContact.y * 2.0);
                float cameraSurfaceProximity = 1.0 - smoothstep(waterlineThickness, meniscusRange,
                                                                abs(signedCameraDistance));
                waterline *= cameraSurfaceProximity;

                float2 distortion = mul((float3x3)UNITY_MATRIX_V, surfaceNormal).xy *
                                    _AbyssalUnderwater.x * 0.008;
                distortion += sin(float2(uv.y * 173.0, uv.x * 151.0) + _AbyssalTime * float2(1.7, -1.25)) *
                              _AbyssalUnderwater.x * 0.0008;
                float2 distortedUV = saturate(uv + distortion * saturate(waterPath * 0.15));
                float3 sourceColour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV).rgb;

                float3 transmittance = AbyssalBeerLambert(waterPath);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(sceneWS));
                float phase = AbyssalHenyeyGreenstein(dot(rayDirection, -mainLight.direction));
                float3 scattering = _AbyssalScattering.rgb * (1.0 - transmittance) *
                                    _AbyssalScattering.w * (0.72 + phase * 3.0);
                float3 underwaterColour = sourceColour * transmittance + scattering;

                float sceneDepthBelowWater = AbyssalWaterHeight(sceneWS.xz) - sceneWS.y;
                if (sky < 0.5 && sceneDepthBelowWater > max(0.35, waterlineThickness * 4.0))
                {
                    float3 caustic = AbyssalPhysicalCaustic(sceneWS.xz, sceneDepthBelowWater,
                                                           mainLight.direction);
                    underwaterColour += caustic * _AbyssalCausticColor.rgb *
                                        mainLight.color * mainLight.shadowAttenuation * transmittance;
                }

                if (_AbyssalGodRays.x > 0.0)
                {
                    float rayPattern = pow(saturate(sin(dot(samplePoint.xz, float2(0.071, 0.093)) +
                                                        _AbyssalTime * 0.22) * 0.5 + 0.5), 7.0);
                    float horizonFade = saturate(dot(-rayDirection, mainLight.direction) * 0.5 + 0.5);
                    underwaterColour += mainLight.color * rayPattern * horizonFade *
                                        _AbyssalGodRays.x * (1.0 - transmittance);
                }

                float meniscus = waterline * _AbyssalUnderwater.z;
                underwaterColour = lerp(underwaterColour,
                    _AbyssalFoamColor.rgb + mainLight.color * 0.25, saturate(meniscus));
                return half4(lerp(original.rgb, underwaterColour, underwaterAmount), original.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
