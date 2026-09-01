using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 触发器上下文：效果配置、来源 Ability、目标等。
    /// </summary>
    public struct TriggerContext
    {
        public Effect EffectConfig;
        public Ability SourceAbility;
        public Entity TriggerSource;
        public Entity Target;
        /// <summary>技能多段伤害使用的段索引（从 1 开始），0 表示未指定或不适用。</summary>
        public int DamageSegmentIndex;
        /// <summary>攻击盒与受击体接触点；Buff 跳字等无盒体时为 false。</summary>
        public bool HasHitWorldPosition;
        public Vector3 HitWorldPosition;
    }
}
