// 程序化星空：按相机视角方向采样，与物体世界坐标无关。
#ifndef ACT_STARFIELD_INCLUDED
#define ACT_STARFIELD_INCLUDED

static const half ACT_PI = 3.14159265h;
static const half ACT_TAU = 6.28318530h;

// 从裁剪空间像素重建世界空间视线（仅依赖相机与屏幕位置）
float3 ACT_ViewDirFromClip(float4 positionCS)
{
    float4 ndc = float4(positionCS.x / positionCS.w, positionCS.y / positionCS.w, 1.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
    if (_ProjectionParams.x < 0.0)
        ndc.y = -ndc.y;
#endif
    float4 viewPos = mul(unity_CameraInvProjection, ndc);
    viewPos.xyz /= max(viewPos.w, 1e-5);
    return normalize(mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz));
}

// 世界方向 → 可循环滚动的等距柱状 UV
float2 ACT_DirToStarUV(float3 dir)
{
    dir = normalize(dir);
    float u = atan2(dir.x, dir.z) * (0.5 / ACT_PI) + 0.5;
    float v = asin(clamp(dir.y, -1.0, 1.0)) * (1.0 / ACT_PI) + 0.5;
    return float2(u, v);
}

// 绕世界 Y 轴旋转（时间流动主路径，360° 无缝循环）
float3 ACT_RotateY(float3 v, half rad)
{
    half c = cos(rad);
    half s = sin(rad);
    return float3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
}

half ACT_Hash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// 单层星点：密度、稀疏阈值、尺寸由 hash 决定
half ACT_StarLayer(float2 uv, half density, half threshold, half timePhase)
{
    float2 p = uv * density;
    float2 cell = floor(p);
    float2 f = frac(p);

    half h = ACT_Hash21(cell);
    if (h < threshold)
        return 0.0h;

    float2 star = float2(ACT_Hash21(cell + 17.31), ACT_Hash21(cell + 43.17));
    half d = length(f - star);
    half size = lerp(0.025h, 0.09h, ACT_Hash21(cell + 91.73));

    half core = smoothstep(size, size * 0.15h, d);
    half twinkle = 0.65h + 0.35h * sin(timePhase + h * ACT_TAU);
    return core * twinkle;
}

// 合成程序化星空（背景渐变 + 三层星 + 弱星云）
half3 ACT_SampleStarfield(
    float3 viewDirWS,
    half timeFlow,
    half scrollSpeed,
    half driftSpeed,
    half twinkleSpeed,
    half starDensity,
    half starBrightness,
    half nebulaStrength,
    half4 horizonColor,
    half4 zenithColor,
    half4 starColor)
{
    half flow = timeFlow * scrollSpeed;
    float3 dir = ACT_RotateY(viewDirWS, flow * ACT_TAU);

    // 副轴漂移：经度偏移同样按 TAU 取模，保证循环
    float2 uv = ACT_DirToStarUV(dir);
    uv.x = frac(uv.x + timeFlow * driftSpeed);

    half timePhase = timeFlow * twinkleSpeed * ACT_TAU;
    half density = max(starDensity, 1.0h);

    half stars = 0.0h;
    stars += ACT_StarLayer(uv, density * 48.0h, 0.90h, timePhase);
    stars += ACT_StarLayer(uv + 0.173, density * 96.0h, 0.93h, timePhase * 1.17h) * 0.55h;
    stars += ACT_StarLayer(uv + 0.419, density * 180.0h, 0.96h, timePhase * 0.83h) * 0.30h;

    half skyT = saturate(viewDirWS.y * 0.5h + 0.5h);
    half3 sky = lerp(horizonColor.rgb, zenithColor.rgb, pow(skyT, 1.35h));

    half nebula = ACT_Hash21(uv * 6.0h + half2(flow * 0.07h, 0.0h));
    nebula = smoothstep(0.55h, 0.95h, nebula) * nebulaStrength;
    sky += zenithColor.rgb * nebula * 0.35h;

    half3 starRgb = starColor.rgb * (stars * starBrightness * starColor.a);
    return sky + starRgb;
}

#endif
