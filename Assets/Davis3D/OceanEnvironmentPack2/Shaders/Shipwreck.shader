// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Davis3D/OceanEnviroment2/Shipwreck"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_Diffuse("Diffuse", 2D) = "white" {}
		_Brightness("Brightness", Float) = 1.5
		[Toggle(_DETAILNORMALENABLE_ON)] _DetailNormalEnable("DetailNormalEnable", Float) = 0
		_Normal("Normal", 2D) = "bump" {}
		_NormalDetail("Normal Detail", 2D) = "bump" {}
		_Normal_Detail_Scale("Normal_Detail_Scale", Float) = 1
		_Normal_Detail_Amount("Normal_Detail_Amount", Float) = 0.5
		_Metallic("Metallic", Float) = 0
		_Roughness_A("Roughness_A", Float) = 0.5
		_Roughness_B("Roughness_B", Float) = 1
		_Macro("Macro", 2D) = "white" {}
		_Macro_Scale("Macro_Scale", Float) = 50
		_Macro_Intensity("Macro_Intensity", Float) = 1
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
        
		#pragma shader_feature_local _DETAILNORMALENABLE_ON
		#pragma shader_feature_local _FUZZYSHADING_ON
        
        sampler2D _Normal;
sampler2D _NormalDetail;
sampler2D _Diffuse;
sampler2D _Macro;
CBUFFER_START(UnityPerMaterial)
float _Normal_Detail_Scale;
float _Normal_Detail_Amount;
float4 _Color;
float4 _Diffuse_ST;
float _Macro_Scale;
float _Macro_Intensity;
float _Brightness;
float _Metallic;
float _Roughness_A;
float _Roughness_B;
CBUFFER_END

		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
			float3 worldNormal;
			INTERNAL_DATA
			half ASEVFace;
		};

		inline float4 TriplanarSampling23( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
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

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float3 tex2DNode7 = UnpackNormal( tex2D( _Normal, i.uv_texcoord ) );
			#ifdef _DETAILNORMALENABLE_ON
				float3 staticSwitch9 = BlendNormals( tex2DNode7 , UnpackScaleNormal( tex2D( _NormalDetail, ( i.uv_texcoord * _Normal_Detail_Scale ) ), _Normal_Detail_Amount ) );
			#else
				float3 staticSwitch9 = tex2DNode7;
			#endif
			o.Normal = staticSwitch9;
			float2 uv_Diffuse = i.uv_texcoord * _Diffuse_ST.xy + _Diffuse_ST.zw;
			float4 tex2DNode14 = tex2D( _Diffuse, uv_Diffuse );
			float4 temp_output_15_0 = ( _Color * tex2DNode14 );
			float2 temp_cast_1 = (_Macro_Scale).xx;
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float4 triplanar23 = TriplanarSampling23( _Macro, ase_worldPos, ase_worldNormal, 1.0, temp_cast_1, 1.0, 0 );
			float4 lerpResult26 = lerp( temp_output_15_0 , ( ( triplanar23 + float4( 0.5,0.5,0.5,0.5 ) ) * temp_output_15_0 ) , _Macro_Intensity);
			float4 temp_output_29_0 = ( lerpResult26 * _Brightness );
			float3 ase_worldViewDir = Unity_SafeNormalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 appendResult2_g1 = (float3(1.0 , 1.0 , i.ASEVFace));
			float3 ase_worldTangent = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float3 ase_worldBitangent = WorldNormalVector( i, float3( 0, 1, 0 ) );
			float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
			float3 tangentToWorldDir10_g1 = mul( ase_tangentToWorldFast, ( float3( 0,0,1 ) * appendResult2_g1 ) );
			float dotResult12_g1 = dot( ase_worldViewDir , tangentToWorldDir10_g1 );
			float clampResult13_g1 = clamp( dotResult12_g1 , 0.0 , 1.0 );
			float3 temp_output_26_0_g1 = temp_output_29_0.xyz;
			float3 desaturateInitialColor27_g1 = temp_output_26_0_g1;
			float desaturateDot27_g1 = dot( desaturateInitialColor27_g1, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar27_g1 = lerp( desaturateInitialColor27_g1, desaturateDot27_g1.xxx, 0.5 );
			float clampResult21_g1 = clamp( ( pow( ( 1.0 - clampResult13_g1 ) , 6.0 ) * 0.8 ) , 0.0 , 1.0 );
			float3 lerpResult23_g1 = lerp( ( ( 1.0 - ( clampResult13_g1 * 0.8 ) ) * temp_output_26_0_g1 ) , ( desaturateVar27_g1 * float3( 1.5,1.5,1.5 ) ) , clampResult21_g1);
			#ifdef _FUZZYSHADING_ON
				float4 staticSwitch30 = float4( lerpResult23_g1 , 0.0 );
			#else
				float4 staticSwitch30 = temp_output_29_0;
			#endif
			o.Albedo = staticSwitch30.xyz;
			o.Metallic = _Metallic;
			float lerpResult16 = lerp( _Roughness_A , _Roughness_B , tex2DNode14.g);
			o.Smoothness = ( 1.0 - lerpResult16 );
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
