using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 状态组件：持有角色 Buff 列表，并按 Priority 分发行动点上的 TriggerBuff。
    /// </summary>
    public class StatusComponent : Component
    {
        struct PendingAddStatus
        {
            public int BuffId;
            public ICombatUnit Caster;
            public List<string> ParamString1;
        }

        public override bool IsNeedUpdate { get; protected set; } = true;
        public List<Buff> Statuses = new List<Buff>();
        public Dictionary<int, List<Buff>> TypeIdStatuses = new Dictionary<int, List<Buff>>();
        public Dictionary<int, Buff> IdStatuses = new Dictionary<int, Buff>();
        public GameplayTagContainer TagContainer = new GameplayTagContainer();

        readonly List<PendingAddStatus> _pendingAdds = new List<PendingAddStatus>(4);
        readonly List<PendingAddStatus> _flushAdds = new List<PendingAddStatus>(4);
        readonly List<int> _pendingRemoves = new List<int>(4);
        readonly List<int> _removeScratch = new List<int>(8);
        readonly List<int> _removeFlush = new List<int>(8);
        int _effectLock;
        bool _teardown;
        int _dispatchDepth;

        /// <summary>伤害/施法流程持有效果锁时为 true，此时增删不立刻改遍历列表。</summary>
        public bool IsEffectLocked => _effectLock > 0;

        /// <summary>进入效果锁。可嵌套。</summary>
        public void BeginEffectLock()
        {
            _effectLock++;
        }

        /// <summary>退出效果锁。归零时提交延后的移除与添加。</summary>
        public void EndEffectLock()
        {
            if (_effectLock <= 0)
                return;
            _effectLock--;
            if (_effectLock == 0)
                FlushPending();
        }

        /// <summary>
        /// 按 Priority 从高到低遍历已激活 Buff，命中该行动点的 TriggerBuff / RemoveTriggerBuff 才执行。
        /// 热路径无 LINQ、无排序、无监听器注册。
        /// </summary>
        public void Dispatch(ActionPointType point, Entity action)
        {
            if (point == ActionPointType.None || Statuses == null || Statuses.Count == 0)
                return;

            int n = Statuses.Count;
            _dispatchDepth++;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    Buff buff = Statuses[i];
                    if (buff == null || !buff.Enable || buff.IsRemoving || buff.IsDisposed || buff.Setting == null)
                        continue;

                    BuffDemoSetting setting = buff.Setting;
                    BuffType type = setting.BuffType;
                    if (type.HasFlag(BuffType.TriggerBuff) && (setting.ActionPointType & point) != 0)
                        buff.OnEvent(action);

                    if (type.HasFlag(BuffType.RemoveTriggerBuff) && (setting.RemoveActionPointType & point) != 0)
                    {
                        if (buff.TryGet(out BuffTriggerComponent trigger))
                            trigger.ShouldRemove = true;
                        buff.CheckIsCanRemove();
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0 && _effectLock == 0 && _pendingRemoves.Count > 0)
                    FlushRemoves();
            }
        }

        /// <summary>
        /// 统一落地入口：已有则按重复规则处理；效果锁中新建入队。
        /// 不跑 Pre / 免疫。战斗施加须先走 <see cref="AddStatusAction"/>，入队的是已裁决 BuffId。
        /// </summary>
        public BuffAddRequestResult RequestAddStatus(int buffId, ICombatUnit caster, List<string> paramString1)
        {
            if (buffId <= 0)
                return BuffAddRequestResult.Applied;

            if (HasBuffId(buffId))
            {
                Buff existing = GetBuffById(buffId);
                if (existing != null && existing.IsRemoving)
                    return BuffAddRequestResult.Reapplied;
                if (existing != null && !existing.Enable)
                    existing.ActivateBuff();
                else
                    ReApplyExistingBuff(existing, SkillSettingMgr.Instance.GetBuffDemoSetting(buffId));
                return BuffAddRequestResult.Reapplied;
            }

            if (_effectLock > 0)
            {
                EnqueueAdd(buffId, caster, paramString1);
                return BuffAddRequestResult.Queued;
            }

            AttachNew(buffId, caster, paramString1);
            return BuffAddRequestResult.Applied;
        }

        public override void OnDestroy()
        {
            _teardown = true;
            _effectLock = 0;
            _dispatchDepth = 0;
            _pendingAdds.Clear();
            _flushAdds.Clear();
            _pendingRemoves.Clear();
            _removeScratch.Clear();
            _removeFlush.Clear();
            if (Statuses.Count > 0)
            {
                for (int i = 0; i < Statuses.Count; i++)
                {
                    Buff buff = Statuses[i];
                    if (buff == null || buff.IsDisposed)
                        continue;
                    if (buff.Enable)
                        buff.DeactivateBuff();
                    Entity.Destroy(buff);
                }
            }
            Statuses.Clear();
            TypeIdStatuses.Clear();
            IdStatuses.Clear();
            TagContainer.Reset();
        }

        public override void OnReset()
        {
            _teardown = false;
            _effectLock = 0;
            _dispatchDepth = 0;
            _pendingAdds.Clear();
            _flushAdds.Clear();
            _pendingRemoves.Clear();
            _removeScratch.Clear();
            _removeFlush.Clear();
            Statuses.Clear();
            TypeIdStatuses.Clear();
            IdStatuses.Clear();
            TagContainer.Reset();
        }

        /// <summary>创建并插入 Buff 列表（按 Priority 降序）。效果锁中会入队并返回 null。</summary>
        public Buff AttachStatus(int buffId)
        {
            if (_effectLock > 0)
            {
                EnqueueAdd(buffId, Entity as ICombatUnit, null);
                return null;
            }
            return AttachStatusImmediate(buffId);
        }

        public Buff AttachStatus(BuffDemoSetting config)
        {
            return AttachStatus(config.BuffId);
        }

        /// <summary>卸除指定 Buff。无原因时视为 <see cref="BuffRemoveReason.Manual"/>。</summary>
        public void RemoveStatus(int buffId)
        {
            RemoveStatus(buffId, BuffRemoveReason.Manual);
        }

        /// <summary>
        /// 统一卸入口：卸前只让正在卸的那条 TriggerBuff 打 <see cref="ActionPointType.PreRemoveStatus"/>，再 Revert。
        /// 效果锁中先开火再反激活，解锁后才从列表拿掉。
        /// </summary>
        public void RemoveStatus(int buffId, BuffRemoveReason reason)
        {
            if (_teardown || !IdStatuses.TryGetValue(buffId, out Buff buff) || buff == null || buff.IsRemoving)
                return;

            buff.MarkRemoving(reason);
            if (buff.Enable)
                FireOnRemoved(buff, reason);
            if (buff.Enable)
                buff.DeactivateBuff();

            Entity.Publish(new RemoveStatusEvent
            {
                Entity = this.Entity,
                buff = buff,
                BuffId = buff.Id,
                Reason = reason,
            });

            if (_effectLock > 0 || _dispatchDepth > 0)
            {
                EnqueueRemove(buffId);
                return;
            }

            DetachBuff(buff);
        }

        /// <summary>按当前列表顺序（Priority 高→低）卸全部，带原因。死亡走 <see cref="BuffRemoveReason.Death"/>。</summary>
        public void RemoveAll(BuffRemoveReason reason)
        {
            if (Statuses == null || Statuses.Count == 0)
                return;

            _removeScratch.Clear();
            for (int i = 0; i < Statuses.Count; i++)
            {
                Buff buff = Statuses[i];
                if (buff != null && !buff.IsRemoving)
                    _removeScratch.Add(buff.BuffID);
            }

            for (int i = 0; i < _removeScratch.Count; i++)
                RemoveStatus(_removeScratch[i], reason);
            _removeScratch.Clear();
        }

        public bool HasBuffId(int BuffId)
        {
            return IdStatuses.ContainsKey(BuffId);
        }

        public Buff GetBuffById(int BuffId)
        {
            IdStatuses.TryGetValue(BuffId, out var buff);
            return buff;
        }

        public bool TryGetBuffById(int buffId, out Buff buff)
        {
            return IdStatuses.TryGetValue(buffId, out buff) && buff != null;
        }

        public bool HasBigBuffType(int BigBuffType)
        {
            if (TypeIdStatuses.TryGetValue(BigBuffType, out List<Buff> list))
                return list.Count > 0;
            return false;
        }

        /// <summary>按大类驱散，默认 <see cref="BuffRemoveReason.Dispelled"/>、不按极性过滤。</summary>
        public void RemoveBuffByBigType(int BigBuffType)
        {
            RemoveBuffByBigType(BigBuffType, BuffRemoveReason.Dispelled, BuffDispelPolarity.All);
        }

        /// <summary>按大类驱散。极性看表 <c>BuffTag</c> 是否含 Debuff/增益 Tag，不是运行时容器。</summary>
        public void RemoveBuffByBigType(int BigBuffType, BuffRemoveReason reason, BuffDispelPolarity polarity)
        {
            RemoveStatuses(reason, polarity, BigBuffType);
        }

        /// <summary>
        /// 按原因卸一批。<paramref name="bigBuffType"/> &gt; 0 时只扫该大类；0 表示全表。
        /// </summary>
        public void RemoveStatuses(BuffRemoveReason reason, BuffDispelPolarity polarity, int bigBuffType = 0)
        {
            if (Statuses == null || Statuses.Count == 0)
                return;

            _removeScratch.Clear();
            if (bigBuffType > 0)
            {
                if (!TypeIdStatuses.TryGetValue(bigBuffType, out List<Buff> list) || list == null)
                    return;
                for (int i = 0; i < list.Count; i++)
                {
                    Buff buff = list[i];
                    if (buff == null || buff.IsRemoving)
                        continue;
                    if (!MatchesPolarity(buff, polarity))
                        continue;
                    _removeScratch.Add(buff.BuffID);
                }
            }
            else
            {
                for (int i = 0; i < Statuses.Count; i++)
                {
                    Buff buff = Statuses[i];
                    if (buff == null || buff.IsRemoving)
                        continue;
                    if (!MatchesPolarity(buff, polarity))
                        continue;
                    _removeScratch.Add(buff.BuffID);
                }
            }

            for (int i = 0; i < _removeScratch.Count; i++)
                RemoveStatus(_removeScratch[i], reason);
            _removeScratch.Clear();
        }

        /// <summary>技能轴结束（含 Break）后卸所有绑在该 Runner 上的 Buff。</summary>
        public void RemoveBoundToRunner(long runnerId)
        {
            if (runnerId == 0 || Statuses == null || Statuses.Count == 0)
                return;

            _removeScratch.Clear();
            for (int i = 0; i < Statuses.Count; i++)
            {
                Buff buff = Statuses[i];
                if (buff == null || buff.IsRemoving)
                    continue;
                if ((buff.ExpirePolicy & BuffExpirePolicy.SkillRunner) == 0)
                    continue;
                if (buff.BoundRunnerId != runnerId)
                    continue;
                _removeScratch.Add(buff.BuffID);
            }
            CommitScratchRemoves(BuffRemoveReason.Expired);
        }

        /// <summary>离开形态时卸绑在旧 FormId 上的 Buff。</summary>
        public void RemoveBoundToForm(int formId)
        {
            if (Statuses == null || Statuses.Count == 0)
                return;

            _removeScratch.Clear();
            for (int i = 0; i < Statuses.Count; i++)
            {
                Buff buff = Statuses[i];
                if (buff == null || buff.IsRemoving)
                    continue;
                if ((buff.ExpirePolicy & BuffExpirePolicy.Form) == 0)
                    continue;
                if (buff.BoundFormId != formId)
                    continue;
                _removeScratch.Add(buff.BuffID);
            }
            CommitScratchRemoves(BuffRemoveReason.Expired);
        }

        /// <summary>持有者被实际扣血一次。闪避/免疫不要调。</summary>
        public void NotifyHitTaken()
        {
            ConsumeHitPolicies(BuffExpirePolicy.HitsTaken);
        }

        /// <summary>持有者打出实际扣血一次。闪避/免疫不要调。</summary>
        public void NotifyHitDealt()
        {
            ConsumeHitPolicies(BuffExpirePolicy.HitsDealt);
        }

        void ConsumeHitPolicies(BuffExpirePolicy flag)
        {
            if (Statuses == null || Statuses.Count == 0)
                return;

            int n = Statuses.Count;
            for (int i = 0; i < n; i++)
            {
                Buff buff = Statuses[i];
                if (buff == null || !buff.Enable || buff.IsRemoving)
                    continue;
                if ((buff.ExpirePolicy & flag) == 0)
                    continue;
                if (buff.ConsumeHitAndShouldRemove())
                    RemoveStatus(buff.BuffID, BuffRemoveReason.Expired);
            }
        }

        void CommitScratchRemoves(BuffRemoveReason reason)
        {
            _removeFlush.Clear();
            for (int i = 0; i < _removeScratch.Count; i++)
                _removeFlush.Add(_removeScratch[i]);
            _removeScratch.Clear();
            for (int i = 0; i < _removeFlush.Count; i++)
                RemoveStatus(_removeFlush[i], reason);
            _removeFlush.Clear();
        }

        public override void Update(float deltaTime)
        {
            for (int i = Statuses.Count - 1; i >= 0; i--)
            {
                Buff buff = Statuses[i];
                if (buff != null && buff.IsCanRemoveBuff && !buff.IsRemoving)
                    RemoveStatus(buff.BuffID, BuffRemoveReason.Expired);
            }
        }

        public void OnStatusesChanged(Buff buff, bool isAdd)
        {
        }

        void EnqueueAdd(int buffId, ICombatUnit caster, List<string> paramString1)
        {
            for (int i = 0; i < _pendingAdds.Count; i++)
            {
                if (_pendingAdds[i].BuffId == buffId)
                    return;
            }
            _pendingAdds.Add(new PendingAddStatus
            {
                BuffId = buffId,
                Caster = caster,
                ParamString1 = paramString1,
            });
        }

        void EnqueueRemove(int buffId)
        {
            for (int i = 0; i < _pendingRemoves.Count; i++)
            {
                if (_pendingRemoves[i] == buffId)
                    return;
            }
            _pendingRemoves.Add(buffId);
        }

        void FlushRemoves()
        {
            int removeCount = _pendingRemoves.Count;
            for (int i = 0; i < removeCount; i++)
            {
                int buffId = _pendingRemoves[i];
                if (IdStatuses.TryGetValue(buffId, out Buff buff) && buff != null)
                    DetachBuff(buff);
            }
            _pendingRemoves.Clear();
        }

        void FlushPending()
        {
            FlushRemoves();

            int addCount = _pendingAdds.Count;
            if (addCount == 0)
                return;

            _flushAdds.Clear();
            for (int i = 0; i < addCount; i++)
                _flushAdds.Add(_pendingAdds[i]);
            _pendingAdds.Clear();

            // 解锁后只落地，不再跑 Pre：战斗源在 AddStatusAction 里已经裁决过；免疫单根本不会入队。
            ICombatUnit owner = Entity as ICombatUnit;
            for (int i = 0; i < _flushAdds.Count; i++)
            {
                PendingAddStatus pending = _flushAdds[i];
                BuffAddRequestResult result = RequestAddStatus(pending.BuffId, pending.Caster, pending.ParamString1);
                if (result == BuffAddRequestResult.Queued)
                    continue;
                CombatBuffPipeline.Notify(pending.Caster, ActionPointType.PostGiveStatus, Entity);
                CombatBuffPipeline.Notify(owner, ActionPointType.PostReceiveStatus, Entity);
            }
            _flushAdds.Clear();
        }

        Buff AttachStatusImmediate(int buffId)
        {
            Buff buff = Entity.AddChild<Buff>(buffId);
            InsertByPriority(buff);
            IdStatuses.Add(buffId, buff);
            BuffDemoSetting setting = SkillSettingMgr.Instance.GetBuffDemoSetting(buffId);
            if (setting != null)
            {
                if (!TypeIdStatuses.TryGetValue(setting.BigBuffType, out List<Buff> typeList))
                {
                    typeList = new List<Buff>();
                    TypeIdStatuses.Add(setting.BigBuffType, typeList);
                }
                typeList.Add(buff);
            }
            return buff;
        }

        void AttachNew(int buffId, ICombatUnit caster, List<string> paramString1)
        {
            Buff buff = AttachStatusImmediate(buffId);
            buff.Caster = caster?.Entity;
            ApplyKvParams(buff, paramString1);
            buff.ActivateBuff();
        }

        void InsertByPriority(Buff buff)
        {
            int prio = buff.Setting != null ? buff.Setting.Priority : 0;
            int idx = Statuses.Count;
            for (int i = 0; i < Statuses.Count; i++)
            {
                int other = Statuses[i].Setting != null ? Statuses[i].Setting.Priority : 0;
                if (prio > other)
                {
                    idx = i;
                    break;
                }
            }
            Statuses.Insert(idx, buff);
        }

        void FireOnRemoved(Buff buff, BuffRemoveReason reason)
        {
            BuffDemoSetting setting = buff.Setting;
            if (setting == null)
                return;
            if (!setting.BuffType.HasFlag(BuffType.TriggerBuff))
                return;
            if ((setting.ActionPointType & ActionPointType.PreRemoveStatus) == 0)
                return;

            RemoveStatusAction action = Entity.AddChild<RemoveStatusAction>();
            action.Creator = buff.Caster as ICombatUnit ?? Entity as ICombatUnit;
            action.Target = Entity as ICombatUnit;
            action.RemovedBuff = buff;
            action.BuffId = buff.BuffID;
            action.Reason = reason;
            try
            {
                buff.OnEvent(action);
            }
            finally
            {
                if (action != null && !action.IsDisposed)
                    action.FinishAction();
            }
        }

        void DetachBuff(Buff buff)
        {
            if (buff == null)
                return;
            int buffId = buff.BuffID;
            if (buff.Enable)
                buff.DeactivateBuff();
            Statuses.Remove(buff);
            IdStatuses.Remove(buffId);
            if (buff.Setting != null && TypeIdStatuses.TryGetValue(buff.Setting.BigBuffType, out List<Buff> typeList))
                typeList.Remove(buff);
            Entity.Destroy(buff);
        }

        static bool MatchesPolarity(Buff buff, BuffDispelPolarity polarity)
        {
            if (polarity == BuffDispelPolarity.All)
                return true;
            if (polarity == BuffDispelPolarity.DebuffOnly)
                return HasBuffTag(buff, CombatTags.BuffDebuff);
            if (polarity == BuffDispelPolarity.BuffOnly)
                return HasBuffTag(buff, CombatTags.BuffGain);
            return true;
        }

        static bool HasBuffTag(Buff buff, string tag)
        {
            if (buff?.Setting?.BuffTag == null || string.IsNullOrEmpty(tag))
                return false;
            List<string> tags = buff.Setting.BuffTag;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                    return true;
            }
            return false;
        }

        static void ReApplyExistingBuff(Buff existingBuff, BuffDemoSetting config)
        {
            if (existingBuff == null || config == null)
                return;

            BuffReApplyRule rule = config.ReApplyRule;
            existingBuff.TryGet(out BuffTimeComponent timeComponent);

            if (rule == BuffReApplyRule.AddDuration)
            {
                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ExtendDuration(config.BaseDuration);
            }
            else if (rule == BuffReApplyRule.RefreshDuration)
            {
                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ResetDuration(config.BaseDuration);
            }
            else if (rule == BuffReApplyRule.AddStackAndRefresh)
            {
                if (existingBuff.IsCanStack)
                {
                    BuffAttributesComponent attrs = existingBuff.GetComponent<BuffAttributesComponent>();
                    if (attrs != null)
                    {
                        BuffProperty stackProperty = attrs.GetNumeric(AttributeType.BuffMaxStacks);
                        if (stackProperty != null)
                            stackProperty.CurrentValue = stackProperty.CurrentValue + 1f;
                    }
                }

                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ResetDuration(config.BaseDuration);
            }
            else if (rule != BuffReApplyRule.Exclusive && timeComponent != null && config.BaseDuration > 0f)
            {
                timeComponent.ResetDuration(config.BaseDuration);
            }

            // 刷新时间 / 叠层：盾回满。叠加时间只续时，不补已吃掉的盾。
            if (rule != BuffReApplyRule.AddDuration && rule != BuffReApplyRule.Exclusive)
                BuffModifyProcessorTable.RefreshStickyShields(existingBuff);
        }

        static void ApplyKvParams(Buff ability, List<string> paramPairs)
        {
            if (ability == null || paramPairs == null || paramPairs.Count == 0)
                return;

            for (int i = 0; i < paramPairs.Count; i++)
            {
                string s = paramPairs[i];
                if (string.IsNullOrEmpty(s))
                    continue;
                int eq = s.IndexOf('=');
                if (eq <= 0 || eq >= s.Length - 1)
                    continue;
                if (!int.TryParse(s.Substring(0, eq), out int keyInt))
                    continue;
                if (!int.TryParse(s.Substring(eq + 1), out int valInt))
                    continue;
                if (Enum.IsDefined(typeof(AttributeType), keyInt))
                    ability.AddBuffAttribute((AttributeType)keyInt, ModifyType.SetBase, null, valInt, true);
            }
        }
    }
}
