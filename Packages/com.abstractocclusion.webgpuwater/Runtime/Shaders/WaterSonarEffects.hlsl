// WebGpuWater - gameplay-controlled water-visibility effects.
// Kept separate from the legacy Volumetric Fog shader globals so sonar can
// target this water volume without changing any other fog effect in the project.
#ifndef WEBGPU_WATER_SONAR_EFFECTS_INCLUDED
#define WEBGPU_WATER_SONAR_EFFECTS_INCLUDED

#define WATER_SONAR_PULSE_CAPACITY 12

int _WaterSonarPulseCount;
float4 _WaterSonarPulseOrigins[WATER_SONAR_PULSE_CAPACITY]; // xyz origin, w radius
float4 _WaterSonarPulseParams[WATER_SONAR_PULSE_CAPACITY];  // x width, y strength, z end fade

float _WaterSonarLanternEnabled;
float3 _WaterSonarLanternPosition;
float3 _WaterSonarLanternForward;
float4 _WaterSonarLanternShape;  // x forward offset, y radius, z edge fade
float4 _WaterSonarLanternHeight; // x bottom offset, y height

// Sky/background depth has no concrete scene surface to scan. Keeping it fogged
// makes the reveal strictly follow the expanding shell over actual objects.
bool WaterSonarHasSceneSurface(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth > 0.00001;
#else
    return rawDepth < 0.99999;
#endif
}

float WaterSonarPulseClearAt(float3 worldPosition, float rawDepth)
{
    if (_WaterSonarPulseCount <= 0 || !WaterSonarHasSceneSurface(rawDepth))
        return 0.0;

    float clearAmount = 0.0;
    [unroll]
    for (int i = 0; i < WATER_SONAR_PULSE_CAPACITY; i++)
    {
        if (i >= _WaterSonarPulseCount)
            break;

        float4 originRadius = _WaterSonarPulseOrigins[i];
        float4 pulseParams = _WaterSonarPulseParams[i];
        float distanceToShell = abs(distance(worldPosition, originRadius.xyz) - originRadius.w);
        float width = max(0.001, pulseParams.x);
        float shell = 1.0 - smoothstep(width * 0.65, width, distanceToShell);
        clearAmount = max(clearAmount, shell * pulseParams.y * pulseParams.z);
    }
    return saturate(clearAmount);
}

float WaterSonarLanternClearAt(float3 worldPosition)
{
    if (_WaterSonarLanternEnabled < 0.5)
        return 0.0;

    float3 center = _WaterSonarLanternPosition
                  + _WaterSonarLanternForward * _WaterSonarLanternShape.x;
    float2 horizontal = worldPosition.xz - center.xz;
    float radius = max(0.001, _WaterSonarLanternShape.y);
    float edge = max(0.001, _WaterSonarLanternShape.z);
    float radial = 1.0 - smoothstep(radius - edge, radius, length(horizontal));

    float bottom = _WaterSonarLanternPosition.y + _WaterSonarLanternHeight.x;
    float top = bottom + max(0.001, _WaterSonarLanternHeight.y);
    float verticalEdge = min(edge, max(0.001, (top - bottom) * 0.5));
    float lower = smoothstep(bottom, bottom + verticalEdge, worldPosition.y);
    float upper = 1.0 - smoothstep(top - verticalEdge, top, worldPosition.y);
    return saturate(radial * lower * upper);
}

float WaterSonarVisibilityClearAt(float3 worldPosition, float rawDepth)
{
    return max(WaterSonarPulseClearAt(worldPosition, rawDepth),
               WaterSonarLanternClearAt(worldPosition));
}

#endif // WEBGPU_WATER_SONAR_EFFECTS_INCLUDED
