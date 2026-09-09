// Hand-ported to URP 17; original material properties and shader GUID are preserved.
Shader "Davis3D/OceanEnviroment2/ParticlesBubbles"
{
	Properties
	{
		_FadeDistance("FadeDistance", Float) = 1000
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_TintColor("TintColor", Color) = (1,1,1,1)
		_Refraction("Refraction", 2D) = "white" {}
		_Refraction_A("Refraction_A", Float) = 1.1
		_Refraction_B("Refraction_B", Float) = 2.1
		_Refraction_UV_Add("Refraction_UV_Add", Float) = 3
		_Refraction_Waviness("Refraction_Waviness", 2D) = "white" {}
		[Header(Refraction)]
		_ChromaticAberration("Chromatic Aberration", Range( 0 , 0.3)) = 0.1
		_Refraction_Waviness_Intensity("Refraction_Waviness_Intensity", Float) = 0.15
		_Normal("Normal", 2D) = "bump" {}
		_Metallic("Metallic", Float) = 0.75
		_Roughness("Roughness", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}


    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ParticlesBubbles"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanEffectVertex
            #pragma fragment BubbleFragment
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
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #include "OceanEffectsURP.hlsl"
            // URP's TEXTURE2D_X opaque color replaces the unsupported GrabPass.
            // Enable Opaque Texture on the active pipeline asset/camera.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_Refraction); SAMPLER(sampler_Refraction);
            TEXTURE2D(_Refraction_Waviness); SAMPLER(sampler_Refraction_Waviness);
            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _texcoord_ST;
                float _FadeDistance, _Cutoff, _Refraction_A, _Refraction_B;
                float _Refraction_UV_Add, _ChromaticAberration, _Refraction_Waviness_Intensity;
                float _Metallic, _Roughness;
                int __dirty;
            CBUFFER_END

            half4 BubbleFragment(OceanEffectVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv * _texcoord_ST.xy + _texcoord_ST.zw;
                half waveA = SAMPLE_TEXTURE2D(_Refraction_Waviness, sampler_Refraction_Waviness,
                    uv + _Time.y * float2(0.2, 0)).g;
                half waveB = SAMPLE_TEXTURE2D(_Refraction_Waviness, sampler_Refraction_Waviness,
                    uv + _Time.y * float2(0, 0.4)).g;
                float2 refractUV = uv + _Refraction_Waviness_Intensity * lerp(waveA, waveB, 0.5)
                    + _Refraction_UV_Add / 100.0;
                half refractionMask = SAMPLE_TEXTURE2D(_Refraction, sampler_Refraction, refractUV).g;
                half maskAlpha = refractionMask * _TintColor.a;
                clip(maskAlpha - _Cutoff);
                float distanceFade = saturate(1.0 - distance(GetCameraPositionWS(), input.positionWS)
                    / OceanSafeDenominator(_FadeDistance));

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _TintColor.rgb;
                surface.normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, refractUV));
                surface.metallic = _Metallic * distanceFade;
                surface.smoothness = (1.0 - _Roughness) * distanceFade;
                surface.occlusion = 1;
                surface.alpha = saturate(maskAlpha * distanceFade);
                InputData lightingInput = OceanEffectInputData(input, surface.normalTS);
                half4 lit = UniversalFragmentPBR(lightingInput, surface);

                float indexOfRefraction = lerp(_Refraction_A, _Refraction_B, refractionMask);
                float3 normalVS = TransformWorldToViewDir(lightingInput.normalWS);
                float2 offset = (indexOfRefraction - 1.0) * normalVS.xy
                    * (1.0 - dot(lightingInput.normalWS, lightingInput.viewDirectionWS));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 background;
                background.r = SampleSceneColor(screenUV + offset).r;
                background.g = SampleSceneColor(screenUV + offset * (1.0 - _ChromaticAberration)).g;
                background.b = SampleSceneColor(screenUV + offset * (1.0 + _ChromaticAberration)).b;

                // Scene color already contains fog. Fog only the bubble contribution;
                // adding fog to the composited scene a second time would wash it out.
                lit.rgb = MixFogColor(lit.rgb, unity_FogColor.rgb * surface.alpha, lightingInput.fogCoord);
                // Like the original final-color function, output the complete
                // refracted composite at alpha one. Only opaque scene color can be
                // refracted in URP; other transparent particles are not in this copy.
                return half4(lit.rgb + background * (1.0 - surface.alpha), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
