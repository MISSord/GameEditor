// ACT Ice — 二游冰冻晶体外壳（半透明叠加，不写 Stencil）
// 用法：
// 1. 角色继续用 ACT/Character，MPB _FreezeAmount 做身体结霜；
// 2. 同一 Renderer 加本材质作第二槽，或挂一层同模，出冰壳/冰沿。
// 与 ACT/Ghost、ACT/ScanEdgeHighlight 同一套 URP Unlit 叠加写法。

Shader "ACT/Ice"
{
    Properties
    {
        [Header(Freeze)]
        _FreezeAmount ("Freeze Amount", Range(0, 1)) = 1
        [HDR] _IceColor ("Ice Color", Color) = (0.45, 0.88, 1.15, 1)
        [HDR] _IceRimColor ("Rim Color", Color) = (0.75, 0.95, 1.4, 1)
        _IceFill ("Fill Opacity", Range(0, 1)) = 0.22
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 3.2
        _FresnelIntensity ("Fresnel Intensity", Range(0, 4)) = 1.6
        _Sparkle ("Sparkle", Range(0, 1)) = 0.55
        _NoiseScale ("Frost Scale", Range(0.2, 8)) = 2.4

        [Header(Ice Crust)]
        _CrustWidth ("Crust Width", Range(0, 0.08)) = 0.014
        _CrustOpacity ("Crust Opacity", Range(0, 2)) = 0.85

        [Header(Optional Frost Map)]
        _FrostMap ("Frost Noise (R)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+45"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // 表面霜膜：贴在身体上，Z 不写，避免和第二套 Character 抢深度
        Pass
        {
            Name "IceFilm"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrostMap);
            SAMPLER(sampler_FrostMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrostMap_ST;
                half4 _IceColor;
                half4 _IceRimColor;
                half _FreezeAmount;
                half _IceFill;
                half _FresnelPower;
                half _FresnelIntensity;
                half _Sparkle;
                half _NoiseScale;
                half _CrustWidth;
                half _CrustOpacity;
            CBUFFER_END

            half Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _FrostMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half amount = saturate(_FreezeAmount);
                clip(amount - 0.01h);

                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half ndv = saturate(dot(n, v));
                half fresnel = pow(1.0h - ndv, _FresnelPower) * _FresnelIntensity;

                half frost = SAMPLE_TEXTURE2D(_FrostMap, sampler_FrostMap, input.uv * _NoiseScale).r;
                half frostW = SAMPLE_TEXTURE2D(_FrostMap, sampler_FrostMap, input.positionWS.xz * (_NoiseScale * 0.12)).r;
                frost = saturate(frost * 0.55h + frostW * 0.45h);
                half cracks = smoothstep(0.4h, 0.82h, frost);

                half sparkle = Hash13(floor(input.positionWS * 20.0) + floor(_Time.y * 4.5));
                sparkle = saturate(sparkle - 0.86h) * 12.0h * _Sparkle * (0.25h + fresnel);

                half film = saturate(_IceFill + fresnel * 0.35h + cracks * 0.2h);
                half3 rgb = _IceColor.rgb * film + _IceRimColor.rgb * fresnel + sparkle * _IceRimColor.rgb;
                half alpha = saturate(film + fresnel * 0.5h) * amount;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }

        // 外扩冰壳：Cull Front，鸣潮/原神冻结常见的一层「厚度」
        Pass
        {
            Name "IceCrust"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrostMap);
            SAMPLER(sampler_FrostMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrostMap_ST;
                half4 _IceColor;
                half4 _IceRimColor;
                half _FreezeAmount;
                half _IceFill;
                half _FresnelPower;
                half _FresnelIntensity;
                half _Sparkle;
                half _NoiseScale;
                half _CrustWidth;
                half _CrustOpacity;
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
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                half amount = saturate(_FreezeAmount);
                float width = _CrustWidth * amount;
                float3 posOS = input.positionOS.xyz + normalize(input.normalOS) * width;
                float3 positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half amount = saturate(_FreezeAmount);
                clip(amount - 0.01h);
                clip(_CrustWidth - 0.0005h);

                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                // Cull Front 看到的是壳内壁，用 abs 让沿边更亮
                half ndv = abs(dot(n, v));
                half rim = pow(1.0h - saturate(ndv), 2.2h);

                half3 rgb = lerp(_IceColor.rgb, _IceRimColor.rgb, rim) * _CrustOpacity * amount;
                return half4(rgb * (0.35h + rim), 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
