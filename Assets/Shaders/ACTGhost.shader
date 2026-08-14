// ACT Ghost — 闪避 / 冲刺残影（Unlit 半透明，不参与描边与 Stencil）
Shader "ACT/Ghost"
{
    Properties
    {
        [HDR] _GhostColor ("Ghost Color", Color) = (0.35, 0.75, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.4
        _EmissionBoost ("Emission Boost", Range(0, 3)) = 1.2
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
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GhostColor;
                half _Alpha;
                half _EmissionBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 rgb = _GhostColor.rgb * _EmissionBoost;
                half alpha = _GhostColor.a * _Alpha;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
