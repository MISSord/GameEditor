using System;

namespace ACTGameEditor.Combat
{
    /// <summary>表现包触发来源。</summary>
    public enum CombatFxTriggerKind : byte
    {
        /// <summary>战斗行动点（伤害、施法等）。</summary>
        ActionPoint = 0,
        /// <summary>技能时间轴 MsgEvent（PlayFxPackage）。</summary>
        TimelineMessage = 1,
        /// <summary>实体死亡事件。</summary>
        EntityDead = 2,
        /// <summary>仅代码 / 调试手动调用。</summary>
        Manual = 3,
    }

    /// <summary>ActionPoint 规则过滤条件。</summary>
    [Flags]
    public enum CombatFxTriggerFlags : byte
    {
        None = 0,
        /// <summary>仅本地玩家实体触发（如攻击者侧 HitStop）。</summary>
        LocalTruePlayerOnly = 1 << 0,
        /// <summary>DamageAction 为 Dodge 时不播。</summary>
        SkipOnDodge = 1 << 1,
        /// <summary>DamageAction 为 Immunity 时不播。</summary>
        SkipOnImmunity = 1 << 2,
        /// <summary>DamageAction 为 Interrupt 时不播。</summary>
        SkipOnInterrupt = 1 << 3,
        /// <summary>DamageValue &lt;= 0 时不播。</summary>
        RequirePositiveDamage = 1 << 4,
    }
}
