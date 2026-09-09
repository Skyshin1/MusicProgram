// Hand-ported to URP 17; original material properties and shader GUID are preserved.
Shader "Davis3D/OceanEnviroment2/GodRay"
{
	Properties
	{
		_OpacityMask("Opacity Mask", 2D) = "white" {}
		_FadeDistance("FadeDistance", Float) = 1000
		_FadeLength("FadeLength", Float) = 150
		_FadeOffset("FadeOffset", Float) = 50
		_Glow("Glow", Float) = 3
		_Panner_A_UTile("Panner_A_UTile", Float) = 25
		_Panner_A_VTile("Panner_A_VTile", Float) = 0
		_Panner_B_UTile("Panner_B_UTile", Float) = 5
		_Panner_B_VTile("Panner_B_VTile", Float) = 0
		_SinVal("SinVal", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}


    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" "IsEmissive"="true" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "GodRay"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanEffectVertex
            #pragma fragment GodRayFragment
            #pragma multi_compile_instancing
            #include "OceanEffectsURP.hlsl"

            TEXTURE2D(_OpacityMask); SAMPLER(sampler_OpacityMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _OpacityMask_ST;
                float4 _texcoord_ST;
                float _FadeDistance, _FadeLength, _FadeOffset, _Glow;
                float _Panner_A_UTile, _Panner_A_VTile, _Panner_B_UTile, _Panner_B_VTile;
                float _SinVal;
                int __dirty;
            CBUFFER_END

            half4 GodRayFragment(OceanEffectVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv * _texcoord_ST.xy + _texcoord_ST.zw;
                half3 emission = _Glow * SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask,
                    uv * _OpacityMask_ST.xy + _OpacityMask_ST.zw).rgb;
                float2 pannerA = uv * float2(_Panner_A_UTile, _Panner_A_VTile) + _Time.y * float2(0.5, 0);
                float2 pannerB = uv * float2(_Panner_B_UTile, _Panner_B_VTile) + _Time.y * float2(-0.5, 0);
                float wave = (sin(pannerA.x) * sin(pannerB.x) + 0.8) * (_SinVal / 1000.0);
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndotv = dot(NormalizeNormalPerPixel(input.normalWS), viewDirection);
                float fresnelBase = max(0.0, 1.0 - ndotv);
                float angleFade = (1.0 - pow(fresnelBase, 0.3)) * (1.0 - fresnelBase * fresnelBase);
                float distanceFade = saturate(1.0 - distance(GetCameraPositionWS(), input.positionWS)
                    / OceanSafeDenominator(_FadeDistance));
                float nearFade = (input.eyeDepth - _ProjectionParams.y - _FadeOffset)
                    / OceanSafeDenominator(_FadeLength);
                half alpha = saturate(distanceFade * wave * angleFade * nearFade);
                // Original rays are emissive, additive, two-sided, unshadowed and unfogged.
                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
