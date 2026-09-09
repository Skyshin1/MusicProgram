// Hand-ported to URP 17; original material properties and shader GUID are preserved.
Shader "Davis3D/OceanEnviroment2/DustMote"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_DustTex("DustTex", 2D) = "white" {}
		_Glow("Glow", Float) = 0.01
		_Wave_Push_Intensity("Wave_Push_Intensity", Float) = 0
		_Wave_Push_Speed("Wave_Push_Speed", Float) = 0.1
		_Opacity("Opacity", Float) = 0
		_DepthFadeLength("DepthFadeLength", Float) = 100
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}


    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "IsEmissive"="true" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DustMote"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DustVertex
            #pragma fragment DustFragment
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
            #include "OceanEffectsURP.hlsl"

            TEXTURE2D(_DustTex); SAMPLER(sampler_DustTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _DustTex_ST;
                float4 _texcoord_ST;
                float _Cutoff, _Glow, _Wave_Push_Intensity, _Wave_Push_Speed;
                float _Opacity, _DepthFadeLength;
                int __dirty;
            CBUFFER_END

            OceanEffectVaryings DustVertex(OceanEffectAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _Wave_Push_Speed * -0.5;
                float3 longWave = abs(frac(float3(0, time, 0) + worldPosition / 10.24 + 0.5) * 2.0 - 1.0);
                longWave = (3.0 - 2.0 * longWave) * longWave * longWave;
                float3 shortWave = abs(frac(time + worldPosition / 2.0 + 0.5) * 2.0 - 1.0);
                shortWave = (3.0 - 2.0 * shortWave) * shortWave * shortWave;
                float angle = longWave.y + length(shortWave);
                float sine, cosine;
                sincos(angle, sine, cosine);
                // The source graph rotates the origin about (0,0,-10) around X,
                // then applies that world-driven offset in object space.
                input.positionOS.xyz += float3(0, -10.0 * sine, 10.0 * cosine - 10.0) * _Wave_Push_Intensity;
                return OceanEffectVertex(input);
            }

            half4 DustFragment(OceanEffectVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv * _texcoord_ST.xy + _texcoord_ST.zw;
                uv = uv * _DustTex_ST.xy + _DustTex_ST.zw;
                half dust = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex, uv).g;
                // This is a camera-near fade, not a scene-depth intersection fade.
                float cameraFade = (input.eyeDepth - _ProjectionParams.y) / OceanSafeDenominator(_DepthFadeLength);
                float alpha = dust * _Color.a * cameraFade * _Opacity;
                clip(alpha - _Cutoff);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _Color.rgb * dust;
                surface.emission = _Color.rgb * _Glow;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = saturate(alpha);
                InputData lightingInput = OceanEffectInputData(input, surface.normalTS);
                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, lightingInput.fogCoord);
                color.a = surface.alpha;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
