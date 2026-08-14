using System.Collections.Generic;
using ACTGameEditor;
using XiaoCao;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 将“被动技能”以常驻 Buff 的形式挂载到实体身上，并负责差量同步（挂载/卸载）。
    /// 只管理自己挂载的 Buff，不影响其它系统添加的 Buff。
    /// </summary>
    public sealed class PassiveSkillBuffComponent : Component
    {
        private readonly HashSet<int> _appliedPassiveBuffIds = new HashSet<int>();
        private readonly HashSet<int> _desiredPassiveBuffIds = new HashSet<int>();

        /// <summary>
        /// 根据被动技能 ID 列表同步常驻 Buff。
        /// </summary>
        /// <param name="passiveSkillIds">被动技能ID集合（可重复/可包含非法ID，内部会过滤）。</param>
        public void SyncFromPassiveSkillIds(IReadOnlyCollection<int> passiveSkillIds)
        {
            var status = Entity?.GetComponent<StatusComponent>();
            if (status == null) return;

            _desiredPassiveBuffIds.Clear();
            if (passiveSkillIds != null)
            {
                foreach (var passiveSkillId in passiveSkillIds)
                {
                    if (passiveSkillId <= 0) continue;
                    int buffId = PassiveSkillBuffMapCollection.GetBuffId(passiveSkillId);
                    if (buffId > 0) _desiredPassiveBuffIds.Add(buffId);
                }
            }

            // 移除不再需要的
            if (_appliedPassiveBuffIds.Count > 0)
            {
                var toRemove = PoolManager.Instance.TryGet<List<int>>();
                toRemove.Clear();
                foreach (var buffId in _appliedPassiveBuffIds)
                {
                    if (!_desiredPassiveBuffIds.Contains(buffId))
                        toRemove.Add(buffId);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int buffId = toRemove[i];
                    status.RemoveStatus(buffId);
                    _appliedPassiveBuffIds.Remove(buffId);
                }
                PoolManager.Instance.Return(toRemove);
            }

            // 挂载新增的
            foreach (var buffId in _desiredPassiveBuffIds)
            {
                if (_appliedPassiveBuffIds.Contains(buffId)) continue;
                if (!status.HasBuffId(buffId))
                    status.AttachStatus(buffId);
                _appliedPassiveBuffIds.Add(buffId);
            }
        }
    }
}

