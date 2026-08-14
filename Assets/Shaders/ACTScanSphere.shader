// 扫描外扩球：半透明壳 + 边缘 Fresnel
Shader "ACT/ScanSphere"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 0.9, 1, 0.35)
        _EdgePower ("Edge Power", Range(0.5, 8)) = 2.5
        _EdgeBoost ("Edge Boost", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgePower;
                half _EdgeBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = normalize(input.viewDirWS);
                // Cull Front：法线朝内，用 abs 保证边缘仍亮
                half fresnel = pow(saturate(1.0h - abs(dot(n, v))), _EdgePower);
                half alpha = saturate(_Color.a + fresnel * _EdgeBoost);
                half3 rgb = _Color.rgb * (1.0h + fresnel * _EdgeBoost);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
