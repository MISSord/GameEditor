namespace EGamePlay.Combat
{
    /// <summary>
    /// 命中子轴：Combat HitPipeline 只依赖此接口，具体 XC 子轴由 ACT 实现。
    /// </summary>
    public interface IHitSubRunner
    {
        bool IsDisposed { get; }
        RunnerState State { get; }

        /// <summary>过滤阶段：只读检查，不写入去重表。</summary>
        HitResultKind PeekHit(ICombatUnit defender, object triggerEvent);

        /// <summary>落地阶段：写入去重表，返回是否首次命中。</summary>
        bool CommitHit(ICombatUnit defender, object triggerEvent);

        /// <summary>对已通过过滤的命中执行效果列表。</summary>
        void ApplyAcceptedHit(in HitRequest request);
        void PostAcceptedHit();
    }
}
