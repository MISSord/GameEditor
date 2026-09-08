using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>战术层状态。Dead/Control/Hit/Skill 由战斗态强制同步，禁止 Brain 自行写入 CurState。</summary>
    public enum EnemyTacticalState : byte
    {
        Idle = 0,
        Alert = 1,
        Approach = 2,
        Orbit = 3,
        Windup = 4,
        Skill = 5,
        Recover = 6,
        Hit = 7,
        Control = 8,
        Dead = 9,
    }

    /// <summary>强制态判定（受击/硬控/占轴/死亡盖住战术循环）。</summary>
    public static class EnemyHfsm
    {
        /// <summary>这些态由 CombatStateDirector / 占轴驱动，不跑 Approach/Orbit 逻辑。</summary>
        public static bool IsForced(EnemyTacticalState state) =>
            state == EnemyTacticalState.Dead
            || state == EnemyTacticalState.Control
            || state == EnemyTacticalState.Hit
            || state == EnemyTacticalState.Skill;

        /// <summary>
        /// 按战斗态解析本帧强制战术态。优先级 Dead &gt; Control &gt; Hit &gt; Skill（占轴）。
        /// 未命中返回 false，Brain 才跑 Alert/Approach/Orbit。
        /// </summary>
        public static bool TryResolveForced(CombatEntity owner, bool occupyingAxis, out EnemyTacticalState state)
        {
            if (owner == null || owner.IsDisposed || owner.IsDead)
            {
                state = EnemyTacticalState.Dead;
                return true;
            }

            if (owner.StateDirector != null && owner.StateDirector.IsControl)
            {
                state = EnemyTacticalState.Control;
                return true;
            }

            if (owner.CurState == PlayerStateEnum.Hit)
            {
                state = EnemyTacticalState.Hit;
                return true;
            }

            if (occupyingAxis)
            {
                state = EnemyTacticalState.Skill;
                return true;
            }

            state = EnemyTacticalState.Idle;
            return false;
        }
    }
}
