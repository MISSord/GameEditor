// 玩家立方体迷雾：盒体内、可视半径外的像素被雾色笼罩（含天空）
Shader "ACT/PlayerFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.92, 0.94, 0.97, 1)
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
            Name "PlayerFog"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            float4 _FogColor;
            float _Intensity;
            float4 _FogCenter;
            float4 _ClearCenter;
            float4 _FogHalfExtents;
            float _ClearRadius;
            float _FogFade;
            float _HeightFalloff;
            float _FogSky; // 1=天空也罩雾

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

            float BoxMask(float3 local, float3 halfExt, float soft)
            {
                float3 d = abs(local) - halfExt;
                float outside = length(max(d, 0.0)) + min(max(d.x, max(d.y, d.z)), 0.0);
                return 1.0 - smoothstep(0.0, max(0.01, soft), outside);
            }

            float IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                return rawDepth <= 1.0e-5 ? 1.0 : 0.0;
                #else
                return rawDepth >= 0.99999 ? 1.0 : 0.0;
                #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
                half3 src = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, uv).rgb;

                float3 center = _FogCenter.xyz;
                float3 halfExt = max(_FogHalfExtents.xyz, float3(0.01, 0.01, 0.01));
                float intensity = saturate(_Intensity);

                float rawDepth = SampleSceneDepth(uv);

                // 天空：远裁深度无法可靠重建世界坐标，相机在雾盒内时直接罩满雾色
                if (IsSkyDepth(rawDepth) > 0.5 && _FogSky > 0.5)
                {
                    float camInBox = BoxMask(_WorldSpaceCameraPos - center, halfExt, 0.05);
                    float skyFog = camInBox * intensity;
                    return half4(lerp(src, _FogColor.rgb, skyFog), 1);
                }

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 local = worldPos - center;

                float box = BoxMask(local, halfExt, max(0.05, _FogFade * 0.5));
                if (box <= 0.001)
                    return half4(src, 1);

                // 可视清晰半径以玩家（ClearCenter）为中心，跟随玩家移动
                float3 toClear = worldPos - _ClearCenter.xyz;
                float distH = length(toClear.xz);
                float dist3 = length(toClear);
                float dist = lerp(distH, dist3, saturate(_HeightFalloff));

                float clearR = max(0.01, _ClearRadius);
                float fade = max(0.01, _FogFade);
                float fog = smoothstep(clearR, clearR + fade, dist);
                fog *= box;
                fog *= intensity;

                half3 col = lerp(src, _FogColor.rgb, fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
