Shader "Highway/RideStreak"
{
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                if (v.uv.y < 0)
                {
                    float2 p = (v.uv + float2(0, 2)) * 2 - 1;
                    float rim = smoothstep(.45, 1.40, length(p));
                    float alpha = v.color.a * rim * rim;
                    // Dither the very dark gradient before the 8-bit Spout texture conversion.
                    float noise = frac(52.9829189 * frac(dot(v.positionCS.xy, float2(.06711056, .00583715)))) - .5;
                    return half4(v.color.rgb, max(0, alpha + noise / 255.0 * step(.0001, alpha)));
                }
                float across = saturate(1 - abs(v.uv.x * 2 - 1));
                float along = smoothstep(0, .18, v.uv.y) * (1 - smoothstep(.7, 1, v.uv.y));
                return half4(v.color.rgb, v.color.a * across * across * along);
            }
            ENDHLSL
        }
    }
}
