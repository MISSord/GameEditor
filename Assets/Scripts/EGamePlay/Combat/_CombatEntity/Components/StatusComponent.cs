using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 状态组件：角色身上的 Buff 列表、效果锁延后增删、按 Priority 分发 TriggerBuff。
    /// 战斗施加走 <see cref="AddStatusAction"/>；本组件不负责免疫，只负责落地与卸。
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
        /// <summary>已激活 Buff，按 Priority 降序。Dispatch 只扫这一份，不要另建监听表。</summary>
        public List<Buff> Statuses = new List<Buff>();
        /// <summary>大类 → 列表。驱散按 BigBuffType 走这里，避免全表扫。</summary>
        public Dictionary<int, List<Buff>> TypeIdStatuses = new Dictionary<int, List<Buff>>();
        /// <summary>同一 BuffId 默认一条。重复施加走 ReApply，不 new 第二条。</summary>
        public Dictionary<int, Buff> IdStatuses = new Dictionary<int, Buff>();
        public GameplayTagContainer TagContainer = new GameplayTagContainer();

        // 效果锁 / Dispatch 期间不能改 Statuses，增删先入队，解锁后再落地。
        readonly List<PendingAddStatus> _pendingAdds = new List<PendingAddStatus>(4);
        readonly List<PendingAddStatus> _flushAdds = new List<PendingAddStatus>(4);
        readonly List<int> _pendingRemoves = new List<int>(4);
        // 先收集 Id 再 RemoveStatus：遍历中卸会改 Statuses / TypeId 列表。
        readonly List<int> _removeScratch = new List<int>(8);
        readonly List<int> _removeFlush = new List<int>(8);
        readonly RemoveStatusEvent _removeStatusEvent = new RemoveStatusEvent();
        // 卸 Buff 回调上下文：常驻一条；OnEvent 重入再临时 AddChild。
        RemoveStatusAction _removeAction;
        bool _removeActionBusy;
        int _effectLock;
        /// <summary>OnDestroy 后为 true，挡住销毁过程中再次 RemoveStatus。</summary>
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

            // 固定本次长度：回调里新挂的 Buff 本轮不参与，避免套进同一刀。
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
                    if ((type & BuffType.TriggerBuff) != 0 && (setting.ActionPointType & point) != 0)
                        buff.OnEvent(action);

                    // 到期触发器只打标记，真正卸走统一 RemoveStatus，避免在循环里 Detach。
                    if ((type & BuffType.RemoveTriggerBuff) != 0 && (setting.RemoveActionPointType & point) != 0)
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
                // 最外层且没有效果锁才刷延后卸，嵌套 Dispatch 仍可能在扫同一份列表。
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
                // 正在卸的那条还在字典里；当成刷新成功，避免解锁后又挂回一条。
                if (existing != null && existing.IsRemoving)
                    return BuffAddRequestResult.Reapplied;
                if (existing != null && !existing.Enable)
                    existing.ActivateBuff();
                else
                    ReApplyExistingBuff(existing, SkillSettingMgr.Instance.GetBuffDemoSetting(buffId));
                return BuffAddRequestResult.Reapplied;
            }

            // 硬控互斥在入队前：被挡的 Id 根本不应进 pending。
            if (!HardControlMutex.Admit(this, buffId))
                return BuffAddRequestResult.Blocked;

            if (_effectLock > 0)
            {
                EnqueueAdd(buffId, caster, paramString1);
                return BuffAddRequestResult.Queued;
            }

            AttachNew(buffId, caster, paramString1);
            return BuffAddRequestResult.Applied;
        }

        /// <summary>
        /// 实体销毁。CombatEntity.Dispose 会先拆子 Buff；这里清列表和延后队列，漏网的再 Deactivate/Destroy。
        /// </summary>
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
            ClearRemoveStatusEvent();
            _removeAction = null;
            _removeActionBusy = false;
        }

        /// <summary>回池。只清本组件表和队列；Buff 子实体已在 OnDestroy 路径拆掉。</summary>
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
            ClearRemoveStatusEvent();
            _removeAction = null;
            _removeActionBusy = false;
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

        /// <summary>直挂入口（测试面板 / 被动）。绕过免疫与 PreGive，战斗施加请走 <see cref="AddStatusAction"/>。</summary>
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
            // 先让本条 TriggerBuff 看见「即将卸」，再 Revert。Dispatch 全表会扫到已 Mark 的条目。
            if (buff.Enable)
                FireOnRemoved(buff, reason);
            if (buff.Enable)
                buff.DeactivateBuff();

            Entity.Publish(FillRemoveStatusEvent(buff, reason));

            // 锁或 Dispatch 中只反激活，列表等循环结束再摘，避免边扫边 Remove。
            if (_effectLock > 0 || _dispatchDepth > 0)
            {
                EnqueueRemove(buffId);
                return;
            }

            DetachBuff(buff);
        }

        /// <summary>复用事件实例，避免每次卸 Buff new。调用方不得缓存返回值。</summary>
        RemoveStatusEvent FillRemoveStatusEvent(Buff buff, BuffRemoveReason reason)
        {
            _removeStatusEvent.Entity = Entity;
            _removeStatusEvent.buff = buff;
            _removeStatusEvent.BuffId = buff.Id;
            _removeStatusEvent.Reason = reason;
            return _removeStatusEvent;
        }

        void ClearRemoveStatusEvent()
        {
            _removeStatusEvent.Entity = null;
            _removeStatusEvent.buff = null;
            _removeStatusEvent.BuffId = 0;
            _removeStatusEvent.Reason = default;
        }

        /// <summary>按当前列表顺序（Priority 高→低）卸全部，带原因。死亡走 <see cref="BuffRemoveReason.Death"/>。</summary>
        public void RemoveAll(BuffRemoveReason reason)
        {
            if (Statuses == null || Statuses.Count == 0)
                return;

            // 先抄 Id：RemoveStatus 会改 Statuses，不能边扫边卸。
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

        /// <summary>是否已有该 BuffId（含正在卸、尚未从字典摘掉的）。</summary>
        public bool HasBuffId(int BuffId)
        {
            return IdStatuses.ContainsKey(BuffId);
        }

        /// <summary>按 BuffId 取实例。没有则 null。</summary>
        public Buff GetBuffById(int BuffId)
        {
            IdStatuses.TryGetValue(BuffId, out var buff);
            return buff;
        }

        /// <summary>按 BuffId 取实例。没有或已销毁返回 false。</summary>
        public bool TryGetBuffById(int buffId, out Buff buff)
        {
            return IdStatuses.TryGetValue(buffId, out buff) && buff != null;
        }

        /// <summary>身上是否还有该大类（<c>Buff.BigBuffType</c>）的条目。</summary>
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
                // 大类列表是 Statuses 的子集，同样不能边遍历边 Detach。
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

            // 用快照长度：本帧新挂的不扣次。到期走 RemoveStatus(Id)，不在这里手改列表。
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
            // 拷到 flush：RemoveStatus 回调可能再往 _removeScratch 塞 Id。
            _removeFlush.Clear();
            for (int i = 0; i < _removeScratch.Count; i++)
                _removeFlush.Add(_removeScratch[i]);
            _removeScratch.Clear();
            for (int i = 0; i < _removeFlush.Count; i++)
                RemoveStatus(_removeFlush[i], reason);
            _removeFlush.Clear();
        }

        /// <summary>到期标记在 Buff 自己的 Tick 里写；这里只收口卸，不在本组件里减时间。</summary>
        public override void Update(float deltaTime)
        {
            // 倒序：无锁时 RemoveStatus 立刻 Detach，从尾部摘不影响尚未扫到的下标。
            for (int i = Statuses.Count - 1; i >= 0; i--)
            {
                Buff buff = Statuses[i];
                if (buff != null && buff.IsCanRemoveBuff && !buff.IsRemoving)
                    RemoveStatus(buff.BuffID, BuffRemoveReason.Expired);
            }
        }

        /// <summary>历史钩子，无调用方。表现不要写这里，走 CombatPresentationDirector。</summary>
        public void OnStatusesChanged(Buff buff, bool isAdd)
        {
        }

        void EnqueueAdd(int buffId, ICombatUnit caster, List<string> paramString1)
        {
            // 同一锁窗口同 Id 只留一条，解锁后走 ReApply 而不是挂两条。
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
            // 同 Id 只记一次：锁窗口内多次 RemoveStatus 仍是卸这一条。
            for (int i = 0; i < _pendingRemoves.Count; i++)
            {
                if (_pendingRemoves[i] == buffId)
                    return;
            }
            _pendingRemoves.Add(buffId);
        }

        void FlushRemoves()
        {
            // Fire / Deactivate 已在 RemoveStatus 当时做过，这里只从列表摘掉并 Destroy。
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
            // 先卸后加：同 Id 先摘掉正在卸的，再落地新的，避免 ReApply 打到 IsRemoving 条目。
            FlushRemoves();

            int addCount = _pendingAdds.Count;
            if (addCount == 0)
                return;

            // 拷出来再清 pending：落地过程中可能再次 EnqueueAdd，不能边遍历边改同一份。
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
                // Queued 说明又进了新锁（落地过程中又开锁），留给下一轮 Flush；Blocked 是硬控互斥。
                if (result == BuffAddRequestResult.Queued || result == BuffAddRequestResult.Blocked)
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
            // 三份索引一起写：Dispatch 用 Statuses，查 Id 用 IdStatuses，驱散用 TypeId。
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
            // 先入表再 Activate：Modify / Tag 回调里能 HasBuffId 查到自己。
            buff.ActivateBuff();
        }

        void InsertByPriority(Buff buff)
        {
            int prio = buff.Setting != null ? buff.Setting.Priority : 0;
            int idx = Statuses.Count;
            // 稳定插入：同等 Priority 后挂的排在后面，Dispatch 仍是高 → 低。
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
            if ((setting.BuffType & BuffType.TriggerBuff) == 0)
                return;
            if ((setting.ActionPointType & ActionPointType.PreRemoveStatus) == 0)
                return;

            // 只通知正在卸的这一条，不 Dispatch 全表。OnRemoved 爆炸/净化只看 Reason。
            bool rented = false;
            RemoveStatusAction action;
            if (!_removeActionBusy)
            {
                if (_removeAction == null || _removeAction.IsDisposed)
                    _removeAction = Entity.AddChild<RemoveStatusAction>();
                action = _removeAction;
                _removeActionBusy = true;
                rented = true;
            }
            else
            {
                // OnEvent 里又卸另一条：复用实例会被覆盖，临时 new 一条。
                action = Entity.AddChild<RemoveStatusAction>();
            }

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
                if (rented)
                {
                    // 不清 Destroy：常驻子实体下次继续用。回调里若自己 Finish 了，丢掉引用。
                    if (action != null && !action.IsDisposed)
                        action.OnReset();
                    else
                        _removeAction = null;
                    _removeActionBusy = false;
                }
                else if (action != null && !action.IsDisposed)
                {
                    action.FinishAction();
                }
            }
        }

        /// <summary>从列表和索引摘掉并 Destroy。调用前应已 Fire / Deactivate。</summary>
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
            // 极性看配表 BuffTag，不是运行时 TagHost。驱散「只解 Debuff」靠表行打了 Buff.Debuff。
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
                // 只续时，不刷新已走过的剩余；护盾也不回满（见方法末尾）。
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
                // 未配规则时默认刷新时长；Exclusive 表示同 Id 直接忽略，连时间也不动。
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

            // ParamString1："属性枚举int=值"。非法段跳过，不要 throw 打断整次施加。
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
