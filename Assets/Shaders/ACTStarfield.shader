// ACT Starfield — 程序化星空（视角相关、位置无关、时间循环）
// 视线由屏幕像素 + 相机矩阵重建，物体平移不改变星图；环视 / 转相机才会变化。
// 时间流动：绕世界 Y 轴旋转采样方向（360° 无缝）+ 可选经度漂移。

Shader "ACT/Starfield"
{
    Properties
    {
        [Header(Sky Background)]
        _HorizonColor ("Horizon Color", Color) = (0.04, 0.06, 0.14, 1)
        _ZenithColor ("Zenith Color", Color) = (0.01, 0.02, 0.06, 1)

        [Header(Stars)]
        [HDR] _StarColor ("Star Color", Color) = (1, 0.95, 0.85, 1)
        _StarDensity ("Star Density", Range(0.25, 2)) = 1
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.35
        _NebulaStrength ("Nebula Strength", Range(0, 1)) = 0.25

        [Header(Time Flow)]
        _TimeFlow ("Time Flow Scale", Range(0, 3)) = 1
        _ScrollSpeed ("Scroll Speed (Y rot / cycle)", Range(0, 0.25)) = 0.035
        _DriftSpeed ("Drift Speed (U scroll / cycle)", Range(0, 0.5)) = 0.08
        _TwinkleSpeed ("Twinkle Speed", Range(0, 3)) = 1

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "Starfield"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ACTStarfield.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _StarColor;
                half _StarDensity;
                half _StarBrightness;
                half _NebulaStrength;
                half _TimeFlow;
                half _ScrollSpeed;
                half _DriftSpeed;
                half _TwinkleSpeed;
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 仅依赖相机投影 + 屏幕位置，不使用 positionWS 采样
                float3 viewDirWS = ACT_ViewDirFromClip(input.positionCS);
                half timeFlow = _Time.y * _TimeFlow;

                half3 color = ACT_SampleStarfield(
                    viewDirWS,
                    timeFlow,
                    _ScrollSpeed,
                    _DriftSpeed,
                    _TwinkleSpeed,
                    _StarDensity,
                    _StarBrightness,
                    _NebulaStrength,
                    _HorizonColor,
                    _ZenithColor,
                    _StarColor);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
