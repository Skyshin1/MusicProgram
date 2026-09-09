// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Davis3D/OceanEnviroment2/Kelp"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_DifColor_BeforeHue("DifColor_BeforeHue", Color) = (1,1,1,1)
		_Diffuse("Diffuse", 2D) = "white" {}
		_Brightness("Brightness", Float) = 1
		_Contrast("Contrast", Float) = 1
		_RandomLeafContrast("RandomLeafContrast", Float) = 0.5
		_Desaturation("Desaturation", Float) = 0
		_Hue("Hue", Float) = 1
		[Toggle(_RANDOMHUE_ON)] _RandomHue("RandomHue", Float) = 0
		_Hue_Variation_Intensity("Hue_Variation_Intensity", Float) = 0.2
		[Toggle(_CUSTOMROUGHNESS_ON)] _CustomRoughness("CustomRoughness", Float) = 0
		_Metallic("Metallic", Float) = 0
		_Roughness_Min("Roughness_Min", Float) = 0
		_Roughness_Max("Roughness_Max", Float) = 1
		_Roughness_A("Roughness_A", Float) = 1
		_Roughness_B("Roughness_B", Float) = 1
		_Normal1("Normal", 2D) = "bump" {}
		[Toggle(_GLOWON_ON)] _GlowOn("GlowOn", Float) = 0
		_Glow("Glow", 2D) = "white" {}
		_GlowColor("GlowColor", Color) = (0,0.6901961,1,1)
		_GlowIntensity("Glow Intensity", Float) = 5
		_Shallow_ZHeight("Shallow_ZHeight", Float) = 1000
		_Deep_ZHeight("Deep_ZHeight", Float) = -2000
		[Toggle(_WIND_ON)] _Wind("Wind", Float) = 0
		_VerticalGradient("VerticalGradient", 2D) = "white" {}
		_Wind_Intensity("Wind_Intensity", Float) = 1
		_Wind_Speed("Wind_Speed", Float) = 0.1
		_Tentacle_Wind_Amount("Tentacle_Wind_Amount", Float) = 3
		_Tentacle_Speed("Tentacle_Speed", Float) = 0.2
		[Toggle(_FUZZYSHADING_ON)] _FuzzyShading("FuzzyShading", Float) = 0
		_FuzzyShading_Power("FuzzyShading_Power", Float) = 6
		_FuzzyShading_Dark("FuzzyShading_Dark", Float) = 0.8
		_FuzzyShading_Bright("FuzzyShading_Bright", Float) = 0.8
		[HideInInspector] _texcoord3( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    // URP port; original authored graph equations below are retained.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "UniversalMaterialType"="Lit" }
        Cull Off
        HLSLINCLUDE
        #include "OceanSurfaceCompatURP.hlsl"
        
		#pragma shader_feature_local _WIND_ON
		#pragma shader_feature_local _FUZZYSHADING_ON
		#pragma shader_feature_local _RANDOMHUE_ON
		#pragma shader_feature_local _GLOWON_ON
		#pragma shader_feature_local _CUSTOMROUGHNESS_ON
        #define OCEAN_VERTEX_ANIMATION 1
#define OCEAN_KELP 1

        sampler2D _VerticalGradient;
sampler2D _Normal1;
sampler2D _Diffuse;
sampler2D _Glow;
CBUFFER_START(UnityPerMaterial)
float _Wind_Speed;
float _Wind_Intensity;
float _Tentacle_Wind_Amount;
float _Tentacle_Speed;
float4 _Normal1_ST;
float _Hue;
float _Hue_Variation_Intensity;
float4 _DifColor_BeforeHue;
float4 _Diffuse_ST;
float _Contrast;
float _Desaturation;
float _RandomLeafContrast;
float _Brightness;
float _FuzzyShading_Dark;
float _FuzzyShading_Power;
float _FuzzyShading_Bright;
float4 _Glow_ST;
float4 _GlowColor;
float _GlowIntensity;
float _Deep_ZHeight;
float _Shallow_ZHeight;
float _Metallic;
float _Roughness_A;
float _Roughness_B;
float _Roughness_Min;
float _Roughness_Max;
float _Cutoff;
CBUFFER_END

		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			half ASEVFace;
			float2 ase_texcoord5;
			float2 uv3_texcoord3;
			float3 worldNormal;
			INTERNAL_DATA
		};

		float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
		{
			original -= center;
			float C = cos( angle );
			float S = sin( angle );
			float t = 1 - C;
			float m00 = t * u.x * u.x + C;
			float m01 = t * u.x * u.y - S * u.z;
			float m02 = t * u.x * u.z + S * u.y;
			float m10 = t * u.x * u.y + S * u.z;
			float m11 = t * u.y * u.y + C;
			float m12 = t * u.y * u.z - S * u.x;
			float m20 = t * u.x * u.z - S * u.y;
			float m21 = t * u.y * u.z + S * u.x;
			float m22 = t * u.z * u.z + C;
			float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
			return mul( finalMatrix, original ) + center;
		}

		void vertexDataFunc( inout OceanAttributes v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 _0101 = float4(0,1,0,1);
			float3 appendResult13_g613 = (float3(_0101.x , _0101.y , _0101.z));
			float3 normalizeResult14_g613 = normalize( appendResult13_g613 );
			float3 temp_cast_0 = (3.0).xxx;
			float temp_output_19_0_g613 = ( _0101.w * ( ( _Time.y * _Wind_Speed ) * -0.5 ) );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
			float3 temp_output_45_0_g613 = abs( ( ( frac( ( ( ( normalizeResult14_g613 * temp_output_19_0_g613 ) + ( ase_worldPos / 10.24 ) ) + 0.5 ) ) * 2.0 ) + -1.0 ) );
			float dotResult58_g613 = dot( normalizeResult14_g613 , ( ( ( temp_cast_0 - ( temp_output_45_0_g613 * 2.0 ) ) * temp_output_45_0_g613 ) * temp_output_45_0_g613 ) );
			float3 temp_cast_1 = (3.0).xxx;
			float3 temp_output_46_0_g613 = abs( ( ( frac( ( ( temp_output_19_0_g613 + ( ase_worldPos / 2.0 ) ) + 0.5 ) ) * 2.0 ) + -1.0 ) );
			float3 temp_cast_2 = (0.0).xxx;
			float3 temp_cast_3 = (0.0).xxx;
			float3 temp_output_8_0_g613 = temp_cast_3;
			float3 rotatedValue6_g613 = RotateAroundAxis( ( float3(0,0,-10) + temp_output_8_0_g613 ), temp_output_8_0_g613, cross( normalizeResult14_g613 , float3(0,0,1) ), ( dotResult58_g613 + distance( ( ( ( temp_cast_1 - ( temp_output_46_0_g613 * 2.0 ) ) * temp_output_46_0_g613 ) * temp_output_46_0_g613 ) , temp_cast_2 ) ) );
			float2 temp_cast_4 = (( _Tentacle_Speed * 0.3 )).xx;
			float2 panner29 = ( 1.0 * _Time.y * temp_cast_4 + v.texcoord2.xy);
			float2 temp_cast_5 = (( _Tentacle_Speed * 0.5 )).xx;
			float2 panner25 = ( 1.0 * _Time.y * temp_cast_5 + v.texcoord2.xy);
			float2 temp_cast_6 = (( _Tentacle_Speed * 0.358 )).xx;
			float2 panner31 = ( 1.0 * _Time.y * temp_cast_6 + v.texcoord2.xy);
			float3 appendResult43 = (float3(tex2Dlod( _VerticalGradient, float4( panner29, 0, 0.0) ).g , tex2Dlod( _VerticalGradient, float4( panner25, 0, 0.0) ).g , tex2Dlod( _VerticalGradient, float4( panner31, 0, 0.0) ).g));
			#ifdef _WIND_ON
				float3 staticSwitch77 = ( ( ( ( ( rotatedValue6_g613 * 0.01 ) * _Wind_Intensity ) + temp_output_8_0_g613 ) * v.texcoord3.xy.y ) + ( _Tentacle_Wind_Amount * ( appendResult43 * v.texcoord3.xy.y ) ) );
			#else
				float3 staticSwitch77 = float3( 0,0,0 );
			#endif
			v.vertex.xyz += staticSwitch77;
			v.vertex.w = 1;
			o.ase_texcoord5 = v.ase_texcoord4.xy;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal1 = i.uv_texcoord * _Normal1_ST.xy + _Normal1_ST.zw;
			float3 tex2DNode78 = UnpackNormal( tex2D( _Normal1, uv_Normal1 ) );
			float3 appendResult91 = (float3(tex2DNode78.r , tex2DNode78.g , ( 1.0 - tex2DNode78.b )));
			float3 switchResult90 = (((i.ASEVFace>0)?(tex2DNode78):(appendResult91)));
			o.Normal = switchResult90;
			float4 color5_g603 = IsGammaSpace() ? float4(1,1,1,1) : float4(1,1,1,1);
			float4 normalizeResult6_g603 = normalize( color5_g603 );
			float4 transform1 = mul(unity_WorldToObject,float4( 0,0,0,1 ));
			float2 appendResult2 = (float2(transform1.x , transform1.z));
			float dotResult4_g592 = dot( appendResult2 , float2( 12.9898,78.233 ) );
			float lerpResult10_g592 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g592 ) * 43758.55 ) ));
			float temp_output_8_0 = lerpResult10_g592;
			float lerpResult12 = lerp( ( _Hue - _Hue_Variation_Intensity ) , ( _Hue + _Hue_Variation_Intensity ) , temp_output_8_0);
			#ifdef _RANDOMHUE_ON
				float staticSwitch18 = lerpResult12;
			#else
				float staticSwitch18 = _Hue;
			#endif
			float3 temp_cast_1 = (0.0).xxx;
			float2 uv_Diffuse = i.uv_texcoord * _Diffuse_ST.xy + _Diffuse_ST.zw;
			float4 tex2DNode11 = tex2D( _Diffuse, uv_Diffuse );
			float3 temp_output_3_0_g603 = ( _DifColor_BeforeHue * tex2DNode11 ).rgb;
			float3 rotatedValue2_g603 = RotateAroundAxis( temp_cast_1, temp_output_3_0_g603, normalizeResult6_g603.rgb, staticSwitch18 );
			float3 temp_output_1_0_g607 = ( rotatedValue2_g603 + temp_output_3_0_g603 );
			float3 normalizeResult2_g607 = SafeNormalize( temp_output_1_0_g607 );
			float3 desaturateInitialColor5_g607 = temp_output_1_0_g607;
			float desaturateDot5_g607 = dot( desaturateInitialColor5_g607, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar5_g607 = lerp( desaturateInitialColor5_g607, desaturateDot5_g607.xxx, 1.0 );
			float3 temp_cast_3 = (_Contrast).xxx;
			float3 desaturateInitialColor47 = ( normalizeResult2_g607 * pow( max(desaturateVar5_g607, 0.0) , temp_cast_3 ) );
			float desaturateDot47 = dot( desaturateInitialColor47, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar47 = lerp( desaturateInitialColor47, desaturateDot47.xxx, _Desaturation );
			float2 panner83 = ( temp_output_8_0 * float2( 1,1 ) + i.ase_texcoord5.xy);
			float lerpResult42 = lerp( tex2D( _VerticalGradient, panner83 ).g , 1.0 , ( 1.0 - i.uv3_texcoord3.y ));
			float3 lerpResult85 = lerp( desaturateVar47 , ( lerpResult42 * desaturateVar47 ) , _RandomLeafContrast);
			float3 temp_output_64_0 = ( lerpResult85 * _Brightness );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = Unity_SafeNormalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 appendResult2_g614 = (float3(1.0 , 1.0 , i.ASEVFace));
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_worldTangent = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float3 ase_worldBitangent = WorldNormalVector( i, float3( 0, 1, 0 ) );
			float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
			float3 tangentToWorldDir10_g614 = mul( ase_tangentToWorldFast, ( tex2DNode78 * appendResult2_g614 ) );
			float dotResult12_g614 = dot( ase_worldViewDir , tangentToWorldDir10_g614 );
			float clampResult13_g614 = clamp( dotResult12_g614 , 0.0 , 1.0 );
			float3 temp_output_26_0_g614 = temp_output_64_0;
			float3 desaturateInitialColor27_g614 = temp_output_26_0_g614;
			float desaturateDot27_g614 = dot( desaturateInitialColor27_g614, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar27_g614 = lerp( desaturateInitialColor27_g614, desaturateDot27_g614.xxx, 0.5 );
			float clampResult21_g614 = clamp( ( pow( ( 1.0 - clampResult13_g614 ) , _FuzzyShading_Power ) * _FuzzyShading_Bright ) , 0.0 , 1.0 );
			float3 lerpResult23_g614 = lerp( ( ( 1.0 - ( clampResult13_g614 * _FuzzyShading_Dark ) ) * temp_output_26_0_g614 ) , ( desaturateVar27_g614 * float3( 1.5,1.5,1.5 ) ) , clampResult21_g614);
			#ifdef _FUZZYSHADING_ON
				float3 staticSwitch81 = lerpResult23_g614;
			#else
				float3 staticSwitch81 = temp_output_64_0;
			#endif
			o.Albedo = staticSwitch81;
			float2 uv_Glow = i.uv_texcoord * _Glow_ST.xy + _Glow_ST.zw;
			float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) ).xyz;
			float3 objToWorld26 = mul( unity_ObjectToWorld, float4( ase_vertex3Pos, 1 ) ).xyz;
			float temp_output_14_0 = distance( _Deep_ZHeight , _Shallow_ZHeight );
			float clampResult68 = clamp( ( 1.0 - ( ( objToWorld26.y - ( _Deep_ZHeight - ( temp_output_14_0 / 2.0 ) ) ) / temp_output_14_0 ) ) , 0.0 , 1.0 );
			#ifdef _GLOWON_ON
				float4 staticSwitch79 = ( ( ( tex2D( _Glow, uv_Glow ) * _GlowColor ) * _GlowIntensity ) * clampResult68 );
			#else
				float4 staticSwitch79 = float4( 0,0,0,0 );
			#endif
			o.Emission = staticSwitch79.rgb;
			o.Metallic = _Metallic;
			float lerpResult67 = lerp( 0.0 , _Roughness_A , _Roughness_B);
			float lerpResult56 = lerp( _Roughness_A , _Roughness_B , tex2DNode11.g);
			float clampResult70 = clamp( lerpResult56 , _Roughness_Min , _Roughness_Max );
			#ifdef _CUSTOMROUGHNESS_ON
				float staticSwitch75 = clampResult70;
			#else
				float staticSwitch75 = lerpResult67;
			#endif
			o.Smoothness = ( 1.0 - staticSwitch75 );
			o.Alpha = tex2DNode11.a;
			clip( tex2DNode11.a - _Cutoff );
		}

        #include "OceanSurfacePassesURP.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanFragment
            #pragma multi_compile_instancing
            
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fog

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanShadowVertex
            #pragma fragment OceanDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanDepthFragment
            #pragma multi_compile_instancing
            
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanMetaVertex
            #pragma fragment OceanMetaFragment
            #pragma multi_compile_instancing
            
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
