// Hand-ported to URP 17; original material properties and shader GUID are preserved.
Shader "Davis3D/OceanEnviroment2/CausticDecal"
{
	Properties
	{
		_FadeRadius("FadeRadius", Float) = 0.1
		_FadeStrength("FadeStrength", Float) = 0.5
		_Add("Add", Float) = 8
		_Add_Blur("Add_Blur", Float) = 8
		_Scale("Scale", Float) = 1
		_GiantMask_Scale("GiantMask_Scale", Float) = 0.2
		_Waves("Waves", 2D) = "white" {}
		_CausticWave1_Multiply("CausticWave1_Multiply", Float) = 0.75
		_Multiply_Caustics("Multiply_Caustics", Float) = 1.12
		_Caustic_Scale1("Caustic_Scale1", Float) = 2
		_Caustic_Scale2("Caustic_Scale2", Float) = 2
		_Caustic_Scale3("Caustic_Scale3", Float) = 2
		_Caustic_Speed1("Caustic_Speed1", Float) = 2
		_Caustic_Speed2("Caustic_Speed2", Float) = 2
		_Caustic_Speed3("Caustic_Speed3", Float) = 2
		_Flowmap("Flowmap", 2D) = "white" {}
		_Flowmap_Value("Flowmap_Value", Float) = 0
		_Flowmap_Scale1("Flowmap_Scale1", Float) = 3
		_Flowmap_Scale2("Flowmap_Scale2", Float) = 2.3
		_Flowmap_Scale3("Flowmap_Scale3", Float) = 3.34
		[Toggle(_ONLYBLURCAUSTIC_ON)] _OnlyBlurCaustic("OnlyBlurCaustic", Float) = 0
		_BluredWaves("BluredWaves", 2D) = "white" {}
		_Multiply_Blur("Multiply_Blur", Float) = 1.12
		_Blurred_Scale("Blurred_Scale", Float) = 1
		_Caustic_Speed_BLUR("Caustic_Speed_BLUR", Float) = 1

	}
	

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        LOD 100
        // Project after opaque depth is available, without writing the volume's
        // back face into the depth buffer. Preserve multiplicative brightening.
        Blend DstColor One
        Cull Front
        ZWrite Off
        ZTest Always
        ColorMask RGB

        Pass
        {
            Name "CausticDecal"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CausticVertex
            #pragma fragment CausticFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ONLYBLURCAUSTIC_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_Waves); SAMPLER(sampler_Waves);
            TEXTURE2D(_Flowmap); SAMPLER(sampler_Flowmap);
            TEXTURE2D(_BluredWaves); SAMPLER(sampler_BluredWaves);
            CBUFFER_START(UnityPerMaterial)
                float _FadeRadius, _FadeStrength, _Add, _Add_Blur;
                float _Scale, _GiantMask_Scale, _CausticWave1_Multiply, _Multiply_Caustics;
                float _Caustic_Scale1, _Caustic_Scale2, _Caustic_Scale3;
                float _Caustic_Speed1, _Caustic_Speed2, _Caustic_Speed3;
                float _Flowmap_Value, _Flowmap_Scale1, _Flowmap_Scale2, _Flowmap_Scale3;
                float _OnlyBlurCaustic, _Multiply_Blur, _Blurred_Scale, _Caustic_Speed_BLUR;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CausticVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 CausticFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    clip(rawDepth - 0.000001);
                    float deviceDepth = rawDepth;
                #else
                    clip(0.999999 - rawDepth);
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                // UNITY_MATRIX_I_VP and the texture array slice are both per-eye.
                float3 worldPosition = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                float3 positionOS = TransformWorldToObject(worldPosition);
                // Preserve the graph's XY bounds and spherical edge falloff.
                clip(0.5 - abs(positionOS.xy));
                float edgeFade = 1.0 - saturate(
                    (length(positionOS) - _FadeRadius) / max(0.00001, 1.0 - _FadeStrength));
                float2 uv = worldPosition.xz * _Scale;
                float2 uv1 = lerp(uv, SAMPLE_TEXTURE2D(_Flowmap, sampler_Flowmap, uv * _Flowmap_Scale1).rg, _Flowmap_Value) * _Caustic_Scale1;
                float2 uv2 = lerp(uv, SAMPLE_TEXTURE2D(_Flowmap, sampler_Flowmap, uv * _Flowmap_Scale2).rg, _Flowmap_Value) * _Caustic_Scale2;
                float2 uv3 = lerp(uv, SAMPLE_TEXTURE2D(_Flowmap, sampler_Flowmap, uv * _Flowmap_Scale3).rg, _Flowmap_Value) * _Caustic_Scale3;
                float3 time = _Time.y * float3(_Caustic_Speed1, _Caustic_Speed2, _Caustic_Speed3);
                const float2 direction1 = float2(-0.012, -0.0134);
                const float2 direction2 = float2(0.015, 0.02);
                const float2 direction3 = float2(0.01, -0.04);

                #if !defined(_ONLYBLURCAUSTIC_ON)
                    half sharpA = (SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv1 + time.x * direction1).g
                                 + SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv2 + time.y * direction2).g)
                                 * _CausticWave1_Multiply
                                 * SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv3 + time.z * direction3).g;
                    half sharpB = (SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv1 + time.z * direction1).g
                                 + SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv2 + time.y * direction2).g)
                                 * _CausticWave1_Multiply
                                 * SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv3 + time.x * direction3).g;
                    half sharp = lerp(sharpA, sharpB,
                        SAMPLE_TEXTURE2D(_Waves, sampler_Waves, uv * _GiantMask_Scale).g) * _Multiply_Caustics;
                #endif

                time *= _Caustic_Speed_BLUR;
                half blurA = (SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv1 + time.x * direction1).g
                            + SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv2 + time.y * direction2).g)
                            * _CausticWave1_Multiply
                            * SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv3 + time.z * direction3).g;
                half blurB = (SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv1 + time.z * direction1).g
                            + SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv2 + time.y * direction2).g)
                            * _CausticWave1_Multiply
                            * SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves, uv3 + time.x * direction3).g;
                half caustic = lerp(blurA, blurB,
                    SAMPLE_TEXTURE2D(_BluredWaves, sampler_BluredWaves,
                        uv * _GiantMask_Scale * _Blurred_Scale).g) * _Multiply_Blur + _Add_Blur * 0.01;
                #if !defined(_ONLYBLURCAUSTIC_ON)
                    caustic *= sharp;
                #endif
                // This was an explicit exponential attenuation in the source graph.
                float fog = saturate(exp2(-unity_FogParams.y * distance(worldPosition, GetCameraPositionWS())));
                half intensity = fog * edgeFade * (2.0 * caustic + _Add * 0.01);
                return half4(intensity, intensity, intensity, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
