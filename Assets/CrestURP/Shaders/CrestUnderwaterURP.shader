// Stereo-safe URP underwater composition for the MIT-licensed Crest simulation.
// Uses Crest's displacement texture arrays to solve the water crossing per pixel.

Shader "Crest/URP/Underwater"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "CrestURPUnderwater"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ CREST_FLOATING_ORIGIN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "../../ThirdParty/CrestWater4/Shaders/ShaderLibrary/Common.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanGlobals.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanInputsDriven.hlsl"
            #include "../../ThirdParty/CrestWater4/Shaders/OceanHelpersNew.hlsl"

            float _CrestURPWaterLevel;
            half4 _CrestURPShallowColor;
            half4 _CrestURPDeepColor;
            float4 _CrestURPAbsorption;
            half4 _CrestURPScattering;
            float _CrestURPVisibility;
            float4 _CrestURPCaustics;
            half4 _CrestURPCausticsColor;
            float4 _CrestURPPhysicalCaustics0;
            float4 _CrestURPPhysicalCaustics1;
            float4 _CrestURPPhysicalCaustics2;
            float4 _CrestURPMeniscus;
            float4 _CrestURPGodRays;
            float4 _CrestURPMainLightDirection;
            half4 _CrestURPMainLightColor;
            float _CrestURPUnderwaterEnabled;
            float _CrestURPPlanarReflectionRendering;

            void CrestSelectSurfaceSlices(float2 worldXZ, out uint slice0, out uint slice1,
                out float blend, out float texelWidth)
            {
                // Full-screen receiver positions are not ocean mesh vertices, so
                // select the finest cascade that actually contains the sample.
                // This remains valid when Crest snaps/follows the viewpoint.
                uint lastDataSlice = (uint)max(0.0, _SliceCount - 2.0);
                slice0 = lastDataSlice;
                blend = 0.0;
                texelWidth = _CrestCascadeData[lastDataSlice]._texelWidth;

                UNITY_LOOP
                for (uint slice = 0; slice < 15; slice++)
                {
                    if (slice > lastDataSlice) break;
                    const CascadeParams candidate = _CrestCascadeData[slice];
                    float2 candidateUV = WorldToUV(worldXZ, candidate);
                    float edgeDistance = min(min(candidateUV.x, candidateUV.y),
                        min(1.0 - candidateUV.x, 1.0 - candidateUV.y));
                    if (edgeDistance >= 0.0)
                    {
                        slice0 = slice;
                        texelWidth = candidate._texelWidth;
                        // Cross-fade through the outer 12.5% of a cascade.
                        blend = saturate((0.125 - edgeDistance) * 8.0);
                        break;
                    }
                }

                slice1 = min(slice0 + 1, lastDataSlice + 1);
            }

            float3 CrestSurfaceDisplacement(float2 worldXZ, out float texelWidth)
            {
                uint slice0;
                uint slice1;
                float blend;
                CrestSelectSurfaceSlices(worldXZ, slice0, slice1, blend, texelWidth);
                const CascadeParams cascade0 = _CrestCascadeData[slice0];
                const CascadeParams cascade1 = _CrestCascadeData[slice1];
                const float weight0 = (1.0 - blend) * cascade0._weight;
                const float weight1 = (1.0 - weight0) * cascade1._weight;
                float3 displacement = 0.0;
                if (weight0 > 0.001)
                {
                    displacement += weight0 * _LD_TexArray_AnimatedWaves.SampleLevel(
                        LODData_linear_clamp_sampler, WorldToUV(worldXZ, cascade0, slice0), 0.0).xyz;
                }
                if (weight1 > 0.001)
                {
                    displacement += weight1 * _LD_TexArray_AnimatedWaves.SampleLevel(
                        LODData_linear_clamp_sampler, WorldToUV(worldXZ, cascade1, slice1), 0.0).xyz;
                }
                return displacement;
            }

            float3 CrestSurfaceDisplacement(float2 worldXZ)
            {
                float unusedTexelWidth;
                return CrestSurfaceDisplacement(worldXZ, unusedTexelWidth);
            }

            float CrestSurfaceDisplacementY(float2 worldXZ)
            {
                return CrestSurfaceDisplacement(worldXZ).y;
            }

            float SolveSurfaceIntersection(float3 cameraPosition, float3 rayDirection)
            {
                float denominator = abs(rayDirection.y) < 0.0001 ? (rayDirection.y < 0.0 ? -0.0001 : 0.0001) : rayDirection.y;
                float distanceAlongRay = (_CrestURPWaterLevel - cameraPosition.y) / denominator;
                distanceAlongRay = clamp(distanceAlongRay, -4000.0, 4000.0);

                UNITY_UNROLL
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    float2 positionXZ = cameraPosition.xz + rayDirection.xz * distanceAlongRay;
                    float surfaceHeight = _CrestURPWaterLevel + CrestSurfaceDisplacementY(positionXZ);
                    distanceAlongRay = (surfaceHeight - cameraPosition.y) / denominator;
                    distanceAlongRay = clamp(distanceAlongRay, -4000.0, 4000.0);
                }
                return distanceAlongRay;
            }

            void CrestDisplacementAndGradient(float2 worldXZ, float radius,
                out float3 centerDisplacement, out float2 gradient, out float sampleRadius)
            {
                float texelWidth;
                centerDisplacement = CrestSurfaceDisplacement(worldXZ, texelWidth);
                sampleRadius = max(max(radius, 0.02), texelWidth * _CrestURPPhysicalCaustics2.w);
                float3 left = float3(worldXZ.x - sampleRadius, 0.0, worldXZ.y)
                    + CrestSurfaceDisplacement(worldXZ - float2(sampleRadius, 0.0));
                float3 right = float3(worldXZ.x + sampleRadius, 0.0, worldXZ.y)
                    + CrestSurfaceDisplacement(worldXZ + float2(sampleRadius, 0.0));
                float3 down = float3(worldXZ.x, 0.0, worldXZ.y - sampleRadius)
                    + CrestSurfaceDisplacement(worldXZ - float2(0.0, sampleRadius));
                float3 up = float3(worldXZ.x, 0.0, worldXZ.y + sampleRadius)
                    + CrestSurfaceDisplacement(worldXZ + float2(0.0, sampleRadius));
                float3 normal = normalize(cross(up - down, right - left));
                if (normal.y < 0.0) normal = -normal;
                gradient = -normal.xz / max(normal.y, 0.04);
            }

            float3 CrestRefractedSunRay(float2 gradient)
            {
                float3 surfaceNormal = normalize(float3(-gradient.x, 1.0, -gradient.y));
                float3 incidentRay = -normalize(_CrestURPMainLightDirection.xyz);
                return refract(incidentRay, surfaceNormal, rcp(max(_CrestURPPhysicalCaustics0.x, 1.01)));
            }

            float2 CrestRefractedHorizontalOffset(float2 gradient)
            {
                float3 ray = CrestRefractedSunRay(gradient);
                return ray.xz / max(-ray.y, 0.04);
            }

            float2 CrestRefractedReceiverPosition(float2 sourceXZ, float receiverY,
                float requestedRadius, out float2 gradient, out float usedRadius,
                out float3 surfacePoint)
            {
                float3 displacement;
                CrestDisplacementAndGradient(sourceXZ, requestedRadius, displacement, gradient, usedRadius);
                surfacePoint = float3(sourceXZ.x + displacement.x,
                    _CrestURPWaterLevel + displacement.y,
                    sourceXZ.y + displacement.z);
                float3 refractedRay = CrestRefractedSunRay(gradient);
                float rayY = min(refractedRay.y, -0.001);
                float distanceAlongRay = max(0.0, (receiverY - surfacePoint.y) / rayY);
                return surfacePoint.xz + refractedRay.xz * distanceAlongRay;
            }

            void CrestPhysicalCaustic(float2 receiverXZ, float receiverY,
                out float concentration, out float mappingGain, out float slopeMagnitude, out float transmission)
            {
                float requestedRadius = max(_CrestURPPhysicalCaustics0.y, 0.02);
                float2 sourceXZ = receiverXZ;
                float2 gradient;
                float radius;
                float3 center;
                float2 projected;

                // Invert the refracted surface-to-receiver mapping. High mode
                // performs a third correction for steep/choppy surfaces.
                UNITY_UNROLL
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    projected = CrestRefractedReceiverPosition(sourceXZ, receiverY,
                        requestedRadius, gradient, radius, center);
                    int correctionCount = _CrestURPPhysicalCaustics1.w > 0.5 ? 3 : 2;
                    float active = iteration < correctionCount ? 1.0 : 0.0;
                    sourceXZ += (receiverXZ - projected) * _CrestURPPhysicalCaustics0.z * active;
                }

                projected = CrestRefractedReceiverPosition(sourceXZ, receiverY,
                    requestedRadius, gradient, radius, center);
                float unusedRadius;
                float2 unusedGradient;
                float3 unusedPoint;
                float2 mappedXMinus = CrestRefractedReceiverPosition(
                    sourceXZ - float2(radius, 0.0), receiverY, requestedRadius,
                    unusedGradient, unusedRadius, unusedPoint);
                float2 mappedXPlus = CrestRefractedReceiverPosition(
                    sourceXZ + float2(radius, 0.0), receiverY, requestedRadius,
                    unusedGradient, unusedRadius, unusedPoint);
                float2 mappedZMinus = CrestRefractedReceiverPosition(
                    sourceXZ - float2(0.0, radius), receiverY, requestedRadius,
                    unusedGradient, unusedRadius, unusedPoint);
                float2 mappedZPlus = CrestRefractedReceiverPosition(
                    sourceXZ + float2(0.0, radius), receiverY, requestedRadius,
                    unusedGradient, unusedRadius, unusedPoint);

                // Direct ray-map Jacobian: reciprocal area compression is the
                // physically meaningful irradiance concentration.
                float2 jacobianX = (mappedXPlus - mappedXMinus) / (2.0 * radius);
                float2 jacobianZ = (mappedZPlus - mappedZMinus) / (2.0 * radius);
                float determinant = abs(jacobianX.x * jacobianZ.y - jacobianX.y * jacobianZ.x);
                float gain = min(rcp(max(determinant, _CrestURPPhysicalCaustics1.z)),
                    _CrestURPPhysicalCaustics1.y);
                mappingGain = gain;
                concentration = pow(max(gain - 1.0, 0.0), _CrestURPPhysicalCaustics1.x);
                slopeMagnitude = length(gradient);

                float3 normal = normalize(float3(-gradient.x, 1.0, -gradient.y));
                float3 incident = -normalize(_CrestURPMainLightDirection.xyz);
                float cosine = saturate(dot(normal, -incident));
                float f0 = pow((_CrestURPPhysicalCaustics0.x - 1.0) /
                    (_CrestURPPhysicalCaustics0.x + 1.0), 2.0);
                transmission = 1.0 - (f0 + (1.0 - f0) * pow(1.0 - cosine, 5.0));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                if (_CrestURPUnderwaterEnabled < 0.5 || _CrestURPPlanarReflectionRendering > 0.5)
                {
                    return source;
                }

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 0.00001;
                #else
                    bool isSky = rawDepth >= 0.99999;
                #endif

                float3 cameraPosition = GetCameraPositionWS();
                float3 farPosition = ComputeWorldSpacePosition(uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 rayDirection = SafeNormalize(farPosition - cameraPosition);
                float3 scenePosition = isSky
                    ? cameraPosition + rayDirection * 1000.0
                    : ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float sceneDistance = isSky ? 1000.0 : distance(cameraPosition, scenePosition);

                float cameraSurface = _CrestURPWaterLevel + CrestSurfaceDisplacementY(cameraPosition.xz);
                bool cameraUnderwater = cameraPosition.y < cameraSurface;
                float crossingDistance = SolveSurfaceIntersection(cameraPosition, rayDirection);
                bool crossesSurface = crossingDistance > 0.0 && crossingDistance < sceneDistance;

                float submergedDistance = 0.0;
                float underwaterMask = 0.0;
                if (cameraUnderwater)
                {
                    submergedDistance = crossesSurface ? crossingDistance : sceneDistance;
                    underwaterMask = 1.0;
                }
                else if (crossesSurface)
                {
                    submergedDistance = max(0.0, sceneDistance - crossingDistance);
                    underwaterMask = submergedDistance > 0.0001 ? 1.0 : 0.0;
                }

                if (underwaterMask < 0.5)
                {
                    return source;
                }

                float distortion = _CrestURPMeniscus.w * 0.0015;
                float2 distortionVector = float2(
                    sin(uv.y * 112.0 + _Time.y * 1.7) + sin(uv.x * 73.0 - _Time.y * 1.13),
                    cos(uv.x * 97.0 + _Time.y * 1.37) - cos(uv.y * 61.0 - _Time.y * 0.91));
                float distortionFade = saturate(submergedDistance * 0.14) * saturate(1.0 - abs(cameraPosition.y - cameraSurface) * 0.25);
                half3 sceneColor = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture, sampler_LinearClamp, saturate(uv + distortionVector * distortion * distortionFade), _BlitMipLevel).rgb;

                float effectiveDistance = min(submergedDistance, max(_CrestURPVisibility * 2.0, 1.0));
                half3 absorption = exp(-max(_CrestURPAbsorption.xyz, 0.0001) * effectiveDistance);
                float fogAmount = 1.0 - exp(-effectiveDistance / max(_CrestURPVisibility, 0.01));
                half3 waterColor = lerp(_CrestURPShallowColor.rgb, _CrestURPDeepColor.rgb,
                    saturate(effectiveDistance / max(_CrestURPVisibility, 0.01)));
                half3 color = sceneColor * absorption;
                color += lerp(_CrestURPScattering.rgb, waterColor, 0.62h) * fogAmount;

                if (!isSky)
                {
                    float surfaceAtScene = _CrestURPWaterLevel + CrestSurfaceDisplacementY(scenePosition.xz);
                    float depthBelowSurface = max(0.0, surfaceAtScene - scenePosition.y);
                    float causticFade = exp(-depthBelowSurface * _CrestURPCaustics.w)
                        * saturate(1.0 - depthBelowSurface / max(_CrestURPVisibility, 0.01));
                    float caustic = 0.0;
                    float causticMappingGain = 1.0;
                    float waveSlope = 0.0;
                    float fresnelTransmission = 1.0;
                    if (_CrestURPPhysicalCaustics0.w > 0.5 || _CrestURPPhysicalCaustics2.z > 0.5)
                    {
                        CrestPhysicalCaustic(scenePosition.xz, scenePosition.y,
                            caustic, causticMappingGain, waveSlope, fresnelTransmission);
                    }
                    float sunFacing = saturate(_CrestURPMainLightDirection.y);
                    float3 sceneNormal = SafeNormalize(cross(ddy(scenePosition), ddx(scenePosition)));
                    float receiverFacing = pow(saturate(abs(dot(sceneNormal,
                        normalize(_CrestURPMainLightDirection.xyz)))), _CrestURPPhysicalCaustics2.x);
                    color += _CrestURPCausticsColor.rgb * caustic * causticFade
                        * _CrestURPCaustics.x * sunFacing * receiverFacing * fresnelTransmission
                        * _CrestURPMainLightColor.rgb * _CrestURPPhysicalCaustics0.w;

                    if (_CrestURPPhysicalCaustics2.z > 0.5 && _CrestURPPhysicalCaustics2.z < 1.5)
                    {
                        float signedCompression = causticMappingGain - 1.0;
                        float3 neutral = float3(0.015, 0.02, 0.025);
                        float3 focused = lerp(neutral, float3(1.0, 0.72, 0.12),
                            saturate(signedCompression * 5.0));
                        float3 defocused = lerp(neutral, float3(0.04, 0.22, 0.85),
                            saturate(-signedCompression * 5.0));
                        color = signedCompression >= 0.0 ? focused : defocused;
                    }
                    else if (_CrestURPPhysicalCaustics2.z >= 1.5 && _CrestURPPhysicalCaustics2.z < 2.5)
                    {
                        color = lerp(float3(0.02, 0.08, 0.12), float3(0.2, 1.0, 0.72),
                            saturate(waveSlope * 8.0));
                    }
                    else if (_CrestURPPhysicalCaustics2.z >= 2.5 && _CrestURPPhysicalCaustics2.z < 3.5)
                    {
                        color = lerp(float3(0.02, 0.05, 0.12), float3(0.2, 0.72, 1.0),
                            saturate(depthBelowSurface / max(_CrestURPVisibility, 0.01)));
                    }
                    else if (_CrestURPPhysicalCaustics2.z >= 3.5)
                    {
                        float displacementHeight = surfaceAtScene - _CrestURPWaterLevel;
                        float magnitude = saturate(abs(displacementHeight) * 2.0);
                        color = displacementHeight >= 0.0
                            ? lerp(float3(0.015, 0.02, 0.025), float3(1.0, 0.18, 0.04), magnitude)
                            : lerp(float3(0.015, 0.02, 0.025), float3(0.04, 0.32, 1.0), magnitude);
                    }
                }

                float forwardScatter = pow(saturate(dot(rayDirection, -normalize(_CrestURPMainLightDirection.xyz))),
                    max(_CrestURPGodRays.y, 1.0));
                float rayNoise = saturate(0.55 + 0.45 * sin(dot(scenePosition.xz, float2(0.13, 0.17)) + _Time.y * 0.21));
                color += _CrestURPMainLightColor.rgb * _CrestURPScattering.rgb * forwardScatter
                    * rayNoise * fogAmount * _CrestURPGodRays.x;

                float distanceToSurface = abs(cameraPosition.y - cameraSurface);
                float nearSurface = saturate(1.0 - distanceToSurface / max(_CrestURPMeniscus.y, 0.001));
                float horizonDistance = abs(rayDirection.y);
                float meniscus = 1.0 - smoothstep(
                    _CrestURPMeniscus.x,
                    _CrestURPMeniscus.x * 3.5,
                    horizonDistance + distanceToSurface * 0.018);
                color += _CrestURPCausticsColor.rgb * meniscus * nearSurface * _CrestURPMeniscus.z;

                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
