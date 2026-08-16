#ifndef ABYSSAL_WATER_COMMON_INCLUDED
#define ABYSSAL_WATER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define ABYSSAL_MAX_WAVES 12
#define ABYSSAL_MAX_MICRO_WAVES 8

int _AbyssalWaveCount;
float4 _AbyssalWaveDataA[ABYSSAL_MAX_WAVES]; // direction xy, amplitude, wavenumber
float4 _AbyssalWaveDataB[ABYSSAL_MAX_WAVES]; // omega, steepness, phase, unused
int _AbyssalMicroWaveCount;
float4 _AbyssalMicroWaveDataA[ABYSSAL_MAX_MICRO_WAVES];
float4 _AbyssalMicroWaveDataB[ABYSSAL_MAX_MICRO_WAVES];
float4 _AbyssalAntiTiling; // enabled, phase warp strength, inverse patch size, stochastic normal blend
float _AbyssalAntiTilingSeed;
float _AbyssalTime;
float _AbyssalWaterLevel;
float4 _AbyssalAbsorption;   // RGB coefficient, max optical depth
float4 _AbyssalScattering;   // RGB colour, strength
float _AbyssalAnisotropy;
float4 _AbyssalOptics;       // IOR, refraction, reflection, smoothness
float4 _AbyssalSurface;      // normal strength, crest transmission, power, chop
float4 _AbyssalCrestColor;
float4 _AbyssalFoamColor;
float4 _AbyssalFoam;         // strength, threshold, feather, shoreline distance
float4 _AbyssalContact;      // contact strength, meniscus width, unused, unused
float4 _AbyssalCaustics;     // intensity, scale, focus, chromatic aberration
float4 _AbyssalCausticColor;
float _AbyssalCausticMaximumDepth;
float4 _AbyssalUnderwater;   // distortion, waterline thickness, meniscus, fog multiplier
float4 _AbyssalGodRays;
float4 _AbyssalDynamicCenterSize; // center xz, world size, enabled
float4 _AbyssalDynamicParameters; // displacement, inverse resolution, contact foam, unused

TEXTURE2D(_AbyssalDynamicCurrent);
SAMPLER(sampler_AbyssalDynamicCurrent);
TEXTURE2D(_AbyssalDynamicPrevious);
SAMPLER(sampler_AbyssalDynamicPrevious);

struct AbyssalWaveSample
{
    float3 displacement;
    float3 normalWS;
    float jacobian;
    float crest;
    float dynamicHeight;
    float dynamicVelocity;
    float2 slope;
};

// A continuous, analytic phase warp breaks the long straight repetition of a
// finite Gerstner spectrum without introducing patch borders or changing the
// water height discontinuously. The derivative is returned so normals and
// caustic curvature remain physically consistent with the displaced surface.
float AbyssalPhaseWarp(float2 worldXZ, float waveIndex, out float2 phaseGradient)
{
    phaseGradient = 0.0;
    if (_AbyssalAntiTiling.x < 0.5 || _AbyssalAntiTiling.y <= 0.0) return 0.0;

    float2 directionA = normalize(float2(0.7548777, 0.6558659));
    float2 directionB = normalize(float2(-0.5698403, 0.8217559));
    float seedPhase = _AbyssalAntiTilingSeed * 0.0137 + waveIndex * 2.3999632;
    float angularA = TWO_PI * max(1e-5, _AbyssalAntiTiling.z);
    float angularB = angularA * 1.618034;
    float argumentA = dot(worldXZ, directionA) * angularA + seedPhase;
    float argumentB = dot(worldXZ, directionB) * angularB - seedPhase * 1.37;
    float sineA;
    float cosineA;
    float sineB;
    float cosineB;
    sincos(argumentA, sineA, cosineA);
    sincos(argumentB, sineB, cosineB);
    phaseGradient = (cosineA * directionA * angularA * 0.62 +
                     cosineB * directionB * angularB * 0.38) * _AbyssalAntiTiling.y;
    return (sineA * 0.62 + sineB * 0.38) * _AbyssalAntiTiling.y;
}

float AbyssalInsideDynamicDomain(float2 uv)
{
    float2 inside = step(0.0, uv) * step(uv, 1.0);
    return inside.x * inside.y * step(0.5, _AbyssalDynamicCenterSize.w);
}

void AbyssalSampleDynamic(float2 worldXZ, out float height, out float2 gradient, out float velocity)
{
    float size = max(1.0, _AbyssalDynamicCenterSize.z);
    float2 uv = (worldXZ - _AbyssalDynamicCenterSize.xy) / size + 0.5;
    float inside = AbyssalInsideDynamicDomain(uv);
    float texel = max(_AbyssalDynamicParameters.y, 1e-5);
    float center = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicCurrent, sampler_AbyssalDynamicCurrent, uv, 0).r;
    float previous = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicPrevious, sampler_AbyssalDynamicPrevious, uv, 0).r;
    float left = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicCurrent, sampler_AbyssalDynamicCurrent, uv - float2(texel, 0), 0).r;
    float right = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicCurrent, sampler_AbyssalDynamicCurrent, uv + float2(texel, 0), 0).r;
    float down = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicCurrent, sampler_AbyssalDynamicCurrent, uv - float2(0, texel), 0).r;
    float up = SAMPLE_TEXTURE2D_LOD(_AbyssalDynamicCurrent, sampler_AbyssalDynamicCurrent, uv + float2(0, texel), 0).r;
    height = center * _AbyssalDynamicParameters.x * inside;
    gradient = float2(right - left, up - down) * (0.5 / max(texel * size, 1e-4)) *
               _AbyssalDynamicParameters.x * inside;
    velocity = (center - previous) * _AbyssalDynamicParameters.x * inside;
}

AbyssalWaveSample AbyssalEvaluateWavesFiltered(float2 worldXZ, float minimumWavelength)
{
    AbyssalWaveSample result;
    result.displacement = 0.0;
    float2 slope = 0.0;
    float2x2 horizontalJacobian = float2x2(1.0, 0.0, 0.0, 1.0);
    float amplitudeSum = 1e-4;

    [unroll(ABYSSAL_MAX_WAVES)]
    for (int i = 0; i < ABYSSAL_MAX_WAVES; i++)
    {
        if (i >= _AbyssalWaveCount) break;
        float4 dataA = _AbyssalWaveDataA[i];
        float4 dataB = _AbyssalWaveDataB[i];
        float2 direction = normalize(dataA.xy + 1e-6);
        float amplitude = dataA.z;
        float k = dataA.w;
        float wavelength = TWO_PI / max(k, 1e-4);
        float lodWeight = minimumWavelength <= 0.0 ? 1.0 :
                          smoothstep(minimumWavelength * 0.75,
                                     minimumWavelength * 1.25, wavelength);
        amplitude *= lodWeight;
        float omega = dataB.x;
        float steepness = saturate(dataB.y * _AbyssalSurface.w);
        float2 phaseGradient;
        float phaseWarp = AbyssalPhaseWarp(worldXZ, (float)i, phaseGradient);
        float2 thetaGradient = direction * k + phaseGradient;
        float theta = dot(direction, worldXZ) * k + phaseWarp +
                      omega * _AbyssalTime + dataB.z;
        float sineValue;
        float cosineValue;
        sincos(theta, sineValue, cosineValue);
        float qa = steepness * amplitude;

        result.displacement.xz += direction * qa * cosineValue;
        result.displacement.y += amplitude * sineValue;
        slope += thetaGradient * (amplitude * cosineValue);
        amplitudeSum += amplitude;

        float2 derivativeX = -direction * qa * sineValue * thetaGradient.x;
        float2 derivativeY = -direction * qa * sineValue * thetaGradient.y;
        horizontalJacobian[0][0] += derivativeX.x;
        horizontalJacobian[0][1] += derivativeY.x;
        horizontalJacobian[1][0] += derivativeX.y;
        horizontalJacobian[1][1] += derivativeY.y;
    }

    float dynamicHeight;
    float2 dynamicGradient;
    float dynamicVelocity;
    AbyssalSampleDynamic(worldXZ + result.displacement.xz, dynamicHeight, dynamicGradient, dynamicVelocity);
    result.displacement.y += dynamicHeight;
    slope += dynamicGradient;
    result.normalWS = normalize(float3(-slope.x * _AbyssalSurface.x, 1.0,
                                       -slope.y * _AbyssalSurface.x));
    result.jacobian = determinant(horizontalJacobian);
    float normalizedHeight = result.displacement.y / amplitudeSum;
    result.crest = saturate(normalizedHeight * 0.5 + 0.5);
    result.dynamicHeight = dynamicHeight;
    result.dynamicVelocity = dynamicVelocity;
    result.slope = slope;
    return result;
}

AbyssalWaveSample AbyssalEvaluateMicroWavesFiltered(float2 worldXZ, float minimumWavelength)
{
    AbyssalWaveSample result;
    result.displacement = 0.0;
    result.dynamicHeight = 0.0;
    result.dynamicVelocity = 0.0;
    float2 slope = 0.0;
    float2x2 horizontalJacobian = float2x2(1.0, 0.0, 0.0, 1.0);
    float amplitudeSum = 1e-4;

    [unroll(ABYSSAL_MAX_MICRO_WAVES)]
    for (int i = 0; i < ABYSSAL_MAX_MICRO_WAVES; i++)
    {
        if (i >= _AbyssalMicroWaveCount) break;
        float4 dataA = _AbyssalMicroWaveDataA[i];
        float4 dataB = _AbyssalMicroWaveDataB[i];
        float2 direction = normalize(dataA.xy + 1e-6);
        float amplitude = dataA.z;
        float k = dataA.w;
        float wavelength = TWO_PI / max(k, 1e-4);
        float lodWeight = minimumWavelength <= 0.0 ? 1.0 :
                          smoothstep(minimumWavelength * 0.72,
                                     minimumWavelength * 1.28, wavelength);
        amplitude *= lodWeight;
        float2 phaseGradient;
        float phaseWarp = AbyssalPhaseWarp(worldXZ, (float)i + 19.0, phaseGradient);
        float2 thetaGradient = direction * k + phaseGradient;
        float theta = dot(direction, worldXZ) * k + phaseWarp +
                      dataB.x * _AbyssalTime + dataB.z;
        float sineValue;
        float cosineValue;
        sincos(theta, sineValue, cosineValue);
        float qa = saturate(dataB.y) * amplitude;

        result.displacement.xz += direction * qa * cosineValue;
        result.displacement.y += amplitude * sineValue;
        slope += thetaGradient * (amplitude * cosineValue);
        amplitudeSum += amplitude;

        float2 derivativeX = -direction * qa * sineValue * thetaGradient.x;
        float2 derivativeY = -direction * qa * sineValue * thetaGradient.y;
        horizontalJacobian[0][0] += derivativeX.x;
        horizontalJacobian[0][1] += derivativeY.x;
        horizontalJacobian[1][0] += derivativeX.y;
        horizontalJacobian[1][1] += derivativeY.y;
    }

    result.slope = slope;
    result.normalWS = normalize(float3(-slope.x * _AbyssalSurface.x, 1.0,
                                       -slope.y * _AbyssalSurface.x));
    result.jacobian = determinant(horizontalJacobian);
    result.crest = saturate(result.displacement.y / amplitudeSum * 0.5 + 0.5);
    return result;
}

AbyssalWaveSample AbyssalEvaluateOpticalWaves(float2 worldXZ,
                                               float minimumMacroWavelength,
                                               float minimumMicroWavelength)
{
    AbyssalWaveSample result = AbyssalEvaluateWavesFiltered(worldXZ, minimumMacroWavelength);
    AbyssalWaveSample micro = AbyssalEvaluateMicroWavesFiltered(worldXZ, minimumMicroWavelength);
    result.displacement += micro.displacement;
    result.slope += micro.slope;
    result.normalWS = normalize(float3(-result.slope.x * _AbyssalSurface.x, 1.0,
                                       -result.slope.y * _AbyssalSurface.x));
    result.jacobian *= micro.jacobian;
    return result;
}

AbyssalWaveSample AbyssalEvaluateWaves(float2 worldXZ)
{
    return AbyssalEvaluateWavesFiltered(worldXZ, 0.0);
}

float AbyssalWaterHeight(float2 worldXZ)
{
    return _AbyssalWaterLevel + AbyssalEvaluateWaves(worldXZ).displacement.y;
}

float3 AbyssalBeerLambert(float opticalLength)
{
    float distance = min(max(0.0, opticalLength), _AbyssalAbsorption.w);
    return exp(-_AbyssalAbsorption.rgb * distance * _AbyssalUnderwater.w);
}

float AbyssalHenyeyGreenstein(float cosTheta)
{
    float g = clamp(_AbyssalAnisotropy, -0.9, 0.9);
    float g2 = g * g;
    float denominator = max(1e-3, pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
    return (1.0 - g2) / (4.0 * PI * denominator);
}

float2 AbyssalCausticRayHit(float2 surfaceXZ, AbyssalWaveSample sample,
                            float receiverDepth, float3 incidentDirection, float eta)
{
    float3 transmitted = refract(incidentDirection, sample.normalWS, eta);
    transmitted.y = min(transmitted.y, -0.04);
    float travel = max(0.01, receiverDepth + sample.displacement.y) /
                   max(0.04, -transmitted.y);
    return surfaceXZ + sample.displacement.xz + transmitted.xz * travel;
}

float AbyssalCausticChannel(float2 origin, float receiverDepth, float3 incidentDirection,
                            float eta, float footprint, AbyssalWaveSample center,
                            AbyssalWaveSample offsetX, AbyssalWaveSample offsetY)
{
    float2 hit = AbyssalCausticRayHit(origin, center, receiverDepth, incidentDirection, eta);
    float2 hitX = AbyssalCausticRayHit(origin + float2(footprint, 0.0), offsetX,
                                      receiverDepth, incidentDirection, eta);
    float2 hitY = AbyssalCausticRayHit(origin + float2(0.0, footprint), offsetY,
                                      receiverDepth, incidentDirection, eta);
    float2 derivativeX = (hitX - hit) / footprint;
    float2 derivativeY = (hitY - hit) / footprint;
    float determinant = abs(derivativeX.x * derivativeY.y - derivativeX.y * derivativeY.x);
    float irradiance = rcp(max(0.35, determinant));
    float focused = saturate((irradiance - 1.08) * 0.32 * _AbyssalCaustics.z);
    return pow(focused, 2.0);
}

float3 AbyssalPhysicalCaustic(float2 worldXZ, float depthBelowSurface, float3 directionToLight)
{
    if (depthBelowSurface < 0.0 || depthBelowSurface > _AbyssalCausticMaximumDepth) return 0.0;
    float3 incidentDirection = normalize(-directionToLight);
    if (incidentDirection.y > -0.02) return 0.0;

    float receiverDepth = depthBelowSurface * _AbyssalCaustics.y;
    float2 origin = worldXZ - incidentDirection.xz * receiverDepth /
                    max(0.05, -incidentDirection.y);
    // Macro waves shape the broad caustic pools while a multi-directional
    // optical micro spectrum supplies the irregular cells. Both spectra use
    // the same continuous phase warp, so no texture tile or patch seam can
    // reappear in the projected pattern. Depth-dependent filtering represents
    // the growing optical footprint and suppresses sub-pixel shimmer.
    float depthRatio = saturate(receiverDepth / max(1.0, _AbyssalCausticMaximumDepth));
    float minimumMacroWavelength = lerp(0.9, 2.8, depthRatio);
    float minimumMicroWavelength = lerp(0.20, 0.92, depthRatio);
    float footprint = max(0.11, minimumMicroWavelength * 0.42);
    AbyssalWaveSample center = AbyssalEvaluateOpticalWaves(
        origin, minimumMacroWavelength, minimumMicroWavelength);
    AbyssalWaveSample offsetX = AbyssalEvaluateOpticalWaves(
        origin + float2(footprint, 0.0), minimumMacroWavelength, minimumMicroWavelength);
    AbyssalWaveSample offsetY = AbyssalEvaluateOpticalWaves(
        origin + float2(0.0, footprint), minimumMacroWavelength, minimumMicroWavelength);
    float eta = rcp(max(1.001, _AbyssalOptics.x));
    float dispersion = _AbyssalCaustics.w * 0.0025;
    float3 focused = float3(
        AbyssalCausticChannel(origin, receiverDepth, incidentDirection, eta + dispersion,
                              footprint, center, offsetX, offsetY),
        AbyssalCausticChannel(origin, receiverDepth, incidentDirection, eta,
                              footprint, center, offsetX, offsetY),
        AbyssalCausticChannel(origin, receiverDepth, incidentDirection, eta - dispersion,
                              footprint, center, offsetX, offsetY));
    float depthFade = saturate(1.0 - depthBelowSurface / max(0.01, _AbyssalCausticMaximumDepth));
    float dynamicFocus = saturate(abs(center.dynamicVelocity) * 5.0 + abs(center.dynamicHeight) * 1.5);
    float3 energy = (focused + dynamicFocus * 0.05) * depthFade * _AbyssalCaustics.x;
    return energy / (1.0 + energy);
}

float AbyssalSolveRayToSurface(float3 rayOrigin, float3 rayDirection, float maximumDistance)
{
    if (abs(rayDirection.y) < 1e-4) return maximumDistance;
    float distance = (_AbyssalWaterLevel - rayOrigin.y) / rayDirection.y;
    distance = clamp(distance, 0.0, maximumDistance);
    [unroll(3)]
    for (int i = 0; i < 3; i++)
    {
        float3 samplePosition = rayOrigin + rayDirection * distance;
        float heightError = AbyssalWaterHeight(samplePosition.xz) - samplePosition.y;
        distance = clamp(distance + heightError / max(abs(rayDirection.y), 0.08), 0.0, maximumDistance);
    }
    return distance;
}

#endif
