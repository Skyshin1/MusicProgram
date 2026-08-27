Shader "Hidden/Sonar/Water Exit Lens Droplets"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Water Exit Lens Droplets"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_WaterExitSceneTex);

            float _EffectWeight;
            float _EffectTime;
            float _EdgeWidth;
            float _DropletDensity;
            float _FallSpeed;
            float _Distortion;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float DropletLayer(float2 uv, float density, float time, out float2 normal)
            {
                float2 gridUv = uv * float2(density, density * 0.62);
                float2 cell = floor(gridUv);
                float2 local = frac(gridUv) - 0.5;
                float random = Hash21(cell);
                float xOffset = (random - 0.5) * 0.62;
                float y = frac(random * 4.7 - time * (0.34 + random * 0.66));
                float2 delta = local - float2(xOffset, y - 0.5);
                delta.y *= 0.52;

                float radius = lerp(0.07, 0.18, random);
                float drop = 1.0 - smoothstep(radius * 0.45, radius, length(delta));
                float streakX = 1.0 - smoothstep(0.025, 0.075, abs(local.x - xOffset));
                float below = smoothstep(y - 0.52, y - 0.1, local.y + 0.5) *
                              (1.0 - smoothstep(y - 0.1, y + 0.02, local.y + 0.5));
                float streak = streakX * below * 0.38;
                normal = normalize(float2(delta.x, delta.y * 0.55) + 1e-4) * (drop + streak);
                return saturate(drop + streak);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float distanceToEdge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float edgeMask = 1.0 - smoothstep(0.0, max(_EdgeWidth, 0.001), distanceToEdge);

                float2 normalA;
                float2 normalB;
                float dropsA = DropletLayer(uv, _DropletDensity,
                    _EffectTime * _FallSpeed, normalA);
                float dropsB = DropletLayer(uv + float2(0.137, 0.071),
                    _DropletDensity * 0.63,
                    _EffectTime * _FallSpeed * 0.73 + 3.1, normalB);
                float drops = saturate(max(dropsA, dropsB * 0.72) * edgeMask * _EffectWeight);
                float2 normal = normalA + normalB * 0.6;
                float2 warpedUv = clamp(uv + normal * (_Distortion * drops), 0.001, 0.999);

                half3 scene = SAMPLE_TEXTURE2D_X(
                    _WaterExitSceneTex, sampler_LinearClamp, warpedUv).rgb;
                half3 wetHighlight = half3(0.68, 0.84, 0.9);
                scene = lerp(scene, scene * 0.92 + wetHighlight * 0.08, drops * 0.45);
                return half4(scene, 1.0);
            }
            ENDHLSL
        }
    }
}
