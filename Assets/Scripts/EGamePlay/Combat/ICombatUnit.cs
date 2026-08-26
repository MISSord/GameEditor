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
        bool CanSpellSkillWithTagLists(System.Collections.Generic.List<string> required, System.Collections.Generic.List<string> blocked);

        /// <summary>HP 归零后的统一死亡落地。</summary>
        void ApplyDeath();
    }
}
