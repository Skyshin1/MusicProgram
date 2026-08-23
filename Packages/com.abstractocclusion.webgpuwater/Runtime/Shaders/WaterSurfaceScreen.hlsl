// WaterSurface pass: URP screen-texture access (opaque colour + raw scene depth)
// and the shared screen-space helpers (ScreenUV, EyeDepthOf).
// Split out of WaterSurface.shader (SHADER-SPLIT-2) as VERBATIM moves - any
// behavior change here is a bug. First of the WaterSurface* includes: the shadow
// tap and the SSR march need sampler_PointClamp / RawSceneDepth from here.
#ifndef WATER_SURFACE_SCREEN_INCLUDED
#define WATER_SURFACE_SCREEN_INCLUDED

// URP scene textures (enable Opaque Texture + Depth Texture in the URP asset).
// Important: this shader already sits at the ps_4_0 limit of 16 samplers. Do NOT
// use UNITY_DECLARE_DEPTH_TEXTURE here: its generated sampler would be a 17th
// sampler. The depth texture deliberately shares sampler_PointClamp with the
// manual shadow taps below, exactly as the desktop shader did.
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
Texture2DArray _CameraOpaqueTexture;
SamplerState sampler_CameraOpaqueTexture;
Texture2DArray _CameraDepthTexture;
#else
sampler2D _CameraOpaqueTexture;
Texture2D _CameraDepthTexture;
#endif
SamplerState sampler_PointClamp;
// Every read is explicit-LOD (loop-safe, WGSL-safe): LinearEyeDepth(RawSceneDepth(uv)).
float RawSceneDepth(float2 uv)
{
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    return _CameraDepthTexture.SampleLevel(sampler_PointClamp,
                                           float3(uv, unity_StereoEyeIndex), 0.0).r;
#else
    return _CameraDepthTexture.SampleLevel(sampler_PointClamp, uv, 0.0).r;
#endif
}

float3 SampleCameraOpaque(float2 uv)
{
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    return _CameraOpaqueTexture.Sample(sampler_CameraOpaqueTexture,
                                       float3(uv, unity_StereoEyeIndex)).rgb;
#else
    return tex2D(_CameraOpaqueTexture, uv).rgb;
#endif
}

// Unity has a screenspace helper for implicit LOD but not an explicit-LOD variant.
// The conditional keeps desktop/WebGL on the original sampler2D path while Quest
// reads the correct slice of its XR texture array.
float3 SampleCameraOpaqueLod(float2 uv, float lod)
{
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    return _CameraOpaqueTexture.SampleLevel(sampler_CameraOpaqueTexture,
                                             float3(uv, unity_StereoEyeIndex), lod).rgb;
#else
    return tex2Dlod(_CameraOpaqueTexture, float4(uv, 0.0, lod)).rgb;
#endif
}

// Perspective divide of a ComputeScreenPos-style position -> [0,1] screen UV.
// ONE helper for every screen-space consumer (SSR march, planar mirror,
// refraction, contact foam) so the w-guard can never drift between them.
#define SCREEN_UV_MIN_W 1e-5   // guards the divide at/behind the camera plane
float2 ScreenUV(float4 screenPos)
{
    return screenPos.xy / max(screenPos.w, SCREEN_UV_MIN_W);
}

// Positive view-space (eye) depth of a world point (view forward is -Z, so the
// negation yields metres in front of the camera). ONE helper for the SSR march
// and the refraction/contact-foam thickness tests, so the sign convention can
// never drift between them.
float EyeDepthOf(float3 worldPos)
{
    return -mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).z;
}

#endif // WATER_SURFACE_SCREEN_INCLUDED
