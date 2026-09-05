// 二游冰冻：去饱和青蓝、Fresnel 冰沿、晶体高光、霜噪、细闪
// 依赖：ACTCharacterProperties.hlsl（或同名 uniform）+ _DissolveMap
#ifndef ACT_FREEZE_INCLUDED
#define ACT_FREEZE_INCLUDED

half ACT_FreezeHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

half ACT_FreezeFrost(float2 uv, float3 positionWS)
{
    half a = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, uv * 3.4).r;
    half b = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, uv * 7.1 + 0.41).r;
    half w = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, positionWS.xz * 0.28 + positionWS.y * 0.08).r;
    return saturate(a * 0.45h + b * 0.25h + w * 0.3h);
}

/// <summary>把已打光颜色混成冰冻外观；amount=0 时原样返回。</summary>
half3 ACT_ApplyFreeze(
    half3 litColor,
    half3 albedo,
    half3 normalWS,
    half3 viewDirWS,
    half3 lightDirWS,
    half3 lightColor,
    float3 positionWS,
    float2 frostUV)
{
    half amount = saturate(_FreezeAmount);
    if (amount < 0.001h)
        return litColor;

    half ndv = saturate(dot(normalWS, viewDirWS));
    half fresnel = pow(1.0h - ndv, _FreezeFresnelPower) * _FreezeFresnelIntensity;

    half3 halfDir = normalize(lightDirWS + viewDirWS);
    half ndh = saturate(dot(normalWS, halfDir));
    half iceSpec = pow(ndh, _FreezeSpecPower) * _FreezeSpecIntensity;

    half frost = ACT_FreezeFrost(frostUV, positionWS);
    half frostLayer = lerp(0.2h, 1.0h, frost) * _FreezeNoiseStrength;
    half cracks = smoothstep(0.42h, 0.78h, frost);

    half sparkle = ACT_FreezeHash13(floor(positionWS * 22.0) + floor(_Time.y * 5.0));
    sparkle = saturate(sparkle - 0.84h) * 10.0h * _FreezeSparkle * (0.35h + fresnel);

    half luma = dot(litColor, half3(0.22h, 0.67h, 0.11h));
    half3 iceBase = luma * _FreezeTint.rgb * 0.42h + albedo * _FreezeTint.rgb * 0.38h;
    iceBase = lerp(iceBase, _FreezeColor.rgb, 0.22h + frostLayer * 0.45h);

    half3 iceLit = iceBase;
    iceLit += _FreezeColor.rgb * (fresnel * 0.85h + iceSpec * lightColor);
    iceLit += cracks * _FreezeColor.rgb * 0.22h;
    iceLit += sparkle * half3(0.82h, 0.94h, 1.0h);

    return lerp(litColor, iceLit, amount);
}

#endif
