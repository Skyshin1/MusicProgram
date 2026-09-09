// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Davis3D/OceanEnviroment2/CoralSun"
{
	Properties
	{
		_DifColor_BeforeHue("DifColor_BeforeHue", Color) = (1,1,1,1)
		_Diffuse("Diffuse", 2D) = "white" {}
		_Brightness("Brightness", Float) = 1
		_Contrast("Contrast", Float) = 1
		_Desaturation("Desaturation", Float) = 0
		[Toggle(_RANDOMHUE_ON)] _RandomHue("RandomHue", Float) = 0
		_Hue("Hue", Float) = 1
		_Hue_Variation_Intensity("Hue_Variation_Intensity", Float) = 0.2
		[Toggle(_CUSTOMROUGHNESS_ON)] _CustomRoughness("CustomRoughness", Float) = 0
		_Metallic("Metallic", Float) = 0
		_Roughness_Min("Roughness_Min", Float) = 0
		_Roughness_Max("Roughness_Max", Float) = 1
		_Roughness_A("Roughness_A", Float) = 1
		_Roughness_B("Roughness_B", Float) = 1
		_Normal("Normal", 2D) = "bump" {}
		[Toggle(_FUZZYSHADING_ON)] _FuzzyShading("FuzzyShading", Float) = 0
		[Toggle(_GLOW_ON)] _Glow("Glow", Float) = 0
		_GlowColor("GlowColor", Color) = (0,0.6901961,1,1)
		_GlowIntensity("Glow Intensity", Float) = 5
		_Shallow_ZHeight("Shallow_ZHeight", Float) = 1000
		_Deep_ZHeight("Deep_ZHeight", Float) = -2000
		[Toggle(_WIND_ON)] _Wind("Wind", Float) = 0
		_Wind_Intensity("Wind_Intensity", Float) = 1
		_Wind_Speed("Wind_Speed", Float) = 0.1
		_VerticalGradient("VerticalGradient", 2D) = "white" {}
		_Tentacle_Wind_Amount("Tentacle_Wind_Amount", Float) = 3
		_Tentacle_Speed("Tentacle_Speed", Float) = 0.2
		[HideInInspector] _texcoord4( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    // URP port; original authored graph equations below are retained.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        Cull Back
        HLSLINCLUDE
        #include "OceanSurfaceCompatURP.hlsl"
        
		#pragma shader_feature_local _WIND_ON
		#pragma shader_feature_local _FUZZYSHADING_ON
		#pragma shader_feature_local _RANDOMHUE_ON
		#pragma shader_feature_local _GLOW_ON
		#pragma shader_feature_local _CUSTOMROUGHNESS_ON
        #define OCEAN_VERTEX_ANIMATION 1
#define OCEAN_CORAL_SUN 1

        sampler2D _VerticalGradient;
sampler2D _Normal;
sampler2D _Diffuse;
CBUFFER_START(UnityPerMaterial)
float _Wind_Speed;
float _Wind_Intensity;
float _Tentacle_Wind_Amount;
float _Tentacle_Speed;
float4 _Normal_ST;
float _Hue;
float _Hue_Variation_Intensity;
float4 _DifColor_BeforeHue;
float4 _Diffuse_ST;
float _Contrast;
float _Desaturation;
float _Brightness;
float4 _GlowColor;
float _GlowIntensity;
float _Deep_ZHeight;
float _Shallow_ZHeight;
float _Metallic;
float _Roughness_A;
float _Roughness_B;
float _Roughness_Min;
float _Roughness_Max;
CBUFFER_END

		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float2 ase_texcoord5;
			float2 uv4_texcoord4;
			half ASEVFace;
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
			float2 panner106 = ( 1.0 * _Time.y * temp_cast_4 + v.ase_texcoord4.xy);
			float2 temp_cast_5 = (( _Tentacle_Speed * 0.5 )).xx;
			float2 panner107 = ( 1.0 * _Time.y * temp_cast_5 + v.ase_texcoord4.xy);
			float2 temp_cast_6 = (( _Tentacle_Speed * 0.358 )).xx;
			float2 panner108 = ( 1.0 * _Time.y * temp_cast_6 + v.ase_texcoord4.xy);
			float3 appendResult98 = (float3(tex2Dlod( _VerticalGradient, float4( panner106, 0, 0.0) ).g , tex2Dlod( _VerticalGradient, float4( panner107, 0, 0.0) ).g , tex2Dlod( _VerticalGradient, float4( panner108, 0, 0.0) ).g));
			#ifdef _WIND_ON
				float3 staticSwitch75 = ( ( ( ( ( rotatedValue6_g613 * 0.01 ) * _Wind_Intensity ) + temp_output_8_0_g613 ) * v.texcoord2.xy.y ) + ( _Tentacle_Wind_Amount * ( appendResult98 * v.texcoord3.xy.y ) ) );
			#else
				float3 staticSwitch75 = float3( 0,0,0 );
			#endif
			v.vertex.xyz += staticSwitch75;
			v.vertex.w = 1;
			o.ase_texcoord5 = v.ase_texcoord4.xy;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			o.Normal = UnpackNormal( tex2D( _Normal, uv_Normal ) );
			float lerpResult88 = lerp( tex2D( _VerticalGradient, i.ase_texcoord5.xy ).g , 1.0 , ( 1.0 - i.uv4_texcoord4.y ));
			float4 color5_g603 = IsGammaSpace() ? float4(1,1,1,1) : float4(1,1,1,1);
			float4 normalizeResult6_g603 = normalize( color5_g603 );
			float4 transform33 = mul(unity_WorldToObject,float4( 0,0,0,1 ));
			float2 appendResult34 = (float2(transform33.x , transform33.z));
			float dotResult4_g592 = dot( appendResult34 , float2( 12.9898,78.233 ) );
			float lerpResult10_g592 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g592 ) * 43758.55 ) ));
			float lerpResult39 = lerp( ( _Hue - _Hue_Variation_Intensity ) , ( _Hue + _Hue_Variation_Intensity ) , lerpResult10_g592);
			#ifdef _RANDOMHUE_ON
				float staticSwitch40 = lerpResult39;
			#else
				float staticSwitch40 = _Hue;
			#endif
			float3 temp_cast_1 = (0.0).xxx;
			float2 uv_Diffuse = i.uv_texcoord * _Diffuse_ST.xy + _Diffuse_ST.zw;
			float4 tex2DNode27 = tex2D( _Diffuse, uv_Diffuse );
			float3 temp_output_3_0_g603 = ( _DifColor_BeforeHue * tex2DNode27 ).rgb;
			float3 rotatedValue2_g603 = RotateAroundAxis( temp_cast_1, temp_output_3_0_g603, normalizeResult6_g603.rgb, staticSwitch40 );
			float3 temp_output_1_0_g607 = ( rotatedValue2_g603 + temp_output_3_0_g603 );
			float3 normalizeResult2_g607 = SafeNormalize( temp_output_1_0_g607 );
			float3 desaturateInitialColor5_g607 = temp_output_1_0_g607;
			float desaturateDot5_g607 = dot( desaturateInitialColor5_g607, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar5_g607 = lerp( desaturateInitialColor5_g607, desaturateDot5_g607.xxx, 1.0 );
			float3 temp_cast_3 = (_Contrast).xxx;
			float3 desaturateInitialColor17 = ( normalizeResult2_g607 * pow( max(desaturateVar5_g607, 0.0) , temp_cast_3 ) );
			float desaturateDot17 = dot( desaturateInitialColor17, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar17 = lerp( desaturateInitialColor17, desaturateDot17.xxx, _Desaturation );
			float3 temp_output_12_0 = ( ( lerpResult88 * desaturateVar17 ) * _Brightness );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = Unity_SafeNormalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 appendResult2_g614 = (float3(1.0 , 1.0 , i.ASEVFace));
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_worldTangent = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float3 ase_worldBitangent = WorldNormalVector( i, float3( 0, 1, 0 ) );
			float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
			float3 tangentToWorldDir10_g614 = mul( ase_tangentToWorldFast, ( float3( 0,0,1 ) * appendResult2_g614 ) );
			float dotResult12_g614 = dot( ase_worldViewDir , tangentToWorldDir10_g614 );
			float clampResult13_g614 = clamp( dotResult12_g614 , 0.0 , 1.0 );
			float3 temp_output_26_0_g614 = temp_output_12_0;
			float3 desaturateInitialColor27_g614 = temp_output_26_0_g614;
			float desaturateDot27_g614 = dot( desaturateInitialColor27_g614, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar27_g614 = lerp( desaturateInitialColor27_g614, desaturateDot27_g614.xxx, 0.5 );
			float clampResult21_g614 = clamp( ( pow( ( 1.0 - clampResult13_g614 ) , 6.0 ) * 0.8 ) , 0.0 , 1.0 );
			float3 lerpResult23_g614 = lerp( ( ( 1.0 - ( clampResult13_g614 * 0.8 ) ) * temp_output_26_0_g614 ) , ( desaturateVar27_g614 * float3( 1.5,1.5,1.5 ) ) , clampResult21_g614);
			#ifdef _FUZZYSHADING_ON
				float3 staticSwitch9 = lerpResult23_g614;
			#else
				float3 staticSwitch9 = temp_output_12_0;
			#endif
			o.Albedo = staticSwitch9;
			float temp_output_64_0 = distance( _Deep_ZHeight , _Shallow_ZHeight );
			float clampResult57 = clamp( ( 1.0 - ( ( ase_worldPos.y - ( _Deep_ZHeight - ( temp_output_64_0 / 2.0 ) ) ) / temp_output_64_0 ) ) , 0.0 , 1.0 );
			#ifdef _GLOW_ON
				float4 staticSwitch55 = ( ( ( tex2DNode27.a * _GlowColor ) * _GlowIntensity ) * clampResult57 );
			#else
				float4 staticSwitch55 = float4( 0,0,0,0 );
			#endif
			o.Emission = staticSwitch55.rgb;
			o.Metallic = _Metallic;
			float lerpResult49 = lerp( 0.0 , _Roughness_A , _Roughness_B);
			float lerpResult44 = lerp( _Roughness_A , _Roughness_B , tex2DNode27.g);
			float clampResult43 = clamp( lerpResult44 , _Roughness_Min , _Roughness_Max );
			#ifdef _CUSTOMROUGHNESS_ON
				float staticSwitch41 = clampResult43;
			#else
				float staticSwitch41 = lerpResult49;
			#endif
			o.Smoothness = ( 1.0 - staticSwitch41 );
			o.Alpha = 1;
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
