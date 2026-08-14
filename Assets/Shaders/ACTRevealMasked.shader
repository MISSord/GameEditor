// 显现物体：平时不可见；仅当处于球形 or 手电筒圆锥遮罩内时显示（Shader 裁剪）
Shader "ACT/RevealMasked"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (0.2, 0.9, 1, 0.9)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        _Softness ("Edge Softness Boost", Range(0, 1)) = 0.15
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
            Name "RevealMasked"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Softness;
            CBUFFER_END

            float _RevealSphereActive;
            float4 _RevealSphereCenter;
            float _RevealSphereRadius;

            float _RevealConeActive;
            float4 _RevealConeOrigin;
            float4 _RevealConeDir;
            float _RevealConeRange;
            float _RevealConeCosOuter;
            float _RevealConeCosInner;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half EvalSphere(float3 posWS)
            {
                if (_RevealSphereActive < 0.5)
                    return 0;

                float d = distance(posWS, _RevealSphereCenter.xyz);
                float r = max(0.001, _RevealSphereRadius);
                float soft = lerp(0.02, 0.25, saturate(_Softness));
                return 1.0 - smoothstep(r * (1.0 - soft), r, d);
            }

            half EvalCone(float3 posWS)
            {
                if (_RevealConeActive < 0.5)
                    return 0;

                float3 toP = posWS - _RevealConeOrigin.xyz;
                float dist = length(toP);
                if (dist < 1e-5)
                    return 1;

                float3 dir = normalize(_RevealConeDir.xyz);
                float nd = dot(toP / dist, dir);

                // nd >= cosOuter 在锥内；用 smoothstep 做软边
                float ang = smoothstep(_RevealConeCosOuter, _RevealConeCosInner, nd);
                float soft = lerp(0.02, 0.2, saturate(_Softness));
                float distMask = 1.0 - smoothstep(_RevealConeRange * (1.0 - soft), _RevealConeRange, dist);
                return ang * distMask;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half mask = max(EvalSphere(input.positionWS), EvalCone(input.positionWS));
                clip(mask - 0.001h);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 col = tex * _Color;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
