Shader "Hidden/SonicWorld/Grayscale Reveal"
{
    Properties
    {
        _GrayscaleContrast ("Grayscale Contrast", Range(0.5, 2.0)) = 1.08
        _GrayscaleBrightness ("Grayscale Brightness", Range(-0.25, 0.25)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero

        Pass
        {
            Name "Sonic Spatial Grayscale Reveal"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define SONIC_REVEAL_WAVE_CAPACITY 12

            float _GrayscaleContrast;
            float _GrayscaleBrightness;
            float _SonicGlobalColorRestore;
            float4 _SonicGlobalColorWave;
            float4 _SonicGlobalColorWaveParams;
            int _SonicRevealWaveCount;
            float4 _SonicRevealWaveOrigins[SONIC_REVEAL_WAVE_CAPACITY];
            float4 _SonicRevealWaveParams[SONIC_REVEAL_WAVE_CAPACITY];

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    _BlitMipLevel);

                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                luminance = (luminance - 0.5h) * _GrayscaleContrast +
                    0.5h +
                    _GrayscaleBrightness;
                half3 grayscale = saturate(luminance).xxx;

                float deviceDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool hasSceneDepth = deviceDepth > 0.00001;
                #else
                    bool hasSceneDepth = deviceDepth < 0.99999;
                    deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, deviceDepth);
                #endif

                float reveal = 0.0;
                if (hasSceneDepth)
                {
                    float3 worldPosition =
                        ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);

                    [unroll]
                    for (int i = 0; i < SONIC_REVEAL_WAVE_CAPACITY; i++)
                    {
                        if (i >= _SonicRevealWaveCount)
                            break;

                        float4 originRadius = _SonicRevealWaveOrigins[i];
                        float4 waveParams = _SonicRevealWaveParams[i];
                        float distanceToOrigin =
                            distance(worldPosition, originRadius.xyz);
                        float signedDistance = distanceToOrigin - originRadius.w;
                        float width = max(0.001, waveParams.x);
                        float trail = max(0.001, waveParams.z);

                        float crest =
                            1.0 - smoothstep(0.0, width, abs(signedDistance));
                        float insideTrail =
                            step(signedDistance, 0.0) *
                            exp(min(0.0, signedDistance) / trail);
                        float shell = max(crest, insideTrail * 0.72);
                        reveal = max(
                            reveal,
                            shell * waveParams.y * waveParams.w);
                    }
                }

                float globalReveal = saturate(_SonicGlobalColorRestore);
                if (_SonicGlobalColorWaveParams.w > 0.5)
                {
                    float transitionProgress =
                        saturate(_SonicGlobalColorWaveParams.z);
                    if (hasSceneDepth)
                    {
                        float3 transitionWorldPosition =
                            ComputeWorldSpacePosition(
                                uv,
                                deviceDepth,
                                UNITY_MATRIX_I_VP);
                        float transitionDistance = distance(
                            transitionWorldPosition,
                            _SonicGlobalColorWave.xyz);
                        float transitionRadius = _SonicGlobalColorWave.w;
                        float transitionFeather =
                            max(0.001, _SonicGlobalColorWaveParams.x);
                        float insideWave =
                            1.0 -
                            smoothstep(
                                transitionRadius - transitionFeather,
                                transitionRadius + transitionFeather,
                                transitionDistance);
                        globalReveal = _SonicGlobalColorWaveParams.y > 0.0
                            ? insideWave
                            : 1.0 - insideWave;
                    }
                    else
                    {
                        // Depthless sky pixels cannot be positioned inside the
                        // world-space sphere, so fade them with wave progress.
                        globalReveal = _SonicGlobalColorWaveParams.y > 0.0
                            ? transitionProgress
                            : 1.0 - transitionProgress;
                    }
                }

                reveal = max(reveal, globalReveal);
                source.rgb = lerp(grayscale, source.rgb, saturate(reveal));
                return source;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
