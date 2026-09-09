#ifndef DAVIS3D_SURFACE_COMPAT_URP
#define DAVIS3D_SURFACE_COMPAT_URP

// Adapter for this pack's authored surface equations. Material property names,
// UV channels and all authoring math are deliberately kept in each shader.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0
#define Unity_SafeNormalize SafeNormalize
#define UnityWorldSpaceViewDir GetWorldSpaceViewDir
#define INTERNAL_DATA float3 tangentWS; float3 bitangentWS;
#define WorldNormalVector(data, n) (data.tangentWS * (n).x + data.bitangentWS * (n).y + data.worldNormal * (n).z)

half3 UnpackScaleNormal(half4 packed, half scale) { return UnpackNormalScale(packed, scale); }
half3 BlendNormals(half3 a, half3 b) { return normalize(half3(a.xy + b.xy, a.z * b.z)); }
bool IsGammaSpace()
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return true;
#else
    return false;
#endif
}

struct SurfaceOutputStandard
{
    half3 Albedo;
    half3 Normal;
    half3 Emission;
    half Metallic;
    half Smoothness;
    half Occlusion;
    half Alpha;
};

struct OceanAttributes
{
    float4 vertex : POSITION;
    float4 tangent : TANGENT;
    float3 normal : NORMAL;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 texcoord3 : TEXCOORD3;
    float4 ase_texcoord4 : TEXCOORD4;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OceanVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    float4 extraUV : TEXCOORD4;
    float4 fogAndVertexLight : TEXCOORD5;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
#endif
