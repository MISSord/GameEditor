// 近距镂空：屏幕空间抖动 clip（非透明混合）
// _ProximityDither 须在各 Pass 的 UnityPerMaterial 中声明。
// 使用 shader_feature_local _PROXIMITY_DITHER_ON：无 Keyword 的变体不含下列指令。
#ifndef ACT_PROXIMITY_DITHER_INCLUDED
#define ACT_PROXIMITY_DITHER_INCLUDED

#if defined(_PROXIMITY_DITHER_ON)
float ACT_InterleavedGradientNoise(float2 screenPos)
{
    return frac(52.9829189 * frac(dot(screenPos, float2(0.06711056, 0.00583715))));
}

void ACT_ApplyProximityDither(float4 positionCS)
{
    if (_ProximityDither <= 0.001h)
        return;

    float t = ACT_InterleavedGradientNoise(positionCS.xy);
    clip(t - _ProximityDither);
}
#else
void ACT_ApplyProximityDither(float4 positionCS) { }
#endif

#endif
