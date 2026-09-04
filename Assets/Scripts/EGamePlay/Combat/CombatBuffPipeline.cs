using System;

namespace EGamePlay.Combat
{
    /// <summary>Buff 添加请求在效果锁下的落地结果。</summary>
    public enum BuffAddRequestResult : byte
    {
        /// <summary>新建并已激活。</summary>
        Applied = 0,
        /// <summary>已有 Buff，按重复规则刷新/叠层。</summary>
        Reapplied = 1,
        /// <summary>处于效果锁中，已入队，待解锁后落地。</summary>
        Queued = 2,
    }

    /// <summary>卸 Buff 原因。OnRemoved 开火时可读；调用方按语义填写。</summary>
    public enum BuffRemoveReason : byte
    {
        /// <summary>到期、次数耗尽、RemoveTrigger 到点。</summary>
        Expired = 0,
        /// <summary>净化 / 按大类驱散。</summary>
        Dispelled = 1,
        /// <summary>护盾破了、引爆吃层。护盾段吃到 0 由 Vital 调用；引爆见第 3 条。</summary>
        Consumed = 2,
        /// <summary>互斥覆盖。调用方见硬控互斥，本切片只占位。</summary>
        Replaced = 3,
        /// <summary>持有者死亡，<see cref="ICombatUnit.ApplyDeath"/> 清列表。</summary>
        Death = 4,
        /// <summary>轴结束、调试、被动 Sync 等显式卸除。</summary>
        Manual = 5,
    }

    /// <summary>净化极性。看 Buff 表 <c>BuffTag</c> 是否含 <see cref="CombatTags.BuffDebuff"/> / <see cref="CombatTags.BuffGain"/>。</summary>
    public enum BuffDispelPolarity : byte
    {
        /// <summary>不按极性过滤。</summary>
        All = 0,
        /// <summary>只卸带 <see cref="CombatTags.BuffDebuff"/> 的。</summary>
        DebuffOnly = 1,
        /// <summary>只卸带 <see cref="CombatTags.BuffGain"/> 的。</summary>
        BuffOnly = 2,
    }

    /// <summary>
    /// 战斗流程通知：先按优先级 Dispatch Buff，再广播 ActionPoint 给表现/系统规则。
    /// </summary>
    public static class CombatBuffPipeline
    {
        /// <summary>对单个单位：Buff 分发 + ActionPoint。</summary>
        public static void Notify(ICombatUnit unit, ActionPointType point, Entity action)
        {
            if (unit == null || unit.IsDisposed || point == ActionPointType.None)
                return;
            unit.Status?.Dispatch(point, action);
            unit.TriggerActionPoint(point, action);
        }

        /// <summary>锁住攻受双方的 Buff 列表，本段流程内的新增/移除延后提交。</summary>
        public static BuffEffectScope Lock(ICombatUnit a, ICombatUnit b = null)
        {
            return new BuffEffectScope(a, b);
        }
    }

    /// <summary>效果锁作用域。嵌套安全，最外层 Dispose 时才 Flush 延后队列。</summary>
    public readonly struct BuffEffectScope : IDisposable
    {
        readonly ICombatUnit _a;
        readonly ICombatUnit _b;
        readonly bool _lockB;

        /// <summary>对 a、b 加效果锁；同一单位只锁一次。</summary>
        public BuffEffectScope(ICombatUnit a, ICombatUnit b)
        {
            _a = a;
            _b = b;
            _a?.Status?.BeginEffectLock();
            _lockB = b != null && (a == null || b.Id != a.Id);
            if (_lockB)
                _b.Status?.BeginEffectLock();
        }

        /// <summary>成对解锁；最外层会提交延后的加/删 Buff。</summary>
        public void Dispose()
        {
            if (_lockB)
                _b?.Status?.EndEffectLock();
            _a?.Status?.EndEffectLock();
        }
    }
}
