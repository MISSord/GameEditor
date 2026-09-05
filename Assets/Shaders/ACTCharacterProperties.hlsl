// ACT/Character 与各 Pass 共用 UnityPerMaterial（SRP Batcher 要求字段一致）
#ifndef ACT_CHARACTER_PROPERTIES_INCLUDED
#define ACT_CHARACTER_PROPERTIES_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _DissolveMap_ST;
    half4 _BaseColor;
    half4 _HitFlashColor;
    half4 _DissolveEdgeColor;
    half4 _OutlineColor;
    half4 _ShadeColor;
    half4 _FreezeColor;
    half4 _FreezeTint;
    half _HitFlash;
    half _Dissolve;
    half _DissolveEdgeWidth;
    half _OutlineWidth;
    half _ForceOutline;
    half _ProximityDither;
    half _FreezeAmount;
    half _FreezeFresnelPower;
    half _FreezeFresnelIntensity;
    half _FreezeSpecPower;
    half _FreezeSpecIntensity;
    half _FreezeNoiseStrength;
    half _FreezeSparkle;
CBUFFER_END

#endif
