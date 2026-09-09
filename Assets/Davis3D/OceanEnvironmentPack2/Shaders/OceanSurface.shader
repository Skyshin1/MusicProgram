// Hand-ported to URP 17; original material properties and shader GUID are preserved.
Shader "Davis3D/OceanEnviroment2/OceanSurface"
{
	Properties
	{
		_Cubemap("Cubemap", CUBE) = "white" {}
		_Cubemap_Brightness("Cubemap_Brightness", Float) = 1
		_Cubemap_Clamp_Max("Cubemap_Clamp_Max", Float) = 10
		_Cubemap_Power("Cubemap_Power", Float) = 1
		_Exp_In("Exp_In", Float) = 5
		_NormFresnelPower("Norm Fresnel Power", Float) = 1
		_NormIntensityAbove("Norm Intensity Above", Float) = 1
		_NormIntensityDistant("Norm Intensity Distant", Float) = 1
		_RefFraction("RefFraction", Float) = 0.04
		_DeepSeaBrightness("DeepSea Brightness", Float) = 0
		_DeepZHeight("Deep ZHeight", Float) = -2000
		_Normal("Normal", 2D) = "bump" {}
		_Water_Intensity_5("Water_Intensity_5", Float) = 3
		_Water_MASTER_Scale("Water_MASTER_Scale", Float) = 1
		_Water_Tile1("Water_Tile1", Float) = 50
		_Water_Tile2("Water_Tile2", Float) = 40
		_Water_Tile3("Water_Tile3", Float) = 50
		_Water_Tile4("Water_Tile4", Float) = 40
		_Water_Tile5("Water_Tile5", Float) = 10
		_SPEED_MASTER("SPEED_MASTER", Float) = 1
		_Water_Speed1("Water_Speed1", Float) = 0.1
		_Water_Speed2("Water_Speed2", Float) = 0.1
		_Water_Speed3("Water_Speed3", Float) = 0.1
		_Water_Speed4("Water_Speed4", Float) = 0.1
		_Water_Speed5("Water_Speed5", Float) = 0.01
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}


    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "IsEmissive"="true" }
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #define _SURFACE_TYPE_TRANSPARENT 1
        #include "OceanEffectsURP.hlsl"
        TEXTURECUBE(_Cubemap); SAMPLER(sampler_Cubemap);
        TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
        CBUFFER_START(UnityPerMaterial)
            float4 _texcoord_ST;
            float _Cubemap_Brightness, _Cubemap_Clamp_Max, _Cubemap_Power;
            float _Exp_In, _NormFresnelPower, _NormIntensityAbove, _NormIntensityDistant;
            float _RefFraction, _DeepSeaBrightness, _DeepZHeight;
            float _Water_Intensity_5, _Water_MASTER_Scale;
            float _Water_Tile1, _Water_Tile2, _Water_Tile3, _Water_Tile4, _Water_Tile5;
            float _SPEED_MASTER, _Water_Speed1, _Water_Speed2, _Water_Speed3, _Water_Speed4, _Water_Speed5;
            int __dirty;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "OceanSurface"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanEffectVertex
            #pragma fragment OceanSurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            half4 OceanSurfaceFragment(OceanEffectVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = (input.uv * _texcoord_ST.xy + _texcoord_ST.zw) * _Water_MASTER_Scale;
                float time = _Time.y * _SPEED_MASTER;
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv * _Water_Tile1 + time * _Water_Speed1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv * _Water_Tile2 + time * _Water_Speed2));
                half3 n3 = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv * _Water_Tile3 + time * _Water_Speed3));
                half3 n4 = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv * _Water_Tile4 + time * _Water_Speed4));
                half3 n5 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv * _Water_Tile5 + time * _Water_Speed5), _Water_Intensity_5);
                half3 waves = OceanBlendNormals(OceanBlendNormals(n1, n2),
                    OceanBlendNormals(OceanBlendNormals(n3, n4), n5));
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = _RefFraction + _Exp_In * pow(max(0.0, 1.0 - dot(normalWS, viewDirection)), _NormFresnelPower);
                half3 waveNormal = lerp(
                    lerp(half3(0, 0, 1), waves, _NormIntensityAbove),
                    lerp(half3(0, 0, 1), waves, _NormIntensityDistant), fresnel);

                // Preserve the authored cubemap convention, including its Z flip.
                float3 reflection = reflect(viewDirection, normalWS);
                reflection.z *= -1.0;
                half3x3 tangentToWorld = OceanEffectTangentToWorld(input);
                half3 reflectionTS = mul(tangentToWorld, reflection);
                half3 sampleDirection = TransformTangentToWorld(
                    OceanBlendNormals(reflectionTS, waveNormal), tangentToWorld);
                half3 cube = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, sampleDirection).rgb;
                cube = clamp(pow(max(cube, 0.0), _Cubemap_Power), half3(-100, 0, 0), _Cubemap_Clamp_Max);
                float deepFade = (GetCameraPositionWS().z - _DeepZHeight)
                    / OceanSafeDenominator(2.0 * abs(_DeepZHeight));
                half brightness = lerp(_DeepSeaBrightness, 1.0, deepFade);

                SurfaceData surface = (SurfaceData)0;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.emission = cube * _Cubemap_Brightness * brightness;
                InputData lightingInput = OceanEffectInputData(input, surface.normalTS);
                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, lightingInput.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanEffectShadowVertex
            #pragma fragment OceanEffectShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    Fallback Off
}
