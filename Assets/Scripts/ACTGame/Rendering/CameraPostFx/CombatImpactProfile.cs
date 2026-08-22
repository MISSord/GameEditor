using System;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 战斗冲击镜头参数（CA + RadialBlur pulse）。
    /// </summary>
    [Serializable]
    public sealed class CombatImpactProfile
    {
        [Tooltip("色差峰值强度 0~1")]
        [Range(0f, 1f)]
        public float ChromaticAberrationPeak = 0.42f;

        [Tooltip("径向模糊峰值强度 0~1")]
        [Range(0f, 1f)]
        public float RadialBlurPeak = 0.5f;

        [Tooltip("效果持续时间（unscaled 秒）")]
        [Min(0.02f)]
        public float Duration = 0.12f;

        [Tooltip("径向模糊中心（屏幕 UV）")]
        public Vector2 RadialCenter = new Vector2(0.5f, 0.5f);

        [Tooltip("径向模糊采样数")]
        [Range(4, 16)]
        public int RadialSampleCount = 10;

        /// <summary>默认 HitStop 配套参数。</summary>
        public static CombatImpactProfile CreateHitStopDefault() => new CombatImpactProfile();

        /// <summary>按 HitStop 时长缩放 pulse。</summary>
        public CombatImpactProfile ScaledByHitStop(float hitStopDuration)
        {
            float scale = Mathf.Clamp(hitStopDuration / 0.08f, 0.65f, 1.35f);
            return new CombatImpactProfile
            {
                ChromaticAberrationPeak = ChromaticAberrationPeak * scale,
                RadialBlurPeak = RadialBlurPeak * scale,
                Duration = Mathf.Max(0.04f, Mathf.Max(Duration, hitStopDuration * 1.15f)),
                RadialCenter = RadialCenter,
                RadialSampleCount = RadialSampleCount,
            };
        }
    }
}
