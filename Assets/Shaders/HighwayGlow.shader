Shader "Highway/Glow"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
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
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv*2-1; o.color=v.color; return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float r2=dot(v.uv,v.uv);
                float profile=(exp(-r2*70)*2.5 + exp(-r2*13)*0.13 + exp(-r2*4)*0.015)*saturate((1-r2)*4);
                if(v.color.a>1.5) profile=exp(-r2*4)*saturate((1-r2)*3);
                return half4(v.color.rgb*profile,0);
            }
            ENDHLSL
        }
    }
}
