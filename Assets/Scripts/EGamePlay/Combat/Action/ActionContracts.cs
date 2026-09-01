using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>战斗行动能力（挂在 ICombatUnit 上）。</summary>
    public interface IActionAbility
    {
        ICombatUnit OwnerEntity { get; }
        bool Enable { get; set; }
    }

    /// <summary>
    /// 行动执行接口，造成伤害、治疗英雄、赋给效果等属于战斗行动，战斗行动是实际应用技能效果 <see cref="AbilityEffect"/>，对战斗直接产生影响的行为
    /// 技能和buff都是挂在角色身上的一种状态，而技能表现则是一系列连续的行为（行动、事件）的组合所造成的表现和数值变化
    /// </summary>
    /// <remarks>
    /// 战斗行动由战斗实体主动发起，包含本次行动所需要用到的所有数据，并且会触发一系列行动点事件 <see cref="ActionPoint"/>
    /// </remarks>
    public interface IActionExecute
    {
        /// 行动实体
        ICombatUnit Creator { get; set; }

        /// 目标对象
        ICombatUnit Target { get; set; }
    }

    /// <summary>触发器上下文：效果配置、来源 Ability、目标等，随 IActionExecute 传递。</summary>
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
