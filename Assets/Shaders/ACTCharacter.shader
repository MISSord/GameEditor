// ACT Character — URP 手搓角色着色器
// 支持：主光 Half-Lambert、受击闪白、冰冻、噪声图溶解、遮挡外轮廓、ShadowCaster

Shader "ACT/Character"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Hit Flash)]
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _HitFlashColor ("Hit Flash Color", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        _DissolveMap ("Dissolve Noise", 2D) = "white" {}
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.2)) = 0.04
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (2, 0.6, 0.1, 1)

        [Header(Occlusion Outline)]
        _OutlineColor ("Outline Color", Color) = (0.2, 0.85, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.025
        _ForceOutline ("Force Outline", Range(0, 1)) = 0

        [Header(Depth Vision)]
        [Toggle] _IncludeInDepthVision ("Include In Depth Vision", Float) = 1

        [Header(Proximity Dither Fade)]
        _ProximityDither ("Proximity Dither (0..1)", Range(0, 1)) = 0

        [Header(Lighting)]
        _ShadeColor ("Shade Color", Color) = (0.4, 0.4, 0.45, 1)

        [Header(Freeze)]
        _FreezeAmount ("Freeze Amount", Range(0, 1)) = 0
        [HDR] _FreezeColor ("Freeze Color", Color) = (0.42, 0.86, 1.15, 1)
        _FreezeTint ("Ice Tint", Color) = (0.58, 0.82, 0.98, 1)
        _FreezeFresnelPower ("Fresnel Power", Range(1, 8)) = 3.4
        _FreezeFresnelIntensity ("Fresnel Intensity", Range(0, 4)) = 1.35
        _FreezeSpecPower ("Ice Spec Power", Range(8, 128)) = 48
        _FreezeSpecIntensity ("Ice Spec Intensity", Range(0, 2)) = 0.7
        _FreezeNoiseStrength ("Frost Noise", Range(0, 1)) = 0.4
        _FreezeSparkle ("Sparkle", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            // Geometry+1：ForwardLit 须在场景 Opaque(2000) 之后，否则先写 Stencil=1、墙后改深度，
            // OcclusionOutline 的 Stencil NotEqual 会把「被墙挡住的像素」误杀（TestZone 常见，ShaderTest 因遮挡物常挡在镜头前而不易复现）。
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            // 用户位 bit0：标记已画出的身体像素，供之后的 OcclusionOutline 剔除自身互遮
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            #include "ACTCharacterProperties.hlsl"
            #include "ACTFreeze.hlsl"

            #pragma shader_feature_local _PROXIMITY_DITHER_ON
            #include "ACTProximityDither.hlsl"

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
                float2 dissolveUV : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void ApplyDissolve(float2 dissolveUV, inout half3 color)
            {
                half noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, dissolveUV).r;
                half threshold = _Dissolve;
                clip(noise - threshold);

                half edge = smoothstep(threshold, threshold + _DissolveEdgeWidth, noise);
                color = lerp(_DissolveEdgeColor.rgb, color, edge);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.dissolveUV = TRANSFORM_TEX(input.uv, _DissolveMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 n = normalize(input.normalWS);
                half3 shade = lerp(_ShadeColor.rgb, _FreezeTint.rgb * 0.38h, saturate(_FreezeAmount));
                half ndotl = saturate(dot(n, mainLight.direction) * 0.5h + 0.5h);
                half3 lighting = lerp(shade, mainLight.color, ndotl) * mainLight.shadowAttenuation;
                lighting += SampleSH(n) * 0.35h;

                half3 color = albedo.rgb * lighting;
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                color = ACT_ApplyFreeze(
                    color,
                    albedo.rgb,
                    n,
                    viewDir,
                    mainLight.direction,
                    mainLight.color,
                    input.positionWS,
                    input.dissolveUV);
                color = lerp(color, _HitFlashColor.rgb, _HitFlash);

                ApplyDissolve(input.dissolveUV, color);
                ACT_ApplyProximityDither(input.positionCS);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // 必须在墙等 Opaque Forward 之后画，否则描边会被墙色盖住（Frame Debugger 有 Pass 但画面没有）
        // URP 顺序：SRPDefaultUnlit → UniversalForward → UniversalForwardOnly
        Pass
        {
            Name "OcclusionOutline"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Front
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp NotEqual
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            #include "ACTCharacterProperties.hlsl"

            #pragma shader_feature_local _PROXIMITY_DITHER_ON
            #include "ACTProximityDither.hlsl"

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
                float2 dissolveUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(posOS);
                output.dissolveUV = TRANSFORM_TEX(input.uv, _DissolveMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.dissolveUV).r;
                clip(noise - _Dissolve);
                ACT_ApplyProximityDither(input.positionCS);
                return _OutlineColor;
            }
            ENDHLSL
        }

        // 扫描揭示旧路径（_ForceOutline>0.5）；LightMode 让给 OcclusionOutline
        Pass
        {
            Name "ForcedOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            #include "ACTCharacterProperties.hlsl"

            #pragma shader_feature_local _PROXIMITY_DITHER_ON
            #include "ACTProximityDither.hlsl"

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
                float2 dissolveUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 未强制时顶点塌缩，避免无效外扩开销
                float width = _OutlineWidth * step(0.5h, _ForceOutline);
                float3 posOS = input.positionOS.xyz + normalize(input.normalOS) * width;
                output.positionCS = TransformObjectToHClip(posOS);
                output.dissolveUV = TRANSFORM_TEX(input.uv, _DissolveMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_ForceOutline - 0.5h);
                half noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.dissolveUV).r;
                clip(noise - _Dissolve);
                ACT_ApplyProximityDither(input.positionCS);
                return _OutlineColor;
            }
            ENDHLSL
        }

        // URP 生成 _CameraDepthTexture / DepthPrepass 时会画 DepthOnly
        // 注意：此处不要写 Stencil。若在墙之前写入 Ref=1，墙只改深度不改模板，
        // 墙后像素仍为 1，后续 OcclusionOutline 的 NotEqual 会被误杀。
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            #include "ACTCharacterProperties.hlsl"

            #pragma shader_feature_local _PROXIMITY_DITHER_ON
            #include "ACTProximityDither.hlsl"

            // 全局开关 × 单对象开关（MPB / 材质 _IncludeInDepthVision）
            float _ACT_IncludeCharacterDepth;
            float _IncludeInDepthVision;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 dissolveUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dissolveUV = TRANSFORM_TEX(input.uv, _DissolveMap);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // 全局总闸
                clip(_ACT_IncludeCharacterDepth - 0.5h);
                // 单对象精细控制（默认材质/MPB 为 1）
                clip(_IncludeInDepthVision - 0.5h);

                half noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.dissolveUV).r;
                clip(noise - _Dissolve);
                ACT_ApplyProximityDither(input.positionCS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            #include "ACTCharacterProperties.hlsl"

            #pragma shader_feature_local _PROXIMITY_DITHER_ON
            #include "ACTProximityDither.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                float2 dissolveUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClipLocal(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetShadowPositionHClipLocal(input);
                output.dissolveUV = TRANSFORM_TEX(input.uv, _DissolveMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.dissolveUV).r;
                clip(noise - _Dissolve);
                ACT_ApplyProximityDither(input.positionCS);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
