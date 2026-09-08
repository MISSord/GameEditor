using System;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 镜头震动参数（Trauma 模型）：位移 + 旋转 + FOV 冲击。
    /// 静态 Preset 对应命中轻重段，供表现包目录（CombatFxPackageCatalog）引用。
    /// </summary>
    [Serializable]
    public sealed class CameraShakeProfile
    {
        [Tooltip("每次叠加的创伤量 0~1（累加后 clamp）")]
        [Range(0f, 1f)]
        public float Trauma = 0.5f;

        [Tooltip("位移幅度（米，相机本地空间）")]
        public float PositionAmplitude = 0.1f;

        [Tooltip("旋转幅度（度，相机本地空间）")]
        public float RotationAmplitude = 1.5f;

        [Tooltip("FOV 冲击峰值（度，正值拉宽；命中冲击感）")]
        public float FovPunch = 3f;

        [Tooltip("FOV 回落时长（unscaled 秒）")]
        [Min(0.02f)]
        public float FovDuration = 0.18f;

        [Tooltip("创伤衰减速度（每秒）；越大停得越快")]
        public float TraumaDecay = 2.2f;

        [Tooltip("噪声频率（Hz）；越大抖动越细碎")]
        public float Frequency = 14f;

        [Tooltip("方向性 Kick 位移幅度（米，世界空间沿命中方向，命中后镜头被'带'一下再收回）")]
        public float KickAmplitude = 0.09f;

        [Tooltip("方向性 Kick 回落时长（unscaled 秒）")]
        [Min(0.02f)]
        public float KickDuration = 0.16f;

        // —— 内置分级预设（与 CombatFxPackageCatalog 的 HitReaction 轻重分级对应）——

        /// <summary>轻命中（普攻第一段）：轻微震感。</summary>
        public static CameraShakeProfile Light() => new CameraShakeProfile
        {
            Trauma = 0.35f,
            PositionAmplitude = 0.06f,
            RotationAmplitude = 0.8f,
            FovPunch = 1.5f,
            KickAmplitude = 0.04f,
            KickDuration = 0.12f,
        };

        /// <summary>重命中（段号 ≥ 2）：明显震感。</summary>
        public static CameraShakeProfile Heavy() => new CameraShakeProfile
        {
            Trauma = 0.55f,
            PositionAmplitude = 0.11f,
            RotationAmplitude = 1.6f,
            FovPunch = 3f,
            KickAmplitude = 0.09f,
            KickDuration = 0.16f,
        };

        /// <summary>暴击命中：强震 + 明显 FOV 冲击。</summary>
        public static CameraShakeProfile Crit() => new CameraShakeProfile
        {
            Trauma = 0.7f,
            PositionAmplitude = 0.14f,
            RotationAmplitude = 2.2f,
            FovPunch = 4f,
            KickAmplitude = 0.13f,
            KickDuration = 0.2f,
        };

        /// <summary>破韧/失衡：最重的一档。</summary>
        public static CameraShakeProfile StaggerBreak() => new CameraShakeProfile
        {
            Trauma = 0.9f,
            PositionAmplitude = 0.2f,
            RotationAmplitude = 3f,
            FovPunch = 6f,
            KickAmplitude = 0.18f,
            KickDuration = 0.28f,
        };

        /// <summary>受击（本地玩家被重击）：镜头向攻击来源方向撞一下。</summary>
        public static CameraShakeProfile HitTaken() => new CameraShakeProfile
        {
            Trauma = 0.6f,
            PositionAmplitude = 0.13f,
            RotationAmplitude = 1.2f,
            FovPunch = 2f,
            KickAmplitude = 0.1f,
            KickDuration = 0.18f,
        };
    }
}
