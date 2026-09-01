using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 战斗表现默认参数；可在 Inspector 挂到 <see cref="EGamePlayInit"/> 覆盖。
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFxPreset", menuName = "ACTGame/Combat Fx Preset", order = 101)]
    public sealed class CombatFxPreset : ScriptableObject
    {
        [Header("受击闪白")]
        [Tooltip("受击目标 MPB 闪白时长（秒）。")]
        public float HitFlashDuration = 0.12f;

        [Header("命中顿帧（本地攻击者）")]
        [Tooltip("HitStop 世界时间缩放持续时间（秒）。")]
        public float HitStopDuration = 0.08f;

        [Tooltip("HitStop 期间世界 TimeScale。")]
        [Range(0f, 1f)]
        public float HitStopWorldScale = 0.08f;

        [Tooltip("HitStop 时播放镜头冲击。")]
        public bool HitStopCameraImpact = true;

        [Header("时空断裂")]
        [Tooltip("TimeFracture 世界 TimeScale。")]
        [Range(0f, 1f)]
        public float TimeFractureWorldScale = 0.3f;

        static CombatFxPreset _active;

        /// <summary>当前生效预设；未指定时使用代码默认值。</summary>
        public static CombatFxPreset Active => _active != null ? _active : _active = CreateInstance<CombatFxPreset>();

        /// <summary>由 EGamePlayInit 注入项目级预设。</summary>
        public static void SetActive(CombatFxPreset preset) => _active = preset;
    }
}
