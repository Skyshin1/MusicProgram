#ifndef DAVIS3D_SURFACE_PASSES_URP
#define DAVIS3D_SURFACE_PASSES_URP

OceanVaryings OceanVertex(OceanAttributes v)
{
    OceanVaryings o = (OceanVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#ifdef OCEAN_VERTEX_ANIMATION
    Input customInput;
    vertexDataFunc(v, customInput);
#endif
    VertexPositionInputs p = GetVertexPositionInputs(v.vertex.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(v.normal, v.tangent);
    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS = n.normalWS;
    o.tangentWS = float4(n.tangentWS, v.tangent.w * GetOddNegativeScale());
    o.uv = v.texcoord.xy;
    o.extraUV.xy = v.ase_texcoord4.xy;
#ifdef OCEAN_CORAL_SUN
    o.extraUV.zw = v.texcoord3.xy;
#elif defined(OCEAN_KELP)
    o.extraUV.zw = v.texcoord2.xy;
#endif
    o.fogAndVertexLight = float4(ComputeFogFactor(p.positionCS.z), VertexLighting(p.positionWS, n.normalWS));
    OUTPUT_LIGHTMAP_UV(v.texcoord1.xy, unity_LightmapST, o.lightmapUV);
    OUTPUT_SH(n.normalWS, o.vertexSH);
    return o;
}

SurfaceOutputStandard OceanEvaluate(OceanVaryings v, half face)
{
    Input i = (Input)0;
    i.worldPos = v.positionWS;
    i.worldNormal = normalize(v.normalWS);
    i.tangentWS = normalize(v.tangentWS.xyz);
    i.bitangentWS = cross(i.worldNormal, i.tangentWS) * v.tangentWS.w;
    i.uv_texcoord = v.uv;
    i.ASEVFace = face;
#ifdef OCEAN_CORAL_SUN
    i.ase_texcoord5 = v.extraUV.xy;
    i.uv4_texcoord4 = v.extraUV.zw;
#elif defined(OCEAN_KELP)
    i.ase_texcoord5 = v.extraUV.xy;
    i.uv3_texcoord3 = v.extraUV.zw;
#endif
    SurfaceOutputStandard surface = (SurfaceOutputStandard)0;
    surface.Normal = half3(0, 0, 1);
    surface.Occlusion = 1;
    surface.Alpha = 1;
    surf(i, surface);
    return surface;
}

half3 OceanWorldNormal(OceanVaryings v, half3 normalTS)
{
    float3 normal = normalize(v.normalWS);
    float3 tangent = normalize(v.tangentWS.xyz);
    float3 bitangent = cross(normal, tangent) * v.tangentWS.w;
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(tangent, bitangent, normal)));
}

half4 OceanFragment(OceanVaryings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    SurfaceOutputStandard s = OceanEvaluate(v, IS_FRONT_VFACE(face, 1.0, -1.0));
    SurfaceData data = (SurfaceData)0;
    data.albedo = s.Albedo;
    data.metallic = saturate(s.Metallic);
    data.smoothness = saturate(s.Smoothness);
    data.normalTS = s.Normal;
    data.emission = s.Emission;
    data.occlusion = s.Occlusion;
    data.alpha = s.Alpha;
    InputData lighting = (InputData)0;
    lighting.positionWS = v.positionWS;
    lighting.normalWS = OceanWorldNormal(v, s.Normal);
    lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(v.positionWS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(v.positionWS));
#else
    lighting.shadowCoord = TransformWorldToShadowCoord(v.positionWS);
#endif
    lighting.fogCoord = v.fogAndVertexLight.x;
    lighting.vertexLighting = v.fogAndVertexLight.yzw;
    lighting.bakedGI = SAMPLE_GI(v.lightmapUV, v.vertexSH, lighting.normalWS);
    lighting.shadowMask = SAMPLE_SHADOWMASK(v.lightmapUV);
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(v.positionCS);
    half4 color = UniversalFragmentPBR(lighting, data);
    color.rgb = MixFog(color.rgb, lighting.fogCoord);
    return color;
}

float3 _LightDirection;
float3 _LightPosition;
OceanVaryings OceanShadowVertex(OceanAttributes v)
{
    OceanVaryings o = OceanVertex(v);
#ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirection = normalize(_LightPosition - o.positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, lightDirection)));
    return o;
}

half4 OceanDepthFragment(OceanVaryings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    OceanEvaluate(v, IS_FRONT_VFACE(face, 1.0, -1.0));
    return 0;
}

half4 OceanDepthNormalsFragment(OceanVaryings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    SurfaceOutputStandard s = OceanEvaluate(v, IS_FRONT_VFACE(face, 1.0, -1.0));
    half3 normalWS = OceanWorldNormal(v, s.Normal);
#ifdef _GBUFFER_NORMALS_OCT
    float2 oct = PackNormalOctQuadEncode(normalWS);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normalWS, 0);
#endif
}

OceanVaryings OceanMetaVertex(OceanAttributes v)
{
    OceanVaryings o = OceanVertex(v);
    o.positionCS = MetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST);
    return o;
}
half4 OceanMetaFragment(OceanVaryings v) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    SurfaceOutputStandard s = OceanEvaluate(v, 1);
    MetaInput meta = (MetaInput)0;
    BRDFData brdf;
    InitializeBRDFData(s.Albedo, saturate(s.Metallic), half3(0, 0, 0), saturate(s.Smoothness), s.Alpha, brdf);
    meta.Albedo = brdf.diffuse + brdf.specular * brdf.roughness * 0.5;
    meta.Emission = s.Emission;
    return MetaFragment(meta);
}
#endif
