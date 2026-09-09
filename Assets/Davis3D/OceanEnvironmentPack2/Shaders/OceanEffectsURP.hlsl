#ifndef DAVIS3D_OCEAN_EFFECTS_URP_INCLUDED
#define DAVIS3D_OCEAN_EFFECTS_URP_INCLUDED

// Shared by the hand-ported effects. Screen textures are declared by URP, whose
// TEXTURE2D_X sampling selects the correct eye for single-pass instanced XR.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct OceanEffectAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OceanEffectVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    float4 fogAndVertexLight : TEXCOORD4;
    float eyeDepth : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OceanEffectVaryings OceanEffectVertex(OceanEffectAttributes input)
{
    OceanEffectVaryings output = (OceanEffectVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = input.uv;
    output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
    output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normal.normalWS);
    output.eyeDepth = -position.positionVS.z;
    return output;
}

half3x3 OceanEffectTangentToWorld(OceanEffectVaryings input)
{
    half3 normal = NormalizeNormalPerPixel(input.normalWS);
    half3 tangent = SafeNormalize(input.tangentWS.xyz);
    half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
    return half3x3(tangent, bitangent, normal);
}

InputData OceanEffectInputData(OceanEffectVaryings input, half3 normalTS)
{
    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.normalWS = NormalizeNormalPerPixel(
        TransformTangentToWorld(normalTS, OceanEffectTangentToWorld(input)));
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    // Match URP GetShadowCoord: opaque screen-space-shadow consumers require
    // projected coordinates; transparent effects still sample the shadow atlas.
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
        data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    data.fogCoord = input.fogAndVertexLight.x;
    data.vertexLighting = input.fogAndVertexLight.yzw;
    data.bakedGI = SampleSH(data.normalWS);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    data.shadowMask = half4(1, 1, 1, 1);
    return data;
}

// The original graphs use whiteout normal blending, not reoriented blending.
half3 OceanBlendNormals(half3 a, half3 b)
{
    return normalize(half3(a.xy + b.xy, a.z * b.z));
}

float OceanSafeDenominator(float value)
{
    return abs(value) < 0.00001 ? (value < 0.0 ? -0.00001 : 0.00001) : value;
}

// OceanSurface originally cast an opaque shadow despite its transparent queue.
float3 _LightDirection;
float3 _LightPosition;

OceanEffectVaryings OceanEffectShadowVertex(OceanEffectAttributes input)
{
    OceanEffectVaryings output = OceanEffectVertex(input);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirectionWS = normalize(_LightPosition - output.positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(output.positionWS, output.normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}

half4 OceanEffectShadowFragment(OceanEffectVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}
#endif
