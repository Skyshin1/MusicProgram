Shader "Hidden/Sonar/Quest Flashlight Beam"
{
    Properties
    {
        _BeamColor("Beam Color", Color) = (0.7, 0.9, 1, 0.18)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
        }
        Pass
        {
            Name "Quest Flashlight Beam"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float lengthFade = saturate(input.positionOS.z) *
                                   (1.0 - smoothstep(0.72, 1.0, input.positionOS.z));
                float edge = 1.0 - saturate(abs(dot(
                    normalize(input.normalWS),
                    normalize(_WorldSpaceCameraPos - input.positionWS))));
                float alpha = _BeamColor.a * lengthFade * (0.25 + edge * 0.75);
                return half4(_BeamColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
