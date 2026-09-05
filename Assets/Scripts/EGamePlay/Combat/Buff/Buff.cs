using System;

namespace EGamePlay.Combat
{
    /// <summary>
    /// Buff 重复添加时的处理规则。
    /// 对应配置表 BuffDemoSetting.RepeatedAddition 的取值：
    /// 1 = 叠加时间；2 = 刷新时间；3 = 叠加层数并刷新时间；4 = 互斥不叠加。
    /// </summary>
    public enum BuffReApplyRule
    {
        /// <summary>1. 叠加时间到当前 Buff 中。</summary>
        AddDuration = 1,
        /// <summary>2. 刷新时间（使用配置中的默认持续时间）。</summary>
        RefreshDuration = 2,
        /// <summary>3. 在已有 Buff 上叠加层数并刷新时间。</summary>
        AddStackAndRefresh = 3,
        /// <summary>4. 互斥：已有 Buff 时忽略新的添加。</summary>
        Exclusive = 4,
    }

    /// <summary>
    /// TimeBuff 周期跳（OnTick）成功触发后的掉层策略。
    /// 配在该 Buff 第一条 BuffHpDamage 的 ParamFloat2 上：0=不掉层，1=减 1，2=减半（向下取整），3=清层。
    /// OnStart 首次触发不掉层，避免挂上立刻掉一层。
    /// </summary>
    public enum TickStackPolicy : byte
    {
        /// <summary>0. 跳伤不改层数（风蚀类：层数只影响强度）。</summary>
        None = 0,
        /// <summary>1. 每次跳成功减 1 层（光噪类）。</summary>
        MinusOne = 1,
        /// <summary>2. 每次跳成功层数减半，向下取整（电磁类）。</summary>
        MinusHalf = 2,
        /// <summary>3. 一次跳成功清层并移除。</summary>
        Clear = 3,
    }

    /// <summary>
    /// Buff 结束条件。可组合：Duration 与其它同时成立时，谁先到谁卸。
    /// 配在 <c>BuffTag</c>：<see cref="CombatTags.BuffBindSkill"/> 等。
    /// </summary>
    [Flags]
    public enum BuffExpirePolicy : byte
    {
        /// <summary>无额外绑定（仅表上的 TimeBuff / 显式卸）。</summary>
        None = 0,
        /// <summary>TimeBuff 到期。由现有计时器触发，不必单独挂钩。</summary>
        Duration = 1,
        /// <summary>持有者被实际扣血 N 次。</summary>
        HitsTaken = 2,
        /// <summary>持有者打出实际扣血 N 次。</summary>
        HitsDealt = 4,
        /// <summary>直到施加时 Caster 的那次技能轴结束（含 Break）。</summary>
        SkillRunner = 8,
        /// <summary>直到持有者离开施加时的形态。</summary>
        Form = 16,
        /// <summary>切人退场。尚未挂钩。</summary>
        OnSwitchOut = 32,
    }

    /// <summary>
    /// BuffDemoSetting 的扩展，提供对 RepeatedAddition 的强类型封装。
    /// </summary>
    public sealed partial class BuffDemoSetting
    {
        /// <summary>Buff 重复添加时的规则（从 RepeatedAddition 转换而来）。</summary>
        public BuffReApplyRule ReApplyRule => (BuffReApplyRule)RepeatedAddition;
    }

    /// <summary>
    /// Buff 实体，继承 AbilityBase 与 Ability 共享统一能力生命周期；通过 OnActivate/OnDeactivate 管理标签与组件。
    /// </summary>
    public class Buff : AbilityBase
    {
        public Entity Caster; // 施法者
        // 承受者由基类 OwnerEntity 提供

        // Buff 静态数据
        public int BuffID { get; private set; }
        public BuffDemoSetting Setting { get; private set; } //配置表配置
        public bool IsCanRemoveBuff { get; private set; } = false;    // 是否能移除该Buff
        /// <summary>已进入统一卸入口，避免卸前开火重入。</summary>
        public bool IsRemoving { get; private set; }
        /// <summary>到期标记时记下的原因；显式 <see cref="StatusComponent.RemoveStatus"/> 会覆盖。</summary>
        public BuffRemoveReason PendingRemoveReason { get; private set; }
        public bool IsCanStack => Setting.CanStack == 1; //是否能叠层
        /// <summary>周期跳成功后的掉层策略；由 BuffHpDamage.ParamFloat2 解析，热路径只读字段。</summary>
        public TickStackPolicy TickStackPolicy { get; private set; }
        /// <summary>结束条件（表 Tag 解析）。刷新同 Id 不重绑 Runner/Form。</summary>
        public BuffExpirePolicy ExpirePolicy { get; private set; }
        /// <summary>SkillRunner 绑定的轴 Id；0 表示未绑。</summary>
        public long BoundRunnerId { get; private set; }
        /// <summary>Form 绑定的形态 Id（0 为默认槽位）。</summary>
        public int BoundFormId { get; private set; }
        /// <summary>HitsTaken / HitsDealt 剩余次数。</summary>
        public int HitsRemaining { get; private set; }

        bool _lifetimeBound;
        private BuffAttributesComponent _attributes;
        private BuffTimeComponent _time;
        private BuffFrequencyComponent _frequency;
        private BuffModifyComponent _modify;
        private StatusComponent _status;

        public override void Awake(object initData)
        {
            int BuffId = (int)initData;
            BuffID = BuffId;
            Setting = SkillSettingMgr.Instance.GetBuffDemoSetting(BuffID);
            if (Setting == null)
            {
                GameLog.CombatError($"BuffId 配置缺失，BuffID={BuffID}，请检查配置！");
                return;
            }

            //Buff属性组件
            _attributes = AddComponent<BuffAttributesComponent>();
            if (IsCanStack == true)
            {
                BuffProperty buff = _attributes.AddNumeric(AttributeType.BuffMaxStacks, Setting.MaxLevel);
                buff.CurrentValue = 1;
            }

            //条件判断组件
            if (string.IsNullOrEmpty(Setting.TriggerFormula) == false)
            {
                AddComponent<BuffStateCheckComponent>();
            }

            TickStackPolicy = ResolveTickStackPolicy(Setting);
            ExpirePolicy = ResolveExpirePolicy(Setting);

            // 时间处理组件：ETTimerManager 按世界钟调度，不乘实体 TimeScale
            if (Setting.BuffType.HasFlag(BuffType.TimeBuff) || Setting.BuffType.HasFlag(BuffType.CDBuff))
            {
                _time = AddComponent<BuffTimeComponent>();
                _time.Init(Setting.BaseDuration, Setting.IntervalTime, Setting.DelayTime, Setting.CDTime);
                _time.Events.OnTick = OnIntervalTick;
                _time.Events.OnStart = OnEvent;
            }

            //触发器组件
            if (Setting.BuffType.HasFlag(BuffType.TriggerBuff) || Setting.BuffType.HasFlag(BuffType.RemoveTriggerBuff))
            {
                AddComponent<BuffTriggerComponent>();
            }

            //次数组件
            if (Setting.BuffType.HasFlag(BuffType.NumberBuff))
            {
                var timesProperty = _attributes.AddNumeric(AttributeType.BuffMaxNumber, Setting.BaseTimes);
                timesProperty.CurrentValue = timesProperty.MaxValue.Value;
                _frequency = AddComponent<BuffFrequencyComponent>();
            }

            //修饰组件（PlayerControll / PlayerModify / ActionModify）
            if (Setting.BuffModifyList != null && Setting.BuffModifyList.Count > 0)
            {
                _modify = AddComponent<BuffModifyComponent>();
            }

            _status = Parent?.GetComponent<StatusComponent>();

            //// 被动技能融合：如果为该 Buff 配置了被动技能配置，则挂接被动触发与效果组件。
            //var passiveConfig = BuffPassiveConfigCollection.GetPassiveConfig(BuffID);
            //if (passiveConfig != null)
            //{
            //    AddComponent<AbilityEffectComponent>(passiveConfig);
            //}
        }

        //修改Buff现有属性
        public void AddBuffAttribute(AttributeType type, ModifyType modifyType, FloatModifier modifier, float baseValue = 0, bool isAdd = false)
        {
            FloatNumeric number = GetFloatNumeric(type, isAdd);
            if (number == null) return;
            if(modifyType == ModifyType.SetBase)
            {
                number.SetBase(baseValue);
            }
            else
            {
                number.AddModifier(modifyType, modifier);
            }
        }

        public void RemoveBuffAttribute(AttributeType type, ModifyType modifyType, FloatModifier modifier, float baseValue)
        {
            FloatNumeric number = GetFloatNumeric(type);
            if (number == null) return;
            number.RemoveModifier(modifyType, modifier);
        }

        public FloatNumeric GetFloatNumeric(AttributeType type, bool isAdd = false)
        {
            //与时间相关
            if (type == AttributeType.BuffIntervalTime || type == AttributeType.BuffCDTime || type == AttributeType.BuffMaxTime)
            {
                return _time != null ? _time.GetTimeAttribute(type) : null;
            }
            //与次数相关
            else if (type == AttributeType.BuffMaxNumber)
            {
                return _frequency != null ? _frequency.GetNumberNumeric() : null;
            }
            //与属性相关
            else
            {
                var attrs = _attributes;
                if (attrs == null)
                {
                    return null;
                }

                if (!attrs.TryGetNumeric(type, out var property) || property == null)
                {
                    if (isAdd == false)
                    {
                        return null;
                    }

                    property = attrs.AddNumeric(type, 0);
                }

                return property.MaxValue;
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (_status == null) _status = Parent?.GetComponent<StatusComponent>();
            BindLifetimeIfNeeded();
            var tags = Setting?.BuffTag;
            if (_status == null || tags == null) return;
            var src = TagSource.Buff(Id);
            for (int i = 0; i < tags.Count; i++)
                _status.TagContainer.Push(src, tags[i]);
        }

        protected override void OnDeactivate()
        {
            if (_status == null) _status = Parent?.GetComponent<StatusComponent>();
            var tags = Setting?.BuffTag;
            if (_status != null && tags != null)
            {
                var src = TagSource.Buff(Id);
                for (int i = 0; i < tags.Count; i++)
                    _status.TagContainer.Pop(src, tags[i]);
            }
            base.OnDeactivate();
        }

        /// <summary>激活 Buff（与 IAbility.Activate 统一，对外保留原名便于语义区分）。</summary>
        public void ActivateBuff() => Activate();

        /// <summary>反激活 Buff（与 IAbility.Deactivate 统一）。</summary>
        public void DeactivateBuff() => Deactivate();

        //触发方法 //由触发器触发
        public void OnEvent(Entity actionExecution)
        {
            // 这里是状态判断，状态判断是判断目标的状态是否满足条件，满足才能触发效果
            var stateCheckResult = true;
            if (TryGet(out BuffStateCheckComponent component))
            {
                stateCheckResult = component.CheckTargetState(actionExecution);
            }

            //如果当前Buff有CD设置，判断CD是否结束
            if (stateCheckResult && Setting.BuffType.HasFlag(BuffType.CDBuff))
            {
                if (_time == null) return;
                stateCheckResult = _time.GetIsCanTrigger();
                if (stateCheckResult == true)
                {
                    _time.RefreshCDTime();
                }
            }

            // 条件满足则触发效果
            if (stateCheckResult)
            {
                if (TryGet(out BuffModifyComponent modifyCom))
                    modifyCom.OnTriggerModify(ResolveModifyTarget(actionExecution));
                //次数耗尽
                if (Setting.BuffType.HasFlag(BuffType.NumberBuff) && _frequency != null && _frequency.ConsumeStack())
                {
                    CheckIsCanRemove();
                }
            }
        }

        /// <summary>
        /// 行动点回调传入的是 Action 实体，不能直接当战斗单位。
        /// 伤害/施加状态取 <see cref="IActionExecute.Target"/>；否则落到 Buff 拥有者。
        /// </summary>
        Entity ResolveModifyTarget(Entity actionExecution)
        {
            if (actionExecution is IActionExecute action && action.Target?.Entity != null)
                return action.Target.Entity;
            if (actionExecution is ICombatUnit unit && unit.Entity != null)
                return unit.Entity;
            return OwnerEntity?.Entity ?? actionExecution;
        }

        /// <summary>命中计数减一；到 0 时由 <see cref="StatusComponent"/> 卸除。</summary>
        public bool ConsumeHitAndShouldRemove()
        {
            if (HitsRemaining <= 0)
                return true;
            HitsRemaining--;
            return HitsRemaining <= 0;
        }

        void BindLifetimeIfNeeded()
        {
            if (_lifetimeBound)
                return;
            _lifetimeBound = true;

            if ((ExpirePolicy & (BuffExpirePolicy.HitsTaken | BuffExpirePolicy.HitsDealt)) != 0)
            {
                int times = Setting != null ? Setting.BaseTimes : 0;
                HitsRemaining = times > 0 ? times : 1;
            }

            if ((ExpirePolicy & BuffExpirePolicy.SkillRunner) != 0)
            {
                ICombatUnit caster = Caster as ICombatUnit ?? OwnerEntity;
                ISkillExecutionHandle runner = caster?.ActiveExecution;
                if (runner != null && !runner.IsDisposed)
                    BoundRunnerId = runner.Id;
            }

            if ((ExpirePolicy & BuffExpirePolicy.Form) != 0)
            {
                CombatFormComponent form = OwnerEntity?.Entity?.GetComponent<CombatFormComponent>();
                BoundFormId = form != null ? form.ActiveFormId : 0;
            }
        }

        static BuffExpirePolicy ResolveExpirePolicy(BuffDemoSetting setting)
        {
            BuffExpirePolicy policy = BuffExpirePolicy.None;
            if (setting == null)
                return policy;
            if (setting.BuffType.HasFlag(BuffType.TimeBuff))
                policy |= BuffExpirePolicy.Duration;
            if (HasSettingTag(setting, CombatTags.BuffBindSkill))
                policy |= BuffExpirePolicy.SkillRunner;
            if (HasSettingTag(setting, CombatTags.BuffBindForm))
                policy |= BuffExpirePolicy.Form;
            if (HasSettingTag(setting, CombatTags.BuffBindHitsTaken))
                policy |= BuffExpirePolicy.HitsTaken;
            if (HasSettingTag(setting, CombatTags.BuffBindHitsDealt))
                policy |= BuffExpirePolicy.HitsDealt;
            return policy;
        }

        static bool HasSettingTag(BuffDemoSetting setting, string tag)
        {
            if (setting?.BuffTag == null || string.IsNullOrEmpty(tag))
                return false;
            var tags = setting.BuffTag;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                    return true;
            }
            return false;
        }

        /// <summary>由 <see cref="StatusComponent"/> 在统一卸入口标记，防止重入。</summary>
        public void MarkRemoving(BuffRemoveReason reason)
        {
            IsRemoving = true;
            IsCanRemoveBuff = true;
            PendingRemoveReason = reason;
        }

        /// <summary>到期 / 次数耗尽 / 移除触发到点时走统一卸入口，不再提前 Deactivate。</summary>
        public void CheckIsCanRemove()
        {
            if (IsRemoving || IsDisposed)
                return;
            if (GetIsNoTimes() == true || GetIsTimeOut() == true || GetIsTriggerOut() == true || GetIsNoStacks() == true)
            {
                IsCanRemoveBuff = true;
                PendingRemoveReason = BuffRemoveReason.Expired;
                if (_status == null)
                    _status = Parent?.GetComponent<StatusComponent>();
                _status?.RemoveStatus(BuffID, BuffRemoveReason.Expired);
            }
        }

        /// <summary>
        /// 可叠层时返回当前层数（可为 0）；不可叠层视为 1，供粘性 PlayerModify 按层缩放。
        /// </summary>
        public float GetStackCount()
        {
            if (Setting == null || !IsCanStack || _attributes == null)
                return 1f;
            var property = _attributes.GetNumeric(AttributeType.BuffMaxStacks);
            return property != null ? property.CurrentValue : 1f;
        }

        //由时间触发（OnStart：挂上立刻跳一次，不掉层）
        public void OnEvent()
        {
            TryApplyTimeTrigger(consumeTickStacks: false);
        }

        /// <summary>周期跳：效果成功触发后再按 TickStackPolicy 掉层。</summary>
        void OnIntervalTick()
        {
            TryApplyTimeTrigger(consumeTickStacks: true);
        }

        bool TryApplyTimeTrigger(bool consumeTickStacks)
        {
            var stateCheckResult = true;
            if (TryGet(out BuffStateCheckComponent component))
            {
                stateCheckResult = component.CheckTargetState(OwnerEntity.Entity);
            }

            if (stateCheckResult && Setting.BuffType.HasFlag(BuffType.CDBuff))
            {
                if (_time == null) return false;
                stateCheckResult = _time.GetIsCanTrigger();
                if (stateCheckResult)
                    _time.RefreshCDTime();
            }

            if (stateCheckResult)
            {
                if (TryGet(out BuffModifyComponent modifyCom))
                    modifyCom.OnTriggerModify(OwnerEntity.Entity);
                if (consumeTickStacks)
                    ApplyTickStackPolicy();
                if (Setting.BuffType.HasFlag(BuffType.NumberBuff) && _frequency != null && _frequency.ConsumeStack())
                    CheckIsCanRemove();
            }

            return stateCheckResult;
        }

        void ApplyTickStackPolicy()
        {
            if (TickStackPolicy == TickStackPolicy.None || !IsCanStack)
                return;
            var stack = _attributes?.GetNumeric(AttributeType.BuffMaxStacks);
            if (stack == null)
                return;

            float current = stack.CurrentValue;
            if (current <= 0f)
            {
                CheckIsCanRemove();
                return;
            }

            float next = current;
            if (TickStackPolicy == TickStackPolicy.MinusOne)
                next = current - 1f;
            else if (TickStackPolicy == TickStackPolicy.MinusHalf)
                next = (int)(current * 0.5f);
            else if (TickStackPolicy == TickStackPolicy.Clear)
                next = 0f;

            stack.CurrentValue = next;
            if (stack.CurrentValue <= 0f)
                CheckIsCanRemove();
        }

        static TickStackPolicy ResolveTickStackPolicy(BuffDemoSetting setting)
        {
            var list = setting?.BuffModifyList;
            if (list == null || list.Count == 0)
                return TickStackPolicy.None;
            var mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return TickStackPolicy.None;
            for (int i = 0; i < list.Count; i++)
            {
                var mod = mgr.GetBuffModifySetting(list[i]);
                if (mod == null || mod.EffectModifyType != EffectModifyType.BuffHpDamage)
                    continue;
                int code = (int)mod.ParamFloat2;
                if (code == (int)TickStackPolicy.MinusOne
                    || code == (int)TickStackPolicy.MinusHalf
                    || code == (int)TickStackPolicy.Clear)
                    return (TickStackPolicy)code;
                return TickStackPolicy.None;
            }
            return TickStackPolicy.None;
        }

        //辅助方法

        //是否次数耗尽
        private bool GetIsNoTimes()
        {
            BuffFrequencyComponent dataComponent;
            if (Setting.BuffType.HasFlag(BuffType.NumberBuff) && TryGet<BuffFrequencyComponent>(out dataComponent))
            {
                return dataComponent.NumberTimes.CurrentValue <= 0;
            }
            return false;
        }

        bool GetIsNoStacks()
        {
            if (!IsCanStack)
                return false;
            var property = _attributes?.GetNumeric(AttributeType.BuffMaxStacks);
            return property != null && property.CurrentValue <= 0f;
        }

        //是否已过期
        private bool GetIsTimeOut()
        {
            BuffTimeComponent dataComponent;
            if (Setting.BuffType.HasFlag(BuffType.TimeBuff) && TryGet<BuffTimeComponent>(out dataComponent))
            {
                return dataComponent.State.IsFinished;
            }
            return false;
        }

        //是否移除触发器触发了
        private bool GetIsTriggerOut()
        {
            BuffTriggerComponent triggerComponent;
            if (Setting.BuffType.HasFlag(BuffType.RemoveTriggerBuff) && TryGet<BuffTriggerComponent>(out triggerComponent))
            {
                return triggerComponent.ShouldRemove;
            }
            return false;
        }

        public override void OnDestroy()
        {
            Enable = false;
            Caster = null;
            Setting = null;
            IsCanRemoveBuff = false;
            IsRemoving = false;
            PendingRemoveReason = BuffRemoveReason.Expired;
            TickStackPolicy = TickStackPolicy.None;
            ExpirePolicy = BuffExpirePolicy.None;
            BoundRunnerId = 0;
            BoundFormId = 0;
            HitsRemaining = 0;
            _lifetimeBound = false;
        }

        public override void OnReset()
        {
            OnDestroy();
        }
    }
}
