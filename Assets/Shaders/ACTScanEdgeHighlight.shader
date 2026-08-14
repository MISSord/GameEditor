// 扫描揭示：物体边缘 Fresnel 高亮（独立 Shader，可透视）
Shader "ACT/ScanEdgeHighlight"
{
    Properties
    {
        [HDR] _EdgeColor ("Edge Color", Color) = (0.3, 1.2, 1.8, 1)
        _EdgePower ("Edge Power", Range(0.5, 8)) = 3
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 2
        _Reveal ("Reveal", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ScanEdge"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            // Always：被遮挡也能看到边缘高亮
            ZTest Always
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor;
                half _EdgePower;
                half _EdgeIntensity;
                half _Reveal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_Reveal - 0.5h);

                half3 n = normalize(input.normalWS);
                half3 v = normalize(input.viewDirWS);
                half ndotv = saturate(dot(n, v));
                half fresnel = pow(1.0h - ndotv, _EdgePower);
                half3 col = _EdgeColor.rgb * fresnel * _EdgeIntensity;
                return half4(col, fresnel);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
