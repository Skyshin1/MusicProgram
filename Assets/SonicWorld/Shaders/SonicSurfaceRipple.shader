Shader "SonicWorld/Surface Ripple"
{
    Properties
    {
        [HDR] _RippleTint ("Ripple Tint", Color) = (0.05, 1.4, 2.2, 1)
        _RippleDisplacement ("Displacement", Range(0, 0.06)) = 0.025
        _RippleBrightness ("Brightness", Range(0.5, 8)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Sonic Surface Ripple"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RippleTint;
                float _RippleDisplacement;
                float _RippleBrightness;
            CBUFFER_END

            float4 _SonicRippleOrigins[4];
            float4 _SonicRippleData[4];
            float4 _SonicRippleColors[4];
            int _SonicRippleCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float RippleEnvelope(float distanceToOrigin, float4 origin, float4 data)
            {
                float age = max(0.0, _Time.y - origin.w);
                float front = age * data.y;
                float width = max(0.015, data.z);
                float offset = (distanceToOrigin - front) / width;
                return exp(-offset * offset * 2.2) *
                    exp(-age * 1.35) *
                    data.x *
                    data.w;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float displacement = 0.0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float active = step(i, _SonicRippleCount - 1);
                    float distanceToOrigin = distance(positionWS, _SonicRippleOrigins[i].xyz);
                    displacement += RippleEnvelope(
                        distanceToOrigin,
                        _SonicRippleOrigins[i],
                        _SonicRippleData[i]) * active;
                }

                positionWS += normalWS * displacement * _RippleDisplacement;
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = 0;
                float intensity = 0.0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float active = step(i, _SonicRippleCount - 1);
                    float distanceToOrigin =
                        distance(input.positionWS, _SonicRippleOrigins[i].xyz);
                    float ring = RippleEnvelope(
                        distanceToOrigin,
                        _SonicRippleOrigins[i],
                        _SonicRippleData[i]) * active;
                    color += _SonicRippleColors[i].rgb * ring;
                    intensity += ring;
                }

                half fresnel = pow(
                    1.0h - saturate(dot(
                        normalize(input.normalWS),
                        normalize(GetWorldSpaceViewDir(input.positionWS)))),
                    2.0h);
                color = color * _RippleTint.rgb * _RippleBrightness;
                half alpha = saturate(intensity * 0.9h + fresnel * intensity * 0.35h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
