using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>战斗单位抽象：EGamePlay.Combat 内 Action/Gate/Hit 只依赖此接口。</summary>
    public interface ICombatUnit
    {
        Entity Entity { get; }
        long Id { get; }
        bool IsDisposed { get; }

        VitalComponent CurrentVital { get; }
        CombatTagComponent TagHost { get; }
        CombatStateDirector StateDirector { get; }
        ResourceActionAbility ResourceAbility { get; }
        DamageActionAbility DamageAbility { get; }
        AddStatusActionAbility AddStatusAbility { get; }

        bool IsCanSpellSkill { get; }
        /// <summary>高优先级自身取消（大招顶普攻等）。闪避另走 <see cref="IsCanRollSkill"/>。</summary>
        bool IsCanSelfCancelSkill { get; }
        /// <summary>闪避槽：死亡/受击/禁移不可；沉默（仅 SkillForbid）仍可。</summary>
        bool IsCanRollSkill { get; }
        bool IsDead { get; }
        bool isTruePlayer { get; }
        /// <summary>技能轴、CD、动画走玩家钟（本地玩家，或 SkillTimeStop 发起者 hold）。</summary>
        bool UsesPlayerCombatClock { get; }

        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }

        /// <summary>移动子态（Run/Idle），由 StateDirector 写入。</summary>
        MoveTypeEnum CurMoveState { get; set; }

        /// <summary>StateDirector 合成后的可见行为态。</summary>
        void ApplyStateFromDirector(PlayerStateEnum state);

        /// <summary>MoveForbid 叶子计数 0→1 / 1→0。硬控断招与控制槽。</summary>
        void NotifyHardControlChanged(bool entered);

        /// <summary>Buff.Freeze 叶子计数 0→1 / 1→0。实体钟归零 + 冰冻外观。</summary>
        void NotifyFreezeChanged(bool entered);

        ISkillExecutionHandle ActiveExecution { get; set; }

        float GetTimeScale();
        void TriggerActionPoint(ActionPointType actionPointType, Entity action);
        /// <summary>角色 Buff 列表；伤害流程通过它按优先级 Dispatch。</summary>
        StatusComponent Status { get; }
        /// <summary>技能组等级。未挂组件时按 1 级结算。</summary>
        SkillLevelComponent SkillLevels { get; }
        void PopTagsFrom(TagSource source);
        bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked);

        /// <summary>HP 归零后的统一死亡落地。</summary>
        void ApplyDeath();
    }

    /// <summary>卸 Buff 上下文，供正在卸的那条 TriggerBuff 在 Revert 前开火。</summary>
    public interface ICombatRemoveStatusContext
    {
        /// <summary>Buff 持有者。</summary>
        ICombatUnit Owner { get; }
        /// <summary>正在卸、仍 Enable 的 Buff。</summary>
        Buff RemovedBuff { get; }
        /// <summary>表 Id。</summary>
        int BuffId { get; }
        /// <summary>本次卸除原因。</summary>
        BuffRemoveReason Reason { get; }
    }

    /// <summary>施加 Buff 行动上下文，供 PreGiveStatus / PreReceiveStatus 改单。</summary>
    public interface ICombatAddStatusContext
    {
        /// <summary>施加者。</summary>
        ICombatUnit Caster { get; }
        /// <summary>承受者。</summary>
        ICombatUnit Target { get; }
        /// <summary>本次要挂的 BuffId；Pre 回调可改写（本切片 Resolver 只处理免疫）。</summary>
        int BuffId { get; set; }
        /// <summary>请求原值，只读。</summary>
        int RequestedBuffId { get; }
        /// <summary>裁决结果：Interrupt 不落地不后置；Immunity / Resisted 不落地仍后置。</summary>
        AddStatusActionEffect Effect { get; set; }
    }

    /// <summary>施法行动上下文，供 PreSpell/PostSpell 行动点消费。</summary>
    public interface ICombatSpellActionContext
    {
        ICombatUnit Caster { get; }
        ICombatUnit InputTarget { get; }
        Vector3 InputPoint { get; }
        Vector3 InputDirection { get; }
        /// <summary>本次出手技能 ID。</summary>
        int SkillId { get; }
        /// <summary>本次出手 Sort（槽位/连招边，用于区分普攻、闪避、大招等）。</summary>
        int Sort { get; }
    }

    /// <summary>技能占轴句柄：Combat 层只读 Id/Sort/阶段，具体实现由 ACT 技能 runtime 提供。</summary>
    public interface ISkillExecutionHandle
    {
        long Id { get; }
        int Sort { get; }
        bool IsDisposed { get; }
        bool IsMainFinish { get; }
        /// <summary>轴已结束（可销毁 Session）。</summary>
        bool IsFinished { get; }

        void BreakSkill();
        void Tick(float deltaTime);
    }

    /// <summary>技能 CD 查询/启动，由 ACT 层 SkillCDTimer 实现。</summary>
    public interface ICooldownQuery
    {
        bool IsCDEnd(int skillId);
        void StartCooldown(int skillId);
    }

    /// <summary>一次出手意图（纯数据）。</summary>
    public struct CombatCastIntent
    {
        public int SkillId;
        public int Sort;
        public ICombatUnit Target;
        public Vector3 Point;
        public Vector3 Direction;
    }
}
