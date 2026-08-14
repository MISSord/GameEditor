using UnityEngine;
using XiaoCao;

namespace EGamePlay.Combat
{

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
        public bool IsCanStack => Setting.CanStack == 1; //是否能叠层

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

            // 时间处理组件：由 ETTimerManager 集中调度，不再每帧 Update
            if (Setting.BuffType.HasFlag(BuffType.TimeBuff) || Setting.BuffType.HasFlag(BuffType.CDBuff))
            {
                _time = AddComponent<BuffTimeComponent>();
                _time.Init(Setting.BaseDuration, Setting.IntervalTime, Setting.DelayTime, Setting.CDTime);
                _time.Events.OnTick = OnEvent;
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
            //    AddComponent<BuffAbilityTriggerComponent>(passiveConfig.TriggerActions);
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
            var tags = Setting?.BuffTag;
            if (_status == null || tags == null) return;
            for (int i = 0; i < tags.Count; i++)
                _status.TagContainer.AddTag(tags[i]);
        }

        protected override void OnDeactivate()
        {
            if (_status == null) _status = Parent?.GetComponent<StatusComponent>();
            var tags = Setting?.BuffTag;
            if (_status != null && tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                    _status.TagContainer.RemoveTag(tags[i]);
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
                    modifyCom.OnTriggerModify(actionExecution);
                //次数耗尽
                if (Setting.BuffType.HasFlag(BuffType.NumberBuff) && _frequency != null && _frequency.ConsumeStack())
                {
                    CheckIsCanRemove();
                }
            }
        }

        public void CheckIsCanRemove()
        {
            //没次数或者没时间或者移除触发点到了
            if (GetIsNoTimes() == true || GetIsTimeOut() == true || GetIsTriggerOut() == true)
            {
                //关闭组件，等外部回收（下一帧回收！！）
                DeactivateBuff();
                //这里有点bug，AI忘记搞这个了
                IsCanRemoveBuff = true;
            }
        }

        //由时间触发
        public void OnEvent()
        {
            // 这里是状态判断，状态判断是判断目标的状态是否满足条件，满足才能触发效果
            var stateCheckResult = true;
            if (TryGet(out BuffStateCheckComponent component))
            {
                stateCheckResult = component.CheckTargetState(this.OwnerEntity);
            }

            //如果当前Buff有CD设置，判断CD是否结束
            if(stateCheckResult && Setting.BuffType.HasFlag(BuffType.CDBuff))
            {
                if (_time == null) return;
                stateCheckResult = _time.GetIsCanTrigger();
                if(stateCheckResult == true)
                {
                    _time.RefreshCDTime();
                }
            }

            // 条件满足则触发效果
            if (stateCheckResult)
            {
                if (TryGet(out BuffModifyComponent modifyCom))
                    modifyCom.OnTriggerModify(this.OwnerEntity);
                //次数耗尽
                if (Setting.BuffType.HasFlag(BuffType.NumberBuff) && _frequency != null && _frequency.ConsumeStack())
                {
                    CheckIsCanRemove();
                }
            }
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
        }
    }
}
