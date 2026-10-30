Shader "Highway/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.direction=v.positionOS.xyz; return o; }
            half4 Frag(Varyings v):SV_Target
            {
                float3 d=normalize(v.direction);
                float h=max(d.y,0);
                float band=exp(-abs(d.y)*18);
                float city=exp(-pow(d.x-0.35,2)*4);
                float3 c=lerp(float3(0.004,0.007,0.019),float3(0.0007,0.0014,0.0045),saturate(h*1.8));
                c+=band*lerp(float3(0.010,0.009,0.014),float3(0.030,0.014,0.008),city);
                c=lerp(c,float3(0.002,0.004,0.008),saturate(-d.y*12));
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
