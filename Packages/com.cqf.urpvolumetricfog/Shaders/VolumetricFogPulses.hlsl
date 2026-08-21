#ifndef VOLUMETRIC_FOG_PULSES_INCLUDED
#define VOLUMETRIC_FOG_PULSES_INCLUDED

#define VOLUMETRIC_FOG_PULSE_CAPACITY 12

int _VolumetricFogPulseCount;
float4 _VolumetricFogPulseOrigins[VOLUMETRIC_FOG_PULSE_CAPACITY];
float4 _VolumetricFogPulseParams[VOLUMETRIC_FOG_PULSE_CAPACITY];

float _SonarFogLanternEnabled;
float3 _SonarFogLanternPosition;
float3 _SonarFogLanternForward;
float4 _SonarFogLanternShape;
float4 _SonarFogLanternHeight;

// Returns one only on the expanding shell. The interior and exterior both
// preserve fog, so an object's pixels are cleared only while the ring touches
// their exact world-space surface.
float VolumetricFogPulseClearAt(float2 uv)
{
    if (_VolumetricFogPulseCount <= 0)
        return 0.0;

    float deviceDepth = SampleSceneDepth(uv);
#if UNITY_REVERSED_Z
    if (deviceDepth <= 0.00001)
        return 0.0;
#else
    if (deviceDepth >= 0.99999)
        return 0.0;
    deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, deviceDepth);
#endif

    float3 worldPosition = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
    float clearAmount = 0.0;

    [unroll]
    for (int i = 0; i < VOLUMETRIC_FOG_PULSE_CAPACITY; i++)
    {
        if (i >= _VolumetricFogPulseCount)
            break;

        float4 originRadius = _VolumetricFogPulseOrigins[i];
        float4 pulseParams = _VolumetricFogPulseParams[i];
        float distanceToShell = abs(distance(worldPosition, originRadius.xyz) - originRadius.w);
        float width = max(0.001, pulseParams.x);
        float shell = 1.0 - smoothstep(width * 0.65, width, distanceToShell);
        clearAmount = max(clearAmount, shell * pulseParams.y * pulseParams.z);
    }

    return saturate(clearAmount);
}

// A forward-facing horizontal cylinder. The player light affects fog only;
// reveal and outline systems continue to operate independently.
float SonarFogLanternClearAt(float2 uv)
{
    if (_SonarFogLanternEnabled < 0.5)
        return 0.0;

    float deviceDepth = SampleSceneDepth(uv);
#if UNITY_REVERSED_Z
    if (deviceDepth <= 0.00001)
        return 0.0;
#else
    if (deviceDepth >= 0.99999)
        return 0.0;
    deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, deviceDepth);
#endif

    float3 worldPosition = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
    float3 center = _SonarFogLanternPosition + _SonarFogLanternForward * _SonarFogLanternShape.x;
    float2 horizontal = worldPosition.xz - center.xz;
    float radius = max(0.001, _SonarFogLanternShape.y);
    float edge = max(0.001, _SonarFogLanternShape.z);
    float radial = 1.0 - smoothstep(radius - edge, radius, length(horizontal));

    float bottom = _SonarFogLanternPosition.y + _SonarFogLanternHeight.x;
    float top = bottom + max(0.001, _SonarFogLanternHeight.y);
    float verticalEdge = min(edge, max(0.001, (top - bottom) * 0.5));
    float lower = smoothstep(bottom, bottom + verticalEdge, worldPosition.y);
    float upper = 1.0 - smoothstep(top - verticalEdge, top, worldPosition.y);
    return saturate(radial * lower * upper);
}

#endif
