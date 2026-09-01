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
        bool IsCanSelfCancelSkill { get; }
        bool IsDead { get; }
        bool isTruePlayer { get; }

        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }

        /// <summary>移动子态（Run/Idle），由 StateDirector 写入。</summary>
        MoveTypeEnum CurMoveState { get; set; }

        /// <summary>StateDirector 合成后的可见行为态。</summary>
        void ApplyStateFromDirector(PlayerStateEnum state);

        ISkillExecutionHandle ActiveExecution { get; set; }

        float GetTimeScale();
        void TriggerActionPoint(ActionPointType actionPointType, Entity action);
        void PopTagsFrom(TagSource source);
        bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked);

        /// <summary>HP 归零后的统一死亡落地。</summary>
        void ApplyDeath();
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
