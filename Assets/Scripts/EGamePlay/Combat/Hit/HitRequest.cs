using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>技能子轴 / 执行器生命周期状态。</summary>
    public enum RunnerState : byte
    {
        Update = 0,
        Stop,
        StopEnd,
        Break,
        Finish,
    }

    /// <summary>命中裁决结果。格挡/霸体在 HitPipeline.FilterDefend 扩展时再增加枚举值。</summary>
    public enum HitResultKind : byte
    {
        Land = 0,
        Ignored = 1,
    }

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

    /// <summary>
    /// 盒体申报的命中请求。只携带引用，不拷贝效果列表，避免物理回调分配。
    /// </summary>
    public struct HitRequest
    {
        public ICombatUnit Attacker;
        public ICombatUnit Defender;
        public IHitSubRunner Runner;
        public object TriggerEvent;
        /// <summary>攻击盒与受击体最近点；无碰撞采样时为 false。</summary>
        public bool HasHitWorldPosition;
        public Vector3 HitWorldPosition;
    }
}
