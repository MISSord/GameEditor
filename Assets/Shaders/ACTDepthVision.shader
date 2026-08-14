// 深度视界：按线性眼空间深度，近白 → 深远灰
Shader "ACT/DepthVision"
{
    Properties
    {
        _NearColor ("Near Color", Color) = (1, 1, 1, 1)
        _FarColor ("Far Color", Color) = (0.18, 0.18, 0.18, 1)
        _DepthNear ("Depth Near", Float) = 1
        _DepthFar ("Depth Far", Float) = 35
        _Intensity ("Intensity", Range(0, 1)) = 1
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
            Name "DepthVision"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            float4 _NearColor;
            float4 _FarColor;
            float _DepthNear;
            float _DepthFar;
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
                // 某些 Blit 网格 UV 翻转
                #if UNITY_UV_STARTS_AT_TOP
                // URP fullscreen mesh 通常已处理；保留 uv
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
                float rawDepth = SampleSceneDepth(uv);
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float range = max(0.0001, _DepthFar - _DepthNear);
                float t = saturate((eyeDepth - _DepthNear) / range);
                // 远处略加压暗曲线，更接近深度图观感
                t = t * t * (3.0 - 2.0 * t);

                half3 depthCol = lerp(_NearColor.rgb, _FarColor.rgb, t);
                half3 src = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, uv).rgb;
                half3 col = lerp(src, depthCol, saturate(_Intensity));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
