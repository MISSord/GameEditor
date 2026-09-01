using System.Collections.Generic;
using EGamePlay;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 命中队列：物理回调只入队，由 CombatContext 在 Simulate 之后按阶段 Flush。
    /// 热路径使用预分配 List，Flush 后 Clear 保留容量。
    /// </summary>
    public sealed class HitPipeline
    {
        private List<HitRequest> _queue = new List<HitRequest>(32);
        private List<HitRequest> _processing = new List<HitRequest>(32);
        private List<HitRequest> _landed = new List<HitRequest>(32);

        /// <summary>物理回调入队。字段非法时直接丢弃，不抛异常。</summary>
        public void Enqueue(in HitRequest request)
        {
            if (request.Attacker == null || request.Attacker.IsDisposed)
                return;
            if (request.Defender == null || request.Defender.IsDisposed)
                return;
            if (request.Attacker.Id == request.Defender.Id)
                return;
            if (request.Runner == null || request.Runner.IsDisposed)
                return;
            if (request.TriggerEvent == null)
                return;

            _queue.Add(request);
        }

        /// <summary>
        /// 本帧命中分三阶段：过滤 → 落地效果 → 后处理。
        /// 双缓冲：结算期间新入队的请求留到下一次 Flush。
        /// </summary>
        public void Flush()
        {
            int count = _queue.Count;
            if (count == 0)
                return;

            List<HitRequest> swap = _queue;
            _queue = _processing;
            _processing = swap;

            _landed.Clear();
            for (int i = 0; i < count; i++)
            {
                HitRequest req = _processing[i];
                HitResultKind result = Filter(req);
                if (result == HitResultKind.Land)
                    _landed.Add(req);
            }
            _processing.Clear();

            for (int i = 0; i < _landed.Count; i++)
            {
                HitRequest request = _landed[i];
                if (!CanApply(request))
                    continue;
                if (!request.Runner.CommitHit(request.Defender, request.TriggerEvent))
                    continue;
                request.Runner.ApplyAcceptedHit(request);
            }

            for (int i = 0; i < _landed.Count; i++)
            {
                HitRequest request = _landed[i];
                if (request.Runner == null || request.Runner.IsDisposed)
                    continue;
                request.Runner.PostAcceptedHit();
            }
            _landed.Clear();
        }

        /// <summary>清空队列，供战局销毁时调用。</summary>
        public void Clear()
        {
            _queue.Clear();
            _processing.Clear();
            _landed.Clear();
        }

        /// <summary>
        /// 防守/进攻过滤。死亡与销毁直接忽略；格挡、霸体在 FilterDefend 扩展。
        /// </summary>
        private static HitResultKind Filter(in HitRequest request)
        {
            if (FilterAttack(request.Attacker) != HitResultKind.Land)
                return HitResultKind.Ignored;
            if (FilterDefend(request.Defender) != HitResultKind.Land)
                return HitResultKind.Ignored;
            if (request.Runner == null || request.Runner.IsDisposed)
                return HitResultKind.Ignored;
            if (request.TriggerEvent == null)
                return HitResultKind.Ignored;

            return request.Runner.PeekHit(request.Defender, request.TriggerEvent);
        }

        /// <summary>进攻侧：已销毁或已死亡的攻击者不再造成后续命中。</summary>
        private static HitResultKind FilterAttack(ICombatUnit attacker)
        {
            if (attacker == null || attacker.IsDisposed || attacker.IsDead)
                return HitResultKind.Ignored;
            return HitResultKind.Land;
        }

        /// <summary>防守侧：已销毁或已死亡忽略。格挡/霸体在此按 HitResultKind 扩展。</summary>
        private static HitResultKind FilterDefend(ICombatUnit defender)
        {
            if (defender == null || defender.IsDisposed)
                return HitResultKind.Ignored;
            if (defender.IsDead)
                return HitResultKind.Ignored;
            return HitResultKind.Land;
        }

        /// <summary>落地前再检一次：前序命中可能已打死某一方或打断子轴。</summary>
        private static bool CanApply(in HitRequest request)
        {
            if (request.Runner == null || request.Runner.IsDisposed)
                return false;
            if (request.Runner.State != RunnerState.Update)
                return false;
            if (FilterAttack(request.Attacker) != HitResultKind.Land)
                return false;
            if (FilterDefend(request.Defender) != HitResultKind.Land)
                return false;
            return true;
        }
    }
}
