Shader "Hidden/DownsampleDepth"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DownsampleDepth"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            #pragma target 4.5
            #pragma editor_sync_compilation

            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 depths;

                // 当前半分辨率像素，对应原始深度图中的 2x2 区域
                uint2 fullResTopLeftCorner = uint2(input.positionCS.xy * 2.0);

                // 读取 Blitter 实际传进来的 source，也就是相机深度图
                // 不再读取全局 _CameraDepthTexture / LoadSceneDepth
                depths.x = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(0, 1)).r;
                depths.y = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(1, 1)).r;
                depths.z = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(1, 0)).r;
                depths.w = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(0, 0)).r;

                float minDepth = Min3(depths.x, depths.y, min(depths.z, depths.w));
                float maxDepth = Max3(depths.x, depths.y, max(depths.z, depths.w));

                // 原版是 minDepth / maxDepth 交错返回：
                // return (uint(input.positionCS.x + input.positionCS.y) & 1) > 0 ? minDepth : maxDepth;
                //
                // 这会产生棋盘/条纹状的深度变化。
                // 改成固定取最近深度，避免半分辨率体积雾上采样时出现条纹。
                #if UNITY_REVERSED_Z
                    return maxDepth;
                #else
                    return minDepth;
                #endif
            }

            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DownsampleDepth"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            #pragma editor_sync_compilation

            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 depths;

                // 当前半分辨率像素，对应原始深度图中的 2x2 区域
                uint2 fullResTopLeftCorner = uint2(input.positionCS.xy * 2.0);

                // 读取 Blitter 传入的相机深度图
                depths.x = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(0, 1)).r;
                depths.y = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(1, 1)).r;
                depths.z = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(1, 0)).r;
                depths.w = LOAD_TEXTURE2D_X(_BlitTexture, fullResTopLeftCorner + uint2(0, 0)).r;

                float minDepth = Min3(depths.x, depths.y, min(depths.z, depths.w));
                float maxDepth = Max3(depths.x, depths.y, max(depths.z, depths.w));

                // 固定取最近深度，避免 min/max 交错造成可见条纹
                #if UNITY_REVERSED_Z
                    return maxDepth;
                #else
                    return minDepth;
                #endif
            }

            ENDHLSL
        }
    }

    Fallback Off
}