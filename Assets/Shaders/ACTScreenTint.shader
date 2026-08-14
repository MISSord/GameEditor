// 全屏颜色叠加（球形显现时浅蓝色罩）
Shader "ACT/ScreenTint"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.55, 0.78, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.28
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
            Name "ScreenTint"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            float4 _TintColor;
            float _Intensity;

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
                half3 src = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, uv).rgb;
                half3 tinted = lerp(src, _TintColor.rgb, saturate(_Intensity));
                // 轻微提亮一点浅蓝感
                tinted = lerp(tinted, tinted * _TintColor.rgb + _TintColor.rgb * 0.08h, saturate(_Intensity));
                return half4(tinted, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
