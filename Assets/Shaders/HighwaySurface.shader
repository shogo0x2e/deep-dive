Shader "Highway/Surface"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float distance : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.color = v.color;
                o.distance = distance(world, _WorldSpaceCameraPos);
                return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                // Traffic fades only at the far horizon. Screen-space dithering retains
                // correct opaque depth/occlusion without sorting transparent car bodies.
                float dither = frac(52.9829189 * frac(dot(v.positionCS.xy, float2(0.06711056, 0.00583715))));
                clip(v.color.a - max(0.0001, dither));
                float fog = 1-exp(-v.distance*0.0019);
                return half4(lerp(v.color.rgb, float3(0.009,0.011,0.018), fog*fog),1);
            }
            ENDHLSL
        }
    }
}
