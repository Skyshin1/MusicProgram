// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Davis3D/OceanEnviroment2/Coral"
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
		_Metallic("Metallic", Float) = 0.1
		_Roughness_Min("Roughness_Min", Float) = 0
		_Roughness_Max("Roughness_Max", Float) = 1
		_Roughness_A("Roughness_A", Float) = 1
		_Roughness_B("Roughness_B", Float) = 1
		[Toggle(_DETAILNORMALENABLE_ON)] _DetailNormalEnable("DetailNormalEnable", Float) = 0
		_Normal("Normal", 2D) = "bump" {}
		_NormalDetail("Normal Detail", 2D) = "bump" {}
		_Normal_Detail_Scale("Normal_Detail_Scale", Float) = 1
		_Normal_Detail_Amount("Normal_Detail_Amount", Float) = 0.5
		_Macro("Macro", 2D) = "white" {}
		_Macro_Intensity("Macro_Intensity", Float) = 1
		_Macro_Scale("Macro_Scale", Float) = 50
		_Macro_Triplanar_Blending("Macro_Triplanar_Blending", Float) = 1
		[Toggle(_GLOW_ON)] _Glow("Glow", Float) = 0
		_GlowColor("GlowColor", Color) = (0,0.6901961,1,1)
		_GlowIntensity("Glow Intensity", Float) = 5
		_Shallow_ZHeight("Shallow_ZHeight", Float) = 1000
		_Deep_ZHeight("Deep_ZHeight", Float) = -2000
		[Toggle(_WIND_ON)] _Wind("Wind", Float) = 0
		_Wind_Intensity("Wind_Intensity", Float) = 1
		_Wind_Speed("Wind_Speed", Float) = 0.1
		[Toggle(_FUZZYSHADING_ON)] _FuzzyShading("FuzzyShading", Float) = 0
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
		#pragma shader_feature_local _DETAILNORMALENABLE_ON
		#pragma shader_feature_local _FUZZYSHADING_ON
		#pragma shader_feature_local _RANDOMHUE_ON
		#pragma shader_feature_local _GLOW_ON
		#pragma shader_feature_local _CUSTOMROUGHNESS_ON
        #define OCEAN_VERTEX_ANIMATION 1

        sampler2D _Normal;
sampler2D _NormalDetail;
sampler2D _Macro;
sampler2D _Diffuse;
CBUFFER_START(UnityPerMaterial)
float _Wind_Speed;
float _Wind_Intensity;
float4 _Normal_ST;
float _Normal_Detail_Scale;
float _Normal_Detail_Amount;
float _Macro_Scale;
float _Macro_Triplanar_Blending;
float _Hue;
float _Hue_Variation_Intensity;
float4 _DifColor_BeforeHue;
float4 _Diffuse_ST;
float _Contrast;
float _Desaturation;
float _Macro_Intensity;
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
			float3 worldNormal;
			INTERNAL_DATA
			half ASEVFace;
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

		inline float3 TriplanarSampling5( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
		{
			float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
			projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
			float3 nsign = sign( worldNormal );
			half4 xNorm; half4 yNorm; half4 zNorm;
			xNorm = tex2D( topTexMap, tiling * worldPos.zy * float2(  nsign.x, 1.0 ) );
			yNorm = tex2D( topTexMap, tiling * worldPos.xz * float2(  nsign.y, 1.0 ) );
			zNorm = tex2D( topTexMap, tiling * worldPos.xy * float2( -nsign.z, 1.0 ) );
			xNorm.xyz  = half3( UnpackScaleNormal( xNorm, normalScale.y ).xy * float2(  nsign.x, 1.0 ) + worldNormal.zy, worldNormal.x ).zyx;
			yNorm.xyz  = half3( UnpackScaleNormal( yNorm, normalScale.x ).xy * float2(  nsign.y, 1.0 ) + worldNormal.xz, worldNormal.y ).xzy;
			zNorm.xyz  = half3( UnpackScaleNormal( zNorm, normalScale.y ).xy * float2( -nsign.z, 1.0 ) + worldNormal.xy, worldNormal.z ).xyz;
			return normalize( xNorm.xyz * projNormal.x + yNorm.xyz * projNormal.y + zNorm.xyz * projNormal.z );
		}

		inline float4 TriplanarSampling21( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
		{
			float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
			projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
			float3 nsign = sign( worldNormal );
			half4 xNorm; half4 yNorm; half4 zNorm;
			xNorm = tex2D( topTexMap, tiling * worldPos.zy * float2(  nsign.x, 1.0 ) );
			yNorm = tex2D( topTexMap, tiling * worldPos.xz * float2(  nsign.y, 1.0 ) );
			zNorm = tex2D( topTexMap, tiling * worldPos.xy * float2( -nsign.z, 1.0 ) );
			return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
		}

		void vertexDataFunc( inout OceanAttributes v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 _0101 = float4(0,1,0,1);
			float3 appendResult13_g609 = (float3(_0101.x , _0101.y , _0101.z));
			float3 normalizeResult14_g609 = normalize( appendResult13_g609 );
			float3 temp_cast_0 = (3.0).xxx;
			float temp_output_19_0_g609 = ( _0101.w * ( ( _Time.y * _Wind_Speed ) * -0.5 ) );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
			float3 temp_output_45_0_g609 = abs( ( ( frac( ( ( ( normalizeResult14_g609 * temp_output_19_0_g609 ) + ( ase_worldPos / 10.24 ) ) + 0.5 ) ) * 2.0 ) + -1.0 ) );
			float dotResult58_g609 = dot( normalizeResult14_g609 , ( ( ( temp_cast_0 - ( temp_output_45_0_g609 * 2.0 ) ) * temp_output_45_0_g609 ) * temp_output_45_0_g609 ) );
			float3 temp_cast_1 = (3.0).xxx;
			float3 temp_output_46_0_g609 = abs( ( ( frac( ( ( temp_output_19_0_g609 + ( ase_worldPos / 2.0 ) ) + 0.5 ) ) * 2.0 ) + -1.0 ) );
			float3 temp_cast_2 = (0.0).xxx;
			float3 temp_cast_3 = (0.0).xxx;
			float3 temp_output_8_0_g609 = temp_cast_3;
			float3 rotatedValue6_g609 = RotateAroundAxis( ( float3(0,0,-10) + temp_output_8_0_g609 ), temp_output_8_0_g609, cross( normalizeResult14_g609 , float3(0,0,1) ), ( dotResult58_g609 + distance( ( ( ( temp_cast_1 - ( temp_output_46_0_g609 * 2.0 ) ) * temp_output_46_0_g609 ) * temp_output_46_0_g609 ) , temp_cast_2 ) ) );
			#ifdef _WIND_ON
				float3 staticSwitch75 = ( ( ( ( rotatedValue6_g609 * 0.01 ) * _Wind_Intensity ) + temp_output_8_0_g609 ) * v.texcoord2.xy.y );
			#else
				float3 staticSwitch75 = float3( 0,0,0 );
			#endif
			v.vertex.xyz += staticSwitch75;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			float3 tex2DNode6 = UnpackNormal( tex2D( _Normal, uv_Normal ) );
			float2 temp_cast_0 = (_Normal_Detail_Scale).xx;
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_worldTangent = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float3 ase_worldBitangent = WorldNormalVector( i, float3( 0, 1, 0 ) );
			float3x3 ase_worldToTangent = float3x3( ase_worldTangent, ase_worldBitangent, ase_worldNormal );
			float3 triplanar5 = TriplanarSampling5( _NormalDetail, ase_worldPos, ase_worldNormal, 1.0, temp_cast_0, _Normal_Detail_Amount, 0 );
			float3 tanTriplanarNormal5 = mul( ase_worldToTangent, triplanar5 );
			#ifdef _DETAILNORMALENABLE_ON
				float3 staticSwitch8 = BlendNormals( tex2DNode6 , tanTriplanarNormal5 );
			#else
				float3 staticSwitch8 = tex2DNode6;
			#endif
			o.Normal = staticSwitch8;
			float2 temp_cast_1 = (_Macro_Scale).xx;
			float4 triplanar21 = TriplanarSampling21( _Macro, ase_worldPos, ase_worldNormal, _Macro_Triplanar_Blending, temp_cast_1, 1.0, 0 );
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
			float3 temp_cast_3 = (0.0).xxx;
			float2 uv_Diffuse = i.uv_texcoord * _Diffuse_ST.xy + _Diffuse_ST.zw;
			float4 tex2DNode27 = tex2D( _Diffuse, uv_Diffuse );
			float3 temp_output_3_0_g603 = ( _DifColor_BeforeHue * tex2DNode27 ).rgb;
			float3 rotatedValue2_g603 = RotateAroundAxis( temp_cast_3, temp_output_3_0_g603, normalizeResult6_g603.rgb, staticSwitch40 );
			float3 temp_output_1_0_g607 = ( rotatedValue2_g603 + temp_output_3_0_g603 );
			float3 normalizeResult2_g607 = SafeNormalize( temp_output_1_0_g607 );
			float3 desaturateInitialColor5_g607 = temp_output_1_0_g607;
			float desaturateDot5_g607 = dot( desaturateInitialColor5_g607, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar5_g607 = lerp( desaturateInitialColor5_g607, desaturateDot5_g607.xxx, 1.0 );
			float3 temp_cast_5 = (_Contrast).xxx;
			float3 desaturateInitialColor17 = ( normalizeResult2_g607 * pow( max(desaturateVar5_g607, 0.0) , temp_cast_5 ) );
			float desaturateDot17 = dot( desaturateInitialColor17, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar17 = lerp( desaturateInitialColor17, desaturateDot17.xxx, _Desaturation );
			float4 lerpResult15 = lerp( float4( 0,0,0,0 ) , ( ( triplanar21 + float4( 0.5,0.5,0.5,0.5 ) ) * float4( desaturateVar17 , 0.0 ) ) , _Macro_Intensity);
			float4 temp_output_12_0 = ( lerpResult15 * _Brightness );
			float3 ase_worldViewDir = Unity_SafeNormalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 appendResult2_g611 = (float3(1.0 , 1.0 , i.ASEVFace));
			float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
			float3 tangentToWorldDir10_g611 = mul( ase_tangentToWorldFast, ( float3( 0,0,1 ) * appendResult2_g611 ) );
			float dotResult12_g611 = dot( ase_worldViewDir , tangentToWorldDir10_g611 );
			float clampResult13_g611 = clamp( dotResult12_g611 , 0.0 , 1.0 );
			float3 temp_output_26_0_g611 = temp_output_12_0.xyz;
			float3 desaturateInitialColor27_g611 = temp_output_26_0_g611;
			float desaturateDot27_g611 = dot( desaturateInitialColor27_g611, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar27_g611 = lerp( desaturateInitialColor27_g611, desaturateDot27_g611.xxx, 0.5 );
			float clampResult21_g611 = clamp( ( pow( ( 1.0 - clampResult13_g611 ) , 6.0 ) * 0.8 ) , 0.0 , 1.0 );
			float3 lerpResult23_g611 = lerp( ( ( 1.0 - ( clampResult13_g611 * 0.8 ) ) * temp_output_26_0_g611 ) , ( desaturateVar27_g611 * float3( 1.5,1.5,1.5 ) ) , clampResult21_g611);
			#ifdef _FUZZYSHADING_ON
				float4 staticSwitch9 = float4( lerpResult23_g611 , 0.0 );
			#else
				float4 staticSwitch9 = temp_output_12_0;
			#endif
			o.Albedo = staticSwitch9.xyz;
			float4 color5_g608 = IsGammaSpace() ? float4(1,1,1,1) : float4(1,1,1,1);
			float4 normalizeResult6_g608 = normalize( color5_g608 );
			float3 temp_cast_11 = (0.0).xxx;
			float3 temp_output_3_0_g608 = _GlowColor.rgb;
			float3 rotatedValue2_g608 = RotateAroundAxis( temp_cast_11, temp_output_3_0_g608, normalizeResult6_g608.rgb, staticSwitch40 );
			float temp_output_64_0 = distance( _Deep_ZHeight , _Shallow_ZHeight );
			float clampResult57 = clamp( ( 1.0 - ( ( ase_worldPos.y - _Deep_ZHeight ) / temp_output_64_0 ) ) , 0.0 , 1.0 );
			#ifdef _GLOW_ON
				float3 staticSwitch55 = ( ( ( tex2DNode27.a * ( rotatedValue2_g608 + temp_output_3_0_g608 ) ) * _GlowIntensity ) * clampResult57 );
			#else
				float3 staticSwitch55 = float3( 0,0,0 );
			#endif
			o.Emission = staticSwitch55;
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
