Shader "Hidden/SonicWorld/Terrain Layer Bake"
{
    Properties
    {
        _LayerTexture ("Layer", 2D) = "white" {}
        _AlphaMap ("Alpha", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _LayerTexture;
            sampler2D _AlphaMap;
            float4 _AlphaChannel;
            float4 _LayerST;

            fixed4 Frag(v2f_img input) : SV_Target
            {
                fixed4 weights = tex2D(_AlphaMap, input.uv);
                fixed weight = dot(weights, _AlphaChannel);
                fixed3 albedo = tex2D(
                    _LayerTexture,
                    input.uv * _LayerST.xy + _LayerST.zw).rgb;
                return fixed4(albedo * weight, weight);
            }
            ENDCG
        }
    }
    Fallback Off
}
