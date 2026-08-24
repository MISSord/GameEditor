using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    public class AddStatusActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out AddStatusAction action)
        {
            if (!Enable)
            {
                action = null;
            }
            else
            {
                action = (AddStatusAction)CombatContext.Instance.AddAction<AddStatusAction>();
                action.Creator = OwnerEntity;
            }
            return Enable;
        }
    }

    /// <summary>
    /// 施加 Buff 行动
    /// </summary>
    public class AddStatusAction : Entity, IActionExecute
    {
        public TriggerContext TriggerContext { get; set; }

        /// <summary>释放者</summary>
        public CombatEntity Creator { get; set; }

        /// <summary>目标实体</summary>
        public Entity Target { get; set; }

        public void FinishAction()
        {
            Entity.Destroy(this);
        }

        // 前置处理（目前预留）
        private void PreProcess()
        {
        }

        /// <summary>
        /// 处理已有同 ID Buff 时的重复添加逻辑。
        /// </summary>
        private void HandleReApplyExistingBuff(Buff existingBuff, BuffDemoSetting config)
        {
            if (existingBuff == null || config == null)
            {
                return;
            }

            var rule = config.ReApplyRule;

            // 时间相关组件（可能为空：非时间型 Buff）
            existingBuff.TryGet(out BuffTimeComponent timeComponent);

            if (rule == BuffReApplyRule.AddDuration)
            {
                // 1. 叠加时间到当前 Buff 中，使用配置中的基础持续时间。
                if (timeComponent != null && config.BaseDuration > 0f)
                {
                    timeComponent.ExtendDuration(config.BaseDuration);
                }
            }
            else if (rule == BuffReApplyRule.RefreshDuration)
            {
                // 2. 刷新时间：使用配置中的基础持续时间作为刷新时间。
                if (timeComponent != null && config.BaseDuration > 0f)
                {
                    timeComponent.ResetDuration(config.BaseDuration);
                }
            }
            else if (rule == BuffReApplyRule.AddStackAndRefresh)
            {
                // 3. 叠加层数并刷新时间。
                if (existingBuff.IsCanStack)
                {
                    var attrs = existingBuff.GetComponent<BuffAttributesComponent>();
                    if (attrs != null)
                    {
                        var stackProperty = attrs.GetNumeric(AttributeType.BuffMaxStacks);
                        if (stackProperty != null)
                        {
                            // BuffProperty 内部会根据 MaxValue 做 Clamp。
                            stackProperty.CurrentValue = stackProperty.CurrentValue + 1f;
                        }
                    }
                }

                if (timeComponent != null && config.BaseDuration > 0f)
                {
                    timeComponent.ResetDuration(config.BaseDuration);
                }
            }
            else if (rule == BuffReApplyRule.Exclusive)
            {
                // 4. 互斥不叠加：已有 Buff 时什么都不做，直接忽略本次添加。
            }
            else
            {
                // 未配置或未知规则时，默认走刷新时间的行为，避免 Buff 彻底失效。
                if (timeComponent != null && config.BaseDuration > 0f)
                {
                    timeComponent.ResetDuration(config.BaseDuration);
                }
            }
        }

        /// <summary>
        /// 表化入口：直接按 BuffModifySetting 的参数施加 Buff。
        /// ParamInt1=BuffId，ParamString1=kv 列表（\"key=value\"），用于写入 Buff 运行时参数。
        /// </summary>
        public void ApplyAddStatusBySetting(int statusId, List<string> paramString1)
        {
            PreProcess();

            if (statusId <= 0 || Target == null || !Target.TryGet(out StatusComponent statusComp))
            {
                FinishAction();
                return;
            }

            var buffConfig = SkillSettingMgr.Instance.GetBuffDemoSetting(statusId);

            if (statusComp.HasBuffId(statusId))
            {
                var existingBuff = statusComp.GetBuffById(statusId);
                HandleReApplyExistingBuff(existingBuff, buffConfig);
                PostProcess();
                FinishAction();
                return;
            }

            Buff buffAbility = statusComp.AttachStatus(statusId);
            buffAbility.Caster = Creator;
            ProcessInputKVParams(buffAbility, paramString1);
            buffAbility.ActivateBuff();

            PostProcess();
            FinishAction();
        }

        // 后置处理
        private void PostProcess()
        {
            Creator.TriggerActionPoint(ActionPointType.PostGiveStatus, this);
            if (Target is CombatEntity target)
            {
                target.TriggerActionPoint(ActionPointType.PostReceiveStatus, this);
            }
        }

        // 处理输入的 KV 参数
        private void ProcessInputKVParams(Buff ability, Dictionary<string, string> Params)
        {
            foreach (var keyValue in Params)
            {
                // 是否是属性类型
                if (Enum.IsDefined(typeof(AttributeType), int.Parse(keyValue.Key)))
                {
                    ability.AddBuffAttribute((AttributeType)int.Parse(keyValue.Key), ModifyType.SetBase, null, int.Parse(keyValue.Value), true);
                }
            }
        }

        private void ProcessInputKVParams(Buff ability, List<string> paramPairs)
        {
            if (paramPairs == null || paramPairs.Count == 0) return;
            for (int i = 0; i < paramPairs.Count; i++)
            {
                var s = paramPairs[i];
                if (string.IsNullOrEmpty(s)) continue;
                int eq = s.IndexOf('=');
                if (eq <= 0 || eq >= s.Length - 1) continue;

                // key/value 都是 int（key 为 AttributeType 的 int 值）
                if (!int.TryParse(s.Substring(0, eq), out var keyInt)) continue;
                if (!int.TryParse(s.Substring(eq + 1), out var valInt)) continue;

                if (Enum.IsDefined(typeof(AttributeType), keyInt))
                {
                    ability.AddBuffAttribute((AttributeType)keyInt, ModifyType.SetBase, null, valInt, true);
                }
            }
        }
    }
}