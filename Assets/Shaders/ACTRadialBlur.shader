// 径向模糊（战斗冲击 / HitStop 镜头）
Shader "ACT/RadialBlur"
{
    Properties
    {
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Intensity ("Intensity", Range(0, 1)) = 0.4
        _SampleCount ("Sample Count", Range(4, 16)) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RadialBlur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            float4 _Center;
            float _Intensity;
            float _SampleCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
                float2 center = _Center.xy;
                float2 dir = uv - center;
                float dist = length(dir);
                float2 delta = dir * (_Intensity * 0.06h + 0.001h);

                int count = (int)clamp(_SampleCount, 4.0, 16.0);
                half3 acc = 0;
                float weightSum = 0;

                [loop]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= count)
                        break;

                    float t = i / max(count - 1, 1);
                    float w = 1.0 - t * 0.65;
                    float2 sampleUv = uv - delta * t * (0.5 + dist);
                    acc += SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, sampleUv).rgb * w;
                    weightSum += w;
                }

                half3 rgb = acc / max(weightSum, 0.001);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
