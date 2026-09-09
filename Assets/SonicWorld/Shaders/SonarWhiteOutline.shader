Shader "Hidden/Sonar/White Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass
        {
            Name "Sonar White Outline"
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
                float _OutlineStrength;
                float4 _OutlineColor;
            CBUFFER_END
            float4 _SonarOutlineDrawColor;
            float _SonarOutlineMaximumY;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float worldY : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 expandedWS = TransformObjectToWorld(input.positionOS.xyz) +
                    TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(expandedWS);
                output.worldY = expandedWS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_SonarOutlineMaximumY - input.worldY);
                float sceneDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS.xy));
                float sceneEyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                float outlineEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                clip(sceneEyeDepth + 0.01 - outlineEyeDepth);
                return half4(_SonarOutlineDrawColor.rgb, saturate(_SonarOutlineDrawColor.a * _OutlineStrength));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
