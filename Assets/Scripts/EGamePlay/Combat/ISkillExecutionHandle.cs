namespace EGamePlay.Combat
{
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
}
